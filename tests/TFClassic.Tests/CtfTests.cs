using System.Numerics;
using TFClassic.Core;
using Xunit;
using static TFClassic.Tests.Helpers;

namespace TFClassic.Tests;

public class CtfTests
{
    [Fact]
    public void PickUpCarryAndCapture()
    {
        var g = NewGame();
        var p = Human(g, Team.Blue, PlayerClassId.Scout);
        var redFlag = g.Flags[(int)Team.Red];

        Place(g, p, redFlag.Home);
        g.Tick(Dt);
        Assert.Same(p, redFlag.Carrier);
        Assert.Same(redFlag, p.CarryingFlag);

        Place(g, p, g.Flags[(int)Team.Blue].Home);
        g.Tick(Dt);
        Assert.Null(redFlag.Carrier);
        Assert.True(redFlag.AtHome);
        Assert.Equal(1, g.TeamScore[(int)Team.Blue]);
        Assert.Equal(1, p.Captures);
    }

    [Fact]
    public void CannotCaptureWhileOwnFlagIsStolen()
    {
        var g = NewGame();
        var blue = Human(g, Team.Blue, PlayerClassId.Scout);
        var red = Human(g, Team.Red, PlayerClassId.Scout);

        Place(g, blue, g.Flags[(int)Team.Red].Home);
        Place(g, red, g.Flags[(int)Team.Blue].Home);
        g.Tick(Dt);
        Assert.NotNull(blue.CarryingFlag);
        Assert.NotNull(red.CarryingFlag);

        // Blue returns to base but Blue's own flag is in Red's hands: no capture.
        Place(g, blue, g.Flags[(int)Team.Blue].Home);
        g.Tick(Dt);
        Assert.Equal(0, g.TeamScore[(int)Team.Blue]);
    }

    [Fact]
    public void KilledCarrierDropsFlagAndTeammateReturnsIt()
    {
        var g = NewGame();
        var carrier = Human(g, Team.Blue, PlayerClassId.Scout);
        var defender = Human(g, Team.Red, PlayerClassId.Scout);
        var flag = g.Flags[(int)Team.Red];

        Place(g, carrier, flag.Home);
        g.Tick(Dt);
        Place(g, carrier, new Vector3(0, 0, 700));
        g.Kill(carrier, null, "test");
        Assert.Null(flag.Carrier);
        Assert.True(flag.Dropped);
        Assert.InRange(MathF.Abs(flag.Position.Z - 700f), 0f, 5f);

        Place(g, defender, flag.Position);
        g.Tick(Dt);
        Assert.True(flag.AtHome);
    }

    [Fact]
    public void DroppedFlagReturnsAfterTimeout()
    {
        var g = NewGame();
        var carrier = Human(g, Team.Blue, PlayerClassId.Scout);
        var flag = g.Flags[(int)Team.Red];
        Place(g, carrier, flag.Home);
        g.Tick(Dt);
        Place(g, carrier, new Vector3(0, 0, 700));
        g.Kill(carrier, null, "test");
        Run(g, 31f);
        Assert.True(flag.AtHome);
    }

    [Fact]
    public void MatchEndsAtScoreLimitAndResets()
    {
        var g = NewGame();
        var p = Human(g, Team.Blue, PlayerClassId.Scout);
        for (int i = 0; i < Game.ScoreLimit; i++)
        {
            Place(g, p, g.Flags[(int)Team.Red].Home);
            g.Tick(Dt);
            Place(g, p, g.Flags[(int)Team.Blue].Home);
            g.Tick(Dt);
        }
        Assert.True(g.MatchOver);
        Assert.Equal(Team.Blue, g.Winner);
        Run(g, 9f);
        Assert.False(g.MatchOver);
        Assert.Equal(0, g.TeamScore[(int)Team.Blue]);
    }
}
