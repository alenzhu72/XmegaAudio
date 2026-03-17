using System.IO;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace XmegaAudio.Audio;

public sealed class SoundEffectsPlayer : IDisposable
{
    private readonly object gate = new();
    private string? renderDeviceId;
    private WasapiOut? outDevice;
    private AudioFileReader? reader;

    public void SetRenderDevice(string? deviceId)
    {
        lock (gate)
        {
            renderDeviceId = deviceId;
        }
    }

    public void Play(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return;
        }

        lock (gate)
        {
            Stop_NoLock();

            if (string.IsNullOrWhiteSpace(renderDeviceId))
            {
                return;
            }

            using var enumerator = new MMDeviceEnumerator();
            var renderDevice = enumerator.GetDevice(renderDeviceId);
            var mixFormat = renderDevice.AudioClient.MixFormat;

            reader = new AudioFileReader(filePath);
            ISampleProvider src = reader;
            src = EnsureChannels(src, mixFormat.Channels);
            src = EnsureSampleRate(src, mixFormat.SampleRate);

            IWaveProvider waveProvider = new SampleToWaveProvider(src);

            outDevice = new WasapiOut(renderDevice, AudioClientShareMode.Shared, true, 60);
            outDevice.Init(waveProvider);
            outDevice.PlaybackStopped += OnPlaybackStopped;
            outDevice.Play();
        }
    }

    public void Stop()
    {
        lock (gate)
        {
            Stop_NoLock();
        }
    }

    private void Stop_NoLock()
    {
        if (outDevice is not null)
        {
            outDevice.PlaybackStopped -= OnPlaybackStopped;
            try
            {
                outDevice.Stop();
            }
            catch
            {
            }

            outDevice.Dispose();
            outDevice = null;
        }

        if (reader is not null)
        {
            reader.Dispose();
            reader = null;
        }
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        lock (gate)
        {
            Stop_NoLock();
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
