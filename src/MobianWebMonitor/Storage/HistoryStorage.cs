using Microsoft.Data.Sqlite;
using MobianWebMonitor.Models;

namespace MobianWebMonitor.Storage;

public sealed class HistoryStorage : IDisposable
{
    private readonly string _dataDir;
    private readonly ILogger<HistoryStorage> _logger;
    private readonly Lock _lock = new();
    private SqliteConnection? _currentConnection;
    private string? _currentDate;

    public HistoryStorage(ILogger<HistoryStorage> logger)
    {
        _dataDir = "/data/history";
        _logger = logger;
    }

    public void Initialize()
    {
        Directory.CreateDirectory(_dataDir);
        EnsureCurrentDb();
    }

    public async Task WriteSampleAsync(DateTime tsUtc, string metricKey, double? value)
    {
        if (value == null) return;

        var conn = EnsureCurrentDb();
        if (conn == null) return;

        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO samples (ts_utc, metric_key, value_real) VALUES (@ts, @key, @val)";
            cmd.Parameters.AddWithValue("@ts", tsUtc.ToString("o"));
            cmd.Parameters.AddWithValue("@key", metricKey);
            cmd.Parameters.AddWithValue("@val", value.Value);
            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing sample {Key}", metricKey);
        }
    }

    public async Task WriteSamplesAsync(DateTime tsUtc, Dictionary<string, double?> samples)
    {
        var conn = EnsureCurrentDb();
        if (conn == null) return;

        try
        {
            using var transaction = conn.BeginTransaction();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO samples (ts_utc, metric_key, value_real) VALUES (@ts, @key, @val)";
            var tsParam = cmd.Parameters.Add("@ts", SqliteType.Text);
            var keyParam = cmd.Parameters.Add("@key", SqliteType.Text);
            var valParam = cmd.Parameters.Add("@val", SqliteType.Real);

            var tsStr = tsUtc.ToString("o");

            foreach (var (key, value) in samples)
            {
                if (value == null) continue;
                tsParam.Value = tsStr;
                keyParam.Value = key;
                valParam.Value = value.Value;
                await cmd.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing batch samples");
        }
    }

    public async Task<HistoryResponse> QueryAsync(string metricPrefix, DateTime from, DateTime to)
    {
        var response = new HistoryResponse
        {
            From = from,
            To = to,
            StepSeconds = CalculateStep(from, to)
        };

        try
        {
            var dates = GetDatesInRange(from, to);

            foreach (var date in dates)
            {
                var dbPath = GetDbPath(date);
                if (!File.Exists(dbPath)) continue;

                using var conn = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
                await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT ts_utc, metric_key, value_real
                    FROM samples
                    WHERE metric_key LIKE @prefix AND ts_utc >= @from AND ts_utc <= @to
                    ORDER BY ts_utc";
                cmd.Parameters.AddWithValue("@prefix", metricPrefix + "%");
                cmd.Parameters.AddWithValue("@from", from.ToString("o"));
                cmd.Parameters.AddWithValue("@to", to.ToString("o"));

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var tsStr = reader.GetString(0);
                    var key = reader.GetString(1);
                    var val = reader.IsDBNull(2) ? (double?)null : reader.GetDouble(2);

                    if (!DateTime.TryParse(tsStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var ts))
                        continue;

                    if (!response.Series.ContainsKey(key))
                        response.Series[key] = [];

                    response.Series[key].Add(new HistoryPoint
                    {
                        TimestampUtc = ts,
                        MetricKey = key,
                        Value = val
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying history for {Prefix}", metricPrefix);
        }

        return response;
    }

    private SqliteConnection? EnsureCurrentDb()
    {
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");

        lock (_lock)
        {
            if (_currentDate == today && _currentConnection != null)
                return _currentConnection;

            _currentConnection?.Dispose();
            var dbPath = GetDbPath(today);
            var isNew = !File.Exists(dbPath);

            try
            {
                _currentConnection = new SqliteConnection($"Data Source={dbPath}");
                _currentConnection.Open();

                if (isNew)
                {
                    using var cmd = _currentConnection.CreateCommand();
                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS samples (
                            id INTEGER PRIMARY KEY AUTOINCREMENT,
                            ts_utc TEXT NOT NULL,
                            metric_key TEXT NOT NULL,
                            value_real REAL NULL
                        );
                        CREATE INDEX IF NOT EXISTS ix_samples_metric_time ON samples(metric_key, ts_utc);
                        PRAGMA journal_mode=WAL;
                        PRAGMA synchronous=NORMAL;";
                    cmd.ExecuteNonQuery();

                    _logger.LogInformation("Created new history database: {Path}", dbPath);
                }

                _currentDate = today;
                return _currentConnection;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error opening SQLite database: {Path}", dbPath);
                _currentConnection = null;
                return null;
            }
        }
    }

    private string GetDbPath(string date) => Path.Combine(_dataDir, $"metrics-{date}.db");

    private static List<string> GetDatesInRange(DateTime from, DateTime to)
    {
        var dates = new List<string>();
        var current = from.Date;
        while (current <= to.Date)
        {
            dates.Add(current.ToString("yyyy-MM-dd"));
            current = current.AddDays(1);
        }
        return dates;
    }

    private static int CalculateStep(DateTime from, DateTime to)
    {
        var range = to - from;
        return range.TotalMinutes switch
        {
            <= 10 => 5,
            <= 60 => 15,
            <= 360 => 60,
            _ => 300
        };
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _currentConnection?.Dispose();
            _currentConnection = null;
        }
    }
}
