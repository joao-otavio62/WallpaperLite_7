using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SkiaSharp;

namespace WallpaperLite;

/// <summary>
/// Superfícies nativas que exibem o GIF atrás dos ícones, uma por monitor. Os frames são decodificados sob demanda
/// (SKCodec) para UM único buffer compartilhado.
/// Renderizadores: WPF, WPF por software ou GDI puro (StretchDIBits).
/// </summary>
public sealed class AnimatedWallpaperWindow
{
    sealed class Surface
    {
        public IntPtr Hwnd;
        public HwndSource? Src;
        public Image? Img;
        public int W, H;
        public SKBitmap? Buf;     // só no GDI: frame já escalado (UniformToFill) para este monitor
    }

    readonly string _path;
    readonly ManualResetEventSlim _run = new(true);
    readonly CancellationTokenSource _cts = new();
    readonly object _gate = new();
    Surface[] _surfaces = Array.Empty<Surface>();
    Dispatcher? _ui;
    DispatcherTimer? _guard;
    volatile bool _closed;

    static bool Gdi => Native.Renderer == 2;

    public AnimatedWallpaperWindow(string path) => _path = path;

    public void SetPaused(bool paused) { if (paused) _run.Reset(); else _run.Set(); }

    public void Show()
    {
        _ui = Dispatcher.CurrentDispatcher;
        Native.GetDesktopTarget(out var parent, out var after, out _, out _, out _, out _, out _);

        var list = new List<Surface>();
        try
        {
            foreach (var screen in System.Windows.Forms.Screen.AllScreens)
            {
                var bounds = screen.Bounds;
                var position = new POINT { X = bounds.Left, Y = bounds.Top };
                if (parent != IntPtr.Zero && !Native.ScreenToClient(parent, ref position))
                    throw new InvalidOperationException($"Não foi possível posicionar o GIF no monitor {screen.DeviceName}.");

                var sf = Create(parent, after, position.X, position.Y, bounds.Width, bounds.Height);
                if (sf == null)
                    throw new InvalidOperationException($"Não foi possível criar a superfície do GIF no monitor {screen.DeviceName}.");
                list.Add(sf);
                Native.GetWindowRect(sf.Hwnd, out var rc);
                Log.Write($"Superficie criada: monitor={screen.DeviceName} renderizador={Native.Renderers[Native.Renderer]} visivel={Native.IsWindowVisible(sf.Hwnd)} rect=({rc.Left},{rc.Top},{rc.Right},{rc.Bottom})");
            }
        }
        catch
        {
            foreach (var sf in list)
            {
                if (sf.Src != null) sf.Src.Dispose();
                else if (sf.Hwnd != IntPtr.Zero) Native.DestroyWindow(sf.Hwnd);
            }
            throw;
        }
        _surfaces = list.ToArray();
        Native.DumpDesktop("depois");
        if (_surfaces.Length == 0) return;

        if (parent == IntPtr.Zero)
        {
            var handles = Array.ConvertAll(_surfaces, surface => surface.Hwnd);
            _guard = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
            _guard.Tick += (_, _) => Native.KeepAboveProgman(handles);
            _guard.Start();
        }
        Task.Run(() => Loop(_cts.Token));
    }

    static IntPtr PassThrough(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == 0x0084) { handled = true; return new IntPtr(-1); }
        if (msg == 0x0021) { handled = true; return new IntPtr(3); }
        return IntPtr.Zero;
    }

    static Surface? Create(IntPtr parent, IntPtr after, int x, int y, int w, int h)
    {
        var sf = new Surface { W = w, H = h };
        var clickThrough = parent == IntPtr.Zero;
        var extendedStyle = Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE;
        if (clickThrough) extendedStyle |= Native.WS_EX_TRANSPARENT | Native.WS_EX_LAYERED;
        if (Gdi)
        {
            // STATIC é transparente a cliques por padrão (HTTRANSPARENT); SS_OWNERDRAW não pinta nada sozinho.
            sf.Hwnd = Native.CreateWindowEx((uint)extendedStyle, "STATIC", null,
                Native.ChildOrPopupStyle(parent) | 0x04000000u | 0x0Du, x, y, w, h, parent, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            if (sf.Hwnd == IntPtr.Zero) { Log.Write("GDI: CreateWindowEx falhou."); return null; }
            if (clickThrough && !Native.SetLayeredWindowAttributes(sf.Hwnd, 0, 255, Native.LWA_ALPHA))
                Log.Write("GDI: não foi possível configurar a janela transparente a cliques.");
        }
        else
        {
            sf.Img = new Image { Stretch = Stretch.UniformToFill };
            RenderOptions.SetBitmapScalingMode(sf.Img, BitmapScalingMode.LowQuality);
            var p = new HwndSourceParameters("WallpaperLiteSurface")
            {
                ParentWindow = parent,
                WindowStyle = unchecked((int)Native.ChildOrPopupStyle(parent)),
                ExtendedWindowStyle = (int)extendedStyle,
                UsesPerPixelOpacity = clickThrough,
                PositionX = x, PositionY = y, Width = w, Height = h,
            };
            sf.Src = new HwndSource(p) { RootVisual = new Border { Background = Brushes.Black, Child = sf.Img } };
            if (Native.Renderer == 1 && sf.Src.CompositionTarget != null) sf.Src.CompositionTarget.RenderMode = RenderMode.SoftwareOnly;
            if (clickThrough) sf.Src.AddHook(PassThrough);
            sf.Hwnd = sf.Src.Handle;
        }
        Native.Place(sf.Hwnd, parent, after, x, y, w, h);
        return sf;
    }

    public void Close()
    {
        _closed = true;
        _cts.Cancel();
        _run.Set();
        _guard?.Stop();
        _guard = null;
        Surface[] all;
        lock (_gate) { all = _surfaces; _surfaces = Array.Empty<Surface>(); }
        foreach (var sf in all)
        {
            lock (_gate) { sf.Buf?.Dispose(); sf.Buf = null; }
            if (sf.Src != null) sf.Src.Dispose();
            else if (sf.Hwnd != IntPtr.Zero) Native.DestroyWindow(sf.Hwnd);
            sf.Hwnd = IntPtr.Zero;
        }
    }

    /// <summary>Modo GDI: escala o frame (UniformToFill) para o tamanho do monitor e copia para a janela.</summary>
    void PresentGdi(Surface sf, SKBitmap frame)
    {
        lock (_gate)
        {
            var hwnd = sf.Hwnd;
            if (_closed || hwnd == IntPtr.Zero) return;
            sf.Buf ??= new SKBitmap(new SKImageInfo(sf.W, sf.H, SKColorType.Bgra8888, SKAlphaType.Premul));
            using (var canvas = new SKCanvas(sf.Buf))
            {
                canvas.Clear(SKColors.Black);
                var scale = Math.Max((float)sf.W / frame.Width, (float)sf.H / frame.Height);
                float dw = frame.Width * scale, dh = frame.Height * scale;
                canvas.DrawBitmap(frame, SKRect.Create((sf.W - dw) / 2, (sf.H - dh) / 2, dw, dh));
            }
            var bi = new BITMAPINFO { Size = 40, Width = sf.W, Height = -sf.H, Planes = 1, BitCount = 32 };
            var dc = Native.GetDC(hwnd);
            if (dc == IntPtr.Zero) return;
            Native.StretchDIBits(dc, 0, 0, sf.W, sf.H, 0, 0, sf.W, sf.H, sf.Buf.GetPixels(), ref bi, 0, 0x00CC0020);
            Native.ReleaseDC(hwnd, dc);
        }
    }

    void Loop(CancellationToken ct)
    {
        try
        {
            using var codec = SKCodec.Create(_path);
            if (codec == null) { Log.Write("SKCodec nao conseguiu abrir o GIF."); return; }
            int w = codec.Info.Width, h = codec.Info.Height;
            if ((long)w * h > 7680L * 4320) return;                  // protege contra imagens "bomba"

            var frames = codec.FrameInfo;
            using var bmp = new SKBitmap(new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Premul));
            WriteableBitmap? wb = null;
            if (!Gdi)
                _ui!.Invoke(() =>
                {
                    wb = new WriteableBitmap(w, h, 96, 96, PixelFormats.Pbgra32, null);
                    foreach (var sf in _surfaces) if (sf.Img != null) sf.Img.Source = wb;   // mesmo bitmap em todos os monitores
                });

            int last = -1;
            while (true)
            {
                for (int i = 0; i < Math.Max(1, frames.Length); i++)
                {
                    _run.Wait(ct);                                   // pausado = thread dormindo, 0% CPU
                    long t0 = Environment.TickCount64;
                    Decode(codec, bmp, frames, i, ref last);
                    if (Gdi) foreach (var sf in _surfaces) PresentGdi(sf, bmp);
                    else _ui!.Invoke(() => wb!.WritePixels(new Int32Rect(0, 0, w, h), bmp.GetPixels(), bmp.ByteCount, bmp.RowBytes));
                    if (frames.Length <= 1) return;                  // GIF de 1 frame: desenha e encerra

                    int wait = (frames[i].Duration > 0 ? frames[i].Duration : 100) - (int)(Environment.TickCount64 - t0);
                    if (wait > 0) ct.WaitHandle.WaitOne(wait);
                    ct.ThrowIfCancellationRequested();
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Log.Write("Falha ao exibir o GIF: " + ex); }
    }

    static void Decode(SKCodec codec, SKBitmap bmp, SKCodecFrameInfo[] frames, int i, ref int last)
    {
        int req = frames.Length > 1 ? frames[i].RequiredFrame : -1;
        if (req >= 0 && req != last) Decode(codec, bmp, frames, req, ref last);   // frame depende de outro
        codec.GetPixels(bmp.Info, bmp.GetPixels(), new SKCodecOptions(i, req));
        last = i;
    }
}
