namespace TFClassic.Core;

/// <summary>
/// Generates every game sound procedurally (filtered noise, sweeps, tones) so the game needs no audio files.
/// Output is mono floats in [-1, 1] at <see cref="SampleRate"/>; deterministic for a given SoundId.
/// </summary>
public static class SoundSynth
{
    public const int SampleRate = 22050;

    enum Wave { Sine, Square, Triangle }

    public static float[] Generate(SoundId id)
    {
        var s = new Synth((int)id * 7919 + 17);
        switch (id)
        {
            // ───────── weapons ─────────
            case SoundId.Shotgun:
                s.Len(0.3f); s.Noise(0, 0.3f, 0.9f, 0.35f, 14); s.Tone(0, 0.15f, 110, 55, 0.8f, 20); break;
            case SoundId.Nailgun:
                s.Len(0.07f); s.Noise(0, 0.05f, 0.6f, 0.8f, 70, hp: true); s.Tone(0, 0.05f, 900, 500, 0.5f, 60, Wave.Square); break;
            case SoundId.SuperNailgun:
                s.Len(0.09f); s.Noise(0, 0.07f, 0.65f, 0.7f, 55, hp: true); s.Tone(0, 0.07f, 700, 350, 0.5f, 45, Wave.Square); break;
            case SoundId.RocketLaunch:
                s.Len(0.7f); s.Noise(0, 0.6f, 0.7f, 0.12f, 4, attack: 0.03f); s.Tone(0, 0.3f, 80, 40, 0.7f, 8); break;
            case SoundId.GrenadeLauncher:
                s.Len(0.3f); s.Tone(0, 0.22f, 150, 60, 0.9f, 14); s.Noise(0, 0.06f, 0.4f, 0.3f, 40); break;
            case SoundId.PipeLaunch:
                s.Len(0.25f); s.Tone(0, 0.18f, 220, 90, 0.8f, 16); s.Noise(0, 0.05f, 0.4f, 0.4f, 45); break;
            case SoundId.AssaultCannon:
                s.Len(0.1f); s.Noise(0, 0.06f, 0.8f, 0.5f, 45); s.Tone(0, 0.08f, 200, 100, 0.7f, 35); break;
            case SoundId.AutoRifle:
                s.Len(0.08f); s.Noise(0, 0.05f, 0.7f, 0.7f, 60); s.Tone(0, 0.04f, 1200, 600, 0.3f, 70); break;
            case SoundId.SniperShot:
                s.Len(0.9f); s.Noise(0, 0.1f, 1f, 0.7f, 30); s.Tone(0, 0.7f, 70, 35, 0.9f, 4); s.Noise(0.02f, 0.6f, 0.35f, 0.08f, 5); break;
            case SoundId.Flame:
                s.Len(0.16f); s.Noise(0, 0.14f, 0.6f, 0.2f, 12, attack: 0.01f); break;
            case SoundId.Tranq:
                s.Len(0.15f); s.Noise(0, 0.12f, 0.5f, 0.9f, 25, hp: true); s.Tone(0, 0.06f, 1800, 900, 0.2f, 40); break;
            case SoundId.MeleeSwing:
                s.Len(0.2f); s.Noise(0, 0.16f, 0.5f, 0.25f, 18, attack: 0.05f); break;
            case SoundId.MeleeHit:
                s.Len(0.15f); s.Tone(0, 0.12f, 120, 70, 0.9f, 24); s.Noise(0, 0.05f, 0.5f, 0.4f, 50); break;
            case SoundId.Backstab:
                s.Len(0.4f); s.Tone(0, 0.12f, 120, 70, 0.9f, 24); s.Noise(0, 0.05f, 0.5f, 0.4f, 50);
                s.Tone(0.03f, 0.25f, 500, 180, 0.5f, 12, Wave.Square); s.Noise(0.02f, 0.2f, 0.4f, 0.2f, 14); break;
            case SoundId.SentryFire:
                s.Len(0.06f); s.Tone(0, 0.035f, 1500, 700, 0.5f, 80, Wave.Square); s.Noise(0, 0.03f, 0.5f, 0.8f, 90); break;

            // ───────── blasts and effects ─────────
            case SoundId.Explosion:
                s.Len(1.1f); s.Noise(0, 1f, 1f, 0.08f, 3.2f); s.Noise(0, 0.15f, 0.8f, 0.5f, 25); s.Tone(0, 0.6f, 70, 30, 0.9f, 4); break;
            case SoundId.DetpackBlast:
                s.Len(1.7f); s.Noise(0, 1.5f, 1f, 0.06f, 2.2f); s.Tone(0, 1f, 55, 22, 1f, 2.5f); s.Noise(0, 0.2f, 0.9f, 0.5f, 20); break;
            case SoundId.ConcussionBlast:
                s.Len(0.7f); s.Tone(0, 0.6f, 320, 35, 0.9f, 4.5f); s.Noise(0, 0.3f, 0.6f, 0.15f, 8); break;
            case SoundId.NapalmWhoosh:
                s.Len(1f); s.Noise(0, 0.9f, 0.8f, 0.07f, 3.5f, attack: 0.06f);
                for (int i = 0; i < 9; i++) s.Noise(0.1f + i * 0.08f, 0.03f, 0.5f, 0.6f, 60, hp: true); break;
            case SoundId.EmpZap:
                s.Len(0.6f); s.Tone(0, 0.5f, 2200, 150, 0.45f, 5, Wave.Square); s.Noise(0, 0.5f, 0.5f, 0.6f, 6);
                for (int i = 0; i < 7; i++) s.Noise(i * 0.06f, 0.02f, 0.8f, 0.9f, 80, hp: true); break;
            case SoundId.GasHiss:
                s.Len(1f); s.Noise(0, 0.9f, 0.5f, 0.95f, 3, attack: 0.08f, hp: true); break;
            case SoundId.NailSpray:
                s.Len(0.5f); for (int i = 0; i < 10; i++) s.Noise(i * 0.04f, 0.02f, 0.5f, 0.8f, 90, hp: true); break;
            case SoundId.CaltropScatter:
                s.Len(0.35f); for (int i = 0; i < 5; i++) s.Tone(i * 0.045f, 0.06f, 3200 + i * 230, 3000, 0.4f, 55); break;
            case SoundId.Sabotage:
                s.Len(0.5f); s.Tone(0, 0.4f, 800, 200, 0.3f, 6, Wave.Square);
                for (int i = 0; i < 6; i++) s.Noise(i * 0.06f, 0.025f, 0.7f, 0.85f, 70, hp: true); break;
            case SoundId.GrenadePin:
                s.Len(0.1f); s.Tone(0, 0.02f, 2400, 2400, 0.5f, 150); s.Tone(0.04f, 0.04f, 1600, 1600, 0.5f, 90); break;
            case SoundId.GrenadeThrow:
                s.Len(0.25f); s.Noise(0, 0.2f, 0.4f, 0.2f, 12, attack: 0.04f); break;
            case SoundId.GrenadeBounce:
                s.Len(0.1f); s.Noise(0, 0.06f, 0.5f, 0.25f, 45); s.Tone(0, 0.05f, 300, 150, 0.4f, 40); break;
            case SoundId.DetpackBeep:
                s.Len(0.12f); s.Tone(0, 0.09f, 1500, 1500, 0.5f, 25); break;

            // ───────── players ─────────
            case SoundId.Hurt:
                s.Len(0.22f); s.Tone(0, 0.18f, 320, 160, 0.6f, 14, Wave.Triangle); s.Noise(0, 0.12f, 0.35f, 0.3f, 20); break;
            case SoundId.Death:
                s.Len(0.6f); s.Tone(0, 0.5f, 260, 70, 0.7f, 5, Wave.Triangle); s.Noise(0, 0.4f, 0.4f, 0.2f, 7); break;
            case SoundId.HitMarker:
                s.Len(0.14f); s.Tone(0, 0.09f, 1300, 1300, 0.5f, 35); s.Tone(0.01f, 0.1f, 1950, 1950, 0.35f, 30); break;
            case SoundId.Jump:
                s.Len(0.16f); s.Noise(0, 0.12f, 0.3f, 0.15f, 20, attack: 0.03f); break;
            case SoundId.Land:
                s.Len(0.14f); s.Noise(0, 0.1f, 0.5f, 0.18f, 28); s.Tone(0, 0.08f, 90, 60, 0.6f, 30); break;
            case SoundId.Footstep:
                s.Len(0.09f); s.Noise(0, 0.07f, 0.4f, 0.22f, 40); break;
            case SoundId.Respawn:
                s.Len(0.4f); s.Tone(0, 0.3f, 300, 700, 0.35f, 6); break;
            case SoundId.Resupply:
                s.Len(0.25f); s.Noise(0, 0.05f, 0.5f, 0.3f, 50); s.Tone(0.04f, 0.15f, 600, 900, 0.4f, 14); break;

            // ───────── capture the flag ─────────
            case SoundId.FlagTake:
                s.Len(0.4f); s.Tone(0, 0.12f, 660, 660, 0.5f, 12, Wave.Triangle); s.Tone(0.12f, 0.2f, 990, 990, 0.5f, 9, Wave.Triangle); break;
            case SoundId.FlagCapture:
                s.Len(0.9f);
                foreach (var (t, f) in new[] { (0f, 523f), (0.11f, 659f), (0.22f, 784f), (0.33f, 1047f) }) s.Tone(t, 0.35f, f, f, 0.45f, 6, Wave.Triangle);
                break;
            case SoundId.FlagReturn:
                s.Len(0.4f); s.Tone(0, 0.12f, 880, 880, 0.5f, 12, Wave.Triangle); s.Tone(0.12f, 0.2f, 660, 660, 0.5f, 9, Wave.Triangle); break;
            case SoundId.FlagDrop:
                s.Len(0.5f); s.Tone(0, 0.15f, 330, 330, 0.5f, 8, Wave.Triangle); s.Tone(0.15f, 0.25f, 220, 220, 0.5f, 6, Wave.Triangle); break;
            case SoundId.MatchWin:
                s.Len(1.6f);
                foreach (var (t, f) in new[] { (0f, 392f), (0.14f, 523f), (0.28f, 659f), (0.42f, 784f), (0.56f, 659f), (0.70f, 784f), (0.84f, 1047f) })
                    s.Tone(t, 0.5f, f, f, 0.4f, 4, Wave.Triangle);
                break;

            // ───────── engineer, spy, medic ─────────
            case SoundId.BuildStart:
                s.Len(0.55f); for (int i = 0; i < 6; i++) { s.Noise(i * 0.07f, 0.025f, 0.5f, 0.6f, 80); s.Tone(i * 0.07f, 0.03f, 700 + i * 60, 700 + i * 60, 0.25f, 60, Wave.Square); } break;
            case SoundId.Upgrade:
                s.Len(0.5f);
                foreach (var (t, f) in new[] { (0f, 440f), (0.1f, 554f), (0.2f, 659f) }) s.Tone(t, 0.25f, f, f, 0.4f, 8, Wave.Triangle);
                break;
            case SoundId.Teleport:
                s.Len(0.6f); s.Tone(0, 0.2f, 300, 1200, 0.5f, 5); s.Tone(0.2f, 0.25f, 1200, 300, 0.5f, 8); s.Noise(0, 0.4f, 0.2f, 0.7f, 8, hp: true); break;
            case SoundId.DispenserUse:
                s.Len(0.16f); s.Tone(0, 0.12f, 900, 1200, 0.3f, 20); break;
            case SoundId.Disguise:
                s.Len(0.3f); s.Noise(0, 0.25f, 0.4f, 0.5f, 10, attack: 0.08f); s.Tone(0, 0.25f, 300, 900, 0.2f, 8); break;
            case SoundId.Feign:
                s.Len(0.4f); s.Tone(0, 0.3f, 200, 60, 0.7f, 8); s.Noise(0, 0.1f, 0.4f, 0.2f, 25); break;
            case SoundId.Infected:
                s.Len(0.5f); s.Tone(0, 0.4f, 150, 90, 0.6f, 6, Wave.Triangle, vibrato: 18); s.Noise(0, 0.35f, 0.5f, 0.12f, 8); break;
            case SoundId.Cure:
                s.Len(0.4f); s.Tone(0, 0.12f, 880, 880, 0.4f, 10, Wave.Triangle); s.Tone(0.1f, 0.2f, 1320, 1320, 0.4f, 8, Wave.Triangle); break;
            case SoundId.Heal:
                s.Len(0.2f); s.Tone(0, 0.15f, 1040, 1040, 0.3f, 14, Wave.Triangle); break;
            default:
                throw new ArgumentOutOfRangeException(nameof(id), id, "no recipe for this sound");
        }
        return s.Finish();
    }

    /// <summary>Encodes samples as a 16-bit mono PCM WAV file in memory.</summary>
    public static byte[] ToWav(float[] samples, int sampleRate = SampleRate) => Encode(samples, sampleRate, 1f, 1f, stereo: false);

    /// <summary>Encodes a mono sound as a 16-bit stereo WAV with the given left and right gains (for pre-baked panning).</summary>
    public static byte[] ToWavStereo(float[] samples, float left, float right, int sampleRate = SampleRate) =>
        Encode(samples, sampleRate, left, right, stereo: true);

    static byte[] Encode(float[] samples, int sampleRate, float left, float right, bool stereo)
    {
        int channels = stereo ? 2 : 1;
        int dataBytes = samples.Length * 2 * channels;
        using var ms = new MemoryStream(44 + dataBytes);
        using var w = new BinaryWriter(ms);
        w.Write("RIFF"u8.ToArray());
        w.Write(36 + dataBytes);
        w.Write("WAVEfmt "u8.ToArray());
        w.Write(16);                          // fmt chunk size
        w.Write((short)1);                    // PCM
        w.Write((short)channels);
        w.Write(sampleRate);
        w.Write(sampleRate * 2 * channels);   // byte rate
        w.Write((short)(2 * channels));       // block align
        w.Write((short)16);                   // bits per sample
        w.Write("data"u8.ToArray());
        w.Write(dataBytes);
        foreach (float f in samples)
        {
            w.Write(ToPcm(f * left));
            if (stereo) w.Write(ToPcm(f * right));
        }
        return ms.ToArray();
    }

    static short ToPcm(float f) => (short)Math.Clamp((int)MathF.Round(f * 32767f), -32768, 32767);

    /// <summary>Tiny additive/subtractive sound builder with a deterministic noise source.</summary>
    sealed class Synth
    {
        float[] buf = Array.Empty<float>();
        uint state;

        public Synth(int seed) => state = (uint)seed * 2654435761u + 1u;

        public void Len(float seconds) => buf = new float[(int)(seconds * SampleRate)];

        float Rand()
        {
            state ^= state << 13; state ^= state >> 17; state ^= state << 5;
            return (state & 0xFFFFFF) / (float)0x800000 - 1f;
        }

        /// <summary>Low-passed (smoothing = coefficient 0..1, higher is brighter) or high-passed noise with an exponential decay.</summary>
        public void Noise(float start, float dur, float amp, float smoothing, float decay, float attack = 0.001f, bool hp = false)
        {
            int i0 = (int)(start * SampleRate), n = (int)(dur * SampleRate);
            float lp = 0;
            for (int i = 0; i < n && i0 + i < buf.Length; i++)
            {
                float t = i / (float)SampleRate;
                float x = Rand();
                lp += (x - lp) * smoothing;
                float v = hp ? x - lp : lp;
                float env = MathF.Min(1f, t / MathF.Max(attack, 1e-4f)) * MathF.Exp(-t * decay);
                buf[i0 + i] += v * env * amp * (hp ? 1f : 1f / MathF.Max(smoothing, 0.15f) * 0.35f);
            }
        }

        /// <summary>A tone sweeping from f0 to f1 over its duration, with an exponential decay.</summary>
        public void Tone(float start, float dur, float f0, float f1, float amp, float decay, Wave wave = Wave.Sine, float vibrato = 0f)
        {
            int i0 = (int)(start * SampleRate), n = (int)(dur * SampleRate);
            double phase = 0;
            for (int i = 0; i < n && i0 + i < buf.Length; i++)
            {
                float t = i / (float)SampleRate, k = n > 1 ? i / (float)(n - 1) : 0f;
                float f = f0 + (f1 - f0) * k;
                if (vibrato > 0) f *= 1f + 0.05f * MathF.Sin(2 * MathF.PI * vibrato * t);
                phase += 2 * Math.PI * f / SampleRate;
                float p = (float)(phase % (2 * Math.PI));
                float v = wave switch
                {
                    Wave.Square => p < MathF.PI ? 0.6f : -0.6f,
                    Wave.Triangle => 2f / MathF.PI * MathF.Asin(MathF.Sin(p)),
                    _ => MathF.Sin(p),
                };
                float env = MathF.Min(1f, t / 0.002f) * MathF.Exp(-t * decay);
                buf[i0 + i] += v * env * amp;
            }
        }

        /// <summary>Soft-clips, fades the last few ms and scales the peak to a consistent loudness.</summary>
        public float[] Finish()
        {
            float peak = 1e-6f;
            for (int i = 0; i < buf.Length; i++)
            {
                buf[i] = MathF.Tanh(buf[i] * 1.2f);
                peak = MathF.Max(peak, MathF.Abs(buf[i]));
            }
            int fade = Math.Min(buf.Length, (int)(0.01f * SampleRate));
            float gain = 0.9f / peak;
            for (int i = 0; i < buf.Length; i++)
            {
                float f = i >= buf.Length - fade ? (buf.Length - i) / (float)fade : 1f;
                buf[i] *= gain * f;
            }
            return buf;
        }
    }
}
