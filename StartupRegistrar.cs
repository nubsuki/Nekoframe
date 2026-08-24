using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Nekoframe;

// Manages the Windows Scheduled Task that auto-starts Nekoframe at logon with
// highest privileges — avoids a UAC prompt on every boot via Task Scheduler.
public static class StartupRegistrar
{
    private const string TaskName = "Nekoframe System Stats";

    public static void EnsureScheduledTask()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        try
        {
            if (TaskExists()) return;

            var exePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exePath)) return;

            var xml = BuildTaskXml(exePath);
            var xmlPath = Path.Combine(Path.GetTempPath(), "nekoframe_task.xml");
            File.WriteAllText(xmlPath, xml, System.Text.Encoding.Unicode);

            try   { RunSchtasks($"/Create /TN \"{TaskName}\" /XML \"{xmlPath}\" /F"); }
            finally { if (File.Exists(xmlPath)) File.Delete(xmlPath); }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Startup] Could not register scheduled task: {ex.Message}");
        }
    }

    public static void RemoveScheduledTask()
    {
        try   { RunSchtasks($"/Delete /TN \"{TaskName}\" /F"); }
        catch (Exception ex) { Console.WriteLine($"[Startup] Could not remove task: {ex.Message}"); }
    }

    public static bool TaskExists()
    {
        try   { return RunSchtasks($"/Query /TN \"{TaskName}\" /FO LIST").ExitCode == 0; }
        catch { return false; }
    }

    private static (int ExitCode, string Output) RunSchtasks(string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName               = "schtasks.exe",
            Arguments              = arguments,
            UseShellExecute        = false,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            CreateNoWindow         = true,
        };

        using var proc = Process.Start(psi) ?? throw new Exception("Failed to start schtasks.exe");
        var output = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit();
        return (proc.ExitCode, output);
    }

    // RunLevel=HighestAvailable → runs as admin at logon without a UAC prompt each boot.
    // Delay PT5S → gives the desktop time to load before the app starts.
    private static string BuildTaskXml(string exePath)
    {
        var workingDir = Path.GetDirectoryName(exePath) ?? "";
        var now = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");

        return $"""
<?xml version="1.0" encoding="UTF-16"?>
<Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
  <RegistrationInfo>
    <Date>{now}</Date>
    <Author>Nekoframe</Author>
    <Description>Starts the Nekoframe system stats WebSocket server on user logon. Runs at ws://localhost:8181</Description>
  </RegistrationInfo>
  <Triggers>
    <LogonTrigger>
      <Enabled>true</Enabled>
      <Delay>PT5S</Delay>
    </LogonTrigger>
  </Triggers>
  <Principals>
    <Principal id="Author">
      <LogonType>InteractiveToken</LogonType>
      <RunLevel>HighestAvailable</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <AllowHardTerminate>false</AllowHardTerminate>
    <StartWhenAvailable>false</StartWhenAvailable>
    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
    <IdleSettings>
      <StopOnIdleEnd>false</StopOnIdleEnd>
      <RestartOnIdle>false</RestartOnIdle>
    </IdleSettings>
    <AllowStartOnDemand>true</AllowStartOnDemand>
    <Enabled>true</Enabled>
    <Hidden>false</Hidden>
    <RunOnlyIfIdle>false</RunOnlyIfIdle>
    <WakeToRun>false</WakeToRun>
    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
    <Priority>7</Priority>
  </Settings>
  <Actions Context="Author">
    <Exec>
      <Command>{System.Security.SecurityElement.Escape(exePath)}</Command>
      <WorkingDirectory>{System.Security.SecurityElement.Escape(workingDir)}</WorkingDirectory>
    </Exec>
  </Actions>
</Task>
""";
    }
}
