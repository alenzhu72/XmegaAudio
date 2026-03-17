using NAudio.Dsp;
using NAudio.Wave;

namespace XmegaAudio.Audio.SampleProviders;

public sealed class EqualizerSampleProvider : ISampleProvider
{
    private static readonly float[] BandFrequenciesHz = new[]
    {
        31.25f, 62.5f, 125f, 250f, 500f, 1000f, 2000f, 4000f, 8000f, 16000f
    };

    private readonly ISampleProvider source;
    private readonly int channels;
    private readonly float sampleRate;
    private readonly BiQuadFilter[][] filters;

    private readonly float[] gainsDb;
    private float q = 1.0f;
    private bool dirty = true;

    public EqualizerSampleProvider(ISampleProvider source)
    {
        this.source = source;
        channels = source.WaveFormat.Channels;
        sampleRate = source.WaveFormat.SampleRate;
        gainsDb = new float[BandFrequenciesHz.Length];
        filters = new BiQuadFilter[channels][];
        for (int ch = 0; ch < channels; ch++)
        {
            filters[ch] = new BiQuadFilter[BandFrequenciesHz.Length];
            for (int i = 0; i < BandFrequenciesHz.Length; i++)
            {
                filters[ch][i] = BiQuadFilter.PeakingEQ(sampleRate, BandFrequenciesHz[i], q, 0f);
            }
        }
    }

    public WaveFormat WaveFormat => source.WaveFormat;

    public void SetParameters(float qValue, float[] bandGainsDb)
    {
        q = MathF.Max(0.1f, qValue);
        int len = Math.Min(gainsDb.Length, bandGainsDb.Length);
        for (int i = 0; i < len; i++)
        {
            gainsDb[i] = bandGainsDb[i];
        }

        dirty = true;
    }

    public int Read(float[] buffer, int offset, int count)
    {
        int read = source.Read(buffer, offset, count);
        if (read <= 0)
        {
            return read;
        }

        if (dirty)
        {
            RebuildFilters();
            dirty = false;
        }

        for (int n = 0; n < read; n += channels)
        {
            for (int ch = 0; ch < channels; ch++)
            {
                float s = buffer[offset + n + ch];
                for (int i = 0; i < BandFrequenciesHz.Length; i++)
                {
                    s = filters[ch][i].Transform(s);
                }

                buffer[offset + n + ch] = s;
            }
        }

        return read;
    }

    private void RebuildFilters()
    {
        for (int ch = 0; ch < channels; ch++)
        {
            for (int i = 0; i < BandFrequenciesHz.Length; i++)
            {
                filters[ch][i] = BiQuadFilter.PeakingEQ(sampleRate, BandFrequenciesHz[i], q, gainsDb[i]);
            }
        }
    }
}
