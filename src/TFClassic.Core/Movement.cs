using System.Numerics;

namespace TFClassic.Core;

/// <summary>Quake/Half-Life style player movement: ground friction, air strafing, stair stepping.</summary>
public static class Movement
{
    public const float Gravity = 800f;
    public const float JumpSpeed = 270f;
    public const float StepHeight = 18f;
    public const float Friction = 4f;
    public const float StopSpeed = 100f;
    public const float Accel = 10f;
    public const float AirAccel = 10f;
    public const float AirCap = 30f;

    static readonly Vector3 Half = Player.HullHalf;
    static readonly Vector3 CenterOffset = new(0, Player.HullHalf.Y, 0);

    public static void Simulate(World world, Player p, float speedScale, float dt)
    {
        var inp = p.Input;
        p.Yaw = inp.Yaw;
        p.Pitch = Math.Clamp(inp.Pitch, -1.5f, 1.5f);

        bool inWater = world.InZone(ZoneKind.Water, p.Position + new Vector3(0, 20, 0));
        p.OnGround = CheckGround(world, p);

        bool jumpPressed = inp.Jump && !p.JumpHeld;
        p.JumpHeld = inp.Jump;

        float fwd = Math.Clamp(inp.Forward, -1f, 1f), side = Math.Clamp(inp.Right, -1f, 1f);
        var f = new Vector3(MathF.Sin(p.Yaw), 0, MathF.Cos(p.Yaw));
        var r = new Vector3(-MathF.Cos(p.Yaw), 0, MathF.Sin(p.Yaw));
        var wish = f * fwd + r * side;
        float mag = wish.Length();
        var wishDir = mag > 1e-4f ? wish / mag : Vector3.Zero;
        float wishSpeed = p.Class.Speed * speedScale * MathF.Min(1f, mag) * (inWater ? 0.7f : 1f);

        if (p.OnGround && jumpPressed)
        {
            p.Velocity.Y = JumpSpeed;
            p.OnGround = false;
        }

        if (p.OnGround)
        {
            ApplyFriction(p, dt);
            Accelerate(p, wishDir, wishSpeed, wishSpeed, Accel, dt);
            if (p.Velocity.Y < 0) p.Velocity.Y = 0;
        }
        else
        {
            Accelerate(p, wishDir, wishSpeed, MathF.Min(wishSpeed, AirCap), AirAccel, dt);
            p.Velocity.Y -= Gravity * dt;
        }

        bool wasOnGround = p.OnGround;
        StepSlideMove(world, p, dt, wasOnGround);

        // Stay glued to the ground when walking down stairs and slopes.
        if (wasOnGround && p.Velocity.Y <= 0f)
            SnapToGround(world, p);

        p.OnGround = CheckGround(world, p);
        if (p.OnGround && p.Velocity.Y < 0f) p.Velocity.Y = 0f;
    }

    static bool CheckGround(World world, Player p)
    {
        if (p.Velocity.Y > 10f) return false;
        return world.Sweep(p.Position + CenterOffset, Half, new Vector3(0, -2f, 0), out _, out var n) && n.Y >= 0.7f;
    }

    static void SnapToGround(World world, Player p)
    {
        var delta = new Vector3(0, -StepHeight, 0);
        if (world.Sweep(p.Position + CenterOffset, Half, delta, out float t, out var n) && n.Y >= 0.7f)
            p.Position.Y -= StepHeight * t - 0.01f;
    }

    static void ApplyFriction(Player p, float dt)
    {
        float speed = MathF.Sqrt(p.Velocity.X * p.Velocity.X + p.Velocity.Z * p.Velocity.Z);
        if (speed < 1f)
        {
            p.Velocity.X = 0;
            p.Velocity.Z = 0;
            return;
        }
        float control = MathF.Max(speed, StopSpeed);
        float drop = control * Friction * dt;
        float scale = MathF.Max(0f, speed - drop) / speed;
        p.Velocity.X *= scale;
        p.Velocity.Z *= scale;
    }

    static void Accelerate(Player p, Vector3 wishDir, float wishSpeed, float cappedSpeed, float accel, float dt)
    {
        if (wishDir == Vector3.Zero) return;
        float current = Vector3.Dot(p.Velocity, wishDir);
        float add = cappedSpeed - current;
        if (add <= 0f) return;
        float amount = MathF.Min(accel * dt * wishSpeed, add);
        p.Velocity += wishDir * amount;
    }

    static void StepSlideMove(World world, Player p, float dt, bool wasOnGround)
    {
        Vector3 startPos = p.Position, startVel = p.Velocity;
        Vector3 pos = startPos, vel = startVel;
        SlideMove(world, ref pos, ref vel, dt);
        Vector3 slidePos = pos, slideVel = vel;

        if (wasOnGround)
        {
            pos = startPos;
            vel = startVel;

            float up = StepHeight;
            if (world.Sweep(pos + CenterOffset, Half, new Vector3(0, up, 0), out float tu, out _)) up *= tu;
            pos.Y += up;

            SlideMove(world, ref pos, ref vel, dt);

            float down = up + 1f;
            bool landed = world.Sweep(pos + CenterOffset, Half, new Vector3(0, -down, 0), out float td, out var n)
                          && n.Y >= 0.7f;
            if (landed)
            {
                pos.Y -= down * td - 0.01f;
                float stepDist = HorizSq(pos - startPos), slideDist = HorizSq(slidePos - startPos);
                if (stepDist > slideDist + 1f)
                {
                    slidePos = pos;
                    slideVel = vel;
                }
            }
        }

        p.Position = slidePos;
        p.Velocity = slideVel;
    }

    static float HorizSq(Vector3 v) => v.X * v.X + v.Z * v.Z;

    static void SlideMove(World world, ref Vector3 pos, ref Vector3 vel, float dt)
    {
        float remaining = dt;
        Span<Vector3> planes = stackalloc Vector3[4];
        int planeCount = 0;

        for (int bump = 0; bump < 4; bump++)
        {
            var delta = vel * remaining;
            if (delta.LengthSquared() < 1e-8f) break;

            if (!world.Sweep(pos + CenterOffset, Half, delta, out float t, out var n))
            {
                pos += delta;
                break;
            }

            pos += delta * t + n * 0.01f;
            remaining *= 1f - t;

            vel -= n * Vector3.Dot(vel, n);
            planes[planeCount++] = n;

            // Wedged into a crease between two planes: slide along the crease.
            for (int i = 0; i < planeCount - 1; i++)
            {
                if (Vector3.Dot(vel, planes[i]) >= -0.001f) continue;
                var crease = Vector3.Cross(planes[i], n);
                if (crease.LengthSquared() < 1e-6f)
                {
                    vel = Vector3.Zero;
                    break;
                }
                crease = Vector3.Normalize(crease);
                vel = crease * Vector3.Dot(crease, vel);
            }

            if (planeCount >= 4) break;
        }
    }
}
