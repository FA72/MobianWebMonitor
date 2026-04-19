using MobianWebMonitor.Models;
using MobianWebMonitor.Options;
using Microsoft.Extensions.Options;

namespace MobianWebMonitor.Metrics.Fast;

public sealed class MemoryCollector
{
    private readonly string _meminfoPath;
    private readonly ILogger<MemoryCollector> _logger;
    private bool _errorLogged;

    public MemoryCollector(IOptions<HostPathsOptions> hostPaths, ILogger<MemoryCollector> logger)
    {
        _meminfoPath = Path.Combine(hostPaths.Value.ProcRoot, "meminfo");
        _logger = logger;
    }

    public MemoryMetrics Collect()
    {
        var result = new MemoryMetrics();

        try
        {
            if (!File.Exists(_meminfoPath))
            {
                LogOnce("Meminfo file not found: {Path}", _meminfoPath);
                return result;
            }

            var fields = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in File.ReadLines(_meminfoPath))
            {
                var parts = line.Split(':', 2);
                if (parts.Length != 2) continue;
                var key = parts[0].Trim();
                var valuePart = parts[1].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (valuePart.Length >= 1 && long.TryParse(valuePart[0], out var val))
                    fields[key] = val;
            }

            result.TotalKb = fields.GetValueOrDefault("MemTotal");
            result.FreeKb = fields.GetValueOrDefault("MemFree");
            result.BuffersKb = fields.GetValueOrDefault("Buffers");
            result.CachedKb = fields.GetValueOrDefault("Cached") + fields.GetValueOrDefault("SReclaimable");
            result.UsedKb = result.TotalKb - result.FreeKb - result.BuffersKb - result.CachedKb;
            if (result.UsedKb < 0) result.UsedKb = result.TotalKb - result.FreeKb;

            if (fields.TryGetValue("SwapTotal", out var swapTotal))
            {
                result.SwapTotalKb = swapTotal;
                result.SwapUsedKb = swapTotal - fields.GetValueOrDefault("SwapFree");
            }

            _errorLogged = false;
        }
        catch (Exception ex)
        {
            LogOnce("Error collecting memory metrics: {Error}", ex.Message);
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
