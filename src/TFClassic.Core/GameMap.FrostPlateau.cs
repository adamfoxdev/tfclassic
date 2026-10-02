using System.Numerics;

namespace TFClassic.Core;

/// <summary>
/// frost_trench: a snow map. A deep trench splits the field, crossed by two bridges (or by dropping in and
/// climbing the stairs on the far side). Each flag sits on a raised plateau reached by a front staircase
/// or a side stair on either flank; the spawn bunker is down on the field behind the plateau.
/// </summary>
public sealed partial class GameMap
{
    public static GameMap FrostTrench()
    {
        var m = new GameMap
        {
            Name = "frost_trench",
            Theme = MapTheme.Snow,
            DefendNodes = new[] { "plat", "flag", "gate" },
            TeleporterExitNode = "brE",
            TeleporterEntranceNode = "spawnexit",
            FieldNode = "field",
        };
        m.BuildFrostGeometry();
        m.BuildFrostGameplay();
        m.BuildFrostNav();
        return m;
    }

    void BuildFrostGeometry()
    {
        Add(-1000, -200, 400, 1000, 0, 2000, Material.Ground);
        AddRaw(-1000, -200, -400, 1000, -64, 400, Material.Floor);               // trench floor
        AddRaw(-1016, -200, -2016, -1000, 600, 2016, Material.Wall);
        AddRaw(1000, -200, -2016, 1016, 600, 2016, Material.Wall);
        Add(-1016, -200, 2000, 1016, 600, 2016, Material.Wall);

        // Trench exits: stairs on both banks at the east and west ends.
        foreach (float x0 in new[] { 600f, -760f })
            for (int i = 0; i < 4; i++)
                Add(x0, -64, 400 - 24 * (i + 1), x0 + 160, -16 * (i + 1), 400 - 24 * i, Material.Stairs);

        // Two bridges with low rails.
        foreach (float bx in new[] { -350f, 350f })
        {
            AddRaw(bx - 60, -16, -400, bx + 60, 0, 400, Material.Bridge);
            AddRaw(bx - 60, 0, -400, bx - 52, 32, 400, Material.Bridge);
            AddRaw(bx + 52, 0, -400, bx + 60, 32, 400, Material.Bridge);
        }

        // Flag plateau (top y=64) with a front stair and a side stair on each flank, parapet around the edge.
        Add(-500, 0, 1450, 500, 64, 2000, Material.Floor);
        for (int i = 0; i < 3; i++)
            Add(-150, 0, 1450 - 24 * (i + 1), 150, 16 * (3 - i), 1450 - 24 * i, Material.Stairs);
        foreach (float sx in new[] { 1f, -1f })
        {
            for (int i = 0; i < 3; i++)
            {
                float a = 500 + 24 * i, b = 500 + 24 * (i + 1);
                Add(sx > 0 ? a : -b, 0, 1650, sx > 0 ? b : -a, 16 * (3 - i), 1750, Material.Stairs);
            }
            float e0 = sx > 0 ? 484 : -500, e1 = sx > 0 ? 500 : -484;
            Add(e0, 64, 1450, e1, 112, 1650, Material.Wall);
            Add(e0, 64, 1750, e1, 112, 2000, Material.Wall);
            Add(sx * 150 - (sx > 0 ? 0 : 350), 64, 1450, sx * 150 + (sx > 0 ? 350 : 0), 112, 1466, Material.Wall);
        }
        Add(-500, 64, 1984, 500, 112, 2000, Material.Wall);
        Add(-40, 64, 1760, 40, 72, 1840, Material.RedTeam);                      // flag pedestal
        Add(-24, 64, 1950, 24, 140, 1984, Material.RedTeam);                     // banner post
        foreach (float sx in new[] { 1f, -1f })
            Add(sx * 200 - 30, 64, 1640, sx * 200 + 30, 120, 1700, Material.Crate);

        // Spawn bunker down on the field, doorway facing the enemy.
        const float h = 112f;
        Add(640, 0, 1100, 740, h, 1116, Material.Wall);
        Add(840, 0, 1100, 940, h, 1116, Material.Wall);
        Add(640, 0, 1116, 656, h, 1400, Material.Wall);
        Add(924, 0, 1116, 940, h, 1400, Material.Wall);
        Add(640, 0, 1384, 940, h, 1400, Material.Wall);
        Add(640, h, 1100, 940, h + 16, 1400, Material.Roof);
        Add(732, 0, 1096, 740, h, 1116, Material.RedTeam);
        Add(840, 0, 1096, 848, h, 1116, Material.RedTeam);

        // Cover.
        foreach (float sx in new[] { 1f, -1f })
        {
            Add(sx * 300 - 40, 0, 820, sx * 300 + 40, 48, 836, Material.Crate);
            Add(sx * 128 - 32, 0, 900, sx * 128 + 32, 64, 964, Material.Crate);
            Add(sx * 690 - 30, 0, 500, sx * 690 + 30, 64, 560, Material.Crate);
        }
    }

    void BuildFrostGameplay()
    {
        var redFlag = new Vector3(0, 72, 1800);
        FlagHome[(int)Team.Red] = redFlag;
        FlagHome[(int)Team.Blue] = Mirror(redFlag);

        World.AddZone(new Vector3(-500, 64, 1450), new Vector3(500, 200, 2000), ZoneKind.Resupply, Team.Red);
        World.AddZone(new Vector3(-500, 64, -2000), new Vector3(500, 200, -1450), ZoneKind.Resupply, Team.Blue);
        World.AddZone(new Vector3(656, 0, 1116), new Vector3(924, 112, 1384), ZoneKind.Resupply, Team.Red);
        World.AddZone(new Vector3(656, 0, -1384), new Vector3(924, 112, -1116), ZoneKind.Resupply, Team.Blue);

        foreach (float x in new[] { 700f, 780f, 860f })
            foreach (float z in new[] { 1180f, 1260f, 1340f })
            {
                Spawns[(int)Team.Red].Add(new SpawnPoint(new Vector3(x, 2, z), MathF.PI));
                Spawns[(int)Team.Blue].Add(new SpawnPoint(new Vector3(x, 2, -z), 0f));
            }
    }

    void BuildFrostNav()
    {
        Node("field", 0, 700);
        Node("gate", 0, 1330);
        Node("plat", 0, 1520, 64);
        Node("flag", 0, 1800, 72);
        Node("platE", 440, 1700, 64);
        Node("platW", -440, 1700, 64);
        Node("sideEb", 590, 1700);
        Node("sideWb", -590, 1700);
        Node("sideEm", 530, 1700, 44);
        Node("sideWm", -530, 1700, 44);
        Node("eastRd", 590, 1040);
        Node("westRd", -590, 1040);
        Node("fieldE", 500, 700);
        Node("fieldW", -500, 700);
        Node("brE", 350, 440);
        Node("brW", -350, 440);
        Node("bankE", 680, 420);
        Node("bankW", -680, 420);
        Node("stE", 680, 300, -64);
        Node("stW", -680, 300, -64);
        Node("spawn", 790, 1260);
        Node("spawnexit", 790, 1040);

        Nav.Add("bE", new Vector3(350, 0, 0));
        Nav.Add("bW", new Vector3(-350, 0, 0));
        Nav.Add("trenchE", new Vector3(680, -64, 0));
        Nav.Add("trenchW", new Vector3(-680, -64, 0));
        foreach (var (a, b) in new[] { ("brE", "bE"), ("brW", "bW"), ("stE", "trenchE"), ("stW", "trenchW") })
        {
            Nav.Link("R_" + a, b); Nav.Link("B_" + a, b);
        }

        Link("field", "brE"); Link("field", "brW");
        Link("field", "fieldE"); Link("field", "fieldW");
        Link("fieldE", "brE"); Link("fieldW", "brW");
        Link("brE", "bankE"); Link("brW", "bankW");
        Link("bankE", "stE"); Link("bankW", "stW");
        Link("field", "gate");
        Link("fieldE", "eastRd"); Link("fieldW", "westRd");
        Link("gate", "eastRd"); Link("gate", "westRd");
        Link("eastRd", "sideEb"); Link("westRd", "sideWb");
        Link("sideEb", "sideEm"); Link("sideEm", "platE");
        Link("sideWb", "sideWm"); Link("sideWm", "platW");
        Link("gate", "plat");
        Link("plat", "flag");
        Link("plat", "platE"); Link("plat", "platW");
        Link("platE", "flag"); Link("platW", "flag");
        Link("spawn", "spawnexit");
        Link("spawnexit", "eastRd");
        Link("spawnexit", "fieldE");
    }
}
