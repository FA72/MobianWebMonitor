using Docker.DotNet;
using Docker.DotNet.Models;
using MobianWebMonitor.Models;

namespace MobianWebMonitor.Metrics.Slow;

public sealed class DockerCollector : IDisposable
{
    private static readonly TimeSpan StatsTimeout = TimeSpan.FromSeconds(2);
    private readonly ILogger<DockerCollector> _logger;
    private readonly Lock _cacheLock = new();
    private readonly Dictionary<string, CachedContainerMetadata> _metadataCache = [];
    private readonly Dictionary<string, CachedContainerStats> _statsCache = [];
    private DockerClient? _client;
    private bool _errorLogged;

    public DockerCollector(ILogger<DockerCollector> logger)
    {
        _logger = logger;
    }

    public async Task<List<DockerContainerInfo>> CollectAsync(CancellationToken ct)
    {
        var result = new List<DockerContainerInfo>();

        try
        {
            var client = GetClient();
            if (client == null) return result;

            var containers = await client.Containers.ListContainersAsync(
                new ContainersListParameters { All = true }, ct);

            var seenContainerIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (var c in containers)
            {
                seenContainerIds.Add(c.ID);

                try
                {
                    var state = c.State ?? "unknown";
                    var isRunning = string.Equals(state, "running", StringComparison.OrdinalIgnoreCase);
                    var startedAtUtc = isRunning
                        ? await GetStartedAtUtcAsync(client, c.ID, state, ct)
                        : null;

                    var info = new DockerContainerInfo
                    {
                        Name = c.Names.FirstOrDefault()?.TrimStart('/') ?? "unknown",
                        Status = c.Status ?? "N/A",
                        State = state,
                        ImageTag = c.Image ?? "N/A",
                        Uptime = FormatUptime(startedAtUtc, state),
                        StartedAtUtc = startedAtUtc
                    };

                    if (isRunning)
                    {
                        try
                        {
                            var stats = await GetContainerStatsAsync(client, c.ID, ct);
                            if (stats != null)
                            {
                                info.CpuUsage = FormatCpuPercent(stats);
                                info.MemoryUsage = FormatMemoryUsage(stats);
                                info.ResourceStatsAreStale = false;
                                UpdateStatsCache(c.ID, info.CpuUsage, info.MemoryUsage);
                            }
                            else
                            {
                                ApplyCachedStats(info, c.ID);
                            }
                        }
                        catch
                        {
                            ApplyCachedStats(info, c.ID);
                        }
                    }

                    result.Add(info);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Skipping Docker metrics for container {ContainerId}", c.ID);
                }
            }

            TrimCaches(seenContainerIds);

            _errorLogged = false;
        }
        catch (Exception ex)
        {
            LogOnce("Error collecting Docker metrics: {Error}", ex.Message);
        }

        return result;
    }

    private async Task<DateTime?> GetStartedAtUtcAsync(
        DockerClient client,
        string containerId,
        string state,
        CancellationToken ct)
    {
        lock (_cacheLock)
        {
            if (_metadataCache.TryGetValue(containerId, out var cached) &&
                string.Equals(cached.State, state, StringComparison.OrdinalIgnoreCase))
            {
                return cached.StartedAtUtc;
            }
        }

        var details = await client.Containers.InspectContainerAsync(containerId, ct);
        var startedAtUtc = ParseDockerTimestamp(details.State?.StartedAt);

        lock (_cacheLock)
        {
            _metadataCache[containerId] = new CachedContainerMetadata(startedAtUtc, state);
        }

        return startedAtUtc;
    }

    private void ApplyCachedStats(DockerContainerInfo info, string containerId)
    {
        lock (_cacheLock)
        {
            if (!_statsCache.TryGetValue(containerId, out var cached))
            {
                return;
            }

            info.CpuUsage = cached.CpuUsage;
            info.MemoryUsage = cached.MemoryUsage;
            info.ResourceStatsAreStale = true;
        }
    }

    private void UpdateStatsCache(string containerId, string cpuUsage, string memoryUsage)
    {
        lock (_cacheLock)
        {
            _statsCache[containerId] = new CachedContainerStats(cpuUsage, memoryUsage);
        }
    }

    private void TrimCaches(HashSet<string> seenContainerIds)
    {
        lock (_cacheLock)
        {
            var metadataKeysToRemove = _metadataCache.Keys.Where(id => !seenContainerIds.Contains(id)).ToArray();
            foreach (var key in metadataKeysToRemove)
            {
                _metadataCache.Remove(key);
            }

            var statsKeysToRemove = _statsCache.Keys.Where(id => !seenContainerIds.Contains(id)).ToArray();
            foreach (var key in statsKeysToRemove)
            {
                _statsCache.Remove(key);
            }
        }
    }

    private DockerClient? GetClient()
    {
        if (_client != null) return _client;

        var socketPath = "/var/run/docker.sock";
        if (!File.Exists(socketPath) && !Directory.Exists("/var/run"))
        {
            LogOnce("Docker socket not found at {Path}", socketPath);
            return null;
        }

        try
        {
            _client = new DockerClientConfiguration(new Uri("unix:///var/run/docker.sock"))
                .CreateClient();
            return _client;
        }
        catch (Exception ex)
        {
            LogOnce("Cannot connect to Docker: {Error}", ex.Message);
            return null;
        }
    }

    private static async Task<ContainerStatsResponse?> GetContainerStatsAsync(
        DockerClient client, string containerId, CancellationToken ct)
    {
        ContainerStatsResponse? statsResponse = null;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(StatsTimeout);

        await client.Containers.GetContainerStatsAsync(
            containerId,
            new ContainerStatsParameters { Stream = false },
            new Progress<ContainerStatsResponse>(s => statsResponse = s),
            cts.Token);

        return statsResponse;
    }

    private static string FormatCpuPercent(ContainerStatsResponse stats)
    {
        var cpuDelta = (double)(stats.CPUStats.CPUUsage.TotalUsage - stats.PreCPUStats.CPUUsage.TotalUsage);
        var systemDelta = (double)(stats.CPUStats.SystemUsage - stats.PreCPUStats.SystemUsage);
        var cores = stats.CPUStats.OnlineCPUs;
        if (cores == 0) cores = (uint)(stats.CPUStats.CPUUsage.PercpuUsage?.Count ?? 1);

        if (systemDelta > 0 && cpuDelta >= 0)
        {
            var percent = cpuDelta / systemDelta * cores * 100.0;
            return $"{percent:F1}%";
        }

        return "N/A";
    }

    private static string FormatMemoryUsage(ContainerStatsResponse stats)
    {
        var usedBytes = stats.MemoryStats.Usage;
        var cacheBytes = stats.MemoryStats.Stats?.TryGetValue("cache", out var cache) == true ? cache : 0;
        var actual = usedBytes - cacheBytes;
        return FormatBytes((long)actual);
    }

    private static DateTime? ParseDockerTimestamp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return DateTimeOffset.TryParse(value, out var parsed)
            ? parsed.UtcDateTime
            : null;
    }

    private static string FormatUptime(DateTime? startedAtUtc, string? state)
    {
        if (!string.Equals(state, "running", StringComparison.OrdinalIgnoreCase))
            return state ?? "N/A";

        if (!startedAtUtc.HasValue)
            return "N/A";

        var elapsed = DateTime.UtcNow - startedAtUtc.Value;
        if (elapsed.TotalDays >= 1) return $"{elapsed.Days}d {elapsed.Hours}h";
        if (elapsed.TotalHours >= 1) return $"{elapsed.Hours}h {elapsed.Minutes}m";
        return $"{elapsed.Minutes}m";
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 0) bytes = 0;
        return bytes switch
        {
            >= 1_073_741_824 => $"{bytes / 1_073_741_824.0:F1} GB",
            >= 1_048_576 => $"{bytes / 1_048_576.0:F1} MB",
            >= 1024 => $"{bytes / 1024.0:F1} KB",
            _ => $"{bytes} B"
        };
    }

    private void LogOnce(string message, params object[] args)
    {
        if (_errorLogged) return;
        _errorLogged = true;
        _logger.LogWarning(message, args);
    }

    public void Dispose()
    {
        _client?.Dispose();
    }

    private sealed record CachedContainerMetadata(DateTime? StartedAtUtc, string State);

    private sealed record CachedContainerStats(string CpuUsage, string MemoryUsage);
}
