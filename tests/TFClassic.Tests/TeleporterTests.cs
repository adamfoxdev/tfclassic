using System.Numerics;
using TFClassic.Core;
using Xunit;
using static TFClassic.Tests.Helpers;

namespace TFClassic.Tests;

public class TeleporterTests
{
    static (Game g, Player eng) Setup()
    {
        var g = NewGame();
        var eng = Human(g, Team.Red, PlayerClassId.Engineer);
        Place(g, eng, new Vector3(0, 0, 900), yaw: MathF.PI);
        g.Tick(Dt);
        return (g, eng);
    }

    static Teleporter? BuildAt(Game g, Player eng, Vector3 standAt, TeleporterRole expected)
    {
        Place(g, eng, standAt, yaw: MathF.PI);
        g.Tick(Dt);
        eng.Input = new PlayerInput { SelectSlot = -1, Yaw = eng.Yaw, BuildTeleporter = true };
        g.Tick(Dt);
        eng.Input.BuildTeleporter = false;
        return g.TeleporterOf(eng, expected);
    }

    /// <summary>Builds an entrance and an exit and waits for both to come online.</summary>
    static (Teleporter entrance, Teleporter exit) BuildPair(Game g, Player eng)
    {
        var entrance = BuildAt(g, eng, new Vector3(0, 0, 900), TeleporterRole.Entrance);
        Assert.NotNull(entrance);
        eng.Metal = 200;
        var exit = BuildAt(g, eng, new Vector3(300, 0, 500), TeleporterRole.Exit);
        Assert.NotNull(exit);
        Run(g, 3.3f);
        return (entrance!, exit!);
    }

    [Fact]
    public void BuildsEntranceThenExitThenDemolishesBoth()
    {
        var (g, eng) = Setup();
        var entrance = BuildAt(g, eng, new Vector3(0, 0, 900), TeleporterRole.Entrance);
        Assert.NotNull(entrance);
        Assert.Equal(100, eng.Metal);
        Assert.Null(g.TeleporterOf(eng, TeleporterRole.Exit));
        Assert.True(entrance!.Building);

        eng.Metal = 200;
        var exit = BuildAt(g, eng, new Vector3(300, 0, 500), TeleporterRole.Exit);
        Assert.NotNull(exit);
        Assert.Equal(100, eng.Metal);

        Run(g, 3.3f);
        eng.Input = new PlayerInput { SelectSlot = -1, Yaw = eng.Yaw, BuildTeleporter = true };
        g.Tick(Dt);
        Assert.Empty(g.Teleporters);
        Assert.Equal(180, eng.Metal);
    }

    [Fact]
    public void NeedsMetal()
    {
        var (g, eng) = Setup();
        eng.Metal = 50;
        Assert.Null(BuildAt(g, eng, new Vector3(0, 0, 900), TeleporterRole.Entrance));
        Assert.Contains("metal", eng.Notice);
    }

    [Fact]
    public void TeammateSteppingOnTheEntranceArrivesAtTheExit()
    {
        var (g, eng) = Setup();
        var (entrance, exit) = BuildPair(g, eng);
        var friend = Human(g, Team.Red, PlayerClassId.Scout);
        Place(g, friend, entrance.Position);
        friend.Velocity = new Vector3(100, 0, 100);
        g.Tick(Dt);

        Assert.InRange(Vector3.Distance(new Vector3(friend.Position.X, 0, friend.Position.Z), new Vector3(exit.Position.X, 0, exit.Position.Z)), 0f, 5f);
        Assert.Equal(1, friend.TeleportCount);
        Assert.True(entrance.CooldownTimer > 2f);
    }

    [Fact]
    public void EnemiesCannotUseItAndNothingHappensWithoutAnExit()
    {
        var (g, eng) = Setup();
        var entrance = BuildAt(g, eng, new Vector3(0, 0, 900), TeleporterRole.Entrance)!;
        Run(g, 3.3f);

        var friend = Human(g, Team.Red, PlayerClassId.Scout);
        Place(g, friend, entrance.Position);
        Run(g, 0.5f);
        Assert.Equal(0, friend.TeleportCount);                    // no exit yet

        eng.Metal = 200;
        BuildAt(g, eng, new Vector3(300, 0, 500), TeleporterRole.Exit);
        Run(g, 3.3f);
        var enemy = Human(g, Team.Blue, PlayerClassId.Scout);
        Place(g, enemy, entrance.Position);
        Run(g, 0.5f);
        Assert.Equal(0, enemy.TeleportCount);
    }

    [Fact]
    public void CooldownLimitsUseAndExitIsOneWay()
    {
        var (g, eng) = Setup();
        var (entrance, exit) = BuildPair(g, eng);
        var friend = Human(g, Team.Red, PlayerClassId.Scout);
        Place(g, friend, entrance.Position);
        g.Tick(Dt);
        Assert.Equal(1, friend.TeleportCount);

        Place(g, friend, entrance.Position);          // immediately again: still cooling down
        Run(g, 1f);
        Assert.Equal(1, friend.TeleportCount);
        Run(g, 2.5f);
        Assert.Equal(2, friend.TeleportCount);

        var other = Human(g, Team.Red, PlayerClassId.Scout);
        Place(g, other, exit.Position);               // standing on the exit does nothing
        Run(g, 4f);
        Assert.Equal(0, other.TeleportCount);
    }

    [Fact]
    public void ArrivingTelefragsEnemiesOnTheExit()
    {
        var (g, eng) = Setup();
        var (entrance, exit) = BuildPair(g, eng);
        var victim = Human(g, Team.Blue, PlayerClassId.HeavyWeapons);
        Place(g, victim, exit.Position);
        var friend = Human(g, Team.Red, PlayerClassId.Scout);
        Place(g, friend, entrance.Position);
        g.Tick(Dt);
        Assert.False(victim.Alive);
        Assert.Equal(1, friend.Frags);
        Assert.Contains(g.Events, e => e.Text.Contains("Telefrag"));
    }

    [Fact]
    public void CarriedFlagComesAlong()
    {
        var (g, eng) = Setup();
        var (entrance, exit) = BuildPair(g, eng);
        var runner = Human(g, Team.Red, PlayerClassId.Scout);
        Place(g, runner, g.Flags[(int)Team.Blue].Home);
        g.Tick(Dt);
        Assert.NotNull(runner.CarryingFlag);
        Place(g, runner, entrance.Position);
        g.Tick(Dt);
        Assert.NotNull(runner.CarryingFlag);
        Assert.Equal(1, runner.TeleportCount);
    }

    [Fact]
    public void SabotagedOrBrokenTeleportersDoNotWork()
    {
        var (g, eng) = Setup();
        var (entrance, exit) = BuildPair(g, eng);
        exit.SabotageTimer = 4;
        exit.Saboteur = eng;
        var friend = Human(g, Team.Red, PlayerClassId.Scout);
        Place(g, friend, entrance.Position);
        Run(g, 1f);
        Assert.Equal(0, friend.TeleportCount);
    }

    [Fact]
    public void EnemiesCanDestroyThemAndSpiesSabotage()
    {
        var (g, eng) = Setup();
        var (entrance, exit) = BuildPair(g, eng);

        var spy = Human(g, Team.Blue, PlayerClassId.Spy);
        Place(g, spy, entrance.Position + new Vector3(0, 0, -50), yaw: 0);
        var to = entrance.Hull.Center - spy.Eye;
        spy.Input = new PlayerInput { SelectSlot = 0, Fire = true, Yaw = MathF.Atan2(to.X, to.Z), Pitch = MathF.Atan2(to.Y, MathF.Sqrt(to.X * to.X + to.Z * to.Z)) };
        g.Tick(Dt);
        spy.FireCooldown = 0;
        Run(g, 0.45f);
        spy.Input.Fire = false;
        Assert.True(entrance.Sabotaged);
        Run(g, 4.5f);
        Assert.True(entrance.Dead);

        var soldier = Human(g, Team.Blue, PlayerClassId.Soldier);
        Place(g, soldier, exit.Position + new Vector3(0, 0, -300));
        exit.Health = 30;
        for (int i = 0; i < 60 * 5 && !exit.Dead; i++)
        {
            var d = exit.Hull.Center - soldier.Eye;
            soldier.Input = new PlayerInput
            {
                SelectSlot = -1, Fire = true,
                Yaw = MathF.Atan2(d.X, d.Z), Pitch = MathF.Atan2(d.Y, MathF.Sqrt(d.X * d.X + d.Z * d.Z)),
            };
            g.Tick(Dt);
        }
        Assert.True(exit.Dead);
        g.Tick(Dt);
        Assert.Empty(g.Teleporters);
    }

    [Fact]
    public void WrenchRepairsAndCancelsSabotage()
    {
        var (g, eng) = Setup();
        var (entrance, exit) = BuildPair(g, eng);
        entrance.Health = 30;
        exit.SabotageTimer = 3;
        exit.Saboteur = eng;
        Place(g, eng, entrance.Position + new Vector3(0, 0, 50), yaw: MathF.PI);   // standing south of it, facing -Z
        var to = entrance.Hull.Center - eng.Eye;
        eng.Input = new PlayerInput { SelectSlot = 0, Fire = true, Yaw = MathF.Atan2(to.X, to.Z), Pitch = MathF.Atan2(to.Y, MathF.Sqrt(to.X * to.X + to.Z * to.Z)) };
        g.Tick(Dt);
        eng.FireCooldown = 0;
        Run(g, 0.5f);
        Assert.True(entrance.Health > 30);

        Place(g, eng, exit.Position + new Vector3(0, 0, 50), yaw: MathF.PI);
        to = exit.Hull.Center - eng.Eye;
        eng.Input = new PlayerInput { SelectSlot = 0, Fire = true, Yaw = MathF.Atan2(to.X, to.Z), Pitch = MathF.Atan2(to.Y, MathF.Sqrt(to.X * to.X + to.Z * to.Z)) };
        eng.FireCooldown = 0;
        Run(g, 0.5f);
        Assert.False(exit.Sabotaged);
    }

    [Fact]
    public void ClearedOnMatchReset()
    {
        var (g, eng) = Setup();
        BuildPair(g, eng);
        g.ResetMatch();
        Assert.Empty(g.Teleporters);
    }

    [Fact]
    public void EngineerBotBuildsAPairAndAttackersUseIt()
    {
        var g = NewGame(4);
        var bot = g.AddBot(Team.Red, PlayerClassId.Engineer);
        for (int i = 0; i < 4; i++) g.AddBot(Team.Red, PlayerClassId.Scout);
        for (int i = 0; i < 3; i++) g.AddBot(Team.Blue, PlayerClassId.Scout);

        int uses = 0;
        bool builtPair = false;
        for (int i = 0; i < 60 * 240; i++)
        {
            g.Tick(Dt);
            uses = g.Players.Sum(p => p.TeleportCount);
            builtPair |= g.TeleporterOf(bot, TeleporterRole.Entrance) != null && g.TeleporterOf(bot, TeleporterRole.Exit) != null;
        }
        Assert.True(builtPair, "the engineer bot should build both ends");
        Assert.True(uses >= 1, "teammates should have ridden the teleporter");
    }
}
