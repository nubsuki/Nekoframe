using Nekoframe;
using System.Windows.Forms;

// WinExe has no console — catch crashes and show a MessageBox instead of dying silently
Application.ThreadException += (_, e) =>
    MessageBox.Show($"Nekoframe error:\n\n{e.Exception.Message}",
        "Nekoframe Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

AppDomain.CurrentDomain.UnhandledException += (_, e) =>
{
    var msg = e.ExceptionObject is Exception ex ? ex.Message : e.ExceptionObject?.ToString();
    MessageBox.Show($"Nekoframe crashed:\n\n{msg}",
        "Nekoframe Fatal Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
};

Application.EnableVisualStyles();
Application.SetCompatibleTextRenderingDefault(false);
Application.SetHighDpiMode(HighDpiMode.SystemAware);

// Prevent multiple instances and allow installer to detect running app
bool createdNew;
using var mutex = new Mutex(true, "NekoframeMutex", out createdNew);
if (!createdNew)
{
    MessageBox.Show("Nekoframe is already running.", "Nekoframe", MessageBoxButtons.OK, MessageBoxIcon.Information);
    return;
}

Application.Run(new TrayContext());
