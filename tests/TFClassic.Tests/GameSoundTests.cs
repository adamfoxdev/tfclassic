using System.Numerics;
using TFClassic.Core;
using Xunit;
using static TFClassic.Tests.Helpers;

namespace TFClassic.Tests;

public class GameSoundTests
{
    static Game Recording()
    {
        var g = NewGame();
        g.RecordSounds = true;
        g.DrainSounds();                 // discard spawn sounds
        return g;
    }

    static List<SoundId> Heard(Game g) => g.DrainSounds().Select(s => s.Id).ToList();

    [Fact]
    public void NothingIsRecordedUnlessAskedAndDrainClears()
    {
        var quiet = NewGame();
        for (int i = 0; i < 3; i++) { quiet.AddBot(Team.Red); quiet.AddBot(Team.Blue); }
        Run(quiet, 60);
        Assert.Empty(quiet.DrainSounds());

        var g = Recording();
        var p = Human(g, Team.Red, PlayerClassId.Soldier);
        Assert.NotEmpty(g.DrainSounds());                    // the respawn
        Assert.Empty(g.DrainSounds());                       // drained
    }

    [Fact]
    public void RecordingIsCappedSoNobodyListeningCannotLeakMemory()
    {
        var g = Recording();
        var p = Human(g, Team.Red, PlayerClassId.Scout);
        g.DrainSounds();
        Place(g, p, new Vector3(0, 0, 800));
        for (int i = 0; i < 5000; i++) g.Kill(p, null, "spam");     // each death emits; Kill returns early once dead
        Assert.True(g.DrainSounds().Count <= 600);
    }

    [Fact]
    public void FiringEmitsTheWeaponsSoundAtTheShooter()
    {
        var g = Recording();
        var p = Human(g, Team.Red, PlayerClassId.Soldier);
        Place(g, p, new Vector3(0, 0, 800), yaw: MathF.PI);
        g.DrainSounds();
        p.Input = new PlayerInput { SelectSlot = -1, Yaw = p.Yaw, Fire = true };
        g.Tick(Dt);
        var sounds = g.DrainSounds();
        var launch = Assert.Single(sounds, s => s.Id == SoundId.RocketLaunch);
        Assert.Equal(p.Id, launch.SourcePlayerId);
        Assert.InRange(Vector3.Distance(launch.Position, p.Eye), 0f, 1f);
    }

    [Fact]
    public void ExplosionsHurtsKillsAndHitMarkersAreReported()
    {
        var g = Recording();
        var shooter = Human(g, Team.Red, PlayerClassId.Scout);
        var victim = Human(g, Team.Blue, PlayerClassId.Scout);
        Place(g, shooter, new Vector3(0, 0, 800), yaw: MathF.PI);
        Place(g, victim, new Vector3(0, 0, 700));
        victim.Armor = 0;
        g.DrainSounds();

        g.Damage(victim, shooter, 10f, "test", Vector3.Zero);
        var first = g.DrainSounds();
        Assert.Contains(first, s => s.Id == SoundId.Hurt && s.SourcePlayerId == victim.Id);
        Assert.Contains(first, s => s.Id == SoundId.HitMarker && s.SourcePlayerId == shooter.Id);

        g.Damage(victim, shooter, 500f, "test", Vector3.Zero);
        Assert.Contains(Heard(g), id => id == SoundId.Death);

        // Explosions
        var g2 = Recording();
        var s2 = Human(g2, Team.Red, PlayerClassId.Soldier);
        Place(g2, s2, new Vector3(0, 0, 800), yaw: MathF.PI);
        g2.DrainSounds();
        g2.Projectiles.Add(new Projectile { Kind = ProjectileKind.HandGrenade, Owner = s2, Team = Team.Red, Position = new Vector3(400, 3, 500), Stuck = true, Fuse = 0.04f, Damage = 100, Splash = 150 });
        Run(g2, 0.2f);
        Assert.Contains(Heard(g2), id => id == SoundId.Explosion);
    }

    [Fact]
    public void PacingStopsPainAndHitTicksFromMachineGunning()
    {
        var g = Recording();
        var a = Human(g, Team.Red, PlayerClassId.HeavyWeapons);
        var b = Human(g, Team.Blue, PlayerClassId.HeavyWeapons);
        Place(g, a, new Vector3(0, 0, 800));
        Place(g, b, new Vector3(0, 0, 700));          // clears spawn protection
        g.DrainSounds();
        for (int i = 0; i < 6; i++) g.Damage(b, a, 1f, "pellet", Vector3.Zero);   // six pellets in one instant
        var sounds = g.DrainSounds();
        Assert.Single(sounds, s => s.Id == SoundId.Hurt);
        Assert.Single(sounds, s => s.Id == SoundId.HitMarker);
    }

    [Fact]
    public void WalkingJumpingAndLandingMakeNoise()
    {
        var g = Recording();
        var p = Human(g, Team.Red, PlayerClassId.Scout);
        Place(g, p, new Vector3(0, 0, 700));
        g.Tick(Dt);
        g.DrainSounds();

        p.Input = new PlayerInput { SelectSlot = -1, Forward = 1, Yaw = 0 };
        Run(g, 1.5f);
        var walking = Heard(g);
        Assert.True(walking.Count(id => id == SoundId.Footstep) >= 3, "footsteps while running");

        p.Input.Jump = true;
        g.Tick(Dt);
        p.Input.Jump = false;
        Run(g, 1.2f);
        var air = Heard(g);
        Assert.Contains(SoundId.Jump, air);
        Assert.Contains(SoundId.Land, air);
    }

    [Fact]
    public void CaptureTheFlagEventsAreHeardEverywhere()
    {
        var g = Recording();
        var p = Human(g, Team.Blue, PlayerClassId.Scout);
        g.DrainSounds();
        Place(g, p, g.Flags[(int)Team.Red].Home);
        g.Tick(Dt);
        var take = g.DrainSounds().Single(s => s.Id == SoundId.FlagTake);
        Assert.True(take.Range > 10000f);

        Place(g, p, g.Flags[(int)Team.Blue].Home);
        g.Tick(Dt);
        Assert.Contains(Heard(g), id => id == SoundId.FlagCapture);
    }

    [Fact]
    public void GrenadesDetpacksAndBuildingEachHaveTheirSounds()
    {
        var g = Recording();
        var demo = Human(g, Team.Red, PlayerClassId.Demoman);
        Place(g, demo, new Vector3(0, 0, 900), yaw: MathF.PI);
        g.Tick(Dt);
        g.DrainSounds();

        demo.Input = new PlayerInput { SelectSlot = -1, Yaw = demo.Yaw, Grenade1 = true };
        Run(g, 0.2f);
        Assert.Contains(Heard(g), id => id == SoundId.GrenadePin);
        demo.Input.Grenade1 = false;
        g.Tick(Dt);
        Assert.Contains(Heard(g), id => id == SoundId.GrenadeThrow);

        demo.DetpackFuseIndex = 0;
        demo.Input = new PlayerInput { SelectSlot = -1, Yaw = demo.Yaw, PlaceDetpack = true };
        g.Tick(Dt);
        demo.Input.PlaceDetpack = false;
        Assert.Contains(Heard(g), id => id == SoundId.BuildStart);
        Run(g, 3.2f + 1.5f);
        Assert.Contains(Heard(g), id => id == SoundId.DetpackBeep);
        Run(g, 4f);
        Assert.Contains(Heard(g), id => id == SoundId.DetpackBlast);
    }

    [Fact]
    public void EngineerSpyAndMedicActionsAreAudible()
    {
        var g = Recording();
        var eng = Human(g, Team.Red, PlayerClassId.Engineer);
        Place(g, eng, new Vector3(0, 0, 900), yaw: MathF.PI);
        g.Tick(Dt);
        g.DrainSounds();
        eng.Input = new PlayerInput { SelectSlot = -1, Yaw = eng.Yaw, AltFire = true };
        g.Tick(Dt);
        Assert.Contains(Heard(g), id => id == SoundId.BuildStart);

        var spy = Human(g, Team.Blue, PlayerClassId.Spy);
        Place(g, spy, new Vector3(800, 0, 700));
        g.Tick(Dt);
        g.DrainSounds();
        spy.Input = new PlayerInput { SelectSlot = -1, DisguiseNext = true };
        g.Tick(Dt);
        Assert.Contains(Heard(g), id => id == SoundId.Disguise);
        spy.Input = new PlayerInput { SelectSlot = -1, Feign = true };
        g.Tick(Dt);
        Assert.Contains(Heard(g), id => id == SoundId.Feign);

        var medic = Human(g, Team.Blue, PlayerClassId.Medic);
        var enemy = Human(g, Team.Red, PlayerClassId.Soldier);
        Place(g, medic, new Vector3(-800, 0, 700), yaw: MathF.PI);
        Place(g, enemy, new Vector3(-800, 0, 650));
        g.Tick(Dt);
        g.DrainSounds();
        var to = enemy.Center - medic.Eye;
        medic.Input = new PlayerInput { SelectSlot = 0, Fire = true, Yaw = MathF.Atan2(to.X, to.Z), Pitch = MathF.Atan2(to.Y, MathF.Sqrt(to.X * to.X + to.Z * to.Z)) };
        g.Tick(Dt);
        medic.FireCooldown = 0;
        g.Tick(Dt);
        var heard = Heard(g);
        Assert.Contains(SoundId.Infected, heard);
        Assert.Contains(SoundId.MeleeHit, heard);
    }
}
