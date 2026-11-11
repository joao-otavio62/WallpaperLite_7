using System;
using System.IO;
using System.Threading;
using System.Windows;

namespace WallpaperLite;

public partial class App : Application
{
    Mutex? _mutex;
    AppServices? _services;
    TrayIcon? _tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(true, "WallpaperLite.SingleInstance", out var first);
        if (!first)
        {
            MessageBox.Show("O WallpaperLite já está em execução. Procure o ícone na bandeja (perto do relógio), " +
                            "clique com o botão direito e escolha \"Sair\"; depois abra de novo.", "WallpaperLite");
            Shutdown();
            return;
        }
        Log.Write($"=== Início | build de {File.GetLastWriteTime(typeof(App).Assembly.Location):dd/MM HH:mm:ss}");

        base.OnStartup(e);
        _services = new AppServices();
        _services.Wallpapers.RestoreLast();
        _tray = new TrayIcon(_services);
        if (Array.IndexOf(e.Args, "--tray") < 0) _tray.ShowMainWindow();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _services?.Dispose();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
