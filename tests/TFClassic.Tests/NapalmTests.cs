using System.Numerics;
using TFClassic.Core;
using Xunit;
using static TFClassic.Tests.Helpers;

namespace TFClassic.Tests;

public class NapalmTests
{
    static Game NewMatch(out Player pyro)
    {
        var g = NewGame();
        pyro = Human(g, Team.Red, PlayerClassId.Pyro);
        Place(g, pyro, new Vector3(0, 0, 800), yaw: MathF.PI);
        g.Tick(Dt);
        return g;
    }

    /// <summary>A napalm grenade lying on the ground about to go off.</summary>
    static void DropNapalm(Game g, Player owner, Vector3 at) =>
        g.Projectiles.Add(new Projectile
        {
            Kind = ProjectileKind.Napalm, Owner = owner, Team = owner.Team,
            Position = at + new Vector3(0, 3, 0), Stuck = true, Fuse = 0.05f, Splash = FirePatch.Radius, Damage = 20,
        });

    [Fact]
    public void PyrosCarryNapalmAndOthersKeepTheirOwnSecondary()
    {
        var g = NewGame();
        var pyro = Human(g, Team.Red, PlayerClassId.Pyro);
        var scout = Human(g, Team.Red, PlayerClassId.Scout);
        var soldier = Human(g, Team.Red, PlayerClassId.Soldier);
        Assert.Equal(new[] { 2, 2 }, pyro.Grenades);
        Assert.Equal(GrenadeKind.Napalm, pyro.Class.SecondaryKind);
        Assert.Equal(new[] { 2, 3 }, scout.Grenades);
        Assert.Equal(GrenadeKind.Concussion, scout.Class.SecondaryKind);
        Assert.Equal(new[] { 4, 0 }, soldier.Grenades);
    }

    [Fact]
    public void SecondaryKeyThrowsNapalmForAPyro()
    {
        var g = NewGame();
        var pyro = Human(g, Team.Red, PlayerClassId.Pyro);
        Place(g, pyro, new Vector3(0, 0, 800), yaw: MathF.PI);
        g.Tick(Dt);

        pyro.Input = new PlayerInput { SelectSlot = -1, Yaw = pyro.Yaw, Grenade2 = true };
        Run(g, 0.3f);
        Assert.Equal(1, pyro.Primed);
        pyro.Input.Grenade2 = false;
        g.Tick(Dt);
        Assert.Contains(g.Projectiles, p => p.Kind == ProjectileKind.Napalm);
        Assert.Equal(1, pyro.Grenades[1]);
        Assert.Equal(2, pyro.Grenades[0]);                         // frags untouched
    }

    [Fact]
    public void DetonationIgnitesEnemiesInRangeAndLeavesAFirePatch()
    {
        var g = NewMatch(out var pyro);
        var enemy = Human(g, Team.Blue, PlayerClassId.HeavyWeapons);
        var friend = Human(g, Team.Red, PlayerClassId.HeavyWeapons);
        var far = Human(g, Team.Blue, PlayerClassId.HeavyWeapons);
        Place(g, enemy, new Vector3(60, 0, 600));
        Place(g, friend, new Vector3(-60, 0, 600));
        Place(g, far, new Vector3(600, 0, 600));
        float enemyHp = enemy.Health;
        DropNapalm(g, pyro, new Vector3(0, 0, 600));
        Run(g, 0.2f);

        Assert.Single(g.FirePatches);
        Assert.InRange(g.FirePatches[0].Position.Y, -1f, 2f);
        Assert.True(enemy.BurnTime > 0);
        Assert.True(enemy.Health < enemyHp || enemy.Armor < enemy.Class.StartArmor, "initial burst hurts");
        Assert.Equal(0f, friend.BurnTime);
        Assert.Equal(0f, far.BurnTime);
    }

    [Fact]
    public void FirePatchKeepsBurningEnemiesWhoStayAndThenExpires()
    {
        var g = NewMatch(out var pyro);
        var enemy = Human(g, Team.Blue, PlayerClassId.HeavyWeapons);
        Place(g, enemy, new Vector3(0, 0, 600));
        DropNapalm(g, pyro, new Vector3(0, 0, 600));
        Run(g, 0.3f);
        enemy.Armor = 0;
        enemy.Health = 100;
        enemy.Input = new PlayerInput { SelectSlot = -1 };
        Run(g, 3f);
        Assert.True(enemy.Health < 40f, $"hp={enemy.Health}: roughly 16 dps from the patch plus burning");

        // After it burns out nothing keeps damaging him.
        enemy.Health = 100;
        Place(g, enemy, new Vector3(0, 0, 600));
        Run(g, 12f);
        Assert.Empty(g.FirePatches);
        enemy.BurnTime = 0;
        float hp = enemy.Health;
        Run(g, 2f);
        Assert.Equal(hp, enemy.Health);
    }

    [Fact]
    public void WalkingThroughIsALotLessBadThanStandingInIt()
    {
        var g = NewMatch(out var pyro);
        var stands = Human(g, Team.Blue, PlayerClassId.HeavyWeapons);
        var runs = Human(g, Team.Blue, PlayerClassId.HeavyWeapons);
        stands.Armor = runs.Armor = 0;
        DropNapalm(g, pyro, new Vector3(0, 0, 600));
        Place(g, stands, new Vector3(0, 0, 600));
        Place(g, runs, new Vector3(0, 0, 600));
        runs.Input = new PlayerInput { SelectSlot = -1, Forward = 1, Yaw = 0 };   // runs +Z out of the fire
        Run(g, 6f);
        Assert.True(stands.Health < runs.Health - 20f, $"standing {stands.Health} vs running {runs.Health}");
    }

    [Fact]
    public void BurnsEnemyStructures()
    {
        var g = NewMatch(out var pyro);
        var eng = Human(g, Team.Blue, PlayerClassId.Engineer);
        Place(g, eng, new Vector3(900, 0, 100));
        var sentry = new Sentry { Owner = eng, Team = Team.Blue, Position = new Vector3(40, 0, 600), BuildTimer = 0, Health = 40 };
        g.Sentries.Add(sentry);
        DropNapalm(g, pyro, new Vector3(0, 0, 600));
        Run(g, 8f);
        Assert.True(sentry.Dead);
    }

    [Fact]
    public void WallsBlockTheFireAndWaterPutsItOut()
    {
        var g = NewMatch(out var pyro);
        var behind = Human(g, Team.Blue, PlayerClassId.Scout);
        Place(g, behind, new Vector3(-250, 0, 1340));            // inside the red hall
        DropNapalm(g, pyro, new Vector3(-250, 0, 1250));         // just outside the front wall
        Run(g, 4f);
        Assert.Equal(0f, behind.BurnTime);
        Assert.True(behind.Health == behind.Class.MaxHealth);

        g.FirePatches.Clear();
        DropNapalm(g, pyro, new Vector3(0, -90, 0));             // in the river
        Run(g, 0.3f);
        Assert.Empty(g.FirePatches);
    }

    [Fact]
    public void ResetClearsFire()
    {
        var g = NewMatch(out var pyro);
        DropNapalm(g, pyro, new Vector3(0, 0, 600));
        Run(g, 0.2f);
        Assert.NotEmpty(g.FirePatches);
        g.ResetMatch();
        Assert.Empty(g.FirePatches);
    }

    [Fact]
    public void PyroBotsLobNapalm()
    {
        var g = NewGame(8);
        for (int i = 0; i < 4; i++)
        {
            g.AddBot(Team.Red, PlayerClassId.Pyro);
            g.AddBot(Team.Blue, PlayerClassId.Soldier);
        }
        var seen = new HashSet<Projectile>();
        int patches = 0;
        for (int i = 0; i < 60 * 300; i++)
        {
            g.Tick(Dt);
            foreach (var pr in g.Projectiles)
                if (pr.Kind == ProjectileKind.Napalm) seen.Add(pr);
            patches = Math.Max(patches, g.FirePatches.Count);
        }
        Assert.True(seen.Count >= 2, $"bots threw {seen.Count} napalm grenades");
        Assert.True(patches >= 1);
    }
}
