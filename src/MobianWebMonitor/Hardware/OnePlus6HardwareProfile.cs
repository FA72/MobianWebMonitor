namespace MobianWebMonitor.Hardware;

public sealed class OnePlus6HardwareProfile : IHardwareProfile
{
    public string ProfileName => "OnePlus 6 (sdm845)";
    public string BatteryGaugeName => "bq27411-0";
    public string ChargerName => "pmi8998-charger";
    public IReadOnlyList<string> ThermalZoneNames { get; } = ["cpu-thermal", "battery"];
    public IReadOnlyList<string> ExtraPowerSupplyFields { get; } = ["current_max", "input_current_limit"];
}
