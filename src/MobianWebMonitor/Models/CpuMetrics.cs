namespace MobianWebMonitor.Models;

public sealed class CpuMetrics
{
    public double TotalUsagePercent { get; set; }
    public List<double> CoreUsagePercents { get; set; } = [];
}
