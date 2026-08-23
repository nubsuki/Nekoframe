namespace Nekoframe;

public static class Logger
{
    public static readonly string LogDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Nekoframe");

    public static readonly string LogPath = Path.Combine(LogDir, "nekoframe.log");

    private static readonly object _lock = new();
    private static bool _enabled = false;


    // Creates the log file and enables all future Write() calls.
    public static void Enable()
    {
        lock (_lock)
        {
            if (_enabled) return;
            try
            {
                Directory.CreateDirectory(LogDir);
                File.AppendAllText(LogPath,
                    $"[{DateTime.Now:HH:mm:ss}] [INFO ] Log opened — {DateTime.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}");
                _enabled = true;
            }
            catch { }
        }
    }

    // ── Write methods ──────────────────────────────────────────────

    public static void Info(string message)    => Write("INFO ", message);
    public static void Warn(string message)    => Write("WARN ", message);
    public static void Sensors(string message) => Write("SENS ", message);

    public static void Error(string message, Exception? ex = null)
        => Write("ERROR", ex != null ? $"{message}: {ex.Message}" : message);

    private static void Write(string level, string message)
    {
        if (!_enabled) return;
        var line = $"[{DateTime.Now:HH:mm:ss}] [{level}] {message}";
        lock (_lock)
        {
            try { File.AppendAllText(LogPath, line + Environment.NewLine); }
            catch { }
        }
    }
}
