using System;
using System.IO;

namespace WallpaperLite;

public static class AppPaths
{
    public static readonly string Root =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WallpaperLite");

    public static string Cache => Dir("cache");        // imagens (baixadas ou importadas)
    public static string Thumbs => Dir("thumbs");      // miniaturas JPEG para a UI
    public static string WebViewData => Dir("webview");

    static string Dir(string name)
    {
        var p = Path.Combine(Root, name);
        Directory.CreateDirectory(p);
        return p;
    }
}
