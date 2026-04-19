using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using MobianWebMonitor.Auth;
using MobianWebMonitor.Api;
using MobianWebMonitor.Components;
using MobianWebMonitor.Hardware;
using MobianWebMonitor.Hubs;
using MobianWebMonitor.Metrics;
using MobianWebMonitor.Metrics.Fast;
using MobianWebMonitor.Metrics.Slow;
using MobianWebMonitor.Metrics.Services;
using MobianWebMonitor.Middleware;
using MobianWebMonitor.Options;
using MobianWebMonitor.Storage;

// CLI: generate password hash and exit
if (args.Length >= 2 && args[0] == "--generate-hash")
{
    var hasher = new PasswordHasher<object>();
    var hash = hasher.HashPassword(new object(), args[1]);
    Console.WriteLine(hash);
    return;
}

// CLI: container healthcheck
if (args.Length >= 1 && args[0] == "--healthcheck")
{
    using var client = new HttpClient();
    try
    {
        var response = await client.GetAsync("http://localhost:8082/login");
        return response.IsSuccessStatusCode ? 0 : 1;
    }
    catch
    {
        return 1;
    }
}

var builder = WebApplication.CreateBuilder(args);

// Options
builder.Services.Configure<HostPathsOptions>(builder.Configuration.GetSection("HostPaths"));
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection("Auth"));

// Auth
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/login";
        o.Cookie.Name = "MobianMonitor";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Strict;
        o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        o.ExpireTimeSpan = TimeSpan.FromDays(7);
        o.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();

// Core services
builder.Services.AddSingleton<PasswordService>();
builder.Services.AddSingleton(sp =>
{
    var opts = sp.GetRequiredService<IOptions<AuthOptions>>().Value;
    return new RateLimitStore(opts.MaxFailedAttempts, opts.LockoutMinutes);
});
builder.Services.AddSingleton<IHardwareProfile>(sp =>
    HardwareProfileFactory.Create(
        sp.GetRequiredService<IOptions<HostPathsOptions>>(),
        sp.GetRequiredService<ILoggerFactory>().CreateLogger("HardwareProfile")));
builder.Services.AddSingleton<MetricsAggregator>();
builder.Services.AddSingleton<HistoryStorage>();

// Collectors
builder.Services.AddSingleton<CpuCollector>();
builder.Services.AddSingleton<MemoryCollector>();
builder.Services.AddSingleton<BatteryCollector>();
builder.Services.AddSingleton<DiskCollector>();
builder.Services.AddSingleton<DockerCollector>();
builder.Services.AddSingleton<SystemdServiceCollector>();

// Background services
builder.Services.AddHostedService<FastMetricsService>();
builder.Services.AddHostedService<SlowMetricsService>();
builder.Services.AddHostedService<HistoryCleanupService>();

// SignalR
builder.Services.AddSignalR();

// Blazor
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Initialize history storage
app.Services.GetRequiredService<HistoryStorage>().Initialize();

// Middleware
app.UseMiddleware<SecurityHeadersMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();

// Endpoints
app.MapAuthEndpoints();
app.MapOverviewEndpoints();
app.MapHistoryEndpoints();

// SignalR
app.MapHub<MetricsHub>("/hubs/metrics");

// Blazor
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
