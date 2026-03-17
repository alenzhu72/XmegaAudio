using NAudio.Wave;

namespace XmegaAudio.Audio.SampleProviders;

public sealed class AdaptiveNoiseSuppressorSampleProvider : ISampleProvider
{
    private readonly ISampleProvider source;
    private readonly int channels;
    private readonly float sampleRate;

    private float strength = 0.6f;
    private float noiseFloor;
    private float env;
    private float fastCoeff;
    private float slowCoeff;
    private const float Eps = 1e-7f;

    public AdaptiveNoiseSuppressorSampleProvider(ISampleProvider source)
    {
        this.source = source;
        channels = source.WaveFormat.Channels;
        sampleRate = source.WaveFormat.SampleRate;
        SetCoeffs(attackMs: 10f, releaseMs: 250f);
    }

    public WaveFormat WaveFormat => source.WaveFormat;

    public void SetStrength(float s01)
    {
        strength = s01 < 0f ? 0f : (s01 > 1f ? 1f : s01);
    }

    public int Read(float[] buffer, int offset, int count)
    {
        int read = source.Read(buffer, offset, count);
        if (read <= 0)
        {
            return read;
        }

        for (int n = 0; n < read; n += channels)
        {
            float peak = 0f;
            for (int ch = 0; ch < channels; ch++)
            {
                float abs = MathF.Abs(buffer[offset + n + ch]);
                if (abs > peak) peak = abs;
            }

            env = peak > env
                ? peak + fastCoeff * (env - peak)
                : peak + slowCoeff * (env - peak);

            float observed = peak;
            float targetNoise = MathF.Min(env, MathF.Max(noiseFloor, observed));
            noiseFloor = targetNoise + slowCoeff * (noiseFloor - targetNoise);

            float baseRatio = noiseFloor / (observed + noiseFloor + Eps);
            float p = 1.5f;
            float attenuation = strength * MathF.Pow(baseRatio, p);
            float gain = 1f - attenuation;

            for (int ch = 0; ch < channels; ch++)
            {
                buffer[offset + n + ch] *= gain;
            }
        }

        return read;
    }

    private void SetCoeffs(float attackMs, float releaseMs)
    {
        fastCoeff = TimeToCoeff(attackMs, sampleRate);
        slowCoeff = TimeToCoeff(releaseMs, sampleRate);
    }

    private static float TimeToCoeff(float ms, float sampleRate)
    {
        float t = MathF.Max(0.1f, ms) / 1000f;
        return MathF.Exp(-1f / (t * sampleRate));
    }
}
