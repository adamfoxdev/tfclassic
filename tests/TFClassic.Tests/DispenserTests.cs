using System.Numerics;
using TFClassic.Core;
using Xunit;
using static TFClassic.Tests.Helpers;

namespace TFClassic.Tests;

public class DispenserTests
{
    // Engineer faces -Z (yaw π) in open ground at z=800; things built appear 56 units ahead.
    static (Game g, Player eng) Setup(Team team = Team.Red)
    {
        var g = NewGame();
        var eng = Human(g, team, PlayerClassId.Engineer);
        Place(g, eng, new Vector3(0, 0, 800), yaw: MathF.PI);
        g.Tick(Dt);
        return (g, eng);
    }

    static Dispenser Build(Game g, Player eng)
    {
        eng.Input = new PlayerInput { SelectSlot = -1, Yaw = eng.Yaw, BuildDispenser = true };
        g.Tick(Dt);
        eng.Input.BuildDispenser = false;
        var d = g.DispenserOf(eng);
        Assert.NotNull(d);
        return d!;
    }

    static void Swing(Game g, Player p, Vector3 target, int swings = 1)
    {
        var to = target - p.Eye;
        p.Input = new PlayerInput
        {
            SelectSlot = 0, Fire = true,
            Yaw = MathF.Atan2(to.X, to.Z), Pitch = MathF.Atan2(to.Y, MathF.Sqrt(to.X * to.X + to.Z * to.Z)),
        };
        g.Tick(Dt);
        p.FireCooldown = 0;
        Run(g, 0.45f * swings);
        p.Input.Fire = false;
    }

    [Fact]
    public void BuildsInFrontForMetalAndComesOnlineAfterThreeSeconds()
    {
        var (g, eng) = Setup();
        var d = Build(g, eng);
        Assert.Equal(100, eng.Metal);
        Assert.InRange(d.Position.Z, 800 - 56 - 1, 800 - 56 + 1);
        Assert.True(d.Building);
        Run(g, 3.2f);
        Assert.False(d.Building);
    }

    [Fact]
    public void NeedsMetalAndDemolishRefundsPartOfIt()
    {
        var (g, eng) = Setup();
        eng.Metal = 60;
        eng.Input = new PlayerInput { SelectSlot = -1, Yaw = eng.Yaw, BuildDispenser = true };
        g.Tick(Dt);
        Assert.Null(g.DispenserOf(eng));
        Assert.Contains("metal", eng.Notice);

        eng.Metal = 200;
        eng.Input.BuildDispenser = false;
        g.Tick(Dt);
        Build(g, eng);
        g.Tick(Dt);
        eng.Input.BuildDispenser = true;
        g.Tick(Dt);
        Assert.Null(g.DispenserOf(eng));
        Assert.Equal(140, eng.Metal);
    }

    [Fact]
    public void SentryAndDispenserCannotOverlap()
    {
        var (g, eng) = Setup();
        eng.Input = new PlayerInput { SelectSlot = -1, Yaw = eng.Yaw, AltFire = true };
        g.Tick(Dt);
        eng.Input.AltFire = false;
        Assert.NotNull(g.SentryOf(eng));

        eng.Metal = 200;
        eng.Input = new PlayerInput { SelectSlot = -1, Yaw = eng.Yaw, BuildDispenser = true };   // same spot
        g.Tick(Dt);
        Assert.Null(g.DispenserOf(eng));
        Assert.Contains("room", eng.Notice);
    }

    [Fact]
    public void RestocksNearbyTeammatesOnly()
    {
        var (g, eng) = Setup();
        var d = Build(g, eng);
        Run(g, 3.2f);

        var friend = Human(g, Team.Red, PlayerClassId.Soldier);
        var farFriend = Human(g, Team.Red, PlayerClassId.Soldier);
        var enemy = Human(g, Team.Blue, PlayerClassId.Soldier);
        foreach (var p in new[] { friend, farFriend, enemy })
        {
            p.Ammo[(int)AmmoType.Rockets] = 0;
            p.Armor = 0;
        }
        Place(g, friend, d.Position + new Vector3(50, 0, 0));
        Place(g, farFriend, d.Position + new Vector3(0, 0, -400));
        Place(g, enemy, d.Position + new Vector3(-50, 0, 0));
        foreach (var p in new[] { friend, farFriend, enemy }) { p.Ammo[(int)AmmoType.Rockets] = 0; p.Armor = 0; }
        Run(g, 2.2f);

        Assert.True(friend.Ammo[(int)AmmoType.Rockets] >= 10);
        Assert.True(friend.Armor >= 20);
        Assert.Equal(0, farFriend.Ammo[(int)AmmoType.Rockets]);
        Assert.Equal(0, enemy.Ammo[(int)AmmoType.Rockets]);
        Assert.True(d.Store < Dispenser.MaxStore);
    }

    [Fact]
    public void WallsBlockTheDispenser()
    {
        var g = NewGame();
        var eng = Human(g, Team.Red, PlayerClassId.Engineer);
        // Dispenser inside the red hall, friend outside the front wall (z=1300..1316).
        var d = new Dispenser { Owner = eng, Team = Team.Red, Position = new Vector3(-250, 0, 1400), BuildTimer = 0 };
        g.Dispensers.Add(d);
        var friend = Human(g, Team.Red, PlayerClassId.Soldier);
        Place(g, friend, new Vector3(-250, 0, 1290));
        friend.Ammo[(int)AmmoType.Rockets] = 0;
        Run(g, 2.2f);
        Assert.Equal(0, friend.Ammo[(int)AmmoType.Rockets]);
    }

    [Fact]
    public void RunsDryThenWrenchRestocksIt()
    {
        var (g, eng) = Setup();
        var d = Build(g, eng);
        Run(g, 3.2f);
        d.Store = 0;
        var friend = Human(g, Team.Red, PlayerClassId.Soldier);
        Place(g, friend, d.Position + new Vector3(50, 0, 0));
        friend.Armor = 0;
        Run(g, 2.2f);
        Assert.Equal(0f, friend.Armor);

        eng.Metal = 100;
        d.Health = 50;
        Swing(g, eng, d.Hull.Center, 2);
        Assert.True(d.Store >= 100);
        Assert.True(d.Health > 50);
        Run(g, 1.2f);
        Assert.True(friend.Armor > 0, "dispensing resumes once restocked");
    }

    [Fact]
    public void EngineersGetMetalFromTheirDispenser()
    {
        var (g, eng) = Setup();
        var d = Build(g, eng);
        Run(g, 3.2f);
        eng.Metal = 0;
        Run(g, 3.2f);
        Assert.True(eng.Metal >= 40, $"metal={eng.Metal}");
    }

    [Fact]
    public void EnemiesCanDestroyItAndSpyCanSabotageIt()
    {
        var (g, eng) = Setup();
        var d = Build(g, eng);
        Run(g, 3.2f);

        var soldier = Human(g, Team.Blue, PlayerClassId.Soldier);
        Place(g, soldier, d.Position + new Vector3(0, 0, -300));
        Place(g, eng, new Vector3(400, 0, 900));    // out of the rocket splash
        d.Health = 40;
        for (int i = 0; i < 60 * 5 && !d.Dead; i++)
        {
            var to = d.Hull.Center - soldier.Eye;
            soldier.Input = new PlayerInput
            {
                SelectSlot = -1, Fire = true,
                Yaw = MathF.Atan2(to.X, to.Z), Pitch = MathF.Atan2(to.Y, MathF.Sqrt(to.X * to.X + to.Z * to.Z)),
            };
            g.Tick(Dt);
        }
        Assert.True(d.Dead);
        Assert.True(soldier.Frags >= 1);
        Assert.Null(g.DispenserOf(eng));

        // Fresh one: a spy sabotages it and it blows up unless wrenched.
        soldier.Input = new PlayerInput { SelectSlot = -1 };
        Place(g, eng, new Vector3(0, 0, 800), yaw: MathF.PI);
        g.Tick(Dt);
        eng.Metal = 200;
        eng.Input = new PlayerInput { SelectSlot = -1, Yaw = eng.Yaw, BuildDispenser = true };
        g.Tick(Dt);
        eng.Input.BuildDispenser = false;
        var d2 = g.DispenserOf(eng)!;
        Run(g, 3.2f);
        var spy = Human(g, Team.Blue, PlayerClassId.Spy);
        Place(g, spy, d2.Position + new Vector3(0, 0, -50), yaw: 0);
        Swing(g, spy, d2.Hull.Center);
        Assert.True(d2.Sabotaged);
        Run(g, 4.5f);
        Assert.True(d2.Dead);
    }

    [Fact]
    public void WrenchCancelsSabotage()
    {
        var (g, eng) = Setup();
        var d = Build(g, eng);
        Run(g, 3.2f);
        d.SabotageTimer = 3;
        d.Saboteur = eng;      // owner is irrelevant for the test
        Swing(g, eng, d.Hull.Center);
        Assert.False(d.Sabotaged);
    }

    [Fact]
    public void ClearedOnMatchReset()
    {
        var (g, eng) = Setup();
        Build(g, eng);
        g.ResetMatch();
        Assert.Empty(g.Dispensers);
    }

    [Fact]
    public void EngineerBotBuildsSentryThenDispenser()
    {
        var g = NewGame(4);
        var bot = g.AddBot(Team.Red, PlayerClassId.Engineer);
        Run(g, 120f);
        Assert.NotNull(g.SentryOf(bot));
        Assert.NotNull(g.DispenserOf(bot));
        Assert.True(g.SentryOf(bot)!.Level >= 2, $"level={g.SentryOf(bot)!.Level}");
    }
}
