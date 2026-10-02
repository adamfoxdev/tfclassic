using System.Numerics;

namespace TFClassic.Core;

/// <summary>How loud a sound is for a listener and which of the pre-baked stereo pans to use.</summary>
public readonly record struct Mix(float Volume, int PanIndex);

/// <summary>Distance attenuation and stereo panning of world sounds, kept free of any audio library so it can be tested.</summary>
public static class SoundMix
{
    /// <summary>Number of pre-rendered stereo positions per sound: 0 = hard left ... 4 = hard right, 2 = centre.</summary>
    public const int PanLevels = 5;
    public const int Centre = PanLevels / 2;

    /// <summary>Events with a range at least this big are "everywhere" sounds (flag events, match won).</summary>
    public const float GlobalRange = 50000f;

    public static Mix Compute(in SoundEvent e, Vector3 listenerPos, Vector3 listenerRight, int listenerId)
    {
        // The hit-confirm tick is for the shooter's ears only.
        if (e.Id == SoundId.HitMarker)
            return e.SourcePlayerId == listenerId ? new Mix(e.Volume, Centre) : new Mix(0f, Centre);

        if (e.Range >= GlobalRange) return new Mix(e.Volume * 0.8f, Centre);

        var to = e.Position - listenerPos;
        float dist = to.Length();
        float fall = Math.Clamp(1f - dist / e.Range, 0f, 1f);
        float volume = e.Volume * fall * MathF.Sqrt(fall);          // fall^1.5: drops off quickly then lingers

        // Your own footsteps, shots and so on come from "inside your head", and very close sounds aren't directional.
        if (e.SourcePlayerId == listenerId || dist < 40f) return new Mix(volume, Centre);

        to.Y = 0;
        float flat = to.Length();
        if (flat < 1e-3f) return new Mix(volume, Centre);
        float lateral = Vector3.Dot(to / flat, listenerRight);       // -1 = directly left, +1 = directly right
        int index = (int)MathF.Round((lateral + 1f) / 2f * (PanLevels - 1));
        return new Mix(volume, Math.Clamp(index, 0, PanLevels - 1));
    }

    /// <summary>Left/right gains for a pan index: centre is (1, 1); the far side fades to 15%.</summary>
    public static (float Left, float Right) Gains(int panIndex)
    {
        float p = (Math.Clamp(panIndex, 0, PanLevels - 1) - Centre) / (float)Centre;   // -1 left ... +1 right
        float left = p > 0 ? 1f - 0.85f * p : 1f;
        float right = p < 0 ? 1f + 0.85f * p : 1f;
        return (left, right);
    }
}
