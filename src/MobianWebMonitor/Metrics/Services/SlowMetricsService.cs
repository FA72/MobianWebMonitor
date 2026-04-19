using MobianWebMonitor.Metrics.Slow;

namespace MobianWebMonitor.Metrics.Services;

public sealed class SlowMetricsService : BackgroundService
{
    private readonly DiskCollector _disk;
    private readonly DockerCollector _docker;
    private readonly SystemdServiceCollector _systemd;
    private readonly MetricsAggregator _aggregator;
    private readonly ILogger<SlowMetricsService> _logger;

    private const int DiskIntervalSeconds = 30;
    private const int DockerIntervalSeconds = 15;
    private const int ServiceIntervalSeconds = 10;

    public SlowMetricsService(
        DiskCollector disk,
        DockerCollector docker,
        SystemdServiceCollector systemd,
        MetricsAggregator aggregator,
        ILogger<SlowMetricsService> logger)
    {
        _disk = disk;
        _docker = docker;
        _systemd = systemd;
        _aggregator = aggregator;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Slow metrics service started");

        int tick = 0;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));

        // Initial collection
        await CollectAll(stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                tick += 5;

                if (tick % DockerIntervalSeconds == 0 || tick % ServiceIntervalSeconds == 0)
                {
                    var docker = tick % DockerIntervalSeconds == 0
                        ? await _docker.CollectAsync(stoppingToken)
                        : _aggregator.Current.DockerContainers;

                    var service = tick % ServiceIntervalSeconds == 0
                        ? await _systemd.CollectAsync("battery-limiter.service", stoppingToken)
                        : _aggregator.Current.BatteryLimiterService;

                    var disk = tick % DiskIntervalSeconds == 0
                        ? _disk.Collect()
                        : _aggregator.Current.Disk;

                    _aggregator.UpdateSlow(disk, docker, service);
                }

                if (tick >= 300) tick = 0; // Reset counter
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in slow metrics loop");
            }
        }
    }

    private async Task CollectAll(CancellationToken ct)
    {
        try
        {
            var disk = _disk.Collect();
            var docker = await _docker.CollectAsync(ct);
            var service = await _systemd.CollectAsync("battery-limiter.service", ct);
            _aggregator.UpdateSlow(disk, docker, service);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during initial slow metrics collection");
        }
    }
}
