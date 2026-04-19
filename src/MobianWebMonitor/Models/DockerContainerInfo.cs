namespace MobianWebMonitor.Models;

public sealed class DockerContainerInfo
{
    public string Name { get; set; } = "N/A";
    public string Status { get; set; } = "N/A";
    public string State { get; set; } = "unknown";
    public string ImageTag { get; set; } = "N/A";
    public string Uptime { get; set; } = "N/A";
    public DateTime? StartedAtUtc { get; set; }
    public string CpuUsage { get; set; } = "N/A";
    public string MemoryUsage { get; set; } = "N/A";
}
