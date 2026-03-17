using XmegaAudio.Audio;

namespace XmegaAudio.Settings;

public sealed class AppSettings
{
    public string? CaptureDeviceId { get; set; }
    public string? RenderDeviceId { get; set; }
    public ProcessingSettings Processing { get; set; } = new();
    public bool GlobalHotkeysEnabled { get; set; } = true;
    public Dictionary<string, string> Hotkeys { get; set; } = new()
    {
        ["MuteToggle"] = "F12",
        ["PlayLaugh"] = "F11",
        ["StopSfx"] = "F10",
        ["ToggleNoiseSuppression"] = "F8",
        ["ToggleAutoGain"] = "F7",
        ["ToggleReverb"] = "F9"
    };

    public string? LaughFilePath { get; set; }
}
