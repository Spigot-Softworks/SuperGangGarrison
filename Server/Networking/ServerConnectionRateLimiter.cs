using System.Net;

namespace OpenGarrison.Server;

internal sealed class ServerConnectionRateLimiter(
    int maxNewHelloAttemptsPerWindow,
    TimeSpan helloAttemptWindow,
    TimeSpan helloCooldown,
    int maxPasswordFailuresPerWindow,
    TimeSpan passwordFailureWindow,
    TimeSpan passwordCooldown,
    Func<TimeSpan> elapsedGetter)
{
    private readonly EndpointRateLimiter _helloRateLimiter = new(maxNewHelloAttemptsPerWindow, helloAttemptWindow, helloCooldown, elapsedGetter);
    private readonly EndpointRateLimiter _passwordRateLimiter = new(maxPasswordFailuresPerWindow, passwordFailureWindow, passwordCooldown, elapsedGetter);

    public void Prune()
    {
        _helloRateLimiter.Prune();
        _passwordRateLimiter.Prune();
    }

    public string? GetHelloRateLimitReason(IPEndPoint remoteEndPoint)
    {
        if (_passwordRateLimiter.IsLimited(remoteEndPoint, out var passwordRetryAfter))
        {
            return BuildRetryMessage("Too many password attempts", passwordRetryAfter);
        }

        if (!_helloRateLimiter.TryConsume(remoteEndPoint, out var helloRetryAfter))
        {
            return BuildRetryMessage("Too many connection attempts", helloRetryAfter);
        }

        return null;
    }

    public string? GetPasswordRateLimitReason(IPEndPoint remoteEndPoint)
    {
        if (!_passwordRateLimiter.IsLimited(remoteEndPoint, out var retryAfter))
        {
            return null;
        }

        return BuildRetryMessage("Too many password attempts", retryAfter);
    }

    public void RecordPasswordFailure(IPEndPoint remoteEndPoint)
    {
        _passwordRateLimiter.TryConsume(remoteEndPoint, out _);
    }

    public void ClearPasswordFailures(IPEndPoint remoteEndPoint)
    {
        _passwordRateLimiter.Reset(remoteEndPoint);
    }

    public void ResetConnectionAttemptLimits(IPEndPoint remoteEndPoint)
    {
        _helloRateLimiter.Reset(remoteEndPoint);
        _passwordRateLimiter.Reset(remoteEndPoint);
    }

    private static string BuildRetryMessage(string prefix, TimeSpan retryAfter)
    {
        var seconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
        return $"{prefix}. Try again in {seconds}s.";
    }
}
