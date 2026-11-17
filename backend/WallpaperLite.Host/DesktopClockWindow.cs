using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace WallpaperLite;

public sealed class DesktopClockWindow : IDisposable
{
    const int ClockWidth = 420, ClockHeight = 112;
    sealed class ClockSurface
    {
        public required HwndSource Source;
        public required TextBlock Time;
    }

    readonly ClockSurface[] _surfaces;
    readonly DispatcherTimer _timer;
    readonly Action<IntPtr[]> _keepAboveDesktop;
    bool _disposed;

    public IntPtr[] Handles => _surfaces.Select(surface => surface.Source.Handle).ToArray();

    public DesktopClockWindow(Action<IntPtr[]> keepAboveDesktop)
    {
        _keepAboveDesktop = keepAboveDesktop;
        var surfaces = new List<ClockSurface>();
        try
        {
            foreach (var screen in System.Windows.Forms.Screen.AllScreens)
            {
                var bounds = screen.Bounds;
                var width = Math.Min(ClockWidth, bounds.Width);
                var time = new TextBlock
                {
                    Text = DateTime.Now.ToString("HH:mm:ss"),
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 38,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brushes.White,
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Center,
                    Effect = new DropShadowEffect
                    {
                        Color = Colors.Black,
                        BlurRadius = 7,
                        ShadowDepth = 2,
                        Opacity = 0.95,
                    },
                };
                var root = new Border
                {
                    Background = Brushes.Transparent,
                    Padding = new Thickness(0),
                    Child = time,
                };
                var parameters = new HwndSourceParameters("WallpaperLiteDesktopClock")
                {
                    WindowStyle = unchecked((int)Native.ChildOrPopupStyle(IntPtr.Zero)),
                    ExtendedWindowStyle = (int)(Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE | Native.WS_EX_TRANSPARENT | Native.WS_EX_LAYERED),
                    UsesPerPixelOpacity = true,
                    PositionX = bounds.Left + (bounds.Width - width) / 2,
                    PositionY = bounds.Top + 20,
                    Width = width,
                    Height = ClockHeight,
                };
                var source = new HwndSource(parameters) { RootVisual = root };
                var surface = new ClockSurface { Source = source, Time = time };
                surfaces.Add(surface);
                source.AddHook(PassThrough);
                Native.Place(source.Handle, IntPtr.Zero, new IntPtr(1), parameters.PositionX, parameters.PositionY, width, ClockHeight);
            }

            if (surfaces.Count == 0)
                throw new InvalidOperationException("Não foi possível localizar monitores para exibir o relógio.");
        }
        catch
        {
            foreach (var surface in surfaces)
            {
                surface.Source.RemoveHook(PassThrough);
                surface.Source.Dispose();
            }
            throw;
        }

        _surfaces = surfaces.ToArray();
        _keepAboveDesktop(Handles);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) =>
        {
            var now = DateTime.Now.ToString("HH:mm:ss");
            foreach (var surface in _surfaces) surface.Time.Text = now;
            _keepAboveDesktop(Handles);
        };
        _timer.Start();
    }

    static IntPtr PassThrough(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == 0x0084) { handled = true; return new IntPtr(-1); }
        if (message == 0x0021) { handled = true; return new IntPtr(3); }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        foreach (var surface in _surfaces)
        {
            surface.Source.RemoveHook(PassThrough);
            surface.Source.Dispose();
        }
    }
}
