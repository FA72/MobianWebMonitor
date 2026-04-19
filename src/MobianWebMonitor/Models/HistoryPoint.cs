namespace MobianWebMonitor.Models;

public sealed class HistoryPoint
{
    public DateTime TimestampUtc { get; set; }
    public string MetricKey { get; set; } = string.Empty;
    public double? Value { get; set; }
}

public sealed class HistoryResponse
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public int StepSeconds { get; set; }
    public Dictionary<string, List<HistoryPoint>> Series { get; set; } = [];
}
