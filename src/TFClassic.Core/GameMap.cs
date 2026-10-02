using System.Numerics;

namespace TFClassic.Core;

public readonly record struct SpawnPoint(Vector3 Position, float Yaw);

/// <summary>
/// A playable map: geometry, flag stands, spawn points and a bot waypoint graph.
/// Red fortress sits at +Z, Blue at -Z; a river with one bridge runs through the middle.
/// </summary>
/// <summary>Visual flavour of a map; the client picks palettes and sky colours from it.</summary>
public enum MapTheme { Grassland, Desert }

public sealed partial class GameMap
{
    public string Name { get; init; } = "";
    public MapTheme Theme { get; init; } = MapTheme.Grassland;

    /// <summary>Playable extent on the ground plane (X, Z), used for the radar.</summary>
    public Vector2 BoundsMin { get; init; } = new(-1000, -2000);
    public Vector2 BoundsMax { get; init; } = new(1000, 2000);

    /// <summary>Bot waypoint names (without the R_/B_ prefix) where defenders like to stand watch.</summary>
    public string[] DefendNodes { get; init; } = { "flagdoor", "hall", "door" };

    /// <summary>Where an engineer bot puts the teleporter exit (the entrance goes outside the spawn building).</summary>
    public string TeleporterExitNode { get; init; } = "bridge";
    public string TeleporterEntranceNode { get; init; } = "spawnexit";

    /// <summary>A forward node bots retreat toward (e.g. after setting a detpack).</summary>
    public string FieldNode { get; init; } = "field";
    public World World { get; } = new();
    public NavGraph Nav { get; } = new();
    public Vector3[] FlagHome { get; } = new Vector3[2];
    public List<SpawnPoint>[] Spawns { get; } = { new(), new() };

    static Material Swap(Material m) => m switch
    {
        Material.RedTeam => Material.BlueTeam,
        Material.BlueTeam => Material.RedTeam,
        _ => m,
    };

    static Vector3 Mirror(Vector3 v) => new(v.X, v.Y, -v.Z);

    /// <summary>Adds a box for the Red half and its Z-mirrored twin for the Blue half.</summary>
    void Add(float x0, float y0, float z0, float x1, float y1, float z1, Material m)
    {
        World.AddSolid(new Vector3(x0, y0, z0), new Vector3(x1, y1, z1), m);
        World.AddSolid(new Vector3(x0, y0, -z1), new Vector3(x1, y1, -z0), Swap(m));
    }

    void AddRaw(float x0, float y0, float z0, float x1, float y1, float z1, Material m) =>
        World.AddSolid(new Vector3(x0, y0, z0), new Vector3(x1, y1, z1), m);

    void Node(string name, float x, float z, float y = 0)
    {
        Nav.Add("R_" + name, new Vector3(x, y, z));
        Nav.Add("B_" + name, new Vector3(x, y, -z));
    }

    void Link(string a, string b)
    {
        Nav.Link("R_" + a, "R_" + b);
        Nav.Link("B_" + a, "B_" + b);
    }

    public static readonly string[] Names = { "2fort_lite", "bunker_yard" };

    /// <summary>Builds a map by name (case-insensitive, unambiguous prefixes like "bunker" or "2fort" work).</summary>
    public static GameMap Create(string name)
    {
        var matches = Names.Where(n => n.StartsWith(name, StringComparison.OrdinalIgnoreCase)).ToList();
        if (matches.Count != 1)
            throw new ArgumentException($"Unknown or ambiguous map '{name}'. Available: {string.Join(", ", Names)}");
        return matches[0] switch
        {
            "bunker_yard" => BunkerYard(),
            _ => TwoFortLite(),
        };
    }

    public static GameMap TwoFortLite()
    {
        var m = new GameMap { Name = "2fort_lite" };
        m.BuildGeometry();
        m.BuildGameplay();
        m.BuildNav();
        return m;
    }

    void BuildGeometry()
    {
        const float wallH = 192f;

        // Land, river bed and boundary walls.
        Add(-1000, -200, 300, 1000, 0, 2000, Material.Ground);
        AddRaw(-1000, -200, -300, 1000, -96, 300, Material.RiverBed);
        AddRaw(-1016, -200, -2016, -1000, 600, 2016, Material.Wall);
        AddRaw(1000, -200, -2016, 1016, 600, 2016, Material.Wall);
        Add(-1016, -200, 2000, 1016, 600, 2016, Material.Wall);
        World.AddZone(new Vector3(-1000, -96, -300), new Vector3(1000, -24, 300), ZoneKind.Water);

        // Bridge across the river with low side walls.
        AddRaw(-120, -16, -300, 120, 0, 300, Material.Bridge);
        AddRaw(-120, 0, -300, -112, 32, 300, Material.Bridge);
        AddRaw(112, 0, -300, 120, 32, 300, Material.Bridge);

        // Stairs so players can climb out of the river on both banks.
        foreach (float x0 in new[] { 600f, -760f })
            for (int i = 0; i < 5; i++)
                Add(x0, -96, 300 - 24 * (i + 1), x0 + 160, -16 * (i + 1), 300 - 24 * i, Material.Stairs);

        // Fortress shell: front wall with a door, side walls with side doors, back wall, flag-room partition.
        Add(-416, 0, 1300, -80, wallH, 1316, Material.Wall);
        Add(80, 0, 1300, 416, wallH, 1316, Material.Wall);
        Add(-96, 0, 1296, -80, wallH, 1316, Material.RedTeam);
        Add(80, 0, 1296, 96, wallH, 1316, Material.RedTeam);
        foreach (var (x0, x1) in new[] { (-416f, -400f), (400f, 416f) })
        {
            Add(x0, 0, 1316, x1, wallH, 1380, Material.Wall);
            Add(x0, 0, 1480, x1, wallH, 1900, Material.Wall);
        }
        Add(-416, 0, 1900, 416, wallH, 1916, Material.Wall);
        Add(-400, 0, 1700, -80, wallH, 1716, Material.Wall);
        Add(80, 0, 1700, 400, wallH, 1716, Material.Wall);

        // Roof deck with parapets, reached by an exterior staircase on the right-hand side.
        Add(-416, wallH, 1300, 416, wallH + 16, 1916, Material.Roof);
        Add(-416, wallH + 16, 1300, 416, wallH + 48, 1316, Material.Wall);
        Add(-416, wallH + 16, 1900, 416, wallH + 48, 1916, Material.Wall);
        Add(-416, wallH + 16, 1316, -400, wallH + 48, 1900, Material.Wall);
        Add(400, wallH + 16, 1316, 416, wallH + 48, 1830, Material.Wall);
        Add(400, wallH + 16, 1890, 416, wallH + 48, 1900, Material.Wall);
        for (int i = 0; i < 13; i++)
            Add(416, 0, 1560 + 24 * i, 500, 16 * (i + 1), 1560 + 24 * (i + 1), Material.Stairs);
        Add(416, 0, 1872, 500, wallH + 48, 1888, Material.Wall);

        // Flag room carpet and pedestal.
        Add(-200, 0, 1716, 200, 2, 1900, Material.RedTeam);
        Add(-24, 2, 1776, 24, 8, 1824, Material.RedTeam);

        // Spawn building beside the fortress, with its doorway facing the field.
        const float spawnH = 128f;
        Add(-900, 0, 1560, -780, spawnH, 1576, Material.Wall);
        Add(-680, 0, 1560, -560, spawnH, 1576, Material.Wall);
        Add(-900, 0, 1576, -884, spawnH, 1900, Material.Wall);
        Add(-576, 0, 1576, -560, spawnH, 1900, Material.Wall);
        Add(-900, 0, 1900, -560, spawnH, 1916, Material.Wall);
        Add(-900, spawnH, 1560, -560, spawnH + 16, 1916, Material.Roof);
        Add(-796, 0, 1556, -780, spawnH, 1576, Material.RedTeam);
        Add(-680, 0, 1556, -664, spawnH, 1576, Material.RedTeam);
        Add(-860, 0, 1620, -600, 2, 1860, Material.RedTeam);

        // Outdoor cover.
        Add(-300, 0, 850, -150, 48, 866, Material.Crate);
        Add(150, 0, 850, 300, 48, 866, Material.Crate);
        foreach (float sx in new[] { 1f, -1f })
        {
            float a = 380 * sx, b = 200 * sx;
            Add(a - 32, 0, 1018, a + 32, 64, 1082, Material.Crate);
            Add(b - 32, 0, 1068, b + 32, 64, 1132, Material.Crate);
        }
    }

    void BuildGameplay()
    {
        var redFlag = new Vector3(0, 8, 1800);
        FlagHome[(int)Team.Red] = redFlag;
        FlagHome[(int)Team.Blue] = Mirror(redFlag);

        World.AddZone(new Vector3(-400, 0, 1316), new Vector3(400, 192, 1700), ZoneKind.Resupply, Team.Red);
        World.AddZone(new Vector3(-400, 0, -1700), new Vector3(400, 192, -1316), ZoneKind.Resupply, Team.Blue);

        World.AddZone(new Vector3(-884, 0, 1576), new Vector3(-576, 128, 1900), ZoneKind.Resupply, Team.Red);
        World.AddZone(new Vector3(-884, 0, -1900), new Vector3(-576, 128, -1576), ZoneKind.Resupply, Team.Blue);

        foreach (float x in new[] { -830f, -730f, -630f })
            foreach (float z in new[] { 1700f, 1780f, 1860f })
            {
                Spawns[(int)Team.Red].Add(new SpawnPoint(new Vector3(x, 2, z), MathF.PI));
                Spawns[(int)Team.Blue].Add(new SpawnPoint(new Vector3(x, 2, -z), 0f));
            }
    }

    void BuildNav()
    {
        Node("bridge", 0, 330);
        Node("field", 0, 700);
        Node("door", 0, 1240);
        Node("hall", 0, 1500);
        Node("flagdoor", 0, 1650);
        Node("flag", 0, 1800, 8);
        Node("L1", -460, 760);
        Node("L2", -460, 1430);
        Node("Lin", -330, 1430);
        Node("R1", 460, 760);
        Node("R2", 460, 1430);
        Node("Rin", 330, 1430);
        Node("bankE", 680, 340);
        Node("bankW", -680, 340);
        Node("riverE", 680, 120, -96);
        Node("riverW", -680, 120, -96);
        Node("spawn", -730, 1740);
        Node("spawnexit", -730, 1500);

        Link("bridge", "field");
        Link("field", "door");
        Link("door", "hall");
        Link("hall", "flagdoor");
        Link("flagdoor", "flag");
        Link("field", "L1");
        Link("L1", "L2");
        Link("L2", "Lin");
        Link("Lin", "hall");
        Link("field", "R1");
        Link("R1", "R2");
        Link("R2", "Rin");
        Link("Rin", "hall");
        Link("R1", "bankE");
        Link("L1", "bankW");
        Link("bankE", "riverE");
        Link("bankW", "riverW");
        Link("spawn", "spawnexit");
        Link("spawnexit", "L2");
        Link("spawnexit", "L1");

        Nav.Add("mid", new Vector3(0, 0, 0));
        Nav.Link("R_bridge", "mid");
        Nav.Link("B_bridge", "mid");

        // River lanes: wade across the middle of the river under the bridge's flanks.
        Nav.Add("riverMidE", new Vector3(680, -96, 0));
        Nav.Add("riverMidW", new Vector3(-680, -96, 0));
        Nav.Link("R_riverE", "riverMidE");
        Nav.Link("B_riverE", "riverMidE");
        Nav.Link("R_riverW", "riverMidW");
        Nav.Link("B_riverW", "riverMidW");
    }
}
