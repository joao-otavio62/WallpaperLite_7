using System;
using System.IO;

namespace WallpaperLite;

/// <summary>Log simples em %LOCALAPPDATA%\WallpaperLite\log.txt (ajuda a diagnosticar o encaixe no desktop).</summary>
public static class Log
{
    static readonly object Gate = new();
    public static string FilePath => Path.Combine(AppPaths.Root, "log.txt");

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(AppPaths.Root);
                File.AppendAllText(FilePath, $"{DateTime.Now:HH:mm:ss} {message}{Environment.NewLine}");
            }
        }
        catch (IOException) { }
    }
}
