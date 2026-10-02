using System.Numerics;
using TFClassic.Core;
using Xunit;

namespace TFClassic.Tests;

public class SoundSynthTests
{
    public static IEnumerable<object[]> AllSounds() => Enum.GetValues<SoundId>().Select(id => new object[] { id });

    static float Rms(float[] s) => MathF.Sqrt(s.Sum(x => x * x) / s.Length);

    /// <summary>Share of a sound's energy below ~300 Hz (one-pole low-pass): high for booms, low for hisses and clicks.</summary>
    static float BassShare(float[] s)
    {
        float a = 2 * MathF.PI * 300f / SoundSynth.SampleRate, y = 0, low = 0, total = 0;
        foreach (float x in s)
        {
            y += (x - y) * a;
            low += y * y;
            total += x * x;
        }
        return low / total;
    }

    [Theory]
    [MemberData(nameof(AllSounds))]
    public void EverySoundIsFiniteAudibleAndShort(SoundId id)
    {
        var s = SoundSynth.Generate(id);
        Assert.NotEmpty(s);
        Assert.InRange(s.Length / (float)SoundSynth.SampleRate, 0.05f, 2.0f);
        Assert.All(s, x => Assert.True(float.IsFinite(x)));
        float peak = s.Max(MathF.Abs);
        Assert.InRange(peak, 0.6f, 1.0f);                       // normalised loud enough, never clipping
        Assert.True(Rms(s) > 0.03f, $"{id} is nearly silent (rms {Rms(s)})");
        Assert.True(MathF.Abs(s[^1]) < 0.05f, "ends faded out so it doesn't click");
    }

    [Fact]
    public void GenerationIsDeterministicAndEverySoundIsDistinct()
    {
        var seen = new Dictionary<string, SoundId>();
        foreach (var id in Enum.GetValues<SoundId>())
        {
            var a = SoundSynth.Generate(id);
            var b = SoundSynth.Generate(id);
            Assert.Equal(a, b);
            string key = a.Length + ":" + string.Join(",", a.Skip(a.Length / 3).Take(48).Select(x => x.ToString("F3")));
            Assert.False(seen.TryGetValue(key, out var other), $"{id} sounds identical to {other}");
            seen[key] = id;
        }
    }

    [Fact]
    public void SoundsHaveTheRightCharacter()
    {
        float Bass(SoundId id) => BassShare(SoundSynth.Generate(id));

        // Booms are bass-heavy; clicks, hisses and chimes are not.
        foreach (var id in new[] { SoundId.Explosion, SoundId.DetpackBlast, SoundId.ConcussionBlast, SoundId.SniperShot, SoundId.GrenadeLauncher })
            Assert.True(Bass(id) > 0.4f, $"{id} should be bass-heavy ({Bass(id)})");
        foreach (var id in new[] { SoundId.HitMarker, SoundId.Nailgun, SoundId.GasHiss, SoundId.EmpZap, SoundId.NailSpray })
            Assert.True(Bass(id) < 0.15f, $"{id} should not be bass-heavy ({Bass(id)})");

        // Big things are longer than small things.
        Assert.True(SoundSynth.Generate(SoundId.DetpackBlast).Length > SoundSynth.Generate(SoundId.Explosion).Length);
        Assert.True(SoundSynth.Generate(SoundId.Explosion).Length > SoundSynth.Generate(SoundId.Shotgun).Length);
        Assert.True(SoundSynth.Generate(SoundId.Shotgun).Length > SoundSynth.Generate(SoundId.Nailgun).Length);
    }

    [Fact]
    public void WavEncodingIsAValidPcmFile()
    {
        var samples = SoundSynth.Generate(SoundId.Shotgun);
        var wav = SoundSynth.ToWav(samples);
        Assert.Equal(44 + samples.Length * 2, wav.Length);
        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(wav, 0, 4));
        Assert.Equal("WAVE", System.Text.Encoding.ASCII.GetString(wav, 8, 4));
        Assert.Equal("fmt ", System.Text.Encoding.ASCII.GetString(wav, 12, 4));
        Assert.Equal(1, BitConverter.ToInt16(wav, 20));                     // PCM
        Assert.Equal(1, BitConverter.ToInt16(wav, 22));                     // mono
        Assert.Equal(SoundSynth.SampleRate, BitConverter.ToInt32(wav, 24));
        Assert.Equal(16, BitConverter.ToInt16(wav, 34));
        Assert.Equal("data", System.Text.Encoding.ASCII.GetString(wav, 36, 4));
        Assert.Equal(samples.Length * 2, BitConverter.ToInt32(wav, 40));
        Assert.Equal(wav.Length - 8, BitConverter.ToInt32(wav, 4));
    }

    [Fact]
    public void StereoWavAppliesPerChannelGains()
    {
        var samples = SoundSynth.Generate(SoundId.Shotgun);
        var wav = SoundSynth.ToWavStereo(samples, 1f, 0.25f);
        Assert.Equal(44 + samples.Length * 4, wav.Length);
        Assert.Equal(2, BitConverter.ToInt16(wav, 22));                     // two channels
        Assert.Equal(4, BitConverter.ToInt16(wav, 32));                     // block align
        Assert.Equal(samples.Length * 4, BitConverter.ToInt32(wav, 40));

        int loudest = Enumerable.Range(0, samples.Length).MaxBy(i => MathF.Abs(samples[i]));
        short l = BitConverter.ToInt16(wav, 44 + loudest * 4), r = BitConverter.ToInt16(wav, 44 + loudest * 4 + 2);
        Assert.InRange(MathF.Abs(r) / MathF.Abs(l), 0.24f, 0.26f);
    }
}

public class SoundMixTests
{
    // A listener at the origin looking down +Z; "right" for that view is -X (see Player.Right).
    static readonly Vector3 Right = new(-1, 0, 0);

    static Mix MixOf(SoundId id, Vector3 at, float range = 1000f, float volume = 1f, int source = 0, int listener = 1) =>
        SoundMix.Compute(new SoundEvent(id, at, volume, range, source), Vector3.Zero, Right, listener);

    [Fact]
    public void LoudnessFallsWithDistanceAndHitsZeroAtTheRange()
    {
        float near = MixOf(SoundId.Explosion, new Vector3(0, 0, 100)).Volume;
        float mid = MixOf(SoundId.Explosion, new Vector3(0, 0, 500)).Volume;
        float far = MixOf(SoundId.Explosion, new Vector3(0, 0, 900)).Volume;
        Assert.True(near > mid && mid > far && far > 0f);
        Assert.Equal(0f, MixOf(SoundId.Explosion, new Vector3(0, 0, 1000)).Volume);
        Assert.Equal(0f, MixOf(SoundId.Explosion, new Vector3(0, 0, 5000)).Volume);
        Assert.InRange(MixOf(SoundId.Explosion, new Vector3(0, 0, 50), volume: 0.5f).Volume, 0.4f, 0.5f);
    }

    [Fact]
    public void PanFollowsWhichSideTheSoundIsOn()
    {
        Assert.Equal(SoundMix.Centre, MixOf(SoundId.Shotgun, new Vector3(0, 0, 400)).PanIndex);    // dead ahead
        Assert.Equal(SoundMix.Centre, MixOf(SoundId.Shotgun, new Vector3(0, 0, -400)).PanIndex);   // dead behind
        Assert.Equal(SoundMix.PanLevels - 1, MixOf(SoundId.Shotgun, new Vector3(-400, 0, 0)).PanIndex);   // to the right
        Assert.Equal(0, MixOf(SoundId.Shotgun, new Vector3(400, 0, 0)).PanIndex);                  // to the left
        Assert.True(MixOf(SoundId.Shotgun, new Vector3(-300, 0, 300)).PanIndex > SoundMix.Centre); // front-right
        Assert.True(MixOf(SoundId.Shotgun, new Vector3(300, 0, 300)).PanIndex < SoundMix.Centre);  // front-left
    }

    [Fact]
    public void OwnAndPointBlankSoundsAreCentredAndGlobalOnesAreNotAttenuated()
    {
        Assert.Equal(SoundMix.Centre, MixOf(SoundId.Shotgun, new Vector3(-300, 0, 0), source: 1).PanIndex);   // I fired it
        Assert.Equal(SoundMix.Centre, MixOf(SoundId.Footstep, new Vector3(-20, 0, 0)).PanIndex);

        var flag = MixOf(SoundId.FlagCapture, new Vector3(0, 0, 90000), range: 99999f, volume: 1f);
        Assert.InRange(flag.Volume, 0.7f, 0.9f);
        Assert.Equal(SoundMix.Centre, flag.PanIndex);
    }

    [Fact]
    public void HitMarkersAreOnlyForTheShooter()
    {
        Assert.True(MixOf(SoundId.HitMarker, Vector3.Zero, range: 100f, volume: 0.6f, source: 1, listener: 1).Volume > 0.5f);
        Assert.Equal(0f, MixOf(SoundId.HitMarker, Vector3.Zero, range: 100f, volume: 0.6f, source: 2, listener: 1).Volume);
    }

    [Fact]
    public void PanGainsFavourTheNearEar()
    {
        Assert.Equal((1f, 1f), SoundMix.Gains(SoundMix.Centre));
        var left = SoundMix.Gains(0);
        var right = SoundMix.Gains(SoundMix.PanLevels - 1);
        Assert.True(left.Left > left.Right * 5f);
        Assert.True(right.Right > right.Left * 5f);
        Assert.InRange(left.Right, 0.1f, 0.2f);
        Assert.True(SoundMix.Gains(1).Right < 1f && SoundMix.Gains(1).Right > SoundMix.Gains(0).Right);
    }
}
