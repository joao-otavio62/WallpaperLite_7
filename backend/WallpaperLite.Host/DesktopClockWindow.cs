using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace WallpaperLite;

public sealed class DesktopClockWindow : IDisposable
{
    const int ClockWidth = 420, ClockHeight = 68;
    readonly HwndSource _source;
    readonly TextBlock _time;
    readonly DispatcherTimer _timer;
    readonly Action<IntPtr> _keepAboveDesktop;
    bool _disposed;

    public IntPtr Handle => _source.Handle;

    public DesktopClockWindow(Action<IntPtr> keepAboveDesktop)
    {
        _keepAboveDesktop = keepAboveDesktop;
        var screen = System.Windows.Forms.Screen.PrimaryScreen
            ?? throw new InvalidOperationException("Não foi possível localizar o monitor principal para o relógio.");
        var bounds = screen.Bounds;
        var width = Math.Min(ClockWidth, bounds.Width);
        _time = new TextBlock
        {
            Text = DateTime.Now.ToString("HH:mm:ss"),
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 38,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
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
            Child = _time,
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
        _source = new HwndSource(parameters) { RootVisual = root };
        _source.AddHook(PassThrough);
        Native.Place(_source.Handle, IntPtr.Zero, new IntPtr(1), parameters.PositionX, parameters.PositionY, width, ClockHeight);
        _keepAboveDesktop(_source.Handle);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) =>
        {
            _time.Text = DateTime.Now.ToString("HH:mm:ss");
            _keepAboveDesktop(Handle);
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
        _source.RemoveHook(PassThrough);
        _source.Dispose();
    }
}
