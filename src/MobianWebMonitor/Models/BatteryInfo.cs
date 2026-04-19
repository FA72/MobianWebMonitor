namespace MobianWebMonitor.Models;

public sealed class BatteryInfo
{
    public int? CapacityPercent { get; set; }
    public string? Status { get; set; }
    public string? ChargeStatus { get; set; }
    public double? TemperatureCelsius { get; set; }
    public int? CurrentMicroAmps { get; set; }
    public int? VoltageMicroVolts { get; set; }
    public string? Health { get; set; }
    public bool? ExternalPowered { get; set; }
    public string? ChargerType { get; set; }
    public int? CurrentMaxMicroAmps { get; set; }
    public string? BatteryLimiterStatus { get; set; }
}
