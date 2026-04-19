namespace MobianWebMonitor.Models;

public sealed class DiskMetrics
{
    public long TotalBytes { get; set; }
    public long UsedBytes { get; set; }
    public long FreeBytes { get; set; }
    public double UsedPercent => TotalBytes > 0 ? Math.Round(100.0 * UsedBytes / TotalBytes, 1) : 0;
    public string MountPoint { get; set; } = "/";
}
