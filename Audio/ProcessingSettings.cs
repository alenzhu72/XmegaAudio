namespace XmegaAudio.Audio;

public sealed class ProcessingSettings
{
    public bool NoiseSuppressionEnabled { get; set; } = true;
    public float NoiseGateThresholdDb { get; set; } = -35f;
    public float NoiseReductionDb { get; set; } = -24f;
    public float NoiseGateAttackMs { get; set; } = 5f;
    public float NoiseGateReleaseMs { get; set; } = 120f;
    public float NoiseSuppressStrength01 { get; set; } = 0.6f;

    public bool AutoGainEnabled { get; set; } = true;
    public float AutoGainTargetRmsDb { get; set; } = -18f;
    public float AutoGainMaxGainDb { get; set; } = 18f;
    public float AutoGainAttackMs { get; set; } = 15f;
    public float AutoGainReleaseMs { get; set; } = 150f;
    public float LimiterCeilingDb { get; set; } = -1f;

    public bool ReverbEnabled { get; set; } = false;
    public float ReverbRoomSize { get; set; } = 0.45f;
    public float ReverbDamping { get; set; } = 0.35f;
    public float ReverbWet { get; set; } = 0.18f;

    public bool EqEnabled { get; set; } = true;
    public float EqMaxGainDb { get; set; } = 12f;
    public float[] EqBandGainsDb { get; set; } = new float[10];
}
