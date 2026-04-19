namespace MobianWebMonitor.Options;

public sealed class HostPathsOptions
{
    public const string Section = "HostPaths";

    public string ProcRoot { get; set; } = "/proc";
    public string SysRoot { get; set; } = "/sys";
}
