using Microsoft.AspNetCore.Authorization;
using MobianWebMonitor.Storage;

namespace MobianWebMonitor.Api;

public static class HistoryEndpoints
{
    public static void MapHistoryEndpoints(this WebApplication app)
    {
        app.MapGet("/api/history/{metric}", [Authorize] async (
            string metric,
            string? range,
            HistoryStorage storage) =>
        {
            var duration = ParseRange(range ?? "5m");
            var to = DateTime.UtcNow;
            var from = to - duration;

            var prefix = metric switch
            {
                "cpu" => "cpu.",
                "ram" or "mem" => "mem.",
                "battery" or "bat" => "bat.",
                _ => metric + "."
            };

            var result = await storage.QueryAsync(prefix, from, to);
            return Results.Ok(result);
        });
    }

    private static TimeSpan ParseRange(string range)
    {
        if (range.EndsWith('m') && int.TryParse(range[..^1], out var minutes))
            return TimeSpan.FromMinutes(minutes);
        if (range.EndsWith('h') && int.TryParse(range[..^1], out var hours))
            return TimeSpan.FromHours(hours);
        if (range.EndsWith('d') && int.TryParse(range[..^1], out var days))
            return TimeSpan.FromDays(days);
        return TimeSpan.FromMinutes(5);
    }
}
