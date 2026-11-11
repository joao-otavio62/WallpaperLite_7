using System;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Win32;

namespace WallpaperLite;

/// <summary>
/// Pausa inteligente: pausa o GIF em bateria, em tela cheia/jogo/apresentação ou com janela maximizada cobrindo o desktop.
/// Reage a eventos do shell (RegisterShellHookWindow) e energia; um timer de 2s cobre o que não gera evento.
/// </summary>
public sealed class PauseMonitor : IDisposable
{
    readonly WallpaperManager _w;
    readonly HwndSource _src;
    readonly uint _shellMsg = Native.RegisterWindowMessage("SHELLHOOK");
    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(2) };

    public PauseMonitor(WallpaperManager w)
    {
        _w = w;
        _src = new HwndSource(new HwndSourceParameters("WallpaperLiteShellHook", 0, 0)); // janela oculta
        Native.RegisterShellHookWindow(_src.Handle);
        _src.AddHook(Hook);
        SystemEvents.PowerModeChanged += OnPower;
        _timer.Tick += (_, _) => Evaluate();
        _timer.Start();
    }

    IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == _shellMsg) Evaluate();
        return IntPtr.Zero;
    }

    void OnPower(object? s, PowerModeChangedEventArgs e) => _src.Dispatcher.BeginInvoke(Evaluate);

    void Evaluate()
    {
        if (!_w.HasAnimated) { _w.SetAutoPaused(null); return; }
        _w.SetAutoPaused(OnBattery() ? "notebook na bateria"
            : FullscreenApp() ? "app em tela cheia"
            : CoveredByWindow() ? "janela maximizada" : null);
    }

    static bool OnBattery() => SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Offline;

    static bool FullscreenApp()
    {
        // Em primeiro plano o desktop/shell/nossa janela (a superfície do wallpaper cobre o monitor e o Windows
        // a trata como "tela cheia"): nunca pausar por isso.
        var fg = Native.GetForegroundWindow();
        if (fg == IntPtr.Zero || IsShellOrOurs(fg)) return false;

        // 2 = ocupado/tela cheia, 3 = Direct3D tela cheia, 4 = modo apresentação
        if (Native.SHQueryUserNotificationState(out var s) == 0 && s is 2 or 3 or 4) return true;

        // Jogos "borderless": janela em primeiro plano ocupando o monitor inteiro
        if (!Native.GetWindowRect(fg, out var r)) return false;
        var b = Screen.FromHandle(fg).Bounds;
        return r.Left <= b.Left && r.Top <= b.Top && r.Right >= b.Right && r.Bottom >= b.Bottom;
    }

    static bool CoveredByWindow()
    {
        var covered = false;
        Native.EnumWindows((h, _) =>
        {
            if (!Native.IsWindowVisible(h) || Native.IsIconic(h) || !Native.IsZoomed(h) || Native.IsCloaked(h) || IsShellOrOurs(h)) return true;
            if (!Screen.FromHandle(h).Primary) return true;
            covered = true;
            return false;
        }, IntPtr.Zero);
        return covered;
    }

    static bool IsShellOrOurs(IntPtr h)
    {
        Native.GetWindowThreadProcessId(h, out var pid);
        if (pid == Environment.ProcessId) return true;
        var cls = Native.ClassName(h);
        return cls is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd";
    }

    public void Dispose()
    {
        _timer.Stop();
        SystemEvents.PowerModeChanged -= OnPower;
        Native.DeregisterShellHookWindow(_src.Handle);
        _src.Dispose();
    }
}
