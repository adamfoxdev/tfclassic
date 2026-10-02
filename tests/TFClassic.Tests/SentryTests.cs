using System.Numerics;
using TFClassic.Core;
using Xunit;
using static TFClassic.Tests.Helpers;

namespace TFClassic.Tests;

public class SentryTests
{
    // Engineer stands in open ground facing -Z (yaw π); the sentry appears 56 units ahead.
    static (Game g, Player eng) Setup(Team team = Team.Red)
    {
        var g = NewGame();
        var eng = Human(g, team, PlayerClassId.Engineer);
        Place(g, eng, new Vector3(0, 0, 800), yaw: MathF.PI);
        g.Tick(Dt);                       // settle onto the ground
        return (g, eng);
    }

    static void Press(Game g, Player p, Action<Player> set)
    {
        set(p);
        g.Tick(Dt);
    }

    static Sentry Build(Game g, Player eng)
    {
        eng.Input = new PlayerInput { SelectSlot = -1, Yaw = eng.Yaw, AltFire = true };
        g.Tick(Dt);
        eng.Input.AltFire = false;
        var s = g.SentryOf(eng);
        Assert.NotNull(s);
        return s!;
    }

    static void Wrench(Game g, Player eng, Sentry s, int swings)
    {
        var to = s.Hull.Center - eng.Eye;
        eng.Input = new PlayerInput
        {
            SelectSlot = 0, Fire = true,
            Yaw = MathF.Atan2(to.X, to.Z), Pitch = MathF.Atan2(to.Y, MathF.Sqrt(to.X * to.X + to.Z * to.Z)),
        };
        g.Tick(Dt);                                  // weapon switch delay
        Run(g, 0.5f * swings);
        eng.Input.Fire = false;
    }

    [Fact]
    public void EngineerBuildsASentryInFrontForMetal()
    {
        var (g, eng) = Setup();
        Assert.Equal(200, eng.Metal);
        var s = Build(g, eng);

        Assert.Equal(70, eng.Metal);
        Assert.InRange(s.Position.Z, 800 - 56 - 1, 800 - 56 + 1);
        Assert.Equal(Team.Red, s.Team);
        Assert.True(s.Building);
        Run(g, 3.2f);
        Assert.False(s.Building);
    }

    [Fact]
    public void CannotBuildWithoutMetalOrRoom()
    {
        var (g, eng) = Setup();
        eng.Metal = 100;
        eng.Input = new PlayerInput { SelectSlot = -1, Yaw = eng.Yaw, AltFire = true };
        g.Tick(Dt);
        Assert.Null(g.SentryOf(eng));
        Assert.Contains("metal", eng.Notice);

        // Facing the boundary wall at point-blank leaves no room.
        eng.Metal = 200;
        eng.Input.AltFire = false;
        g.Tick(Dt);
        Place(g, eng, new Vector3(0, 0, 1985), yaw: 0);
        g.Tick(Dt);
        eng.Input = new PlayerInput { SelectSlot = -1, Yaw = 0, AltFire = true };
        g.Tick(Dt);
        Assert.Null(g.SentryOf(eng));
        Assert.Equal(200, eng.Metal);
    }

    [Fact]
    public void AltFireAgainDemolishesWithPartialRefund()
    {
        var (g, eng) = Setup();
        Build(g, eng);
        g.Tick(Dt);
        Press(g, eng, p => p.Input.AltFire = true);
        Assert.Null(g.SentryOf(eng));
        Assert.Equal(120, eng.Metal);
    }

    [Fact]
    public void SentryKillsEnemyInRangeButIgnoresTeammates()
    {
        var (g, eng) = Setup();
        var s = Build(g, eng);
        Run(g, 3.2f);

        var enemy = Human(g, Team.Blue, PlayerClassId.Scout);
        var friend = Human(g, Team.Red, PlayerClassId.Scout);
        Place(g, friend, new Vector3(40, 0, 500));
        Place(g, enemy, new Vector3(0, 0, 450));
        enemy.Armor = 0;
        float friendHp = friend.Health;
        Run(g, 6f);

        Assert.False(enemy.Alive);
        Assert.Equal(friendHp, friend.Health);
        Assert.True(eng.Frags >= 1, "owner is credited with sentry kills");
        Assert.True(s.Ammo < s.MaxAmmo);
        Assert.Contains(g.Events, e => e.Text.Contains("Sentry Gun"));
    }

    [Fact]
    public void SentryNeedsLineOfSightAndRange()
    {
        var (g, eng) = Setup();
        var s = Build(g, eng);
        Run(g, 3.2f);

        var far = Human(g, Team.Blue, PlayerClassId.HeavyWeapons);
        Place(g, far, new Vector3(0, 0, 800 - s.Range - 400));
        far.Armor = 0;
        Run(g, 3f);
        Assert.Equal(far.Class.MaxHealth, far.Health);

        // Behind the cover wall at z=850-866 (x -300..-150): sentry stands at x=0, so use a wall that blocks.
        var (g2, eng2) = Setup();
        Place(g2, eng2, new Vector3(-225, 0, 900), yaw: MathF.PI);   // wall is just in front (z 850..866)
        var wallSentry = new Sentry { Owner = eng2, Team = Team.Red, Position = new Vector3(-225, 0, 900), BuildTimer = 0 };
        g2.Sentries.Add(wallSentry);
        var enemy = Human(g2, Team.Blue, PlayerClassId.HeavyWeapons);
        Place(g2, enemy, new Vector3(-225, 0, 780));
        enemy.Armor = 0;
        Run(g2, 3f);
        Assert.Equal(enemy.Class.MaxHealth, enemy.Health);
    }

    [Fact]
    public void WrenchUpgradesToLevelThreeAndRocketsFire()
    {
        var (g, eng) = Setup();
        var s = Build(g, eng);
        Run(g, 3.2f);
        Wrench(g, eng, s, 1);
        Assert.Equal(1, s.Level);                // 70 metal left: not enough to upgrade
        Assert.Contains("metal", eng.Notice);
        eng.Metal = 100;
        Wrench(g, eng, s, 1);
        Assert.Equal(2, s.Level);
        Assert.Equal(180, s.MaxHealth);
        eng.Metal = 100;
        Wrench(g, eng, s, 1);
        Assert.Equal(3, s.Level);
        Assert.Equal(20, s.Rockets);
        Assert.Equal(0, eng.Metal);

        var enemy = Human(g, Team.Blue, PlayerClassId.HeavyWeapons);
        Place(g, enemy, new Vector3(0, 0, 500));
        enemy.Armor = 0;
        eng.Input = new PlayerInput { SelectSlot = -1 };
        Run(g, 2f);
        Assert.True(s.Rockets < 20, "level 3 sentry fires rockets");
    }

    [Fact]
    public void WrenchRepairsDamageAndRefillsAmmo()
    {
        var (g, eng) = Setup();
        var s = Build(g, eng);
        Run(g, 3.2f);
        eng.Metal = 100;
        s.Level = 3;
        s.Health = 50;
        s.Ammo = 0;
        Wrench(g, eng, s, 3);
        Assert.True(s.Health > 120, $"hp={s.Health}");
        Assert.True(s.Ammo >= 60);
        Assert.True(eng.Metal < 100);
    }

    [Fact]
    public void EnemiesCanDestroySentryAndGetCredit()
    {
        var (g, eng) = Setup();
        var s = Build(g, eng);
        Run(g, 3.2f);
        var soldier = Human(g, Team.Blue, PlayerClassId.Soldier);
        Place(g, soldier, new Vector3(0, 0, 800 - 56 - 400));   // beyond nothing: just shoot it from range
        var to = s.Hull.Center - soldier.Eye;
        eng.Input = new PlayerInput { SelectSlot = -1 };
        s.Health = 60;
        for (int i = 0; i < 60 * 6 && !s.Dead; i++)
        {
            to = s.Hull.Center - soldier.Eye;
            soldier.Input = new PlayerInput
            {
                SelectSlot = -1, Fire = true,
                Yaw = MathF.Atan2(to.X, to.Z), Pitch = MathF.Atan2(to.Y, MathF.Sqrt(to.X * to.X + to.Z * to.Z)),
            };
            soldier.Health = 100;   // keep the attacker alive; this test is about the sentry taking damage
            soldier.Armor = 200;
            g.Tick(Dt);
        }
        Assert.True(s.Dead);
        Assert.Null(g.SentryOf(eng));
        Assert.True(soldier.Frags >= 1);
    }

    [Fact]
    public void SentriesAreClearedWhenMatchResets()
    {
        var (g, eng) = Setup();
        Build(g, eng);
        g.ResetMatch();
        Assert.Empty(g.Sentries);
    }

    [Fact]
    public void EngineerBotBuildsAndUpgradesASentry()
    {
        var g = NewGame(4);
        var bot = g.AddBot(Team.Red, PlayerClassId.Engineer);
        Run(g, 90f);
        var s = g.SentryOf(bot);
        Assert.NotNull(s);
        Assert.True(s!.Level >= 2, $"level={s.Level} metal={bot.Metal}");
    }
}
