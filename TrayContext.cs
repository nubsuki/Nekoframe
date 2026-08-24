using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace Nekoframe;

public class TrayContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly SynchronizationContext? _synchronizationContext;
    private StatsFetcher? _fetcher;
    private WebSocketServerManager? _wsServer;
    private ToolStripMenuItem _startupItem = null!;

    public TrayContext()
    {
        // Captured here so background threads can post balloon notifications to the UI thread
        _synchronizationContext = SynchronizationContext.Current;

        // Show the tray icon before sensor init — LHM can take a few seconds to open
        _trayIcon = new NotifyIcon
        {
            Icon             = LoadIcon(),
            Text             = "Nekoframe — Starting…",
            Visible          = true,
            ContextMenuStrip = BuildContextMenu(),
        };

        Task.Run(InitializeServices);
    }

    private void InitializeServices()
    {
        try
        {
            _fetcher = new StatsFetcher();
        }
        catch (Exception ex)
        {
            ShowBalloon("Nekoframe — Sensor Warning",
                $"Some sensors could not be read: {ex.Message}",
                ToolTipIcon.Warning);
            try { _fetcher = new StatsFetcher(); } catch { }
        }

        try
        {
            _wsServer = new WebSocketServerManager("ws://0.0.0.0:8181");
            _wsServer.Start(_fetcher!, broadcastIntervalMs: 1000);
            _trayIcon.Text = "Nekoframe — ws://localhost:8181";
            ShowBalloon("Nekoframe Running",
                "System stats broadcasting at ws://localhost:8181",
                ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            _trayIcon.Text = "Nekoframe — WebSocket Error";
            ShowBalloon("Nekoframe — WebSocket Error",
                $"Failed to start WebSocket server: {ex.Message}",
                ToolTipIcon.Error);
        }
    }

    private static Icon LoadIcon()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Assets", "icon.ico"),
            Path.Combine(AppContext.BaseDirectory, "Assets", "32x32.png"),
        };

        foreach (var path in candidates)
        {
            if (!File.Exists(path)) continue;
            try
            {
                if (path.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
                    return new Icon(path);

                using var bmp = new Bitmap(path);
                return Icon.FromHandle(bmp.GetHicon());
            }
            catch { }
        }

        return SystemIcons.Application;
    }

    private ContextMenuStrip BuildContextMenu()
    {
        _startupItem = new ToolStripMenuItem("🚀 Start with Windows", null, (_, _) => ToggleStartup())
        {
            Checked = StartupRegistrar.TaskExists(),
        };

        var menu = new ContextMenuStrip();
        menu.Items.AddRange(new ToolStripItem[]
        {
            new ToolStripMenuItem("🐱 Nekoframe")       { Enabled = false },
            new ToolStripMenuItem("ws://localhost:8181") { Enabled = false, Font = new Font("Segoe UI", 7.5f) },
            new ToolStripSeparator(),
            new ToolStripMenuItem("📄 View Report",  null, (_, _) => OpenReport()),
            _startupItem,
            new ToolStripSeparator(),
            new ToolStripMenuItem("✖ Exit",          null, (_, _) => ExitApp()),
        });

        return menu;
    }

    private void ToggleStartup()
    {
        try
        {
            if (StartupRegistrar.TaskExists())
            {
                StartupRegistrar.RemoveScheduledTask();
                _startupItem.Checked = false;
            }
            else
            {
                StartupRegistrar.EnsureScheduledTask();
                _startupItem.Checked = StartupRegistrar.TaskExists();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not update startup setting:\n{ex.Message}",
                "Nekoframe", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void OpenReport()
    {
        if (_fetcher == null)
        {
            MessageBox.Show("Sensors are still initializing. Try again in a moment.",
                "Nekoframe", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        Logger.WriteReportAndOpen(_fetcher.GenerateReport());
    }

    private void ExitApp()
    {
        _trayIcon.Visible = false;
        _wsServer?.Dispose();
        _fetcher?.Dispose();
        Application.Exit();
    }

    private void ShowBalloon(string title, string text, ToolTipIcon icon)
    {
        void Show()
        {
            _trayIcon.BalloonTipTitle = title;
            _trayIcon.BalloonTipText  = text;
            _trayIcon.BalloonTipIcon  = icon;
            _trayIcon.ShowBalloonTip(5000);
        }

        if (_synchronizationContext != null && SynchronizationContext.Current != _synchronizationContext)
            _synchronizationContext.Post(_ => Show(), null);
        else
            Show();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _wsServer?.Dispose();
            _fetcher?.Dispose();
        }
        base.Dispose(disposing);
    }
}
