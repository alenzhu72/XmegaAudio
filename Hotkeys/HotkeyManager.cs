using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace XmegaAudio.Hotkeys;

public sealed class HotkeyManager : IDisposable
{
    private const int WmHotkey = 0x0312;
    private readonly Window window;
    private readonly Dictionary<int, string> idToAction = new();
    private HwndSource? hwndSource;
    private int nextId = 1;

    public HotkeyManager(Window window)
    {
        this.window = window;
        window.SourceInitialized += OnSourceInitialized;
        window.Closed += OnClosed;

        TryInitSource();
    }

    public event EventHandler<string>? ActionTriggered;

    public bool IsReady => hwndSource is not null;

    public void Register(string action, Key key)
    {
        if (key == Key.None)
        {
            return;
        }

        if (hwndSource is null)
        {
            return;
        }

        int vk = KeyInterop.VirtualKeyFromKey(key);
        int id = nextId++;
        if (!RegisterHotKey(hwndSource.Handle, id, 0, (uint)vk))
        {
            return;
        }

        idToAction[id] = action;
    }

    public void UnregisterAll()
    {
        if (hwndSource is null)
        {
            idToAction.Clear();
            return;
        }

        foreach (int id in idToAction.Keys.ToArray())
        {
            UnregisterHotKey(hwndSource.Handle, id);
        }

        idToAction.Clear();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        TryInitSource();
    }

    private void TryInitSource()
    {
        if (hwndSource is not null)
        {
            return;
        }

        hwndSource = (HwndSource?)PresentationSource.FromVisual(window);
        if (hwndSource is null)
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle != IntPtr.Zero)
            {
                hwndSource = HwndSource.FromHwnd(handle);
            }
        }

        hwndSource?.AddHook(WndProc);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        Dispose();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey)
        {
            int id = wParam.ToInt32();
            if (idToAction.TryGetValue(id, out string? action))
            {
                ActionTriggered?.Invoke(this, action);
                handled = true;
            }
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        UnregisterAll();

        if (hwndSource is not null)
        {
            hwndSource.RemoveHook(WndProc);
            hwndSource = null;
        }

        window.SourceInitialized -= OnSourceInitialized;
        window.Closed -= OnClosed;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
