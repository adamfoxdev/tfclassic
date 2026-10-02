using System.Numerics;
using Raylib_cs;
using TFClassic.Core;
using Material = TFClassic.Core.Material;

namespace TFClassic.Desktop;

/// <summary>Full-screen map picker: a list of maps and a top-down preview of the highlighted one.</summary>
static class MapMenu
{
    const int RowH = 64, ListW = 420, PreviewW = 260, PreviewH = 470;

    static void Text(string s, int x, int y, int size, Color c)
    {
        Raylib.DrawText(s, x + 1, y + 1, size, new Color(0, 0, 0, 160));
        Raylib.DrawText(s, x, y, size, c);
    }

    static void Centered(string s, int cx, int y, int size, Color c) => Text(s, cx - Raylib.MeasureText(s, size) / 2, y, size, c);

    /// <summary>
    /// Returns the chosen map name, or null if the player quit / cancelled. When <paramref name="screenshot"/> is set the
    /// menu is drawn for a few frames, saved, and the highlighted map is returned.
    /// </summary>
    public static string? Run(string current, bool canCancel, string? screenshot = null)
    {
        var maps = GameMap.Names.Select(GameMap.Create).ToArray();
        int sel = Math.Max(0, Array.FindIndex(maps, m => m.Name == current));
        Raylib.EnableCursor();
        int frames = 0;

        while (!Raylib.WindowShouldClose())
        {
            int w = Raylib.GetScreenWidth(), h = Raylib.GetScreenHeight();
            int lx = w / 2 - (ListW + PreviewW + 40) / 2, ly = 170;

            int n = maps.Length;
            if (Raylib.IsKeyPressed(KeyboardKey.Down) || Raylib.IsKeyPressed(KeyboardKey.S) || Raylib.IsKeyPressed(KeyboardKey.Kp2)) sel = (sel + 1) % n;
            if (Raylib.IsKeyPressed(KeyboardKey.Up) || Raylib.IsKeyPressed(KeyboardKey.W) || Raylib.IsKeyPressed(KeyboardKey.Kp8)) sel = (sel + n - 1) % n;
            for (int i = 0; i < n && i < 9; i++)
                if (Raylib.IsKeyPressed(KeyboardKey.One + i) || Raylib.IsKeyPressed(KeyboardKey.Kp1 + i)) sel = i;

            bool confirm = Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.KpEnter) || Raylib.IsKeyPressed(KeyboardKey.Space);
            var mouse = Raylib.GetMousePosition();
            for (int i = 0; i < n; i++)
            {
                var row = new Rectangle(lx, ly + i * RowH, ListW, RowH - 8);
                if (Raylib.CheckCollisionPointRec(mouse, row) && Raylib.GetMouseDelta() != Vector2.Zero) sel = i;
                if (Raylib.CheckCollisionPointRec(mouse, row) && Raylib.IsMouseButtonPressed(MouseButton.Left)) { sel = i; confirm = true; }
            }
            if (confirm) return maps[sel].Name;
            if (Raylib.IsKeyPressed(KeyboardKey.Escape)) return null;

            Raylib.BeginDrawing();
            Raylib.ClearBackground(new Color(18, 20, 26, 255));
            Centered("TEAM FORTRESS CLASSIC", w / 2, 40, 54, Color.White);
            Centered("Choose a map", w / 2, 108, 26, Color.LightGray);

            for (int i = 0; i < n; i++)
            {
                var m = maps[i];
                int y = ly + i * RowH;
                bool on = i == sel;
                if (on) Raylib.DrawRectangle(lx - 8, y - 4, ListW + 16, RowH - 4, new Color(80, 110, 180, 150));
                Text($"{i + 1}", lx, y + 2, 22, Color.White);
                Text(m.Name, lx + 36, y + 2, 22, on ? Color.White : Color.LightGray);
                Raylib.DrawRectangle(lx + ListW - 18, y + 6, 14, 14, Palette.Of(Material.Ground, m.Theme));
                var words = m.Blurb.Split(' ');
                string line = "", line2 = "";
                foreach (var word in words)
                {
                    if (Raylib.MeasureText(line + " " + word, 13) < ListW - 40 && line2 == "") line = (line + " " + word).Trim();
                    else line2 = (line2 + " " + word).Trim();
                }
                Text(line, lx + 36, y + 28, 13, Color.Gray);
                if (line2 != "") Text(line2, lx + 36, y + 42, 13, Color.Gray);
            }

            DrawPreview(maps[sel], lx + ListW + 40, ly - 8);

            Centered(canCancel ? "UP/DOWN or 1-9 or mouse to choose  -  ENTER / click to start a new match  -  ESC to go back"
                               : "UP/DOWN or 1-9 or mouse to choose  -  ENTER / click to start  -  ESC to quit",
                w / 2, h - 36, 18, Color.White);
            Raylib.EndDrawing();

            if (screenshot != null && ++frames == 4)
            {
                var image = Raylib.LoadImageFromScreen();
                Raylib.ExportImage(image, screenshot);
                Raylib.UnloadImage(image);
                Console.WriteLine($"saved {screenshot}");
                return maps[sel].Name;
            }
        }
        return null;
    }

    static void DrawPreview(GameMap map, int px, int py)
    {
        var min = map.BoundsMin; var max = map.BoundsMax;
        float sx = PreviewW / (max.X - min.X), sy = PreviewH / (max.Y - min.Y);
        float s = MathF.Min(sx, sy);
        int pw = (int)((max.X - min.X) * s), ph = (int)((max.Y - min.Y) * s);
        px += (PreviewW - pw) / 2;

        Vector2 P(float x, float z) => new(px + (x - min.X) * s, py + (1 - (z - min.Y) / (max.Y - min.Y)) * ph);

        Raylib.DrawRectangle(px - 3, py - 3, pw + 6, ph + 6, new Color(255, 255, 255, 60));
        Raylib.DrawRectangle(px, py, pw, ph, Palette.SkyOf(map.Theme));

        foreach (var solid in map.World.Solids.OrderBy(sd => sd.Box.Max.Y))
        {
            var b = solid.Box;
            if (b.Max.X - b.Min.X > 1900 && b.Max.Y > 100) continue;           // boundary walls
            if (b.Max.Z - b.Min.Z > 3900 && b.Max.Y > 100) continue;
            var a = P(b.Min.X, b.Max.Z); var c = P(b.Max.X, b.Min.Z);
            float shade = Math.Clamp(0.78f + b.Max.Y / 500f, 0.5f, 1.15f);
            if (b.Max.Y < 0) shade = 0.6f;
            int rw = Math.Max(1, (int)MathF.Ceiling(c.X - a.X)), rh = Math.Max(1, (int)MathF.Ceiling(c.Y - a.Y));
            Raylib.DrawRectangle((int)a.X, (int)a.Y, rw, rh, Palette.Shade(Palette.Of(solid.Material, map.Theme), shade));
        }
        foreach (var z in map.World.Zones)
        {
            if (z.Kind != ZoneKind.Water) continue;
            var a = P(z.Box.Min.X, z.Box.Max.Z); var c = P(z.Box.Max.X, z.Box.Min.Z);
            Raylib.DrawRectangle((int)a.X, (int)a.Y, (int)(c.X - a.X), (int)(c.Y - a.Y), new Color(60, 110, 200, 150));
        }
        foreach (var t in new[] { Team.Red, Team.Blue })
        {
            var f = P(map.FlagHome[(int)t].X, map.FlagHome[(int)t].Z);
            Raylib.DrawRectangle((int)f.X - 4, (int)f.Y - 4, 9, 9, Palette.Team(t));
            Raylib.DrawRectangleLines((int)f.X - 4, (int)f.Y - 4, 9, 9, Color.White);
            foreach (var sp in map.Spawns[(int)t])
            {
                var q = P(sp.Position.X, sp.Position.Z);
                Raylib.DrawCircle((int)q.X, (int)q.Y, 2f, Palette.Team(t));
            }
        }
    }
}
