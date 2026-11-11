using System;
using System.IO;

namespace WallpaperLite;

/// <summary>Valida o tipo real do arquivo pelos "magic numbers" (nunca pela extensão).</summary>
public static class FileSignature
{
    static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    /// <returns>"gif", "png", "jpg" ou null se não for uma imagem permitida.</returns>
    public static string? Detect(string path)
    {
        Span<byte> head = stackalloc byte[8];
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Detect(head[..fs.Read(head)]);
    }

    public static string? Detect(ReadOnlySpan<byte> b)
    {
        // GIF87a / GIF89a  -> 47 49 46 38 (37|39) 61
        if (b.Length >= 6 && b[0] == 0x47 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x38 && (b[4] == 0x37 || b[4] == 0x39) && b[5] == 0x61) return "gif";
        if (b.Length >= 8 && b[..8].SequenceEqual(Png)) return "png";
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return "jpg";
        return null;
    }
}
