using NAudio.Wave;

namespace XmegaAudio.Audio.SampleProviders;

public sealed class NoiseGateSampleProvider : ISampleProvider
{
    private readonly ISampleProvider source;
    private readonly int channels;

    private float thresholdLinear = DbToLinear(-45f);
    private float reductionLinear = DbToLinear(-18f);
    private float attackCoeff;
    private float releaseCoeff;
    private float envelope;
    private float gain = 1f;

    public NoiseGateSampleProvider(ISampleProvider source)
    {
        this.source = source;
        channels = source.WaveFormat.Channels;
        SetAttackRelease(5f, 120f);
    }

    public WaveFormat WaveFormat => source.WaveFormat;

    public void SetParameters(float thresholdDb, float reductionDb, float attackMs, float releaseMs)
    {
        thresholdLinear = DbToLinear(thresholdDb);
        reductionLinear = DbToLinear(reductionDb);
        SetAttackRelease(attackMs, releaseMs);
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
                if (abs > peak)
                {
                    peak = abs;
                }
            }

            envelope = peak > envelope
                ? peak + attackCoeff * (envelope - peak)
                : peak + releaseCoeff * (envelope - peak);

            float targetGain = envelope < thresholdLinear ? reductionLinear : 1f;
            gain = targetGain > gain
                ? targetGain + attackCoeff * (gain - targetGain)
                : targetGain + releaseCoeff * (gain - targetGain);

            for (int ch = 0; ch < channels; ch++)
            {
                buffer[offset + n + ch] *= gain;
            }
        }

        return read;
    }

    private void SetAttackRelease(float attackMs, float releaseMs)
    {
        float sampleRate = source.WaveFormat.SampleRate;
        attackCoeff = TimeToCoeff(attackMs, sampleRate);
        releaseCoeff = TimeToCoeff(releaseMs, sampleRate);
    }

    private static float TimeToCoeff(float ms, float sampleRate)
    {
        float t = MathF.Max(0.1f, ms) / 1000f;
        return MathF.Exp(-1f / (t * sampleRate));
    }

    private static float DbToLinear(float db) => MathF.Pow(10f, db / 20f);
}

