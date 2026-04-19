namespace MobianWebMonitor.Storage;

public sealed class HistoryCleanupService : BackgroundService
{
    private readonly ILogger<HistoryCleanupService> _logger;
    private const string DataDir = "/data/history";

    public HistoryCleanupService(ILogger<HistoryCleanupService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("History cleanup service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                Cleanup();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during history cleanup");
            }

            // Run cleanup once per hour
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }

    private void Cleanup()
    {
        if (!Directory.Exists(DataDir)) return;

        var today = DateTime.UtcNow.Date;
        var yesterday = today.AddDays(-1);

        var keepDates = new HashSet<string>
        {
            today.ToString("yyyy-MM-dd"),
            yesterday.ToString("yyyy-MM-dd")
        };

        foreach (var file in Directory.GetFiles(DataDir, "metrics-*.db"))
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            var dateStr = fileName.Replace("metrics-", "");

            if (!keepDates.Contains(dateStr))
            {
                try
                {
                    File.Delete(file);
                    // Also delete WAL and SHM files
                    var walFile = file + "-wal";
                    var shmFile = file + "-shm";
                    if (File.Exists(walFile)) File.Delete(walFile);
                    if (File.Exists(shmFile)) File.Delete(shmFile);

                    _logger.LogInformation("Deleted old history file: {File}", fileName);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Cannot delete old history file: {File}", fileName);
                }
            }
        }
    }
}
