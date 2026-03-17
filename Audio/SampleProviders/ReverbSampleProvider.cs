using NAudio.Wave;

namespace XmegaAudio.Audio.SampleProviders;

public sealed class ReverbSampleProvider : ISampleProvider
{
    private readonly ISampleProvider source;
    private readonly int channels;
    private readonly ReverbChannel[] channelReverbs;

    private float roomSize = 0.45f;
    private float damping = 0.35f;
    private float wet = 0.18f;

    public ReverbSampleProvider(ISampleProvider source)
    {
        this.source = source;
        channels = source.WaveFormat.Channels;
        channelReverbs = new ReverbChannel[channels];
        for (int ch = 0; ch < channels; ch++)
        {
            channelReverbs[ch] = new ReverbChannel(source.WaveFormat.SampleRate);
        }

        ApplyParams();
    }

    public WaveFormat WaveFormat => source.WaveFormat;

    public void SetParameters(float roomSize01, float damping01, float wet01)
    {
        roomSize = Clamp01(roomSize01);
        damping = Clamp01(damping01);
        wet = Clamp01(wet01);
        ApplyParams();
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
            for (int ch = 0; ch < channels; ch++)
            {
                float dry = buffer[offset + n + ch];
                float rvb = channelReverbs[ch].Process(dry);
                buffer[offset + n + ch] = dry * (1f - wet) + rvb * wet;
            }
        }

        return read;
    }

    private void ApplyParams()
    {
        for (int ch = 0; ch < channels; ch++)
        {
            channelReverbs[ch].SetParameters(roomSize, damping);
        }
    }

    private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

    private sealed class ReverbChannel
    {
        private readonly Comb[] combs;
        private readonly AllPass[] allpasses;

        public ReverbChannel(int sampleRate)
        {
            int scale = Math.Max(1, sampleRate / 44100);
            combs =
            [
                new Comb(1116 * scale),
                new Comb(1188 * scale),
                new Comb(1277 * scale),
                new Comb(1356 * scale),
                new Comb(1422 * scale),
                new Comb(1491 * scale),
                new Comb(1557 * scale),
                new Comb(1617 * scale)
            ];

            allpasses =
            [
                new AllPass(556 * scale),
                new AllPass(441 * scale),
                new AllPass(341 * scale),
                new AllPass(225 * scale)
            ];
        }

        public void SetParameters(float roomSize01, float damping01)
        {
            float feedback = 0.28f + roomSize01 * 0.62f;
            float damp = damping01 * 0.4f;
            for (int i = 0; i < combs.Length; i++)
            {
                combs[i].Feedback = feedback;
                combs[i].Damp = damp;
            }

            for (int i = 0; i < allpasses.Length; i++)
            {
                allpasses[i].Feedback = 0.5f;
            }
        }

        public float Process(float input)
        {
            float sum = 0f;
            for (int i = 0; i < combs.Length; i++)
            {
                sum += combs[i].Process(input);
            }

            float outSample = sum * 0.125f;
            for (int i = 0; i < allpasses.Length; i++)
            {
                outSample = allpasses[i].Process(outSample);
            }

            return outSample;
        }

        private sealed class Comb
        {
            private readonly float[] buffer;
            private int idx;
            private float filterStore;

            public Comb(int size)
            {
                buffer = new float[Math.Max(1, size)];
            }

            public float Feedback { get; set; }
            public float Damp { get; set; }

            public float Process(float input)
            {
                float output = buffer[idx];
                filterStore = output * (1f - Damp) + filterStore * Damp;
                buffer[idx] = input + filterStore * Feedback;
                if (++idx >= buffer.Length)
                {
                    idx = 0;
                }

                return output;
            }
        }

        private sealed class AllPass
        {
            private readonly float[] buffer;
            private int idx;

            public AllPass(int size)
            {
                buffer = new float[Math.Max(1, size)];
            }

            public float Feedback { get; set; } = 0.5f;

            public float Process(float input)
            {
                float bufOut = buffer[idx];
                float output = -input + bufOut;
                buffer[idx] = input + bufOut * Feedback;
                if (++idx >= buffer.Length)
                {
                    idx = 0;
                }

                return output;
            }
        }
    }
}

