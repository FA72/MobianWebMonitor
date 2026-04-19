using MobianWebMonitor.Models;

namespace MobianWebMonitor.Metrics.Slow;

public sealed class DiskCollector
{
    private readonly ILogger<DiskCollector> _logger;
    private bool _errorLogged;

    public DiskCollector(ILogger<DiskCollector> logger)
    {
        _logger = logger;
    }

    public DiskMetrics Collect()
    {
        var result = new DiskMetrics();

        try
        {
            // Use root mount point — works via bind-mount or native
            var driveInfo = DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
                .OrderBy(d => d.Name.Length)
                .FirstOrDefault();

            if (driveInfo == null)
            {
                LogOnce("No fixed drives found");
                return result;
            }

            result.MountPoint = driveInfo.Name;
            result.TotalBytes = driveInfo.TotalSize;
            result.FreeBytes = driveInfo.AvailableFreeSpace;
            result.UsedBytes = result.TotalBytes - result.FreeBytes;
            _errorLogged = false;
        }
        catch (Exception ex)
        {
            LogOnce("Error collecting disk metrics: {Error}", ex.Message);
        }

        return result;
    }

    private void LogOnce(string message, params object[] args)
    {
        if (_errorLogged) return;
        _errorLogged = true;
        _logger.LogWarning(message, args);
    }
}
