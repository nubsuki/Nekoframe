using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace Nekoframe;


// System tray application context.

public class TrayContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly SynchronizationContext? _synchronizationContext;
    private StatsFetcher? _fetcher;
    private WebSocketServerManager? _wsServer;

    public TrayContext()
    {
        Logger.Info("TrayContext starting...");

        // Capture UI SynchronizationContext so background threads can post notifications
        _synchronizationContext = SynchronizationContext.Current;

        // Show tray icon FIRST
        _trayIcon = new NotifyIcon
        {
            Icon    = LoadIcon(),
            Text    = "Nekoframe — Starting…",
            Visible = true,
            ContextMenuStrip = BuildContextMenu(),
        };
        _trayIcon.DoubleClick += (_, _) => OpenDashboard();

        Logger.Info("Tray icon visible.");

        // Register startup task
        StartupRegistrar.EnsureScheduledTask();

        // Init sensors + WebSocket on background thread
        Task.Run(InitializeServices);
    }

    // ── Initialization ─────────────────────────────────────────────

    private void InitializeServices()
    {
        try
        {
            Logger.Info("Initializing hardware sensors (background thread)...");
            _fetcher = new StatsFetcher();
            Logger.Info("Sensors initialized.");
        }
        catch (Exception ex)
        {
            Logger.Error("Hardware sensor initialization failed", ex);
            ShowBalloon("Nekoframe — Sensor Warning",
                $"Some sensors could not be read.\n{ex.Message}\nWebSocket will still start.",
                ToolTipIcon.Warning);
            // Continue anyway
            _fetcher = new StatsFetcher();
        }

        try
        {
            Logger.Info("Starting WebSocket server on ws://127.0.0.1:8181...");
            _wsServer = new WebSocketServerManager("ws://0.0.0.0:8181");
            _wsServer.Start(_fetcher!, broadcastIntervalMs: 1000);
            Logger.Info("WebSocket server running on ws://localhost:8181");

            // Update tray tooltip and show ready balloon
            _trayIcon.Text = "Nekoframe — ws://localhost:8181";
            ShowBalloon("Nekoframe Running",
                "System stats ready at ws://localhost:8181\nDouble-click to open dashboard.",
                ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            Logger.Error("WebSocket server failed to start", ex);
            _trayIcon.Text = "Nekoframe — WebSocket Error";
            ShowBalloon("Nekoframe — WebSocket Error",
                $"Failed to bind ws://localhost:8181\n{ex.Message}\nCheck the log for details.",
                ToolTipIcon.Error);
        }
    }

    // ── Icon ───────────────────────────────────────────────────────

    private static Icon LoadIcon()
    {
        // Try icon.ico first, then fall back to system icon
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

                // Convert PNG to Icon for non-.ico files
                using var bmp = new Bitmap(path);
                return Icon.FromHandle(bmp.GetHicon());
            }
            catch (Exception ex)
            {
                Logger.Warn($"Could not load icon from {path}: {ex.Message}");
            }
        }

        Logger.Warn("No icon found in Assets/ — using default system icon.");
        return SystemIcons.Application;
    }

    // ── Context Menu ───────────────────────────────────────────────

    private ContextMenuStrip BuildContextMenu()
    {
        var menu = new ContextMenuStrip();

        menu.Items.AddRange(new ToolStripItem[]
        {
            new ToolStripMenuItem("🐱 Nekoframe")          { Enabled = false },
            new ToolStripMenuItem("ws://localhost:8181")    { Enabled = false, Font = new Font("Segoe UI", 7.5f) },
            new ToolStripSeparator(),
            new ToolStripMenuItem("📊 Open Dashboard", null, (_, _) => OpenDashboard()),
            new ToolStripMenuItem("📄 View Log",       null, (_, _) => OpenLog()),
            new ToolStripSeparator(),
            new ToolStripMenuItem("✖ Exit",            null, (_, _) => ExitApp()),
        });

        return menu;
    }

    // ── Actions ────────────────────────────────────────────────────

    private static void OpenDashboard()
    {
        // Look next to the exe and also in the project root (for dev runs)
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "test_websocket.html"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "test_websocket.html"),
        };

        foreach (var p in candidates)
        {
            var full = Path.GetFullPath(p);
            if (!File.Exists(full)) continue;
            Process.Start(new ProcessStartInfo(full) { UseShellExecute = true });
            Logger.Info($"Opened dashboard: {full}");
            return;
        }

        Logger.Warn("test_websocket.html not found.");
        MessageBox.Show("Dashboard file not found.\nExpected: test_websocket.html next to Nekoframe.exe",
            "Nekoframe", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private static void OpenLog()
    {
        Logger.Enable(); // Creates the log file if it doesn't exist yet
        Process.Start(new ProcessStartInfo(Logger.LogPath) { UseShellExecute = true });
    }

    private void ExitApp()
    {
        Logger.Info("User requested exit.");
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

        // Post to UI thread if called from background thread
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
