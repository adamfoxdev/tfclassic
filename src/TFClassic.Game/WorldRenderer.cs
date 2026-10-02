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
        DrawDispensers();
        DrawTeleporters();
        DrawDetpacks();
        DrawFirePatches();
        DrawAreaEffects();
        DrawFlags(time);
        DrawProjectiles();
        DrawEffects();
        DrawWater();

        Rlgl.EnableBackfaceCulling();
    }

    /// <summary>A gassed viewer sees some players' team colours flicker to the wrong side.</summary>
    public static bool Hallucinating(Core.Game game, Player viewer, Player p) =>
        viewer.GasTime > 0 && p != viewer && ((int)(game.Time * 1.3f) + p.Id * 7) % 3 == 0;

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
        if (p.Feigning)
        {
            DrawCorpse(p);
            return;
        }

        // Enemies of a fully disguised spy see the disguise instead of the spy.
        bool fooled = p.IsDisguised && viewer.Team != p.Team;
        var shownTeam = fooled ? p.DisguiseTeam!.Value : p.Team;
        if (Hallucinating(game, viewer, p)) shownTeam = shownTeam.Opposite();   // gassed: friend and foe swap colours
        var team = Palette.Team(shownTeam);
        var cls = Palette.ClassColor(fooled ? p.DisguiseClass : p.Class.Id);
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

        if (p.IsInfected) DrawInfection(p);
    }

    /// <summary>Sickly green motes drifting up around an infected player, visible to everyone.</summary>
    void DrawInfection(Player p)
    {
        for (int i = 0; i < 5; i++)
        {
            float phase = game.Time * 0.9f + i * 1.26f;
            float rise = phase % 1f;
            float a = i * 2.1f + game.Time * 1.5f;
            var pos = p.Position + new Vector3(MathF.Cos(a) * (14 + rise * 8), 20 + rise * 60, MathF.Sin(a) * (14 + rise * 8));
            Raylib.DrawSphere(pos, 4.6f - rise * 2.2f, new Color(120, 220, 60, (int)(230 * (1f - rise))));
        }
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

            if (s.Sabotaged)
            {
                float flicker = 0.5f + 0.5f * MathF.Sin(game.Time * 25f);
                Raylib.DrawSphere(s.Position + new Vector3(0, 50 + flicker * 6, 0), 7f + flicker * 4f, new Color(70, 70, 70, 190));
                Raylib.DrawSphere(s.Position + new Vector3(0, 38, 0), 5f, new Color(255, (int)(120 + 100 * flicker), 40, 220));
            }

            // Health bar above the turret, visible to everyone.
            float frac = Math.Clamp(s.Health / s.MaxHealth, 0f, 1f);
            var barPos = s.Position + new Vector3(0, 52, 0);
            Raylib.DrawCubeV(barPos, new Vector3(26, 2, 2), new Color(30, 30, 30, 200));
            Raylib.DrawCubeV(barPos + new Vector3(-13 * (1 - frac), 0, 0), new Vector3(26 * frac, 3, 3),
                frac > 0.5f ? new Color(90, 230, 90, 255) : new Color(240, 90, 70, 255));
        }
    }

    void DrawDispensers()
    {
        foreach (var d in game.Dispensers)
        {
            var team = Palette.Team(d.Team);
            float grow = d.Building ? 0.35f + 0.65f * (1f - d.BuildTimer / 3f) : 1f;
            var metal = new Color(104, 108, 118, 255);
            var screen = d.Building ? new Color(60, 60, 60, 255) : new Color(90, 230, 150, 255);

            Rlgl.PushMatrix();
            Rlgl.Translatef(d.Position.X, d.Position.Y, d.Position.Z);
            Rlgl.Rotatef(d.Yaw * (180f / MathF.PI), 0, 1, 0);

            Draw3D.Box(new Vector3(-14, 0, -12), new Vector3(14, 10 * grow, 12), Palette.Shade(team, 0.85f), true);        // base
            Draw3D.Box(new Vector3(-13, 10 * grow, -10), new Vector3(13, 52 * grow, 10), metal, true);                      // cabinet
            if (!d.Building)
            {
                Draw3D.Box(new Vector3(-13, 44, -10), new Vector3(13, 52, 10), team, true);                                 // team cap
                float fill = Math.Clamp(d.Store / (float)Dispenser.MaxStore, 0f, 1f);
                Draw3D.Box(new Vector3(-9, 14, 10), new Vector3(9, 40, 11), new Color(30, 32, 38, 255));                    // screen bezel (front = +Z)
                Draw3D.Box(new Vector3(-7, 16, 11), new Vector3(-7 + 14 * fill, 38, 12), screen);                           // store level
            }
            Rlgl.PopMatrix();

            if (d.Sabotaged)
            {
                float flicker = 0.5f + 0.5f * MathF.Sin(game.Time * 25f);
                Raylib.DrawSphere(d.Position + new Vector3(0, 62 + flicker * 6, 0), 7f + flicker * 4f, new Color(70, 70, 70, 190));
                Raylib.DrawSphere(d.Position + new Vector3(0, 46, 0), 5f, new Color(255, (int)(120 + 100 * flicker), 40, 220));
            }

            float frac = Math.Clamp(d.Health / Dispenser.MaxHealth, 0f, 1f);
            var barPos = d.Position + new Vector3(0, 66, 0);
            Raylib.DrawCubeV(barPos, new Vector3(26, 2, 2), new Color(30, 30, 30, 200));
            Raylib.DrawCubeV(barPos + new Vector3(-13 * (1 - frac), 0, 0), new Vector3(26 * frac, 3, 3),
                frac > 0.5f ? new Color(90, 230, 90, 255) : new Color(240, 90, 70, 255));
        }
    }

    void DrawTeleporters()
    {
        foreach (var t in game.Teleporters)
        {
            var team = Palette.Team(t.Team);
            bool entrance = t.Role == TeleporterRole.Entrance;
            bool partnerUp = game.Teleporters.Any(x => x.Owner == t.Owner && x.Role != t.Role && x.Active);
            bool ready = t.Active && partnerUp && t.CooldownTimer <= 0;
            float grow = t.Building ? 0.3f + 0.7f * (1f - t.BuildTimer / 3f) : 1f;

            var glow = entrance ? new Color(80, 220, 255, 255) : new Color(255, 170, 60, 255);
            if (!ready) glow = new Color(95, 95, 100, 255);
            float pulse = 0.75f + 0.25f * MathF.Sin(game.Time * 6f);

            Rlgl.PushMatrix();
            Rlgl.Translatef(t.Position.X, t.Position.Y, t.Position.Z);
            Draw3D.Box(new Vector3(-28, 0, -28), new Vector3(28, 2, 28), Palette.Shade(team, 0.9f), true);                  // team plate
            Draw3D.Box(new Vector3(-24, 2, -24), new Vector3(24, 6 * grow, 24), new Color(70, 72, 82, 255), true);           // pad
            if (!t.Building)
                Draw3D.Box(new Vector3(-14, 6, -14), new Vector3(14, 8, 14), ready ? Palette.Shade(glow, pulse) : glow);    // glowing core
            Rlgl.PopMatrix();

            if (ready)
                Raylib.DrawCubeV(t.Position + new Vector3(0, 60, 0), new Vector3(26, 120, 26), Palette.WithAlpha(glow, 55));

            if (t.Sabotaged)
            {
                float flicker = 0.5f + 0.5f * MathF.Sin(game.Time * 25f);
                Raylib.DrawSphere(t.Position + new Vector3(0, 22 + flicker * 6, 0), 6f + flicker * 4f, new Color(70, 70, 70, 190));
                Raylib.DrawSphere(t.Position + new Vector3(0, 12, 0), 4f, new Color(255, (int)(120 + 100 * flicker), 40, 220));
            }

            float frac = Math.Clamp(t.Health / Teleporter.MaxHealth, 0f, 1f);
            var barPos = t.Position + new Vector3(0, 30, 0);
            Raylib.DrawCubeV(barPos, new Vector3(26, 2, 2), new Color(30, 30, 30, 200));
            Raylib.DrawCubeV(barPos + new Vector3(-13 * (1 - frac), 0, 0), new Vector3(26 * frac, 3, 3),
                frac > 0.5f ? new Color(90, 230, 90, 255) : new Color(240, 90, 70, 255));
        }
    }

    void DrawAreaEffects()
    {
        foreach (var a in game.AreaEffects)
        {
            switch (a.Kind)
            {
                case AreaKind.Caltrops:
                    for (int i = 0; i < 20; i++)
                    {
                        float seed = i * 12.9898f + a.Position.X * 0.37f + a.Position.Z * 0.11f;
                        float ang = (seed * 7.1f) % MathF.Tau;
                        float rad = a.Radius * (((seed * 3.7f) % 1f) * 0.9f + 0.05f);
                        var p = a.Position + new Vector3(MathF.Cos(ang) * rad, 0, MathF.Sin(ang) * rad);
                        Rlgl.PushMatrix();
                        Rlgl.Translatef(p.X, p.Y, p.Z);
                        Rlgl.Rotatef(seed * 40f, 0, 1, 0);
                        Draw3D.Box(new Vector3(-3, 0, -3), new Vector3(3, 2, 3), new Color(120, 120, 128, 255), true);
                        Draw3D.Box(new Vector3(-1, 2, -1), new Vector3(1, 7, 1), new Color(200, 200, 210, 255));
                        Rlgl.PopMatrix();
                    }
                    break;

                case AreaKind.NailGrenade:
                    Rlgl.PushMatrix();
                    Rlgl.Translatef(a.Position.X, a.Position.Y + MathF.Sin(game.Time * 8f) * 2f, a.Position.Z);
                    Rlgl.Rotatef(game.Time * 900f, 0, 1, 0);
                    Draw3D.Box(new Vector3(-6, -5, -6), new Vector3(6, 5, 6), new Color(150, 150, 160, 255), true);
                    Draw3D.Box(new Vector3(-9, -1, -1), new Vector3(9, 1, 1), new Color(220, 220, 230, 255));
                    Draw3D.Box(new Vector3(-1, -1, -9), new Vector3(1, 1, 9), new Color(220, 220, 230, 255));
                    Rlgl.PopMatrix();
                    break;

                case AreaKind.GasCloud:
                {
                    float fade = Math.Clamp(a.Life / 1.5f, 0f, 1f);
                    for (int i = 0; i < 9; i++)
                    {
                        float seed = i * 2.399f;
                        float drift = game.Time * 0.35f + seed;
                        var p = a.Position + new Vector3(MathF.Cos(seed * 3f + drift) * a.Radius * 0.55f,
                            12f + (i % 3) * 22f + MathF.Sin(drift * 1.7f) * 6f, MathF.Sin(seed * 3f + drift) * a.Radius * 0.55f);
                        Raylib.DrawSphere(p, a.Radius * (0.32f + 0.04f * (i % 3)), new Color(130, 210, 60, (int)(52 * fade)));
                    }
                    break;
                }
            }
        }
    }

    /// <summary>Napalm fire: a scorched, glowing floor with flames licking up from random spots.</summary>
    void DrawFirePatches()
    {
        foreach (var f in game.FirePatches)
        {
            float fade = Math.Clamp(f.Life / 1.5f, 0f, 1f);                // gutters out over the last 1.5 s
            Raylib.DrawCubeV(f.Position + new Vector3(0, 0.6f, 0), new Vector3(FirePatch.Radius * 1.7f, 1f, FirePatch.Radius * 1.7f),
                new Color(60, 25, 10, (int)(150 * fade)));

            for (int i = 0; i < 14; i++)
            {
                float seed = i * 12.9898f;
                float a = (seed * 7.1f) % 6.283f;
                float rad = FirePatch.Radius * 0.85f * ((seed * 3.7f) % 1f + 0.05f);
                float flick = 0.5f + 0.5f * MathF.Sin(game.Time * (7f + i % 5) + seed);
                float height = (14f + 26f * flick) * fade;
                var basePos = f.Position + new Vector3(MathF.Cos(a) * rad, 0, MathF.Sin(a) * rad);
                Raylib.DrawCubeV(basePos + new Vector3(0, height * 0.5f, 0), new Vector3(16, height, 16),
                    new Color(255, (int)(90 + 120 * flick), 30, (int)(200 * fade)));
                Raylib.DrawCubeV(basePos + new Vector3(0, height * 0.35f, 0), new Vector3(8, height * 0.7f, 8),
                    new Color(255, 230, 120, (int)(220 * fade)));
            }
        }
    }

    void DrawDetpacks()
    {
        foreach (var d in game.Detpacks)
        {
            var team = Palette.Team(d.Team);
            Rlgl.PushMatrix();
            Rlgl.Translatef(d.Position.X, d.Position.Y, d.Position.Z);
            Rlgl.Rotatef(d.Yaw * (180f / MathF.PI), 0, 1, 0);
            Draw3D.Box(new Vector3(-12, 0, -12), new Vector3(12, 10, 12), new Color(84, 92, 62, 255), true);        // charge
            Draw3D.Box(new Vector3(-12, 3, -12), new Vector3(12, 6, 12), team, true);                               // team band
            Rlgl.PopMatrix();

            // LED: steady amber while arming, then blinking faster as the fuse runs down.
            bool on = d.Building || ((int)(game.Time * (d.Fuse < 5f ? 10 : d.Fuse < 12f ? 5 : 2)) % 2 == 0);
            var led = d.Building ? new Color(255, 190, 60, 255) : on ? new Color(255, 50, 40, 255) : new Color(90, 20, 20, 255);
            Raylib.DrawSphere(d.Position + new Vector3(0, 13, 0), 3f, led);

            if (d.DisarmProgress > 0)
            {
                float frac = Math.Clamp(d.DisarmProgress / Detpack.DisarmTime, 0f, 1f);
                var bar = d.Position + new Vector3(0, 28, 0);
                Raylib.DrawCubeV(bar, new Vector3(30, 3, 3), new Color(30, 30, 30, 220));
                Raylib.DrawCubeV(bar + new Vector3(-15 * (1 - frac), 0, 0), new Vector3(30 * frac, 4, 4), new Color(90, 220, 255, 255));
            }
        }
    }

    void DrawCorpse(Player p)
    {
        var team = Palette.Team(p.Team);
        Rlgl.PushMatrix();
        Rlgl.Translatef(p.Position.X, p.Position.Y, p.Position.Z);
        Rlgl.Rotatef(p.Yaw * (180f / MathF.PI), 0, 1, 0);
        Draw3D.Box(new Vector3(-14, 0, -30), new Vector3(14, 12, 8), team, true);                                    // torso
        Draw3D.Box(new Vector3(-8, 0, 8), new Vector3(8, 14, 22), new Color(222, 184, 150, 255), true);              // head
        Draw3D.Box(new Vector3(-11, 0, -52), new Vector3(11, 10, -30), new Color(60, 60, 70, 255), true);            // legs
        Rlgl.PopMatrix();
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
                case ProjectileKind.MirvBomblet:
                    Raylib.DrawSphere(pr.Position, 3.2f, ((int)(game.Time * 16) % 2 == 0) ? new Color(255, 190, 60, 255) : new Color(170, 60, 50, 255));
                    break;
                case ProjectileKind.HandGrenade:
                case ProjectileKind.Concussion:
                case ProjectileKind.Napalm:
                case ProjectileKind.Caltrops:
                case ProjectileKind.Nail:
                case ProjectileKind.Mirv:
                case ProjectileKind.Gas:
                case ProjectileKind.Emp:
                {
                    // Flashes faster as the fuse runs down.
                    float left = pr.Fuse - pr.Age;
                    bool flash = left < 1f && ((int)(left * (left < 0.4f ? 14 : 7)) % 2 == 0);
                    var body = pr.Kind switch
                    {
                        ProjectileKind.HandGrenade => new Color(60, 100, 60, 255),
                        ProjectileKind.Napalm => new Color(235, 130, 40, 255),
                        ProjectileKind.Caltrops => new Color(150, 150, 158, 255),
                        ProjectileKind.Nail => new Color(205, 205, 215, 255),
                        ProjectileKind.Mirv => new Color(170, 60, 50, 255),
                        ProjectileKind.Gas => new Color(120, 200, 70, 255),
                        ProjectileKind.Emp => new Color(90, 140, 255, 255),
                        _ => new Color(90, 190, 230, 255),
                    };
                    Raylib.DrawSphere(pr.Position, 4.5f, flash ? new Color(255, 80, 60, 255) : body);
                    break;
                }
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
                case EffectKind.Concussion:
                    Raylib.DrawSphere(e.A, e.Radius * (0.15f + 0.85f * t), new Color(120, 210, 255, (int)(110 * (1 - t))));
                    Raylib.DrawSphereWires(e.A, e.Radius * (0.15f + 0.85f * t), 10, 10, new Color(220, 245, 255, (int)(200 * (1 - t))));
                    break;
                case EffectKind.Emp:
                    Raylib.DrawSphere(e.A, e.Radius * (0.1f + 0.9f * t), new Color(110, 160, 255, (int)(70 * (1 - t))));
                    Raylib.DrawSphereWires(e.A, e.Radius * (0.1f + 0.9f * t), 8, 8, new Color(230, 245, 255, (int)(255 * (1 - t))));
                    for (int i = 0; i < 6; i++)   // crackling arcs
                    {
                        float a = i * 1.047f + t * 9f;
                        Raylib.DrawLine3D(e.A, e.A + new Vector3(MathF.Cos(a), 0.3f * MathF.Sin(a * 3f), MathF.Sin(a)) * e.Radius * t, new Color(200, 230, 255, 255));
                    }
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
