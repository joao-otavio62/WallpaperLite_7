using System;
using System.Linq;
using System.Windows.Interop;

namespace WallpaperLite;

public sealed class DesktopControls : IDisposable
{
    const int TaskbarHotkeyId = 1;
    const int WmHotkey = 0x0312;
    const uint ModControl = 0x0002, ModAlt = 0x0001;
    const uint VirtualKeyT = 0x54;
    readonly HwndSource _hotkeyWindow = new(new HwndSourceParameters("WallpaperLiteDesktopControls", 0, 0));
    readonly bool _hotkeyRegistered;
    bool? _originalIconsVisible;
    bool? _originalTaskbarAutoHide;
    bool _disposed;

    public DesktopControls()
    {
        _hotkeyWindow.AddHook(HandleHotkey);
        _hotkeyRegistered = Native.RegisterHotKey(_hotkeyWindow.Handle, TaskbarHotkeyId, ModControl | ModAlt, VirtualKeyT);
        if (!_hotkeyRegistered) Log.Write("Atalho Ctrl+Alt+T indisponível; não será possível ocultar a barra de tarefas.");
    }

    public event Action? Changed;

    public bool DesktopIconsVisible => GetVisibility(Native.GetDesktopIconViews());
    public bool TaskbarVisible => !Native.IsTaskbarAutoHideEnabled;

    public void ToggleDesktopIcons()
    {
        Toggle(Native.GetDesktopIconViews(), DesktopIconsVisible, ref _originalIconsVisible, "os ícones da área de trabalho");
        Changed?.Invoke();
    }

    public void ToggleTaskbar()
    {
        if (TaskbarVisible && !_hotkeyRegistered)
            throw new InvalidOperationException("Não foi possível registrar Ctrl+Alt+T; a barra de tarefas não será ocultada.");
        var autoHide = Native.IsTaskbarAutoHideEnabled;
        _originalTaskbarAutoHide ??= autoHide;
        if (!Native.SetTaskbarAutoHideEnabled(!autoHide))
            throw new InvalidOperationException("Não foi possível alterar a visibilidade da barra de tarefas.");
        Changed?.Invoke();
    }

    IntPtr HandleHotkey(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message != WmHotkey || wParam.ToInt32() != TaskbarHotkeyId) return IntPtr.Zero;
        handled = true;
        try { ToggleTaskbar(); }
        catch (InvalidOperationException ex) { Log.Write("Ctrl+Alt+T: " + ex.Message); }
        return IntPtr.Zero;
    }

    static bool GetVisibility(IntPtr[] windows) => windows.Length == 0 || windows.All(Native.IsWindowVisible);

    static void Toggle(IntPtr[] windows, bool currentlyVisible, ref bool? originalVisibility, string target)
    {
        if (windows.Length == 0)
            throw new InvalidOperationException($"Não foi possível localizar {target} no Windows.");

        originalVisibility ??= currentlyVisible;
        if (!Native.SetWindowsVisible(windows, !currentlyVisible))
            throw new InvalidOperationException($"Não foi possível alterar a visibilidade de {target}.");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_hotkeyRegistered) Native.UnregisterHotKey(_hotkeyWindow.Handle, TaskbarHotkeyId);
        _hotkeyWindow.Dispose();
        Restore(_originalIconsVisible, Native.GetDesktopIconViews(), "ícones da área de trabalho");
        if (_originalTaskbarAutoHide.HasValue &&
            !Native.SetTaskbarAutoHideEnabled(_originalTaskbarAutoHide.Value))
            Log.Write("Não foi possível restaurar a configuração original de ocultação da barra de tarefas.");
    }

    static void Restore(bool? visibility, IntPtr[] windows, string target)
    {
        if (!visibility.HasValue || windows.Length == 0) return;
        if (!Native.SetWindowsVisible(windows, visibility.Value))
            Log.Write($"Não foi possível restaurar a visibilidade de {target}.");
    }
}
