using Nekoframe;
using System.Windows.Forms;

// Global exception handlers
Application.ThreadException += (_, e) =>
{
    Logger.Error("Unhandled UI thread exception", e.Exception);
    MessageBox.Show(
        $"Nekoframe encountered an error:\n\n{e.Exception.Message}\n\nCheck the log for details:\n{Logger.LogPath}",
        "Nekoframe Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
};

AppDomain.CurrentDomain.UnhandledException += (_, e) =>
{
    var ex = e.ExceptionObject as Exception;
    Logger.Error("Unhandled domain exception", ex);
    MessageBox.Show(
        $"Nekoframe crashed:\n\n{ex?.Message ?? e.ExceptionObject?.ToString()}\n\nCheck the log:\n{Logger.LogPath}",
        "Nekoframe Fatal Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
};

Application.EnableVisualStyles();
Application.SetCompatibleTextRenderingDefault(false);
Application.SetHighDpiMode(HighDpiMode.SystemAware);

Application.Run(new TrayContext());
