using System.Numerics;

namespace TFClassic.Core;

/// <summary>
/// bunker_yard: a desert map. A walled base compound at each end (Red at +Z) with a raised flag dais,
/// a side-door spawn bunker, and a big depot in the middle of the field. The depot can be crossed
/// indoors or over its roof via an exterior staircase at each end; two open flanks run past it.
/// </summary>
public sealed partial class GameMap
{
    public static GameMap BunkerYard()
    {
        var m = new GameMap
        {
            Name = "bunker_yard",
            Theme = MapTheme.Desert,
            Blurb = "Desert compounds around a central depot: cross it indoors, over the roof, or around either flank.",
            DefendNodes = new[] { "flagdoor", "yard", "gate" },
            TeleporterExitNode = "stairs",
            TeleporterEntranceNode = "spawnexit",
            FieldNode = "field",
        };
        m.BuildBunkerGeometry();
        m.BuildBunkerGameplay();
        m.BuildBunkerNav();
        return m;
    }

    void BuildBunkerGeometry()
    {
        const float wallH = 120f;

        // Ground and boundary walls.
        Add(-1000, -200, 0, 1000, 0, 2000, Material.Ground);
        AddRaw(-1016, -200, -2016, -1000, 600, 2016, Material.Wall);
        AddRaw(1000, -200, -2016, 1016, 600, 2016, Material.Wall);
        Add(-1016, -200, 2000, 1016, 600, 2016, Material.Wall);

        // Central depot: walls with a doorway at each end, a flat roof and a parapet.
        const float depotH = 128f;
        Add(-250, 0, 284, -48, depotH, 300, Material.Wall);
        Add(48, 0, 284, 250, depotH, 300, Material.Wall);
        Add(-250, 0, 0, -234, depotH, 300, Material.Wall);
        Add(234, 0, 0, 250, depotH, 300, Material.Wall);
        Add(-250, depotH, 0, 250, depotH + 16, 300, Material.Roof);
        Add(-250, depotH + 16, 284, 250, depotH + 48, 300, Material.Wall);
        Add(-250, depotH + 16, 0, -234, depotH + 48, 300, Material.Wall);
        Add(234, depotH + 16, 0, 250, depotH + 48, 90, Material.Wall);
        Add(234, depotH + 16, 150, 250, depotH + 48, 300, Material.Wall);
        Add(-56, 0, 284, -48, depotH, 300, Material.RedTeam);
        Add(48, 0, 284, 56, depotH, 300, Material.RedTeam);
        Add(-120, 0, 120, -60, 56, 180, Material.Crate);          // cover inside the hall

        // Exterior staircase to the roof on the east side; Red climbs from z=300 toward the middle.
        for (int i = 0; i < 8; i++)
            Add(250, 0, 300 - 24 * (i + 1), 330, 16 * (i + 1), 300 - 24 * i, Material.Stairs);

        // Base compound: front wall with a main gate, side walls with side gates, back wall.
        Add(-516, 0, 1300, -80, wallH, 1316, Material.Wall);
        Add(80, 0, 1300, 516, wallH, 1316, Material.Wall);
        Add(-96, 0, 1296, -80, wallH, 1316, Material.RedTeam);
        Add(80, 0, 1296, 96, wallH, 1316, Material.RedTeam);
        foreach (var (x0, x1) in new[] { (-516f, -500f), (500f, 516f) })
        {
            Add(x0, 0, 1316, x1, wallH, 1450, Material.Wall);
            Add(x0, 0, 1550, x1, wallH, 1966, Material.Wall);
        }
        Add(-516, 0, 1950, 516, wallH, 1966, Material.Wall);

        // Flag bunker at the back of the yard, entered through a narrow door; raised two-tier dais.
        Add(-200, 0, 1700, -184, wallH, 1950, Material.Wall);
        Add(184, 0, 1700, 200, wallH, 1950, Material.Wall);
        Add(-200, 0, 1700, -60, wallH, 1716, Material.Wall);
        Add(60, 0, 1700, 200, wallH, 1716, Material.Wall);
        Add(-200, wallH, 1700, 200, wallH + 16, 1950, Material.Roof);
        Add(-72, 0, 1758, 72, 16, 1902, Material.RedTeam);
        Add(-40, 0, 1790, 40, 32, 1870, Material.RedTeam);

        // Spawn bunker outside the east wall, doorway facing the field.
        const float spawnH = 112f;
        Add(620, 0, 1560, 700, spawnH, 1576, Material.Wall);
        Add(820, 0, 1560, 940, spawnH, 1576, Material.Wall);
        Add(620, 0, 1576, 636, spawnH, 1900, Material.Wall);
        Add(924, 0, 1576, 940, spawnH, 1900, Material.Wall);
        Add(620, 0, 1900, 940, spawnH, 1916, Material.Wall);
        Add(620, spawnH, 1560, 940, spawnH + 16, 1916, Material.Roof);
        Add(692, 0, 1556, 700, spawnH, 1576, Material.RedTeam);
        Add(820, 0, 1556, 828, spawnH, 1576, Material.RedTeam);
        Add(660, 0, 1620, 900, 2, 1880, Material.RedTeam);

        // Cover on the field and in the yard.
        Add(-340, 0, 820, -260, 48, 836, Material.Crate);
        Add(260, 0, 820, 340, 48, 836, Material.Crate);
        Add(-160, 0, 900, -96, 64, 964, Material.Crate);
        Add(96, 0, 900, 160, 64, 964, Material.Crate);
        Add(-720, 0, 480, -660, 64, 540, Material.Crate);
        Add(660, 0, 480, 720, 64, 540, Material.Crate);
        Add(-250, 0, 1400, -190, 56, 1460, Material.Crate);
        Add(190, 0, 1400, 250, 56, 1460, Material.Crate);
    }

    void BuildBunkerGameplay()
    {
        var redFlag = new Vector3(0, 32, 1830);
        FlagHome[(int)Team.Red] = redFlag;
        FlagHome[(int)Team.Blue] = Mirror(redFlag);

        World.AddZone(new Vector3(-200, 0, 1716), new Vector3(200, 128, 1950), ZoneKind.Resupply, Team.Red);
        World.AddZone(new Vector3(-200, 0, -1950), new Vector3(200, 128, -1716), ZoneKind.Resupply, Team.Blue);
        World.AddZone(new Vector3(636, 0, 1576), new Vector3(924, 112, 1900), ZoneKind.Resupply, Team.Red);
        World.AddZone(new Vector3(636, 0, -1900), new Vector3(924, 112, -1576), ZoneKind.Resupply, Team.Blue);

        foreach (float x in new[] { 700f, 780f, 860f })
            foreach (float z in new[] { 1680f, 1760f, 1840f })
            {
                Spawns[(int)Team.Red].Add(new SpawnPoint(new Vector3(x, 2, z), MathF.PI));
                Spawns[(int)Team.Blue].Add(new SpawnPoint(new Vector3(x, 2, -z), 0f));
            }
    }

    void BuildBunkerNav()
    {
        // Centre line, through the depot.
        Nav.Add("mid", new Vector3(0, 0, 0));
        Node("front", 0, 340);
        Node("field", 0, 700);
        Node("gate", 0, 1250);
        Node("yard", 0, 1500);
        Node("flagdoor", 0, 1660);
        Node("flag", 0, 1830, 32);

        // Flanks.
        Node("W1", -500, 340);
        Node("E1", 500, 340);
        Node("fieldW", -500, 700);
        Node("fieldE", 500, 700);
        Node("W2", -560, 1200);
        Node("E2", 560, 1200);
        Node("gateW", -570, 1500);
        Node("gateE", 570, 1500);
        Node("gateWin", -440, 1500);
        Node("gateEin", 440, 1500);

        // Roof route.
        Node("stairs", 290, 330);
        Node("stairsTop", 290, 120, 128);
        Node("roofE", 200, 120, 144);
        Nav.Add("roofM", new Vector3(0, 144, 0));

        // Spawn bunker.
        Node("spawn", 780, 1760);
        Node("spawnexit", 760, 1520);

        Nav.Link("R_front", "mid");
        Nav.Link("B_front", "mid");
        Link("front", "field");
        Link("field", "gate");
        Link("gate", "yard");
        Link("yard", "flagdoor");
        Link("flagdoor", "flag");

        Link("front", "W1");
        Link("W1", "fieldW");
        Link("field", "fieldW");
        Link("fieldW", "W2");
        Link("W2", "gateW");
        Link("gateW", "gateWin");
        Link("gateWin", "yard");

        Link("front", "stairs");
        Link("stairs", "E1");
        Link("E1", "fieldE");
        Link("field", "fieldE");
        Link("fieldE", "E2");
        Link("E2", "gateE");
        Link("gateE", "gateEin");
        Link("gateEin", "yard");

        Link("stairs", "stairsTop");
        Link("stairsTop", "roofE");
        Nav.Link("R_roofE", "roofM");
        Nav.Link("B_roofE", "roofM");

        Link("spawn", "spawnexit");
        Link("spawnexit", "gateE");
        Link("spawnexit", "E2");
    }
}
