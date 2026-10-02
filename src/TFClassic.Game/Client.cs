using System.Numerics;
using Raylib_cs;
using TFClassic.Core;

namespace TFClassic.Desktop;

/// <summary>Owns input, the fixed-timestep loop, the camera and the frame layout.</summary>
sealed class Client
{
    const float Dt = 1f / 60f;
    const float MouseSensitivity = 0.0022f;
    const float KeypadTurnRate = 2.4f;   // radians per second
    const float KeypadPitchRate = 1.6f;

    readonly Core.Game game;
    readonly Player me;
    readonly WorldRenderer world;
    readonly Hud hud;
    readonly SoundPlayer sound;
    readonly ViewModel viewModel = new();
    float lastFrame = 1f / 60f;

    bool menuOpen = true;
    bool firstJoin = true;
    PlayerClassId menuClass;
    Team menuTeam;

    bool quit, changeMap;
    float yaw, pitch;
    int pendingSlot = -1;
    bool pendingDisguise, pendingFeign, pendingDispenser, pendingTeleporter, pendingDetpack, pendingDetpackFuse;
    bool swallowKpEnter;   // the key that closed the menu must not also count as alt-fire
    double accumulator;
    float time;

    public Client(Core.Game game, Player me, bool headless, SoundPlayer sound)
    {
        this.game = game;
        this.sound = sound;
        this.me = me;
        world = new WorldRenderer(game);
        hud = new Hud(game);
        yaw = me.Yaw;
        menuClass = me.PendingClass;
        menuTeam = me.Team;
        menuOpen = !headless;
    }

    public enum Exit { Quit, ChangeMap }

    /// <summary>Runs until the player quits or asks to change map (from the class menu); can be called again to resume.</summary>
    public Exit Run()
    {
        changeMap = false;
        Raylib.PollInputEvents();   // the key that got us here (e.g. ENTER in the map menu) must not also count in this menu
        if (menuOpen) Raylib.EnableCursor(); else Raylib.DisableCursor();
        while (!quit && !changeMap && !Raylib.WindowShouldClose())
        {
            float frame = MathF.Min(Raylib.GetFrameTime(), 0.1f);
            lastFrame = frame;
            time += frame;

            if (Raylib.IsKeyPressed(KeyboardKey.F8)) sound.Muted = !sound.Muted;

            if (menuOpen) UpdateMenu();
            else UpdatePlaying(frame);

            accumulator += frame;
            while (accumulator >= Dt)
            {
                if (menuOpen && firstJoin) me.SpawnProtect = 2f;   // don't get shot while picking a class
                else PushInput();
                game.Tick(Dt);
                pendingSlot = -1;
                pendingDisguise = pendingFeign = pendingDispenser = pendingTeleporter = pendingDetpack = pendingDetpackFuse = false;
                accumulator -= Dt;
            }

            PlaySounds();
            Render();
        }
        return changeMap ? Exit.ChangeMap : Exit.Quit;
    }

    void PlaySounds()
    {
        sound.BeginFrame();
        foreach (var e in game.DrainSounds()) sound.Play(e, me);
    }

    /// <summary>Runs the sim for a while with the human idle, then saves a frame and exits.</summary>
    public int RunScreenshot(string path, float warmupSeconds, float[]? at, bool buildSentry = false, bool buildTeleporters = false, bool infectEveryone = false, bool showGrenadeEffects = false, int weaponSlot = -1, bool showFiring = false, bool showGrenade = false, bool lineup = false)
    {
        me.Input = new PlayerInput { SelectSlot = -1, Yaw = me.Yaw };
        for (float t = 0; t < warmupSeconds; t += Dt)
        {
            PushInput();
            game.Tick(Dt);
        }
        if (at is { Length: 5 })
        {
            me.Position = new Vector3(at[0], at[1], at[2]);
            me.Velocity = Vector3.Zero;
            yaw = at[3] * MathF.PI / 180f;
            pitch = at[4] * MathF.PI / 180f;
            me.Yaw = yaw;
            me.Pitch = pitch;
        }
        if (showGrenadeEffects)
        {
            // Debug aid: one of each lingering grenade effect, lined up in front of the camera.
            me.Position = new Vector3(0, 0, 1230);
            me.Velocity = Vector3.Zero;
            yaw = MathF.PI; pitch = -0.1f;
            AreaEffect Area(AreaKind k, float x, float r, float life) => new()
            {
                Kind = k, Owner = me, Team = me.Team, Position = new Vector3(x, 0, 800), Radius = r, Life = life, Charges = 8,
            };
            game.AreaEffects.Add(Area(AreaKind.Caltrops, -330, 90, 30));
            var nail = Area(AreaKind.NailGrenade, -20, 450, 30); nail.Position += new Vector3(0, 30, 0);
            game.AreaEffects.Add(nail);
            game.AreaEffects.Add(Area(AreaKind.GasCloud, 360, 170, 30));
            game.FirePatches.Add(new FirePatch { Owner = me, Team = me.Team, Position = new Vector3(-170, 0, 800) });
            game.Projectiles.Add(new Projectile { Kind = ProjectileKind.Emp, Owner = me, Team = me.Team, Position = new Vector3(160, 50, 800), Stuck = true, Fuse = 0.04f, Splash = 240 });
            game.Projectiles.Add(new Projectile { Kind = ProjectileKind.Mirv, Owner = me, Team = me.Team, Position = new Vector3(250, 8, 880), Stuck = true, Fuse = 0.1f, Damage = 0, Splash = 130 });
            for (int i = 0; i < 8; i++) game.Tick(Dt);
            me.GasTime = 3f;
        }
        if (infectEveryone)
        {
            // Debug aid: everyone (including the viewer) looks infected, to check the visuals.
            var other = game.Players.FirstOrDefault(q => q != me && q.Team != me.Team) ?? game.Players.First(q => q != me);
            foreach (var q in game.Players) q.InfectedBy = q == me ? other : me;
        }
        if (buildTeleporters)
        {
            // Debug aid: build an entrance and an exit in open ground, then look at them from a distance.
            void Stand(float x, float z)
            {
                me.Position = new Vector3(x, 0, z);
                me.Velocity = Vector3.Zero;
                me.Yaw = yaw = MathF.PI;
                me.Metal = 200;
                me.Input = new PlayerInput { SelectSlot = -1, Yaw = yaw };
                game.Tick(Dt);
                me.Input.BuildTeleporter = true;
                game.Tick(Dt);
                me.Input.BuildTeleporter = false;
            }
            Stand(0, 900);
            Stand(200, 560);
            for (int i = 0; i < 220; i++) game.Tick(Dt);
            me.Position = new Vector3(110, 0, 1000);
            yaw = MathF.PI;
            pitch = -0.12f;
        }
        if (buildSentry)
        {
            // Debug aid: engineer builds a sentry, then it is bumped to level 3 for a look at the model.
            me.Input = new PlayerInput { SelectSlot = -1, Yaw = yaw, Pitch = pitch, AltFire = true };
            game.Tick(Dt);
            me.Input.AltFire = false;
            for (int i = 0; i < 200; i++) game.Tick(Dt);
            var s = game.SentryOf(me);
            if (s != null) { s.Level = 3; s.Health = s.MaxHealth * 0.6f; }
        }
        if (weaponSlot >= 0)
        {
            // Debug aid: hold a particular weapon (optionally frozen mid-shot) and show it.
            me.Input = new PlayerInput { SelectSlot = weaponSlot, Yaw = yaw, Pitch = pitch };
            for (int i = 0; i < 30; i++) game.Tick(Dt);
            viewModel.Update(me, Dt);
            viewModel.Settle();
            if (showFiring)
            {
                viewModel.Fire(me.Weapon);
                viewModel.Frozen = true;      // hold the mid-shot frame
            }
        }
        if (lineup)
        {
            // Debug aid: one player holding each weapon, in rows, seen from their right-hand side (plus one cooking a grenade).
            var holders = new List<(PlayerClassId cls, int slot)>();
            foreach (var id in Enum.GetValues<WeaponId>())
            {
                var cls = Classes.All.First(k => k.Slots.Contains(id));
                holders.Add((cls.Id, Array.IndexOf(cls.Slots, id)));
            }
            var extra = game.AddPlayer("cook", Team.Blue, PlayerClassId.Pyro);
            int n = 0;
            foreach (var (cls, slot) in holders.Append((PlayerClassId.Pyro, 2)))
            {
                var q = n == holders.Count ? extra : game.AddPlayer("w" + n, n % 2 == 0 ? Team.Red : Team.Blue, cls);
                q.PendingClass = cls;
                q.Slot = slot;
                q.Position = n == holders.Count ? new Vector3(0, 0, 640) : new Vector3(-220 + (n % 5) * 110, 0, 720 + (n / 5) * 150);
                q.Velocity = Vector3.Zero;
                q.SpawnProtect = 0;
                q.Input = new PlayerInput { SelectSlot = slot, Yaw = -MathF.PI / 2, Pitch = 0.05f };   // facing -X: right shoulder toward the camera
                if (n == holders.Count) { q.Primed = 0; q.PrimedTimer = 2.4f; }
                n++;
            }
            for (int i = 0; i < 40; i++) game.Tick(Dt);
            foreach (var q in game.Players) if (q.Input.SelectSlot == 2 && q.Name == "cook") { q.Primed = 0; q.PrimedTimer = 2.4f; }
            if (at is not { Length: 5 })
            {
                me.Position = new Vector3(0, 0, 420);
                me.Velocity = Vector3.Zero;
                yaw = 0; pitch = -0.18f;
            }
        }
        if (showGrenade)
        {
            // Debug aid: cook a grenade for half a second and show it in hand.
            me.Input = new PlayerInput { SelectSlot = -1, Yaw = yaw, Pitch = pitch, Grenade2 = true };
            for (int i = 0; i < 30; i++) game.Tick(Dt);
            viewModel.Update(me, Dt);
            viewModel.Settle();
            viewModel.Frozen = true;
        }
        for (int i = 0; i < 3; i++) Render();   // let the window/GL settle
        var image = Raylib.LoadImageFromScreen();
        Raylib.ExportImage(image, path);
        Raylib.UnloadImage(image);
        Raylib.CloseWindow();
        Console.WriteLine($"saved {path}");
        return 0;
    }

    // ───────────── input ─────────────

    void UpdateMenu()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.N)) changeMap = true;
        if (Raylib.IsKeyPressed(KeyboardKey.T))
            menuTeam = menuTeam.Opposite();

        for (int i = 0; i < Classes.All.Length; i++)
            if (Raylib.IsKeyPressed(KeyboardKey.One + i) || Raylib.IsKeyPressed(KeyboardKey.Kp1 + i))
                menuClass = Classes.All[i].Id;

        if (Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.KpEnter) || Raylib.IsKeyPressed(KeyboardKey.Space))
        {
            if (menuTeam != me.Team)
            {
                me.Team = menuTeam;
                if (me.CarryingFlag != null) game.Kill(me, null, "changed team");
                me.PendingClass = menuClass;
                game.Respawn(me);
            }
            else
            {
                game.ChangeClass(me, menuClass);
            }
            yaw = me.Yaw;
            pitch = 0;
            menuOpen = false;
            firstJoin = false;
            swallowKpEnter = Raylib.IsKeyDown(KeyboardKey.KpEnter);
            Raylib.DisableCursor();
        }
        else if (Raylib.IsKeyPressed(KeyboardKey.Escape) && !firstJoin)
        {
            menuOpen = false;
            Raylib.DisableCursor();
        }
        else if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            quit = true;
        }
    }

    void UpdatePlaying(float frame)
    {
        var d = Raylib.GetMouseDelta();
        yaw -= d.X * MouseSensitivity;
        pitch = Math.Clamp(pitch - d.Y * MouseSensitivity, -1.5f, 1.5f);

        // Keypad look: 4/6 turn, 8/2 pitch, 5 re-centres the view.
        if (Raylib.IsKeyDown(KeyboardKey.Kp4)) yaw += KeypadTurnRate * frame;
        if (Raylib.IsKeyDown(KeyboardKey.Kp6)) yaw -= KeypadTurnRate * frame;
        if (Raylib.IsKeyDown(KeyboardKey.Kp8)) pitch += KeypadPitchRate * frame;
        if (Raylib.IsKeyDown(KeyboardKey.Kp2)) pitch -= KeypadPitchRate * frame;
        if (Raylib.IsKeyPressed(KeyboardKey.Kp5)) pitch = 0;
        pitch = Math.Clamp(pitch, -1.5f, 1.5f);

        // Spy: F cycles the enemy-class disguise, G feigns death (numpad * and / as alternatives).
        if (Raylib.IsKeyPressed(KeyboardKey.F) || Raylib.IsKeyPressed(KeyboardKey.KpMultiply)) pendingDisguise = true;
        // Engineer: B builds / demolishes a dispenser (numpad * as an alternative; Spies use it for disguise).
        if (Raylib.IsKeyPressed(KeyboardKey.B) || Raylib.IsKeyPressed(KeyboardKey.KpMultiply)) pendingDispenser = true;
        // Engineer: T builds the teleporter entrance, then the exit, then demolishes the pair (numpad / as an alternative).
        if (Raylib.IsKeyPressed(KeyboardKey.T) || Raylib.IsKeyPressed(KeyboardKey.KpDivide)) pendingTeleporter = true;
        // Demoman: V sets a detpack, C cycles its fuse 5 / 20 / 50 s (numpad * and / as alternatives).
        if (Raylib.IsKeyPressed(KeyboardKey.V) || Raylib.IsKeyPressed(KeyboardKey.KpMultiply)) pendingDetpack = true;
        if (Raylib.IsKeyPressed(KeyboardKey.C) || Raylib.IsKeyPressed(KeyboardKey.KpDivide)) pendingDetpackFuse = true;
        if (Raylib.IsKeyPressed(KeyboardKey.G) || Raylib.IsKeyPressed(KeyboardKey.KpDivide)) pendingFeign = true;

        if (Raylib.IsKeyPressed(KeyboardKey.KpAdd)) pendingSlot = (me.Slot + 1) % me.Class.Slots.Length;
        if (Raylib.IsKeyPressed(KeyboardKey.KpSubtract))
            pendingSlot = (me.Slot + me.Class.Slots.Length - 1) % me.Class.Slots.Length;

        if (Raylib.IsKeyPressed(KeyboardKey.One)) pendingSlot = 0;
        if (Raylib.IsKeyPressed(KeyboardKey.Two)) pendingSlot = 1;
        if (Raylib.IsKeyPressed(KeyboardKey.Three)) pendingSlot = 2;
        float wheel = Raylib.GetMouseWheelMove();
        if (wheel != 0)
            pendingSlot = ((me.Slot + (wheel > 0 ? -1 : 1)) % me.Class.Slots.Length + me.Class.Slots.Length) % me.Class.Slots.Length;

        if (Raylib.IsKeyPressed(KeyboardKey.M))
        {
            menuOpen = true;
            menuClass = me.PendingClass;
            menuTeam = me.Team;
            Raylib.EnableCursor();
        }
        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            quit = true;
        }
    }

    void PushInput()
    {
        var input = new PlayerInput
        {
            SelectSlot = pendingSlot, Yaw = yaw, Pitch = pitch,
            DisguiseNext = pendingDisguise, Feign = pendingFeign, BuildDispenser = pendingDispenser, BuildTeleporter = pendingTeleporter, PlaceDetpack = pendingDetpack, DetpackFuseNext = pendingDetpackFuse,
        };
        if (!menuOpen)
        {
            if (Raylib.IsKeyDown(KeyboardKey.W) || Raylib.IsKeyDown(KeyboardKey.Up)) input.Forward += 1;
            if (Raylib.IsKeyDown(KeyboardKey.S) || Raylib.IsKeyDown(KeyboardKey.Down)) input.Forward -= 1;
            if (Raylib.IsKeyDown(KeyboardKey.D) || Raylib.IsKeyDown(KeyboardKey.Right) || Raylib.IsKeyDown(KeyboardKey.Kp9)) input.Right += 1;
            if (Raylib.IsKeyDown(KeyboardKey.A) || Raylib.IsKeyDown(KeyboardKey.Left) || Raylib.IsKeyDown(KeyboardKey.Kp7)) input.Right -= 1;
            input.Forward = Math.Clamp(input.Forward, -1f, 1f);
            input.Right = Math.Clamp(input.Right, -1f, 1f);
            input.Grenade1 = Raylib.IsKeyDown(KeyboardKey.Q) || Raylib.IsKeyDown(KeyboardKey.Kp1);
            input.Grenade2 = Raylib.IsKeyDown(KeyboardKey.E) || Raylib.IsKeyDown(KeyboardKey.Kp3);
            input.Jump = Raylib.IsKeyDown(KeyboardKey.Space);
            input.Grapple = Raylib.IsKeyDown(KeyboardKey.X) || Raylib.IsMouseButtonDown(MouseButton.Middle);
            input.Fire = Raylib.IsMouseButtonDown(MouseButton.Left) || Raylib.IsKeyDown(KeyboardKey.Kp0);
            bool kpEnter = Raylib.IsKeyDown(KeyboardKey.KpEnter);
            if (!kpEnter) swallowKpEnter = false;
            input.AltFire = Raylib.IsMouseButtonDown(MouseButton.Right)
                            || (kpEnter && !swallowKpEnter) || Raylib.IsKeyDown(KeyboardKey.KpDecimal);
        }
        me.Input = input;
    }

    // ───────────── rendering ─────────────

    Camera3D MakeCamera()
    {
        var eye = me.Eye;
        float fov = me.Alive && me.SniperCharge > 0 ? 22f : 72f;
        float cp = MathF.Cos(me.Alive ? me.Pitch : pitch);
        var yawNow = me.Alive || !menuOpen ? yaw : me.Yaw;
        float pitchNow = me.Alive ? me.Pitch : pitch;
        if (me.Alive && me.ConcussTime > 0)
        {
            // Concussed: the whole view sways (the game also throws your aim off by a similar amount).
            float shake = MathF.Min(1f, me.ConcussTime / 2f);
            yawNow += MathF.Sin(time * 4.3f) * 0.07f * shake;
            pitchNow += MathF.Cos(time * 3.7f) * 0.05f * shake;
            cp = MathF.Cos(pitchNow);
        }
        var fwd = new Vector3(MathF.Sin(yawNow) * cp, MathF.Sin(pitchNow), MathF.Cos(yawNow) * cp);
        return new Camera3D
        {
            Position = eye,
            Target = eye + fwd,
            Up = Vector3.UnitY,
            FovY = fov,
            Projection = CameraProjection.Perspective,
        };
    }

    void Render()
    {
        int w = Raylib.GetScreenWidth(), h = Raylib.GetScreenHeight();
        var cam = MakeCamera();

        Raylib.BeginDrawing();
        Raylib.ClearBackground(Palette.Sky);

        Raylib.BeginMode3D(cam);
        world.Draw(me, time);
        Raylib.EndMode3D();

        viewModel.Update(me, lastFrame);
        viewModel.Draw(me, menuOpen, time);

        if (menuOpen) hud.DrawClassMenu(w, h, menuTeam, menuClass, firstJoin);
        else hud.Draw(me, cam, w, h, Raylib.IsKeyDown(KeyboardKey.Tab));
        Raylib.DrawFPS(w - 90, h - 24);
        string audio = !sound.Ready ? "sound: no device" : sound.Muted ? "sound: OFF (F8)" : "sound: on (F8)";
        Raylib.DrawText(audio, w - 190, h - 44, 14, sound.Ready && !sound.Muted ? Color.LightGray : new Color(255, 170, 120, 255));

        Raylib.EndDrawing();
    }
}
