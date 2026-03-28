using System;
using System.Collections.Generic;
using System.Net;

sealed class EndpointRateLimiter
{
    private readonly int _maxAttempts;
    private readonly TimeSpan _window;
    private readonly TimeSpan _cooldown;
    private readonly Func<TimeSpan> _nowProvider;
    private readonly Dictionary<string, AttemptState> _states = new(StringComparer.Ordinal);

    public EndpointRateLimiter(int maxAttempts, TimeSpan window, TimeSpan cooldown, Func<TimeSpan> nowProvider)
    {
        _maxAttempts = Math.Max(1, maxAttempts);
        _window = window > TimeSpan.Zero ? window : TimeSpan.FromSeconds(1);
        _cooldown = cooldown > TimeSpan.Zero ? cooldown : TimeSpan.FromSeconds(1);
        _nowProvider = nowProvider;
    }

    public bool IsLimited(IPEndPoint endPoint, out TimeSpan retryAfter)
    {
        var now = _nowProvider();
        PruneExpired(now);
        if (!_states.TryGetValue(GetKey(endPoint), out var state) || state.BlockedUntil <= now)
        {
            retryAfter = TimeSpan.Zero;
            return false;
        }

        retryAfter = state.BlockedUntil - now;
        return true;
    }

    public bool TryConsume(IPEndPoint endPoint, out TimeSpan retryAfter)
    {
        var now = _nowProvider();
        var key = GetKey(endPoint);
        PruneExpired(now);

        if (!_states.TryGetValue(key, out var state))
        {
            state = new AttemptState(now);
            _states[key] = state;
        }
        else if (state.WindowStartedAt + _window <= now)
        {
            state.WindowStartedAt = now;
            state.Attempts = 0;
            state.BlockedUntil = TimeSpan.Zero;
        }
        else if (state.BlockedUntil > now)
        {
            retryAfter = state.BlockedUntil - now;
            return false;
        }
        else if (state.BlockedUntil != TimeSpan.Zero)
        {
            state.WindowStartedAt = now;
            state.Attempts = 0;
            state.BlockedUntil = TimeSpan.Zero;
        }

        state.Attempts += 1;
        if (state.Attempts > _maxAttempts)
        {
            state.BlockedUntil = now + _cooldown;
            retryAfter = _cooldown;
            return false;
        }

        retryAfter = TimeSpan.Zero;
        return true;
    }

    public void Reset(IPEndPoint endPoint)
    {
        _states.Remove(GetKey(endPoint));
    }

    public void Prune()
    {
        PruneExpired(_nowProvider());
    }

    private void PruneExpired(TimeSpan now)
    {
        if (_states.Count == 0)
        {
            return;
        }

        var expiredKeys = new List<string>();
        foreach (var entry in _states)
        {
            var state = entry.Value;
            var windowExpired = state.WindowStartedAt + _window <= now;
            var cooldownExpired = state.BlockedUntil == TimeSpan.Zero || state.BlockedUntil <= now;
            if (windowExpired && cooldownExpired)
            {
                expiredKeys.Add(entry.Key);
            }
        }

        for (var index = 0; index < expiredKeys.Count; index += 1)
        {
            _states.Remove(expiredKeys[index]);
        }
    }

    private static string GetKey(IPEndPoint endPoint)
    {
        return endPoint.Address.ToString();
    }

    private sealed class AttemptState(TimeSpan now)
    {
        public TimeSpan WindowStartedAt { get; set; } = now;
        public int Attempts { get; set; }
        public TimeSpan BlockedUntil { get; set; }
    }
}
