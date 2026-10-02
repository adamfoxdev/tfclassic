using System.Numerics;
using TFClassic.Core;
using Xunit;
using static TFClassic.Tests.Helpers;

namespace TFClassic.Tests;

public class SpecialGrenadeTests
{
    static Game Match(PlayerClassId cls, out Player thrower, Team team = Team.Red)
    {
        var g = NewGame();
        thrower = Human(g, team, cls);
        Place(g, thrower, new Vector3(900, 0, 100), yaw: MathF.PI);   // out of the way, in the Red half's open field
        g.Tick(Dt);
        return g;
    }

    /// <summary>A grenade lying on the ground about to go off.</summary>
    static Projectile Drop(Game g, Player owner, ProjectileKind kind, Vector3 at, float damage = 0, float splash = 0)
    {
        var pr = new Projectile
        {
            Kind = kind, Owner = owner, Team = owner.Team,
            Position = at + new Vector3(0, 3, 0), Stuck = true, Fuse = 0.05f, Damage = damage, Splash = splash,
        };
        g.Projectiles.Add(pr);
        return pr;
    }

    static void Throw(Game g, Player p, bool secondary)
    {
        p.Input = new PlayerInput { SelectSlot = -1, Yaw = p.Yaw, Grenade1 = !secondary, Grenade2 = secondary };
        Run(g, 0.2f);
        p.Input.Grenade1 = p.Input.Grenade2 = false;
        g.Tick(Dt);
    }

    // ───────── loadouts ─────────

    [Fact]
    public void EveryClassGetsItsTfcLoadout()
    {
        var g = NewGame();
        (PlayerClassId cls, GrenadeKind primary, GrenadeKind? secondary)[] expect =
        {
            (PlayerClassId.Scout, GrenadeKind.Caltrops, GrenadeKind.Concussion),
            (PlayerClassId.Soldier, GrenadeKind.Frag, GrenadeKind.Nail),
            (PlayerClassId.Demoman, GrenadeKind.Frag, GrenadeKind.Mirv),
            (PlayerClassId.HeavyWeapons, GrenadeKind.Frag, GrenadeKind.Mirv),
            (PlayerClassId.Sniper, GrenadeKind.Frag, null),
            (PlayerClassId.Medic, GrenadeKind.Frag, GrenadeKind.Concussion),
            (PlayerClassId.Pyro, GrenadeKind.Frag, GrenadeKind.Napalm),
            (PlayerClassId.Engineer, GrenadeKind.Frag, GrenadeKind.Emp),
            (PlayerClassId.Spy, GrenadeKind.Frag, GrenadeKind.Gas),
        };
        foreach (var (cls, primary, secondary) in expect)
        {
            var c = Classes.Get(cls);
            Assert.Equal(primary, c.PrimaryKind);
            if (secondary != null) { Assert.Equal(secondary.Value, c.SecondaryKind); Assert.True(c.Secondary > 0); }
            else Assert.Equal(0, c.Secondary);
        }
    }

    [Fact]
    public void ThrowingMapsEachKeyToTheClassesOwnGrenade()
    {
        var g = Match(PlayerClassId.Scout, out var scout);
        Throw(g, scout, secondary: false);
        Assert.Contains(g.Projectiles, p => p.Kind == ProjectileKind.Caltrops);
        Throw(g, scout, secondary: true);
        Assert.Contains(g.Projectiles, p => p.Kind == ProjectileKind.Concussion);

        var g2 = Match(PlayerClassId.Spy, out var spy);
        Throw(g2, spy, secondary: true);
        Assert.Contains(g2.Projectiles, p => p.Kind == ProjectileKind.Gas);

        var g3 = Match(PlayerClassId.Engineer, out var eng);
        Throw(g3, eng, secondary: true);
        Assert.Contains(g3.Projectiles, p => p.Kind == ProjectileKind.Emp);
    }

    // ───────── caltrops ─────────

    [Fact]
    public void CaltropsHurtAndSlowEnemiesOnFootOncePerSecond()
    {
        var g = Match(PlayerClassId.Scout, out var scout);
        var enemy = Human(g, Team.Blue, PlayerClassId.HeavyWeapons);
        var friend = Human(g, Team.Red, PlayerClassId.HeavyWeapons);
        Place(g, enemy, new Vector3(0, 0, 600));
        Place(g, friend, new Vector3(30, 0, 600));
        enemy.Armor = 0;
        float hp = enemy.Health;
        Drop(g, scout, ProjectileKind.Caltrops, new Vector3(0, 0, 600));
        Run(g, 0.3f);
        Assert.Single(g.AreaEffects);
        Assert.Equal(AreaKind.Caltrops, g.AreaEffects[0].Kind);
        Assert.InRange(hp - enemy.Health, 9f, 11f);
        Assert.True(enemy.SlowTime > 1f);
        Assert.Equal(friend.Class.StartArmor, friend.Armor);       // teammates untouched
        Assert.Equal(friend.Class.MaxHealth, friend.Health);

        Run(g, 0.3f);
        Assert.InRange(hp - enemy.Health, 9f, 11f);                // not again within the second
        Run(g, 1.0f);
        Assert.InRange(hp - enemy.Health, 19f, 21f);
    }

    [Fact]
    public void JumpingOverCaltropsIsSafeAndTheFieldWearsOut()
    {
        var g = Match(PlayerClassId.Scout, out var scout);
        var enemy = Human(g, Team.Blue, PlayerClassId.HeavyWeapons);
        Place(g, enemy, new Vector3(0, 0, 600));
        Drop(g, scout, ProjectileKind.Caltrops, new Vector3(0, 0, 600));
        Run(g, 0.1f);
        enemy.Position = new Vector3(0, 60, 600);                  // airborne above the spikes
        enemy.Velocity = new Vector3(0, 0, 0);
        enemy.OnGround = false;
        float hp = enemy.Health;
        Run(g, 0.15f);
        Assert.Equal(hp, enemy.Health);

        // Charges run out: after enough hits the field disappears.
        var g2 = Match(PlayerClassId.Scout, out var s2);
        var victim = Human(g2, Team.Blue, PlayerClassId.HeavyWeapons);
        Place(g2, victim, new Vector3(0, 0, 600));
        Drop(g2, s2, ProjectileKind.Caltrops, new Vector3(0, 0, 600));
        Run(g2, 0.2f);
        for (int i = 0; i < 12; i++) { victim.Health = 100; Run(g2, 1.1f); }
        Assert.Empty(g2.AreaEffects);
    }

    // ───────── nail grenade ─────────

    [Fact]
    public void NailGrenadeSpraysNailsAtEnemiesAroundItForAFewSeconds()
    {
        var g = Match(PlayerClassId.Soldier, out var soldier);
        var enemy = Human(g, Team.Blue, PlayerClassId.HeavyWeapons);
        var friend = Human(g, Team.Red, PlayerClassId.HeavyWeapons);
        Place(g, enemy, new Vector3(200, 0, 600));
        Place(g, friend, new Vector3(-200, 0, 600));
        enemy.Armor = friend.Armor = 0;
        Drop(g, soldier, ProjectileKind.Nail, new Vector3(0, 0, 600));
        Run(g, 4.5f);

        Assert.True(enemy.Health < enemy.Class.MaxHealth - 40f, $"enemy hp {enemy.Health}");
        Assert.Equal(friend.Class.MaxHealth, friend.Health);
        Assert.Empty(g.AreaEffects);                               // burned out after ~4 s
    }

    [Fact]
    public void NailsDoNotGoThroughWalls()
    {
        var g = Match(PlayerClassId.Soldier, out var soldier);
        var behind = Human(g, Team.Blue, PlayerClassId.HeavyWeapons);
        Place(g, behind, new Vector3(-250, 0, 1340));              // inside the red hall
        behind.Armor = 0;
        Drop(g, soldier, ProjectileKind.Nail, new Vector3(-250, 0, 1250));
        Run(g, 4.5f);
        Assert.Equal(behind.Class.MaxHealth, behind.Health);
    }

    // ───────── MIRV ─────────

    [Fact]
    public void MirvBlastsThenReleasesFourBombletsThatAlsoExplode()
    {
        var g = Match(PlayerClassId.Demoman, out var demo);
        var enemy = Human(g, Team.Blue, PlayerClassId.Scout);
        Place(g, enemy, new Vector3(0, 0, 1000));
        enemy.Armor = 0;
        Drop(g, demo, ProjectileKind.Mirv, new Vector3(0, 0, 500), damage: 70, splash: 130);
        Run(g, 0.1f);
        Assert.Equal(4, g.Projectiles.Count(p => p.Kind == ProjectileKind.MirvBomblet));

        int detonations = 0, last = 4;
        for (int i = 0; i < 60 * 3; i++)
        {
            g.Tick(Dt);
            int now = g.Projectiles.Count(p => p.Kind == ProjectileKind.MirvBomblet);
            if (now < last) detonations += last - now;
            last = now;
        }
        Assert.Equal(4, detonations);
        Assert.Empty(g.Projectiles);
    }

    [Fact]
    public void BombletsReachPlacesTheParentBlastCannot()
    {
        var g = Match(PlayerClassId.Demoman, out var demo);
        var center = new Vector3(0, 0, 600);
        var ring = new List<Player>();
        for (int i = 0; i < 8; i++)
        {
            var p = Human(g, Team.Blue, PlayerClassId.HeavyWeapons);
            float a = i * MathF.PI / 4f;
            Place(g, p, center + new Vector3(MathF.Cos(a) * 200f, 0, MathF.Sin(a) * 200f));
            p.Armor = 0;
            ring.Add(p);
        }
        Drop(g, demo, ProjectileKind.Mirv, center, damage: 70, splash: 130);
        Run(g, 0.1f);                                              // parent has gone off
        Assert.All(ring, p => Assert.Equal(p.Class.MaxHealth, p.Health));   // nobody is inside its 130 radius

        for (int i = 0; i < 60 * 3; i++)
        {
            g.Tick(Dt);
            foreach (var p in ring) p.Input = new PlayerInput { SelectSlot = -1 };
        }
        Assert.Contains(ring, p => p.Health < p.Class.MaxHealth);  // some bomblets landed among them
    }

    // ───────── gas ─────────

    [Fact]
    public void GasCloudSicklesEnemiesInsideNotFriendsOrThoseOutside()
    {
        var g = Match(PlayerClassId.Spy, out var spy);
        var inside = Human(g, Team.Blue, PlayerClassId.HeavyWeapons);
        var outside = Human(g, Team.Blue, PlayerClassId.HeavyWeapons);
        var friend = Human(g, Team.Red, PlayerClassId.HeavyWeapons);
        Place(g, inside, new Vector3(60, 0, 600));
        Place(g, outside, new Vector3(500, 0, 600));
        Place(g, friend, new Vector3(-60, 0, 600));
        foreach (var p in new[] { inside, outside, friend }) p.Armor = 0;
        Drop(g, spy, ProjectileKind.Gas, new Vector3(0, 0, 600));
        Run(g, 3f);

        Assert.True(inside.GasTime > 0);
        Assert.True(inside.Health < inside.Class.MaxHealth);
        Assert.Equal(0f, outside.GasTime);
        Assert.Equal(0f, friend.GasTime);
        Assert.Equal(friend.Class.MaxHealth, friend.Health);

        Run(g, 8f);
        Assert.Empty(g.AreaEffects);                               // the cloud dissipates
        Run(g, 3.2f);
        Assert.Equal(0f, inside.GasTime);                          // and the dizziness wears off
    }

    [Fact]
    public void GasIsBlockedByWallsAndBlowsADisguise()
    {
        var g = Match(PlayerClassId.Spy, out var spy);
        var behind = Human(g, Team.Blue, PlayerClassId.Scout);
        Place(g, behind, new Vector3(-250, 0, 1340));
        Drop(g, spy, ProjectileKind.Gas, new Vector3(-250, 0, 1250));
        Run(g, 3f);
        Assert.Equal(0f, behind.GasTime);

        var enemySpy = Human(g, Team.Blue, PlayerClassId.Spy);
        Place(g, enemySpy, new Vector3(900, 0, 600));
        g.StartDisguise(enemySpy, Team.Red, PlayerClassId.Soldier);
        enemySpy.DisguiseTimer = 0;
        Drop(g, spy, ProjectileKind.Gas, new Vector3(900, 0, 600));
        Run(g, 1f);
        Assert.False(enemySpy.DisguiseTeam.HasValue);
    }

    // ───────── EMP ─────────

    [Fact]
    public void EmpDrainsAmmoAndMetalAndHurtsInProportion()
    {
        var g = Match(PlayerClassId.Engineer, out var eng);
        var heavy = Human(g, Team.Blue, PlayerClassId.HeavyWeapons);       // 200 shells: lots to detonate
        var scout = Human(g, Team.Blue, PlayerClassId.Soldier);              // 150 rounds vs the HWGuy's 200
        var friend = Human(g, Team.Red, PlayerClassId.HeavyWeapons);
        Place(g, heavy, new Vector3(60, 0, 600));
        Place(g, scout, new Vector3(-60, 0, 600));
        Place(g, friend, new Vector3(0, 0, 660));
        heavy.Armor = scout.Armor = 0;
        Drop(g, eng, ProjectileKind.Emp, new Vector3(0, 0, 600), splash: 240);
        Run(g, 0.1f);

        Assert.All(new[] { 1, 2, 3, 4 }, i => Assert.Equal(0, heavy.Ammo[i]));
        Assert.True(heavy.Health < heavy.Class.MaxHealth - 30f, $"hp {heavy.Health}");
        Assert.True(scout.Health < scout.Class.MaxHealth);
        Assert.True(heavy.Class.MaxHealth - heavy.Health > scout.Class.MaxHealth - scout.Health, "more ammo, more damage");
        Assert.True(friend.Ammo[(int)AmmoType.Shells] > 0, "teammates keep their ammo");
    }

    [Fact]
    public void EmpWrecksEnemyGadgetsAndCooksPrimedGrenades()
    {
        var g = Match(PlayerClassId.Engineer, out var eng);
        var rival = Human(g, Team.Blue, PlayerClassId.Engineer);
        var demo = Human(g, Team.Blue, PlayerClassId.Demoman);
        Place(g, rival, new Vector3(900, 0, 100));
        Place(g, demo, new Vector3(100, 0, 600));
        var sentry = new Sentry { Owner = rival, Team = Team.Blue, Position = new Vector3(-80, 0, 600), BuildTimer = 0, Level = 3, Ammo = 100, Rockets = 20 };
        var disp = new Dispenser { Owner = rival, Team = Team.Blue, Position = new Vector3(0, 0, 700), BuildTimer = 0 };
        var pack = new Detpack { Owner = demo, Team = Team.Blue, Position = new Vector3(60, 0, 700), BuildTimer = 0, Fuse = 30f };
        var tele = new Teleporter { Owner = rival, Team = Team.Blue, Position = new Vector3(-60, 0, 700), BuildTimer = 0 };
        g.Sentries.Add(sentry);
        g.Dispensers.Add(disp);
        g.Detpacks.Add(pack);
        g.Teleporters.Add(tele);
        demo.Armor = 0;
        demo.Input = new PlayerInput { SelectSlot = -1, Yaw = demo.Yaw, Grenade1 = true };
        Run(g, 0.3f);
        Assert.True(demo.Primed >= 0);

        Drop(g, eng, ProjectileKind.Emp, new Vector3(0, 0, 640), splash: 240);
        Run(g, 0.1f);

        Assert.Equal(0, sentry.Ammo);
        Assert.Equal(0, sentry.Rockets);
        Assert.Equal(0, disp.Store);
        Assert.True(pack.Dead);
        Assert.True(tele.CooldownTimer > 10f);
        Assert.Equal(-1, demo.Primed);                             // his grenade went off in his hand
    }

    // ───────── bots and housekeeping ─────────

    [Fact]
    public void ResetClearsLingeringEffects()
    {
        var g = Match(PlayerClassId.Spy, out var spy);
        Drop(g, spy, ProjectileKind.Gas, new Vector3(0, 0, 600));
        Run(g, 0.2f);
        Assert.NotEmpty(g.AreaEffects);
        g.ResetMatch();
        Assert.Empty(g.AreaEffects);
    }

    [Fact]
    public void BotsUseTheirOffensiveSpecialGrenades()
    {
        var g = NewGame(12);
        for (int i = 0; i < 3; i++)
        {
            g.AddBot(Team.Red, PlayerClassId.Soldier);
            g.AddBot(Team.Red, PlayerClassId.Demoman);
            g.AddBot(Team.Blue, PlayerClassId.HeavyWeapons);
            g.AddBot(Team.Blue, PlayerClassId.Soldier);
        }
        var kinds = new HashSet<ProjectileKind>();
        for (int i = 0; i < 60 * 300; i++)
        {
            g.Tick(Dt);
            foreach (var pr in g.Projectiles)
                if (pr.Kind is ProjectileKind.Nail or ProjectileKind.Mirv) kinds.Add(pr.Kind);
        }
        Assert.Contains(ProjectileKind.Nail, kinds);
        Assert.Contains(ProjectileKind.Mirv, kinds);
    }
}
