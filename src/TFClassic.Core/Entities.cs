using System.Numerics;

namespace TFClassic.Core;

public struct PlayerInput
{
    public float Forward;   // -1..1
    public float Right;     // -1..1
    public bool Jump;
    public bool Fire;       // held
    public bool DisguiseNext; // edge: start disguising as the next enemy class (Spy)
    public bool Feign;        // edge: toggle feigning death (Spy)
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

    // Spy state
    public Team? DisguiseTeam;
    public PlayerClassId DisguiseClass;
    public float DisguiseTimer;
    public bool Feigning;
    public float FeignTimer, FeignCooldown;
    public float SlowTime;

    /// <summary>Fully disguised (the 2 s transition is over).</summary>
    public bool IsDisguised => DisguiseTeam.HasValue && DisguiseTimer <= 0f;
    public bool IsDisguisedAs(Team t) => IsDisguised && DisguiseTeam == t;
    /// <summary>Whether automatic defences and bots on <paramref name="team"/> would pick this player as a target.</summary>
    public bool IsTargetableBy(Team team) => Alive && Team != team && !Feigning && !IsDisguisedAs(team);
    public int Frags, Deaths, Captures;
    public int Metal;
    public string Notice = "";
    public float NoticeTimer;

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
    public string Label = "Rocket";
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

/// <summary>An engineer-built automatic turret. Levels 1-3; level 3 also fires rockets.</summary>
public sealed class Sentry
{
    public const int BuildCost = 130;
    public const int UpgradeCost = 100;

    static readonly int[] HealthByLevel = { 150, 180, 220 };
    static readonly int[] AmmoByLevel = { 100, 120, 150 };
    static readonly float[] CooldownByLevel = { 0.22f, 0.11f, 0.11f };
    static readonly float[] RangeByLevel = { 900f, 1000f, 1100f };
    static readonly float[] TurnByLevel = { 3f, 4.5f, 6f };

    public Player Owner = null!;
    public Team Team;
    public Vector3 Position;   // feet
    public float Yaw;
    public int Level = 1;
    public float Health = 150;
    public float SabotageTimer;
    public Player? Saboteur;
    public int Ammo = 100, Rockets;
    public float BuildTimer = 3f;
    public float FireCooldown, RocketCooldown, RetargetTimer;
    public Player? Target;
    public bool Dead;

    public static readonly Vector3 HullHalf = new(14, 20, 14);
    public Aabb Hull => Aabb.FromCenter(Position + new Vector3(0, HullHalf.Y, 0), HullHalf);
    public Vector3 Muzzle => Position + new Vector3(0, 34, 0);

    public int MaxHealth => HealthByLevel[Level - 1];
    public int MaxAmmo => AmmoByLevel[Level - 1];
    public float Cooldown => CooldownByLevel[Level - 1];
    public float Range => RangeByLevel[Level - 1];
    public float TurnRate => TurnByLevel[Level - 1];
    public bool Building => BuildTimer > 0;
    public bool Sabotaged => SabotageTimer > 0;
}
