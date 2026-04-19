using MobianWebMonitor.Hardware;
using MobianWebMonitor.Models;
using MobianWebMonitor.Options;
using Microsoft.Extensions.Options;

namespace MobianWebMonitor.Metrics.Fast;

public sealed class CpuCollector
{
    private readonly string _statPath;
    private readonly ILogger<CpuCollector> _logger;
    private long[]? _prevTotal;
    private long[]? _prevIdle;
    private bool _errorLogged;

    public CpuCollector(IOptions<HostPathsOptions> hostPaths, ILogger<CpuCollector> logger)
    {
        _statPath = Path.Combine(hostPaths.Value.ProcRoot, "stat");
        _logger = logger;
    }

    public CpuMetrics Collect()
    {
        var result = new CpuMetrics();

        try
        {
            if (!File.Exists(_statPath))
            {
                LogOnce("CPU stat file not found: {Path}", _statPath);
                return result;
            }

            var lines = File.ReadAllLines(_statPath);
            var cpuLines = lines.Where(l => l.StartsWith("cpu")).ToList();

            var totalLine = cpuLines.FirstOrDefault(l => l.StartsWith("cpu "));
            var coreLines = cpuLines.Where(l => l.StartsWith("cpu") && !l.StartsWith("cpu ")).ToList();

            int count = 1 + coreLines.Count; // total + per-core
            var currentTotal = new long[count];
            var currentIdle = new long[count];

            if (totalLine != null)
                ParseCpuLine(totalLine, out currentTotal[0], out currentIdle[0]);

            for (int i = 0; i < coreLines.Count; i++)
                ParseCpuLine(coreLines[i], out currentTotal[i + 1], out currentIdle[i + 1]);

            if (_prevTotal != null && _prevTotal.Length == count)
            {
                result.TotalUsagePercent = CalcUsage(_prevTotal[0], _prevIdle![0], currentTotal[0], currentIdle[0]);
                for (int i = 0; i < coreLines.Count; i++)
                    result.CoreUsagePercents.Add(CalcUsage(_prevTotal[i + 1], _prevIdle![i + 1], currentTotal[i + 1], currentIdle[i + 1]));
            }

            _prevTotal = currentTotal;
            _prevIdle = currentIdle;
            _errorLogged = false;
        }
        catch (Exception ex)
        {
            LogOnce("Error collecting CPU metrics: {Error}", ex.Message);
        }

        return result;
    }

    private static void ParseCpuLine(string line, out long total, out long idle)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        // cpu user nice system idle iowait irq softirq steal
        total = 0;
        idle = 0;
        for (int i = 1; i < parts.Length && i <= 8; i++)
        {
            if (long.TryParse(parts[i], out var val))
            {
                total += val;
                if (i == 4 || i == 5) // idle + iowait
                    idle += val;
            }
        }
    }

    private static double CalcUsage(long prevTotal, long prevIdle, long curTotal, long curIdle)
    {
        var totalDiff = curTotal - prevTotal;
        var idleDiff = curIdle - prevIdle;
        if (totalDiff <= 0) return 0;
        return Math.Round(100.0 * (totalDiff - idleDiff) / totalDiff, 1);
    }

    private void LogOnce(string message, params object[] args)
    {
        if (_errorLogged) return;
        _errorLogged = true;
        _logger.LogWarning(message, args);
    }
}
