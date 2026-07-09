using Microsoft.Extensions.Options;
using MobianWebMonitor.Models;
using MobianWebMonitor.Options;

namespace MobianWebMonitor.Metrics.Slow;

public sealed class ProcessCollector
{
    private const long ClockTicksPerSecond = 100;
    private const int MaxCommandLength = 240;

    private readonly string _procRoot;
    private readonly ILogger<ProcessCollector> _logger;
    private readonly Lock _cacheLock = new();
    private readonly Dictionary<int, CachedProcessSample> _previousSamples = [];
    private bool _errorLogged;

    public ProcessCollector(IOptions<HostPathsOptions> hostPaths, ILogger<ProcessCollector> logger)
    {
        _procRoot = hostPaths.Value.ProcRoot;
        _logger = logger;
    }

    public List<ProcessInfo> Collect()
    {
        var result = new List<ProcessInfo>();

        try
        {
            if (!Directory.Exists(_procRoot))
            {
                LogOnce("Proc root not found: {Path}", _procRoot);
                return result;
            }

            var totalJiffies = ReadTotalCpuJiffies();
            var bootTimeUtc = ReadBootTimeUtc();

            Dictionary<int, CachedProcessSample> previousSamples;
            lock (_cacheLock)
            {
                previousSamples = new Dictionary<int, CachedProcessSample>(_previousSamples);
            }

            var currentSamples = new Dictionary<int, CachedProcessSample>();

            foreach (var processDir in Directory.EnumerateDirectories(_procRoot))
            {
                var directoryName = Path.GetFileName(processDir);
                if (!int.TryParse(directoryName, out var pid))
                {
                    continue;
                }

                if (!TryReadProcess(processDir, pid, totalJiffies, bootTimeUtc, previousSamples, out var process, out var sample))
                {
                    continue;
                }

                result.Add(process);
                currentSamples[pid] = sample;
            }

            lock (_cacheLock)
            {
                _previousSamples.Clear();
                foreach (var (pid, sample) in currentSamples)
                {
                    _previousSamples[pid] = sample;
                }
            }

            _errorLogged = false;
        }
        catch (Exception ex)
        {
            LogOnce("Error collecting process metrics: {Error}", ex.Message);
        }

        return result
            .OrderByDescending(p => p.CpuUsagePercent)
            .ThenByDescending(p => p.MemoryBytes)
            .ThenBy(p => p.Pid)
            .ToList();
    }

    private bool TryReadProcess(
        string processDir,
        int pid,
        long totalJiffies,
        DateTime? bootTimeUtc,
        Dictionary<int, CachedProcessSample> previousSamples,
        out ProcessInfo process,
        out CachedProcessSample sample)
    {
        process = new ProcessInfo { Pid = pid };
        sample = default;

        try
        {
            var statPath = Path.Combine(processDir, "stat");
            if (!File.Exists(statPath))
            {
                return false;
            }

            var stat = File.ReadAllText(statPath);
            if (!TryParseStat(stat, out var parsed))
            {
                return false;
            }

            sample = new CachedProcessSample(totalJiffies, parsed.ProcessJiffies, parsed.StartTimeTicks);

            var cpuUsage = 0.0;
            if (previousSamples.TryGetValue(pid, out var previous) &&
                previous.StartTimeTicks == parsed.StartTimeTicks)
            {
                var processDelta = parsed.ProcessJiffies - previous.ProcessJiffies;
                var totalDelta = totalJiffies - previous.TotalCpuJiffies;
                if (processDelta >= 0 && totalDelta > 0)
                {
                    cpuUsage = Math.Round(processDelta * Environment.ProcessorCount * 100.0 / totalDelta, 1);
                }
            }

            var command = ReadCommandLine(processDir);
            var uid = ReadUid(processDir);

            process = new ProcessInfo
            {
                Pid = pid,
                Name = parsed.Name,
                Command = string.IsNullOrWhiteSpace(command) ? parsed.Name : command,
                User = uid ?? "N/A",
                State = parsed.State,
                ThreadCount = parsed.ThreadCount,
                CpuUsagePercent = cpuUsage,
                MemoryBytes = Math.Max(0, parsed.ResidentPages) * (long)Environment.SystemPageSize,
                StartedAtUtc = bootTimeUtc?.AddSeconds(parsed.StartTimeTicks / (double)ClockTicksPerSecond)
            };

            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private long ReadTotalCpuJiffies()
    {
        var statPath = Path.Combine(_procRoot, "stat");
        if (!File.Exists(statPath))
        {
            return 0;
        }

        var line = File.ReadLines(statPath).FirstOrDefault(l => l.StartsWith("cpu ", StringComparison.Ordinal));
        if (line == null)
        {
            return 0;
        }

        long total = 0;
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 1; i < parts.Length; i++)
        {
            if (long.TryParse(parts[i], out var value))
            {
                total += value;
            }
        }

        return total;
    }

    private DateTime? ReadBootTimeUtc()
    {
        var statPath = Path.Combine(_procRoot, "stat");
        if (!File.Exists(statPath))
        {
            return null;
        }

        foreach (var line in File.ReadLines(statPath))
        {
            if (!line.StartsWith("btime ", StringComparison.Ordinal))
            {
                continue;
            }

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && long.TryParse(parts[1], out var unixSeconds))
            {
                return DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;
            }
        }

        return null;
    }

    private static bool TryParseStat(string stat, out ParsedStat parsed)
    {
        parsed = default;

        var openParen = stat.IndexOf('(');
        var closeParen = stat.LastIndexOf(')');
        if (openParen < 0 || closeParen <= openParen)
        {
            return false;
        }

        var name = stat[(openParen + 1)..closeParen];
        var rest = stat[(closeParen + 1)..].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (rest.Length < 22)
        {
            return false;
        }

        if (!long.TryParse(rest[11], out var userJiffies) ||
            !long.TryParse(rest[12], out var systemJiffies) ||
            !int.TryParse(rest[17], out var threadCount) ||
            !long.TryParse(rest[19], out var startTimeTicks) ||
            !long.TryParse(rest[21], out var residentPages))
        {
            return false;
        }

        parsed = new ParsedStat(
            name,
            rest[0],
            userJiffies + systemJiffies,
            startTimeTicks,
            residentPages,
            threadCount);
        return true;
    }

    private static string? ReadCommandLine(string processDir)
    {
        var cmdlinePath = Path.Combine(processDir, "cmdline");
        if (!File.Exists(cmdlinePath))
        {
            return null;
        }

        var bytes = File.ReadAllBytes(cmdlinePath);
        if (bytes.Length == 0)
        {
            return null;
        }

        var command = System.Text.Encoding.UTF8.GetString(bytes)
            .Replace('\0', ' ')
            .Trim();

        if (command.Length > MaxCommandLength)
        {
            command = command[..MaxCommandLength] + "...";
        }

        return command;
    }

    private static string? ReadUid(string processDir)
    {
        var statusPath = Path.Combine(processDir, "status");
        if (!File.Exists(statusPath))
        {
            return null;
        }

        foreach (var line in File.ReadLines(statusPath))
        {
            if (!line.StartsWith("Uid:", StringComparison.Ordinal))
            {
                continue;
            }

            var parts = line.Split('\t', ' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 2 ? parts[1] : null;
        }

        return null;
    }

    private void LogOnce(string message, params object[] args)
    {
        if (_errorLogged) return;
        _errorLogged = true;
        _logger.LogWarning(message, args);
    }

    private readonly record struct CachedProcessSample(long TotalCpuJiffies, long ProcessJiffies, long StartTimeTicks);

    private readonly record struct ParsedStat(
        string Name,
        string State,
        long ProcessJiffies,
        long StartTimeTicks,
        long ResidentPages,
        int ThreadCount);
}
