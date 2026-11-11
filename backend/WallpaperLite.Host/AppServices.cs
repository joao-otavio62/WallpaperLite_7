using System;

namespace WallpaperLite;

/// <summary>Serviços que vivem enquanto o app estiver na bandeja (independentes da janela/WebView).</summary>
public sealed class AppServices : IDisposable
{
    public WallpaperManager Wallpapers { get; } = new();
    public SafeDownloader Downloader { get; } = new();
    public DesktopControls Desktop { get; } = new();

    public void Dispose()
    {
        Desktop.Dispose();
        Wallpapers.Dispose();
        Downloader.Dispose();
    }
}
