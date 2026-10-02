using System.Numerics;
using Raylib_cs;
using TFClassic.Core;

namespace TFClassic.Game;

/// <summary>
/// Plays the game's synthesized sounds through raylib. Every sound is pre-rendered at several stereo pan
/// positions (so panning doesn't depend on any audio-library quirks) and played through a small pool of
/// aliases so rapid fire can overlap. If there is no audio device everything quietly does nothing.
/// </summary>
sealed class SoundPlayer : IDisposable
{
    const int AliasesPerVoice = 4;
    const int MaxPlaysPerFrame = 24;

    readonly Sound[,] sources = new Sound[Enum.GetValues<SoundId>().Length, SoundMix.PanLevels];
    readonly List<Sound>?[,] aliases = new List<Sound>?[Enum.GetValues<SoundId>().Length, SoundMix.PanLevels];
    readonly int[,] nextAlias = new int[Enum.GetValues<SoundId>().Length, SoundMix.PanLevels];
    readonly Random jitter = new(7);
    int playsThisFrame;

    public bool Ready { get; }
    public bool Muted { get; set; }
    public float Volume { get; set; } = 0.55f;
    public int PlayedCount { get; private set; }

    public SoundPlayer()
    {
        Raylib.InitAudioDevice();
        Ready = Raylib.IsAudioDeviceReady();
        if (!Ready) return;

        foreach (var id in Enum.GetValues<SoundId>())
        {
            var mono = SoundSynth.Generate(id);
            for (int pan = 0; pan < SoundMix.PanLevels; pan++)
            {
                var (left, right) = SoundMix.Gains(pan);
                var wave = Raylib.LoadWaveFromMemory(".wav", SoundSynth.ToWavStereo(mono, left, right));
                sources[(int)id, pan] = Raylib.LoadSoundFromWave(wave);
                Raylib.UnloadWave(wave);
            }
        }
    }

    public void BeginFrame() => playsThisFrame = 0;

    /// <summary>Plays one world sound as heard by <paramref name="listener"/>.</summary>
    public void Play(in SoundEvent e, Player listener)
    {
        if (!Ready || Muted || playsThisFrame >= MaxPlaysPerFrame) return;

        var mix = SoundMix.Compute(e, listener.Eye, listener.Right, listener.Id);
        float volume = mix.Volume * Volume;
        if (volume < 0.02f) return;

        var sound = NextVoice((int)e.Id, mix.PanIndex);
        Raylib.SetSoundVolume(sound, Math.Min(volume, 1f));
        Raylib.SetSoundPitch(sound, 0.94f + (float)jitter.NextDouble() * 0.12f);   // a little variation so repeats aren't robotic
        Raylib.PlaySound(sound);
        PlayedCount++;
        playsThisFrame++;
    }

    Sound NextVoice(int id, int pan)
    {
        var pool = aliases[id, pan] ??= new List<Sound>();
        if (pool.Count < AliasesPerVoice)
        {
            var alias = Raylib.LoadSoundAlias(sources[id, pan]);
            pool.Add(alias);
            return alias;
        }
        int i = nextAlias[id, pan]++ % pool.Count;
        return pool[i];
    }

    public void Dispose()
    {
        if (!Ready) return;
        for (int id = 0; id < sources.GetLength(0); id++)
            for (int pan = 0; pan < SoundMix.PanLevels; pan++)
            {
                if (aliases[id, pan] != null)
                    foreach (var a in aliases[id, pan]!) Raylib.UnloadSoundAlias(a);
                Raylib.UnloadSound(sources[id, pan]);
            }
        Raylib.CloseAudioDevice();
    }
}
