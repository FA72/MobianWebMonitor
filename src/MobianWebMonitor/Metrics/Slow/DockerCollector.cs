using Docker.DotNet;
using Docker.DotNet.Models;
using MobianWebMonitor.Models;

namespace MobianWebMonitor.Metrics.Slow;

public sealed class DockerCollector : IDisposable
{
    private readonly ILogger<DockerCollector> _logger;
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

            foreach (var c in containers)
            {
                var info = new DockerContainerInfo
                {
                    Name = c.Names.FirstOrDefault()?.TrimStart('/') ?? "unknown",
                    Status = c.Status ?? "N/A",
                    ImageTag = c.Image ?? "N/A",
                    Uptime = FormatUptime(c.Created, c.State)
                };

                if (string.Equals(c.State, "running", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var stats = await GetContainerStatsAsync(client, c.ID, ct);
                        if (stats != null)
                        {
                            info.CpuUsage = FormatCpuPercent(stats);
                            info.MemoryUsage = FormatMemoryUsage(stats);
                        }
                    }
                    catch
                    {
                        // Stats not available for this container
                    }
                }

                result.Add(info);
            }

            _errorLogged = false;
        }
        catch (Exception ex)
        {
            LogOnce("Error collecting Docker metrics: {Error}", ex.Message);
        }

        return result;
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
        cts.CancelAfter(TimeSpan.FromSeconds(3));

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

    private static string FormatUptime(DateTime created, string? state)
    {
        if (!string.Equals(state, "running", StringComparison.OrdinalIgnoreCase))
            return state ?? "N/A";

        var elapsed = DateTime.UtcNow - created;
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
}
