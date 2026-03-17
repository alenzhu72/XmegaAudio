using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using XmegaAudio.Audio;
using XmegaAudio.Hotkeys;
using XmegaAudio.Settings;

namespace XmegaAudio;

public partial class MainWindow : Window
{
    private readonly AudioEngine engine = new();
    private readonly SoundEffectsPlayer sfx = new();
    private HotkeyManager? hotkeys;
    private bool uiReady;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
        Closed += MainWindow_Closed;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var capture = engine.GetCaptureDevices();
        var render = engine.GetRenderDevices();

        CaptureDeviceCombo.ItemsSource = capture;
        RenderDeviceCombo.ItemsSource = render;

        if (capture.Count > 0)
        {
            CaptureDeviceCombo.SelectedIndex = 0;
        }

        if (render.Count > 0)
        {
            RenderDeviceCombo.SelectedIndex = 0;
        }

        EqCurve.BandGainsChanged += EqCurve_BandGainsChanged;
        hotkeys = new HotkeyManager(this);
        hotkeys.ActionTriggered += Hotkeys_ActionTriggered;
        LoadDefaultSettingsAndApply();
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        SaveDefaultSettings();
        hotkeys?.Dispose();
        sfx.Dispose();
        engine.Dispose();
    }

    private void StartStopButton_Click(object sender, RoutedEventArgs e)
    {
        if (!engine.IsRunning)
        {
            var captureId = CaptureDeviceCombo.SelectedValue as string;
            var renderId = RenderDeviceCombo.SelectedValue as string;
            if (string.IsNullOrWhiteSpace(captureId) || string.IsNullOrWhiteSpace(renderId))
            {
                return;
            }

            ApplySettingsFromUi();
            sfx.SetRenderDevice(renderId);
            engine.Start(captureId, renderId);
        }
        else
        {
            engine.Stop();
        }

        UpdateStartStopText();
    }

    private void RenderDeviceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        sfx.SetRenderDevice(RenderDeviceCombo.SelectedValue as string);
    }

    private void AnySettingChanged(object sender, RoutedEventArgs e)
    {
        if (!uiReady)
        {
            return;
        }

        ApplySettingsFromUi();
    }

    private void AnySliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!uiReady)
        {
            return;
        }

        ApplySettingsFromUi();
    }

    private void EqCurve_BandGainsChanged(object? sender, float[] e)
    {
        if (!uiReady)
        {
            return;
        }

        engine.Settings.EqBandGainsDb = e;
        engine.ApplySettings();
    }

    private void ResetEq_Click(object sender, RoutedEventArgs e)
    {
        var flat = new float[10];
        engine.Settings.EqBandGainsDb = flat;
        EqCurve.SetBandGains(flat);
        engine.ApplySettings();
    }

    private void SaveSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Title = "保存设置",
            Filter = "JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*",
            FileName = "xmega-audio-settings.json",
            AddExtension = true,
            OverwritePrompt = true
        };

        if (dlg.ShowDialog(this) != true)
        {
            return;
        }

        SettingsStore.Save(CaptureCurrentAppSettings(), dlg.FileName);
        SaveDefaultSettings();
    }

    private void LoadSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "加载设置",
            Filter = "JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*",
            Multiselect = false
        };

        if (dlg.ShowDialog(this) != true)
        {
            return;
        }

        var loaded = SettingsStore.Load(dlg.FileName);
        if (loaded is null)
        {
            return;
        }

        ApplyAppSettings(loaded);
        SaveDefaultSettings();
    }

    private void ApplySettingsFromUi()
    {
        var s = engine.Settings;
        s.NoiseSuppressionEnabled = NoiseEnabledCheck.IsChecked == true;
        s.NoiseGateThresholdDb = (float)NoiseThresholdSlider.Value;
        s.NoiseReductionDb = (float)NoiseReductionSlider.Value;
        s.NoiseSuppressStrength01 = (float)NoiseStrengthSlider.Value;

        s.AutoGainEnabled = AutoGainEnabledCheck.IsChecked == true;
        s.AutoGainTargetRmsDb = (float)AutoGainTargetSlider.Value;
        s.AutoGainMaxGainDb = (float)AutoGainMaxSlider.Value;
        s.LimiterCeilingDb = (float)LimiterCeilingSlider.Value;

        s.ReverbEnabled = ReverbEnabledCheck.IsChecked == true;
        s.ReverbRoomSize = (float)ReverbRoomSlider.Value;
        s.ReverbDamping = (float)ReverbDampingSlider.Value;
        s.ReverbWet = (float)ReverbWetSlider.Value;

        s.EqEnabled = EqEnabledCheck.IsChecked == true;
        EqCurve.IsEnabled = s.EqEnabled;

        engine.ApplySettings();
    }

    private void UpdateStartStopText()
    {
        StartStopButton.Content = engine.IsRunning ? "Stop" : "Start";
        UpdateMuteStatusText();
    }

    private void UpdateMuteStatusText()
    {
        MuteStatusText.Text = engine.IsMuted ? "Mute: On" : "Mute: Off";
    }

    private void LoadDefaultSettingsAndApply()
    {
        uiReady = false;

        var loaded = SettingsStore.LoadDefault();
        if (loaded is null)
        {
            ApplyAppSettings(new AppSettings());
        }
        else
        {
            ApplyAppSettings(loaded);
        }

        uiReady = true;
        ApplySettingsFromUi();
        ApplyHotkeysFromUi();
        UpdateStartStopText();
    }

    private void SaveDefaultSettings()
    {
        SettingsStore.SaveDefault(CaptureCurrentAppSettings());
    }

    private AppSettings CaptureCurrentAppSettings()
    {
        ApplySettingsFromUi();

        return new AppSettings
        {
            CaptureDeviceId = CaptureDeviceCombo.SelectedValue as string,
            RenderDeviceId = RenderDeviceCombo.SelectedValue as string,
            Processing = CloneProcessingSettings(engine.Settings),
            GlobalHotkeysEnabled = GlobalHotkeysCheck.IsChecked == true,
            Hotkeys = CaptureHotkeysFromUi(),
            LaughFilePath = LaughFileText.Text
        };
    }

    private void ApplyAppSettings(AppSettings settings)
    {
        var s = settings.Processing ?? new ProcessingSettings();
        var engineSettings = engine.Settings;
        CopyProcessingSettings(s, engineSettings);

        if (!string.IsNullOrWhiteSpace(settings.CaptureDeviceId))
        {
            CaptureDeviceCombo.SelectedValue = settings.CaptureDeviceId;
        }

        if (!string.IsNullOrWhiteSpace(settings.RenderDeviceId))
        {
            RenderDeviceCombo.SelectedValue = settings.RenderDeviceId;
        }

        NoiseEnabledCheck.IsChecked = engineSettings.NoiseSuppressionEnabled;
        NoiseThresholdSlider.Value = engineSettings.NoiseGateThresholdDb;
        NoiseReductionSlider.Value = engineSettings.NoiseReductionDb;
        NoiseStrengthSlider.Value = engineSettings.NoiseSuppressStrength01;

        AutoGainEnabledCheck.IsChecked = engineSettings.AutoGainEnabled;
        AutoGainTargetSlider.Value = engineSettings.AutoGainTargetRmsDb;
        AutoGainMaxSlider.Value = engineSettings.AutoGainMaxGainDb;
        LimiterCeilingSlider.Value = engineSettings.LimiterCeilingDb;

        ReverbEnabledCheck.IsChecked = engineSettings.ReverbEnabled;
        ReverbRoomSlider.Value = engineSettings.ReverbRoomSize;
        ReverbDampingSlider.Value = engineSettings.ReverbDamping;
        ReverbWetSlider.Value = engineSettings.ReverbWet;

        EqEnabledCheck.IsChecked = engineSettings.EqEnabled;
        EqCurve.MaxGainDb = engineSettings.EqMaxGainDb;
        EqCurve.SetBandGains(engineSettings.EqBandGainsDb);
        EqCurve.IsEnabled = engineSettings.EqEnabled;

        GlobalHotkeysCheck.IsChecked = settings.GlobalHotkeysEnabled;
        ApplyHotkeysToUi(settings.Hotkeys);
        LaughFileText.Text = settings.LaughFilePath ?? "";

        engine.ApplySettings();
        ApplyHotkeysFromUi();
    }

    private static ProcessingSettings CloneProcessingSettings(ProcessingSettings s)
    {
        return new ProcessingSettings
        {
            NoiseSuppressionEnabled = s.NoiseSuppressionEnabled,
            NoiseGateThresholdDb = s.NoiseGateThresholdDb,
            NoiseReductionDb = s.NoiseReductionDb,
            NoiseGateAttackMs = s.NoiseGateAttackMs,
            NoiseGateReleaseMs = s.NoiseGateReleaseMs,
            NoiseSuppressStrength01 = s.NoiseSuppressStrength01,
            AutoGainEnabled = s.AutoGainEnabled,
            AutoGainTargetRmsDb = s.AutoGainTargetRmsDb,
            AutoGainMaxGainDb = s.AutoGainMaxGainDb,
            AutoGainAttackMs = s.AutoGainAttackMs,
            AutoGainReleaseMs = s.AutoGainReleaseMs,
            LimiterCeilingDb = s.LimiterCeilingDb,
            ReverbEnabled = s.ReverbEnabled,
            ReverbRoomSize = s.ReverbRoomSize,
            ReverbDamping = s.ReverbDamping,
            ReverbWet = s.ReverbWet,
            EqEnabled = s.EqEnabled,
            EqMaxGainDb = s.EqMaxGainDb,
            EqBandGainsDb = (float[])s.EqBandGainsDb.Clone()
        };
    }

    private static void CopyProcessingSettings(ProcessingSettings from, ProcessingSettings to)
    {
        to.NoiseSuppressionEnabled = from.NoiseSuppressionEnabled;
        to.NoiseGateThresholdDb = from.NoiseGateThresholdDb;
        to.NoiseReductionDb = from.NoiseReductionDb;
        to.NoiseGateAttackMs = from.NoiseGateAttackMs;
        to.NoiseGateReleaseMs = from.NoiseGateReleaseMs;
        to.NoiseSuppressStrength01 = from.NoiseSuppressStrength01;

        to.AutoGainEnabled = from.AutoGainEnabled;
        to.AutoGainTargetRmsDb = from.AutoGainTargetRmsDb;
        to.AutoGainMaxGainDb = from.AutoGainMaxGainDb;
        to.AutoGainAttackMs = from.AutoGainAttackMs;
        to.AutoGainReleaseMs = from.AutoGainReleaseMs;
        to.LimiterCeilingDb = from.LimiterCeilingDb;

        to.ReverbEnabled = from.ReverbEnabled;
        to.ReverbRoomSize = from.ReverbRoomSize;
        to.ReverbDamping = from.ReverbDamping;
        to.ReverbWet = from.ReverbWet;

        to.EqEnabled = from.EqEnabled;
        to.EqMaxGainDb = from.EqMaxGainDb;
        to.EqBandGainsDb = (float[])from.EqBandGainsDb.Clone();
    }

    private void HotkeyUiChanged(object sender, RoutedEventArgs e)
    {
        if (!uiReady)
        {
            return;
        }

        ApplyHotkeysFromUi();
        SaveDefaultSettings();
    }

    private void HotkeyUiChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!uiReady)
        {
            return;
        }

        ApplyHotkeysFromUi();
        SaveDefaultSettings();
    }

    private void HotkeyUiChanged(object sender, TextChangedEventArgs e)
    {
        if (!uiReady)
        {
            return;
        }

        ApplyHotkeysFromUi();
        SaveDefaultSettings();
    }

    private void BrowseLaughFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "选择笑声音效文件",
            Filter = "音频文件 (*.wav;*.mp3;*.aac;*.m4a;*.wma)|*.wav;*.mp3;*.aac;*.m4a;*.wma|所有文件 (*.*)|*.*",
            Multiselect = false
        };

        if (dlg.ShowDialog(this) != true)
        {
            return;
        }

        LaughFileText.Text = dlg.FileName;
        ApplyHotkeysFromUi();
        SaveDefaultSettings();
    }

    private void Hotkeys_ActionTriggered(object? sender, string action)
    {
        Dispatcher.Invoke(() =>
        {
            switch (action)
            {
                case HotkeyActions.MuteToggle:
                    engine.ToggleMuted();
                    UpdateMuteStatusText();
                    break;
                case HotkeyActions.PlayLaugh:
                    if (!string.IsNullOrWhiteSpace(LaughFileText.Text))
                    {
                        sfx.SetRenderDevice(RenderDeviceCombo.SelectedValue as string);
                        sfx.Play(LaughFileText.Text);
                    }

                    break;
                case HotkeyActions.StopSfx:
                    sfx.Stop();
                    break;
                case HotkeyActions.ToggleNoiseSuppression:
                    NoiseEnabledCheck.IsChecked = !(NoiseEnabledCheck.IsChecked == true);
                    ApplySettingsFromUi();
                    break;
                case HotkeyActions.ToggleAutoGain:
                    AutoGainEnabledCheck.IsChecked = !(AutoGainEnabledCheck.IsChecked == true);
                    ApplySettingsFromUi();
                    break;
                case HotkeyActions.ToggleReverb:
                    ReverbEnabledCheck.IsChecked = !(ReverbEnabledCheck.IsChecked == true);
                    ApplySettingsFromUi();
                    break;
            }
        });
    }

    private void ApplyHotkeysFromUi()
    {
        if (hotkeys is null || !hotkeys.IsReady)
        {
            return;
        }

        hotkeys.UnregisterAll();
        if (GlobalHotkeysCheck.IsChecked != true)
        {
            return;
        }

        var selected = new Dictionary<string, Key>
        {
            [HotkeyActions.MuteToggle] = (Key)(MuteHotkeyCombo.SelectedItem ?? Key.None),
            [HotkeyActions.PlayLaugh] = (Key)(LaughHotkeyCombo.SelectedItem ?? Key.None),
            [HotkeyActions.StopSfx] = (Key)(StopSfxHotkeyCombo.SelectedItem ?? Key.None),
            [HotkeyActions.ToggleNoiseSuppression] = (Key)(ToggleNoiseHotkeyCombo.SelectedItem ?? Key.None),
            [HotkeyActions.ToggleAutoGain] = (Key)(ToggleAutoGainHotkeyCombo.SelectedItem ?? Key.None),
            [HotkeyActions.ToggleReverb] = (Key)(ToggleReverbHotkeyCombo.SelectedItem ?? Key.None)
        };

        var used = new Dictionary<Key, string>();
        foreach (var kv in selected)
        {
            if (kv.Value == Key.None)
            {
                continue;
            }

            if (used.TryGetValue(kv.Value, out string? existing))
            {
                MessageBox.Show(this, $"快捷键冲突：{kv.Value} 同时分配给 {existing} 和 {kv.Key}。", "快捷键冲突", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            used[kv.Value] = kv.Key;
        }

        foreach (var kv in selected)
        {
            hotkeys.Register(kv.Key, kv.Value);
        }
    }

    private static IReadOnlyList<Key> GetKeyChoices()
    {
        var list = new List<Key> { Key.None };
        for (int i = 1; i <= 24; i++)
        {
            if (Enum.TryParse<Key>($"F{i}", out var k))
            {
                list.Add(k);
            }
        }

        return list;
    }

    private void EnsureHotkeyCombosInitialized()
    {
        var keys = GetKeyChoices();
        MuteHotkeyCombo.ItemsSource = keys;
        LaughHotkeyCombo.ItemsSource = keys;
        StopSfxHotkeyCombo.ItemsSource = keys;
        ToggleNoiseHotkeyCombo.ItemsSource = keys;
        ToggleAutoGainHotkeyCombo.ItemsSource = keys;
        ToggleReverbHotkeyCombo.ItemsSource = keys;
    }

    private Dictionary<string, string> CaptureHotkeysFromUi()
    {
        return new Dictionary<string, string>
        {
            [HotkeyActions.MuteToggle] = ((Key)(MuteHotkeyCombo.SelectedItem ?? Key.None)).ToString(),
            [HotkeyActions.PlayLaugh] = ((Key)(LaughHotkeyCombo.SelectedItem ?? Key.None)).ToString(),
            [HotkeyActions.StopSfx] = ((Key)(StopSfxHotkeyCombo.SelectedItem ?? Key.None)).ToString(),
            [HotkeyActions.ToggleNoiseSuppression] = ((Key)(ToggleNoiseHotkeyCombo.SelectedItem ?? Key.None)).ToString(),
            [HotkeyActions.ToggleAutoGain] = ((Key)(ToggleAutoGainHotkeyCombo.SelectedItem ?? Key.None)).ToString(),
            [HotkeyActions.ToggleReverb] = ((Key)(ToggleReverbHotkeyCombo.SelectedItem ?? Key.None)).ToString()
        };
    }

    private void ApplyHotkeysToUi(Dictionary<string, string>? hotkeysMap)
    {
        EnsureHotkeyCombosInitialized();

        SetComboKey(MuteHotkeyCombo, hotkeysMap, HotkeyActions.MuteToggle, Key.F12);
        SetComboKey(LaughHotkeyCombo, hotkeysMap, HotkeyActions.PlayLaugh, Key.F11);
        SetComboKey(StopSfxHotkeyCombo, hotkeysMap, HotkeyActions.StopSfx, Key.F10);
        SetComboKey(ToggleNoiseHotkeyCombo, hotkeysMap, HotkeyActions.ToggleNoiseSuppression, Key.F8);
        SetComboKey(ToggleAutoGainHotkeyCombo, hotkeysMap, HotkeyActions.ToggleAutoGain, Key.F7);
        SetComboKey(ToggleReverbHotkeyCombo, hotkeysMap, HotkeyActions.ToggleReverb, Key.F9);
    }

    private static void SetComboKey(ComboBox combo, Dictionary<string, string>? map, string action, Key fallback)
    {
        if (map is not null && map.TryGetValue(action, out string? keyStr) && Enum.TryParse<Key>(keyStr, out var k))
        {
            combo.SelectedItem = k;
            return;
        }

        combo.SelectedItem = fallback;
    }
}
