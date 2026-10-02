using System.Numerics;
using TFClassic.Core;
using Xunit;
using static TFClassic.Tests.Helpers;

namespace TFClassic.Tests;

public class MovementTests
{
    [Fact]
    public void PlayerFallsAndLandsOnGround()
    {
        var g = NewGame();
        var p = Human(g, Team.Red, PlayerClassId.Soldier);
        Place(g, p, new Vector3(0, 300, 900));
        Run(g, 2);
        Assert.True(p.OnGround);
        Assert.InRange(p.Position.Y, -0.1f, 0.1f);
    }

    [Fact]
    public void WalkingForwardReachesSpeedAndStopsAtWall()
    {
        var g = NewGame();
        var p = Human(g, Team.Red, PlayerClassId.Scout);
        // Face the boundary wall (+Z) from inside the red half, away from the fortress.
        Place(g, p, new Vector3(800, 0, 900), yaw: 0);
        p.Input = new PlayerInput { SelectSlot = -1, Forward = 1, Yaw = 0 };
        Run(g, 1.0f);
        Assert.InRange(p.Velocity.Length(), 350f, 410f);
        Run(g, 6f);
        Assert.True(p.Position.Z < 2000f - 16f + 0.5f, $"z={p.Position.Z}");
        Assert.True(p.Position.Z > 1900f);
    }

    [Fact]
    public void CannotWalkThroughFortressWallButCanUseDoor()
    {
        var g = NewGame();
        var p = Human(g, Team.Blue, PlayerClassId.Soldier);

        // Straight into the front wall, off-centre: blocked.
        Place(g, p, new Vector3(-250, 0, 1100), yaw: 0);
        p.Input = new PlayerInput { SelectSlot = -1, Forward = 1, Yaw = 0 };
        Run(g, 4);
        Assert.True(p.Position.Z < 1300f, $"walked through wall, z={p.Position.Z}");

        // Through the door at x=0.
        Place(g, p, new Vector3(0, 0, 1100), yaw: 0);
        p.Input = new PlayerInput { SelectSlot = -1, Forward = 1, Yaw = 0 };
        Run(g, 2.5f);
        Assert.True(p.Position.Z > 1350f, $"did not pass the door, z={p.Position.Z}");
    }

    [Fact]
    public void ClimbsRoofStairs()
    {
        var g = NewGame();
        var p = Human(g, Team.Blue, PlayerClassId.Soldier);
        Place(g, p, new Vector3(458, 0, 1500), yaw: 0);   // yaw 0 = +Z, up the staircase
        p.Input = new PlayerInput { SelectSlot = -1, Forward = 1, Yaw = 0 };
        Run(g, 4);
        Assert.True(p.Position.Y > 150f, $"y={p.Position.Y}");
    }

    [Fact]
    public void JumpClearsFortyFiveUnitsButNotSixty()
    {
        var g = NewGame();
        var p = Human(g, Team.Red, PlayerClassId.Scout);
        Place(g, p, new Vector3(0, 0, 700));
        p.Input = new PlayerInput { SelectSlot = -1, Jump = true };
        float peak = 0;
        for (int i = 0; i < 120; i++)
        {
            g.Tick(Dt);
            peak = MathF.Max(peak, p.Position.Y);
            p.Input.Jump = i < 2;
        }
        Assert.InRange(peak, 40f, 50f);
    }

    [Fact]
    public void RiverIsWadeableAndStairsLeadOut()
    {
        var g = NewGame();
        var p = Human(g, Team.Red, PlayerClassId.Soldier);
        Place(g, p, new Vector3(680, -96, 100), yaw: 0);   // river bed beside the bank stairs, facing +Z
        p.Input = new PlayerInput { SelectSlot = -1, Forward = 1, Yaw = 0 };
        Run(g, 4);
        Assert.True(p.Position.Z > 300f && MathF.Abs(p.Position.Y) < 1f, $"pos={p.Position}");
    }
}
