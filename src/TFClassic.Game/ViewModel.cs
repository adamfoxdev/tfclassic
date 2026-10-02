using System.Numerics;
using Raylib_cs;
using TFClassic.Core;
using static TFClassic.Desktop.WeaponModels;

namespace TFClassic.Desktop;

/// <summary>
/// The weapon (or grenade) you hold, drawn in view space over the world: bob while walking, sway when you turn,
/// recoil and a muzzle flash when you fire, a swing for melee weapons, a draw-in when you change weapon and a
/// throw for grenades. It is drawn in its own pass without a depth buffer so it never clips into walls.
/// </summary>
sealed class ViewModel
{
    static readonly Camera3D Camera = new()
    {
        Position = Vector3.Zero,
        Target = new Vector3(0, 0, -1),
        Up = Vector3.UnitY,
        FovY = 62f,
        Projection = CameraProjection.Perspective,
    };

    readonly WeaponAnimator anim = new();

    const float Scale = 0.62f;     // view-model units are scaled down so a weapon fills a sensible part of the screen

    /// <summary>Stops animation timers advancing (so a screenshot can hold a mid-recoil frame).</summary>
    public bool Frozen { get => anim.Frozen; set => anim.Frozen = value; }

    public void Fire(WeaponDef weapon) => anim.TriggerFire(weapon);

    public void Settle() => anim.Settle();

    public void Update(Player me, float dt) => anim.Update(me, dt);

    public void Draw(Player me, bool hidden, float time)
    {
        if (hidden || !me.Alive || me.Feigning) return;
        if (me.Weapon.Mode == FireMode.SniperCharge && me.SniperCharge > 0) return;   // looking through the scope

        Raylib.BeginMode3D(Camera);
        Rlgl.DisableDepthTest();
        Rlgl.EnableBackfaceCulling();

        float bobX = MathF.Sin(anim.BobPhase) * 0.55f * anim.BobAmount;
        float bobY = MathF.Abs(MathF.Cos(anim.BobPhase)) * -0.5f * anim.BobAmount;

        if (me.Primed >= 0 || anim.Thrown > 0)
            DrawGrenade(me, time, bobX, bobY);
        else if (Models.TryGetValue(me.Weapon.Id, out var model))
            DrawWeapon(me, model, bobX, bobY);

        Rlgl.EnableDepthTest();
        Raylib.EndMode3D();
    }

    void DrawWeapon(Player me, WeaponModel m, float bobX, float bobY)
    {
        float drawT = anim.Draw / WeaponAnimator.DrawTime;                                   // 1 -> 0 as the weapon comes up
        float lower = drawT * drawT * 9f;
        float swingA = MathF.Sin(MathF.PI * (1f - anim.Swing)) * (anim.Swing > 0 ? 1f : 0f);

        Rlgl.PushMatrix();
        Rlgl.Translatef(m.Rest.X * 0.82f + bobX - anim.SwayYaw * 0.35f, m.Rest.Y * 0.8f + 0.2f + bobY - lower + anim.SwayPitch * 0.25f, m.Rest.Z + anim.Kick * m.Kick);
        Rlgl.Scalef(Scale, Scale, Scale);
        Rlgl.Rotatef(-3f - anim.SwayYaw * 0.6f, 0, 1, 0);                                 // angled slightly toward the crosshair
        Rlgl.Rotatef(anim.Kick * m.KickPitch + anim.SwayPitch, 1, 0, 0);
        if (m.Melee)
        {
            Rlgl.Translatef(-swingA * 4.5f, -swingA * 1.5f, -swingA * 4f);          // slash across and forward
            Rlgl.Rotatef(m.Roll + swingA * -55f, 1, 0, 0);
            Rlgl.Rotatef(swingA * 32f, 0, 1, 0);
            Rlgl.Rotatef(swingA * -25f, 0, 0, 1);
        }

        DrawSorted(m.Parts.Concat(ArmParts(me)));

        if (anim.Flash > 0 && m.Muzzle is { } muzzle)
        {
            float f = anim.Flash / WeaponAnimator.FlashTime;
            Raylib.DrawSphere(muzzle + new Vector3(0, 0, -1.5f), 2.1f * f + 0.6f, new Color(255, 230, 140, (int)(230 * f)));
            Raylib.DrawSphere(muzzle + new Vector3(0, 0, -0.6f), 1.2f * f + 0.3f, new Color(255, 255, 230, (int)(255 * f)));
        }
        Rlgl.PopMatrix();
    }

    /// <summary>The right hand and a short sleeve (in the player's team colour) gripping the weapon.</summary>
    static IEnumerable<Part> ArmParts(Player me)
    {
        var sleeve = Palette.Shade(Palette.Team(me.Team), 0.62f);
        yield return P(-1f, 1.2f, -2.5f, -.3f, 1.2f, 3.4f, Skin);          // hand
        yield return P(-1.2f, 1.5f, -3.1f, -.4f, 3.2f, 8f, sleeve);        // forearm
    }

    /// <summary>
    /// The view model is drawn without a depth buffer, so parts must go down far to near (painter's order);
    /// back-face culling takes care of each box's own hidden faces.
    /// </summary>
    static void DrawSorted(IEnumerable<Part> parts)
    {
        foreach (var part in parts.OrderBy(p => (p.Min.Z + p.Max.Z) * 0.5f))
            Draw3D.SolidBox(part.Min, part.Max, part.Color);
    }

    void DrawGrenade(Player me, float time, float bobX, float bobY)
    {
        var kind = me.Primed >= 0 ? me.Class.GrenadeKindOf(me.Primed) : anim.ThrownKind;
        var body = GrenadeColor(kind);
        if (me.Primed >= 0 && me.PrimedTimer < 1f && (int)(me.PrimedTimer * (me.PrimedTimer < 0.4f ? 14 : 7)) % 2 == 0)
            body = new Color(255, 90, 60, 255);                                                                  // about to go off

        float t = anim.Thrown > 0 ? 1f - anim.Thrown / WeaponAnimator.ThrowTime : 0f;                                                        // 0 -> 1 through the throw
        Rlgl.PushMatrix();
        Rlgl.Translatef(3.2f + bobX - t * 2.5f, -2.6f + bobY + MathF.Sin(t * MathF.PI) * 1.2f - t * 1.5f, -8.5f - t * 9f);
        Rlgl.Scalef(Scale, Scale, Scale);
        Rlgl.Rotatef(t * -50f, 1, 0, 0);
        var parts = new List<Part>
        {
            P(-1.5f, 1.5f, -1.2f, 2.2f, -1.5f, 1.5f, body),               // body
            P(-.9f, .9f, 2.2f, 2.9f, -.9f, .9f, Dark),                    // fuse cap
            P(-.3f, .3f, -.8f, 2.9f, -1.9f, -1.5f, Steel),                // safety lever
        };
        if (me.Primed >= 0) parts.AddRange(ArmParts(me));
        DrawSorted(parts);
        Rlgl.PopMatrix();
    }
}
