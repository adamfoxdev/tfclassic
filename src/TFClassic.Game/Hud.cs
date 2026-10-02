using System.Numerics;
using Raylib_cs;
using TFClassic.Core;

namespace TFClassic.Game;

sealed class Hud
{
    readonly Core.Game game;

    public Hud(Core.Game game) => this.game = game;

    static void Text(string s, int x, int y, int size, Color c)
    {
        Raylib.DrawText(s, x + 1, y + 1, size, new Color(0, 0, 0, 160));
        Raylib.DrawText(s, x, y, size, c);
    }

    static void TextCentered(string s, int cx, int y, int size, Color c)
    {
        int w = Raylib.MeasureText(s, size);
        Text(s, cx - w / 2, y, size, c);
    }

    public void Draw(Player me, Camera3D cam, int w, int h, bool showScores)
    {
        DrawNameTags(me, cam);
        DrawRadar(me);

        if (me.Alive) DrawCrosshair(me, w, h);
        DrawVitals(me, w, h);
        DrawGrenades(me, w, h);
        DrawScoreBar(me, w);
        DrawKillFeed(w);
        DrawFlagStatus(me, w, h);
        DrawEngineer(me, w, h);
        DrawSpy(me, w, h);
        if (me.Class.Id != PlayerClassId.Spy && me.SlowTime > 0) Text("SLOWED", w / 2 - 40, h / 2 + 100, 22, new Color(150, 200, 255, 255));

        if (!me.Alive)
        {
            Raylib.DrawRectangle(0, 0, w, h, new Color(120, 0, 0, 70));
            TextCentered("YOU DIED", w / 2, h / 2 - 60, 48, new Color(255, 90, 80, 255));
            TextCentered($"Respawning in {MathF.Max(0, me.RespawnTimer):F0}...   (M: change class)", w / 2, h / 2, 22, Color.White);
        }
        else if (me.SpawnProtect > 0)
        {
            TextCentered("spawn protection", w / 2, h - 150, 18, new Color(200, 255, 200, 255));
        }

        if (game.MatchOver)
        {
            Raylib.DrawRectangle(0, h / 2 - 70, w, 140, new Color(0, 0, 0, 170));
            TextCentered($"{game.Winner} TEAM WINS", w / 2, h / 2 - 50, 52, Palette.Team(game.Winner ?? Team.Red));
            TextCentered("next match starting...", w / 2, h / 2 + 15, 22, Color.White);
        }

        if (showScores) DrawScoreboard(w, h);
    }

    void DrawCrosshair(Player me, int w, int h)
    {
        int cx = w / 2, cy = h / 2;
        var c = new Color(255, 255, 255, 220);
        if (me.Weapon.Mode == FireMode.SniperCharge)
        {
            int r = Math.Min(w, h) / 2 - 20;
            if (me.SniperCharge > 0)
            {
                Raylib.DrawLine(0, cy, w, cy, new Color(0, 0, 0, 220));
                Raylib.DrawLine(cx, 0, cx, h, new Color(0, 0, 0, 220));
                Raylib.DrawCircleLines(cx, cy, r, Color.Black);
                float charge = me.SniperCharge / 2f;
                Raylib.DrawRectangle(cx - 100, cy + r - 40, (int)(200 * charge), 10, new Color(255, 200, 60, 230));
                Raylib.DrawRectangleLines(cx - 100, cy + r - 40, 200, 10, Color.White);
            }
            Raylib.DrawLine(cx - 6, cy, cx + 6, cy, c);
            Raylib.DrawLine(cx, cy - 6, cx, cy + 6, c);
            return;
        }
        Raylib.DrawLine(cx - 12, cy, cx - 4, cy, c);
        Raylib.DrawLine(cx + 4, cy, cx + 12, cy, c);
        Raylib.DrawLine(cx, cy - 12, cx, cy - 4, c);
        Raylib.DrawLine(cx, cy + 4, cx, cy + 12, c);
    }

    void DrawVitals(Player me, int w, int h)
    {
        var hpColor = me.Health > 50 ? new Color(120, 255, 120, 255) : me.Health > 25 ? new Color(255, 210, 80, 255) : new Color(255, 80, 70, 255);
        Text($"{MathF.Max(0, MathF.Ceiling(me.Health))}", 24, h - 80, 56, hpColor);
        Text("HEALTH", 28, h - 100, 16, Color.White);
        Text($"{MathF.Ceiling(me.Armor)}", 190, h - 64, 36, new Color(150, 200, 255, 255));
        Text("ARMOR", 192, h - 84, 16, Color.White);
        Text(me.Class.Name.ToUpperInvariant(), 24, h - 120, 20, Palette.Team(me.Team));

        var wp = me.Weapon;
        string ammo = wp.Ammo == AmmoType.None ? "--" : me.Ammo[(int)wp.Ammo].ToString();
        int ax = w - 250;
        Text(ammo, ax + 120, h - 80, 56, Color.White);
        Text($"{wp.Name}", ax, h - 120, 20, Color.White);
        if (wp.Ammo != AmmoType.None) Text(wp.Ammo.ToString().ToUpperInvariant(), ax + 124, h - 100, 14, Color.LightGray);

        for (int i = 0; i < me.Class.Slots.Length; i++)
        {
            var sw = Weapons.Get(me.Class.Slots[i]);
            var c = i == me.Slot ? Color.White : new Color(160, 160, 160, 255);
            Text($"{i + 1}:{sw.Name}", w - 250, h - 190 + i * 18, 14, c);
        }
    }

    void DrawScoreBar(Player me, int w)
    {
        Raylib.DrawRectangle(w / 2 - 130, 8, 120, 34, Palette.WithAlpha(Palette.Red, 190));
        Raylib.DrawRectangle(w / 2 + 10, 8, 120, 34, Palette.WithAlpha(Palette.Blue, 190));
        TextCentered($"RED {game.TeamScore[0]}", w / 2 - 70, 14, 24, Color.White);
        TextCentered($"BLUE {game.TeamScore[1]}", w / 2 + 70, 14, 24, Color.White);
        TextCentered($"first to {Core.Game.ScoreLimit}", w / 2, 46, 14, Color.LightGray);
    }

    void DrawFlagStatus(Player me, int w, int h)
    {
        var own = game.Flags[(int)me.Team];
        var enemy = game.Flags[(int)me.Team.Opposite()];
        string Status(Flag f) => f.Carrier != null ? $"CARRIED by {f.Carrier.Name}" : f.AtHome ? "at base" : $"DROPPED ({MathF.Ceiling(f.DropTimer)}s)";

        Text($"Your flag: {Status(own)}", w / 2 - 130, 66, 18, own.AtHome ? Color.White : new Color(255, 120, 100, 255));
        Text($"Enemy flag: {Status(enemy)}", w / 2 - 130, 86, 18, enemy.Carrier == me ? new Color(120, 255, 120, 255) : Color.White);
        if (me.CarryingFlag != null)
            TextCentered(own.AtHome ? "YOU HAVE THE FLAG - GET BACK TO YOUR BASE!" : "YOU HAVE THE FLAG - YOUR FLAG IS MISSING, RECOVER IT!",
                w / 2, h / 2 + 120, 24, new Color(255, 230, 90, 255));
    }

    void DrawGrenades(Player me, int w, int h)
    {
        string text = $"Q frag x{me.Grenades[0]}";
        if (me.Class.Concussion > 0) text += $"   E concussion x{me.Grenades[1]}";
        Text(text, w - 250, h - 140, 14, Color.LightGray);

        if (me.Alive && me.Primed >= 0)
        {
            string kind = me.Primed == (int)GrenadeKind.Frag ? "FRAG" : "CONCUSSION";
            float frac = Math.Clamp(me.PrimedTimer / 3f, 0f, 1f);
            int bw = 220, bx = w / 2 - bw / 2, by = h / 2 + 60;
            Raylib.DrawRectangle(bx, by, bw, 12, new Color(0, 0, 0, 160));
            Raylib.DrawRectangle(bx, by, (int)(bw * frac), 12, frac > 0.35f ? new Color(255, 200, 70, 255) : new Color(255, 80, 60, 255));
            Raylib.DrawRectangleLines(bx, by, bw, 12, Color.White);
            TextCentered($"{kind} PRIMED  {me.PrimedTimer:F1}s  (release to throw)", w / 2, by - 22, 16, Color.White);
        }

        if (me.Alive && me.ConcussTime > 0)
        {
            Raylib.DrawRectangle(0, 0, w, h, new Color(120, 200, 255, (int)(Math.Min(1f, me.ConcussTime / 3f) * 55)));
            TextCentered("CONCUSSED", w / 2, h / 2 + 130, 22, new Color(170, 225, 255, 255));
        }
    }

    void DrawSpy(Player me, int w, int h)
    {
        if (me.Class.Id != PlayerClassId.Spy) return;

        string disguise = me.Feigning
            ? $"FEIGNING DEATH ({MathF.Ceiling(me.FeignTimer)}s)  -  G to get up"
            : !me.DisguiseTeam.HasValue
                ? "Not disguised  (F: disguise as an enemy class)"
                : me.IsDisguised
                    ? $"Disguised as {me.DisguiseTeam} {Classes.Get(me.DisguiseClass).Name}  (F: change)"
                    : $"Disguising as {me.DisguiseTeam} {Classes.Get(me.DisguiseClass).Name}... {MathF.Ceiling(me.DisguiseTimer)}s";
        var c = me.IsDisguised ? new Color(150, 255, 150, 255) : me.Feigning ? new Color(255, 200, 100, 255) : Color.White;
        Text(disguise, 24, h - 150, 18, c);
        Text("knife: backstab from behind / sabotage sentries   G: feign death   tranq gun slows", 24, h - 172, 13, Color.LightGray);
        if (me.SlowTime > 0) Text("SLOWED", w / 2 - 40, h / 2 + 100, 22, new Color(150, 200, 255, 255));

        if (me.NoticeTimer > 0)
            TextCentered(me.Notice, w / 2, h / 2 + 70, 22, new Color(255, 230, 120, 255));
    }

    void DrawEngineer(Player me, int w, int h)
    {
        if (me.Class.Id != PlayerClassId.Engineer) return;

        Text($"METAL {me.Metal}", 24, h - 150, 22, new Color(230, 200, 110, 255));
        var s = game.SentryOf(me);
        string status = s == null
            ? $"Sentry: none  (right-click to build, {Sentry.BuildCost} metal)"
            : s.Building
                ? "Sentry: building..."
                : $"Sentry: L{s.Level}  HP {MathF.Ceiling(s.Health)}/{s.MaxHealth}  ammo {s.Ammo}" + (s.Level == 3 ? $"  rockets {s.Rockets}" : "")
                  + (s.Level < 3 ? $"   (wrench: upgrade {Sentry.UpgradeCost})" : "");
        Text(status, 24, h - 176, 16, Color.White);
        if (s != null) Text("right-click again to demolish", 24, h - 196, 13, Color.LightGray);

        var d = game.DispenserOf(me);
        string dStatus = d == null
            ? $"Dispenser: none  (B to build, {Dispenser.BuildCost} metal)"
            : d.Building
                ? "Dispenser: building..."
                : $"Dispenser: HP {MathF.Ceiling(d.Health)}/{Dispenser.MaxHealth}  store {d.Store}/{Dispenser.MaxStore}" + (d.Store <= 0 ? "  EMPTY - use the wrench" : "");
        var entrance = game.TeleporterOf(me, TeleporterRole.Entrance);
        var exit = game.TeleporterOf(me, TeleporterRole.Exit);
        string tStatus = entrance == null && exit == null
            ? $"Teleporters: none  (T: entrance, then exit; {Teleporter.BuildCost} metal each)"
            : entrance == null
                ? "Teleporters: exit only  (T: build the entrance)"
                : exit == null
                    ? "Teleporters: entrance only  (T: build the exit somewhere else)"
                    : entrance.Building || exit.Building
                        ? "Teleporters: building..."
                        : $"Teleporters: linked  (HP {MathF.Ceiling(entrance.Health)} / {MathF.Ceiling(exit.Health)}, T demolishes)";
        Text(tStatus, 24, h - 236, 16, Color.White);
        Text(dStatus, 24, h - 216, 16, d != null && d.Store <= 0 ? new Color(255, 140, 110, 255) : Color.White);

        if (me.NoticeTimer > 0)
            TextCentered(me.Notice, w / 2, h / 2 + 70, 22, new Color(255, 230, 120, 255));
    }

    void DrawKillFeed(int w)
    {
        int y = 12;
        foreach (var e in game.Events.TakeLast(6))
        {
            var c = e.Team.HasValue ? Palette.Team(e.Team.Value) : Color.White;
            int tw = Raylib.MeasureText(e.Text, 16);
            Raylib.DrawRectangle(w - tw - 24, y - 2, tw + 16, 20, new Color(0, 0, 0, 130));
            Text(e.Text, w - tw - 16, y, 16, c);
            y += 22;
        }
    }

    void DrawNameTags(Player me, Camera3D cam)
    {
        foreach (var p in game.Players)
        {
            bool friendly = p.Team == me.Team || p.IsDisguisedAs(me.Team);
            if (p == me || !p.Alive || !friendly || p.Feigning) continue;
            if (!game.World.LineOfSight(me.Eye, p.Eye)) continue;
            var pos = p.Position + new Vector3(0, 84, 0);
            var toCam = pos - cam.Position;
            if (Vector3.Dot(toCam, Vector3.Normalize(cam.Target - cam.Position)) <= 0) continue;
            var s = Raylib.GetWorldToScreen(pos, cam);
            string label = p.CarryingFlag != null ? $"{p.Name} [FLAG]" : p.Name;
            int tw = Raylib.MeasureText(label, 14);
            Text(label, (int)s.X - tw / 2, (int)s.Y, 14, Palette.Team(me.Team));
        }
    }

    void DrawRadar(Player me)
    {
        const int rw = 90, rh = 180, rx = 12, ry = 12;
        const float worldW = 2000, worldH = 4000;
        Raylib.DrawRectangle(rx, ry, rw, rh, new Color(0, 0, 0, 120));
        Raylib.DrawRectangleLines(rx, ry, rw, rh, new Color(255, 255, 255, 140));

        Vector2 Map(Vector3 p) => new(
            rx + (p.X + worldW / 2) / worldW * rw,
            ry + (1 - (p.Z + worldH / 2) / worldH) * rh);

        Raylib.DrawRectangle(rx, ry + rh / 2 - 5, rw, 10, new Color(60, 110, 200, 150)); // river

        foreach (var f in game.Flags)
        {
            var m = Map(f.Position);
            Raylib.DrawRectangle((int)m.X - 3, (int)m.Y - 3, 7, 7, Palette.Team(f.Team));
        }
        foreach (var tp in game.Teleporters)
        {
            if (tp.Team != me.Team && !game.World.LineOfSight(me.Eye, tp.Hull.Center)) continue;
            var tm = Map(tp.Position);
            var tc = tp.Role == TeleporterRole.Entrance ? new Color(80, 220, 255, 255) : new Color(255, 170, 60, 255);
            Raylib.DrawRectangle((int)tm.X - 2, (int)tm.Y - 1, 5, 3, tc);
        }
        foreach (var dsp in game.Dispensers)
        {
            if (dsp.Team != me.Team && !game.World.LineOfSight(me.Eye, dsp.Hull.Center)) continue;
            var dm = Map(dsp.Position);
            Raylib.DrawRectangle((int)dm.X - 1, (int)dm.Y - 3, 3, 7, Palette.Shade(Palette.Team(dsp.Team), 1.2f));
        }
        foreach (var s in game.Sentries)
        {
            if (s.Team != me.Team && !game.World.LineOfSight(me.Eye, s.Hull.Center)) continue;
            var m = Map(s.Position);
            Raylib.DrawRectangle((int)m.X - 2, (int)m.Y - 2, 5, 5, Palette.Shade(Palette.Team(s.Team), 1.2f));
        }
        foreach (var p in game.Players)
        {
            if (!p.Alive) continue;
            bool friendly = p.Team == me.Team || p.IsDisguisedAs(me.Team);
            if (!friendly && (p.Feigning || !game.World.LineOfSight(me.Eye, p.Eye))) continue;
            var m = Map(p.Position);
            Raylib.DrawCircle((int)m.X, (int)m.Y, p == me ? 3.5f : 2.5f, p == me ? Color.White : Palette.Team(friendly ? me.Team : p.Team));
        }
    }

    void DrawScoreboard(int w, int h)
    {
        int bw = 560, bh = 120 + game.Players.Count * 22, bx = w / 2 - bw / 2, by = h / 2 - bh / 2;
        Raylib.DrawRectangle(bx, by, bw, bh, new Color(0, 0, 0, 190));
        TextCentered($"RED {game.TeamScore[0]}  -  {game.TeamScore[1]} BLUE", w / 2, by + 12, 28, Color.White);
        int y = by + 56;
        Text("Name", bx + 20, y, 16, Color.LightGray);
        Text("Class", bx + 200, y, 16, Color.LightGray);
        Text("Frags", bx + 340, y, 16, Color.LightGray);
        Text("Deaths", bx + 410, y, 16, Color.LightGray);
        Text("Caps", bx + 490, y, 16, Color.LightGray);
        y += 24;
        foreach (var p in game.Players.OrderBy(p => p.Team).ThenByDescending(p => p.Frags))
        {
            var c = Palette.Team(p.Team);
            Text(p.Name + (p.CarryingFlag != null ? " [FLAG]" : ""), bx + 20, y, 16, c);
            Text(p.Class.Name, bx + 200, y, 16, c);
            Text(p.Frags.ToString(), bx + 340, y, 16, c);
            Text(p.Deaths.ToString(), bx + 410, y, 16, c);
            Text(p.Captures.ToString(), bx + 490, y, 16, c);
            y += 22;
        }
    }

    // ───────────── class selection ─────────────

    public void DrawClassMenu(int w, int h, Team team, PlayerClassId selected, bool firstJoin)
    {
        Raylib.DrawRectangle(0, 0, w, h, new Color(0, 0, 0, 200));
        TextCentered("TEAM FORTRESS CLASSIC", w / 2, 40, 54, Color.White);
        TextCentered("2fort-lite  -  capture the flag", w / 2, 100, 22, Color.LightGray);

        var tc = Palette.Team(team);
        TextCentered($"Team: {team}   (press T to switch)", w / 2, 150, 26, tc);

        int y = 200;
        for (int i = 0; i < Classes.All.Length; i++)
        {
            var c = Classes.All[i];
            bool sel = c.Id == selected;
            int bx = w / 2 - 330;
            if (sel) Raylib.DrawRectangle(bx - 10, y - 4, 660, 32, Palette.WithAlpha(tc, 110));
            Text($"{i + 1}", bx, y, 22, Color.White);
            Text(c.Name, bx + 40, y, 22, Palette.ClassColor(c.Id));
            Text($"HP {c.MaxHealth}  ARM {c.MaxArmor}  SPD {c.Speed}", bx + 190, y + 3, 16, Color.LightGray);
            Text(string.Join(" / ", c.Slots.Select(s => Weapons.Get(s).Name)), bx + 410, y + 3, 14, Color.Gray);
            y += 36;
        }

        TextCentered(firstJoin ? "Press 1-9 to choose a class, ENTER to join" : "Press 1-9 to choose, ENTER to confirm (applies on respawn or in your resupply room)",
            w / 2, y + 20, 20, Color.White);
        TextCentered("WASD move  -  mouse aim  -  LMB fire  -  RMB detonate pipebombs / engineer: sentry / B dispenser / T teleporters  -  1/2/3 weapons  -  Q/E grenades (hold to cook)  -  F/G spy  -  TAB scores  -  M menu  -  ESC quit",
            w / 2, y + 56, 16, Color.LightGray);
        TextCentered("No mouse?  Numpad 4/6/8/2 look (5 level)  -  arrows or 7/9 move & strafe  -  Numpad 0 fire  -  1/3 grenades  -  Enter or . alt-fire  -  +/- weapon  -  * disguise/dispenser  -  / feign/teleporter",
            w / 2, y + 80, 16, new Color(255, 230, 140, 255));
    }
}
