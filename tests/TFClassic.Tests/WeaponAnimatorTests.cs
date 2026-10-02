using System.Numerics;
using TFClassic.Core;
using Xunit;
using static TFClassic.Tests.Helpers;

namespace TFClassic.Tests;

public class WeaponAnimatorTests
{
    /// <summary>Ticks the game and the animator together, like the client does.</summary>
    static void Step(Game g, WeaponAnimator a, Player p, float seconds)
    {
        int ticks = (int)(seconds / Dt);
        for (int i = 0; i < ticks; i++)
        {
            g.Tick(Dt);
            a.Update(p, Dt);
        }
    }

    static (Game g, Player p, WeaponAnimator a) Setup(PlayerClassId cls, int slot = -1)
    {
        var g = NewGame();
        var p = Human(g, Team.Red, cls);
        Place(g, p, new Vector3(0, 0, 700), yaw: MathF.PI);
        var a = new WeaponAnimator();
        p.Input = new PlayerInput { SelectSlot = slot, Yaw = p.Yaw };
        Step(g, a, p, 0.8f);                                  // settle: draw-in finished, cooldowns cleared
        return (g, p, a);
    }

    [Fact]
    public void RespawnRaisesTheWeaponAndDeathClearsEverything()
    {
        var g = NewGame();
        var p = Human(g, Team.Red, PlayerClassId.Soldier);
        var a = new WeaponAnimator();
        a.Update(p, Dt);
        Assert.True(a.Draw > 0.3f, "a fresh spawn raises the weapon");

        a.TriggerFire(p.Weapon);
        g.Kill(p, null, "test");
        a.Update(p, Dt);
        Assert.Equal(0f, a.Kick);
        Assert.Equal(0f, a.Flash);
        Assert.Equal(0f, a.Draw);
    }

    [Fact]
    public void FiringKicksFlashesAndFadesAway()
    {
        var (g, p, a) = Setup(PlayerClassId.Soldier);
        Assert.Equal(0f, a.Kick);

        p.Input = new PlayerInput { SelectSlot = -1, Yaw = p.Yaw, Fire = true };
        Step(g, a, p, 0.05f);
        p.Input.Fire = false;
        Assert.True(a.Kick > 0.5f, $"kick {a.Kick}");
        Assert.True(a.Flash > 0f, "firearms flash");
        Assert.Equal(0f, a.Swing);

        Step(g, a, p, 0.6f);
        Assert.Equal(0f, a.Kick);
        Assert.Equal(0f, a.Flash);
    }

    [Fact]
    public void MeleeSwingsInsteadOfFlashing()
    {
        var (g, p, a) = Setup(PlayerClassId.Soldier, slot: 0);          // crowbar
        p.Input = new PlayerInput { SelectSlot = -1, Yaw = p.Yaw, Fire = true };
        Step(g, a, p, 0.1f);
        p.Input.Fire = false;
        Assert.True(a.Swing > 0.5f, $"swing {a.Swing}");
        Assert.Equal(0f, a.Flash);

        Step(g, a, p, 0.6f);
        Assert.Equal(0f, a.Swing);
    }

    [Fact]
    public void SwitchingWeaponsPlaysTheDrawInButIsNotMistakenForAShot()
    {
        var (g, p, a) = Setup(PlayerClassId.Soldier, slot: 2);
        p.Input = new PlayerInput { SelectSlot = 1, Yaw = p.Yaw };       // to the shotgun
        Step(g, a, p, 0.03f);
        Assert.True(a.Draw > 0.2f, $"draw {a.Draw}");
        Assert.Equal(0f, a.Kick);                                        // the switch delay sets a cooldown, but that's not a shot
        Assert.Equal(0f, a.Flash);
        Step(g, a, p, 0.4f);
        Assert.Equal(0f, a.Draw);
    }

    [Fact]
    public void SniperShotAlsoCounts()
    {
        var (g, p, a) = Setup(PlayerClassId.Sniper, slot: 1);
        p.Input = new PlayerInput { SelectSlot = -1, Yaw = p.Yaw, Fire = true };
        Step(g, a, p, 0.5f);                                             // charging: nothing yet
        Assert.Equal(0f, a.Kick);
        p.Input.Fire = false;
        Step(g, a, p, 0.05f);
        Assert.True(a.Kick > 0.5f);
        Assert.True(a.Flash > 0f);
    }

    [Fact]
    public void CookingAndThrowingAGrenadeAnimates()
    {
        var (g, p, a) = Setup(PlayerClassId.Pyro);
        p.Input = new PlayerInput { SelectSlot = -1, Yaw = p.Yaw, Grenade2 = true };
        Step(g, a, p, 0.1f);
        Assert.True(p.Primed >= 0);
        Assert.True(a.Draw > 0f, "the grenade is raised");

        p.Input.Grenade2 = false;
        Step(g, a, p, 0.05f);
        Assert.True(a.Thrown > 0f);
        Assert.Equal(GrenadeKind.Napalm, a.ThrownKind);

        Step(g, a, p, 0.5f);
        Assert.Equal(0f, a.Thrown);
    }

    [Fact]
    public void WalkingBobsAndStandingStillSettles()
    {
        var (g, p, a) = Setup(PlayerClassId.Scout);
        Assert.InRange(a.BobAmount, 0f, 0.05f);

        p.Input = new PlayerInput { SelectSlot = -1, Yaw = p.Yaw, Forward = 1 };
        Step(g, a, p, 0.6f);
        float phase = a.BobPhase;
        Assert.True(a.BobAmount > 0.7f, $"bob {a.BobAmount}");
        Step(g, a, p, 0.2f);
        Assert.True(a.BobPhase > phase, "the bob cycle advances while running");

        p.Input = new PlayerInput { SelectSlot = -1, Yaw = p.Yaw };
        Step(g, a, p, 1.0f);
        Assert.True(a.BobAmount < 0.1f);
    }

    [Fact]
    public void TurningSwaysTheWeaponAndItSettlesBack()
    {
        var (g, p, a) = Setup(PlayerClassId.Soldier);
        p.Input = new PlayerInput { SelectSlot = -1, Yaw = p.Yaw + 0.4f };        // a sharp turn
        Step(g, a, p, 0.02f);
        Assert.True(MathF.Abs(a.SwayYaw) > 0.5f, $"sway {a.SwayYaw}");
        Step(g, a, p, 0.8f);
        Assert.InRange(MathF.Abs(a.SwayYaw), 0f, 0.05f);
    }

    [Fact]
    public void FrozenAnimatorDoesNotAdvance()
    {
        var (g, p, a) = Setup(PlayerClassId.Soldier);
        a.TriggerFire(p.Weapon);
        a.Frozen = true;
        float kick = a.Kick;
        Step(g, a, p, 0.5f);
        Assert.Equal(kick, a.Kick);
    }

    [Fact]
    public void MeleeClassificationMatchesTheWeaponModes()
    {
        Assert.True(WeaponAnimator.IsMelee(FireMode.Melee));
        Assert.True(WeaponAnimator.IsMelee(FireMode.Heal));
        Assert.True(WeaponAnimator.IsMelee(FireMode.Wrench));
        Assert.True(WeaponAnimator.IsMelee(FireMode.Backstab));
        Assert.False(WeaponAnimator.IsMelee(FireMode.Hitscan));
        Assert.False(WeaponAnimator.IsMelee(FireMode.Rocket));
        Assert.False(WeaponAnimator.IsMelee(FireMode.Flame));
    }
}
