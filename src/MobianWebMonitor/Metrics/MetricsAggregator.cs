using MobianWebMonitor.Models;

namespace MobianWebMonitor.Metrics;

public sealed class MetricsAggregator
{
    private readonly Lock _lock = new();
    private MetricsSnapshot _current = new();

    public MetricsSnapshot Current
    {
        get { lock (_lock) return _current; }
    }

    public void UpdateFast(CpuMetrics cpu, MemoryMetrics memory, BatteryInfo battery)
    {
        lock (_lock)
        {
            _current.Cpu = cpu;
            _current.Memory = memory;
            _current.Battery = battery;
            _current.LastFastUpdate = DateTime.UtcNow;
            _current.TimestampUtc = DateTime.UtcNow;
            _current.IsStale = false;
        }
    }

    public void UpdateSlow(DiskMetrics disk, List<DockerContainerInfo> docker, ServiceStatusInfo serviceStatus)
    {
        lock (_lock)
        {
            _current.Disk = disk;
            _current.DockerContainers = docker;
            _current.BatteryLimiterService = serviceStatus;
            _current.Battery.BatteryLimiterStatus = serviceStatus.ActiveState;
            _current.LastSlowUpdate = DateTime.UtcNow;
        }
    }

    public void MarkStale()
    {
        lock (_lock)
        {
            _current.IsStale = true;
        }
    }
}
