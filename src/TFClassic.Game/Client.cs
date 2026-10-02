using System.Numerics;
using Raylib_cs;
using TFClassic.Core;

namespace TFClassic.Game;

/// <summary>Owns input, the fixed-timestep loop, the camera and the frame layout.</summary>
sealed class Client
{
    const float Dt = 1f / 60f;
    const float MouseSensitivity = 0.0022f;

    readonly Core.Game game;
    readonly Player me;
    readonly WorldRenderer world;
    readonly Hud hud;

    bool menuOpen = true;
    bool firstJoin = true;
    PlayerClassId menuClass;
    Team menuTeam;

    float yaw, pitch;
    int pendingSlot = -1;
    double accumulator;
    float time;

    public Client(Core.Game game, Player me, bool headless)
    {
        this.game = game;
        this.me = me;
        world = new WorldRenderer(game);
        hud = new Hud(game);
        yaw = me.Yaw;
        menuClass = me.PendingClass;
        menuTeam = me.Team;
        menuOpen = !headless;
    }

    public void Run()
    {
        Raylib.EnableCursor();
        while (!Raylib.WindowShouldClose())
        {
            float frame = MathF.Min(Raylib.GetFrameTime(), 0.1f);
            time += frame;

            if (menuOpen) UpdateMenu();
            else UpdatePlaying();

            accumulator += frame;
            while (accumulator >= Dt)
            {
                if (menuOpen && firstJoin) me.SpawnProtect = 2f;   // don't get shot while picking a class
                else PushInput();
                game.Tick(Dt);
                pendingSlot = -1;
                accumulator -= Dt;
            }

            Render();
        }
    }

    /// <summary>Runs the sim for a while with the human idle, then saves a frame and exits.</summary>
    public int RunScreenshot(string path, float warmupSeconds, float[]? at)
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
        if (Raylib.IsKeyPressed(KeyboardKey.T))
            menuTeam = menuTeam.Opposite();

        for (int i = 0; i < Classes.All.Length; i++)
            if (Raylib.IsKeyPressed(KeyboardKey.One + i))
                menuClass = Classes.All[i].Id;

        if (Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.Space))
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
            Raylib.DisableCursor();
        }
        else if (Raylib.IsKeyPressed(KeyboardKey.Escape) && !firstJoin)
        {
            menuOpen = false;
            Raylib.DisableCursor();
        }
        else if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            Raylib.CloseWindow();
            Environment.Exit(0);
        }
    }

    void UpdatePlaying()
    {
        var d = Raylib.GetMouseDelta();
        yaw -= d.X * MouseSensitivity;
        pitch = Math.Clamp(pitch - d.Y * MouseSensitivity, -1.5f, 1.5f);

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
            Raylib.CloseWindow();
            Environment.Exit(0);
        }
    }

    void PushInput()
    {
        var input = new PlayerInput { SelectSlot = pendingSlot, Yaw = yaw, Pitch = pitch };
        if (!menuOpen)
        {
            if (Raylib.IsKeyDown(KeyboardKey.W)) input.Forward += 1;
            if (Raylib.IsKeyDown(KeyboardKey.S)) input.Forward -= 1;
            if (Raylib.IsKeyDown(KeyboardKey.D)) input.Right += 1;
            if (Raylib.IsKeyDown(KeyboardKey.A)) input.Right -= 1;
            input.Jump = Raylib.IsKeyDown(KeyboardKey.Space);
            input.Fire = Raylib.IsMouseButtonDown(MouseButton.Left);
            input.AltFire = Raylib.IsMouseButtonDown(MouseButton.Right);
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
        var fwd = new Vector3(MathF.Sin(yawNow) * cp, MathF.Sin(me.Alive ? me.Pitch : pitch), MathF.Cos(yawNow) * cp);
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

        if (menuOpen) hud.DrawClassMenu(w, h, menuTeam, menuClass, firstJoin);
        else hud.Draw(me, cam, w, h, Raylib.IsKeyDown(KeyboardKey.Tab));
        Raylib.DrawFPS(w - 90, h - 24);

        Raylib.EndDrawing();
    }
}
