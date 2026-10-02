using System.Numerics;

namespace TFClassic.Core;

/// <summary>
/// Drives a bot-controlled player by producing a PlayerInput each tick:
/// waypoint navigation toward the current objective plus simple target selection and aiming.
/// </summary>
public sealed class BotBrain
{
    readonly Game game;
    readonly Player me;
    readonly Random rng;

    List<int> path = new();
    int pathIndex;
    int pathGoal = -1;
    float repathTimer;

    bool defender;
    int defendNode;
    int lastSpawnCount = -1;

    Player? target;
    Sentry? targetSentry;
    float targetTimer;
    float reactTimer;
    Vector3 aimNoise;
    float noiseTimer;
    readonly float accuracy;

    Vector3 lastPos;
    float stuckTimer;
    float sampleTimer;
    float strafeTimer;
    float strafeDir;
    float jumpTimer;
    float totalStuck;

    float aimYaw, aimPitch;
    bool holdingStill;
    float redisguise;
    float buildTimer;
    int buildFails;
    bool triedBuild;
    bool dispenserTried, dispenserStarted;
    Player? healTarget;
    float healScanTimer;
    float detpackRetreat;
    bool grenadeThrowing;
    int grenadeSlot;
    float grenadeHold, grenadeCooldown;
    bool teleCommit, useTele;
    float teleTimer;
    int teleCountBefore = -1, lastTeleportCount;
    float dispenserTimer;
    float yawOffset;

    public BotBrain(Game game, Player me, int seed)
    {
        this.game = game;
        this.me = me;
        rng = new Random(seed);
        accuracy = 0.025f + (float)rng.NextDouble() * 0.04f;
    }

    void OnSpawn()
    {
        lastSpawnCount = me.SpawnCount;
        dispenserStarted = false;
        dispenserTried = false;
        useTele = rng.NextDouble() < 0.7;
        detpackRetreat = 0;
        grenadeThrowing = false;
        grenadeCooldown = 2f + (float)rng.NextDouble() * 5f;
        lastTeleportCount = me.TeleportCount;
        path.Clear();
        pathGoal = -1;
        target = null;
        targetSentry = null;
        stuckTimer = 0;
        aimYaw = me.Yaw;
        aimPitch = 0;
        lastPos = me.Position;

        double defendChance = me.Class.Id switch
        {
            PlayerClassId.HeavyWeapons => 0.6,
            PlayerClassId.Sniper => 0.7,
            PlayerClassId.Demoman => 0.4,
            PlayerClassId.Soldier => 0.35,
            PlayerClassId.Medic => 0.4,
            PlayerClassId.Pyro => 0.5,
            _ => 0.1,
        };
        // Keep roughly a quarter of each team home; fill the remaining slots by class preference.
        int teamBots = game.Bots.Count(b => b.me.Team == me.Team);
        int defenders = game.Bots.Count(b => b != this && b.me.Team == me.Team && b.defender);
        int wanted = Math.Max(1, teamBots / 4);
        defender = defenders < wanted && rng.NextDouble() < Math.Max(defendChance, 0.5);
        if (me.Class.Id == PlayerClassId.Spy) { defender = false; redisguise = 0.5f; }
        if (me.Class.Id == PlayerClassId.Engineer) defender = true;   // engineers dig in and tend their sentry
        string[] spots = { "flagdoor", "hall", "door" };
        string side = me.Team == Team.Red ? "R_" : "B_";
        defendNode = game.Map.Nav.Find(side + spots[rng.Next(spots.Length)]);
    }

    public void Think(float dt)
    {
        if (!me.Alive)
        {
            me.Input = new PlayerInput { SelectSlot = -1 };
            lastSpawnCount = -1;
            return;
        }
        if (lastSpawnCount != me.SpawnCount) OnSpawn();

        var input = new PlayerInput { SelectSlot = -1 };
        holdingStill = false;

        UpdateTarget(dt);
        Vector3 moveDir = Navigate(dt, out bool wantMove);
        if (me.Class.Id == PlayerClassId.Spy) SpyTick(dt, ref moveDir, ref wantMove);
        if (me.Class.Id == PlayerClassId.Medic) MedicTick(dt, ref moveDir, ref wantMove);

        // Aim: at the target if we have one, otherwise where we're walking.
        float desiredYaw = me.Yaw, desiredPitch = 0f;
        bool engaging = HasTarget;
        float distToTarget = 0f;
        WeaponDef weapon = me.Weapon;

        if (engaging)
        {
            distToTarget = Vector3.Distance(me.Eye, TargetCenter);
            int slot = ChooseSlot(distToTarget);
            if (slot != me.Slot) input.SelectSlot = slot;
            weapon = Weapons.Get(me.Class.Slots[slot]);

            noiseTimer -= dt;
            if (noiseTimer <= 0)
            {
                noiseTimer = 0.3f;
                aimNoise = new Vector3(Rand(), Rand(), Rand()) * accuracy * 1.8f * distToTarget;
            }
            var aimPoint = AimPoint(weapon, distToTarget) + aimNoise;
            var d = aimPoint - me.Eye;
            desiredYaw = MathF.Atan2(d.X, d.Z);
            float horiz = MathF.Sqrt(d.X * d.X + d.Z * d.Z);
            desiredPitch = MathF.Atan2(d.Y, horiz);
            if (weapon.Mode == FireMode.Grenade || weapon.Mode == FireMode.Pipe)
                desiredPitch += LobAngle(horiz, weapon.ProjectileSpeed);
        }
        else if (wantMove && moveDir != Vector3.Zero)
        {
            desiredYaw = MathF.Atan2(moveDir.X, moveDir.Z);
        }

        float turnRate = engaging ? 7f : 4f;
        aimYaw = ApproachAngle(aimYaw, desiredYaw, turnRate * dt);
        aimPitch = ApproachAngle(aimPitch, desiredPitch, turnRate * dt);
        input.Yaw = aimYaw;
        input.Pitch = aimPitch;

        // Movement relative to the current view.
        if (wantMove && moveDir != Vector3.Zero)
        {
            var f = new Vector3(MathF.Sin(aimYaw), 0, MathF.Cos(aimYaw));
            var r = new Vector3(-MathF.Cos(aimYaw), 0, MathF.Sin(aimYaw));
            input.Forward = Vector3.Dot(moveDir, f);
            input.Right = Vector3.Dot(moveDir, r);
        }

        // Shooting.
        if (engaging)
        {
            reactTimer -= dt;
            float yawErr = MathF.Abs(AngleDiff(aimYaw, desiredYaw));
            float pitchErr = MathF.Abs(aimPitch - desiredPitch);
            float tolerance = 0.08f + 40f / MathF.Max(distToTarget, 100f) * 0.1f;
            bool aimed = yawErr < tolerance && pitchErr < tolerance;
            bool inRange = weapon.Mode switch
            {
                FireMode.Flame => distToTarget < weapon.Range,
                FireMode.Melee or FireMode.Heal or FireMode.Wrench or FireMode.Backstab => distToTarget < weapon.Range + 24,
                FireMode.Rocket or FireMode.Grenade or FireMode.Pipe => distToTarget < 1600,
                _ => distToTarget < 2400,
            };

            if (weapon.Mode == FireMode.SniperCharge)
            {
                // Hold still and charge a shot, then release.
                if (aimed && reactTimer <= 0 && inRange)
                {
                    input.Forward = 0;
                    input.Right = 0;
                    input.Fire = me.SniperCharge < 1.1f;
                    holdingStill = true;
                }
            }
            else if (aimed && reactTimer <= 0 && inRange)
            {
                input.Fire = true;
            }

            // Fight while moving: strafe a bit when we aren't following a path.
            if (!wantMove && weapon.Mode != FireMode.SniperCharge)
            {
                strafeTimer -= dt;
                if (strafeTimer <= 0)
                {
                    strafeTimer = 0.6f + (float)rng.NextDouble();
                    strafeDir = rng.Next(2) == 0 ? -1f : 1f;
                }
                input.Right = strafeDir;
            }
        }

        GrenadeTick(dt, engaging, distToTarget, ref input);
        if (me.Class.Id == PlayerClassId.Medic) MedicAct(ref input);
        DetpackTick(dt, engaging, distToTarget, ref input);
        if (me.Class.Id == PlayerClassId.Engineer) EngineerTick(dt, wantMove, engaging, ref input);
        UpdateStuck(dt, wantMove && !holdingStill, ref input);
        me.Input = input;
    }

    /// <summary>
    /// Lobs a cooked frag grenade at a visible target at mid range: prime, hold for roughly the flight time
    /// so it goes off near landing, release.
    /// </summary>
    void GrenadeTick(float dt, bool engaging, float dist, ref PlayerInput input)
    {
        grenadeCooldown -= dt;

        if (grenadeThrowing)
        {
            grenadeHold -= dt;
            if (grenadeSlot == 1) input.Grenade2 = grenadeHold > 0f; else input.Grenade1 = grenadeHold > 0f;
            if (grenadeHold <= 0f)
            {
                grenadeThrowing = false;
                grenadeCooldown = 6f + (float)rng.NextDouble() * 6f;
            }
            if (engaging) AimLob(dist, ref input);
            return;
        }

        // Pyros prefer their napalm; everyone else lobs frags (concussions are left for jumping).
        int slot = me.Class.SecondaryKind == GrenadeKind.Napalm && me.Grenades[1] > 0 ? 1 : 0;
        if (!engaging || grenadeCooldown > 0f || me.Grenades[slot] <= 0 || me.Primed >= 0) return;
        if (dist < 220f || dist > 700f || reactTimer > 0f || holdingStill) return;
        if (me.Class.Id == PlayerClassId.Spy && me.DisguiseTeam.HasValue) return;   // keep the disguise

        grenadeThrowing = true;
        grenadeSlot = slot;
        grenadeHold = Math.Clamp(2.5f - dist / 450f, 0.5f, 2.2f);
        if (slot == 1) input.Grenade2 = true; else input.Grenade1 = true;
        AimLob(dist, ref input);
    }

    /// <summary>
    /// Medics tend teammates who are infected or hurt, and otherwise close in on nearby enemies to infect them
    /// with the medikit (the disease then spreads through their team).
    /// </summary>
    void MedicTick(float dt, ref Vector3 moveDir, ref bool wantMove)
    {
        healScanTimer -= dt;
        if (healTarget != null && (!healTarget.Alive || !NeedsMedic(healTarget))) healTarget = null;
        if (healScanTimer <= 0)
        {
            healScanTimer = 0.5f;
            Player? best = null;
            float bestScore = float.MaxValue;
            foreach (var q in game.Players)
            {
                if (q == me || !q.Alive || q.Team != me.Team || !NeedsMedic(q)) continue;
                float d = Vector3.Distance(me.Position, q.Position);
                if (d > 450f || !game.World.LineOfSight(me.Eye, q.Center)) continue;
                float score = d - (q.IsInfected ? 150f : 0f);       // infected teammates first
                if (score < bestScore) { bestScore = score; best = q; }
            }
            healTarget = best;
        }

        Vector3 to;
        if (healTarget != null && !(HasTarget && Vector3.Distance(me.Eye, TargetCenter) < 250f))
            to = healTarget.Position - me.Position;
        else if (target != null && target.Alive && Vector3.Distance(me.Eye, target.Center) < 260f)
            to = target.Position - me.Position;                      // go and infect him
        else
            return;

        to.Y = 0;
        float flat = to.Length();
        if (flat < 40f) { wantMove = false; return; }
        moveDir = to / flat;
        wantMove = true;
    }

    static bool NeedsMedic(Player q) => q.IsInfected || q.Health < q.Class.MaxHealth * 0.75f;

    /// <summary>Face the teammate being tended (or the enemy being infected) and swing the medikit.</summary>
    void MedicAct(ref PlayerInput input)
    {
        Player? subject = null;
        if (healTarget != null && healTarget.Alive && !(HasTarget && Vector3.Distance(me.Eye, TargetCenter) < 250f))
            subject = healTarget;
        else if (target != null && target.Alive && Vector3.Distance(me.Eye, target.Center) < 100f)
            subject = target;
        if (subject == null) return;

        var to = subject.Center - me.Eye;
        if (to.Length() > 80f) return;
        aimYaw = MathF.Atan2(to.X, to.Z);
        aimPitch = MathF.Atan2(to.Y, MathF.Sqrt(to.X * to.X + to.Z * to.Z));
        input.Yaw = aimYaw;
        input.Pitch = aimPitch;
        if (me.Slot != 0) input.SelectSlot = 0;
        input.Fire = true;
        holdingStill = true;
    }

    /// <summary>Set a short-fuse detpack when a target is at mid range, then leave the area.</summary>
    void DetpackTick(float dt, bool engaging, float dist, ref PlayerInput input)
    {
        detpackRetreat = MathF.Max(0f, detpackRetreat - dt);
        if (me.Class.Id != PlayerClassId.Demoman) return;
        if (!engaging || me.Detpacks <= 0 || me.CarryingFlag != null || game.DetpackOf(me) != null) return;
        if (dist < 150f || dist > 320f || reactTimer > 0f || holdingStill) return;

        if (me.DetpackFuseIndex != 0)
        {
            input.DetpackFuseNext = true;      // cycle down to the 5 s setting first
            return;
        }
        input.PlaceDetpack = true;
        detpackRetreat = 6f;
    }

    /// <summary>Point the view up by the arc a thrown grenade needs to reach the target.</summary>
    void AimLob(float dist, ref PlayerInput input)
    {
        var to = TargetCenter - me.Eye;
        float horiz = MathF.Sqrt(to.X * to.X + to.Z * to.Z);
        aimYaw = MathF.Atan2(to.X, to.Z);
        aimPitch = MathF.Atan2(to.Y, horiz) + LobAngle(horiz, 560f) + 0.05f;
        input.Yaw = aimYaw;
        input.Pitch = aimPitch;
    }

    /// <summary>Spies disguise as the enemy, then close in on whatever they can stab or sabotage.</summary>
    void SpyTick(float dt, ref Vector3 moveDir, ref bool wantMove)
    {
        if (!me.DisguiseTeam.HasValue)
        {
            redisguise -= dt;
            if (redisguise <= 0)
            {
                int cls;
                do cls = rng.Next(Classes.All.Length); while (cls == (int)PlayerClassId.Spy);
                game.StartDisguise(me, me.Team.Opposite(), (PlayerClassId)cls);
                redisguise = 3f;
            }
        }
        else
        {
            redisguise = 3f;
        }

        if (!HasTarget) return;
        var to = TargetCenter - me.Position;
        to.Y = 0;
        float flat = to.Length();
        if (flat > 170f) return;
        if (flat < 40f) { wantMove = false; return; }   // close enough: stand and stab
        moveDir = to / flat;
        wantMove = true;
    }

    /// <summary>Build a sentry at the post, then keep upgrading and repairing it with the wrench.</summary>
    void EngineerTick(float dt, bool wantMove, bool engaging, ref PlayerInput input)
    {
        if (wantMove || engaging) return;
        if (teleCommit && BuildTeleporter(dt, ref input)) return;
        var sentry = game.SentryOf(me);

        if (sentry == null)
        {
            if (triedBuild)
            {
                // The last attempt didn't produce a sentry: turn and try another direction.
                triedBuild = false;
                if (me.Metal >= Sentry.BuildCost) { buildFails++; yawOffset += 1.1f; }
            }
            buildTimer -= dt;
            if (buildTimer > 0 || me.Metal < Sentry.BuildCost) return;
            buildTimer = 1.0f;
            aimYaw += yawOffset;
            yawOffset = 0;
            input.Yaw = aimYaw;
            input.Pitch = 0;
            input.AltFire = !me.PrevAlt;     // needs a fresh press
            triedBuild = true;
            return;
        }
        triedBuild = false;
        buildFails = 0;

        // Next: a dispenser, placed to the side of the sentry (it feeds the engineer metal for upgrades).
        var dispenser = game.DispenserOf(me);
        if (dispenser == null && me.Metal >= Dispenser.BuildCost)
        {
            if (dispenserTried)
            {
                dispenserTried = false;
                yawOffset += 1.3f;                     // that spot didn't work; try another direction
            }
            dispenserTimer -= dt;
            if (dispenserTimer <= 0)
            {
                dispenserTimer = 1f;
                if (!dispenserStarted) { dispenserStarted = true; yawOffset += MathF.PI / 2; }
                aimYaw += yawOffset;
                yawOffset = 0;
                input.Yaw = aimYaw;
                input.Pitch = 0;
                input.BuildDispenser = true;
                dispenserTried = true;
                return;
            }
        }
        else
        {
            dispenserTried = false;
        }

        // Tend whichever structure needs the wrench: sentry first, then dispenser.
        bool sentryNeeds = (sentry.Level < 3 && me.Metal >= Sentry.UpgradeCost)
            || ((sentry.Health < sentry.MaxHealth || sentry.Ammo < sentry.MaxAmmo) && me.Metal >= 10);
        bool dispenserNeeds = dispenser != null && !dispenser.Building
            && (dispenser.Health < Dispenser.MaxHealth || dispenser.Store < Dispenser.MaxStore) && me.Metal >= 10;
        if (!sentryNeeds && !dispenserNeeds)
        {
            // Everything is in good shape: with a full wallet, go set up a teleporter pair.
            bool needEntrance = game.TeleporterOf(me, TeleporterRole.Entrance) == null;
            bool needExit = game.TeleporterOf(me, TeleporterRole.Exit) == null;
            int needed = ((needEntrance ? 1 : 0) + (needExit ? 1 : 0)) * Teleporter.BuildCost;
            if (needed > 0 && sentry.Level >= 2 && dispenser != null && me.Metal >= needed) teleCommit = true;
            return;
        }

        var to = (sentryNeeds ? sentry.Hull.Center : dispenser!.Hull.Center) - me.Eye;
        aimYaw = MathF.Atan2(to.X, to.Z);
        aimPitch = MathF.Atan2(to.Y, MathF.Sqrt(to.X * to.X + to.Z * to.Z));
        input.Yaw = aimYaw;
        input.Pitch = aimPitch;
        if (me.Slot != 0) input.SelectSlot = 0;
        float dist = to.Length();
        if (dist > 70f)
        {
            input.Forward = 1f;      // walk up to it
        }
        else
        {
            input.Fire = true;
            holdingStill = true;
        }
    }

    /// <summary>At the build site: place the next missing teleporter end, turning to find room if blocked. True while still busy.</summary>
    bool BuildTeleporter(float dt, ref PlayerInput input)
    {
        bool needEntrance = game.TeleporterOf(me, TeleporterRole.Entrance) == null;
        bool needExit = game.TeleporterOf(me, TeleporterRole.Exit) == null;
        if (!needEntrance && !needExit)
        {
            teleCommit = false;
            return false;
        }

        int mine = game.Teleporters.Count(t => t.Owner == me && !t.Dead);
        if (teleCountBefore == mine) yawOffset += 1.3f;      // the last attempt placed nothing: face another way
        teleCountBefore = -1;

        teleTimer -= dt;
        if (teleTimer > 0 || me.Metal < Teleporter.BuildCost) return true;
        teleTimer = 1f;
        aimYaw += yawOffset;
        yawOffset = 0;
        input.Yaw = aimYaw;
        input.Pitch = 0;
        input.BuildTeleporter = true;
        teleCountBefore = mine;
        return true;
    }

    float Rand() => (float)(rng.NextDouble() * 2 - 1);

    // ───────────── target selection ─────────────

    bool HasTarget => (target != null && target.Alive) || (targetSentry != null && !targetSentry.Dead);

    Vector3 TargetCenter => target != null && target.Alive ? target.Center : targetSentry!.Hull.Center;

    void UpdateTarget(float dt)
    {
        targetTimer -= dt;
        if (target != null && !target.Alive) target = null;
        if (targetSentry != null && targetSentry.Dead) targetSentry = null;
        if (targetTimer > 0) return;
        targetTimer = 0.25f;

        Player? best = null;
        Sentry? bestSentry = null;
        float playerRange = me.Class.Id == PlayerClassId.Sniper ? 2500f : 1200f;
        float sentryRange = playerRange;
        if (me.Class.Id == PlayerClassId.Spy && me.DisguiseTeam.HasValue)
        {
            // A disguised spy stays quiet and only goes for knife kills and sabotage.
            playerRange = 100f;
            sentryRange = 170f;
        }
        float bestD = float.MaxValue;
        foreach (var q in game.Players)
        {
            if (!q.IsTargetableBy(me.Team)) continue;
            float d = Vector3.Distance(me.Eye, q.Center);
            if (d >= playerRange || d >= bestD) continue;
            if (!game.World.LineOfSight(me.Eye, q.Center)) continue;
            best = q;
            bestD = d;
        }
        foreach (var s in game.Sentries)
        {
            if (s.Dead || s.Team == me.Team || s.Sabotaged) continue;
            float d = Vector3.Distance(me.Eye, s.Hull.Center);
            if (d >= sentryRange || d >= bestD) continue;
            if (!game.World.LineOfSight(me.Eye, s.Hull.Center)) continue;
            bestSentry = s;
            best = null;
            bestD = d;
        }

        bool changed = best != target || bestSentry != targetSentry;
        if (changed && (best != null || bestSentry != null)) reactTimer = 0.25f + (float)rng.NextDouble() * 0.35f;
        target = best;
        targetSentry = bestSentry;
    }

    int ChooseSlot(float dist)
    {
        var slots = me.Class.Slots;
        int main = me.Class.DefaultSlot;

        int want = main;
        switch (me.Class.Id)
        {
            case PlayerClassId.Soldier:
                if (dist < 130) want = 1;
                break;
            case PlayerClassId.Pyro:
                if (dist > 260) want = 1;
                break;
            case PlayerClassId.Sniper:
                want = dist > 600 ? 1 : 2;
                break;
            case PlayerClassId.Demoman:
                if (dist < 150) want = 0;
                break;
            case PlayerClassId.Spy:
                if (dist < 120 || targetSentry != null) want = 0;   // knife for backstabs and sabotage
                break;
            case PlayerClassId.Medic:
                if (dist < 100) want = 0;                           // medikit: infects whoever it hits
                break;
        }

        if (!HasAmmo(slots[want])) want = Enumerable.Range(0, slots.Length)
            .Where(i => HasAmmo(slots[i]))
            .DefaultIfEmpty(0)
            .OrderByDescending(i => i == main)
            .First();
        return want;
    }

    bool HasAmmo(WeaponId id)
    {
        var w = Weapons.Get(id);
        return w.Ammo == AmmoType.None || me.Ammo[(int)w.Ammo] >= w.AmmoPerShot;
    }

    Vector3 AimPoint(WeaponDef w, float dist)
    {
        if (target == null || !target.Alive)
            return TargetCenter;   // sentries don't move; aim at the middle

        var t = target;
        switch (w.Mode)
        {
            case FireMode.Rocket:
            case FireMode.Grenade:
            case FireMode.Pipe:
                // Aim at the feet and lead the target.
                float flight = dist / MathF.Max(w.ProjectileSpeed, 1f);
                var lead = new Vector3(t.Velocity.X, 0, t.Velocity.Z) * flight;
                return t.Position + new Vector3(0, 8, 0) + lead;
            case FireMode.SniperCharge:
                return t.Position + new Vector3(0, 62, 0);
            default:
                return t.Center + new Vector3(0, 8, 0);
        }
    }

    static float LobAngle(float range, float speed)
    {
        const float g = 700f;
        float s = Math.Clamp(g * range / (speed * speed), 0f, 1f);
        return 0.5f * MathF.Asin(s);
    }

    // ───────────── navigation ─────────────

    Vector3 GoalPosition()
    {
        var own = game.Flags[(int)me.Team];
        var enemy = game.Flags[(int)me.Team.Opposite()];

        // Engineers on a teleporter run go to the entrance site, then the exit site.
        if (teleCommit && me.Class.Id == PlayerClassId.Engineer)
        {
            string side = me.Team == Team.Red ? "R_" : "B_";
            bool needEntrance = game.TeleporterOf(me, TeleporterRole.Entrance) == null;
            return game.Map.Nav.Nodes[game.Map.Nav.Find(side + (needEntrance ? "spawnexit" : "bridge"))].Position;
        }

        // A Demoman who just set a detpack backs off toward his own half until it goes off.
        if (detpackRetreat > 0 && me.Class.Id == PlayerClassId.Demoman)
        {
            string side = me.Team == Team.Red ? "R_" : "B_";
            return game.Map.Nav.Nodes[game.Map.Nav.Find(side + "field")].Position;
        }

        if (me.CarryingFlag != null) return own.Home;
        if (own.Carrier != null) return own.Carrier.Position;
        if (own.Dropped) return own.Position;
        if (defender) return game.Map.Nav.Nodes[defendNode].Position;

        // Attackers hop on a friendly teleporter if one is up and close by.
        if (me.TeleportCount != lastTeleportCount) { lastTeleportCount = me.TeleportCount; useTele = false; }
        if (useTele)
        {
            var entrance = game.Teleporters.FirstOrDefault(t => t.Team == me.Team && t.Role == TeleporterRole.Entrance && t.Active
                && Vector3.Distance(t.Position, me.Position) < 1000f
                && game.Teleporters.Any(x => x.Owner == t.Owner && x.Role == TeleporterRole.Exit && x.Active));
            if (entrance != null) return entrance.Position;
        }
        return enemy.Position;
    }

    Vector3 Navigate(float dt, out bool wantMove)
    {
        wantMove = false;
        var nav = game.Map.Nav;
        var goalPos = GoalPosition();

        repathTimer -= dt;
        int goalNode = nav.Nearest(goalPos, game.World);
        if (goalNode != pathGoal || repathTimer <= 0 || path.Count == 0)
        {
            int start = nav.Nearest(me.Position, game.World);
            path = nav.FindPath(start, goalNode, rng, 0.8f);
            pathIndex = 0;
            pathGoal = goalNode;
            repathTimer = 3f;
            // Skip the first node if we're already past it.
            if (path.Count > 1 && Horiz(nav.Nodes[path[0]].Position - me.Position) < 40f) pathIndex = 1;
        }

        Vector3 dest;
        if (pathIndex < path.Count)
        {
            dest = nav.Nodes[path[pathIndex]].Position;
            if (Horiz(dest - me.Position) < 40f && pathIndex < path.Count)
            {
                pathIndex++;
                dest = pathIndex < path.Count ? nav.Nodes[path[pathIndex]].Position : goalPos;
            }
        }
        else
        {
            dest = goalPos;
        }

        float dist = Horiz(dest - me.Position);
        float arrive = pathIndex >= path.Count ? 36f : 12f;
        if (dist < arrive) return Vector3.Zero;

        wantMove = true;
        var dir = dest - me.Position;
        dir.Y = 0;
        return Vector3.Normalize(dir);
    }

    static float Horiz(Vector3 v) => MathF.Sqrt(v.X * v.X + v.Z * v.Z);

    void UpdateStuck(float dt, bool wantMove, ref PlayerInput input)
    {
        jumpTimer -= dt;
        if (!wantMove)
        {
            stuckTimer = 0;
            sampleTimer = 0;
            lastPos = me.Position;
            return;
        }

        sampleTimer += dt;
        if (sampleTimer < 0.25f) return;
        sampleTimer = 0;

        if (Horiz(me.Position - lastPos) > 15f)
        {
            stuckTimer = 0;
            totalStuck = MathF.Max(0f, totalStuck - 0.5f);
        }
        else
        {
            stuckTimer += 0.25f;
            totalStuck += 0.25f;
            if (jumpTimer <= 0)
            {
                input.Jump = true;
                jumpTimer = 0.5f;
            }
            if (stuckTimer >= 2f)
            {
                repathTimer = 0;
                stuckTimer = 0;
            }
        }
        lastPos = me.Position;

        if (totalStuck > 20f)
        {
            totalStuck = 0;
            game.BotStuckResets++;
            game.Kill(me, null, "got stuck");
        }
    }

    // ───────────── angle helpers ─────────────

    static float AngleDiff(float a, float b) => Angles.Diff(a, b);

    static float ApproachAngle(float cur, float des, float maxStep) => Angles.Approach(cur, des, maxStep);
}
