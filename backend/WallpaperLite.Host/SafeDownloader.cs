using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace WallpaperLite;

/// <summary>Download seguro: só https, anti-SSRF, limite de tamanho, nome gerado por nós, validação de assinatura.</summary>
public sealed class SafeDownloader : IDisposable
{
    public const long MaxBytes = 20L * 1024 * 1024;
    const int MaxRedirects = 3;
    readonly HttpClient _http;

    public SafeDownloader()
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,   // redirecionamentos são seguidos manualmente (e revalidados)
            UseProxy = false,            // garante que o ConnectCallback veja o destino real
            ConnectTimeout = TimeSpan.FromSeconds(10),
            ConnectCallback = ConnectAsync,
        };
        _http = new HttpClient(handler, disposeHandler: true) { Timeout = TimeSpan.FromSeconds(60) };
    }

    public static Uri Validate(string? s)
    {
        if (!Uri.TryCreate(s?.Trim(), UriKind.Absolute, out var u) || u.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("Somente links https:// são aceitos.");
        if (!string.IsNullOrEmpty(u.UserInfo))
            throw new InvalidOperationException("Links com usuário/senha não são aceitos.");
        return u;
    }

    // Resolve o DNS aqui e recusa IPs internos (evita SSRF e DNS rebinding).
    static async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext ctx, CancellationToken ct)
    {
        var all = await Dns.GetHostAddressesAsync(ctx.DnsEndPoint.Host, ct);
        var safe = Array.FindAll(all, a => !IsBlocked(a));
        if (safe.Length == 0) throw new HttpRequestException("Destino bloqueado.");

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try { await socket.ConnectAsync(safe, ctx.DnsEndPoint.Port, ct); return new NetworkStream(socket, ownsSocket: true); }
        catch { socket.Dispose(); throw; }
    }

    static bool IsBlocked(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        if (IPAddress.IsLoopback(ip)) return true;
        var b = ip.GetAddressBytes();
        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
            return ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6Multicast || (b[0] & 0xFE) == 0xFC; // fc00::/7
        return b[0] == 0 || b[0] == 10 || b[0] == 127 || b[0] >= 224
            || (b[0] == 169 && b[1] == 254) || (b[0] == 172 && b[1] is >= 16 and <= 31)
            || (b[0] == 192 && b[1] == 168) || (b[0] == 100 && b[1] is >= 64 and <= 127);
    }

    async Task<HttpResponseMessage> SendAsync(HttpMethod method, Uri uri, CancellationToken ct)
    {
        for (var hop = 0; hop <= MaxRedirects; hop++)
        {
            using var req = new HttpRequestMessage(method, uri);
            var res = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            if ((int)res.StatusCode is >= 300 and < 400 && res.Headers.Location is { } loc)
            {
                res.Dispose();
                uri = Validate(new Uri(uri, loc).ToString());
                continue;
            }
            return res;
        }
        throw new InvalidOperationException("Redirecionamentos demais.");
    }

    /// <returns>Caminho do arquivo salvo em <paramref name="cacheDir"/> com nome GUID.</returns>
    public async Task<string> DownloadAsync(string url, string cacheDir, CancellationToken ct)
    {
        var uri = Validate(url);
        var tmp = Path.Combine(cacheDir, Guid.NewGuid().ToString("N") + ".part");
        try
        {
            // 1) HEAD: recusa cedo se o Content-Length já passa do limite (alguns servidores não suportam HEAD; seguimos).
            try
            {
                using var head = await SendAsync(HttpMethod.Head, uri, ct);
                if (head.IsSuccessStatusCode && head.Content.Headers.ContentLength > MaxBytes)
                    throw new InvalidOperationException("O arquivo passa do limite de 20 MB.");
            }
            catch (HttpRequestException) { }

            // 2) GET em streaming, com limite rígido de bytes (o servidor pode mentir no Content-Length).
            using (var res = await SendAsync(HttpMethod.Get, uri, ct))
            {
                res.EnsureSuccessStatusCode();
                if (res.Content.Headers.ContentLength > MaxBytes)
                    throw new InvalidOperationException("O arquivo passa do limite de 20 MB.");

                await using var src = await res.Content.ReadAsStreamAsync(ct);
                await using var dst = new FileStream(tmp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
                var buf = new byte[81920];
                long total = 0;
                int n;
                while ((n = await src.ReadAsync(buf, ct)) > 0)
                {
                    total += n;
                    if (total > MaxBytes) throw new InvalidOperationException("O arquivo passa do limite de 20 MB.");
                    await dst.WriteAsync(buf.AsMemory(0, n), ct);
                }
            }

            // 3) Assinatura real + nome próprio (nunca o nome vindo da URL -> sem path traversal).
            var kind = FileSignature.Detect(tmp)
                ?? throw new InvalidOperationException("O arquivo não é um GIF, PNG ou JPG válido.");
            var final = Path.Combine(cacheDir, Guid.NewGuid().ToString("N") + "." + kind);
            File.Move(tmp, final);
            return final;
        }
        catch
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch (IOException) { }
            throw;
        }
    }

    public void Dispose() => _http.Dispose();
}
