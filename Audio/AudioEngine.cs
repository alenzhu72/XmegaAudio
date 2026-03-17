using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace XmegaAudio.Audio;

public sealed class AudioEngine : IDisposable
{
    private WasapiCapture? capture;
    private WasapiOut? output;
    private BufferedWaveProvider? captureBuffer;
    private MicrophoneProcessor? processor;
    private VolumeSampleProvider? volume;
    private readonly object gate = new();

    public ProcessingSettings Settings { get; } = new();

    public bool IsRunning { get; private set; }
    public bool IsMuted { get; private set; }

    public IReadOnlyList<AudioDeviceInfo> GetCaptureDevices()
    {
        using var enumerator = new MMDeviceEnumerator();
        return enumerator
            .EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
            .Select(d => new AudioDeviceInfo(d.ID, d.FriendlyName))
            .ToList();
    }

    public IReadOnlyList<AudioDeviceInfo> GetRenderDevices()
    {
        using var enumerator = new MMDeviceEnumerator();
        return enumerator
            .EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
            .Select(d => new AudioDeviceInfo(d.ID, d.FriendlyName))
            .ToList();
    }

    public void Start(string captureDeviceId, string renderDeviceId)
    {
        lock (gate)
        {
            Stop_NoLock();

            using var enumerator = new MMDeviceEnumerator();
            var captureDevice = enumerator.GetDevice(captureDeviceId);
            var renderDevice = enumerator.GetDevice(renderDeviceId);

            capture = new WasapiCapture(captureDevice)
            {
                ShareMode = AudioClientShareMode.Shared
            };

            captureBuffer = new BufferedWaveProvider(capture.WaveFormat)
            {
                DiscardOnBufferOverflow = true,
                BufferDuration = TimeSpan.FromSeconds(2)
            };

            capture.DataAvailable += OnCaptureDataAvailable;
            capture.RecordingStopped += OnCaptureStopped;

            ISampleProvider source = captureBuffer.ToSampleProvider();
            processor = new MicrophoneProcessor(source);
            processor.ApplySettings(Settings);

            var mixFormat = renderDevice.AudioClient.MixFormat;
            ISampleProvider processed = processor.Output;
            processed = EnsureChannels(processed, mixFormat.Channels);
            processed = EnsureSampleRate(processed, mixFormat.SampleRate);

            volume = new VolumeSampleProvider(processed)
            {
                Volume = IsMuted ? 0f : 1f
            };

            IWaveProvider waveProvider = new SampleToWaveProvider(volume);

            output = new WasapiOut(renderDevice, AudioClientShareMode.Shared, true, 60);
            output.Init(waveProvider);
            output.Play();
            capture.StartRecording();

            IsRunning = true;
        }
    }

    public void Stop()
    {
        lock (gate)
        {
            Stop_NoLock();
        }
    }

    public void ApplySettings()
    {
        lock (gate)
        {
            processor?.ApplySettings(Settings);
        }
    }

    public void SetMuted(bool muted)
    {
        lock (gate)
        {
            IsMuted = muted;
            if (volume is not null)
            {
                volume.Volume = muted ? 0f : 1f;
            }
        }
    }

    public void ToggleMuted()
    {
        SetMuted(!IsMuted);
    }

    private void Stop_NoLock()
    {
        IsRunning = false;

        if (capture is not null)
        {
            capture.DataAvailable -= OnCaptureDataAvailable;
            capture.RecordingStopped -= OnCaptureStopped;

            try
            {
                capture.StopRecording();
            }
            catch
            {
            }

            capture.Dispose();
            capture = null;
        }

        if (output is not null)
        {
            try
            {
                output.Stop();
            }
            catch
            {
            }

            output.Dispose();
            output = null;
        }

        captureBuffer = null;
        processor = null;
        volume = null;
    }

    private void OnCaptureDataAvailable(object? sender, WaveInEventArgs e)
    {
        lock (gate)
        {
            captureBuffer?.AddSamples(e.Buffer, 0, e.BytesRecorded);
        }
    }

    private void OnCaptureStopped(object? sender, StoppedEventArgs e)
    {
        lock (gate)
        {
            IsRunning = false;
        }
    }

    private static ISampleProvider EnsureChannels(ISampleProvider source, int desiredChannels)
    {
        if (source.WaveFormat.Channels == desiredChannels)
        {
            return source;
        }

        if (source.WaveFormat.Channels == 1 && desiredChannels == 2)
        {
            return new MonoToStereoSampleProvider(source);
        }

        if (source.WaveFormat.Channels == 2 && desiredChannels == 1)
        {
            return new StereoToMonoSampleProvider(source);
        }

        throw new NotSupportedException($"不支持从 {source.WaveFormat.Channels} 声道转换到 {desiredChannels} 声道。");
    }

    private static ISampleProvider EnsureSampleRate(ISampleProvider source, int desiredSampleRate)
    {
        if (source.WaveFormat.SampleRate == desiredSampleRate)
        {
            return source;
        }

        return new WdlResamplingSampleProvider(source, desiredSampleRate);
    }

    public void Dispose()
    {
        Stop();
    }
}
