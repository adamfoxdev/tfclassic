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
    public List<Sentry> Sentries { get; } = new();
    public List<Dispenser> Dispensers { get; } = new();
    public List<Teleporter> Teleporters { get; } = new();
    public List<Detpack> Detpacks { get; } = new();

    /// <summary>Every engineer-built structure currently in the world.</summary>
    public IEnumerable<Structure> Structures => Sentries.Cast<Structure>().Concat(Dispensers).Concat(Teleporters).Concat(Detpacks);
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
        p.Grenades[0] = p.Class.Frag;
        p.Grenades[1] = p.Class.Concussion;
        p.Detpacks = p.Class.Detpacks;
        p.InfectedBy = null;
        p.Primed = -1;
        p.ConcussTime = 0;
        p.DisguiseTeam = null;
        p.DisguiseTimer = 0;
        p.Feigning = false;
        p.FeignCooldown = 0;
        p.SlowTime = 0;
        p.Metal = p.Class.MaxMetal;
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
        UpdateSentries(dt);
        UpdateDispensers(dt);
        UpdateTeleporters(dt);
        UpdateDetpacks(dt);
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
        p.NoticeTimer = MathF.Max(0, p.NoticeTimer - dt);
        p.FireCooldown = MathF.Max(0, p.FireCooldown - dt);
        p.ResupplyCooldown = MathF.Max(0, p.ResupplyCooldown - dt);

        p.SlowTime = MathF.Max(0, p.SlowTime - dt);
        if (p.ConcussTime > 0)
        {
            // A concussed player's aim wanders: the crosshair stays put but shots land off to the side.
            p.ConcussTime = MathF.Max(0, p.ConcussTime - dt);
            float wobble = 0.09f * MathF.Min(1f, p.ConcussTime / 2f);
            p.Input.Yaw += wobble * MathF.Sin(Time * 9f);
            p.Input.Pitch += wobble * MathF.Cos(Time * 7f);
        }
        p.FeignCooldown = MathF.Max(0, p.FeignCooldown - dt);
        if (p.DisguiseTimer > 0) p.DisguiseTimer = MathF.Max(0, p.DisguiseTimer - dt);
        if (p.Feigning)
        {
            p.FeignTimer -= dt;
            if (p.FeignTimer <= 0) { p.Feigning = false; p.FeignCooldown = 1.5f; }
        }

        float speedScale = 1f;
        if (p.SniperCharge > 0) speedScale = 0.3f;
        else if (p.Input.Fire && p.Weapon.Id == WeaponId.AssaultCannon) speedScale = 0.45f;
        if (p.SlowTime > 0) speedScale *= 0.45f;
        if (MatchOver || p.Feigning) { p.Input.Forward = 0; p.Input.Right = 0; p.Input.Fire = false; p.Input.Jump = false; }

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

        if (p.IsInfected)
        {
            UpdateInfection(p, dt);
            if (!p.Alive) return;
        }

        if (p.ResupplyCooldown <= 0 && World.InZone(ZoneKind.Resupply, p.Center, p.Team))
            Resupply(p);

        HandleWeapons(p, dt);
        HandleGrenades(p, dt);
    }

    void Resupply(Player p)
    {
        bool changed = p.Health < p.Class.MaxHealth || p.Armor < p.Class.MaxArmor || p.Metal < p.Class.MaxMetal
                       || p.Grenades[0] < p.Class.Frag || p.Grenades[1] < p.Class.Concussion
                       || p.Detpacks < p.Class.Detpacks || p.IsInfected;
        for (int i = 1; i < p.Ammo.Length; i++)
            if (p.Ammo[i] < p.Class.MaxAmmo[i]) changed = true;
        if (!changed) return;
        p.Health = p.Class.MaxHealth;
        p.Armor = p.Class.MaxArmor;
        p.InfectedBy = null;                                   // the locker's disinfectant
        p.Metal = Math.Min(p.Class.MaxMetal, p.Metal + 20);   // lockers only top metal up a little
        p.Grenades[0] = p.Class.Frag;
        p.Grenades[1] = p.Class.Concussion;
        p.Detpacks = p.Class.Detpacks;
        for (int i = 0; i < p.Ammo.Length; i++) p.Ammo[i] = p.Class.MaxAmmo[i];
        p.ResupplyCooldown = 3f;
    }

    // ───────────────────────── weapons ─────────────────────────

    bool HasAmmo(Player p, WeaponDef w) => w.Ammo == AmmoType.None || p.Ammo[(int)w.Ammo] >= w.AmmoPerShot;

    void HandleWeapons(Player p, float dt)
    {
        var inp = p.Input;
        if (p.Class.Id == PlayerClassId.Spy)
        {
            if (inp.Feign) ToggleFeign(p);
            if (inp.DisguiseNext && !p.Feigning) CycleDisguise(p);
        }
        if (p.Class.Id == PlayerClassId.Engineer && inp.BuildDispenser) ToggleDispenser(p);
        if (p.Class.Id == PlayerClassId.Engineer && inp.BuildTeleporter) ToggleTeleporter(p);
        if (p.Class.Id == PlayerClassId.Demoman)
        {
            if (inp.DetpackFuseNext) p.DetpackFuseIndex = (p.DetpackFuseIndex + 1) % Detpack.Fuses.Length;
            if (inp.PlaceDetpack) PlaceDetpack(p);
        }
        if (p.Feigning) return;

        if (inp.SelectSlot >= 0 && inp.SelectSlot < p.Class.Slots.Length && inp.SelectSlot != p.Slot)
        {
            p.Slot = inp.SelectSlot;
            p.FireCooldown = MathF.Max(p.FireCooldown, 0.25f);
            p.SniperCharge = 0;
        }

        if (inp.AltFire && !p.PrevAlt)
        {
            DetonatePipes(p);
            if (p.Class.Id == PlayerClassId.Engineer) ToggleSentry(p);
        }
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
        BreakDisguise(p);
        if (w.Ammo != AmmoType.None) p.Ammo[(int)w.Ammo] -= w.AmmoPerShot;
        var eye = p.Eye;
        var fwd = p.Forward;

        switch (w.Mode)
        {
            case FireMode.Hitscan:
            case FireMode.Tranq:
                for (int i = 0; i < w.Pellets; i++)
                {
                    var dir = Spread(fwd, w.Spread);
                    var end = HitscanShot(p, p.Team, eye, dir, w.Range, w.Damage, 1.5f, w.Name, 20f, w.Mode == FireMode.Tranq ? 3f : 0f);
                    AddTracer(eye + p.Right * -6 + new Vector3(0, -6, 0) + dir * 14, end, p.Team);
                }
                break;

            case FireMode.Melee:
            case FireMode.Heal:
            case FireMode.Wrench:
            case FireMode.Backstab:
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
        var end = HitscanShot(p, p.Team, eye, dir, w.Range, dmg, 2f, w.Name, 60f);
        AddTracer(eye + dir * 16, end, p.Team);
    }

    void AddTracer(Vector3 a, Vector3 b, Team team) =>
        Effects.Add(new Effect { Kind = EffectKind.Tracer, A = a, B = b, Life = 0.08f, MaxLife = 0.08f, Team = team });

    /// <summary>Nearest hit of a segment against a hull; a segment starting inside counts as t = 0.</summary>
    static bool HitsHull(in Aabb hull, Vector3 from, Vector3 delta, out float t)
    {
        if (hull.Contains(from)) { t = 0; return true; }
        return Collision.RayAabb(from, delta, hull, out t, out _);
    }

    /// <summary>Fires one ray. Hits the nearest enemy player or structure in front of the first wall; returns the end point.</summary>
    Vector3 HitscanShot(Player? owner, Team team, Vector3 origin, Vector3 dir, float range, float damage,
        float headMultiplier, string weapon, float knock, float slowSeconds = 0f)
    {
        var delta = dir * range;
        float bestT = 1f;
        if (World.Raycast(origin, origin + delta, out float tw, out _)) bestT = tw;

        Player? victim = null;
        Structure? structure = null;
        foreach (var q in Players)
        {
            if (q == owner || !q.Alive || q.Team == team || q.Feigning) continue;
            if (HitsHull(q.Hull, origin, delta, out float t) && t < bestT) { bestT = t; victim = q; structure = null; }
        }
        foreach (var s in Structures)
        {
            if (s.Dead || s.Team == team) continue;
            if (HitsHull(s.Hull, origin, delta, out float t) && t < bestT) { bestT = t; structure = s; victim = null; }
        }

        var end = origin + delta * bestT;
        if (victim != null)
        {
            bool head = end.Y - victim.Position.Y > 58f;
            Damage(victim, owner, head ? damage * headMultiplier : damage,
                weapon + (head && headMultiplier >= 2f ? " (headshot)" : ""), dir * knock);
            if (slowSeconds > 0 && victim.Alive) victim.SlowTime = slowSeconds;
        }
        else if (structure != null)
        {
            DamageStructure(structure, owner, damage, weapon);
        }
        return end;
    }

    void MeleeAttack(Player p, WeaponDef w, Vector3 eye, Vector3 fwd)
    {
        var delta = fwd * w.Range;
        float bestT = 1f;
        if (World.Raycast(eye, eye + delta, out float tw, out _)) bestT = tw;

        Player? best = null;
        Structure? bestStructure = null;
        foreach (var q in Players)
        {
            if (q == p || !q.Alive || q.Feigning) continue;
            if (q.Team == p.Team && w.Mode != FireMode.Heal) continue;
            if (HitsHull(q.Hull.Expand(new Vector3(6)), eye, delta, out float t) && t < bestT) { bestT = t; best = q; bestStructure = null; }
        }
        foreach (var s in Structures)
        {
            if (s.Dead || (s.Team == p.Team && w.Mode != FireMode.Wrench)) continue;
            if (HitsHull(s.Hull.Expand(new Vector3(6)), eye, delta, out float t) && t < bestT) { bestT = t; bestStructure = s; best = null; }
        }

        if (bestStructure != null)
        {
            if (bestStructure.Team == p.Team) WrenchStructure(p, bestStructure);
            else if (w.Mode == FireMode.Backstab) SabotageStructure(p, bestStructure);
            else DamageStructure(bestStructure, p, w.Damage, w.Name);
            return;
        }
        if (best == null) return;

        if (best.Team == p.Team)
        {
            bool cured = best.IsInfected;
            if (cured)
            {
                best.InfectedBy = null;
                Notice(best, "A medic cured your infection");
                Notice(p, $"Cured {best.Name}'s infection");
            }
            if (best.Health < best.Class.MaxHealth || cured)
            {
                best.Health = MathF.Min(best.Class.MaxHealth, best.Health + 14);
                best.BurnTime = 0;
                Effects.Add(new Effect { Kind = EffectKind.Heal, A = best.Center, Life = 0.4f, MaxLife = 0.4f, Team = p.Team });
            }
        }
        else if (w.Mode == FireMode.Backstab)
        {
            // Behind the victim = the victim is facing away from us.
            var toAttacker = new Vector3(p.Position.X - best.Position.X, 0, p.Position.Z - best.Position.Z);
            var victimFacing = new Vector3(MathF.Sin(best.Yaw), 0, MathF.Cos(best.Yaw));
            bool behind = toAttacker.LengthSquared() < 1f
                          || Vector3.Dot(victimFacing, Vector3.Normalize(toAttacker)) < -0.3f;
            if (behind) Damage(best, p, 200f, "Knife (backstab)", fwd * 90f);
            else Damage(best, p, w.Damage, w.Name, fwd * 90f);
        }
        else
        {
            Damage(best, p, w.Damage, w.Name, fwd * 90f);
            if (w.Mode == FireMode.Heal) Infect(best, p);
        }
    }

    void FlameAttack(Player p, WeaponDef w, Vector3 eye, Vector3 fwd)
    {
        var muzzle = eye + fwd * 20 + new Vector3(0, -8, 0);
        Effects.Add(new Effect { Kind = EffectKind.Flame, A = muzzle, B = muzzle + fwd * w.Range, Life = 0.15f, MaxLife = 0.15f, Team = p.Team });
        foreach (var s in Structures.ToArray())
        {
            if (s.Dead || s.Team == p.Team) continue;
            var sv = s.Hull.Center - eye;
            float sd = sv.Length();
            if (sd > w.Range || sd < 1f || Vector3.Dot(sv / sd, fwd) < 0.88f) continue;
            if (!World.LineOfSight(eye, s.Hull.Center)) continue;
            DamageStructure(s, p, w.Damage * 0.5f, w.Name);
        }
        foreach (var q in Players)
        {
            if (q == p || !q.Alive || q.Team == p.Team || q.Feigning) continue;
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
        // Index loop: an explosion can kill a player who drops a live grenade, which appends to the list.
        for (int i = 0, n = Projectiles.Count; i < n; i++)
        {
            var pr = Projectiles[i];
            if (pr.Dead) continue;
            pr.Age += dt;

            switch (pr.Kind)
            {
                case ProjectileKind.Rocket:
                    UpdateRocket(pr, dt);
                    break;
                case ProjectileKind.Grenade:
                case ProjectileKind.Pipe:
                case ProjectileKind.HandGrenade:
                case ProjectileKind.Concussion:
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
            if (!q.Alive || q.Team == pr.Team || q.Feigning) continue;
            var hull = q.Hull;
            float t;
            if (hull.Contains(from)) t = 0;
            else if (!Collision.RayAabb(from, delta, hull, out t, out _)) continue;
            if (t < hitT) { hitT = t; best = q; }
        }
        return best;
    }

    /// <summary>Whether the segment strikes an enemy structure before maxT (t is the fraction along it).</summary>
    bool SegmentHitsStructure(Projectile pr, Vector3 from, Vector3 delta, float maxT, out float hitT)
    {
        hitT = maxT;
        bool hit = false;
        foreach (var s in Structures)
        {
            if (s.Dead || s.Team == pr.Team) continue;
            if (HitsHull(s.Hull, from, delta, out float t) && t < hitT) { hitT = t; hit = true; }
        }
        return hit;
    }

    void UpdateRocket(Projectile pr, float dt)
    {
        var delta = pr.Velocity * dt;
        float tWorld = 1f;
        bool worldHit = World.Raycast(pr.Position, pr.Position + delta, out float tw, out _);
        if (worldHit) tWorld = tw;

        var victim = SegmentHitsEnemy(pr, pr.Position, delta, tWorld, out float tPlayer);
        float tNear = victim != null ? tPlayer : tWorld;
        bool structureHit = SegmentHitsStructure(pr, pr.Position, delta, tNear, out float tStructure);
        if (worldHit || victim != null || structureHit)
        {
            float t = structureHit ? tStructure : tNear;
            var dir = Vector3.Normalize(pr.Velocity);
            pr.Dead = true;
            Explode(pr.Position + delta * t - dir * 4f, pr.Owner, pr.Damage, pr.Splash, pr.Label);
            return;
        }

        pr.Position += delta;
        if (pr.Age > 6f) pr.Dead = true;
    }

    void UpdateBouncer(Projectile pr, float dt)
    {
        if (pr.Age >= pr.Fuse)
        {
            pr.Dead = true;
            DetonateTimed(pr);
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
            float tNear = victim != null ? tp : tWorld;
            bool structureHit = SegmentHitsStructure(pr, pr.Position, delta, tNear, out float tStructure);
            if (victim != null || structureHit)
            {
                pr.Dead = true;
                Explode(pr.Position + delta * (structureHit ? tStructure : tp), pr.Owner, pr.Damage, pr.Splash, "Grenade");
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
            if (knock.LengthSquared() > 1100f * 1100f) knock = Vector3.Normalize(knock) * 1100f;
            Damage(q, owner, self ? full * 0.5f : full, weapon, knock);
        }

        foreach (var s in Structures.ToArray())
        {
            if (s.Dead || s.Team == owner.Team) continue;
            float d = s.Hull.DistanceTo(pos);
            if (d >= radius || !World.LineOfSight(pos, s.Hull.Center)) continue;
            DamageStructure(s, owner, damage * (1f - d / radius), weapon);
        }
    }

    // ───────────────────────── medic infection ─────────────────────────

    public const float InfectionInterval = 2f, InfectionDamage = 3f, InfectionSpreadRadius = 130f, InfectionSpreadChance = 0.4f;

    /// <summary>A medikit hit leaves the enemy infected (Medics are immune, and an existing infection isn't overwritten).</summary>
    void Infect(Player victim, Player medic)
    {
        if (!victim.Alive || victim.IsInfected || victim.Class.Id == PlayerClassId.Medic) return;
        if (victim.Team == medic.Team) return;
        victim.InfectedBy = medic;
        victim.InfectionTick = InfectionInterval;
        Notice(victim, "You are infected! Find a medic or a resupply locker");
        Events.Add(new GameEvent(Time, $"{medic.Name} infected {victim.Name}", medic.Team));
    }

    /// <summary>The disease drains health (armor doesn't help) and hops to teammates standing close by.</summary>
    void UpdateInfection(Player p, float dt)
    {
        var source = p.InfectedBy!;
        if (source.Team == p.Team) { p.InfectedBy = null; return; }   // the infecting medic changed sides

        p.InfectionTick -= dt;
        if (p.InfectionTick > 0) return;
        p.InfectionTick = InfectionInterval;

        Effects.Add(new Effect { Kind = EffectKind.Heal, A = p.Center + new Vector3(0, 30, 0), Life = 0.4f, MaxLife = 0.4f, Team = p.Team });
        foreach (var q in Players)
        {
            if (q == p || !q.Alive || q.Team != p.Team || q.IsInfected) continue;
            if (q.Class.Id == PlayerClassId.Medic) continue;
            float dx = q.Position.X - p.Position.X, dz = q.Position.Z - p.Position.Z;
            if (dx * dx + dz * dz > InfectionSpreadRadius * InfectionSpreadRadius || MathF.Abs(q.Position.Y - p.Position.Y) > 60f) continue;
            if (!World.LineOfSight(p.Center, q.Center)) continue;
            if (Rng.NextDouble() < InfectionSpreadChance)
            {
                q.InfectedBy = source;
                q.InfectionTick = InfectionInterval;
                Notice(q, "You caught an infection! Find a medic or a resupply locker");
            }
        }

        Damage(p, source, InfectionDamage, "Infection", Vector3.Zero, ignoreArmor: true);
    }

    // ───────────────────────── detpacks ─────────────────────────

    public Detpack? DetpackOf(Player p) => Detpacks.FirstOrDefault(d => d.Owner == p && !d.Dead);

    /// <summary>Demoman's detpack key: set one in front of you; pressed again while it is still arming, pick it back up.</summary>
    void PlaceDetpack(Player p)
    {
        var existing = DetpackOf(p);
        if (existing != null)
        {
            if (existing.Building)
            {
                existing.Dead = true;
                p.Detpacks++;
                Notice(p, "Detpack picked up");
            }
            else
            {
                Notice(p, $"Detpack armed: {MathF.Ceiling(existing.Fuse)}s left");
            }
            return;
        }
        if (p.Detpacks <= 0) { Notice(p, "No detpacks left"); return; }
        if (!FindBuildSpot(p, Detpack.HullHalf, out var spot)) return;

        p.Detpacks--;
        float fuse = Detpack.Fuses[p.DetpackFuseIndex];
        Detpacks.Add(new Detpack { Owner = p, Team = p.Team, Position = spot, Yaw = p.Yaw, Fuse = fuse });
        Notice(p, $"Setting detpack ({fuse:0}s fuse)...");
    }

    void Disarm(Detpack pack, Player? by)
    {
        if (pack.Dead) return;
        pack.Dead = true;
        Effects.Add(new Effect { Kind = EffectKind.Heal, A = pack.Hull.Center, Life = 0.4f, MaxLife = 0.4f, Team = by?.Team ?? pack.Team });
        if (by != null)
        {
            by.Frags++;
            Events.Add(new GameEvent(Time, $"{by.Name} disarmed {pack.Owner.Name}'s detpack", by.Team));
            Notice(pack.Owner, "Your detpack was disarmed!");
        }
    }

    void UpdateDetpacks(float dt)
    {
        foreach (var d in Detpacks)
        {
            if (d.Dead) continue;
            if (d.Building) { d.BuildTimer -= dt; continue; }

            d.Fuse -= dt;
            if (d.Fuse <= 0)
            {
                d.Dead = true;
                Explode(d.Hull.Center, d.Owner, Detpack.Damage, Detpack.Radius, "Detpack");
                continue;
            }

            // Any enemy who stays beside it long enough defuses it.
            Player? near = null;
            foreach (var q in Players)
            {
                if (!q.Alive || q.Team == d.Team || q.Feigning) continue;
                float dx = q.Position.X - d.Position.X, dz = q.Position.Z - d.Position.Z;
                if (dx * dx + dz * dz < Detpack.DisarmReach * Detpack.DisarmReach && MathF.Abs(q.Position.Y - d.Position.Y) < 60f)
                {
                    near = q;
                    break;
                }
            }
            if (near != null)
            {
                d.Disarmer = near;
                d.DisarmProgress += dt;
                if (d.DisarmProgress >= Detpack.DisarmTime) Disarm(d, near);
            }
            else
            {
                d.DisarmProgress = MathF.Max(0f, d.DisarmProgress - dt * 2f);
            }
        }
        Detpacks.RemoveAll(d => d.Dead);
    }

    // ───────────────────────── hand grenades ─────────────────────────

    const float GrenadeFuse = 3f;
    const float FragDamage = 110f, FragRadius = 150f, ConcussionRadius = 260f;

    /// <summary>Hold the key to prime (and cook) a grenade, release to throw it. Cook too long and it goes off in your hand.</summary>
    void HandleGrenades(Player p, float dt)
    {
        var inp = p.Input;
        bool g1 = inp.Grenade1, g2 = inp.Grenade2;

        if (p.Primed < 0)
        {
            if (!p.Feigning && !MatchOver)
            {
                if (g1 && !p.PrevGrenade1 && p.Grenades[0] > 0) Prime(p, GrenadeKind.Frag);
                else if (g2 && !p.PrevGrenade2 && p.Grenades[1] > 0) Prime(p, GrenadeKind.Concussion);
            }
        }
        else
        {
            p.PrimedTimer -= dt;
            bool held = p.Primed == (int)GrenadeKind.Frag ? g1 : g2;
            if (p.PrimedTimer <= 0) ThrowGrenade(p, inHand: true);
            else if (!held) ThrowGrenade(p);
        }
        p.PrevGrenade1 = g1;
        p.PrevGrenade2 = g2;
    }

    void Prime(Player p, GrenadeKind kind)
    {
        p.Primed = (int)kind;
        p.PrimedTimer = GrenadeFuse;
        p.Grenades[(int)kind]--;
        BreakDisguise(p);
    }

    /// <summary>Throws (or drops, or detonates in the hand) the grenade the player is cooking.</summary>
    void ThrowGrenade(Player p, bool dropped = false, bool inHand = false)
    {
        var kind = (GrenadeKind)p.Primed;
        float fuse = MathF.Max(0.05f, p.PrimedTimer);
        p.Primed = -1;

        var pr = new Projectile
        {
            Kind = kind == GrenadeKind.Frag ? ProjectileKind.HandGrenade : ProjectileKind.Concussion,
            Owner = p,
            Team = p.Team,
            Damage = kind == GrenadeKind.Frag ? FragDamage : 0f,
            Splash = kind == GrenadeKind.Frag ? FragRadius : ConcussionRadius,
            Fuse = fuse,
        };

        if (inHand)
        {
            pr.Position = p.Eye + new Vector3(0, -12, 0);
            pr.Fuse = 0;
            DetonateTimed(pr);
            return;
        }
        if (dropped)
        {
            pr.Position = p.Center;
            pr.Velocity = new Vector3(p.Velocity.X * 0.3f, 60f, p.Velocity.Z * 0.3f);
        }
        else
        {
            var fwd = p.Forward;
            pr.Position = p.Eye + fwd * 16f + new Vector3(0, -8, 0);
            pr.Velocity = fwd * 520f + new Vector3(0, 110f, 0) + p.Velocity * 0.5f;
            BreakDisguise(p);
        }
        Projectiles.Add(pr);
    }

    /// <summary>A timed projectile's fuse ran out (any bouncing kind).</summary>
    void DetonateTimed(Projectile pr)
    {
        switch (pr.Kind)
        {
            case ProjectileKind.Concussion:
                ExplodeConcussion(pr.Position, pr.Owner, pr.Splash);
                break;
            case ProjectileKind.HandGrenade:
                Explode(pr.Position, pr.Owner, pr.Damage, pr.Splash, "Hand Grenade");
                break;
            default:
                Explode(pr.Position, pr.Owner, pr.Damage, pr.Splash, "Grenade");
                break;
        }
    }

    /// <summary>No damage: shoves everyone nearby (teammates and the thrower too) and leaves them dizzy.</summary>
    void ExplodeConcussion(Vector3 pos, Player owner, float radius)
    {
        Effects.Add(new Effect { Kind = EffectKind.Concussion, A = pos, Life = 0.5f, MaxLife = 0.5f, Radius = radius });

        foreach (var q in Players)
        {
            if (!q.Alive) continue;
            float d = q.Hull.DistanceTo(pos);
            if (d >= radius) continue;
            if (!World.LineOfSight(pos, q.Center) && !World.LineOfSight(pos, q.Position + new Vector3(0, 8, 0))) continue;
            if (q != owner && q.SpawnProtect > 0) continue;

            float falloff = 1f - d / radius;
            var away = q.Center - pos;
            away = away.LengthSquared() < 1e-4f ? Vector3.UnitY : Vector3.Normalize(away);
            var push = away * 880f * falloff + new Vector3(0, 160f * falloff, 0);
            q.Velocity += push;
            if (push.Y > 0) q.OnGround = false;
            q.ConcussTime = MathF.Max(q.ConcussTime, 8f * falloff);
        }
    }

    // ───────────────────────── spy ─────────────────────────

    public void StartDisguise(Player p, Team team, PlayerClassId cls)
    {
        p.DisguiseTeam = team;
        p.DisguiseClass = cls;
        p.DisguiseTimer = 2f;
    }

    public void BreakDisguise(Player p)
    {
        if (!p.DisguiseTeam.HasValue) return;
        if (p.IsDisguised) Notice(p, "Disguise blown!");
        p.DisguiseTeam = null;
        p.DisguiseTimer = 0;
    }

    /// <summary>Disguise as the next enemy class (cycling through every class but Spy).</summary>
    void CycleDisguise(Player p)
    {
        int cls = p.DisguiseTeam.HasValue ? (int)p.DisguiseClass : (int)PlayerClassId.Spy;
        do cls = (cls + 1) % Classes.All.Length; while (cls == (int)PlayerClassId.Spy);
        StartDisguise(p, p.Team.Opposite(), (PlayerClassId)cls);
        Notice(p, $"Disguising as {p.DisguiseTeam} {Classes.Get((PlayerClassId)cls).Name}...");
    }

    void ToggleFeign(Player p)
    {
        if (p.Feigning)
        {
            p.Feigning = false;
            p.FeignCooldown = 1.5f;
            return;
        }
        if (p.FeignCooldown > 0 || !p.OnGround) return;
        if (p.CarryingFlag != null) { Notice(p, "Can't feign death while carrying the flag"); return; }

        BreakDisguise(p);
        p.Feigning = true;
        p.FeignTimer = 10f;
        p.Velocity = new Vector3(0, p.Velocity.Y, 0);
        Events.Add(new GameEvent(Time, $"{p.Name} died", p.Team.Opposite()));   // the fake corpse announces itself too
        Effects.Add(new Effect { Kind = EffectKind.Gib, A = p.Center, Life = 0.6f, MaxLife = 0.6f, Team = p.Team });
    }

    // ───────────────────────── engineer structures ─────────────────────────

    void DamageStructure(Structure s, Player? attacker, float amount, string weapon)
    {
        if (s.Dead) return;
        if (attacker != null && attacker.Team == s.Team) return;
        s.Health -= amount;
        if (s.Health > 0) return;

        s.Dead = true;
        Effects.Add(new Effect { Kind = EffectKind.Explosion, A = s.Hull.Center, Life = 0.4f, MaxLife = 0.4f, Radius = 90f });
        if (attacker != null)
        {
            attacker.Frags++;
            Events.Add(new GameEvent(Time, $"{attacker.Name} [{weapon}] destroyed {s.Owner.Name}'s {s.Label}", attacker.Team));
        }
    }

    void SabotageStructure(Player spy, Structure s)
    {
        if (s is Detpack pack)
        {
            Disarm(pack, spy);
            return;
        }
        if (s.Building || s.Sabotaged) return;
        s.SabotageTimer = 4f;
        s.Saboteur = spy;
        if (s is Sentry sentry) sentry.Target = null;
        Notice(spy, $"{Capitalize(s.Label)} sabotaged!");
        Notice(s.Owner, $"Your {s.Label} is being sabotaged! (hit it with the wrench)");
    }

    static string Capitalize(string s) => char.ToUpperInvariant(s[0]) + s[1..];

    /// <summary>Counts a sabotaged structure down; returns true while it is disabled (and blows it up when time runs out).</summary>
    bool TickSabotage(Structure s, float dt)
    {
        if (s.SabotageTimer <= 0) return false;
        s.SabotageTimer -= dt;
        if (s.SabotageTimer <= 0) DamageStructure(s, s.Saboteur, 99999f, "Sabotage");
        return true;
    }

    void WrenchStructure(Player p, Structure s)
    {
        switch (s)
        {
            case Sentry sentry: WrenchSentry(p, sentry); break;
            case Dispenser dispenser: WrenchDispenser(p, dispenser); break;
            case Teleporter tele: WrenchTeleporter(p, tele); break;
        }
    }

    // ───────────────────────── teleporters ─────────────────────────

    public Teleporter? TeleporterOf(Player p, TeleporterRole role) =>
        Teleporters.FirstOrDefault(t => t.Owner == p && t.Role == role && !t.Dead);

    /// <summary>
    /// Engineer's teleporter key: builds the entrance first, then the exit; with both standing it
    /// demolishes the pair.
    /// </summary>
    void ToggleTeleporter(Player p)
    {
        var entrance = TeleporterOf(p, TeleporterRole.Entrance);
        var exit = TeleporterOf(p, TeleporterRole.Exit);
        if (entrance != null && exit != null)
        {
            entrance.Dead = exit.Dead = true;
            p.Metal = Math.Min(p.Class.MaxMetal, p.Metal + 80);
            foreach (var t in new[] { entrance, exit })
                Effects.Add(new Effect { Kind = EffectKind.Explosion, A = t.Hull.Center, Life = 0.25f, MaxLife = 0.25f, Radius = 40f });
            Notice(p, "Teleporters demolished (+80 metal)");
            return;
        }

        var role = entrance == null ? TeleporterRole.Entrance : TeleporterRole.Exit;
        string name = role == TeleporterRole.Entrance ? "entrance" : "exit";
        if (p.Metal < Teleporter.BuildCost) { Notice(p, $"Need {Teleporter.BuildCost} metal to build a teleporter {name}"); return; }
        if (!FindBuildSpot(p, Teleporter.HullHalf, out var spot)) return;

        p.Metal -= Teleporter.BuildCost;
        Teleporters.Add(new Teleporter { Owner = p, Team = p.Team, Position = spot, Yaw = p.Yaw, Role = role });
        Notice(p, role == TeleporterRole.Entrance
            ? "Building teleporter entrance... (T again, elsewhere, for the exit)"
            : "Building teleporter exit...");
    }

    void WrenchTeleporter(Player p, Teleporter t)
    {
        if (t.Sabotaged)
        {
            t.SabotageTimer = 0;
            Notice(p, "Sabotage removed");
            return;
        }
        if (t.Building) return;
        if (t.Health < Teleporter.MaxHealth && p.Metal >= 10)
        {
            t.Health = MathF.Min(Teleporter.MaxHealth, t.Health + 40);
            p.Metal -= 10;
            Effects.Add(new Effect { Kind = EffectKind.Heal, A = t.Hull.Center, Life = 0.4f, MaxLife = 0.4f, Team = t.Team });
        }
    }

    void UpdateTeleporters(float dt)
    {
        foreach (var t in Teleporters)
        {
            if (t.Dead) continue;
            if (t.Building) { t.BuildTimer -= dt; continue; }
            t.CooldownTimer = MathF.Max(0, t.CooldownTimer - dt);
            TickSabotage(t, dt);
        }

        foreach (var entrance in Teleporters)
        {
            if (entrance.Role != TeleporterRole.Entrance || !entrance.Active || entrance.CooldownTimer > 0) continue;
            var exit = Teleporters.FirstOrDefault(x => x.Owner == entrance.Owner && x.Role == TeleporterRole.Exit && x.Active);
            if (exit == null) continue;

            foreach (var q in Players)
            {
                if (!q.Alive || q.Team != entrance.Team || q.Feigning) continue;
                float dx = q.Position.X - entrance.Position.X, dz = q.Position.Z - entrance.Position.Z;
                if (dx * dx + dz * dz > 40f * 40f || MathF.Abs(q.Position.Y - entrance.Position.Y) > 24f) continue;
                Teleport(q, entrance, exit);
                break;   // one traveller per cooldown
            }
        }
        Teleporters.RemoveAll(t => t.Dead);
    }

    void Teleport(Player q, Teleporter entrance, Teleporter exit)
    {
        // Anyone standing on the exit pad is telefragged.
        foreach (var enemy in Players.ToArray())
        {
            if (!enemy.Alive || enemy.Team == q.Team) continue;
            float dx = enemy.Position.X - exit.Position.X, dz = enemy.Position.Z - exit.Position.Z;
            if (dx * dx + dz * dz < 32f * 32f && MathF.Abs(enemy.Position.Y - exit.Position.Y) < 60f)
                Kill(enemy, q, "Telefrag");
        }

        Effects.Add(new Effect { Kind = EffectKind.Heal, A = entrance.Hull.Center + new Vector3(0, 20, 0), Life = 0.5f, MaxLife = 0.5f, Team = q.Team });
        q.Position = exit.Position + new Vector3(0, 0.5f, 0);
        q.Velocity = Vector3.Zero;
        q.OnGround = true;
        q.TeleportCount++;
        entrance.CooldownTimer = exit.CooldownTimer = Teleporter.Cooldown;
        Effects.Add(new Effect { Kind = EffectKind.Heal, A = exit.Hull.Center + new Vector3(0, 20, 0), Life = 0.5f, MaxLife = 0.5f, Team = q.Team });
    }

    // ───────────────────────── sentry guns ─────────────────────────

    void Notice(Player p, string text)
    {
        p.Notice = text;
        p.NoticeTimer = 2.5f;
    }

    public Sentry? SentryOf(Player p) => Sentries.FirstOrDefault(s => s.Owner == p && !s.Dead);

    /// <summary>Alt-fire for Engineers: build a sentry in front of you, or demolish the one you have.</summary>
    void ToggleSentry(Player p)
    {
        var existing = SentryOf(p);
        if (existing != null)
        {
            existing.Dead = true;
            p.Metal = Math.Min(p.Class.MaxMetal, p.Metal + 50);
            Effects.Add(new Effect { Kind = EffectKind.Explosion, A = existing.Hull.Center, Life = 0.25f, MaxLife = 0.25f, Radius = 40f });
            Notice(p, "Sentry demolished (+50 metal)");
            return;
        }
        if (p.Metal < Sentry.BuildCost) { Notice(p, $"Need {Sentry.BuildCost} metal to build a sentry"); return; }
        if (!FindBuildSpot(p, Sentry.HullHalf, out var spot)) return;

        p.Metal -= Sentry.BuildCost;
        Sentries.Add(new Sentry { Owner = p, Team = p.Team, Position = spot, Yaw = p.Yaw });
        Notice(p, "Building sentry...");
    }

    /// <summary>Finds floor about 56 units in front of the engineer with room for a structure of the given size.</summary>
    bool FindBuildSpot(Player p, Vector3 half, out Vector3 spot)
    {
        spot = default;
        if (!p.OnGround) { Notice(p, "Stand on the ground to build"); return false; }

        var flat = new Vector3(MathF.Sin(p.Yaw), 0, MathF.Cos(p.Yaw));
        spot = p.Position + flat * 56f;
        var from = spot + new Vector3(0, 30, 0);
        if (World.Raycast(p.Position + new Vector3(0, 30, 0), from, out _, out _) ||
            !World.Raycast(from, from + new Vector3(0, -60, 0), out float t, out var n) || n.Y < 0.7f)
        {
            Notice(p, "No room to build here");
            return false;
        }
        spot.Y = from.Y - 60 * t;

        var center = spot + new Vector3(0, half.Y + 0.5f, 0);
        var box = Aabb.FromCenter(center, half);
        if (World.Overlaps(center, half)
            || World.InZone(ZoneKind.Water, spot + new Vector3(0, 10, 0))
            || Structures.Any(s => !s.Dead && s.Hull.Intersects(box)))
        {
            Notice(p, "No room to build here");
            return false;
        }
        return true;
    }

    public Dispenser? DispenserOf(Player p) => Dispensers.FirstOrDefault(d => d.Owner == p && !d.Dead);

    /// <summary>Engineer's build-dispenser key: build one in front of you, or demolish the one you have.</summary>
    void ToggleDispenser(Player p)
    {
        var existing = DispenserOf(p);
        if (existing != null)
        {
            existing.Dead = true;
            p.Metal = Math.Min(p.Class.MaxMetal, p.Metal + 40);
            Effects.Add(new Effect { Kind = EffectKind.Explosion, A = existing.Hull.Center, Life = 0.25f, MaxLife = 0.25f, Radius = 40f });
            Notice(p, "Dispenser demolished (+40 metal)");
            return;
        }
        if (p.Metal < Dispenser.BuildCost) { Notice(p, $"Need {Dispenser.BuildCost} metal to build a dispenser"); return; }
        if (!FindBuildSpot(p, Dispenser.HullHalf, out var spot)) return;

        p.Metal -= Dispenser.BuildCost;
        Dispensers.Add(new Dispenser { Owner = p, Team = p.Team, Position = spot, Yaw = p.Yaw + MathF.PI });   // screen faces the engineer
        Notice(p, "Building dispenser...");
    }

    void WrenchDispenser(Player p, Dispenser d)
    {
        if (d.Sabotaged)
        {
            d.SabotageTimer = 0;
            Notice(p, "Sabotage removed");
            return;
        }
        if (d.Building) return;
        if ((d.Health < Dispenser.MaxHealth || d.Store < Dispenser.MaxStore) && p.Metal >= 10)
        {
            d.Health = MathF.Min(Dispenser.MaxHealth, d.Health + 40);
            d.Store = Math.Min(Dispenser.MaxStore, d.Store + 100);
            p.Metal -= 10;
            Effects.Add(new Effect { Kind = EffectKind.Heal, A = d.Hull.Center, Life = 0.4f, MaxLife = 0.4f, Team = d.Team });
        }
        else if (d.Health >= Dispenser.MaxHealth && d.Store >= Dispenser.MaxStore)
        {
            Notice(p, "Dispenser is full");
        }
    }

    void UpdateDispensers(float dt)
    {
        foreach (var d in Dispensers)
        {
            if (d.Dead) continue;
            if (d.Building) { d.BuildTimer -= dt; continue; }
            if (TickSabotage(d, dt)) continue;

            d.UseTimer -= dt;
            if (d.UseTimer > 0 || d.Store <= 0) continue;
            d.UseTimer = 1f;

            var center = d.Hull.Center;
            foreach (var q in Players)
            {
                if (d.Store <= 0) break;
                if (!q.Alive || q.Team != d.Team) continue;
                if (q.Hull.DistanceTo(center) > Dispenser.Reach || !World.LineOfSight(center, q.Center)) continue;
                if (Restock(q)) d.Store = Math.Max(0, d.Store - 15);
            }
        }
        Dispensers.RemoveAll(d => d.Dead);
    }

    static readonly int[] DispenserAmmo = { 0, 20, 30, 10, 30 };

    /// <summary>Tops a player up a little (ammo, armor, metal). Returns whether anything was given.</summary>
    bool Restock(Player q)
    {
        bool gave = false;
        for (int i = 1; i < q.Ammo.Length; i++)
        {
            int room = q.Class.MaxAmmo[i] - q.Ammo[i];
            if (room <= 0) continue;
            q.Ammo[i] += Math.Min(room, DispenserAmmo[i]);
            gave = true;
        }
        if (q.Armor < q.Class.MaxArmor) { q.Armor = MathF.Min(q.Class.MaxArmor, q.Armor + 20); gave = true; }
        if (q.Metal < q.Class.MaxMetal) { q.Metal = Math.Min(q.Class.MaxMetal, q.Metal + 20); gave = true; }
        if (gave) Effects.Add(new Effect { Kind = EffectKind.Heal, A = q.Center + new Vector3(0, 10, 0), Life = 0.3f, MaxLife = 0.3f, Team = q.Team });
        return gave;
    }

    void WrenchSentry(Player p, Sentry s)
    {
        if (s.Sabotaged)
        {
            s.SabotageTimer = 0;
            Notice(p, "Sabotage removed");
            return;
        }
        if (s.Building) return;
        if (s.Level < 3 && p.Metal >= Sentry.UpgradeCost)
        {
            s.Level++;
            s.Health = s.MaxHealth;
            s.Ammo = s.MaxAmmo;
            if (s.Level == 3) s.Rockets = 20;
            p.Metal -= Sentry.UpgradeCost;
            Notice(p, $"Sentry upgraded to level {s.Level}");
            Effects.Add(new Effect { Kind = EffectKind.Heal, A = s.Hull.Center, Life = 0.5f, MaxLife = 0.5f, Team = s.Team });
        }
        else if ((s.Health < s.MaxHealth || s.Ammo < s.MaxAmmo || (s.Level == 3 && s.Rockets < 20)) && p.Metal >= 10)
        {
            s.Health = MathF.Min(s.MaxHealth, s.Health + 40);
            s.Ammo = Math.Min(s.MaxAmmo, s.Ammo + 30);
            if (s.Level == 3) s.Rockets = Math.Min(20, s.Rockets + 4);
            p.Metal -= 10;
            Effects.Add(new Effect { Kind = EffectKind.Heal, A = s.Hull.Center, Life = 0.4f, MaxLife = 0.4f, Team = s.Team });
        }
        else if (s.Level < 3)
        {
            Notice(p, $"Need {Sentry.UpgradeCost} metal to upgrade");
        }
    }

    void UpdateSentries(float dt)
    {
        foreach (var s in Sentries)
        {
            if (s.Dead) continue;
            if (s.Building) { s.BuildTimer -= dt; continue; }
            if (TickSabotage(s, dt)) { s.Target = null; continue; }

            s.FireCooldown -= dt;
            s.RocketCooldown -= dt;
            s.RetargetTimer -= dt;
            var muzzle = s.Muzzle;

            if (s.RetargetTimer <= 0 || s.Target == null || !s.Target.IsTargetableBy(s.Team))
            {
                s.RetargetTimer = 0.2f;
                s.Target = null;
                float bestD = s.Range;
                foreach (var q in Players)
                {
                    if (!q.IsTargetableBy(s.Team)) continue;
                    float d = Vector3.Distance(muzzle, q.Center);
                    if (d < bestD && World.LineOfSight(muzzle, q.Center)) { bestD = d; s.Target = q; }
                }
            }

            if (s.Target == null)
            {
                s.Yaw += 0.7f * dt;   // idle sweep
                continue;
            }

            var to = s.Target.Center - muzzle;
            float desired = MathF.Atan2(to.X, to.Z);
            s.Yaw = Angles.Approach(s.Yaw, desired, s.TurnRate * dt);
            if (MathF.Abs(Angles.Diff(s.Yaw, desired)) > 0.12f) continue;

            var dir = Vector3.Normalize(to);
            if (s.FireCooldown <= 0 && s.Ammo > 0)
            {
                s.Ammo--;
                s.FireCooldown = s.Cooldown;
                var shot = Spread(dir, 0.05f);
                var end = HitscanShot(s.Owner, s.Team, muzzle, shot, s.Range + 100f, 8f, 1f, "Sentry Gun", 8f);
                AddTracer(muzzle + shot * 20f, end, s.Team);
            }
            if (s.Level == 3 && s.Rockets > 0 && s.RocketCooldown <= 0)
            {
                s.Rockets--;
                s.RocketCooldown = 3f;
                Projectiles.Add(new Projectile
                {
                    Kind = ProjectileKind.Rocket, Owner = s.Owner, Team = s.Team,
                    Position = muzzle + dir * 22f, Velocity = dir * 900f,
                    Damage = 80, Splash = 150, Label = "Sentry Rocket",
                });
            }
        }
        Sentries.RemoveAll(s => s.Dead);
    }

    // ───────────────────────── damage / death ─────────────────────────

    public void Damage(Player victim, Player? attacker, float amount, string weapon, Vector3 knock, bool ignoreArmor = false)
    {
        if (!victim.Alive || victim.SpawnProtect > 0) return;
        if (attacker != null && attacker != victim && attacker.Team == victim.Team) return;

        BreakDisguise(victim);
        float absorbed = ignoreArmor ? 0f : MathF.Min(victim.Armor, amount * 0.6f);
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
        victim.Feigning = false;
        victim.InfectedBy = null;
        victim.DisguiseTeam = null;
        if (victim.Primed >= 0) ThrowGrenade(victim, dropped: true);

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
        Sentries.Clear();
        Dispensers.Clear();
        Teleporters.Clear();
        Detpacks.Clear();
        foreach (var p in Players) Respawn(p);
    }

    void UpdateEffects(float dt)
    {
        foreach (var e in Effects) e.Life -= dt;
        Effects.RemoveAll(e => e.Life <= 0);
    }
}
