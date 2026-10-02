using System.Numerics;
using Raylib_cs;
using TFClassic.Core;

namespace TFClassic.Desktop;

/// <summary>
/// The blocky 3D model of every weapon, shared by the first-person view model and by what other players hold.
/// Weapon space: +X right, +Y up, -Z forward (the barrel points toward -Z), origin at the grip.
/// </summary>
internal static class WeaponModels
{
    internal readonly record struct Part(Vector3 Min, Vector3 Max, Color Color);

    internal sealed record WeaponModel(Part[] Parts, Vector3? Muzzle, float Kick, float KickPitch, bool Melee, Vector3 Rest, float Roll = 0);

    internal static readonly Color Gun = new(64, 66, 74, 255), Dark = new(38, 40, 46, 255), Steel = new(150, 155, 165, 255),
        Wood = new(122, 82, 46, 255), Olive = new(88, 104, 62, 255), Red = new(188, 52, 46, 255),
        Orange = new(238, 128, 40, 255), White = new(232, 232, 236, 255), Yellow = new(236, 200, 70, 255),
        Silver = new(205, 210, 218, 255), Black = new(26, 26, 30, 255), Skin = new(222, 184, 150, 255);

    /// <summary>Body colour of a grenade by type (also used for the live grenade in a cooking player's hand).</summary>
    internal static Color GrenadeColor(GrenadeKind kind) => kind switch
    {
        GrenadeKind.Frag => new Color(62, 104, 62, 255),
        GrenadeKind.Concussion => new Color(90, 190, 230, 255),
        GrenadeKind.Napalm => new Color(235, 130, 40, 255),
        GrenadeKind.Caltrops => new Color(150, 150, 160, 255),
        GrenadeKind.Nail => new Color(200, 200, 212, 255),
        GrenadeKind.Mirv => new Color(170, 60, 50, 255),
        GrenadeKind.Gas => new Color(130, 200, 70, 255),
        _ => new Color(90, 140, 255, 255),
    };

    /// <summary>
    /// The weapon other players see in <paramref name="holder"/>'s hands: normally the real one, but a fully
    /// disguised spy holds whatever the disguised class would, so the gun doesn't give the disguise away.
    /// </summary>
    internal static WeaponId HeldBy(Player holder, Player viewer)
    {
        if (holder.IsDisguised && viewer.Team != holder.Team)
        {
            var cls = Classes.Get(holder.DisguiseClass);
            return cls.Slots[cls.DefaultSlot];
        }
        return holder.Weapon.Id;
    }

    internal static Part P(float x0, float x1, float y0, float y1, float z0, float z1, Color c) =>
        new(new Vector3(x0, y0, z0), new Vector3(x1, y1, z1), c);

    // Weapon space: +X right, +Y up, -Z forward (the barrel points toward -Z), origin at the grip.
    internal static readonly Dictionary<WeaponId, WeaponModel> Models = new()
    {
        [WeaponId.Crowbar] = new(new[]
        {
            P(-.5f, .5f, -.5f, .5f, -3, 6, Red), P(-.5f, .5f, -.5f, .5f, -16, -3, Red),
            P(-.5f, .5f, -.5f, 2.2f, -18.5f, -16, Red), P(-.5f, .5f, 1.4f, 2.2f, -18.5f, -14, Red),
            P(-.6f, .6f, -.6f, .6f, 1, 4, Black),
        }, null, 0, 0, true, new(3.2f, -3.4f, -12.5f), 8),

        [WeaponId.Wrench] = new(new[]
        {
            P(-.6f, .6f, -.6f, .6f, -2, 6, Gun), P(-.6f, .6f, -.6f, .6f, -12, -2, Steel),
            P(-2f, 2f, -1.1f, 1.1f, -15, -12, Steel), P(-2f, -.9f, -1.1f, 1.1f, -17, -15, Steel), P(.9f, 2f, -1.1f, 1.1f, -17, -15, Steel),
            P(-.7f, .7f, -.7f, .7f, 1, 4, Yellow),
        }, null, 0, 0, true, new(3.2f, -3.4f, -12.5f), 8),

        [WeaponId.Knife] = new(new[]
        {
            P(-.35f, .35f, -.9f, .9f, -14, -3, Silver), P(-.35f, .35f, -.9f, -.2f, -16.5f, -14, Silver),
            P(-.7f, .7f, -1.4f, 1.4f, -3.3f, -2.7f, Dark), P(-.6f, .6f, -.7f, .7f, -2.7f, 3, Black),
        }, null, 0, 0, true, new(3.4f, -3.2f, -12f), 14),

        [WeaponId.Medikit] = new(new[]
        {
            P(-2.6f, 2.6f, -2f, 2f, -8, 0, White), P(-1.3f, 1.3f, 2f, 2.7f, -6, -2, Gun),
            P(-.45f, .45f, -1.5f, 1.5f, -8.4f, -8, Red), P(-1.5f, 1.5f, -.45f, .45f, -8.4f, -8, Red),
            P(-2.7f, -2.5f, -.6f, .6f, -4.5f, -3.5f, Steel),
            P(-.5f, .5f, 2.7f, 3.05f, -6.5f, -1.5f, Red), P(-2f, 2f, 2.7f, 3.05f, -4.5f, -3.5f, Red),
        }, null, 0, 0, true, new(1.8f, -3.2f, -10f)),

        [WeaponId.Shotgun] = new(new[]
        {
            P(-.7f, .7f, .1f, 1.5f, -17, -1, Gun), P(-.8f, .8f, -1.1f, .1f, -13, -3, Dark),
            P(-1.1f, 1.1f, -1.8f, 1.6f, -3, 3, Dark), P(-1f, 1f, -2.8f, .8f, 3, 11, Wood),
            P(-1.2f, 1.2f, -1.9f, -.5f, -10, -5.5f, Wood), P(-.3f, .3f, 1.5f, 2.1f, -16.5f, -16, Steel),
        }, new(0, .8f, -17.5f), 2.2f, 6, false, new(3.4f, -3.2f, -9f)),

        [WeaponId.Nailgun] = new(new[]
        {
            P(-1.3f, 1.3f, -1.8f, 1.8f, -9, 3, Gun), P(-.55f, .55f, .2f, 1.3f, -14, -9, Dark),
            P(-.9f, .9f, 1.8f, 3.4f, -7, -1.5f, Yellow), P(-.8f, .8f, -4.6f, -1.8f, 0, 2.5f, Dark),
            P(-1.4f, 1.4f, -.4f, .4f, -9.5f, -8.5f, Steel),
        }, new(0, .8f, -14.5f), .6f, 1.2f, false, new(3.4f, -3.2f, -9f)),

        [WeaponId.SuperNailgun] = new(new[]
        {
            P(-1.5f, 1.5f, -1.8f, 2f, -10, 3, Gun), P(-1.1f, -.1f, .2f, 1.3f, -15, -10, Dark), P(.1f, 1.1f, .2f, 1.3f, -15, -10, Dark),
            P(-1.1f, 1.1f, 2f, 3.6f, -8, -1.5f, Orange), P(-.8f, .8f, -4.6f, -1.8f, 0, 2.5f, Dark),
            P(-1.7f, 1.7f, -.4f, .4f, -10.5f, -9.5f, Steel),
        }, new(0, .8f, -15.5f), .8f, 1.5f, false, new(3.4f, -3.2f, -9f)),

        [WeaponId.RocketLauncher] = new(new[]
        {
            P(-2.1f, 2.1f, -1.2f, 2.6f, -20, 6, Olive), P(-2.6f, 2.6f, -1.7f, 3.1f, -23, -20, Gun), P(-2.6f, 2.6f, -1.7f, 3.1f, 6, 8.5f, Gun),
            P(-.8f, .8f, -4.4f, -1.2f, -4, -1, Dark), P(-.5f, .5f, 2.6f, 3.6f, -14, -12, Steel), P(-1.9f, 1.9f, -.8f, 2.2f, -23.5f, -23, Dark),
        }, new(0, .7f, -24f), 2.6f, 5, false, new(3.6f, -3.6f, -7.5f)),

        [WeaponId.GrenadeLauncher] = new(new[]
        {
            P(-1.5f, 1.5f, -1.2f, 2f, -15, -7, Gun), P(-2.3f, 2.3f, -2.4f, 2.4f, -8, 0, Olive), P(-1.7f, 1.7f, -2f, 2f, -7.8f, -7.2f, Steel),
            P(-.8f, .8f, -5.4f, -2.4f, -3, 0, Dark), P(-1f, 1f, -1.5f, 1.5f, 0, 7, Wood),
        }, new(0, .4f, -15.5f), 2.2f, 5, false, new(3.4f, -3.4f, -8f)),

        [WeaponId.PipebombLauncher] = new(new[]
        {
            P(-1.5f, 1.5f, -1.2f, 2f, -15, -7, Gun), P(-2.3f, 2.3f, -2.4f, 2.4f, -8, 0, Red), P(-2.4f, 2.4f, -.6f, .6f, -6, -2, White),
            P(-.8f, .8f, -5.4f, -2.4f, -3, 0, Dark), P(-1f, 1f, -1.5f, 1.5f, 0, 7, Wood),
        }, new(0, .4f, -15.5f), 1.8f, 4, false, new(3.4f, -3.4f, -8f)),

        [WeaponId.AssaultCannon] = new(new[]
        {
            P(-2f, 2f, -2f, 2f, -7, 4, Gun), P(-1.5f, 1.5f, -1.5f, 1.5f, -12, -7, Dark),
            P(-2.3f, -.9f, .3f, 1.7f, -21, -12, Steel), P(.9f, 2.3f, .3f, 1.7f, -21, -12, Steel),
            P(-2.3f, -.9f, -1.7f, -.3f, -21, -12, Steel), P(.9f, 2.3f, -1.7f, -.3f, -21, -12, Steel),
            P(-2.7f, 2.7f, -.7f, .7f, -14, -13, Orange), P(-2.7f, 2.7f, -.7f, .7f, -19.5f, -18.5f, Orange),
            P(-.9f, .9f, -6.2f, -2f, 0, 2.5f, Dark), P(-1.2f, 1.2f, 2f, 3.2f, -4, 0, Yellow),
        }, new(0, 0, -21.5f), .6f, 1.5f, false, new(3.2f, -3.4f, -8f)),

        [WeaponId.SniperRifle] = new(new[]
        {
            P(-.6f, .6f, -.6f, .9f, -24, 0, Dark), P(-.9f, .9f, -1.6f, 1.2f, -7, 3, Gun), P(-.9f, .9f, -2.8f, .6f, 3, 12, Wood),
            P(-.8f, .8f, 1.3f, 2.8f, -13, -4, Black), P(-1f, 1f, 1.1f, 3f, -5, -4, Steel), P(-1f, 1f, 1.1f, 3f, -13.4f, -13, Steel),
            P(-.3f, .3f, -.3f, .3f, -26, -24, Steel),
        }, new(0, .1f, -26.5f), 3f, 8, false, new(3.2f, -3.2f, -8f)),

        [WeaponId.AutoRifle] = new(new[]
        {
            P(-.7f, .7f, -.5f, 1f, -17, 0, Gun), P(-1f, 1f, -1.7f, 1.4f, -6, 3, Dark), P(-.8f, .8f, -2.6f, .4f, 3, 10, Dark),
            P(-.7f, .7f, -3.6f, -1.7f, -4, -2, Yellow), P(-.4f, .4f, 1.4f, 2.1f, -2, 1, Steel),
        }, new(0, .3f, -17.5f), .6f, 1.2f, false, new(3.2f, -3.2f, -9f)),

        [WeaponId.Flamethrower] = new(new[]
        {
            P(-1f, 1f, -1f, 1f, -16, 0, Gun), P(-1.3f, 1.3f, -1.3f, 1.3f, -18, -16, Orange), P(-.5f, .5f, -.5f, .5f, -18.4f, -18, Black),
            P(-1.5f, 1.5f, -3.8f, -1f, -8, 2, Red), P(-1.8f, 1.8f, -1.4f, 2.2f, 0, 8, Steel), P(-.6f, .6f, -4.6f, -3.8f, -2, 1, Dark),
        }, new(0, 0, -19f), .35f, .5f, false, new(3.4f, -3.4f, -8f)),

        [WeaponId.Tranquilizer] = new(new[]
        {
            P(-.9f, .9f, -1.4f, 1.2f, -8, 2, Gun), P(-.5f, .5f, -.2f, .8f, -13, -8, Steel), P(-.35f, .35f, .8f, 1.7f, -12, -3, Silver),
            P(-.6f, .6f, 1.7f, 2.3f, -3.5f, -2.5f, Red), P(-.8f, .8f, -4.4f, -1.4f, 0, 2, Dark),
        }, new(0, .3f, -13.5f), 1f, 3, false, new(3.4f, -3.2f, -9f)),
    };
}
