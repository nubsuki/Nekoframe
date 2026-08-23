using LibreHardwareMonitor.Hardware;
using Nekoframe.Models;
using System.Management;
using System.Net.NetworkInformation;

namespace Nekoframe;


// Wraps LibreHardwareMonitorLib to collect system hardware stats.

public class StatsFetcher : IDisposable
{
    private readonly Computer _computer;
    private bool _disposed;

    // Network fallback
    private long _lastNetBytesSent;
    private long _lastNetBytesReceived;
    private DateTime _lastNetSample = DateTime.MinValue;

    public StatsFetcher()
    {
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true,
            IsStorageEnabled = true,
            IsNetworkEnabled = true,
            IsMotherboardEnabled = false,
            IsControllerEnabled = false,
            IsBatteryEnabled = false,
            IsPsuEnabled = false,
        };
        _computer.Open();
        UpdateAll();
        DumpSensors();

        // Initialize network fallback baseline
        SampleNetworkBytes(out _lastNetBytesSent, out _lastNetBytesReceived);
        _lastNetSample = DateTime.UtcNow;
    }

    public SystemStats GetStats()
    {
        UpdateAll();

        return new SystemStats
        {
            Timestamp = DateTime.UtcNow.ToString("o"),
            Cpu     = GetCpuStats(),
            Gpu     = GetGpuStats(),
            Ram     = GetRamStats(),
            Disks   = GetDiskStats(),
            Network = GetNetworkStats(),
        };
    }

    private void UpdateAll()
    {
        foreach (var hardware in _computer.Hardware)
        {
            hardware.Update();
            foreach (var sub in hardware.SubHardware)
                sub.Update();
        }
    }

    // Writes all detected hardware sensors to the log file once at startup.
    private void DumpSensors()
    {
        Logger.Sensors("━━━ Detected hardware sensors ━━━━━━━━━━━━━━━━━━━━━━━");
        foreach (var hw in _computer.Hardware)
        {
            Logger.Sensors($"[{hw.HardwareType}] {hw.Name}");
            foreach (var sensor in hw.Sensors)
            {
                var val = sensor.Value.HasValue ? $"{sensor.Value:F1}" : "null";
                Logger.Sensors($"  {sensor.SensorType,-14} | {sensor.Name,-35} = {val}");
            }
            foreach (var sub in hw.SubHardware)
            {
                Logger.Sensors($"  [{sub.HardwareType}] {sub.Name}");
                foreach (var sensor in sub.Sensors)
                {
                    var val = sensor.Value.HasValue ? $"{sensor.Value:F1}" : "null";
                    Logger.Sensors($"    {sensor.SensorType,-12} | {sensor.Name,-33} = {val}");
                }
            }
        }
        Logger.Sensors("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
    }

    // ──────────────────────────── CPU ────────────────────────────

    private CpuStats GetCpuStats()
    {
        var result = new CpuStats();

        var cpu = _computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
        if (cpu == null) return result;

        result.Name = cpu.Name;

        // Temperature priority buckets
        float? tctlTdie   = null;  
        float? ccdDie     = null;  
        float? pkgTemp    = null;  
        float? maxCoreTmp = null;  

        foreach (var sensor in cpu.Sensors)
        {
            var val = sensor.Value;
            if (val == null) continue;

            switch (sensor.SensorType)
            {
                case SensorType.Load when sensor.Name.Contains("Total", StringComparison.OrdinalIgnoreCase):
                    result.UsagePercent = val.Value;
                    break;

                case SensorType.Clock when sensor.Name.Contains("Core #1", StringComparison.OrdinalIgnoreCase)
                                        || sensor.Name.Contains("CPU Core", StringComparison.OrdinalIgnoreCase):
                    result.FrequencyMhz = val.Value;
                    break;

                case SensorType.Temperature:
                    var n = sensor.Name;
                    // Priority 1: Tctl/Tdie
                    if (n.Contains("Tctl", StringComparison.OrdinalIgnoreCase)
                        || (n.Contains("Tdie", StringComparison.OrdinalIgnoreCase) && !n.Contains("CCD", StringComparison.OrdinalIgnoreCase)))
                    {
                        tctlTdie = tctlTdie.HasValue ? MathF.Max(tctlTdie.Value, val.Value) : val;
                    }
                    // Priority 2: CCD die temps
                    else if (n.Contains("CCD", StringComparison.OrdinalIgnoreCase))
                    {
                        ccdDie = ccdDie.HasValue ? MathF.Max(ccdDie.Value, val.Value) : val;
                    }
                    // Priority 3: Package (Intel or generic)
                    else if (n.Contains("Package", StringComparison.OrdinalIgnoreCase)
                             || n.Equals("CPU", StringComparison.OrdinalIgnoreCase))
                    {
                        pkgTemp = pkgTemp.HasValue ? MathF.Max(pkgTemp.Value, val.Value) : val;
                    }
                    // Priority 4: Per-core temps — track the hottest
                    else if (n.StartsWith("Core", StringComparison.OrdinalIgnoreCase))
                    {
                        maxCoreTmp = maxCoreTmp.HasValue ? MathF.Max(maxCoreTmp.Value, val.Value) : val;
                    }
                    break;

                case SensorType.Power when sensor.Name.Contains("Package", StringComparison.OrdinalIgnoreCase):
                    result.PowerWatts = val;
                    break;
            }
        }

        // Pick: Tctl/Tdie → CCD max → Package → hottest core → WMI fallback
        float? lhmTemp = null;
        if (tctlTdie.HasValue && ccdDie.HasValue)
            lhmTemp = MathF.Max(tctlTdie.Value, ccdDie.Value);
        else
            lhmTemp = tctlTdie ?? ccdDie ?? pkgTemp ?? maxCoreTmp;

        result.TempCelsius = lhmTemp ?? GetCpuTempFromWmi();

        // Core/thread count
        result.Cores = GetCpuCoresFromWmi();
        result.Threads = result.Cores * 2;

        return result;
    }

    private static int GetCpuCoresFromWmi()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT NumberOfCores, NumberOfLogicalProcessors FROM Win32_Processor");
            foreach (ManagementObject obj in searcher.Get())
                return Convert.ToInt32(obj["NumberOfCores"]);
        }
        catch { }
        return Environment.ProcessorCount / 2;
    }

    private static float? GetCpuTempFromWmi()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\wmi", "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");
            foreach (ManagementObject obj in searcher.Get())
            {
                var raw = Convert.ToDouble(obj["CurrentTemperature"]);
                return (float)((raw / 10.0) - 273.15);
            }
        }
        catch { }
        return null;
    }

    // ──────────────────────────── GPU ────────────────────────────

    private GpuStats GetGpuStats()
    {
        var result = new GpuStats();

        var gpu = _computer.Hardware.FirstOrDefault(h =>
            h.HardwareType is HardwareType.GpuNvidia
                           or HardwareType.GpuAmd
                           or HardwareType.GpuIntel);

        if (gpu == null) return result;

        result.Name = gpu.Name;

        foreach (var sensor in gpu.Sensors)
        {
            var val = sensor.Value;
            if (val == null) continue;

            switch (sensor.SensorType)
            {
                case SensorType.Load when sensor.Name.Equals("GPU Core", StringComparison.OrdinalIgnoreCase):
                    result.UsagePercent = val.Value;
                    break;

                case SensorType.Temperature when sensor.Name.Equals("GPU Core", StringComparison.OrdinalIgnoreCase):
                    result.TempCelsius = val;
                    break;

                // Use LHM's own GPU Memory Used sensor
                case SensorType.SmallData when sensor.Name.Equals("GPU Memory Used", StringComparison.OrdinalIgnoreCase):
                    result.VramUsedMb = val.Value;
                    break;

                case SensorType.SmallData when sensor.Name.Equals("GPU Memory Total", StringComparison.OrdinalIgnoreCase):
                    result.VramTotalMb = val.Value;
                    break;

                case SensorType.Power when sensor.Name.Contains("GPU Package", StringComparison.OrdinalIgnoreCase)
                                        || sensor.Name.Contains("GPU Power", StringComparison.OrdinalIgnoreCase):
                    result.PowerWatts = val;
                    break;
            }
        }

        if (result.VramTotalMb == 0)
            result.VramTotalMb = GetVramTotalFromWmi();

        return result;
    }

    private static float GetVramTotalFromWmi()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT AdapterRAM FROM Win32_VideoController");
            foreach (ManagementObject obj in searcher.Get())
            {
                var ram = Convert.ToInt64(obj["AdapterRAM"]);
                if (ram > 0) return ram / (1024f * 1024f);
            }
        }
        catch { }
        return 0;
    }

    // ──────────────────────────── RAM ────────────────────────────

    private RamStats GetRamStats()
    {
        var ram = _computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Memory);
        if (ram == null) return GetRamFromWmi();

        float? usedGb = null, availGb = null;

        foreach (var sensor in ram.Sensors)
        {
            if (sensor.SensorType != SensorType.Data) continue;
            if (sensor.Name.Contains("Used", StringComparison.OrdinalIgnoreCase))
                usedGb = sensor.Value;
            else if (sensor.Name.Contains("Available", StringComparison.OrdinalIgnoreCase))
                availGb = sensor.Value;
        }

        if (usedGb.HasValue && availGb.HasValue)
        {
            var total = usedGb.Value + availGb.Value;
            return new RamStats
            {
                UsedGb        = MathF.Round(usedGb.Value, 2),
                TotalGb       = MathF.Round(total, 2),
                UsagePercent  = total > 0 ? MathF.Round(usedGb.Value / total * 100f, 1) : 0,
            };
        }

        return GetRamFromWmi();
    }

    private static RamStats GetRamFromWmi()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");
            foreach (ManagementObject obj in searcher.Get())
            {
                var totalKb = Convert.ToInt64(obj["TotalVisibleMemorySize"]);
                var freeKb  = Convert.ToInt64(obj["FreePhysicalMemory"]);
                var usedKb  = totalKb - freeKb;
                var totalGb = totalKb / (1024f * 1024f);
                var usedGb  = usedKb  / (1024f * 1024f);
                return new RamStats
                {
                    TotalGb      = MathF.Round(totalGb, 2),
                    UsedGb       = MathF.Round(usedGb, 2),
                    UsagePercent = totalKb > 0 ? MathF.Round((float)usedKb / totalKb * 100f, 1) : 0,
                };
            }
        }
        catch { }
        return new RamStats();
    }

    // ──────────────────────────── DISK ────────────────────────────

    private static List<DiskStats> GetDiskStats()
    {
        var result = new List<DiskStats>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (!drive.IsReady || drive.DriveType != DriveType.Fixed) continue;
            var totalGb = drive.TotalSize / (1024f * 1024f * 1024f);
            var freeGb  = drive.AvailableFreeSpace / (1024f * 1024f * 1024f);
            var usedGb  = totalGb - freeGb;
            result.Add(new DiskStats
            {
                Name         = drive.Name,
                Label        = string.IsNullOrWhiteSpace(drive.VolumeLabel) ? drive.Name : drive.VolumeLabel,
                UsedGb       = MathF.Round(usedGb, 2),
                TotalGb      = MathF.Round(totalGb, 2),
                UsagePercent = totalGb > 0 ? MathF.Round(usedGb / totalGb * 100f, 1) : 0,
                DriveType    = drive.DriveFormat ?? "Unknown",
            });
        }
        return result;
    }

    // ──────────────────────────── NETWORK ────────────────────────────

    private NetworkStats GetNetworkStats()
    {
        // Try LHM's own throughput sensors first (bytes/sec → KB/s)
        float? lhmUp = null, lhmDown = null;

        foreach (var hw in _computer.Hardware)
        {
            if (hw.HardwareType != HardwareType.Network) continue;
            foreach (var sensor in hw.Sensors)
            {
                if (sensor.SensorType != SensorType.Throughput || sensor.Value == null) continue;
                if (sensor.Name.Contains("Upload", StringComparison.OrdinalIgnoreCase)
                    || sensor.Name.Contains("Tx", StringComparison.OrdinalIgnoreCase))
                    lhmUp = (lhmUp ?? 0) + sensor.Value.Value / 1024f;
                else if (sensor.Name.Contains("Download", StringComparison.OrdinalIgnoreCase)
                    || sensor.Name.Contains("Rx", StringComparison.OrdinalIgnoreCase))
                    lhmDown = (lhmDown ?? 0) + sensor.Value.Value / 1024f;
            }
        }

        if (lhmUp.HasValue && lhmDown.HasValue)
        {
            return new NetworkStats
            {
                UploadKbps   = MathF.Max(0, MathF.Round(lhmUp.Value, 2)),
                DownloadKbps = MathF.Max(0, MathF.Round(lhmDown.Value, 2)),
            };
        }

        // Fallback: delta from total bytes across all NICs
        return GetNetworkFromDelta();
    }

    private NetworkStats GetNetworkFromDelta()
    {
        SampleNetworkBytes(out long sent, out long received);
        var now = DateTime.UtcNow;
        var elapsed = Math.Max((now - _lastNetSample).TotalSeconds, 1);

        var up   = (sent     - _lastNetBytesSent)     / elapsed / 1024f;
        var down = (received - _lastNetBytesReceived)  / elapsed / 1024f;

        _lastNetBytesSent     = sent;
        _lastNetBytesReceived = received;
        _lastNetSample        = now;

        return new NetworkStats
        {
            UploadKbps   = MathF.Max(0, MathF.Round((float)up,   2)),
            DownloadKbps = MathF.Max(0, MathF.Round((float)down, 2)),
        };
    }

    private static void SampleNetworkBytes(out long totalSent, out long totalReceived)
    {
        totalSent = 0; totalReceived = 0;
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            var stats = nic.GetIPv4Statistics();
            totalSent     += stats.BytesSent;
            totalReceived += stats.BytesReceived;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _computer.Close();
        GC.SuppressFinalize(this);
    }
}
