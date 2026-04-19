using System.Diagnostics;
using MobianWebMonitor.Models;

namespace MobianWebMonitor.Metrics.Slow;

public sealed class SystemdServiceCollector
{
    private readonly ILogger<SystemdServiceCollector> _logger;
    private bool _errorLogged;

    public SystemdServiceCollector(ILogger<SystemdServiceCollector> logger)
    {
        _logger = logger;
    }

    public async Task<ServiceStatusInfo> CollectAsync(string unitName, CancellationToken ct)
    {
        var result = new ServiceStatusInfo();

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "systemctl",
                Arguments = $"--system show {unitName} --property=ActiveState,SubState,ExecMainStartTimestamp --no-pager",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                LogOnce("Failed to start systemctl process");
                return result;
            }

            var output = await process.StandardOutput.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var eqIndex = line.IndexOf('=');
                if (eqIndex < 0) continue;

                var key = line[..eqIndex].Trim();
                var value = line[(eqIndex + 1)..].Trim();

                switch (key)
                {
                    case "ActiveState":
                        result.ActiveState = value;
                        break;
                    case "SubState":
                        result.SubState = value;
                        break;
                    case "ExecMainStartTimestamp":
                        if (DateTimeOffset.TryParse(value, out var ts))
                            result.StartedAt = ts.UtcDateTime;
                        break;
                }
            }

            _errorLogged = false;
        }
        catch (Exception ex)
        {
            LogOnce("Error collecting systemd service status for {Unit}: {Error}", unitName, ex.Message);
        }

        return result;
    }

    private void LogOnce(string message, params object[] args)
    {
        if (_errorLogged) return;
        _errorLogged = true;
        _logger.LogWarning(message, args);
    }
}
