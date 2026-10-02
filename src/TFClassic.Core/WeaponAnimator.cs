using System.Numerics;

namespace TFClassic.Core;

/// <summary>
/// The animation state of the weapon (or grenade) a player holds in first person: recoil, muzzle flash, melee swing,
/// draw-in, grenade throw, walking bob and turning sway. It only watches the Player, so it needs no graphics and can be tested.
/// </summary>
public sealed class WeaponAnimator
{
    public const float DrawTime = 0.3f, ThrowTime = 0.28f, FlashTime = 0.07f;

    int lastSlot = -1, lastSpawn = -1, lastPrimed = -1;
    float lastCooldown, lastYaw, lastPitch;

    public float Kick { get; private set; }        // 1 right after a shot, fading to 0
    public float Flash { get; private set; }       // seconds of muzzle flash left
    public float Swing { get; private set; }       // melee swing progress left, 1 -> 0
    public float SwingLength { get; private set; } = 0.4f;
    public float Draw { get; private set; }        // seconds of draw-in left
    public float Thrown { get; private set; }      // seconds of throw animation left
    public GrenadeKind ThrownKind { get; private set; }
    public float BobPhase { get; private set; }
    public float BobAmount { get; private set; }
    public float SwayYaw { get; private set; }
    public float SwayPitch { get; private set; }

    /// <summary>Stops the timers advancing (so a screenshot can hold a mid-recoil frame).</summary>
    public bool Frozen { get; set; }

    public static bool IsMelee(FireMode mode) => mode is FireMode.Melee or FireMode.Heal or FireMode.Wrench or FireMode.Backstab;

    /// <summary>Starts the firing animation by hand (used by the screenshot setup).</summary>
    public void TriggerFire(WeaponDef weapon)
    {
        Kick = 1f;
        Flash = IsMelee(weapon.Mode) ? 0f : FlashTime;
        Swing = IsMelee(weapon.Mode) ? 0.45f : 0f;                   // caught mid-swing
        SwingLength = MathF.Max(weapon.Cooldown, 0.2f);
    }

    /// <summary>Skips the draw-in animation.</summary>
    public void Settle() => Draw = 0;

    public void Update(Player me, float dt)
    {
        if (Frozen) return;
        if (dt <= 0) dt = 1f / 60f;

        // Respawn or death resets everything (and a fresh spawn raises the weapon).
        if (!me.Alive || lastSpawn != me.SpawnCount)
        {
            lastSpawn = me.SpawnCount;
            lastSlot = me.Slot;
            lastCooldown = me.FireCooldown;
            lastPrimed = me.Primed;
            lastYaw = me.Yaw;
            lastPitch = me.Pitch;
            Kick = Flash = Swing = Thrown = 0;
            Draw = me.Alive ? 0.35f : 0f;
            return;
        }

        var weapon = me.Weapon;
        bool switched = me.Slot != lastSlot;
        if (switched) Draw = DrawTime;

        // A shot is the cooldown jumping up. (Switching weapons also sets a cooldown, so that tick doesn't count.)
        bool fired = !switched && me.FireCooldown > lastCooldown + 0.001f && me.FireCooldown > 0.03f;
        if (fired)
        {
            Kick = 1f;
            if (IsMelee(weapon.Mode)) { Swing = 1f; SwingLength = MathF.Max(weapon.Cooldown, 0.2f); }
            else Flash = FlashTime;
        }

        // Grenades: cooking raises the grenade; letting go plays a throw and then brings the weapon back.
        if (me.Primed >= 0 && lastPrimed < 0) Draw = 0.2f;
        if (lastPrimed >= 0 && me.Primed < 0)
        {
            Thrown = ThrowTime;
            ThrownKind = me.Class.GrenadeKindOf(lastPrimed);
            Draw = 0.35f;
        }

        // Walking bob and turning sway.
        float speed = MathF.Sqrt(me.Velocity.X * me.Velocity.X + me.Velocity.Z * me.Velocity.Z);
        float targetBob = me.OnGround ? MathF.Min(1f, speed / 280f) : 0f;
        BobAmount += (targetBob - BobAmount) * MathF.Min(1f, dt * 8f);
        BobPhase += speed * dt * 0.022f;

        float dYaw = Angles.Diff(lastYaw, me.Yaw), dPitch = me.Pitch - lastPitch;
        SwayYaw = Math.Clamp(SwayYaw + dYaw * 18f, -4f, 4f);
        SwayPitch = Math.Clamp(SwayPitch - dPitch * 18f, -3f, 3f);
        float decay = MathF.Exp(-dt * 9f);
        SwayYaw *= decay;
        SwayPitch *= decay;

        Kick = MathF.Max(0f, Kick - dt * 8f);
        Flash = MathF.Max(0f, Flash - dt);
        Swing = MathF.Max(0f, Swing - dt / SwingLength);
        Draw = MathF.Max(0f, Draw - dt);
        Thrown = MathF.Max(0f, Thrown - dt);

        lastSlot = me.Slot;
        lastCooldown = me.FireCooldown;
        lastPrimed = me.Primed;
        lastYaw = me.Yaw;
        lastPitch = me.Pitch;
    }
}
