using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace WallpaperLite;

public sealed record ProbeResult(int Mode, int Renderer, int Seen, int Valid);

/// <summary>
/// Autoteste de encaixe: em cada modo e com cada renderizador (WPF, WPF software, GDI puro) cria uma superfície
/// MAGENTA e lê a cor REAL dos pixels da tela. Onde o magenta aparece, o wallpaper animado também aparece.
/// Pontos cobertos por outras janelas (navegador, a própria janela do app...) são ignorados.
/// </summary>
public static class AttachProbe
{
    const uint WS_CHILD = 0x40000000, WS_VISIBLE = 0x10000000, WS_CLIPSIBLINGS = 0x04000000, SS_OWNERDRAW = 0x0D;
    const uint GA_ROOT = 2;

    // Testa apenas encaixes atrás dos ícones; renderizador mais simples primeiro.
    static readonly int[] ModeOrder = { 2, 4, 1, 3, 5 };

    public static async Task<List<ProbeResult>> RunAsync()
    {
        Log.Write("=== Autoteste de encaixe (superfície magenta + leitura de pixels) ===");
        var all = new List<ProbeResult>();
        foreach (var mode in ModeOrder)
            for (var r = 0; r < Native.Renderers.Length; r++)
            {
                ProbeResult res;
                try { res = await ProbeOne(mode, r); }
                catch (Exception ex) { Log.Write($"Teste falhou modo={mode} renderizador={r}: {ex.Message}"); res = new ProbeResult(mode, r, 0, 0); }
                all.Add(res);
                Log.Write($"Teste: modo=\"{Native.AttachModes[mode]}\" renderizador={Native.Renderers[r]} magenta={res.Seen}/{res.Valid} pontos livres");
            }
        return all;
    }

    /// <summary>Melhor combinação: primeiro modo/renderizador em que o magenta apareceu.</summary>
    public static ProbeResult? Best(IEnumerable<ProbeResult> all) => all.FirstOrDefault(p => p.Seen > 0);

    static async Task<ProbeResult> ProbeOne(int mode, int renderer)
    {
        Native.GetDesktopTarget(mode, false, out var parent, out var after, out var x, out var y, out var w, out var h, out _);
        IntPtr gdi = IntPtr.Zero;
        HwndSource? src = null;
        try
        {
            if (renderer == 2)
            {
                gdi = Native.CreateWindowEx((uint)(Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE), "STATIC", null,
                    Native.ChildOrPopupStyle(parent) | WS_CLIPSIBLINGS | SS_OWNERDRAW, x, y, w, h, parent, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                if (gdi == IntPtr.Zero) return new ProbeResult(mode, renderer, 0, 0);
                Native.Place(gdi, parent, after, x, y, w, h);
                var dc = Native.GetDC(gdi);
                var brush = Native.CreateSolidBrush(0x00FF00FF);          // COLORREF 0x00BBGGRR: magenta
                var rc = new RECT { Left = 0, Top = 0, Right = w, Bottom = h };
                Native.FillRect(dc, ref rc, brush);
                Native.DeleteObject(brush);
                Native.ReleaseDC(gdi, dc);
                Native.ValidateRect(gdi, IntPtr.Zero);
            }
            else
            {
                var p = new HwndSourceParameters("WallpaperLiteProbe")
                {
                    ParentWindow = parent,
                    WindowStyle = unchecked((int)Native.ChildOrPopupStyle(parent)),
                    ExtendedWindowStyle = (int)(Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE),
                    PositionX = x, PositionY = y, Width = w, Height = h,
                };
                src = new HwndSource(p) { RootVisual = new Border { Background = new SolidColorBrush(Colors.Magenta) } };
                if (renderer == 1 && src.CompositionTarget != null) src.CompositionTarget.RenderMode = RenderMode.SoftwareOnly;
                Native.Place(src.Handle, parent, after, x, y, w, h);
            }

            await Task.Delay(450);                                         // dá tempo ao DWM/WPF de pintar
            var (seen, valid) = Sample(gdi != IntPtr.Zero ? gdi : src!.Handle);
            return new ProbeResult(mode, renderer, seen, valid);
        }
        finally
        {
            src?.Dispose();
            if (gdi != IntPtr.Zero) Native.DestroyWindow(gdi);
            await Task.Delay(80);
        }
    }

    /// <summary>Lê 9 pontos da tela principal. Devolve (quantos são magenta, quantos estavam livres para teste).</summary>
    static (int seen, int valid) Sample(IntPtr own)
    {
        var b = System.Windows.Forms.Screen.PrimaryScreen!.Bounds;
        int seen = 0, valid = 0;
        using var bmp = new System.Drawing.Bitmap(1, 1);
        using var g = System.Drawing.Graphics.FromImage(bmp);
        foreach (var fy in new[] { 0.12, 0.5, 0.88 })
            foreach (var fx in new[] { 0.12, 0.5, 0.88 })
            {
                var px = b.Left + (int)(b.Width * fx);
                var py = b.Top + (int)(b.Height * fy);
                var hit = Native.WindowFromPoint(new POINT { X = px, Y = py });
                if (hit == IntPtr.Zero) continue;
                var cls = Native.ClassName(Native.GetAncestor(hit, GA_ROOT));
                if (cls != "Progman" && cls != "WorkerW" && Native.GetAncestor(hit, GA_ROOT) != own) continue;       // outra janela cobre o ponto: não conta
                valid++;
                g.CopyFromScreen(px, py, 0, 0, new System.Drawing.Size(1, 1));
                var c = bmp.GetPixel(0, 0);
                if (c.R > 200 && c.G < 80 && c.B > 200) seen++;
            }
        return (seen, valid);
    }

    /// <summary>Texto curto para o usuário, com o diagnóstico em português.</summary>
    public static string Explain(List<ProbeResult> all, ProbeResult? best)
    {
        if (all.All(p => p.Valid == 0))
            return "Teste inconclusivo: todas as áreas da tela estavam cobertas por janelas. Minimize tudo (Win+D) e rode de novo.";
        var wpf = all.Any(p => p.Renderer != 2 && p.Seen > 0);
        var gdi = all.Any(p => p.Renderer == 2 && p.Seen > 0);
        string diag = !wpf && !gdi
            ? "Nenhum encaixe compatível exibiu o GIF atrás dos ícones neste Windows. O app manteve um modo seguro para que os ícones continuem acessíveis."
            : !wpf && gdi
                ? "O Windows aceita o encaixe, mas o WPF não pinta nessa janela. O GDI puro funciona: o app vai usá-lo."
                : "O WPF pinta normalmente nos modos marcados abaixo.";
        var lines = all.Where(p => p.Seen > 0).Select(p => $"  ✔ {Native.AttachModes[p.Mode]}  [{Native.Renderers[p.Renderer]}]");
        var ok = string.Join("\n", lines);
        var pick = best == null ? "\nNada foi alterado." : $"\n\nEscolhido: {Native.AttachModes[best.Mode]} com {Native.Renderers[best.Renderer]}.";
        return diag + (ok.Length > 0 ? "\n\nFuncionam:\n" + ok : "") + pick + "\nDetalhes no log de diagnóstico.";
    }
}
