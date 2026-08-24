namespace Nekoframe.Models;

public class SystemStats
{
    public string Timestamp { get; set; } = DateTime.UtcNow.ToString("o");
    public CpuStats Cpu { get; set; } = new();
    public GpuStats Gpu { get; set; } = new();
    public RamStats Ram { get; set; } = new();
    public List<DiskStats> Disks { get; set; } = new();
    public List<StorageStats> Storage { get; set; } = new();
    public NetworkStats Network { get; set; } = new();
    public List<FanStats> Fans { get; set; } = new();
}

public class CpuStats
{
    public string Name { get; set; } = "Unknown";
    public int Cores { get; set; }
    public int Threads { get; set; }
    public float UsagePercent { get; set; }
    public float FrequencyMhz { get; set; }
    public float? TempCelsius { get; set; }
    public float? PowerWatts { get; set; }
}

public class GpuStats
{
    public string Name { get; set; } = "Unknown";
    public float UsagePercent { get; set; }
    public float? TempCelsius { get; set; }
    public float? HotspotTempCelsius { get; set; }
    public float VramUsedMb { get; set; }
    public float VramTotalMb { get; set; }
    public float? PowerWatts { get; set; }
}

public class RamStats
{
    public float UsedGb { get; set; }
    public float TotalGb { get; set; }
    public float UsagePercent { get; set; }
}

public class DiskStats
{
    public string Name { get; set; } = "";
    public string Label { get; set; } = "";
    public float UsedGb { get; set; }
    public float TotalGb { get; set; }
    public float UsagePercent { get; set; }
    public string DriveType { get; set; } = "";
}

public class StorageStats
{
    public string Name { get; set; } = "";
    public float? TempCelsius { get; set; }
}

public class FanStats
{
    public string Name { get; set; } = "";
    public float Rpm { get; set; }
}

public class NetworkStats
{
    public float UploadKbps { get; set; }
    public float DownloadKbps { get; set; }
}
