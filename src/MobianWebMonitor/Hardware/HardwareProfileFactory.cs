using MobianWebMonitor.Options;
using Microsoft.Extensions.Options;

namespace MobianWebMonitor.Hardware;

public static class HardwareProfileFactory
{
    public static IHardwareProfile Create(IOptions<HostPathsOptions> hostPaths, ILogger logger)
    {
        var sysRoot = hostPaths.Value.SysRoot;
        var psDir = Path.Combine(sysRoot, "class", "power_supply");

        if (Directory.Exists(psDir))
        {
            var dirs = Directory.GetDirectories(psDir).Select(Path.GetFileName).ToList();
            if (dirs.Contains("bq27411-0") && dirs.Contains("pmi8998-charger"))
            {
                logger.LogInformation("Detected OnePlus 6 hardware profile");
                return new OnePlus6HardwareProfile();
            }
        }

        logger.LogInformation("Using generic Linux hardware profile");
        return new LinuxGenericHardwareProfile(hostPaths);
    }
}
