using System;
using System.Diagnostics;
using System.IO;
using System.Drawing;
using System.Windows.Forms;

namespace WallpaperLite;

/// <summary>Modo bandeja: o app segue vivo só com este ícone; a janela (e o WebView2) só existem quando abertas.</summary>
public sealed class TrayIcon : IDisposable
{
    readonly NotifyIcon _icon;
    readonly AppServices _s;
    MainWindow? _window;

    public TrayIcon(AppServices s)
    {
        _s = s;
        var menu = new ContextMenuStrip();
        menu.Items.Add("Abrir", null, (_, _) => ShowMainWindow());
        menu.Items.Add("Pausar / retomar", null, (_, _) => s.Wallpapers.ToggleManualPause());
        menu.Items.Add("Restaurar wallpaper original", null, (_, _) => s.Wallpapers.Stop());
        var modes = new ToolStripMenuItem("Modo de encaixe do GIF");
        for (var i = 0; i < Native.AttachModes.Length; i++)
        {
            var m = i;
            var item = new ToolStripMenuItem(Native.AttachModes[i]) { Checked = i == Native.AttachMode };
            item.Click += (_, _) =>
            {
                Native.AttachMode = m;
                foreach (ToolStripMenuItem o in modes.DropDownItems) o.Checked = o == item;
                s.Wallpapers.Reapply();
            };
            modes.DropDownItems.Add(item);
        }
        menu.Items.Add(modes);

        var rends = new ToolStripMenuItem("Renderizador do GIF");
        for (var i = 0; i < Native.Renderers.Length; i++)
        {
            var r = i;
            var item = new ToolStripMenuItem(Native.Renderers[i]) { Checked = i == Native.Renderer };
            item.Click += (_, _) =>
            {
                Native.Renderer = r;
                foreach (ToolStripMenuItem o in rends.DropDownItems) o.Checked = o == item;
                s.Wallpapers.Reapply();
            };
            rends.DropDownItems.Add(item);
        }
        menu.Items.Add(rends);
        menu.Items.Add("Testar encaixes (autoteste magenta)", null, async (_, _) => await RunProbe(modes, rends));
        menu.Items.Add("Abrir log de diagnóstico", null, (_, _) =>
        {
            if (File.Exists(Log.FilePath)) Process.Start(new ProcessStartInfo(Log.FilePath) { UseShellExecute = true });
        });
        menu.Items.Add("-");
        menu.Items.Add("Sair", null, (_, _) => System.Windows.Application.Current.Shutdown());

        _icon = new NotifyIcon { Icon = SystemIcons.Application, Text = "WallpaperLite", Visible = true, ContextMenuStrip = menu };
        _icon.DoubleClick += (_, _) => ShowMainWindow();
    }

    async System.Threading.Tasks.Task RunProbe(ToolStripMenuItem modes, ToolStripMenuItem rends)
    {
        try
        {
            _s.Wallpapers.PauseForProbe(true);
            var all = await AttachProbe.RunAsync();
            var best = AttachProbe.Best(all);
            if (best != null)
            {
                Native.AttachMode = best.Mode;
                Native.Renderer = best.Renderer;
                for (var i = 0; i < modes.DropDownItems.Count; i++) ((ToolStripMenuItem)modes.DropDownItems[i]).Checked = i == best.Mode;
                for (var i = 0; i < rends.DropDownItems.Count; i++) ((ToolStripMenuItem)rends.DropDownItems[i]).Checked = i == best.Renderer;
            }
            _s.Wallpapers.PauseForProbe(false);
            _s.Wallpapers.Reapply();
            System.Windows.MessageBox.Show(AttachProbe.Explain(all, best), "WallpaperLite - autoteste");
        }
        catch (Exception ex) { Log.Write("Autoteste: " + ex); _s.Wallpapers.PauseForProbe(false); }
    }

    public void ShowMainWindow()
    {
        if (_window != null) { _window.Activate(); return; }
        _window = new MainWindow(_s);
        _window.Closed += (_, _) =>
        {
            _window = null;                       // WebView2 já foi descartado no Closed da janela
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        };
        _window.Show();
    }

    public void Dispose() { _icon.Visible = false; _icon.Dispose(); }
}
