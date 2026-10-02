using System.Numerics;
using Raylib_cs;
using TFClassic.Core;

namespace TFClassic.Game;

sealed class WorldRenderer
{
    readonly Core.Game game;

    public WorldRenderer(Core.Game game) => this.game = game;

    public void Draw(Player viewer, float time)
    {
        Rlgl.DisableBackfaceCulling();

        foreach (var s in game.World.Solids)
            Draw3D.Box(s.Box.Min, s.Box.Max, Palette.Of(s.Material), outline: true);

        foreach (var p in game.Players)
            if (p.Alive && (p != viewer)) DrawPlayer(p, viewer);

        DrawSentries();
        DrawFlags(time);
        DrawProjectiles();
        DrawEffects();
        DrawWater();

        Rlgl.EnableBackfaceCulling();
    }

    void DrawWater()
    {
        foreach (var z in game.World.Zones)
        {
            if (z.Kind != ZoneKind.Water) continue;
            Raylib.DrawCubeV(z.Box.Center, z.Box.Size, new Color(40, 110, 200, 110));
        }
    }

    void DrawPlayer(Player p, Player viewer)
    {
        var team = Palette.Team(p.Team);
        var cls = Palette.ClassColor(p.Class.Id);
        float blink = p.SpawnProtect > 0 && ((int)(game.Time * 10) % 2 == 0) ? 0.5f : 1f;

        Rlgl.PushMatrix();
        Rlgl.Translatef(p.Position.X, p.Position.Y, p.Position.Z);
        Rlgl.Rotatef(p.Yaw * (180f / MathF.PI), 0, 1, 0);

        Draw3D.Box(new Vector3(-11, 0, -6), new Vector3(-2, 28, 6), Palette.Shade(new Color(60, 60, 70, 255), blink), true);   // legs
        Draw3D.Box(new Vector3(2, 0, -6), new Vector3(11, 28, 6), Palette.Shade(new Color(60, 60, 70, 255), blink), true);
        Draw3D.Box(new Vector3(-13, 28, -8), new Vector3(13, 52, 8), Palette.Shade(team, blink), true);                         // torso
        Draw3D.Box(new Vector3(-8, 52, -8), new Vector3(8, 66, 8), Palette.Shade(new Color(222, 184, 150, 255), blink), true);  // head
        Draw3D.Box(new Vector3(-9, 62, -9), new Vector3(9, 68, 9), Palette.Shade(cls, blink), true);                            // class helmet band
        Draw3D.Box(new Vector3(-16, 36, 2), new Vector3(-9, 44, 34), Palette.Shade(new Color(45, 45, 50, 255), blink), true);   // weapon

        Rlgl.PopMatrix();
    }

    void DrawSentries()
    {
        foreach (var s in game.Sentries)
        {
            var team = Palette.Team(s.Team);
            float grow = s.Building ? 0.35f + 0.65f * (1f - s.BuildTimer / 3f) : 1f;
            var metal = new Color(110, 112, 120, 255);
            var dark = new Color(48, 50, 56, 255);

            Rlgl.PushMatrix();
            Rlgl.Translatef(s.Position.X, s.Position.Y, s.Position.Z);

            Draw3D.Box(new Vector3(-18, 0, -18), new Vector3(18, 8 * grow, 18), Palette.Shade(team, 0.85f), true);   // base plate
            Draw3D.Box(new Vector3(-3, 8, -3), new Vector3(3, 24 * grow, 3), metal, true);                           // post

            Rlgl.Rotatef(s.Yaw * (180f / MathF.PI), 0, 1, 0);
            float top = 24 * grow;
            Draw3D.Box(new Vector3(-9, top - 4, -9), new Vector3(9, top + 12, 9), s.Building ? Palette.Shade(metal, 0.7f) : metal, true);   // turret body
            if (!s.Building)
            {
                Draw3D.Box(new Vector3(-9, top + 4, -9), new Vector3(9, top + 8, 9), team, true);                     // team stripe
                if (s.Level == 1)
                    Draw3D.Box(new Vector3(-2, top + 2, 9), new Vector3(2, top + 6, 30), dark, true);
                else
                {
                    Draw3D.Box(new Vector3(-6, top + 2, 9), new Vector3(-2, top + 6, 30), dark, true);
                    Draw3D.Box(new Vector3(2, top + 2, 9), new Vector3(6, top + 6, 30), dark, true);
                }
                if (s.Level == 3)
                {
                    Draw3D.Box(new Vector3(-16, top + 4, -4), new Vector3(-9, top + 14, 12), dark, true);              // rocket pods
                    Draw3D.Box(new Vector3(9, top + 4, -4), new Vector3(16, top + 14, 12), dark, true);
                }
            }
            Rlgl.PopMatrix();

            // Health bar above the turret, visible to everyone.
            float frac = Math.Clamp(s.Health / s.MaxHealth, 0f, 1f);
            var barPos = s.Position + new Vector3(0, 52, 0);
            Raylib.DrawCubeV(barPos, new Vector3(26, 2, 2), new Color(30, 30, 30, 200));
            Raylib.DrawCubeV(barPos + new Vector3(-13 * (1 - frac), 0, 0), new Vector3(26 * frac, 3, 3),
                frac > 0.5f ? new Color(90, 230, 90, 255) : new Color(240, 90, 70, 255));
        }
    }

    void DrawFlags(float time)
    {
        foreach (var f in game.Flags)
        {
            var c = Palette.Team(f.Team);
            var basePos = f.Position;
            float bob = f.Carrier != null ? 0f : MathF.Sin(time * 3f) * 2f;
            Vector3 o = f.Carrier != null ? new Vector3(0, 30, 0) : new Vector3(0, bob, 0);
            var p = basePos + o;

            Draw3D.Box(p + new Vector3(-2, 0, -2), p + new Vector3(2, 70, 2), new Color(210, 210, 210, 255), true);
            Draw3D.Box(p + new Vector3(2, 40, -1), p + new Vector3(34, 68, 1), c, true);

            if (f.Carrier == null)
                Raylib.DrawCubeV(p + new Vector3(0, 220, 0), new Vector3(10, 440, 10), Palette.Shade(c, 1f, 60));
        }
    }

    void DrawProjectiles()
    {
        foreach (var pr in game.Projectiles)
        {
            switch (pr.Kind)
            {
                case ProjectileKind.Rocket:
                    Raylib.DrawSphere(pr.Position, 5f, new Color(60, 60, 60, 255));
                    Raylib.DrawSphere(pr.Position - Vector3.Normalize(pr.Velocity) * 8f, 4f, new Color(255, 170, 40, 255));
                    break;
                case ProjectileKind.Grenade:
                    Raylib.DrawSphere(pr.Position, 5f, new Color(70, 140, 70, 255));
                    break;
                case ProjectileKind.Pipe:
                    Raylib.DrawSphere(pr.Position, 5f, Palette.Team(pr.Team));
                    break;
            }
        }
    }

    void DrawEffects()
    {
        foreach (var e in game.Effects)
        {
            float t = 1f - e.Life / e.MaxLife;
            switch (e.Kind)
            {
                case EffectKind.Tracer:
                    Raylib.DrawLine3D(e.A, e.B, new Color(255, 240, 160, 255));
                    break;
                case EffectKind.Explosion:
                    Raylib.DrawSphere(e.A, e.Radius * (0.25f + 0.75f * t), new Color(255, 150, 40, (int)(200 * (1 - t))));
                    Raylib.DrawSphere(e.A, e.Radius * 0.35f * (1 - t), new Color(255, 240, 160, 230));
                    break;
                case EffectKind.Flame:
                    for (int i = 1; i <= 6; i++)
                    {
                        float f = i / 6f;
                        var pos = Vector3.Lerp(e.A, e.B, f * (0.5f + 0.5f * t));
                        Raylib.DrawSphere(pos, 6f + f * 18f, new Color(255, (int)(190 - 120 * f), 40, (int)(150 * (1 - f * 0.6f))));
                    }
                    break;
                case EffectKind.Gib:
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * 0.785f;
                        var dir = new Vector3(MathF.Cos(a), 0.6f, MathF.Sin(a));
                        var pos = e.A + dir * (t * 60f) + new Vector3(0, -t * t * 60f, 0);
                        Raylib.DrawCubeV(pos, new Vector3(6, 6, 6), Palette.Shade(Palette.Team(e.Team), 0.9f));
                    }
                    break;
                case EffectKind.Heal:
                    Raylib.DrawSphere(e.A + new Vector3(0, 40 + t * 30, 0), 6f, new Color(80, 255, 120, (int)(220 * (1 - t))));
                    break;
            }
        }
    }
}
