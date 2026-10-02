using System.Numerics;
using Raylib_cs;
using TFClassic.Core;

namespace TFClassic.Desktop;

static class Program
{
    const float Dt = 1f / 60f;

    /// <summary>
    /// Plays a known sequence (no window, no game) so the audio path can be checked by ear or by recording the
    /// output: a shotgun to the left, to the right and ahead, then a near and a distant explosion and a hit tick.
    /// </summary>
    static int AudioSelfTest(float volume)
    {
        using var sound = new SoundPlayer { Volume = volume };
        if (!sound.Ready)
        {
            Console.WriteLine("audio: no audio device");
            return 1;
        }

        var listener = new Player { Id = 1, Yaw = 0 };            // at the origin looking down +Z; its right-hand side is -X
        (string label, SoundEvent e)[] steps =
        {
            ("shotgun to the LEFT", new SoundEvent(SoundId.Shotgun, new Vector3(400, 36, 0), 1f, 2400f, 2)),
            ("shotgun to the RIGHT", new SoundEvent(SoundId.Shotgun, new Vector3(-400, 36, 0), 1f, 2400f, 2)),
            ("shotgun straight AHEAD", new SoundEvent(SoundId.Shotgun, new Vector3(0, 36, 400), 1f, 2400f, 2)),
            ("explosion NEAR", new SoundEvent(SoundId.Explosion, new Vector3(0, 36, 300), 1f, 3200f, 0)),
            ("explosion FAR", new SoundEvent(SoundId.Explosion, new Vector3(0, 36, 2500), 1f, 3200f, 0)),
            ("hit-confirm tick", new SoundEvent(SoundId.HitMarker, Vector3.Zero, 0.6f, 100f, 1)),
        };
        // Audio servers often drop the very start of the first stream while it wakes up, so open it with a near-silent tick.
        sound.Play(new SoundEvent(SoundId.Footstep, Vector3.Zero, 0.05f, 100f, 1), listener);
        Thread.Sleep(1200);
        foreach (var (label, e) in steps)
        {
            Console.WriteLine($"playing: {label}");
            sound.BeginFrame();
            sound.Play(e, listener);
            Thread.Sleep(1500);
        }
        Console.WriteLine($"audio: played {sound.PlayedCount} sounds");
        return 0;
    }

    static int Main(string[] args)
    {
        int perTeam = 6, seed = Environment.TickCount, width = 1280, height = 720;
        string? screenshot = null;
        float[]? at = null;
        bool mute = false, audioTest = false, shotFire = false, shotGrenade = false, lineup = false;
        int shotSlot = -1;
        float volume = 0.55f;
        string? dumpSounds = null;
        bool menu = false, sentry = false, tele = false, infect = false, fx = false;
        float warmup = 25f;
        var team = Team.Blue;
        var cls = PlayerClassId.Soldier;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--bots": perTeam = int.Parse(args[++i]); break;
                case "--seed": seed = int.Parse(args[++i]); break;
                case "--width": width = int.Parse(args[++i]); break;
                case "--height": height = int.Parse(args[++i]); break;
                case "--screenshot": screenshot = args[++i]; break;
                case "--at": at = args[++i].Split(',').Select(float.Parse).ToArray(); break;
                case "--mute": mute = true; break;
                case "--volume": volume = Math.Clamp(float.Parse(args[++i]), 0f, 1f); break;
                case "--dump-sounds": dumpSounds = args[++i]; break;
                case "--audio-test": audioTest = true; break;
                case "--slot": shotSlot = int.Parse(args[++i]) - 1; break;
                case "--fire": shotFire = true; break;
                case "--grenade": shotGrenade = true; break;
                case "--lineup": lineup = true; break;
                case "--menu": menu = true; break;
                case "--sentry": sentry = true; break;
                case "--tele": tele = true; break;
                case "--infect": infect = true; break;
                case "--fx": fx = true; break;
                case "--warmup": warmup = float.Parse(args[++i]); break;
                case "--team": team = Enum.Parse<Team>(args[++i], true); break;
                case "--class": cls = Enum.Parse<PlayerClassId>(args[++i], true); break;
                case "--help":
                    Console.WriteLine("TFClassic [--bots N per team] [--seed N] [--team red|blue] [--class <name>] [--width W --height H]\n" +
                                      "          [--mute] [--volume 0..1] [--dump-sounds dir] [--audio-test] [--slot 1..3] [--fire] [--grenade] [--lineup]\n          [--screenshot out.png [--warmup seconds] [--at x,y,z,yawDeg,pitchDeg]]");
                    return 0;
            }
        }

        if (dumpSounds != null)
        {
            // Write every synthesized sound as a WAV so it can be auditioned or inspected, then exit.
            Directory.CreateDirectory(dumpSounds);
            foreach (var id in Enum.GetValues<SoundId>())
                File.WriteAllBytes(Path.Combine(dumpSounds, id + ".wav"), SoundSynth.ToWav(SoundSynth.Generate(id)));
            Console.WriteLine($"wrote {Enum.GetValues<SoundId>().Length} sounds to {dumpSounds}");
            return 0;
        }

        if (audioTest) return AudioSelfTest(volume);

        var game = new Core.Game(GameMap.TwoFortLite(), seed);
        var human = game.AddPlayer("You", team, cls);
        // Fill both teams to perTeam players; the human takes one slot on their side.
        for (int i = 0; i < perTeam; i++)
        {
            if (team != Team.Red || i < perTeam - 1) game.AddBot(Team.Red);
            if (team != Team.Blue || i < perTeam - 1) game.AddBot(Team.Blue);
        }

        Raylib.SetConfigFlags(ConfigFlags.VSyncHint | ConfigFlags.Msaa4xHint | ConfigFlags.ResizableWindow);
        Raylib.InitWindow(width, height, "TF Classic - 2fort_lite");
        Raylib.SetExitKey(KeyboardKey.Null);
        Raylib.SetTargetFPS(120);
        Rlgl.SetClipPlanes(2.0, 20000.0);

        var loadTimer = System.Diagnostics.Stopwatch.StartNew();
        var sound = new SoundPlayer { Muted = mute, Volume = volume };
        Console.WriteLine(sound.Ready
            ? $"audio: device ready, {Enum.GetValues<SoundId>().Length} sounds loaded in {loadTimer.ElapsedMilliseconds} ms"
            : "audio: no audio device, running silent");
        game.RecordSounds = sound.Ready && screenshot == null;

        var client = new Client(game, human, screenshot != null && !menu, sound);
        if (screenshot != null)
            return client.RunScreenshot(screenshot, warmup, at, sentry, tele, infect, fx, shotSlot, shotFire, shotGrenade, lineup);

        client.Run();
        Console.WriteLine($"audio: played {sound.PlayedCount} sounds");
        sound.Dispose();
        Raylib.CloseWindow();
        return 0;
    }
}
