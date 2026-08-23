namespace Nekoframe;

public static class Logger
{
    public static readonly string LogDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Nekoframe");

    public static readonly string ReportPath = Path.Combine(LogDir, "nekoframe_report.txt");

    public static void WriteReportAndOpen(string content)
    {
        try
        {
            Directory.CreateDirectory(LogDir);
            File.WriteAllText(ReportPath, content);
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(ReportPath) { UseShellExecute = true });
        }
        catch { }
    }
}
