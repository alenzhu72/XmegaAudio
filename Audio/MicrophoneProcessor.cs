using NAudio.Wave;
using XmegaAudio.Audio.SampleProviders;

namespace XmegaAudio.Audio;

public sealed class MicrophoneProcessor
{
    private readonly AdaptiveNoiseSuppressorSampleProvider suppressor;
    private readonly NoiseGateSampleProvider noiseGate;
    private readonly AutoGainSampleProvider autoGain;
    private readonly ReverbSampleProvider reverb;
    private readonly EqualizerSampleProvider eq;

    private readonly float[] eqGainsBuffer = new float[10];

    public MicrophoneProcessor(ISampleProvider source)
    {
        suppressor = new AdaptiveNoiseSuppressorSampleProvider(source);
        noiseGate = new NoiseGateSampleProvider(suppressor);
        autoGain = new AutoGainSampleProvider(noiseGate);
        reverb = new ReverbSampleProvider(autoGain);
        eq = new EqualizerSampleProvider(reverb);

        Output = eq;
    }

    public ISampleProvider Output { get; }

    public void ApplySettings(ProcessingSettings s)
    {
        suppressor.SetStrength(s.NoiseSuppressStrength01);
        if (s.NoiseSuppressionEnabled)
        {
            noiseGate.SetParameters(s.NoiseGateThresholdDb, s.NoiseReductionDb, s.NoiseGateAttackMs, s.NoiseGateReleaseMs);
        }
        else
        {
            noiseGate.SetParameters(-120f, 0f, s.NoiseGateAttackMs, s.NoiseGateReleaseMs);
        }

        if (s.AutoGainEnabled)
        {
            autoGain.SetParameters(s.AutoGainTargetRmsDb, s.AutoGainMaxGainDb, s.AutoGainAttackMs, s.AutoGainReleaseMs, s.LimiterCeilingDb);
        }
        else
        {
            autoGain.SetParameters(s.AutoGainTargetRmsDb, 0f, s.AutoGainAttackMs, s.AutoGainReleaseMs, 0f);
        }

        if (s.ReverbEnabled)
        {
            reverb.SetParameters(s.ReverbRoomSize, s.ReverbDamping, s.ReverbWet);
        }
        else
        {
            reverb.SetParameters(s.ReverbRoomSize, s.ReverbDamping, 0f);
        }

        if (s.EqEnabled)
        {
            int len = Math.Min(eqGainsBuffer.Length, s.EqBandGainsDb.Length);
            Array.Copy(s.EqBandGainsDb, eqGainsBuffer, len);
        }
        else
        {
            Array.Clear(eqGainsBuffer);
        }

        eq.SetParameters(1.0f, eqGainsBuffer);
    }
}
