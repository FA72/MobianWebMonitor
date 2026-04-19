namespace MobianWebMonitor.Models;

public sealed class ServiceStatusInfo
{
    public string ActiveState { get; set; } = "N/A";
    public string SubState { get; set; } = "N/A";
    public DateTime? StartedAt { get; set; }
}
