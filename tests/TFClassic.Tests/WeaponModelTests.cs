using System.Numerics;
using TFClassic.Core;
using TFClassic.Desktop;
using Xunit;
using static TFClassic.Tests.Helpers;

namespace TFClassic.Tests;

public class WeaponModelTests
{
    public static IEnumerable<object[]> AllWeapons() => Enum.GetValues<WeaponId>().Select(id => new object[] { id });

    [Theory]
    [MemberData(nameof(AllWeapons))]
    public void EveryWeaponHasAWellFormedModel(WeaponId id)
    {
        Assert.True(WeaponModels.Models.TryGetValue(id, out var model), $"{id} has no model, so nobody would see it in anyone's hands");
        Assert.NotEmpty(model!.Parts);
        foreach (var part in model.Parts)
        {
            Assert.True(part.Min.X < part.Max.X && part.Min.Y < part.Max.Y && part.Min.Z < part.Max.Z, $"{id} has an inside-out part");
            Assert.InRange(part.Max.Z - part.Min.Z, 0.1f, 40f);
        }

        // The barrel points toward -Z, so the weapon's far end is its most negative Z.
        float farthest = model.Parts.Min(p => p.Min.Z);
        Assert.True(farthest < -8f, $"{id} should extend forward of the grip");
        if (!model.Melee)
        {
            Assert.NotNull(model.Muzzle);
            Assert.InRange(model.Muzzle!.Value.Z, farthest - 2f, farthest + 2f);      // flash comes out of the front
        }
        Assert.Equal(WeaponAnimator.IsMelee(Weapons.Get(id).Mode), model.Melee);       // animator and model agree on swinging vs flashing
    }

    [Fact]
    public void EveryGrenadeTypeHasADistinctColour()
    {
        var colours = Enum.GetValues<GrenadeKind>().Select(k => WeaponModels.GrenadeColor(k)).ToList();
        Assert.Equal(colours.Count, colours.Select(c => (c.R, c.G, c.B)).Distinct().Count());
    }

    [Fact]
    public void PlayersHoldTheirRealWeaponNormally()
    {
        var g = NewGame();
        var soldier = Human(g, Team.Red, PlayerClassId.Soldier);
        var viewer = Human(g, Team.Blue, PlayerClassId.Scout);
        Assert.Equal(WeaponId.RocketLauncher, WeaponModels.HeldBy(soldier, viewer));
        soldier.Slot = 1;
        Assert.Equal(WeaponId.Shotgun, WeaponModels.HeldBy(soldier, viewer));
    }

    [Fact]
    public void ADisguisedSpyHoldsTheDisguisedClassesWeaponForEnemiesOnly()
    {
        var g = NewGame();
        var spy = Human(g, Team.Red, PlayerClassId.Spy);
        var enemy = Human(g, Team.Blue, PlayerClassId.Scout);
        var friend = Human(g, Team.Red, PlayerClassId.Scout);
        spy.Slot = 0;                                                    // knife out

        g.StartDisguise(spy, Team.Blue, PlayerClassId.Soldier);
        Assert.Equal(WeaponId.Knife, WeaponModels.HeldBy(spy, enemy));   // still changing clothes: no fooling yet
        spy.DisguiseTimer = 0;
        Assert.Equal(WeaponId.RocketLauncher, WeaponModels.HeldBy(spy, enemy));   // a Soldier's main weapon
        Assert.Equal(WeaponId.Knife, WeaponModels.HeldBy(spy, friend));            // teammates see the truth

        g.StartDisguise(spy, Team.Blue, PlayerClassId.Demoman);
        spy.DisguiseTimer = 0;
        Assert.Equal(WeaponId.GrenadeLauncher, WeaponModels.HeldBy(spy, enemy));   // Demoman's default slot is the grenade launcher
    }
}
