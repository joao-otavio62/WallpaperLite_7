using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace WallpaperLite;

/// <summary>Hospeda o frontend React. Fechar a janela destrói o WebView2 (libera a RAM do Edge).</summary>
public sealed class MainWindow : Window
{
#if DEBUG
    const string DevUrl = "http://localhost:5173/";   // Vite dev server
#endif
    const string AppHost = "app.local", ThumbHost = "thumbs.local";

    readonly WebView2 _web = new();
    readonly AppServices _s;
    WebBridge? _bridge;
    bool _failShown;

    public MainWindow(AppServices s)
    {
        _s = s;
        Title = "WallpaperLite";
        Width = 980; Height = 680; MinWidth = 720; MinHeight = 480;
        Background = new SolidColorBrush(Color.FromRgb(0x18, 0x1A, 0x26));
        Content = _web;
        Loaded += async (_, _) => await InitAsync();
        Closed += (_, _) => { _bridge?.Dispose(); _web.Dispose(); };
    }

    static bool IsAllowed(string uri) =>
#if DEBUG
        uri.StartsWith(DevUrl, StringComparison.Ordinal) ||
#endif
        uri.StartsWith($"https://{AppHost}/", StringComparison.Ordinal) ||
        uri.StartsWith("data:text/html", StringComparison.Ordinal);

    async Task InitAsync()
    {
        var env = await CoreWebView2Environment.CreateAsync(null, AppPaths.WebViewData);
        await _web.EnsureCoreWebView2Async(env);
        var c = _web.CoreWebView2;

        c.Settings.AreDefaultContextMenusEnabled = false;
        c.Settings.AreBrowserAcceleratorKeysEnabled = false;
        c.Settings.IsStatusBarEnabled = false;
#if !DEBUG
        c.Settings.AreDevToolsEnabled = false;
#endif
        c.NavigationStarting += (_, e) => { if (!IsAllowed(e.Uri)) e.Cancel = true; };   // não sai do app
        c.NewWindowRequested += (_, e) => e.Handled = true;
        c.NavigationCompleted += (_, e) =>
        {
            if (e.IsSuccess || _failShown) return;
            _failShown = true;
            c.NavigateToString("<body style='font:16px Segoe UI;background:#181a26;color:#eee;padding:32px'><h2>Interface não encontrada</h2>"
                + "<p>Use o run.bat (ele gera o frontend e abre o programa). Em Debug, rode <code>npm run dev</code> na pasta frontend.</p></body>");
        };

        c.SetVirtualHostNameToFolderMapping(ThumbHost, AppPaths.Thumbs, CoreWebView2HostResourceAccessKind.Allow);
        _bridge = new WebBridge(c, _s, IsAllowed);

#if DEBUG
        c.Navigate(DevUrl);
#else
        c.SetVirtualHostNameToFolderMapping(AppHost, Path.Combine(AppContext.BaseDirectory, "wwwroot"), CoreWebView2HostResourceAccessKind.Allow);
        c.Navigate($"https://{AppHost}/index.html");
#endif
    }
}
