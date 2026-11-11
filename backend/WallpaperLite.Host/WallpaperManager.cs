using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SkiaSharp;

namespace WallpaperLite;

public sealed record WallpaperItem(string Id, string Name, string Kind, string File);
sealed record LibraryFile(List<WallpaperItem> Items, string? ActiveId);

/// <summary>Biblioteca + aplicação de wallpapers (estático via SystemParametersInfo, GIF via janela no WorkerW).</summary>
public sealed class WallpaperManager : IDisposable
{
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    readonly string _libFile = Path.Combine(AppPaths.Root, "library.json");
    readonly string? _original = Native.GetCurrentWallpaper();
    AnimatedWallpaperWindow? _anim;

    public List<WallpaperItem> Items { get; private set; } = new();
    public string? ActiveId { get; private set; }
    public bool ManualPaused { get; private set; }
    public string? AutoReason { get; private set; }
    public bool AutoPaused => AutoReason != null;
    public bool HasAnimated => _anim != null;
    public event Action? Changed;

    public WallpaperManager()
    {
        try
        {
            if (!File.Exists(_libFile)) return;
            var lib = JsonSerializer.Deserialize<LibraryFile>(File.ReadAllText(_libFile), Json);
            if (lib != null) { Items = lib.Items ?? new(); ActiveId = lib.ActiveId; }
        }
        catch (JsonException) { }
    }

    // ---------- importação ----------
    /// <summary>Local: valida assinatura + tamanho e COPIA para o cache com nome GUID.</summary>
    public WallpaperItem AddLocal(string path)
    {
        var fi = new FileInfo(path);
        if (!fi.Exists || fi.Length > SafeDownloader.MaxBytes)
            throw new InvalidOperationException("Arquivo inválido ou maior que 20 MB.");
        var kind = FileSignature.Detect(path)
            ?? throw new InvalidOperationException("O arquivo não é um GIF, PNG ou JPG válido.");
        var dest = Path.Combine(AppPaths.Cache, Guid.NewGuid().ToString("N") + "." + kind);
        File.Copy(path, dest);
        return Register(dest, Path.GetFileNameWithoutExtension(path));
    }

    /// <summary>Arquivo já baixado e validado pelo SafeDownloader.</summary>
    public WallpaperItem AddDownloaded(string cachedFile, string displayName) => Register(cachedFile, displayName);

    WallpaperItem Register(string file, string name)
    {
        var id = Path.GetFileNameWithoutExtension(file);
        var kind = FileSignature.Detect(file) ?? throw new InvalidOperationException("Arquivo inválido.");
        try { MakeThumb(file, ThumbPath(id)); }
        catch (Exception)
        {
            File.Delete(file);
            throw new InvalidOperationException("Não foi possível ler a imagem.");
        }
        var item = new WallpaperItem(id, name.Length > 60 ? name[..60] : name, kind, Path.GetFileName(file));
        Items.Add(item);
        Save();
        Changed?.Invoke();
        return item;
    }

    static string ThumbPath(string id) => Path.Combine(AppPaths.Thumbs, id + ".jpg");

    static void MakeThumb(string src, string dst)
    {
        using var full = SKBitmap.Decode(src) ?? throw new InvalidOperationException();
        int w = 360, h = Math.Max(1, (int)((long)full.Height * w / full.Width));
        using var small = full.Resize(new SKImageInfo(w, h), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear))
            ?? throw new InvalidOperationException();
        using var img = SKImage.FromBitmap(small);
        using var data = img.Encode(SKEncodedImageFormat.Jpeg, 80);
        using var fs = File.Create(dst);
        data.SaveTo(fs);
    }

    // ---------- aplicar / parar ----------
    WallpaperItem Find(string id) => Items.FirstOrDefault(i => i.Id == id)
        ?? throw new InvalidOperationException("Wallpaper não encontrado.");

    public void Apply(string id)
    {
        var it = Find(id);
        var path = Path.Combine(AppPaths.Cache, Path.GetFileName(it.File)); // GetFileName: sem path traversal
        if (!File.Exists(path) || FileSignature.Detect(path) != it.Kind)
            throw new InvalidOperationException("O arquivo está ausente ou corrompido.");

        CloseAnimated();
        if (it.Kind == "gif")
        {
            _anim = new AnimatedWallpaperWindow(path);
            _anim.Show();
            _anim.SetPaused(ManualPaused || AutoPaused);
        }
        else Native.SetWallpaper(path);

        ActiveId = id;
        Save();
        Changed?.Invoke();
    }

    public void RestoreLast()
    {
        if (ActiveId == null) return;
        try { Apply(ActiveId); } catch (InvalidOperationException) { ActiveId = null; }
    }

    /// <summary>Reaplica o wallpaper ativo (usado ao trocar o modo de encaixe).</summary>
    public void Reapply()
    {
        if (ActiveId == null) return;
        try { Apply(ActiveId); } catch (InvalidOperationException) { }
    }

    /// <summary>Fecha a superfície do GIF durante o autoteste (para não atrapalhar a leitura de pixels).</summary>
    public void PauseForProbe(bool on) { if (on) CloseAnimated(); }

    public void Stop()
    {
        CloseAnimated();
        if (_original != null && File.Exists(_original)) Native.SetWallpaper(_original);
        ActiveId = null;
        Save();
        Changed?.Invoke();
    }

    public void Remove(string id)
    {
        var it = Find(id);
        if (ActiveId == id) Stop();
        Items.Remove(it);
        try { File.Delete(Path.Combine(AppPaths.Cache, Path.GetFileName(it.File))); File.Delete(ThumbPath(it.Id)); }
        catch (IOException) { }
        Save();
        Changed?.Invoke();
    }

    // ---------- pausa ----------
    public void ToggleManualPause() { ManualPaused = !ManualPaused; ApplyPause(); }
    public void SetAutoPaused(string? reason) { if (AutoReason != reason) { AutoReason = reason; ApplyPause(); } }
    void ApplyPause() { _anim?.SetPaused(ManualPaused || AutoPaused); Changed?.Invoke(); }

    void CloseAnimated() { _anim?.Close(); _anim = null; }

    void Save()
    {
        Directory.CreateDirectory(AppPaths.Root);
        File.WriteAllText(_libFile, JsonSerializer.Serialize(new LibraryFile(Items, ActiveId), Json));
    }

    public void Dispose() => CloseAnimated();
}
