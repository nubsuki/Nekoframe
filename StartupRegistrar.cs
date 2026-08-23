using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Nekoframe;

// Registers Nekoframe as a Windows Scheduled Task at user logon with highest privileges.
// This allows the app to auto-start with admin rights on every boot without showing a UAC prompt.
public static class StartupRegistrar
{
    private const string TaskName = "Nekoframe System Stats";

    // Checks if the Scheduled Task exists. If not, creates it.

    public static void EnsureScheduledTask()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Console.WriteLine("[Startup] Not Windows — skipping task registration.");
            return;
        }

        try
        {
            if (TaskExists())
            {
                Console.WriteLine("[Startup] Scheduled task already registered. ✓");
                return;
            }

            Console.WriteLine("[Startup] Registering startup task for the first time...");

            var exePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exePath))
            {
                Console.WriteLine("[Startup] Could not determine executable path. Skipping task registration.");
                return;
            }

            // Build the XML task definition
            var xml = BuildTaskXml(exePath);
            var xmlPath = Path.Combine(Path.GetTempPath(), "nekoframe_task.xml");
            File.WriteAllText(xmlPath, xml, System.Text.Encoding.Unicode);

            try
            {
                // schtasks /Create /TN "TaskName" /XML "path.xml" /F
                RunSchtasks($"/Create /TN \"{TaskName}\" /XML \"{xmlPath}\" /F");
                Console.WriteLine($"[Startup] Task '{TaskName}' registered. Nekoframe will auto-start silently on next boot. ✓");
            }
            finally
            {
                // Clean up temp XML
                if (File.Exists(xmlPath))
                    File.Delete(xmlPath);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Startup] Warning: Could not register scheduled task: {ex.Message}");
            Console.WriteLine("[Startup] You can still use Nekoframe manually. Run as Administrator to register the startup task.");
        }
    }


    // Removes the scheduled task
    public static void RemoveScheduledTask()
    {
        try
        {
            RunSchtasks($"/Delete /TN \"{TaskName}\" /F");
            Console.WriteLine($"[Startup] Task '{TaskName}' removed.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Startup] Could not remove task: {ex.Message}");
        }
    }

    private static bool TaskExists()
    {
        try
        {
            var result = RunSchtasks($"/Query /TN \"{TaskName}\" /FO LIST");
            return result.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static (int ExitCode, string Output) RunSchtasks(string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "schtasks.exe",
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        using var proc = Process.Start(psi) ?? throw new Exception("Failed to start schtasks.exe");
        var output = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit();
        return (proc.ExitCode, output);
    }

    // Generates the task XML definition.
    // RunLevel=HighestAvailable ensures it runs as admin without a UAC prompt (via Task Scheduler).
    
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
