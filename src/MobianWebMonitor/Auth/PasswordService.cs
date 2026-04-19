using Microsoft.AspNetCore.Identity;
using MobianWebMonitor.Options;
using Microsoft.Extensions.Options;

namespace MobianWebMonitor.Auth;

public sealed class PasswordService
{
    private readonly string _storedHash;
    private readonly PasswordHasher<string> _hasher = new();

    public PasswordService(IOptions<AuthOptions> authOptions)
    {
        _storedHash = authOptions.Value.PasswordHash;
    }

    public bool Verify(string inputPassword)
    {
        if (string.IsNullOrEmpty(_storedHash) || string.IsNullOrEmpty(inputPassword))
            return false;

        var result = _hasher.VerifyHashedPassword("monitor", _storedHash, inputPassword);
        return result == PasswordVerificationResult.Success ||
               result == PasswordVerificationResult.SuccessRehashNeeded;
    }

    /// <summary>
    /// Utility method to generate a hash for a given password.
    /// Used by the hash generation tool.
    /// </summary>
    public static string GenerateHash(string password)
    {
        var hasher = new PasswordHasher<string>();
        return hasher.HashPassword("monitor", password);
    }
}
