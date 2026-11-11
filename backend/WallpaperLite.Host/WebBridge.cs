using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;

namespace WallpaperLite;

/// <summary>
/// Ponte React -> C#. Recebe { id, type, payload } e responde { id, ok, data | error }.
/// Eventos para a UI: { event: "state", data }.  Toda validação acontece aqui, nunca no React.
/// </summary>
public sealed class WebBridge : IDisposable
{
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    readonly CoreWebView2 _core;
    readonly AppServices _s;
    readonly Func<string, bool> _originOk;
    readonly CancellationTokenSource _cts = new();

    public WebBridge(CoreWebView2 core, AppServices s, Func<string, bool> originOk)
    {
        _core = core; _s = s; _originOk = originOk;
        _core.WebMessageReceived += OnMessage;
        _s.Wallpapers.Changed += PushState;
        _s.Desktop.Changed += PushState;
    }

    public void Dispose()
    {
        _cts.Cancel();
        _core.WebMessageReceived -= OnMessage;
        _s.Wallpapers.Changed -= PushState;
        _s.Desktop.Changed -= PushState;
    }

    void PushState() => Post(new { @event = "state", data = Snapshot() });

    object Snapshot()
    {
        var w = _s.Wallpapers;
        return new
        {
            items = w.Items.AsEnumerable().Reverse().Select(i => new
            {
                id = i.Id, name = i.Name, kind = i.Kind, thumb = $"https://thumbs.local/{i.Id}.jpg",
            }),
            activeId = w.ActiveId,
            paused = w.ManualPaused,
            autoPaused = w.AutoPaused,
            autoReason = w.AutoReason,
            clockEnabled = w.ClockEnabled,
            desktopIconsVisible = _s.Desktop.DesktopIconsVisible,
            taskbarVisible = _s.Desktop.TaskbarVisible,
        };
    }

    async void OnMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (!_originOk(e.Source)) return;
        int id = 0;
        try
        {
            using var doc = JsonDocument.Parse(e.WebMessageAsJson);
            var root = doc.RootElement;
            id = root.GetProperty("id").GetInt32();
            var w = _s.Wallpapers;
            object? data = null;

            switch (root.GetProperty("type").GetString())
            {
                case "getState": data = Snapshot(); break;
                case "importLocal": await ImportLocal(); break;
                case "importUrl": await ImportUrl(Arg(root, "url")); break;
                case "apply": w.Apply(Arg(root, "id")); break;
                case "remove": w.Remove(Arg(root, "id")); break;
                case "stop": w.Stop(); break;
                case "togglePause": w.ToggleManualPause(); break;
                case "toggleDesktopIcons": _s.Desktop.ToggleDesktopIcons(); break;
                case "toggleTaskbar": _s.Desktop.ToggleTaskbar(); break;
                case "setClockEnabled": w.SetClockEnabled(BoolArg(root, "enabled")); break;
                default: throw new InvalidOperationException("Comando desconhecido.");
            }
            Post(new { id, ok = true, data });
        }
        catch (Exception ex)
        {
            // Só mensagens "nossas" chegam à UI; detalhes internos (caminhos etc.) não vazam.
            var msg = ex switch
            {
                InvalidOperationException => ex.Message,
                HttpRequestException or TaskCanceledException => "Não foi possível baixar o link.",
                _ => "Erro inesperado.",
            };
            Post(new { id, ok = false, error = msg });
        }
    }

    static string Arg(JsonElement root, string name) =>
        root.GetProperty("payload").GetProperty(name).GetString() ?? "";

    static bool BoolArg(JsonElement root, string name) =>
        root.GetProperty("payload").GetProperty(name).GetBoolean();

    Task ImportLocal()
    {
        var dlg = new OpenFileDialog { Filter = "Imagens (GIF, PNG, JPG)|*.gif;*.png;*.jpg;*.jpeg", Multiselect = false };
        if (dlg.ShowDialog() == true) _s.Wallpapers.AddLocal(dlg.FileName);
        return Task.CompletedTask;
    }

    async Task ImportUrl(string url)
    {
        var uri = SafeDownloader.Validate(url);
        var file = await _s.Downloader.DownloadAsync(url, AppPaths.Cache, _cts.Token);
        _s.Wallpapers.AddDownloaded(file, uri.Host);
    }

    void Post(object message)
    {
        try { _core.PostWebMessageAsJson(JsonSerializer.Serialize(message, Json)); }
        catch (Exception) { /* janela já foi fechada */ }
    }
}
