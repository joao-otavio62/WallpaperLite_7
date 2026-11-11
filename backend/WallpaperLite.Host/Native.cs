using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace WallpaperLite;

[StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
[StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
[StructLayout(LayoutKind.Sequential)]
struct APPBARDATA
{
    public uint cbSize;
    public IntPtr hWnd;
    public uint uCallbackMessage, uEdge;
    public RECT rc;
    public IntPtr lParam;
}
[StructLayout(LayoutKind.Sequential)]
public struct BITMAPINFO
{
    public int Size, Width, Height; public short Planes, BitCount; public int Compression, SizeImage, XPels, YPels, ClrUsed, ClrImportant;
}

/// <summary>P/Invoke (user32/shell32/dwmapi).</summary>
public static class Native
{
    public delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);

    const uint AbmGetState = 0x00000004, AbmSetState = 0x0000000A;
    const uint AbsAutoHide = 0x00000001;

    public const int GWL_STYLE = -16, GWL_EXSTYLE = -20;
    public const long WS_POPUP = 0x80000000L, WS_CHILD = 0x40000000L;
    public const long WS_EX_TOOLWINDOW = 0x80, WS_EX_TRANSPARENT = 0x20, WS_EX_LAYERED = 0x00080000, WS_EX_NOACTIVATE = 0x08000000;

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", CharSet = CharSet.Unicode)]
    static extern bool SpiSet(uint action, uint param, string? vparam, uint winIni);
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", CharSet = CharSet.Unicode)]
    static extern bool SpiGet(uint action, uint param, StringBuilder vparam, uint winIni);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string? cls, string? title);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string? cls, string? title);
    [DllImport("user32.dll")] static extern IntPtr SendMessageTimeout(IntPtr h, uint msg, IntPtr w, IntPtr l, uint flags, uint timeout, out IntPtr result);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc proc, IntPtr lParam);
    [DllImport("user32.dll")] public static extern IntPtr SetParent(IntPtr child, IntPtr parent);
    [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr h, int x, int y, int w, int hh, bool repaint);
    [DllImport("user32.dll")] public static extern bool ScreenToClient(IntPtr h, ref POINT p);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] public static extern IntPtr GetWindowLongPtr(IntPtr h, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] public static extern IntPtr SetWindowLongPtr(IntPtr h, int index, IntPtr value);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int command);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsZoomed(IntPtr h);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder sb, int max);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern uint RegisterWindowMessage(string name);
    [DllImport("user32.dll")] public static extern bool RegisterShellHookWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool DeregisterShellHookWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern bool SetLayeredWindowAttributes(IntPtr h, uint colorKey, byte alpha, uint flags);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint virtualKey);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("shell32.dll")] public static extern int SHQueryUserNotificationState(out int state);
    [DllImport("shell32.dll")] static extern UIntPtr SHAppBarMessage(uint message, ref APPBARDATA data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "CreateWindowExW")]
    public static extern IntPtr CreateWindowEx(uint exStyle, string cls, string? title, uint style, int x, int y, int w, int h, IntPtr parent, IntPtr menu, IntPtr inst, IntPtr param);
    [DllImport("user32.dll")] public static extern bool DestroyWindow(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr h, uint cmd);
    [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr h);
    [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr h, IntPtr dc);
    [DllImport("user32.dll")] public static extern bool ValidateRect(IntPtr h, IntPtr rect);
    [DllImport("user32.dll")] public static extern int FillRect(IntPtr dc, ref RECT r, IntPtr brush);
    [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(POINT p);
    [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr h, uint flags);
    [DllImport("gdi32.dll")] public static extern IntPtr CreateSolidBrush(uint colorRef);
    [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr o);
    [DllImport("gdi32.dll")] public static extern int SetStretchBltMode(IntPtr dc, int mode);
    [DllImport("gdi32.dll")] public static extern int StretchDIBits(IntPtr dc, int xd, int yd, int wd, int hd, int xs, int ys, int ws, int hs, IntPtr bits, ref BITMAPINFO bmi, uint usage, uint rop);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int attr, out int value, int size);

    public static string ClassName(IntPtr h) { var sb = new StringBuilder(64); GetClassName(h, sb, 64); return sb.ToString(); }
    public static bool IsCloaked(IntPtr h) => DwmGetWindowAttribute(h, 14 /*DWMWA_CLOAKED*/, out var v, 4) == 0 && v != 0;
    public static bool IsTaskbarAutoHideEnabled
    {
        get
        {
            return (GetTaskbarState() & AbsAutoHide) != 0;
        }
    }

    public static bool SetTaskbarAutoHideEnabled(bool enabled)
    {
        var state = GetTaskbarState();
        var data = new APPBARDATA
        {
            cbSize = (uint)Marshal.SizeOf<APPBARDATA>(),
            lParam = new IntPtr(enabled ? state | AbsAutoHide : state & ~AbsAutoHide),
        };
        SHAppBarMessage(AbmSetState, ref data);
        return IsTaskbarAutoHideEnabled == enabled;
    }

    static uint GetTaskbarState()
    {
        var data = new APPBARDATA { cbSize = (uint)Marshal.SizeOf<APPBARDATA>() };
        return (uint)SHAppBarMessage(AbmGetState, ref data).ToUInt64();
    }

    public static IntPtr[] GetDesktopIconViews()
    {
        var windows = new List<IntPtr>();
        EnumWindows((top, _) =>
        {
            var view = FindWindowEx(top, IntPtr.Zero, "SHELLDLL_DefView", null);
            var icons = view != IntPtr.Zero ? FindWindowEx(view, IntPtr.Zero, "SysListView32", null) : IntPtr.Zero;
            if (icons != IntPtr.Zero) windows.Add(icons);
            return true;
        }, IntPtr.Zero);
        return windows.ToArray();
    }

    public static IntPtr[] GetTaskbars()
    {
        var windows = new List<IntPtr>();
        var primary = FindWindow("Shell_TrayWnd", null);
        if (primary != IntPtr.Zero) windows.Add(primary);
        EnumWindows((top, _) =>
        {
            if (ClassName(top) == "Shell_SecondaryTrayWnd" && !windows.Contains(top)) windows.Add(top);
            return true;
        }, IntPtr.Zero);
        return windows.ToArray();
    }

    public static bool SetWindowsVisible(IntPtr[] windows, bool visible)
    {
        foreach (var window in windows)
        {
            ShowWindow(window, visible ? 8 /*SW_SHOWNA*/ : 0 /*SW_HIDE*/);
            if (IsWindowVisible(window) != visible) return false;
        }
        return windows.Length > 0;
    }

    /// <summary>Wallpaper estático: custo zero depois da troca.</summary>
    public static void SetWallpaper(string path)
    {
        using (var k = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", writable: true))
        {
            k?.SetValue("WallpaperStyle", "10"); // Fill
            k?.SetValue("TileWallpaper", "0");
        }
        SpiSet(0x0014 /*SETDESKWALLPAPER*/, 0, path, 0x01 | 0x02 /*UPDATEINIFILE|SENDCHANGE*/);
    }

    public static string? GetCurrentWallpaper()
    {
        var sb = new StringBuilder(260);
        return SpiGet(0x0073 /*GETDESKWALLPAPER*/, 260, sb, 0) && sb.Length > 0 ? sb.ToString() : null;
    }

    /// <summary>Acha a janela "WorkerW" (atrás dos ícones) para hospedar o wallpaper animado.</summary>
    public static IntPtr GetWallpaperHost()
    {
        var progman = FindWindow("Progman", null);
        SendMessageTimeout(progman, 0x052C, new IntPtr(0xD), new IntPtr(1), 0, 1000, out _); // cria o WorkerW

        var workerW = IntPtr.Zero;
        EnumWindows((top, _) =>
        {
            if (FindWindowEx(top, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero)
                workerW = FindWindowEx(IntPtr.Zero, top, "WorkerW", null); // Windows 10 / 11 antigo
            return true;
        }, IntPtr.Zero);

        if (workerW == IntPtr.Zero) workerW = FindWindowEx(progman, IntPtr.Zero, "WorkerW", null); // Windows 11 24H2+
        return workerW != IntPtr.Zero ? workerW : progman;
    }

    public const uint SWP_NOACTIVATE = 0x10, SWP_SHOWWINDOW = 0x40, SWP_NOMOVE = 0x2, SWP_NOSIZE = 0x1;
    public const uint LWA_ALPHA = 0x2;

    /// <summary>Posiciona a superfície atrás da camada de ícones da área de trabalho.</summary>
    public static void Place(IntPtr hwnd, IntPtr parent, IntPtr after, int x, int y, int w, int h)
    {
        SetWindowPos(hwnd, after, x, y, w, h, SWP_NOACTIVATE | SWP_SHOWWINDOW);
        if (parent == IntPtr.Zero)
        {
            var progman = FindWindow("Progman", null);
            if (progman != IntPtr.Zero)
                SetWindowPos(progman, hwnd, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }
    }

    public static void KeepAboveProgman(IntPtr[] surfaces)
    {
        var progman = FindWindow("Progman", null);
        if (progman == IntPtr.Zero || surfaces.Length == 0) return;
        if (Array.IndexOf(surfaces, GetWindow(progman, 3 /*GW_HWNDPREV*/)) >= 0) return;
        foreach (var hwnd in surfaces)
        {
            SetWindowPos(hwnd, new IntPtr(1), 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            SetWindowPos(progman, hwnd, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }
    }

    public static uint ChildOrPopupStyle(IntPtr parent) => parent == IntPtr.Zero ? 0x90000000u : 0x50000000u; // POPUP|VISIBLE ou CHILD|VISIBLE

    public static readonly string[] AttachModes =
    {
        "Automático",
        "Progman, logo abaixo dos ícones",
        "WorkerW filho do Progman",
        "Progman, no fundo (HWND_BOTTOM)",
        "WorkerW clássico (irmão)",
        "Janela de topo acima do Progman (compatível com Windows 11 24H2+, pode cobrir os ícones)",
    };

    /// <summary>Como o quadro do GIF é desenhado na superfície: WPF (GPU), WPF por software ou GDI puro.</summary>
    public static readonly string[] Renderers = { "WPF", "WPF (software)", "GDI" };

    static readonly string ModeFile = Path.Combine(AppPaths.Root, "attach-mode.txt");
    static int _mode = LoadMode();
    static int LoadMode()
    {
        try { if (int.TryParse(File.ReadAllText(ModeFile), out var m) && m >= 0 && m < AttachModes.Length) return m; }
        catch (IOException) { }
        return 0;
    }
    public static int AttachMode
    {
        get => _mode;
        set { _mode = value; try { Directory.CreateDirectory(AppPaths.Root); File.WriteAllText(ModeFile, value.ToString()); } catch (IOException) { } }
    }

    static readonly string RenderFile = Path.Combine(AppPaths.Root, "renderer.txt");
    static int _renderer = LoadRenderer();
    static int LoadRenderer()
    {
        try { if (int.TryParse(File.ReadAllText(RenderFile), out var m) && m >= 0 && m < Renderers.Length) return m; }
        catch (IOException) { }
        return 0;
    }
    public static int Renderer
    {
        get => _renderer;
        set { _renderer = value; try { Directory.CreateDirectory(AppPaths.Root); File.WriteAllText(RenderFile, value.ToString()); } catch (IOException) { } }
    }

    /// <summary>Escreve no log a árvore de janelas do desktop (ordem Z, de cima para baixo).</summary>
    public static void DumpDesktop(string title)
    {
        void One(IntPtr h, string indent)
        {
            GetWindowRect(h, out var r);
            Log.Write($"{indent}0x{h:X} {ClassName(h)} visivel={IsWindowVisible(h)} ({r.Left},{r.Top},{r.Right},{r.Bottom})");
        }
        void WithChildren(IntPtr h, string indent)
        {
            One(h, indent);
            for (var c = FindWindowEx(h, IntPtr.Zero, null, null); c != IntPtr.Zero; c = FindWindowEx(h, c, null, null))
                One(c, indent + "    ");
        }
        Log.Write($"--- arvore do desktop ({title}) ---");
        WithChildren(FindWindow("Progman", null), "Progman ");
        EnumWindows((top, _) => { if (ClassName(top) == "WorkerW") WithChildren(top, "WorkerW(topo) "); return true; }, IntPtr.Zero);
    }

    /// <summary>
    /// Descobre onde encaixar a superfície do wallpaper: janela-pai, posição no eixo Z e retângulo (coords do pai).
    /// </summary>
    public static void GetDesktopTarget(out IntPtr parent, out IntPtr after, out int x, out int y, out int w, out int h, out string layout)
        => GetDesktopTarget(AttachMode, true, out parent, out after, out x, out y, out w, out h, out layout);

    public static void GetDesktopTarget(int mode, bool log, out IntPtr parent, out IntPtr after, out int x, out int y, out int w, out int h, out string layout)
    {
        var progman = FindWindow("Progman", null);
        SendMessageTimeout(progman, 0x052C, new IntPtr(0xD), new IntPtr(1), 0, 1000, out _); // cria o WorkerW

        var classicWorker = IntPtr.Zero;
        EnumWindows((top, _) =>
        {
            if (FindWindowEx(top, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero)
                classicWorker = FindWindowEx(IntPtr.Zero, top, "WorkerW", null);
            return true;
        }, IntPtr.Zero);
        var childWorker = FindWindowEx(progman, IntPtr.Zero, "WorkerW", null);
        var defView = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);

        if (mode == 0)
        {
            var build = Environment.OSVersion.Version.Build;
            mode = build >= 26100 ? 5
                : classicWorker != IntPtr.Zero ? 4
                : childWorker != IntPtr.Zero ? 2
                : defView != IntPtr.Zero ? 1
                : 3;
        }
        var bottom = new IntPtr(1); // HWND_BOTTOM
        switch (mode)
        {
            case 1: parent = progman; after = defView != IntPtr.Zero ? defView : bottom; break;
            case 2:
                parent = childWorker != IntPtr.Zero ? childWorker : progman;
                after = childWorker != IntPtr.Zero ? IntPtr.Zero : defView != IntPtr.Zero ? defView : bottom;
                break;
            case 4:
                parent = classicWorker != IntPtr.Zero ? classicWorker : progman;
                after = classicWorker != IntPtr.Zero ? IntPtr.Zero : defView != IntPtr.Zero ? defView : bottom;
                break;
            case 5: parent = IntPtr.Zero; after = bottom; break;
            default: parent = progman; after = bottom; break;
        }
        layout = AttachModes[mode];

        var b = System.Windows.Forms.Screen.PrimaryScreen!.Bounds;   // pixels físicos
        var pt = new POINT { X = b.Left, Y = b.Top };
        if (parent != IntPtr.Zero) ScreenToClient(parent, ref pt);
        x = pt.X; y = pt.Y; w = b.Width; h = b.Height;
        if (!log) return;
        DumpDesktop("antes");
        Log.Write($"Destino: modo={layout} pai=0x{parent:X} classe={ClassName(parent)} after=0x{after:X} rect=({x},{y},{w},{h}) SO={Environment.OSVersion.Version}");
    }
}
