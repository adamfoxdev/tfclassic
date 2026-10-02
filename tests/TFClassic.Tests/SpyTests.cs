using System.Numerics;
using TFClassic.Core;
using Xunit;
using static TFClassic.Tests.Helpers;

namespace TFClassic.Tests;

public class SpyTests
{
    // Spy (Red) stands at z=800; a Blue victim stands 50 units ahead (toward -Z).
    static (Game g, Player spy, Player victim) Setup(PlayerClassId victimClass = PlayerClassId.HeavyWeapons)
    {
        var g = NewGame();
        var spy = Human(g, Team.Red, PlayerClassId.Spy);
        var victim = Human(g, Team.Blue, victimClass);
        Place(g, spy, new Vector3(0, 0, 800), yaw: MathF.PI);
        Place(g, victim, new Vector3(0, 0, 750));
        victim.Armor = 0;
        g.Tick(Dt);
        return (g, spy, victim);
    }

    static void Stab(Game g, Player spy, Player victim)
    {
        var to = victim.Center - spy.Eye;
        spy.Input = new PlayerInput
        {
            SelectSlot = 0, Fire = true,
            Yaw = MathF.Atan2(to.X, to.Z), Pitch = MathF.Atan2(to.Y, MathF.Sqrt(to.X * to.X + to.Z * to.Z)),
        };
        g.Tick(Dt);
        spy.FireCooldown = 0;       // skip the weapon-switch delay
        g.Tick(Dt);
        spy.Input.Fire = false;
    }

    [Fact]
    public void BackstabKillsInOneHitFromBehind()
    {
        var (g, spy, victim) = Setup();
        victim.Input = new PlayerInput { SelectSlot = -1, Yaw = MathF.PI };   // facing -Z, away from the spy at +Z
        Stab(g, spy, victim);
        Assert.False(victim.Alive);
        Assert.Equal(1, spy.Frags);
        Assert.Contains(g.Events, e => e.Text.Contains("backstab"));
    }

    [Fact]
    public void StabbingFromTheFrontOnlyDoesKnifeDamage()
    {
        var (g, spy, victim) = Setup();
        victim.Input = new PlayerInput { SelectSlot = -1, Yaw = 0 };          // facing +Z, toward the spy
        Stab(g, spy, victim);
        Assert.True(victim.Alive);
        Assert.InRange(victim.Class.MaxHealth - victim.Health, 30f, 50f);
    }

    [Fact]
    public void DisguiseTakesTwoSecondsThenSentriesIgnoreTheSpy()
    {
        var g = NewGame();
        var eng = Human(g, Team.Blue, PlayerClassId.Engineer);
        var spy = Human(g, Team.Red, PlayerClassId.Spy);
        var sentry = new Sentry { Owner = eng, Team = Team.Blue, Position = new Vector3(0, 0, 500), BuildTimer = 0, Yaw = 0 };
        g.Sentries.Add(sentry);
        Place(g, eng, new Vector3(0, 0, 100));
        Place(g, spy, new Vector3(0, 0, 1900), yaw: MathF.PI);   // out of the sentry's range while changing clothes
        spy.Armor = 0;
        g.Tick(Dt);

        spy.Input = new PlayerInput { SelectSlot = -1, Yaw = MathF.PI, DisguiseNext = true };
        g.Tick(Dt);
        spy.Input.DisguiseNext = false;
        Assert.Equal(Team.Blue, spy.DisguiseTeam);
        Assert.False(spy.IsDisguised, "still changing clothes");
        Run(g, 2.1f);
        Assert.True(spy.IsDisguised);
        Assert.True(spy.IsDisguisedAs(Team.Blue));
        Assert.False(spy.IsTargetableBy(Team.Blue));

        Place(g, spy, new Vector3(0, 0, 650), yaw: MathF.PI);    // now walk into its field of fire
        spy.DisguiseTimer = 0;
        float hp = spy.Health;
        Run(g, 3f);
        Assert.Equal(hp, spy.Health);          // sentry never fired at the disguised spy
        Assert.Null(sentry.Target);
    }

    [Fact]
    public void SentryShootsASpyWhoIsNotDisguised()
    {
        var g = NewGame();
        var eng = Human(g, Team.Blue, PlayerClassId.Engineer);
        var spy = Human(g, Team.Red, PlayerClassId.Spy);
        g.Sentries.Add(new Sentry { Owner = eng, Team = Team.Blue, Position = new Vector3(0, 0, 500), BuildTimer = 0 });
        Place(g, spy, new Vector3(0, 0, 650));
        spy.Armor = 0;
        Run(g, 4f);
        Assert.True(spy.Health < spy.Class.MaxHealth || !spy.Alive);
    }

    [Fact]
    public void AttackingOrTakingDamageBlowsTheDisguise()
    {
        var (g, spy, victim) = Setup();
        g.StartDisguise(spy, Team.Blue, PlayerClassId.Soldier);
        spy.DisguiseTimer = 0;
        Assert.True(spy.IsDisguised);
        Stab(g, spy, victim);
        Assert.False(spy.DisguiseTeam.HasValue);

        g.StartDisguise(spy, Team.Blue, PlayerClassId.Scout);
        spy.DisguiseTimer = 0;
        g.Damage(spy, victim, 5f, "test", Vector3.Zero);
        Assert.False(spy.DisguiseTeam.HasValue);
    }

    [Fact]
    public void KnifeSabotagesAnEnemySentryAndItSelfDestructs()
    {
        var g = NewGame();
        var eng = Human(g, Team.Blue, PlayerClassId.Engineer);
        var spy = Human(g, Team.Red, PlayerClassId.Spy);
        var sentry = new Sentry { Owner = eng, Team = Team.Blue, Position = new Vector3(0, 0, 750), BuildTimer = 0 };
        g.Sentries.Add(sentry);
        Place(g, eng, new Vector3(300, 0, 100));
        Place(g, spy, new Vector3(0, 0, 800), yaw: MathF.PI);
        g.StartDisguise(spy, Team.Blue, PlayerClassId.Soldier);
        spy.DisguiseTimer = 0;
        g.Tick(Dt);

        var to = sentry.Hull.Center - spy.Eye;
        spy.Input = new PlayerInput { SelectSlot = 0, Fire = true, Yaw = MathF.Atan2(to.X, to.Z), Pitch = MathF.Atan2(to.Y, MathF.Sqrt(to.X * to.X + to.Z * to.Z)) };
        g.Tick(Dt);
        spy.FireCooldown = 0;
        g.Tick(Dt);
        spy.Input.Fire = false;
        Assert.True(sentry.Sabotaged);

        // A sabotaged sentry doesn't shoot.
        var enemy = Human(g, Team.Red, PlayerClassId.Scout);
        Place(g, enemy, new Vector3(0, 0, 900));
        enemy.Armor = 0;
        Run(g, 3f);
        Assert.Equal(enemy.Class.MaxHealth, enemy.Health);

        Run(g, 2f);
        Assert.True(sentry.Dead);
        Assert.True(spy.Frags >= 1);
    }

    [Fact]
    public void EngineerWrenchCancelsSabotage()
    {
        var g = NewGame();
        var eng = Human(g, Team.Blue, PlayerClassId.Engineer);
        var spy = Human(g, Team.Red, PlayerClassId.Spy);
        Place(g, eng, new Vector3(0, 0, 700), yaw: 0);
        var sentry = new Sentry { Owner = eng, Team = Team.Blue, Position = new Vector3(0, 0, 756), BuildTimer = 0, SabotageTimer = 3, Saboteur = spy };
        g.Sentries.Add(sentry);
        g.Tick(Dt);
        var to = sentry.Hull.Center - eng.Eye;
        eng.Input = new PlayerInput { SelectSlot = 0, Fire = true, Yaw = MathF.Atan2(to.X, to.Z), Pitch = MathF.Atan2(to.Y, MathF.Sqrt(to.X * to.X + to.Z * to.Z)) };
        g.Tick(Dt);
        Run(g, 0.6f);
        Assert.False(sentry.Sabotaged);
        Assert.False(sentry.Dead);
    }

    [Fact]
    public void TranquilizerSlowsTheTarget()
    {
        var (g, spy, victim) = Setup(PlayerClassId.Scout);
        var to = victim.Center - spy.Eye;
        spy.Input = new PlayerInput
        {
            SelectSlot = 2, Fire = true,
            Yaw = MathF.Atan2(to.X, to.Z), Pitch = MathF.Atan2(to.Y, MathF.Sqrt(to.X * to.X + to.Z * to.Z)),
        };
        Run(g, 0.5f);
        Assert.True(victim.SlowTime > 0);

        victim.Input = new PlayerInput { SelectSlot = -1, Forward = 1, Yaw = 0 };
        Run(g, 1f);
        Assert.InRange(new Vector2(victim.Velocity.X, victim.Velocity.Z).Length(), 100f, 220f);   // normally 400
    }

    [Fact]
    public void FeignDeathHidesFromSentriesAndEnds()
    {
        var g = NewGame();
        var eng = Human(g, Team.Blue, PlayerClassId.Engineer);
        var spy = Human(g, Team.Red, PlayerClassId.Spy);
        g.Sentries.Add(new Sentry { Owner = eng, Team = Team.Blue, Position = new Vector3(0, 0, 500), BuildTimer = 0 });
        Place(g, eng, new Vector3(300, 0, 100));
        Place(g, spy, new Vector3(0, 0, 650));
        spy.Armor = 0;
        g.Tick(Dt);

        spy.Input = new PlayerInput { SelectSlot = -1, Feign = true };
        g.Tick(Dt);
        spy.Input.Feign = false;
        Assert.True(spy.Feigning);
        Assert.Contains(g.Events, e => e.Text.Contains("tester died"));

        float hp = spy.Health;
        spy.Input = new PlayerInput { SelectSlot = -1, Forward = 1, Fire = true };   // can't move or shoot while "dead"
        var z = spy.Position.Z;
        Run(g, 3f);
        Assert.Equal(hp, spy.Health);
        Assert.InRange(MathF.Abs(spy.Position.Z - z), 0f, 1f);

        Run(g, 8f);
        Assert.False(spy.Feigning);
    }

    [Fact]
    public void CannotFeignDeathWhileCarryingTheFlag()
    {
        var g = NewGame();
        var spy = Human(g, Team.Red, PlayerClassId.Spy);
        Place(g, spy, g.Flags[(int)Team.Blue].Home);
        g.Tick(Dt);
        Assert.NotNull(spy.CarryingFlag);
        spy.Input = new PlayerInput { SelectSlot = -1, Feign = true };
        g.Tick(Dt);
        Assert.False(spy.Feigning);
    }
}
