namespace TFClassic.Core;

public enum Team { Red = 0, Blue = 1 }

public static class TeamExtensions
{
    public static Team Opposite(this Team t) => t == Team.Red ? Team.Blue : Team.Red;
}

public enum AmmoType { None = 0, Shells = 1, Nails = 2, Rockets = 3, Cells = 4 }

public enum WeaponId
{
    Crowbar, Medikit, Shotgun, Nailgun, SuperNailgun,
    RocketLauncher, GrenadeLauncher, PipebombLauncher,
    AssaultCannon, SniperRifle, AutoRifle, Flamethrower, Wrench, Knife, Tranquilizer,
}

public enum FireMode { Melee, Heal, Wrench, Backstab, Tranq, Hitscan, Rocket, Grenade, Pipe, Flame, SniperCharge }

public sealed record WeaponDef(
    WeaponId Id, string Name, FireMode Mode, AmmoType Ammo, int AmmoPerShot,
    float Cooldown, float Damage, int Pellets, float Spread, float Range,
    float ProjectileSpeed = 0, float Splash = 0);

public static class Weapons
{
    static readonly Dictionary<WeaponId, WeaponDef> Table = new[]
    {
        new WeaponDef(WeaponId.Crowbar, "Crowbar", FireMode.Melee, AmmoType.None, 0, 0.4f, 25, 1, 0, 70),
        new WeaponDef(WeaponId.Medikit, "Medikit", FireMode.Heal, AmmoType.None, 0, 0.4f, 20, 1, 0, 76),
        new WeaponDef(WeaponId.Wrench, "Wrench", FireMode.Wrench, AmmoType.None, 0, 0.45f, 22, 1, 0, 76),
        new WeaponDef(WeaponId.Knife, "Knife", FireMode.Backstab, AmmoType.None, 0, 0.8f, 40, 1, 0, 72),
        new WeaponDef(WeaponId.Tranquilizer, "Tranquilizer", FireMode.Tranq, AmmoType.Nails, 1, 0.8f, 8, 1, 0.01f, 2500),
        new WeaponDef(WeaponId.Shotgun, "Shotgun", FireMode.Hitscan, AmmoType.Shells, 1, 0.55f, 4, 6, 0.06f, 3000),
        new WeaponDef(WeaponId.Nailgun, "Nailgun", FireMode.Hitscan, AmmoType.Nails, 1, 0.1f, 9, 1, 0.025f, 3000),
        new WeaponDef(WeaponId.SuperNailgun, "Super Nailgun", FireMode.Hitscan, AmmoType.Nails, 2, 0.1f, 14, 1, 0.03f, 3000),
        new WeaponDef(WeaponId.RocketLauncher, "Rocket Launcher", FireMode.Rocket, AmmoType.Rockets, 1, 0.8f, 100, 1, 0, 0, 900, 160),
        new WeaponDef(WeaponId.GrenadeLauncher, "Grenade Launcher", FireMode.Grenade, AmmoType.Rockets, 1, 0.65f, 90, 1, 0, 0, 800, 180),
        new WeaponDef(WeaponId.PipebombLauncher, "Pipebomb Launcher", FireMode.Pipe, AmmoType.Rockets, 1, 0.6f, 80, 1, 0, 0, 700, 170),
        new WeaponDef(WeaponId.AssaultCannon, "Assault Cannon", FireMode.Hitscan, AmmoType.Shells, 1, 0.07f, 7, 1, 0.07f, 3000),
        new WeaponDef(WeaponId.SniperRifle, "Sniper Rifle", FireMode.SniperCharge, AmmoType.Shells, 1, 1.4f, 50, 1, 0, 8000),
        new WeaponDef(WeaponId.AutoRifle, "Auto Rifle", FireMode.Hitscan, AmmoType.Shells, 1, 0.15f, 8, 1, 0.012f, 5000),
        new WeaponDef(WeaponId.Flamethrower, "Flamethrower", FireMode.Flame, AmmoType.Cells, 1, 0.1f, 9, 1, 0, 260),
    }.ToDictionary(w => w.Id);

    public static WeaponDef Get(WeaponId id) => Table[id];
}

public enum PlayerClassId { Scout, Soldier, Demoman, HeavyWeapons, Sniper, Medic, Pyro, Engineer, Spy }

public sealed record ClassDef(
    PlayerClassId Id, string Name, int MaxHealth, int MaxArmor, int StartArmor, float Speed,
    WeaponId[] Slots, int DefaultSlot,
    int[] MaxAmmo /* indexed by AmmoType */,
    int MaxMetal = 0);

public static class Classes
{
    // MaxAmmo order: None, Shells, Nails, Rockets, Cells
    public static readonly ClassDef[] All =
    {
        new(PlayerClassId.Scout, "Scout", 75, 50, 25, 400,
            new[] { WeaponId.Crowbar, WeaponId.Shotgun, WeaponId.Nailgun }, 2, new[] { 0, 50, 200, 0, 0 }),
        new(PlayerClassId.Soldier, "Soldier", 100, 200, 100, 240,
            new[] { WeaponId.Crowbar, WeaponId.Shotgun, WeaponId.RocketLauncher }, 2, new[] { 0, 100, 0, 50, 0 }),
        new(PlayerClassId.Demoman, "Demoman", 90, 120, 50, 280,
            new[] { WeaponId.Shotgun, WeaponId.GrenadeLauncher, WeaponId.PipebombLauncher }, 1, new[] { 0, 75, 0, 50, 0 }),
        new(PlayerClassId.HeavyWeapons, "HWGuy", 100, 300, 150, 230,
            new[] { WeaponId.Crowbar, WeaponId.Shotgun, WeaponId.AssaultCannon }, 2, new[] { 0, 200, 0, 0, 0 }),
        new(PlayerClassId.Sniper, "Sniper", 90, 50, 0, 250,
            new[] { WeaponId.Crowbar, WeaponId.SniperRifle, WeaponId.AutoRifle }, 1, new[] { 0, 75, 0, 0, 0 }),
        new(PlayerClassId.Medic, "Medic", 90, 100, 50, 320,
            new[] { WeaponId.Medikit, WeaponId.Shotgun, WeaponId.SuperNailgun }, 2, new[] { 0, 75, 150, 0, 0 }),
        new(PlayerClassId.Pyro, "Pyro", 100, 150, 50, 300,
            new[] { WeaponId.Crowbar, WeaponId.Shotgun, WeaponId.Flamethrower }, 2, new[] { 0, 40, 0, 0, 200 }),
        new(PlayerClassId.Engineer, "Engineer", 80, 50, 25, 300,
            new[] { WeaponId.Wrench, WeaponId.Shotgun, WeaponId.Nailgun }, 1, new[] { 0, 50, 100, 0, 0 }, MaxMetal: 200),
        new(PlayerClassId.Spy, "Spy", 90, 100, 25, 300,
            new[] { WeaponId.Knife, WeaponId.Shotgun, WeaponId.Tranquilizer }, 1, new[] { 0, 40, 60, 0, 0 }),
    };

    public static ClassDef Get(PlayerClassId id) => All[(int)id];
}
