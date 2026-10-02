using System.Numerics;
using TFClassic.Core;
using Xunit;
using Xunit.Abstractions;
using static TFClassic.Tests.Helpers;

namespace TFClassic.Tests;

public class BotMatchTests(ITestOutputHelper output)
{
    /// <summary>Bots with classes drawn from a fixed pool (Engineers optional, since sentries change the balance a lot).</summary>
    static Game BotGame(int seed, int perTeam, bool engineers = false)
    {
        var g = NewGame(seed);
        var rng = new Random(seed * 31);
        int pool = engineers ? Classes.All.Length : Classes.All.Length - 1;   // Engineer is last
        for (int i = 0; i < perTeam; i++)
        {
            g.AddBot(Team.Red, (PlayerClassId)rng.Next(pool));
            g.AddBot(Team.Blue, (PlayerClassId)rng.Next(pool));
        }
        return g;
    }

    [Fact]
    public void BotsPlayFullMatchesWithoutBreakingAndCaptureFlags()
    {
        int captures = 0, pickups = 0, deaths = 0;
        float minY = 0;
        foreach (int seed in new[] { 1, 5, 8 })
        {
            var g = BotGame(seed, 6);
            var carriers = new Player?[2];
            for (int i = 0; i < 60 * 600; i++)           // ten simulated minutes per match
            {
                g.Tick(Dt);
                for (int f = 0; f < 2; f++)
                {
                    var c = g.Flags[f].Carrier;
                    if (c != null && carriers[f] == null) pickups++;
                    carriers[f] = c;
                }
                foreach (var p in g.Players)
                {
                    Assert.False(float.IsNaN(p.Position.X + p.Position.Y + p.Position.Z), "NaN position");
                    minY = MathF.Min(minY, p.Position.Y);
                }
            }
            int caps = g.Players.Sum(p => p.Captures);
            captures += caps;
            deaths += g.Players.Sum(p => p.Deaths);
            output.WriteLine($"seed {seed}: captures={caps} deaths={g.Players.Sum(p => p.Deaths)} score R{g.TeamScore[0]}-B{g.TeamScore[1]}");
        }
        output.WriteLine($"total: captures={captures} pickups={pickups} deaths={deaths} minY={minY}");

        Assert.True(deaths > 100, "bots should be fighting");
        Assert.True(pickups >= 5, "bots should regularly reach and take the enemy flag");
        Assert.True(captures >= 1, "bots should manage to capture at least once");
        Assert.True(minY > -150f, "nobody should fall out of the map");
    }

    [Fact]
    public void BotsDoNotGetStuckOnTheMap()
    {
        var g = BotGame(7, 6);
        Run(g, 600);
        output.WriteLine($"stuck resets in 10 minutes with 12 bots: {g.BotStuckResets}");
        Assert.True(g.BotStuckResets <= 2, $"bots got stuck {g.BotStuckResets} times");
    }

    [Fact]
    public void MatchesWithEngineersBuildSentriesAndStayStable()
    {
        var g = NewGame(3);
        var eng = new[] { g.AddBot(Team.Red, PlayerClassId.Engineer), g.AddBot(Team.Blue, PlayerClassId.Engineer) };
        for (int i = 0; i < 4; i++)
        {
            g.AddBot(Team.Red, PlayerClassId.Soldier);
            g.AddBot(Team.Blue, PlayerClassId.Scout);
        }
        var seen = new HashSet<Sentry>();
        for (int i = 0; i < 60 * 300; i++)
        {
            g.Tick(Dt);
            foreach (var s in g.Sentries) seen.Add(s);
            foreach (var p in g.Players)
                Assert.False(float.IsNaN(p.Position.X + p.Position.Y + p.Position.Z), "NaN position");
        }
        output.WriteLine($"sentries built: {seen.Count}, resets: {g.BotStuckResets}");
        Assert.True(seen.Count >= 2, "both engineers should have built at least one sentry");
        Assert.True(g.BotStuckResets <= 2);
    }
}
