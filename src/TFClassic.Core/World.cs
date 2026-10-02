using System.Numerics;

namespace TFClassic.Core;

public enum Material { Ground, Wall, Floor, Crate, Stairs, Roof, RedTeam, BlueTeam, Bridge, RiverBed }

public enum ZoneKind { Resupply, Water }

public sealed class Solid
{
    public Aabb Box { get; }
    public Material Material { get; }

    public Solid(Aabb box, Material material)
    {
        Box = box;
        Material = material;
    }
}

public sealed class Zone
{
    public Aabb Box { get; }
    public ZoneKind Kind { get; }
    public Team? Team { get; }

    public Zone(Aabb box, ZoneKind kind, Team? team)
    {
        Box = box;
        Kind = kind;
        Team = team;
    }
}

/// <summary>Static level geometry: a soup of axis-aligned boxes plus trigger zones.</summary>
public sealed class World
{
    public List<Solid> Solids { get; } = new();
    public List<Zone> Zones { get; } = new();

    public void AddSolid(Vector3 min, Vector3 max, Material material) =>
        Solids.Add(new Solid(new Aabb(min, max), material));

    public void AddZone(Vector3 min, Vector3 max, ZoneKind kind, Team? team = null) =>
        Zones.Add(new Zone(new Aabb(min, max), kind, team));

    /// <summary>Sweeps a box (centre + half extents) along delta; t in [0,1] is how far it gets.</summary>
    public bool Sweep(Vector3 center, Vector3 half, Vector3 delta, out float t, out Vector3 normal)
    {
        t = 1f;
        normal = Vector3.Zero;
        bool hit = false;
        foreach (var s in Solids)
        {
            var expanded = s.Box.Expand(half);
            if (Collision.RayAabb(center, delta, expanded, out float ht, out Vector3 n) && ht < t)
            {
                t = ht;
                normal = n;
                hit = true;
            }
        }
        return hit;
    }

    public bool Raycast(Vector3 from, Vector3 to, out float t, out Vector3 normal) =>
        Sweep(from, Vector3.Zero, to - from, out t, out normal);

    public bool LineOfSight(Vector3 a, Vector3 b) => !Raycast(a, b, out _, out _);

    /// <summary>True if a box overlaps any solid.</summary>
    public bool Overlaps(Vector3 center, Vector3 half)
    {
        var box = Aabb.FromCenter(center, half);
        foreach (var s in Solids)
            if (s.Box.Intersects(box)) return true;
        return false;
    }

    public bool InZone(ZoneKind kind, Vector3 point, Team? team = null)
    {
        foreach (var z in Zones)
        {
            if (z.Kind != kind) continue;
            if (z.Team.HasValue && team.HasValue && z.Team != team) continue;
            if (z.Box.Contains(point)) return true;
        }
        return false;
    }
}
