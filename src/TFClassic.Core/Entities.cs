using System.Numerics;

namespace TFClassic.Core;

public struct PlayerInput
{
    public float Forward;   // -1..1
    public float Right;     // -1..1
    public bool Jump;
    public bool Fire;       // held
    public bool AltFire;    // held
    public float Yaw;       // radians; 0 faces +Z, increasing turns left
    public float Pitch;     // radians; positive looks up
    public int SelectSlot;  // -1 = no change, 0..2 = switch weapon
}

public sealed class Player
{
    public int Id { get; init; }
    public string Name { get; set; } = "";
    public Team Team { get; set; }
    public bool IsBot { get; init; }

    public ClassDef Class { get; set; } = Classes.Get(PlayerClassId.Soldier);
    public PlayerClassId PendingClass { get; set; }

    public Vector3 Position;   // feet
    public Vector3 Velocity;
    public float Yaw, Pitch;
    public bool OnGround;
    public bool JumpHeld;

    public float Health, Armor;
    public int[] Ammo = new int[5];
    public int Slot;
    public bool Alive;
    public float RespawnTimer;
    public float SpawnProtect;
    public float FireCooldown;
    public float SniperCharge;
    public bool PrevAlt;
    public float ResupplyCooldown;
    public int SpawnCount;

    public float BurnTime, BurnTick;
    public Player? BurnOwner;

    public Flag? CarryingFlag;
    public int Frags, Deaths, Captures;

    public PlayerInput Input;

    public const float EyeHeight = 64f;
    public static readonly Vector3 HullHalf = new(16, 36, 16);

    public Vector3 Eye => Position + new Vector3(0, EyeHeight, 0);
    public Vector3 Center => Position + new Vector3(0, HullHalf.Y, 0);
    public Aabb Hull => Aabb.FromCenter(Center, HullHalf);
    public WeaponDef Weapon => Weapons.Get(Class.Slots[Slot]);

    public Vector3 Forward
    {
        get
        {
            float cp = MathF.Cos(Pitch);
            return new Vector3(MathF.Sin(Yaw) * cp, MathF.Sin(Pitch), MathF.Cos(Yaw) * cp);
        }
    }

    public Vector3 Right => new(-MathF.Cos(Yaw), 0, MathF.Sin(Yaw));
}

public enum ProjectileKind { Rocket, Grenade, Pipe }

public sealed class Projectile
{
    public ProjectileKind Kind;
    public Player Owner = null!;
    public Team Team;
    public Vector3 Position, Velocity;
    public float Age, Fuse;
    public float Damage, Splash;
    public bool Stuck, Dead;
}

public sealed class Flag
{
    public Team Team { get; init; }
    public Vector3 Home { get; init; }
    public Vector3 Position;
    public Player? Carrier;
    public float DropTimer;

    public bool AtHome => Carrier == null && DropTimer <= 0f && Vector3.DistanceSquared(Position, Home) < 1f;
    public bool Dropped => Carrier == null && !AtHome;
}

public enum EffectKind { Tracer, Explosion, Flame, Gib, Heal }

public sealed class Effect
{
    public EffectKind Kind;
    public Vector3 A, B;
    public float Life, MaxLife, Radius;
    public Team Team;
}

public sealed record GameEvent(float Time, string Text, Team? Team);
