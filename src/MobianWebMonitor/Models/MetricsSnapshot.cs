namespace MobianWebMonitor.Models;

public sealed class MetricsSnapshot
{
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public BatteryInfo Battery { get; set; } = new();
    public CpuMetrics Cpu { get; set; } = new();
    public MemoryMetrics Memory { get; set; } = new();
    public DiskMetrics Disk { get; set; } = new();
    public List<DockerContainerInfo> DockerContainers { get; set; } = [];
    public ServiceStatusInfo BatteryLimiterService { get; set; } = new();
    public bool IsStale { get; set; }
    public DateTime LastFastUpdate { get; set; }
    public DateTime LastSlowUpdate { get; set; }
}
