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
            var unitPath = $"/org/freedesktop/systemd1/unit/{EscapeUnitName(unitName)}";

            result.ActiveState = await ReadBusctlStringPropertyAsync(unitPath, "ActiveState", unitName, ct) ?? result.ActiveState;
            result.SubState = await ReadBusctlStringPropertyAsync(unitPath, "SubState", unitName, ct) ?? result.SubState;

            _errorLogged = false;
        }
        catch (Exception ex)
        {
            LogOnce("Error collecting systemd service status for {Unit}: {Error}", unitName, ex.Message);
        }

        return result;
    }

    private async Task<string?> ReadBusctlStringPropertyAsync(string unitPath, string propertyName, string unitName, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "busctl",
            Arguments = $"--system get-property org.freedesktop.systemd1 {unitPath} org.freedesktop.systemd1.Unit {propertyName}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(psi);
        if (process == null)
        {
            LogOnce("Failed to start busctl process");
            return null;
        }

        var output = await process.StandardOutput.ReadToEndAsync(ct);
        var error = await process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            LogOnce("busctl exited with code {Code} for {Unit} {Property}: {Error}", process.ExitCode, unitName, propertyName, error.Trim());
            return null;
        }

        var value = ParseBusctlString(output);
        if (string.IsNullOrEmpty(value))
        {
            LogOnce("busctl returned no value for {Unit} {Property}", unitName, propertyName);
        }

        return value;
    }

    private static string EscapeUnitName(string unitName)
    {
        var chars = new System.Text.StringBuilder(unitName.Length * 2);

        foreach (var ch in unitName)
        {
            if (char.IsAsciiLetterOrDigit(ch))
            {
                chars.Append(ch);
            }
            else
            {
                chars.Append('_');
                chars.Append(((int)ch).ToString("x2"));
            }
        }

        return chars.ToString();
    }

    private static string? ParseBusctlString(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return null;

        var trimmed = output.Trim();
        var firstQuote = trimmed.IndexOf('"');
        var lastQuote = trimmed.LastIndexOf('"');

        if (firstQuote >= 0 && lastQuote > firstQuote)
            return trimmed[(firstQuote + 1)..lastQuote];

        var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? parts[1] : null;
    }

    private void LogOnce(string message, params object[] args)
    {
        if (_errorLogged) return;
        _errorLogged = true;
        _logger.LogWarning(message, args);
    }
}
