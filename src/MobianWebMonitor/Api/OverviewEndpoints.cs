using Microsoft.AspNetCore.Authorization;
using MobianWebMonitor.Metrics;

namespace MobianWebMonitor.Api;

public static class OverviewEndpoints
{
    public static void MapOverviewEndpoints(this WebApplication app)
    {
        app.MapGet("/api/overview", [Authorize] (MetricsAggregator aggregator) =>
        {
            return Results.Ok(aggregator.Current);
        });
    }
}
