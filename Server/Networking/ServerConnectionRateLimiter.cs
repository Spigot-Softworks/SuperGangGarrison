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
        return GetHelloRateLimitReason(remoteEndPoint.Address);
    }

    public string? GetHelloRateLimitReason(IPAddress remoteAddress)
    {
        if (_passwordRateLimiter.IsLimited(remoteAddress, out var passwordRetryAfter))
        {
            return BuildRetryMessage("Too many password attempts", passwordRetryAfter);
        }

        if (!_helloRateLimiter.TryConsume(remoteAddress, out var helloRetryAfter))
        {
            return BuildRetryMessage("Too many connection attempts", helloRetryAfter);
        }

        return null;
    }

    public string? GetPasswordRateLimitReason(IPEndPoint remoteEndPoint)
    {
        return GetPasswordRateLimitReason(remoteEndPoint.Address);
    }

    public string? GetPasswordRateLimitReason(IPAddress remoteAddress)
    {
        if (!_passwordRateLimiter.IsLimited(remoteAddress, out var retryAfter))
        {
            return null;
        }

        return BuildRetryMessage("Too many password attempts", retryAfter);
    }

    public void RecordPasswordFailure(IPEndPoint remoteEndPoint)
    {
        RecordPasswordFailure(remoteEndPoint.Address);
    }

    public void RecordPasswordFailure(IPAddress remoteAddress)
    {
        _passwordRateLimiter.TryConsume(remoteAddress, out _);
    }

    public void ClearPasswordFailures(IPEndPoint remoteEndPoint)
    {
        ClearPasswordFailures(remoteEndPoint.Address);
    }

    public void ClearPasswordFailures(IPAddress remoteAddress)
    {
        _passwordRateLimiter.Reset(remoteAddress);
    }

    public void ResetConnectionAttemptLimits(IPEndPoint remoteEndPoint)
    {
        ResetConnectionAttemptLimits(remoteEndPoint.Address);
    }

    public void ResetConnectionAttemptLimits(IPAddress remoteAddress)
    {
        _helloRateLimiter.Reset(remoteAddress);
        _passwordRateLimiter.Reset(remoteAddress);
    }

    private static string BuildRetryMessage(string prefix, TimeSpan retryAfter)
    {
        var seconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
        return $"{prefix}. Try again in {seconds}s.";
    }
}
