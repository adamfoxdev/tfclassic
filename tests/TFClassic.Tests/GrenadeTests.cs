using System.Numerics;
using TFClassic.Core;
using Xunit;
using static TFClassic.Tests.Helpers;

namespace TFClassic.Tests;

public class GrenadeTests
{
    static (Game g, Player thrower) Setup(PlayerClassId cls = PlayerClassId.Soldier)
    {
        var g = NewGame();
        var p = Human(g, Team.Red, cls);
        Place(g, p, new Vector3(0, 0, 800), yaw: MathF.PI);
        g.Tick(Dt);
        return (g, p);
    }

    static void Hold(Game g, Player p, Action<Player, bool> set, float seconds, float pitch = 0f)
    {
        set(p, true);
        p.Input.Yaw = p.Yaw;
        p.Input.Pitch = pitch;
        Run(g, seconds);
    }

    static readonly Action<Player, bool> Frag = (p, on) => p.Input = new PlayerInput { SelectSlot = -1, Yaw = p.Yaw, Pitch = p.Input.Pitch, Grenade1 = on };
    static readonly Action<Player, bool> Conc = (p, on) => p.Input = new PlayerInput { SelectSlot = -1, Yaw = p.Yaw, Pitch = p.Input.Pitch, Grenade2 = on };

    static Projectile? Live(Game g, ProjectileKind kind) => g.Projectiles.FirstOrDefault(x => x.Kind == kind && !x.Dead);

    [Fact]
    public void ClassesStartWithTheirGrenades()
    {
        var g = NewGame();
        var soldier = Human(g, Team.Red, PlayerClassId.Soldier);
        var scout = Human(g, Team.Red, PlayerClassId.Scout);
        var engineer = Human(g, Team.Red, PlayerClassId.Engineer);
        Assert.Equal(new[] { 4, 2 }, soldier.Grenades);   // frags + nail grenades
        Assert.Equal(new[] { 2, 3 }, scout.Grenades);
        Assert.Equal(new[] { 2, 2 }, engineer.Grenades);  // frags + EMPs
    }

    [Fact]
    public void HoldingPrimesAndReleasingThrows()
    {
        var (g, p) = Setup();
        Hold(g, p, Frag, 0.3f);
        Assert.Equal(0, p.Primed);
        Assert.Equal(3, p.Grenades[0]);
        Assert.Null(Live(g, ProjectileKind.HandGrenade));

        Frag(p, false);
        g.Tick(Dt);
        var pr = Live(g, ProjectileKind.HandGrenade);
        Assert.NotNull(pr);
        Assert.Equal(-1, p.Primed);
        Assert.InRange(pr!.Fuse, 2.5f, 2.8f);                 // ~0.3 s of the 3 s fuse was cooked
        Assert.True(pr.Velocity.Z < -300f, "thrown forward (toward -Z)");
    }

    [Fact]
    public void CookingShortensTheFuse()
    {
        var (g, p) = Setup();
        Hold(g, p, Frag, 1.5f);
        Frag(p, false);
        g.Tick(Dt);
        Assert.InRange(Live(g, ProjectileKind.HandGrenade)!.Fuse, 1.3f, 1.6f);
    }

    [Fact]
    public void CannotPrimeWithNoGrenadesLeft()
    {
        var (g, p) = Setup();
        p.Grenades[0] = 0;
        Hold(g, p, Frag, 0.5f);
        Assert.Equal(-1, p.Primed);
        Assert.Empty(g.Projectiles);
    }

    [Fact]
    public void FragExplodesNearLandingHurtsEnemiesNotTeammates()
    {
        var (g, p) = Setup();
        var enemy = Human(g, Team.Blue, PlayerClassId.HeavyWeapons);
        var friend = Human(g, Team.Red, PlayerClassId.HeavyWeapons);
        enemy.Armor = friend.Armor = 0;
        Place(g, enemy, new Vector3(-900, 0, 1500));
        Place(g, friend, new Vector3(900, 0, 1500));

        Hold(g, p, Frag, 0.2f);
        Frag(p, false);
        g.Tick(Dt);
        var pr = Live(g, ProjectileKind.HandGrenade)!;
        while (pr.Age < pr.Fuse - 0.05f) g.Tick(Dt);
        Place(g, enemy, new Vector3(pr.Position.X + 40, 0, pr.Position.Z));
        Place(g, friend, new Vector3(pr.Position.X - 40, 0, pr.Position.Z));
        enemy.Armor = friend.Armor = 0;
        float enemyHp = enemy.Health, friendHp = friend.Health;
        Run(g, 0.2f);

        Assert.True(enemy.Health < enemyHp - 40f, $"enemy hp {enemy.Health}");
        Assert.Equal(friendHp, friend.Health);
        Assert.Contains(g.Effects, e => e.Kind == EffectKind.Explosion);
    }

    [Fact]
    public void CookingTooLongBlowsUpInYourHand()
    {
        var (g, p) = Setup();
        p.Armor = 0;
        Hold(g, p, Frag, 3.3f);
        Assert.Equal(-1, p.Primed);
        Assert.Equal(3, p.Grenades[0]);                       // one consumed, not thrown
        Assert.Null(Live(g, ProjectileKind.HandGrenade));
        Assert.True(p.Health < p.Class.MaxHealth, "the thrower takes the blast");
    }

    [Fact]
    public void DyingWhilePrimedDropsALiveGrenade()
    {
        var (g, p) = Setup();
        Hold(g, p, Frag, 0.5f);
        g.Kill(p, null, "test");
        Assert.Equal(-1, p.Primed);
        var dropped = Live(g, ProjectileKind.HandGrenade);
        Assert.NotNull(dropped);
        Assert.InRange(dropped!.Fuse, 2.3f, 2.6f);
    }

    [Fact]
    public void ConcussionGrenadeLaunchesTheThrowerWithoutDamage()
    {
        var (g, p) = Setup(PlayerClassId.Scout);
        p.Armor = 0;
        float hp = p.Health;
        Hold(g, p, Conc, 0.2f, pitch: -1.45f);               // look down and throw at your feet
        Conc(p, false);
        p.Input.Pitch = -1.45f;
        float peak = 0;
        for (int i = 0; i < 60 * 4; i++)
        {
            g.Tick(Dt);
            peak = MathF.Max(peak, p.Position.Y);
        }

        Assert.Equal(2, p.Grenades[1]);
        Assert.Equal(hp, p.Health);
        Assert.True(peak > 90f, $"peak={peak}");
    }

    [Fact]
    public void ConcussionShovesAndDizziesEnemiesAndTeammatesAlike()
    {
        var g = NewGame();
        var owner = Human(g, Team.Red, PlayerClassId.Scout);
        var enemy = Human(g, Team.Blue, PlayerClassId.Soldier);
        var friend = Human(g, Team.Red, PlayerClassId.Soldier);
        var far = Human(g, Team.Blue, PlayerClassId.Soldier);
        Place(g, owner, new Vector3(0, 0, 1000), yaw: MathF.PI);
        Place(g, enemy, new Vector3(60, 0, 900));
        Place(g, friend, new Vector3(-60, 0, 900));
        Place(g, far, new Vector3(0, 0, 400));

        var pr = new Projectile
        {
            Kind = ProjectileKind.Concussion, Owner = owner, Team = Team.Red,
            Position = new Vector3(0, 10, 900), Velocity = Vector3.Zero, Stuck = true, Fuse = 0.05f, Splash = 260,
        };
        g.Projectiles.Add(pr);
        float enemyHp = enemy.Health;
        Run(g, 0.2f);

        Assert.True(enemy.ConcussTime > 3f);
        Assert.True(friend.ConcussTime > 3f);
        Assert.Equal(0f, far.ConcussTime);
        Assert.Equal(enemyHp, enemy.Health);
        Assert.True(enemy.Velocity.X > 150f && friend.Velocity.X < -150f, "pushed away from the blast");
    }

    [Fact]
    public void ConcussionIsBlockedByWalls()
    {
        var g = NewGame();
        var owner = Human(g, Team.Red, PlayerClassId.Scout);
        var behindWall = Human(g, Team.Blue, PlayerClassId.Soldier);
        Place(g, owner, new Vector3(-250, 0, 1100));
        Place(g, behindWall, new Vector3(-250, 0, 1450));      // inside the hall, front wall at z=1300
        g.Projectiles.Add(new Projectile
        {
            Kind = ProjectileKind.Concussion, Owner = owner, Team = Team.Red,
            Position = new Vector3(-250, 10, 1250), Stuck = true, Fuse = 0.05f, Splash = 260,
        });
        Run(g, 0.2f);
        Assert.Equal(0f, behindWall.ConcussTime);
    }

    [Fact]
    public void ConcussionFadesAndGrenadesRestockAtTheLocker()
    {
        var (g, p) = Setup();
        p.ConcussTime = 1f;
        p.Grenades[0] = 0;
        Run(g, 1.2f);
        Assert.Equal(0f, p.ConcussTime);

        Place(g, p, new Vector3(0, 0, 1500));                  // inside the red hall (resupply)
        Run(g, 0.3f);
        Assert.Equal(4, p.Grenades[0]);
    }

    [Fact]
    public void BotsThrowGrenadesInMatches()
    {
        var g = NewGame(9);
        for (int i = 0; i < 5; i++)
        {
            g.AddBot(Team.Red, PlayerClassId.Soldier);
            g.AddBot(Team.Blue, PlayerClassId.HeavyWeapons);
        }
        var seen = new HashSet<Projectile>();
        for (int i = 0; i < 60 * 240; i++)
        {
            g.Tick(Dt);
            foreach (var pr in g.Projectiles)
                if (pr.Kind == ProjectileKind.HandGrenade) seen.Add(pr);
        }
        Assert.True(seen.Count >= 3, $"bots threw {seen.Count} grenades");
    }
}
