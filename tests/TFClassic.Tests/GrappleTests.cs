using System.Numerics;
using TFClassic.Core;
using Xunit;
using static TFClassic.Tests.Helpers;

namespace TFClassic.Tests;

public class GrappleTests
{
    // Facing +X from mid-field, aimed up a little: the east boundary wall is ~1000 units away.
    static Player Setup(Game g, float pitch = 0.3f)
    {
        var p = Human(g, Team.Red, PlayerClassId.Scout);
        Place(g, p, new Vector3(0, 0, 800), MathF.PI / 2);
        p.Pitch = pitch;
        return p;
    }

    static void Hold(Player p, bool on, float pitch = 0.3f) =>
        p.Input = new PlayerInput { SelectSlot = -1, Yaw = MathF.PI / 2, Pitch = pitch, Grapple = on };

    [Fact]
    public void PressingAttachesToTheSurfaceUnderTheCrosshairAndReelsYouIn()
    {
        var g = NewGame();
        var p = Setup(g);
        Hold(p, true);
        g.Tick(Dt);
        Assert.True(p.Grappling);
        Assert.InRange(p.GrappleAnchor.X, 990f, 1001f);

        float startX = p.Position.X;
        for (int i = 0; i < 60; i++) { Hold(p, true); g.Tick(Dt); }
        Assert.True(p.Position.X > startX + 150f, $"should have been pulled toward the anchor, moved {p.Position.X - startX}");
        Assert.True(p.Position.Y > 20f, "an upward anchor lifts the player off the ground");
        Assert.True(p.Velocity.Length() <= Game.GrappleMaxSpeed + 120f);
    }

    [Fact]
    public void ReleasingLetsGoAndKeepsMomentum()
    {
        var g = NewGame();
        var p = Setup(g);
        for (int i = 0; i < 30; i++) { Hold(p, true); g.Tick(Dt); }
        Assert.True(p.Grappling);
        float vx = p.Velocity.X;
        Assert.True(vx > 200f);

        Hold(p, false);
        g.Tick(Dt);
        Assert.False(p.Grappling);
        Assert.True(p.Velocity.X > vx * 0.9f, "momentum carries after release");
    }

    [Fact]
    public void AimingAtTheSkyOrBeyondRangeDoesNotAttach()
    {
        var g = NewGame();
        var p = Setup(g, pitch: 1.3f);
        Hold(p, true, 1.3f);
        g.Tick(Dt);
        Assert.False(p.Grappling);

        var far = NewGame();
        var q = Human(far, Team.Red, PlayerClassId.Scout);
        Place(far, q, new Vector3(-900, 0, 800), MathF.PI / 2);   // 1900 from the east wall
        Hold(q, true, 0.05f);
        far.Tick(Dt);
        Assert.False(q.Grappling);
    }

    [Fact]
    public void HoldingFromBeforeDoesNotFireAndDeathCutsTheRope()
    {
        var g = NewGame();
        var p = Setup(g, pitch: 1.3f);
        Hold(p, true, 1.3f);                  // held while aiming at the sky: nothing to hit
        g.Tick(Dt);
        Hold(p, true);                        // now swing the crosshair onto the wall without releasing
        g.Tick(Dt);
        Assert.False(p.Grappling, "a fresh press is needed to fire");

        Hold(p, false);
        g.Tick(Dt);
        p.GrappleCooldown = 0;
        Hold(p, true);
        g.Tick(Dt);
        Assert.True(p.Grappling);
        g.Kill(p, null, "test");
        g.Tick(Dt);
        Assert.False(p.Grappling);
    }

    [Fact]
    public void RopeCutByGeometryReleases()
    {
        var g = NewGame();
        var p = Setup(g);
        Hold(p, true);
        g.Tick(Dt);
        Assert.True(p.Grappling);
        p.Position = new Vector3(1200, 0, 800);            // teleported behind the boundary wall
        Hold(p, true);
        g.Tick(Dt);
        Assert.False(p.Grappling);
    }

    [Fact]
    public void FiringAndHittingAreAudible()
    {
        var g = NewGame();
        g.RecordSounds = true;
        var p = Setup(g);
        g.DrainSounds();
        Hold(p, true);
        g.Tick(Dt);
        var ids = g.DrainSounds().Select(s => s.Id).ToList();
        Assert.Contains(SoundId.GrappleFire, ids);
        Assert.Contains(SoundId.GrappleHit, ids);
    }

    [Fact]
    public void BotsWaitLongerToRespawnThanHumans()
    {
        var g = NewGame();
        var bot = g.AddBot(Team.Red, PlayerClassId.Scout);
        var human = Human(g, Team.Blue, PlayerClassId.Scout);
        Place(g, bot, new Vector3(0, 0, 800));
        Place(g, human, new Vector3(0, 0, 700));
        g.Kill(bot, null, "test");
        g.Kill(human, null, "test");
        Assert.Equal(Game.BotRespawnDelay, bot.RespawnTimer);
        Run(g, Game.HumanRespawnDelay + 0.5f);
        Assert.True(human.Alive);
        Assert.False(bot.Alive, "bots stay down for longer");
        Run(g, Game.BotRespawnDelay);
        Assert.True(bot.Alive);
    }
}
