namespace MobianWebMonitor.Hardware;

public interface IHardwareProfile
{
    string ProfileName { get; }
    string BatteryGaugeName { get; }
    string ChargerName { get; }
    IReadOnlyList<string> ThermalZoneNames { get; }
    IReadOnlyList<string> ExtraPowerSupplyFields { get; }
}
