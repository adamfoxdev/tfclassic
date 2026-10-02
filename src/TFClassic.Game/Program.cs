using System.Numerics;
using Raylib_cs;
using TFClassic.Core;

namespace TFClassic.Game;

static class Program
{
    const float Dt = 1f / 60f;

    static int Main(string[] args)
    {
        int perTeam = 6, seed = Environment.TickCount, width = 1280, height = 720;
        string? screenshot = null;
        float[]? at = null;
        bool menu = false, sentry = false, tele = false;
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
                case "--menu": menu = true; break;
                case "--sentry": sentry = true; break;
                case "--tele": tele = true; break;
                case "--warmup": warmup = float.Parse(args[++i]); break;
                case "--team": team = Enum.Parse<Team>(args[++i], true); break;
                case "--class": cls = Enum.Parse<PlayerClassId>(args[++i], true); break;
                case "--help":
                    Console.WriteLine("TFClassic [--bots N per team] [--seed N] [--team red|blue] [--class <name>] [--width W --height H]\n" +
                                      "          [--screenshot out.png [--warmup seconds] [--at x,y,z,yawDeg,pitchDeg]]");
                    return 0;
            }
        }

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

        var client = new Client(game, human, screenshot != null && !menu);
        if (screenshot != null)
            return client.RunScreenshot(screenshot, warmup, at, sentry, tele);

        client.Run();
        Raylib.CloseWindow();
        return 0;
    }
}
