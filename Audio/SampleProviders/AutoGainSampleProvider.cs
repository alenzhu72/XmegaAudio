using NAudio.Wave;

namespace XmegaAudio.Audio.SampleProviders;

public sealed class AutoGainSampleProvider : ISampleProvider
{
    private readonly ISampleProvider source;
    private readonly int channels;

    private float targetRms = DbToLinear(-18f);
    private float maxGain = DbToLinear(18f);
    private float limiterCeiling = DbToLinear(-1f);
    private float attackCoeff;
    private float releaseCoeff;

    private float currentGain = 1f;
    private float rmsAccumulator;
    private int rmsSamples;
    private readonly int rmsWindowSamples;

    public AutoGainSampleProvider(ISampleProvider source)
    {
        this.source = source;
        channels = source.WaveFormat.Channels;
        rmsWindowSamples = Math.Max(256, (int)(source.WaveFormat.SampleRate * 0.02f) * channels);
        SetAttackRelease(15f, 150f);
    }

    public WaveFormat WaveFormat => source.WaveFormat;

    public void SetParameters(float targetRmsDb, float maxGainDb, float attackMs, float releaseMs, float limiterCeilingDb)
    {
        targetRms = DbToLinear(targetRmsDb);
        maxGain = DbToLinear(maxGainDb);
        limiterCeiling = DbToLinear(limiterCeilingDb);
        SetAttackRelease(attackMs, releaseMs);
    }

    public int Read(float[] buffer, int offset, int count)
    {
        int read = source.Read(buffer, offset, count);
        if (read <= 0)
        {
            return read;
        }

        for (int i = 0; i < read; i++)
        {
            float s = buffer[offset + i];
            rmsAccumulator += s * s;
            rmsSamples++;

            if (rmsSamples >= rmsWindowSamples)
            {
                float rms = MathF.Sqrt(rmsAccumulator / rmsSamples);
                rmsAccumulator = 0f;
                rmsSamples = 0;

                float desired = rms > 1e-6f ? targetRms / rms : maxGain;
                desired = MathF.Min(maxGain, MathF.Max(1f, desired));

                currentGain = desired > currentGain
                    ? desired + attackCoeff * (currentGain - desired)
                    : desired + releaseCoeff * (currentGain - desired);
            }
        }

        float ceiling = limiterCeiling;
        for (int i = 0; i < read; i++)
        {
            float s = buffer[offset + i] * currentGain;
            float abs = MathF.Abs(s);
            if (abs > ceiling)
            {
                s = MathF.Sign(s) * ceiling;
            }

            buffer[offset + i] = s;
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

