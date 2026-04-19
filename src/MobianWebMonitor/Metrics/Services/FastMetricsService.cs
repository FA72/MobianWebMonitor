using MobianWebMonitor.Hubs;
using MobianWebMonitor.Metrics.Fast;
using MobianWebMonitor.Storage;
using Microsoft.AspNetCore.SignalR;

namespace MobianWebMonitor.Metrics.Services;

public sealed class FastMetricsService : BackgroundService
{
    private readonly CpuCollector _cpu;
    private readonly MemoryCollector _memory;
    private readonly BatteryCollector _battery;
    private readonly MetricsAggregator _aggregator;
    private readonly HistoryStorage _storage;
    private readonly IHubContext<MetricsHub> _hub;
    private readonly ILogger<FastMetricsService> _logger;
    private int _sampleCounter;

    public FastMetricsService(
        CpuCollector cpu,
        MemoryCollector memory,
        BatteryCollector battery,
        MetricsAggregator aggregator,
        HistoryStorage storage,
        IHubContext<MetricsHub> hub,
        ILogger<FastMetricsService> logger)
    {
        _cpu = cpu;
        _memory = memory;
        _battery = battery;
        _aggregator = aggregator;
        _storage = storage;
        _hub = hub;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Fast metrics service started (1s interval)");

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                var cpu = _cpu.Collect();
                var memory = _memory.Collect();
                var battery = _battery.Collect();

                _aggregator.UpdateFast(cpu, memory, battery);

                // Write to history every 5 seconds to reduce disk I/O
                _sampleCounter++;
                if (_sampleCounter >= 5)
                {
                    _sampleCounter = 0;
                    var now = DateTime.UtcNow;
                    await _storage.WriteSamplesAsync(now, new Dictionary<string, double?>
                    {
                        ["cpu.total"] = cpu.TotalUsagePercent,
                        ["mem.used_pct"] = memory.UsedPercent,
                        ["mem.cached_pct"] = memory.CachedPercent,
                        ["mem.free_pct"] = memory.FreePercent,
                        ["bat.capacity"] = battery.CapacityPercent,
                        ["bat.temp"] = battery.TemperatureCelsius
                    });

                    for (int i = 0; i < cpu.CoreUsagePercents.Count; i++)
                        await _storage.WriteSampleAsync(now, $"cpu.core{i}", cpu.CoreUsagePercents[i]);
                }

                // Push to connected clients
                var snapshot = _aggregator.Current;
                await _hub.Clients.All.SendAsync("ReceiveMetrics", snapshot, stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in fast metrics loop");
            }
        }
    }
}
