using System.Numerics;

namespace TFClassic.Core;

/// <summary>Axis-aligned box. World units are Half-Life units (player is 32x32x72).</summary>
public readonly struct Aabb
{
    public readonly Vector3 Min;
    public readonly Vector3 Max;

    public Aabb(Vector3 min, Vector3 max)
    {
        Min = min;
        Max = max;
    }

    public static Aabb FromCenter(Vector3 center, Vector3 half) => new(center - half, center + half);

    public Vector3 Center => (Min + Max) * 0.5f;
    public Vector3 Size => Max - Min;

    public Aabb Expand(Vector3 amount) => new(Min - amount, Max + amount);

    public bool Contains(Vector3 p) =>
        p.X >= Min.X && p.X <= Max.X &&
        p.Y >= Min.Y && p.Y <= Max.Y &&
        p.Z >= Min.Z && p.Z <= Max.Z;

    public bool Intersects(Aabb o) =>
        Min.X < o.Max.X && Max.X > o.Min.X &&
        Min.Y < o.Max.Y && Max.Y > o.Min.Y &&
        Min.Z < o.Max.Z && Max.Z > o.Min.Z;

    /// <summary>Distance from a point to the nearest point on the box (0 when inside).</summary>
    public float DistanceTo(Vector3 p)
    {
        var c = Vector3.Clamp(p, Min, Max);
        return Vector3.Distance(c, p);
    }
}

public static class Collision
{
    static float Get(Vector3 v, int i) => i == 0 ? v.X : i == 1 ? v.Y : v.Z;

    static Vector3 Axis(int i, float sign) =>
        i == 0 ? new Vector3(sign, 0, 0) : i == 1 ? new Vector3(0, sign, 0) : new Vector3(0, 0, sign);

    /// <summary>
    /// Segment (origin → origin+delta) against a box. t is the entry fraction in [0,1].
    /// Segments that start inside the box do not report a hit.
    /// </summary>
    public static bool RayAabb(Vector3 origin, Vector3 delta, in Aabb box, out float t, out Vector3 normal)
    {
        t = 0;
        normal = Vector3.Zero;
        float tEnter = float.NegativeInfinity, tExit = float.PositiveInfinity;
        int hitAxis = -1;
        float hitSign = 0;

        for (int i = 0; i < 3; i++)
        {
            float o = Get(origin, i), d = Get(delta, i);
            float mn = Get(box.Min, i), mx = Get(box.Max, i);
            if (MathF.Abs(d) < 1e-9f)
            {
                if (o < mn || o > mx) return false;
                continue;
            }

            float inv = 1f / d;
            float t1 = (mn - o) * inv, t2 = (mx - o) * inv;
            if (t1 > t2) (t1, t2) = (t2, t1);
            if (t1 > tEnter)
            {
                tEnter = t1;
                hitAxis = i;
                hitSign = d > 0 ? -1f : 1f;
            }
            if (t2 < tExit) tExit = t2;
            if (tEnter > tExit) return false;
        }

        if (hitAxis < 0 || tEnter < 0f || tEnter > 1f) return false;
        t = tEnter;
        normal = Axis(hitAxis, hitSign);
        return true;
    }
}
