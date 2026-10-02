using System.Numerics;
using TFClassic.Core;
using Xunit;
using Xunit.Abstractions;

namespace TFClassic.Tests;

public class MapTests(ITestOutputHelper output)
{
    static readonly Vector3 Half = Player.HullHalf;

    [Fact]
    public void SpawnsAndFlagsAreInOpenSpace()
    {
        var map = GameMap.TwoFortLite();
        foreach (var team in new[] { Team.Red, Team.Blue })
            foreach (var s in map.Spawns[(int)team])
                Assert.False(map.World.Overlaps(s.Position + new Vector3(0, 36.5f, 0), Half), $"{team} spawn {s.Position} is inside geometry");

        for (int i = 0; i < 2; i++)
            Assert.False(map.World.Overlaps(map.FlagHome[i] + new Vector3(0, 44f, 0), Half));
    }

    [Fact]
    public void MapIsMirrored()
    {
        var map = GameMap.TwoFortLite();
        Assert.Equal(map.FlagHome[0].X, map.FlagHome[1].X);
        Assert.Equal(map.FlagHome[0].Z, -map.FlagHome[1].Z);
        Assert.Equal(map.Spawns[0].Count, map.Spawns[1].Count);
    }

    [Fact]
    public void EveryWaypointLinkIsWalkable()
    {
        var map = GameMap.TwoFortLite();
        var world = map.World;
        var problems = new List<string>();

        // Anything lower than a step is walkable, so only the hull above step height must be free.
        // (+12 slack: linear interpolation across a staircase dips below the real step surface.)
        const float step = Movement.StepHeight + 12f;
        var upperCenter = new Vector3(0, step + (72f - step) / 2f, 0);
        var upperHalf = new Vector3(Half.X - 0.1f, (72f - step) / 2f, Half.Z - 0.1f);

        foreach (var node in map.Nav.Nodes)
        {
            Assert.False(world.Overlaps(node.Position + upperCenter, upperHalf), $"node {node.Name} is inside geometry");

            foreach (int other in node.Links)
            {
                if (other < node.Id) continue;
                var a = node.Position;
                var b = map.Nav.Nodes[other].Position;
                string name = $"{node.Name}->{map.Nav.Nodes[other].Name}";
                int steps = (int)(Vector3.Distance(a, b) / 8f) + 1;
                for (int i = 0; i <= steps; i++)
                {
                    var p = Vector3.Lerp(a, b, i / (float)steps);
                    if (world.Overlaps(p + upperCenter, upperHalf))
                    {
                        problems.Add($"{name} blocked at {p}");
                        break;
                    }
                    // Floor within reach below the feet (flat-footed on the 0 plane or a low pedestal).
                    var probe = new Vector3(p.X, p.Y - step / 2f, p.Z);
                    if (!world.Overlaps(probe, new Vector3(Half.X, step / 2f + 0.5f, Half.Z)))
                    {
                        problems.Add($"{name} has no floor at {p}");
                        break;
                    }
                }
            }
        }
        foreach (var pr in problems) output.WriteLine(pr);
        Assert.Empty(problems);
    }

    [Fact]
    public void PathExistsFromEverySpawnToBothFlags()
    {
        var map = GameMap.TwoFortLite();
        foreach (var team in new[] { Team.Red, Team.Blue })
        {
            int start = map.Nav.Nearest(map.Spawns[(int)team][0].Position, map.World);
            foreach (var flagTeam in new[] { Team.Red, Team.Blue })
            {
                int goal = map.Nav.Nearest(map.FlagHome[(int)flagTeam], map.World);
                var path = map.Nav.FindPath(start, goal);
                Assert.True(path.Count >= 2 || start == goal, $"{team} spawn cannot reach {flagTeam} flag");
            }
        }
    }
}
