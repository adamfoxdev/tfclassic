using System.Numerics;

namespace TFClassic.Core;

/// <summary>The full match simulation: players, projectiles, flags, scoring. No rendering or I/O.</summary>
public sealed class Game
{
    public const int ScoreLimit = 5;
    const float PipeLimit = 6;

    public GameMap Map { get; }
    public World World => Map.World;
    public List<Player> Players { get; } = new();
    public List<Projectile> Projectiles { get; } = new();
    public List<Effect> Effects { get; } = new();
    public List<GameEvent> Events { get; } = new();
    public Flag[] Flags { get; }
    public int[] TeamScore { get; } = new int[2];
    public float Time { get; private set; }
    public bool MatchOver { get; private set; }
    public Team? Winner { get; private set; }
    public Random Rng { get; }

    /// <summary>How many times a bot gave up on being stuck and killed itself (diagnostic).</summary>
    public int BotStuckResets { get; internal set; }

    readonly List<BotBrain> bots = new();
    internal IReadOnlyList<BotBrain> Bots => bots;
    float matchOverTimer;
    int nextId = 1;

    static readonly string[] BotNames =
    {
        "Grunt", "Nailer", "Hawk", "Rocketeer", "Vex", "Kilo", "Mortar", "Spike", "Flare", "Doc",
        "Bishop", "Tank", "Ghost", "Boomer", "Rook", "Zulu", "Havoc", "Ember", "Quill", "Brick",
    };

    public Game(GameMap map, int seed = 1)
    {
        Map = map;
        Rng = new Random(seed);
        Flags = new[]
        {
            new Flag { Team = Team.Red, Home = map.FlagHome[0], Position = map.FlagHome[0] },
            new Flag { Team = Team.Blue, Home = map.FlagHome[1], Position = map.FlagHome[1] },
        };
    }

    // ───────────────────────── players ─────────────────────────

    public Player AddPlayer(string name, Team team, PlayerClassId cls, bool bot = false)
    {
        var p = new Player { Id = nextId++, Name = name, Team = team, IsBot = bot, PendingClass = cls };
        Players.Add(p);
        Respawn(p);
        return p;
    }

    public Player AddBot(Team team, PlayerClassId? cls = null)
    {
        var c = cls ?? (PlayerClassId)Rng.Next(Classes.All.Length);
        var p = AddPlayer(BotNames[(nextId - 1) % BotNames.Length], team, c, bot: true);
        bots.Add(new BotBrain(this, p, Rng.Next()));
        return p;
    }

    /// <summary>Queues a class change; applies immediately when standing in your own resupply room.</summary>
    public void ChangeClass(Player p, PlayerClassId cls)
    {
        p.PendingClass = cls;
        if (p.Alive && World.InZone(ZoneKind.Resupply, p.Center, p.Team)) Respawn(p);
    }

    public void Respawn(Player p)
    {
        p.Class = Classes.Get(p.PendingClass);
        var spawns = Map.Spawns[(int)p.Team];
        var spawn = spawns[Rng.Next(spawns.Count)];
        for (int tries = 0; tries < 6; tries++)
        {
            var s = spawns[Rng.Next(spawns.Count)];
            bool crowded = Players.Any(o => o != p && o.Alive && Vector3.DistanceSquared(o.Position, s.Position) < 48 * 48);
            if (!crowded) { spawn = s; break; }
        }

        p.Position = spawn.Position;
        p.Velocity = Vector3.Zero;
        p.Yaw = spawn.Yaw;
        p.Pitch = 0;
        p.OnGround = true;
        p.Health = p.Class.MaxHealth;
        p.Armor = p.Class.StartArmor;
        for (int i = 0; i < p.Ammo.Length; i++) p.Ammo[i] = p.Class.MaxAmmo[i];
        p.Slot = p.Class.DefaultSlot;
        p.Alive = true;
        p.SpawnProtect = 2f;
        p.FireCooldown = 0.3f;
        p.SniperCharge = 0;
        p.BurnTime = 0;
        p.ResupplyCooldown = 0;
        p.CarryingFlag = null;
        p.SpawnCount++;
        p.Input = new PlayerInput { SelectSlot = -1, Yaw = spawn.Yaw };
    }

    // ───────────────────────── tick ─────────────────────────

    public void Tick(float dt)
    {
        Time += dt;

        if (MatchOver)
        {
            matchOverTimer -= dt;
            if (matchOverTimer <= 0) ResetMatch();
        }

        foreach (var b in bots) b.Think(dt);
        foreach (var p in Players) UpdatePlayer(p, dt);
        UpdateProjectiles(dt);
        UpdateFlags(dt);
        UpdateEffects(dt);
        Events.RemoveAll(e => Time - e.Time > 8f);
    }

    void UpdatePlayer(Player p, float dt)
    {
        if (!p.Alive)
        {
            p.RespawnTimer -= dt;
            if (p.RespawnTimer <= 0 && !MatchOver) Respawn(p);
            return;
        }

        p.SpawnProtect = MathF.Max(0, p.SpawnProtect - dt);
        p.FireCooldown = MathF.Max(0, p.FireCooldown - dt);
        p.ResupplyCooldown = MathF.Max(0, p.ResupplyCooldown - dt);

        float speedScale = 1f;
        if (p.SniperCharge > 0) speedScale = 0.3f;
        else if (p.Input.Fire && p.Weapon.Id == WeaponId.AssaultCannon) speedScale = 0.45f;
        if (MatchOver) { p.Input.Forward = 0; p.Input.Right = 0; p.Input.Fire = false; }

        Movement.Simulate(World, p, speedScale, dt);

        if (p.Position.Y < -400) { Kill(p, null, "the void"); return; }

        if (p.BurnTime > 0)
        {
            p.BurnTime -= dt;
            p.BurnTick -= dt;
            if (World.InZone(ZoneKind.Water, p.Center)) p.BurnTime = 0;
            if (p.BurnTick <= 0)
            {
                p.BurnTick = 0.5f;
                Damage(p, p.BurnOwner, 4, "flames", Vector3.Zero);
                if (!p.Alive) return;
            }
        }

        if (p.ResupplyCooldown <= 0 && World.InZone(ZoneKind.Resupply, p.Center, p.Team))
            Resupply(p);

        HandleWeapons(p, dt);
    }

    void Resupply(Player p)
    {
        bool changed = p.Health < p.Class.MaxHealth || p.Armor < p.Class.MaxArmor;
        for (int i = 1; i < p.Ammo.Length; i++)
            if (p.Ammo[i] < p.Class.MaxAmmo[i]) changed = true;
        if (!changed) return;
        p.Health = p.Class.MaxHealth;
        p.Armor = p.Class.MaxArmor;
        for (int i = 0; i < p.Ammo.Length; i++) p.Ammo[i] = p.Class.MaxAmmo[i];
        p.ResupplyCooldown = 3f;
    }

    // ───────────────────────── weapons ─────────────────────────

    bool HasAmmo(Player p, WeaponDef w) => w.Ammo == AmmoType.None || p.Ammo[(int)w.Ammo] >= w.AmmoPerShot;

    void HandleWeapons(Player p, float dt)
    {
        var inp = p.Input;
        if (inp.SelectSlot >= 0 && inp.SelectSlot < p.Class.Slots.Length && inp.SelectSlot != p.Slot)
        {
            p.Slot = inp.SelectSlot;
            p.FireCooldown = MathF.Max(p.FireCooldown, 0.25f);
            p.SniperCharge = 0;
        }

        if (inp.AltFire && !p.PrevAlt) DetonatePipes(p);
        p.PrevAlt = inp.AltFire;

        var w = p.Weapon;
        if (w.Mode == FireMode.SniperCharge)
        {
            if (inp.Fire && p.FireCooldown <= 0 && HasAmmo(p, w))
                p.SniperCharge = MathF.Min(2f, p.SniperCharge + dt);
            else if (!inp.Fire && p.SniperCharge > 0)
            {
                FireSniper(p, w);
                p.SniperCharge = 0;
                p.FireCooldown = w.Cooldown;
            }
            return;
        }

        if (inp.Fire && p.FireCooldown <= 0 && HasAmmo(p, w))
        {
            Fire(p, w);
            p.FireCooldown = w.Cooldown;
        }
    }

    static Vector3 Perp(Vector3 dir)
    {
        var r = Vector3.Cross(dir, Vector3.UnitY);
        return r.LengthSquared() < 1e-6f ? Vector3.UnitX : Vector3.Normalize(r);
    }

    Vector3 Spread(Vector3 dir, float spread)
    {
        if (spread <= 0) return dir;
        var r = Perp(dir);
        var u = Vector3.Cross(r, dir);
        float a = (float)(Rng.NextDouble() * 2 - 1) * spread, b = (float)(Rng.NextDouble() * 2 - 1) * spread;
        return Vector3.Normalize(dir + r * a + u * b);
    }

    void Fire(Player p, WeaponDef w)
    {
        if (w.Ammo != AmmoType.None) p.Ammo[(int)w.Ammo] -= w.AmmoPerShot;
        var eye = p.Eye;
        var fwd = p.Forward;

        switch (w.Mode)
        {
            case FireMode.Hitscan:
                for (int i = 0; i < w.Pellets; i++)
                {
                    var dir = Spread(fwd, w.Spread);
                    var victim = TraceHitscan(p, eye, dir, w.Range, out var end, out bool head);
                    AddTracer(eye + p.Right * -6 + new Vector3(0, -6, 0) + dir * 14, end, p.Team);
                    if (victim != null)
                        Damage(victim, p, head ? w.Damage * 1.5f : w.Damage, w.Name, dir * 20f);
                }
                break;

            case FireMode.Melee:
            case FireMode.Heal:
                MeleeAttack(p, w, eye, fwd);
                break;

            case FireMode.Rocket:
                SpawnProjectile(p, ProjectileKind.Rocket, w, fwd);
                break;
            case FireMode.Grenade:
                SpawnProjectile(p, ProjectileKind.Grenade, w, fwd);
                break;
            case FireMode.Pipe:
                SpawnProjectile(p, ProjectileKind.Pipe, w, fwd);
                break;

            case FireMode.Flame:
                FlameAttack(p, w, eye, fwd);
                break;
        }
    }

    void FireSniper(Player p, WeaponDef w)
    {
        p.Ammo[(int)w.Ammo] -= w.AmmoPerShot;
        var eye = p.Eye;
        var dir = p.Forward;
        float dmg = 50f + 225f * (p.SniperCharge / 2f);
        var victim = TraceHitscan(p, eye, dir, w.Range, out var end, out bool head);
        AddTracer(eye + dir * 16, end, p.Team);
        if (victim != null)
            Damage(victim, p, head ? dmg * 2f : dmg, w.Name + (head ? " (headshot)" : ""), dir * 60f);
    }

    void AddTracer(Vector3 a, Vector3 b, Team team) =>
        Effects.Add(new Effect { Kind = EffectKind.Tracer, A = a, B = b, Life = 0.08f, MaxLife = 0.08f, Team = team });

    Player? TraceHitscan(Player shooter, Vector3 origin, Vector3 dir, float range, out Vector3 end, out bool head)
    {
        var delta = dir * range;
        float bestT = 1f;
        if (World.Raycast(origin, origin + delta, out float tw, out _)) bestT = tw;

        Player? best = null;
        foreach (var q in Players)
        {
            if (q == shooter || !q.Alive || q.Team == shooter.Team) continue;
            var hull = q.Hull;
            float t;
            if (hull.Contains(origin)) t = 0;
            else if (!Collision.RayAabb(origin, delta, hull, out t, out _)) continue;
            if (t < bestT) { bestT = t; best = q; }
        }

        end = origin + delta * bestT;
        head = best != null && end.Y - best.Position.Y > 58f;
        return best;
    }

    void MeleeAttack(Player p, WeaponDef w, Vector3 eye, Vector3 fwd)
    {
        var delta = fwd * w.Range;
        float bestT = 1f;
        if (World.Raycast(eye, eye + delta, out float tw, out _)) bestT = tw;

        Player? best = null;
        foreach (var q in Players)
        {
            if (q == p || !q.Alive) continue;
            if (q.Team == p.Team && w.Mode != FireMode.Heal) continue;
            var hull = q.Hull.Expand(new Vector3(6));
            float t;
            if (hull.Contains(eye)) t = 0;
            else if (!Collision.RayAabb(eye, delta, hull, out t, out _)) continue;
            if (t < bestT) { bestT = t; best = q; }
        }
        if (best == null) return;

        if (best.Team == p.Team)
        {
            if (best.Health < best.Class.MaxHealth)
            {
                best.Health = MathF.Min(best.Class.MaxHealth, best.Health + 14);
                best.BurnTime = 0;
                Effects.Add(new Effect { Kind = EffectKind.Heal, A = best.Center, Life = 0.4f, MaxLife = 0.4f, Team = p.Team });
            }
        }
        else
        {
            Damage(best, p, w.Damage, w.Name, fwd * 90f);
        }
    }

    void FlameAttack(Player p, WeaponDef w, Vector3 eye, Vector3 fwd)
    {
        var muzzle = eye + fwd * 20 + new Vector3(0, -8, 0);
        Effects.Add(new Effect { Kind = EffectKind.Flame, A = muzzle, B = muzzle + fwd * w.Range, Life = 0.15f, MaxLife = 0.15f, Team = p.Team });
        foreach (var q in Players)
        {
            if (q == p || !q.Alive || q.Team == p.Team) continue;
            var v = q.Center - eye;
            float d = v.Length();
            if (d > w.Range || d < 1f) continue;
            if (Vector3.Dot(v / d, fwd) < 0.88f) continue;
            if (!World.LineOfSight(eye, q.Center)) continue;
            q.BurnTime = 4f;
            q.BurnOwner = p;
            Damage(q, p, w.Damage, w.Name, fwd * 6f);
        }
    }

    void SpawnProjectile(Player p, ProjectileKind kind, WeaponDef w, Vector3 fwd)
    {
        if (kind == ProjectileKind.Pipe)
        {
            var mine = Projectiles.Where(x => x.Owner == p && x.Kind == ProjectileKind.Pipe && !x.Dead).ToList();
            if (mine.Count >= PipeLimit) mine[0].Dead = true;
        }

        Projectiles.Add(new Projectile
        {
            Kind = kind,
            Owner = p,
            Team = p.Team,
            Position = p.Eye + fwd * 22f + p.Right * 6f + new Vector3(0, -6, 0),
            Velocity = fwd * w.ProjectileSpeed,
            Damage = w.Damage,
            Splash = w.Splash,
            Fuse = kind == ProjectileKind.Grenade ? 2.5f : float.MaxValue,
        });
    }

    void DetonatePipes(Player p)
    {
        foreach (var pr in Projectiles)
        {
            if (pr.Owner != p || pr.Kind != ProjectileKind.Pipe || pr.Dead) continue;
            pr.Dead = true;
            Explode(pr.Position, p, pr.Damage, pr.Splash, "Pipebomb");
        }
    }

    // ───────────────────────── projectiles ─────────────────────────

    const float GrenadeGravity = 700f;

    void UpdateProjectiles(float dt)
    {
        foreach (var pr in Projectiles)
        {
            if (pr.Dead) continue;
            pr.Age += dt;

            switch (pr.Kind)
            {
                case ProjectileKind.Rocket:
                    UpdateRocket(pr, dt);
                    break;
                case ProjectileKind.Grenade:
                case ProjectileKind.Pipe:
                    UpdateBouncer(pr, dt);
                    break;
            }
        }
        Projectiles.RemoveAll(p => p.Dead);
    }

    /// <summary>First enemy of the projectile's team hit by the segment, if any (t is the fraction along it).</summary>
    Player? SegmentHitsEnemy(Projectile pr, Vector3 from, Vector3 delta, float maxT, out float hitT)
    {
        Player? best = null;
        hitT = maxT;
        foreach (var q in Players)
        {
            if (!q.Alive || q.Team == pr.Team) continue;
            var hull = q.Hull;
            float t;
            if (hull.Contains(from)) t = 0;
            else if (!Collision.RayAabb(from, delta, hull, out t, out _)) continue;
            if (t < hitT) { hitT = t; best = q; }
        }
        return best;
    }

    void UpdateRocket(Projectile pr, float dt)
    {
        var delta = pr.Velocity * dt;
        float tWorld = 1f;
        bool worldHit = World.Raycast(pr.Position, pr.Position + delta, out float tw, out _);
        if (worldHit) tWorld = tw;

        var victim = SegmentHitsEnemy(pr, pr.Position, delta, tWorld, out float tPlayer);
        if (worldHit || victim != null)
        {
            float t = victim != null ? tPlayer : tWorld;
            var dir = Vector3.Normalize(pr.Velocity);
            pr.Dead = true;
            Explode(pr.Position + delta * t - dir * 4f, pr.Owner, pr.Damage, pr.Splash, "Rocket");
            return;
        }

        pr.Position += delta;
        if (pr.Age > 6f) pr.Dead = true;
    }

    void UpdateBouncer(Projectile pr, float dt)
    {
        if (pr.Kind == ProjectileKind.Grenade && pr.Age >= pr.Fuse)
        {
            pr.Dead = true;
            Explode(pr.Position, pr.Owner, pr.Damage, pr.Splash, "Grenade");
            return;
        }
        if (pr.Stuck) return;

        pr.Velocity.Y -= GrenadeGravity * dt;
        var delta = pr.Velocity * dt;
        float tWorld = 1f;
        Vector3 n = Vector3.Zero;
        bool worldHit = World.Raycast(pr.Position, pr.Position + delta, out float tw, out Vector3 wn);
        if (worldHit) { tWorld = tw; n = wn; }

        if (pr.Kind == ProjectileKind.Grenade)
        {
            var victim = SegmentHitsEnemy(pr, pr.Position, delta, tWorld, out float tp);
            if (victim != null)
            {
                pr.Dead = true;
                Explode(pr.Position + delta * tp, pr.Owner, pr.Damage, pr.Splash, "Grenade");
                return;
            }
        }

        if (worldHit)
        {
            pr.Position += delta * tWorld + n * 1f;
            pr.Velocity -= 2f * Vector3.Dot(pr.Velocity, n) * n;
            pr.Velocity *= 0.5f;
            if (n.Y > 0.7f && pr.Velocity.LengthSquared() < 70f * 70f)
            {
                pr.Stuck = true;
                pr.Velocity = Vector3.Zero;
            }
        }
        else
        {
            pr.Position += delta;
        }
    }

    void Explode(Vector3 pos, Player owner, float damage, float radius, string weapon)
    {
        Effects.Add(new Effect { Kind = EffectKind.Explosion, A = pos, Life = 0.35f, MaxLife = 0.35f, Radius = radius });

        foreach (var q in Players.ToArray())
        {
            if (!q.Alive) continue;
            float d = q.Hull.DistanceTo(pos);
            if (d >= radius) continue;
            if (!World.LineOfSight(pos, q.Center) && !World.LineOfSight(pos, q.Position + new Vector3(0, 8, 0))) continue;

            float full = damage * (1f - d / radius);
            var away = q.Center - pos;
            away = away.LengthSquared() < 1e-4f ? Vector3.UnitY : Vector3.Normalize(away);
            bool self = q == owner;

            if (!self && q.Team == owner.Team) continue;
            if (q.SpawnProtect > 0) continue;

            var knock = away * full * 6f;
            Damage(q, owner, self ? full * 0.5f : full, weapon, knock);
        }
    }

    // ───────────────────────── damage / death ─────────────────────────

    public void Damage(Player victim, Player? attacker, float amount, string weapon, Vector3 knock)
    {
        if (!victim.Alive || victim.SpawnProtect > 0) return;
        if (attacker != null && attacker != victim && attacker.Team == victim.Team) return;

        float absorbed = MathF.Min(victim.Armor, amount * 0.6f);
        victim.Armor -= absorbed;
        victim.Health -= amount - absorbed;

        victim.Velocity += knock;
        if (knock.Y > 0) victim.OnGround = false;

        if (victim.Health <= 0) Kill(victim, attacker, weapon);
    }

    public void Kill(Player victim, Player? killer, string weapon)
    {
        if (!victim.Alive) return;
        victim.Alive = false;
        victim.Deaths++;
        victim.RespawnTimer = victim.IsBot ? 4f : 5f;
        victim.BurnTime = 0;
        victim.SniperCharge = 0;

        if (killer != null && killer != victim) killer.Frags++;
        else victim.Frags--;

        string text = killer == null || killer == victim
            ? $"{victim.Name} died ({weapon})"
            : $"{killer.Name} [{weapon}] {victim.Name}";
        Events.Add(new GameEvent(Time, text, killer?.Team ?? victim.Team.Opposite()));
        Effects.Add(new Effect { Kind = EffectKind.Gib, A = victim.Center, Life = 0.6f, MaxLife = 0.6f, Team = victim.Team });

        foreach (var pr in Projectiles)
            if (pr.Owner == victim && pr.Kind == ProjectileKind.Pipe) pr.Dead = true;

        if (victim.CarryingFlag != null) DropFlag(victim.CarryingFlag, victim.Position);
    }

    // ───────────────────────── capture the flag ─────────────────────────

    void DropFlag(Flag flag, Vector3 at)
    {
        flag.Carrier!.CarryingFlag = null;
        flag.Carrier = null;
        flag.DropTimer = 30f;

        var floor = at + new Vector3(0, 24, 0);
        flag.Position = World.Raycast(floor, floor + new Vector3(0, -400, 0), out float t, out _)
            ? floor + new Vector3(0, -400 * t, 0)
            : Flags[(int)flag.Team].Home;
        Events.Add(new GameEvent(Time, $"{flag.Team} flag was dropped", flag.Team.Opposite()));
    }

    void ReturnFlag(Flag flag)
    {
        flag.Carrier = null;
        flag.DropTimer = 0;
        flag.Position = flag.Home;
    }

    void UpdateFlags(float dt)
    {
        foreach (var flag in Flags)
        {
            if (flag.Carrier != null)
            {
                flag.Position = flag.Carrier.Position;
                continue;
            }
            if (flag.DropTimer > 0)
            {
                flag.DropTimer -= dt;
                if (flag.DropTimer <= 0)
                {
                    ReturnFlag(flag);
                    Events.Add(new GameEvent(Time, $"{flag.Team} flag returned", flag.Team));
                }
            }
        }

        if (MatchOver) return;

        foreach (var p in Players)
        {
            if (!p.Alive) continue;
            var own = Flags[(int)p.Team];
            var enemy = Flags[(int)p.Team.Opposite()];

            if (enemy.Carrier == null && p.CarryingFlag == null && Near(p, enemy.Position))
            {
                enemy.Carrier = p;
                enemy.DropTimer = 0;
                p.CarryingFlag = enemy;
                Events.Add(new GameEvent(Time, $"{p.Name} took the {enemy.Team} flag", p.Team));
            }

            if (own.Dropped && Near(p, own.Position))
            {
                ReturnFlag(own);
                Events.Add(new GameEvent(Time, $"{p.Name} returned the {own.Team} flag", p.Team));
            }

            if (p.CarryingFlag != null && own.AtHome && Near(p, own.Home))
            {
                var captured = p.CarryingFlag;
                p.CarryingFlag = null;
                ReturnFlag(captured);
                p.Captures++;
                p.Frags += 10;
                TeamScore[(int)p.Team]++;
                Events.Add(new GameEvent(Time, $"{p.Name} captured the {captured.Team} flag!", p.Team));
                if (TeamScore[(int)p.Team] >= ScoreLimit)
                {
                    MatchOver = true;
                    Winner = p.Team;
                    matchOverTimer = 8f;
                    Events.Add(new GameEvent(Time, $"{p.Team} team wins!", p.Team));
                }
            }
        }
    }

    static bool Near(Player p, Vector3 point)
    {
        float dx = p.Position.X - point.X, dz = p.Position.Z - point.Z;
        return dx * dx + dz * dz < 48f * 48f && MathF.Abs(p.Position.Y - point.Y) < 72f;
    }

    public void ResetMatch()
    {
        TeamScore[0] = TeamScore[1] = 0;
        MatchOver = false;
        Winner = null;
        foreach (var f in Flags)
        {
            f.Carrier = null;
            ReturnFlag(f);
        }
        Projectiles.Clear();
        foreach (var p in Players) Respawn(p);
    }

    void UpdateEffects(float dt)
    {
        foreach (var e in Effects) e.Life -= dt;
        Effects.RemoveAll(e => e.Life <= 0);
    }
}
