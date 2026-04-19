namespace MobianWebMonitor.Options;

public sealed class AuthOptions
{
    public const string Section = "Auth";

    /// <summary>
    /// PBKDF2 hash of the shared secret, stored in MONITOR_AUTH_HASH env var.
    /// Format: base64 string from ASP.NET Core Identity PasswordHasher.
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;

    public int MaxFailedAttempts { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 15;
}
