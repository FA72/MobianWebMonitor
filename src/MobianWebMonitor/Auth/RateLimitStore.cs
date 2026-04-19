using System.Collections.Concurrent;

namespace MobianWebMonitor.Auth;

public sealed class RateLimitStore
{
    private readonly ConcurrentDictionary<string, ClientAttempts> _attempts = new();
    private readonly int _maxAttempts;
    private readonly TimeSpan _lockoutDuration;

    public RateLimitStore(int maxAttempts = 5, int lockoutMinutes = 15)
    {
        _maxAttempts = maxAttempts;
        _lockoutDuration = TimeSpan.FromMinutes(lockoutMinutes);
    }

    public bool IsBlocked(string clientIp)
    {
        if (!_attempts.TryGetValue(clientIp, out var attempts))
            return false;

        if (attempts.FailedCount >= _maxAttempts)
        {
            if (DateTime.UtcNow - attempts.LastFailedAt < _lockoutDuration)
                return true;

            // Lockout expired, reset
            _attempts.TryRemove(clientIp, out _);
            return false;
        }

        return false;
    }

    public void RecordFailure(string clientIp)
    {
        var attempts = _attempts.GetOrAdd(clientIp, _ => new ClientAttempts());
        Interlocked.Increment(ref attempts.FailedCount);
        attempts.LastFailedAt = DateTime.UtcNow;
    }

    public void RecordSuccess(string clientIp)
    {
        _attempts.TryRemove(clientIp, out _);
    }

    public TimeSpan GetDelay(string clientIp)
    {
        if (!_attempts.TryGetValue(clientIp, out var attempts))
            return TimeSpan.Zero;

        // Progressive delay: 1s, 2s, 4s, 8s...
        var delaySec = Math.Min(Math.Pow(2, attempts.FailedCount - 1), 30);
        return TimeSpan.FromSeconds(delaySec);
    }

    private sealed class ClientAttempts
    {
        public int FailedCount;
        public DateTime LastFailedAt;
    }
}
