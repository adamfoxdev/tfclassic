using System.Numerics;
using TFClassic.Core;
using Xunit;
using static TFClassic.Tests.Helpers;

namespace TFClassic.Tests;

public class DetpackTests
{
    // Demoman (Red) at z=900 facing -Z; a detpack lands 56 units ahead at z≈844.
    static (Game g, Player demo) Setup()
    {
        var g = NewGame();
        var demo = Human(g, Team.Red, PlayerClassId.Demoman);
        Place(g, demo, new Vector3(0, 0, 900), yaw: MathF.PI);
        g.Tick(Dt);
        return (g, demo);
    }

    static Detpack? SetPack(Game g, Player demo, int fuseIndex = 0)
    {
        demo.DetpackFuseIndex = fuseIndex;
        demo.Input = new PlayerInput { SelectSlot = -1, Yaw = demo.Yaw, PlaceDetpack = true };
        g.Tick(Dt);
        demo.Input.PlaceDetpack = false;
        return g.DetpackOf(demo);
    }

    /// <summary>Moves the owner out of the blast so they survive to be asserted on.</summary>
    static void StepAway(Game g, Player demo) => Place(g, demo, new Vector3(900, 0, 1700), yaw: MathF.PI);

    [Fact]
    public void OnlyDemomenCarryOne()
    {
        var g = NewGame();
        Assert.Equal(1, Human(g, Team.Red, PlayerClassId.Demoman).Detpacks);
        Assert.Equal(0, Human(g, Team.Red, PlayerClassId.Soldier).Detpacks);
    }

    [Fact]
    public void SetsInFrontArmsAfterThreeSecondsAndIsSpent()
    {
        var (g, demo) = Setup();
        var pack = SetPack(g, demo, 0);
        Assert.NotNull(pack);
        Assert.Equal(0, demo.Detpacks);
        Assert.InRange(pack!.Position.Z, 900 - 56 - 1, 900 - 56 + 1);
        Assert.True(pack.Building, "still arming");
        Assert.Equal(5f, pack.Fuse);
        Run(g, 3.2f);
        Assert.False(pack.Building);

        // A second one isn't available.
        demo.Input.PlaceDetpack = true;
        g.Tick(Dt);
        Assert.Single(g.Detpacks);
    }

    [Fact]
    public void FuseCyclesFiveTwentyFifty()
    {
        var (g, demo) = Setup();
        Assert.Equal(1, demo.DetpackFuseIndex);                                 // default 20 s
        var seen = new List<float>();
        for (int i = 0; i < 3; i++)
        {
            demo.Input = new PlayerInput { SelectSlot = -1, Yaw = demo.Yaw, DetpackFuseNext = true };
            g.Tick(Dt);
            seen.Add(Detpack.Fuses[demo.DetpackFuseIndex]);
        }
        Assert.Equal(new[] { 50f, 5f, 20f }, seen);
    }

    [Fact]
    public void CanBePickedBackUpWhileArmingButNotAfter()
    {
        var (g, demo) = Setup();
        SetPack(g, demo);
        demo.Input.PlaceDetpack = true;
        g.Tick(Dt);
        Assert.Empty(g.Detpacks);
        Assert.Equal(1, demo.Detpacks);

        var again = SetPack(g, demo);
        Run(g, 3.2f);
        demo.Input.PlaceDetpack = true;
        g.Tick(Dt);
        Assert.NotNull(g.DetpackOf(demo));
        Assert.Contains("armed", demo.Notice);
    }

    [Fact]
    public void DetonatesOnTimerAndDevastatesEnemiesButNotTeammates()
    {
        var (g, demo) = Setup();
        var pack = SetPack(g, demo, 0)!;
        var enemy = Human(g, Team.Blue, PlayerClassId.HeavyWeapons);
        var friend = Human(g, Team.Red, PlayerClassId.HeavyWeapons);
        Place(g, enemy, pack.Position + new Vector3(0, 0, -200));   // well clear of the disarm radius
        Place(g, friend, pack.Position + new Vector3(0, 0, 200));
        StepAway(g, demo);
        float friendHp = friend.Health;
        Run(g, 8.5f);

        Assert.False(enemy.Alive, "600 damage at 200 units should kill a 100 hp HWGuy even with 300 armor");
        Assert.Equal(friendHp, friend.Health);
        Assert.Empty(g.Detpacks);
        Assert.Contains(g.Events, e => e.Text.Contains("Detpack"));
        Assert.True(demo.Frags >= 1);
    }

    [Fact]
    public void FarAwayAndWalledOffPlayersAreSafe()
    {
        var (g, demo) = Setup();
        var pack = SetPack(g, demo, 0)!;
        var far = Human(g, Team.Blue, PlayerClassId.Scout);
        Place(g, far, pack.Position + new Vector3(0, 0, -500));
        StepAway(g, demo);
        Run(g, 8.5f);
        Assert.Empty(g.Detpacks);                                  // it did go off...
        Assert.True(far.Alive);                                    // ...but 500 units is out of range

        // Behind the fortress wall: the blast needs line of sight.
        var (g2, demo2) = Setup();
        Place(g2, demo2, new Vector3(-250, 0, 1200), yaw: 0);    // facing +Z toward the front wall (z 1300)
        g2.Tick(Dt);
        var pack2 = SetPack(g2, demo2, 0)!;
        var inside = Human(g2, Team.Blue, PlayerClassId.Scout);
        Place(g2, inside, new Vector3(-250, 0, 1400));
        StepAway(g2, demo2);
        Run(g2, 8.5f);
        Assert.Empty(g2.Detpacks);
        Assert.True(inside.Alive);
    }

    [Fact]
    public void DestroysEnemySentriesInTheBlast()
    {
        var (g, demo) = Setup();
        var pack = SetPack(g, demo, 0)!;
        var eng = Human(g, Team.Blue, PlayerClassId.Engineer);
        Place(g, eng, new Vector3(900, 0, 100));
        var sentry = new Sentry { Owner = eng, Team = Team.Blue, Position = pack.Position + new Vector3(0, 0, -150), BuildTimer = 0 };
        g.Sentries.Add(sentry);
        StepAway(g, demo);
        Run(g, 8.5f);
        Assert.True(sentry.Dead);
    }

    [Fact]
    public void KnockbackIsCapped()
    {
        var (g, demo) = Setup();
        var victim = Human(g, Team.Blue, PlayerClassId.Scout);
        victim.Armor = 0;
        Place(g, victim, new Vector3(0, 0, 700));
        g.Detpacks.Add(new Detpack { Owner = demo, Team = Team.Red, Position = new Vector3(0, 0, 600), BuildTimer = 0, Fuse = 0.02f });
        g.Tick(Dt); g.Tick(Dt);
        Assert.True(victim.Velocity.Length() <= 1150f, $"speed {victim.Velocity.Length()}");
    }

    [Fact]
    public void EnemyLingeringBesideItDisarmsItWithoutAnExplosion()
    {
        var (g, demo) = Setup();
        var pack = SetPack(g, demo, 1)!;                         // 20 s fuse
        Run(g, 3.2f);
        StepAway(g, demo);
        var enemy = Human(g, Team.Blue, PlayerClassId.Scout);
        Place(g, enemy, pack.Position + new Vector3(30, 0, 0));
        float hp = enemy.Health;
        Run(g, 2.5f);
        Assert.NotNull(g.DetpackOf(demo));                       // not yet
        Run(g, 1f);
        Assert.Null(g.DetpackOf(demo));
        Assert.Equal(hp, enemy.Health);
        Assert.Equal(1, enemy.Frags);
        Assert.Contains(g.Events, e => e.Text.Contains("disarmed"));
    }

    [Fact]
    public void WalkingPastIsNotEnoughAndTeammatesDontDisarm()
    {
        var (g, demo) = Setup();
        var pack = SetPack(g, demo, 1)!;
        Run(g, 3.2f);
        StepAway(g, demo);
        var enemy = Human(g, Team.Blue, PlayerClassId.Scout);
        for (int i = 0; i < 4; i++)
        {
            Place(g, enemy, pack.Position + new Vector3(30, 0, 0));
            Run(g, 1f);
            Place(g, enemy, pack.Position + new Vector3(400, 0, 0));    // leaves; progress drains
            Run(g, 1f);
        }
        Assert.NotNull(g.DetpackOf(demo));

        var friend = Human(g, Team.Red, PlayerClassId.Scout);
        Place(g, friend, pack.Position + new Vector3(20, 0, 0));
        Run(g, 5f);
        Assert.NotNull(g.DetpackOf(demo));
    }

    [Fact]
    public void ShotApartOrKnifedByASpyItIsDefusedQuietly()
    {
        var (g, demo) = Setup();
        var pack = SetPack(g, demo, 1)!;
        Run(g, 3.2f);
        StepAway(g, demo);

        var soldier = Human(g, Team.Blue, PlayerClassId.Scout);
        Place(g, soldier, pack.Position + new Vector3(0, 0, -300));
        var to = pack.Hull.Center - soldier.Eye;
        soldier.Input = new PlayerInput
        {
            SelectSlot = 2, Fire = true,
            Yaw = MathF.Atan2(to.X, to.Z), Pitch = MathF.Atan2(to.Y, MathF.Sqrt(to.X * to.X + to.Z * to.Z)),
        };
        Run(g, 1.5f);
        Assert.Null(g.DetpackOf(demo));
        Assert.True(soldier.Alive);

        // A spy defuses another instantly with the knife.
        var (g2, demo2) = Setup();
        var pack2 = SetPack(g2, demo2, 1)!;
        Run(g2, 3.2f);
        StepAway(g2, demo2);
        var spy = Human(g2, Team.Blue, PlayerClassId.Spy);
        Place(g2, spy, pack2.Position + new Vector3(0, 0, -45), yaw: 0);
        var d = pack2.Hull.Center - spy.Eye;
        spy.Input = new PlayerInput { SelectSlot = 0, Fire = true, Yaw = MathF.Atan2(d.X, d.Z), Pitch = MathF.Atan2(d.Y, MathF.Sqrt(d.X * d.X + d.Z * d.Z)) };
        g2.Tick(Dt);
        spy.FireCooldown = 0;
        Run(g2, 0.3f);
        Assert.Null(g2.DetpackOf(demo2));
    }

    [Fact]
    public void LockerRestocksDetpackAndResetClearsThem()
    {
        var (g, demo) = Setup();
        SetPack(g, demo);
        Assert.Equal(0, demo.Detpacks);
        Place(g, demo, new Vector3(0, 0, 1500));                   // red hall: resupply room
        Run(g, 0.3f);
        Assert.Equal(1, demo.Detpacks);
        g.ResetMatch();
        Assert.Empty(g.Detpacks);
    }

    [Fact]
    public void DemomanBotsLayAmbushDetpacks()
    {
        var g = NewGame(7);
        for (int i = 0; i < 4; i++)
        {
            g.AddBot(Team.Red, PlayerClassId.Demoman);
            g.AddBot(Team.Blue, PlayerClassId.Soldier);
        }
        var seen = new HashSet<Detpack>();
        for (int i = 0; i < 60 * 300; i++)
        {
            g.Tick(Dt);
            foreach (var d in g.Detpacks) seen.Add(d);
        }
        Assert.True(seen.Count >= 2, $"bots set {seen.Count} detpacks");
    }
}
