using System.Numerics;
using Raylib_cs;
using TFClassic.Core;
using Material = TFClassic.Core.Material;

namespace TFClassic.Game;

static class Palette
{
    public static readonly Color Sky = new(135, 190, 235, 255);
    public static readonly Color Red = new(220, 60, 50, 255);
    public static readonly Color Blue = new(60, 105, 235, 255);

    public static Color Team(Team t) => t == Core.Team.Red ? Red : Blue;

    public static Color Of(Material m) => m switch
    {
        Material.Ground => new Color(92, 118, 74, 255),
        Material.Wall => new Color(158, 148, 132, 255),
        Material.Floor => new Color(110, 110, 112, 255),
        Material.Crate => new Color(150, 106, 62, 255),
        Material.Stairs => new Color(124, 126, 138, 255),
        Material.Roof => new Color(112, 102, 98, 255),
        Material.RedTeam => new Color(192, 54, 48, 255),
        Material.BlueTeam => new Color(52, 86, 196, 255),
        Material.Bridge => new Color(134, 112, 88, 255),
        Material.RiverBed => new Color(104, 94, 76, 255),
        _ => new Color(200, 0, 200, 255),
    };

    public static Color ClassColor(PlayerClassId id) => id switch
    {
        PlayerClassId.Scout => new Color(255, 214, 90, 255),
        PlayerClassId.Soldier => new Color(120, 150, 90, 255),
        PlayerClassId.Demoman => new Color(70, 70, 70, 255),
        PlayerClassId.HeavyWeapons => new Color(150, 120, 90, 255),
        PlayerClassId.Sniper => new Color(60, 130, 120, 255),
        PlayerClassId.Medic => new Color(240, 240, 240, 255),
        PlayerClassId.Pyro => new Color(240, 130, 40, 255),
        PlayerClassId.Engineer => new Color(235, 190, 60, 255),
        _ => Color.White,
    };

    public static Color WithAlpha(Color c, int alpha) => new((int)c.R, (int)c.G, (int)c.B, alpha);

    public static Color Shade(Color c, float f, int alpha = 255) =>
        new((int)Math.Clamp(c.R * f, 0, 255), (int)Math.Clamp(c.G * f, 0, 255), (int)Math.Clamp(c.B * f, 0, 255), alpha);
}
