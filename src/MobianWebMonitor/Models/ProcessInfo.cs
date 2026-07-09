namespace MobianWebMonitor.Models;

public sealed class ProcessInfo
{
    public int Pid { get; set; }
    public string Name { get; set; } = "N/A";
    public string Command { get; set; } = "N/A";
    public string User { get; set; } = "N/A";
    public string State { get; set; } = "N/A";
    public int ThreadCount { get; set; }
    public double CpuUsagePercent { get; set; }
    public long MemoryBytes { get; set; }
    public DateTime? StartedAtUtc { get; set; }
}
