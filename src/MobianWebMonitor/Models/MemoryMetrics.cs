namespace MobianWebMonitor.Models;

public sealed class MemoryMetrics
{
    public long TotalKb { get; set; }
    public long UsedKb { get; set; }
    public long FreeKb { get; set; }
    public long CachedKb { get; set; }
    public long BuffersKb { get; set; }
    public long? SwapTotalKb { get; set; }
    public long? SwapUsedKb { get; set; }
    public double UsedPercent => TotalKb > 0 ? Math.Round(100.0 * UsedKb / TotalKb, 1) : 0;
    public double CachedPercent => TotalKb > 0 ? Math.Round(100.0 * CachedKb / TotalKb, 1) : 0;
    public double FreePercent => TotalKb > 0 ? Math.Round(100.0 * FreeKb / TotalKb, 1) : 0;
}
