#nullable enable

#if !BROWSER_KNI
using DiscordRPC;
using DiscordRPC.Logging;
using System;

using static OpenGarrison.Client.Game1;

namespace OpenGarrison.Client;

public sealed class DiscordRichPresenceController : IDisposable
{
    private static readonly TimeSpan DiscordMenuClientUpdateInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan DiscordOnlineClientUpdateInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DiscordOfflineClientUpdateInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan DiscordPresenceHeartbeatInterval = TimeSpan.FromMinutes(5);

    private readonly IDiscordContext _context;
    private readonly string _applicationId;
    private DiscordRpcClient? _client;
    private string _lastStateKey = string.Empty;
    private string _lastPublishedPresenceSignature = string.Empty;
    private DateTime _stateStartTimestampUtc;
    private DateTime _lastClientUpdateTimestampUtc;
    private DateTime _lastPublishTimestampUtc;
    private bool _failedInitialize;

    public DiscordRichPresenceController(IDiscordContext context)
    {
        _context = context;
        _applicationId = context.ResolveDiscordApplicationId();
        _stateStartTimestampUtc = DateTime.UtcNow;
    }

    public void Update()
    {
        if (string.IsNullOrWhiteSpace(_applicationId) || _failedInitialize)
        {
            return;
        }

        var nowUtc = DateTime.UtcNow;
        var stateKey = _context.BuildDiscordRichPresenceState();
        var stateChanged = !string.Equals(_lastStateKey, stateKey, StringComparison.Ordinal);
        if (stateChanged)
        {
            _lastStateKey = stateKey;
            _stateStartTimestampUtc = nowUtc;
            _lastPublishedPresenceSignature = string.Empty;
        }

        var clientUpdateInterval = GetClientUpdateInterval(stateKey);
        if (!stateChanged
            && _lastClientUpdateTimestampUtc != default
            && nowUtc - _lastClientUpdateTimestampUtc < clientUpdateInterval)
        {
            return;
        }

        _lastClientUpdateTimestampUtc = nowUtc;

        if (!EnsureClient())
        {
            return;
        }

        var presence = _context.BuildDiscordRichPresencePayload(_stateStartTimestampUtc);
        if (!ShouldPublishPresence(presence))
        {
            return;
        }

        try
        {
            var client = _client;
            if (client is null)
            {
                return;
            }

            client.SetPresence(presence);
            _lastPublishedPresenceSignature = BuildPresenceSignature(presence);
            _lastPublishTimestampUtc = nowUtc;
        }
        catch
        {
            _failedInitialize = true;
            _client?.Dispose();
            _client = null;
        }
    }

    private static TimeSpan GetClientUpdateInterval(string stateKey)
    {
        if (stateKey.StartsWith("online:", StringComparison.Ordinal))
        {
            return DiscordOnlineClientUpdateInterval;
        }

        if (stateKey.StartsWith("practice:", StringComparison.Ordinal)
            || stateKey.StartsWith("jump:", StringComparison.Ordinal)
            || stateKey.StartsWith("last_to_die:", StringComparison.Ordinal)
            || stateKey.StartsWith("replay:", StringComparison.Ordinal)
            || stateKey.Equals("garrison_builder", StringComparison.Ordinal))
        {
            return DiscordOfflineClientUpdateInterval;
        }

        return DiscordMenuClientUpdateInterval;
    }

    private bool ShouldPublishPresence(RichPresence presence)
    {
        var signature = BuildPresenceSignature(presence);
        if (!string.Equals(_lastPublishedPresenceSignature, signature, StringComparison.Ordinal))
        {
            return true;
        }

        return DateTime.UtcNow - _lastPublishTimestampUtc >= DiscordPresenceHeartbeatInterval;
    }

    private static string BuildPresenceSignature(RichPresence presence)
    {
        var partySize = presence.Party?.Size ?? 0;
        var partyMax = presence.Party?.Max ?? 0;
        return string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{presence.Details}|{presence.State}|{partySize}|{partyMax}");
    }

    private bool EnsureClient()
    {
        if (_client is not null)
        {
            return _client.IsInitialized;
        }

        try
        {
            var client = new DiscordRpcClient(_applicationId, pipe: -1, logger: null, autoEvents: true, client: null)
            {
                SkipIdenticalPresence = true,
                Logger = new NullLogger()
            };
            client.Initialize();
            _client = client;
            return client.IsInitialized;
        }
        catch
        {
            _failedInitialize = true;
            _client?.Dispose();
            _client = null;
            return false;
        }
    }

    public void Dispose()
    {
        if (_client is null)
        {
            return;
        }

        try
        {
            _client.ClearPresence();
        }
        catch
        {
        }

        _client.Dispose();
        _client = null;
    }

    private sealed class NullLogger : ILogger
    {
        public LogLevel Level { get; set; } = LogLevel.None;

        public void Trace(string message, params object[] args) { }

        public void Info(string message, params object[] args) { }

        public void Warning(string message, params object[] args) { }

        public void Error(string message, params object[] args) { }
    }
}
#endif
