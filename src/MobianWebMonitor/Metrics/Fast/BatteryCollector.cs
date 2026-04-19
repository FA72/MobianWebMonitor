using MobianWebMonitor.Hardware;
using MobianWebMonitor.Models;
using MobianWebMonitor.Options;
using Microsoft.Extensions.Options;

namespace MobianWebMonitor.Metrics.Fast;

public sealed class BatteryCollector
{
    private readonly string _sysRoot;
    private readonly IHardwareProfile _profile;
    private readonly ILogger<BatteryCollector> _logger;
    private bool _errorLogged;

    public BatteryCollector(IOptions<HostPathsOptions> hostPaths, IHardwareProfile profile, ILogger<BatteryCollector> logger)
    {
        _sysRoot = hostPaths.Value.SysRoot;
        _profile = profile;
        _logger = logger;
    }

    public BatteryInfo Collect()
    {
        var info = new BatteryInfo();

        try
        {
            var psDir = Path.Combine(_sysRoot, "class", "power_supply");
            if (!Directory.Exists(psDir))
            {
                LogOnce("Power supply directory not found: {Path}", psDir);
                return info;
            }

            // Read battery gauge
            if (!string.IsNullOrEmpty(_profile.BatteryGaugeName))
            {
                var gaugeDir = Path.Combine(psDir, _profile.BatteryGaugeName);
                if (Directory.Exists(gaugeDir))
                {
                    info.CapacityPercent = ReadIntFile(gaugeDir, "capacity");
                    info.Status = ReadStringFile(gaugeDir, "status");
                    info.TemperatureCelsius = ReadIntFile(gaugeDir, "temp") is int t ? t / 10.0 : null;
                    info.CurrentMicroAmps = ReadIntFile(gaugeDir, "current_now");
                    info.VoltageMicroVolts = ReadIntFile(gaugeDir, "voltage_now");
                    info.Health = ReadStringFile(gaugeDir, "health");
                }
            }

            // Read charger
            if (!string.IsNullOrEmpty(_profile.ChargerName))
            {
                var chargerDir = Path.Combine(psDir, _profile.ChargerName);
                if (Directory.Exists(chargerDir))
                {
                    info.ChargeStatus = ReadStringFile(chargerDir, "status");
                    info.ExternalPowered = ReadStringFile(chargerDir, "online") == "1";
                    info.ChargerType = ReadStringFile(chargerDir, "type");
                    info.CurrentMaxMicroAmps = ReadIntFile(chargerDir, "current_max");
                }
            }

            _errorLogged = false;
        }
        catch (Exception ex)
        {
            LogOnce("Error collecting battery metrics: {Error}", ex.Message);
        }

        return info;
    }

    private static int? ReadIntFile(string dir, string fileName)
    {
        var path = Path.Combine(dir, fileName);
        if (!File.Exists(path)) return null;
        var text = File.ReadAllText(path).Trim();
        return int.TryParse(text, out var val) ? val : null;
    }

    private static string? ReadStringFile(string dir, string fileName)
    {
        var path = Path.Combine(dir, fileName);
        if (!File.Exists(path)) return null;
        var text = File.ReadAllText(path).Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    private void LogOnce(string message, params object[] args)
    {
        if (_errorLogged) return;
        _errorLogged = true;
        _logger.LogWarning(message, args);
    }
}
