using MobianWebMonitor.Options;
using Microsoft.Extensions.Options;

namespace MobianWebMonitor.Hardware;

public sealed class LinuxGenericHardwareProfile : IHardwareProfile
{
    public string ProfileName => "Linux Generic";
    public string BatteryGaugeName { get; private set; } = string.Empty;
    public string ChargerName { get; private set; } = string.Empty;
    public IReadOnlyList<string> ThermalZoneNames { get; private set; } = [];
    public IReadOnlyList<string> ExtraPowerSupplyFields { get; } = [];

    public LinuxGenericHardwareProfile(IOptions<HostPathsOptions> hostPaths)
    {
        Detect(hostPaths.Value.SysRoot);
    }

    private void Detect(string sysRoot)
    {
        var psDir = Path.Combine(sysRoot, "class", "power_supply");
        if (!Directory.Exists(psDir)) return;

        foreach (var dir in Directory.GetDirectories(psDir))
        {
            var typePath = Path.Combine(dir, "type");
            if (!File.Exists(typePath)) continue;
            var type = File.ReadAllText(typePath).Trim();
            var name = Path.GetFileName(dir);

            if (type.Equals("Battery", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(BatteryGaugeName))
                BatteryGaugeName = name;
            else if (!type.Equals("Battery", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(ChargerName))
                ChargerName = name;
        }

        var thermalDir = Path.Combine(sysRoot, "class", "thermal");
        if (!Directory.Exists(thermalDir)) return;

        var zones = new List<string>();
        foreach (var dir in Directory.GetDirectories(thermalDir, "thermal_zone*"))
        {
            var typePath = Path.Combine(dir, "type");
            if (File.Exists(typePath))
                zones.Add(File.ReadAllText(typePath).Trim());
        }
        ThermalZoneNames = zones;
    }
}
