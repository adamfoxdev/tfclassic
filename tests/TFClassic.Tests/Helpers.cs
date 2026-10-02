using System.Numerics;
using TFClassic.Core;

namespace TFClassic.Tests;

static class Helpers
{
    public const float Dt = 1f / 60f;

    public static Game NewGame(int seed = 1) => new(GameMap.TwoFortLite(), seed);

    public static void Run(Game g, float seconds)
    {
        int ticks = (int)(seconds / Dt);
        for (int i = 0; i < ticks; i++) g.Tick(Dt);
    }

    public static Player Place(Game g, Player p, Vector3 pos, float yaw = 0)
    {
        p.Position = pos;
        p.Velocity = Vector3.Zero;
        p.Yaw = yaw;
        p.Input = new PlayerInput { SelectSlot = -1, Yaw = yaw };
        p.SpawnProtect = 0;
        p.FireCooldown = 0;
        p.ResupplyCooldown = 0;
        return p;
    }

    /// <summary>Spawns a human with nothing else around (no bots).</summary>
    public static Player Human(Game g, Team team, PlayerClassId cls) => g.AddPlayer("tester", team, cls);
}
