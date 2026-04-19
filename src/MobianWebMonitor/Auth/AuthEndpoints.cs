using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace MobianWebMonitor.Auth;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        app.MapPost("/auth/login", async (HttpContext ctx, PasswordService passwordService, RateLimitStore rateLimiter) =>
        {
            var clientIp = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            if (rateLimiter.IsBlocked(clientIp))
                return Results.Json(new { success = false, message = "Too many attempts. Try again later." },
                    statusCode: 429);

            var delay = rateLimiter.GetDelay(clientIp);
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay);

            var form = await ctx.Request.ReadFormAsync();
            var password = form["password"].ToString();

            if (string.IsNullOrEmpty(password) || !passwordService.Verify(password))
            {
                rateLimiter.RecordFailure(clientIp);
                return Results.Json(new { success = false, message = "Access denied." },
                    statusCode: 401);
            }

            rateLimiter.RecordSuccess(clientIp);

            var claims = new List<Claim> { new(ClaimTypes.Name, "monitor-user") };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            return Results.Json(new { success = true });
        }).AllowAnonymous();

        app.MapPost("/auth/logout", async (HttpContext ctx) =>
        {
            await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Json(new { success = true });
        });
    }
}
