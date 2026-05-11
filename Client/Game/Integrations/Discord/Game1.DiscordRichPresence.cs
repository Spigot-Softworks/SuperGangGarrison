#nullable enable

#if !BROWSER_KNI
using DiscordRPC;
using DiscordRPC.Logging;
using OpenGarrison.Core;
using System;
using System.Globalization;
using System.Linq;
#endif

namespace OpenGarrison.Client;

public partial class Game1
{
    private const double DiscordMenuPumpIntervalSeconds = 15d;
    private const double DiscordOnlinePumpIntervalSeconds = 5d;
    private const double DiscordOfflinePumpIntervalSeconds = 300d;
    private const string DefaultDiscordApplicationId = "1500219198834737273";
    private static readonly TimeSpan DiscordMenuClientUpdateInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan DiscordOnlineClientUpdateInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DiscordOfflineClientUpdateInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan DiscordPresenceHeartbeatInterval = TimeSpan.FromMinutes(5);

#if !BROWSER_KNI
    private DiscordRichPresenceController? _discordRichPresenceController;
#endif
    private int _practiceSessionElapsedTicks;

#if BROWSER_KNI
    private void UpdateDiscordRichPresence()
    {
    }

    private void PumpDiscordRichPresence(double elapsedSeconds)
    {
    }

    private void InvalidateDiscordRichPresenceRefresh()
    {
    }

    private void ShutdownDiscordRichPresence()
    {
    }
#else
    private double _discordRichPresenceSecondsUntilNextPump;
    private bool _discordRichPresenceRefreshPending = true;

    private void UpdateDiscordRichPresence()
    {
        if (OperatingSystem.IsBrowser() || !OperatingSystem.IsWindows())
        {
            return;
        }

        _discordRichPresenceController ??= new DiscordRichPresenceController(this);
        _discordRichPresenceController.Update();
    }

    private void PumpDiscordRichPresence(double elapsedSeconds)
    {
        if (OperatingSystem.IsBrowser() || !OperatingSystem.IsWindows())
        {
            return;
        }

        if (_discordRichPresenceRefreshPending)
        {
            UpdateDiscordRichPresence();
            _discordRichPresenceRefreshPending = false;
            _discordRichPresenceSecondsUntilNextPump = GetDiscordRichPresencePumpIntervalSeconds();
            return;
        }

        _discordRichPresenceSecondsUntilNextPump = Math.Max(0d, _discordRichPresenceSecondsUntilNextPump - Math.Max(0d, elapsedSeconds));
        if (_discordRichPresenceSecondsUntilNextPump > 0d)
        {
            return;
        }

        UpdateDiscordRichPresence();
        _discordRichPresenceSecondsUntilNextPump = GetDiscordRichPresencePumpIntervalSeconds();
    }

    private void InvalidateDiscordRichPresenceRefresh()
    {
        _discordRichPresenceRefreshPending = true;
        _discordRichPresenceSecondsUntilNextPump = 0d;
    }

    private double GetDiscordRichPresencePumpIntervalSeconds()
    {
        if (_builderEditorEnabled)
        {
            return DiscordMenuPumpIntervalSeconds;
        }

        if (_networkClient.IsConnected && _networkClient.IsReplayConnection)
        {
            return DiscordOfflinePumpIntervalSeconds;
        }

        if (_networkClient.IsConnected && _gameplaySessionKind == GameplaySessionKind.Online)
        {
            return DiscordOnlinePumpIntervalSeconds;
        }

        if (IsPracticeSessionActive || IsLastToDieSessionActive)
        {
            return DiscordOfflinePumpIntervalSeconds;
        }

        return DiscordMenuPumpIntervalSeconds;
    }

    private void ShutdownDiscordRichPresence()
    {
        _discordRichPresenceController?.Dispose();
        _discordRichPresenceController = null;
    }

    private string ResolveDiscordApplicationId()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable("OG_DISCORD_APP_ID");
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return fromEnvironment.Trim();
        }

        var fromSettings = _clientSettings.DiscordApplicationId?.Trim();
        if (!string.IsNullOrWhiteSpace(fromSettings))
        {
            return fromSettings;
        }

        return DefaultDiscordApplicationId;
    }

    private string BuildDiscordRichPresenceState()
    {
        if (_builderEditorEnabled)
        {
            return "garrison_builder";
        }

        if (_mainMenuOpen || _gameplaySessionKind == GameplaySessionKind.None)
        {
            return "main_menu";
        }

        if (IsLastToDieSessionActive && _lastToDieRun is not null)
        {
            return $"last_to_die:{_lastToDieRun.CurrentLevelName}:{_lastToDieRun.StageNumber}:{_lastToDieRun.SurvivorKind}";
        }

        if (IsPracticeSessionActive)
        {
            return $"practice:{_world.Level.Name}";
        }

        if (_networkClient.IsConnected && _networkClient.IsReplayConnection)
        {
            return $"replay:{_networkClient.ReplayDisplayName}:{_networkClient.ReplayServerName}:{ResolveDiscordReplayMapName()}:{FormatDiscordReplayDate(_networkClient.ReplayDateUtc)}";
        }

        if (_networkClient.IsConnected && _gameplaySessionKind == GameplaySessionKind.Online)
        {
            return $"online:{_networkClient.ServerDescription}:{_world.Level.Name}:{GetDiscordOnlineTakenSlots()}:{_networkClient.ServerMaxPlayerCount}";
        }

        return "main_menu";
    }

    private RichPresence BuildDiscordRichPresencePayload(DateTime startTimestampUtc)
    {
        if (_builderEditorEnabled)
        {
            return new RichPresence
            {
                Details = "Garrison Builder",
                Timestamps = new Timestamps(startTimestampUtc)
            };
        }

        if (_mainMenuOpen || _gameplaySessionKind == GameplaySessionKind.None)
        {
            return new RichPresence
            {
                Details = "Main Menu",
                State = "Idle"
            };
        }

        if (IsLastToDieSessionActive && _lastToDieRun is not null)
        {
            var classId = GetLastToDieSurvivorPlayerClass(_lastToDieRun.SurvivorKind);
            return new RichPresence
            {
                Details = $"Last to Die | {classId}",
                State = $"Round {_lastToDieRun.StageNumber}",
                Timestamps = new Timestamps(startTimestampUtc)
            };
        }

        if (IsPracticeSessionActive)
        {
            return new RichPresence
            {
                Details = $"Practice | {_world.Level.Name}",
                Timestamps = new Timestamps(startTimestampUtc)
            };
        }

        if (_networkClient.IsConnected && _networkClient.IsReplayConnection)
        {
            return new RichPresence
            {
                Details = "Watching Replay",
                State = $"{ResolveDiscordReplayServerName()}, {ResolveDiscordReplayMapName()}, {FormatDiscordReplayDate(_networkClient.ReplayDateUtc)}",
                Timestamps = new Timestamps(startTimestampUtc)
            };
        }

        if (_networkClient.IsConnected && _gameplaySessionKind == GameplaySessionKind.Online)
        {
            var takenSlots = GetDiscordOnlineTakenSlots();
            var maxSlots = Math.Max(0, _networkClient.ServerMaxPlayerCount);
            var openSlots = Math.Max(0, maxSlots - takenSlots);
            var state = maxSlots > 0
                ? $"{_world.Level.Name} | {takenSlots}/{maxSlots}"
                : $"{_world.Level.Name} | {takenSlots} players";

            var presence = new RichPresence
            {
                Details = $"Online | {_networkClient.ServerDescription ?? "Connected"}",
                State = state,
                Timestamps = new Timestamps(startTimestampUtc)
            };

            if (maxSlots > 0)
            {
                presence.Party = new Party
                {
                    Size = Math.Max(0, takenSlots),
                    Max = maxSlots,
                    Privacy = Party.PrivacySetting.Public
                };
            }

            return presence;
        }

        return new RichPresence
        {
            Details = "Main Menu",
            State = "Idle"
        };
    }

    private int GetDiscordOnlineTakenSlots()
    {
        var takenSlots = _world.RemoteSnapshotPlayers.Count;
        if (!_networkClient.IsSpectator)
        {
            takenSlots += 1;
        }

        return takenSlots;
    }

    private string ResolveDiscordReplayServerName()
    {
        return FirstNonBlank(
            _networkClient.ReplayServerName,
            _networkClient.ServerDescription,
            _networkClient.ReplayDisplayName,
            "Replay");
    }

    private string ResolveDiscordReplayMapName()
    {
        return FirstNonBlank(
            _networkClient.ReplayMapName,
            _world.Level.Name,
            "Unknown Map");
    }

    private static string FormatDiscordReplayDate(DateTime? replayDateUtc)
    {
        if (replayDateUtc is null)
        {
            return "Unknown Date";
        }

        return replayDateUtc.Value.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static string FirstNonBlank(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }

    private static string FormatDiscordDuration(int elapsedTicks, int ticksPerSecond)
    {
        if (ticksPerSecond <= 0)
        {
            return "00:00";
        }

        var elapsedSeconds = Math.Max(0, elapsedTicks / ticksPerSecond);
        var minutes = elapsedSeconds / 60;
        var seconds = elapsedSeconds % 60;
        return $"{minutes:00}:{seconds:00}";
    }

    private sealed class DiscordRichPresenceController : IDisposable
    {
        private readonly Game1 _game;
        private readonly string _applicationId;
        private DiscordRpcClient? _client;
        private string _lastStateKey = string.Empty;
        private string _lastPublishedPresenceSignature = string.Empty;
        private DateTime _stateStartTimestampUtc;
        private DateTime _lastClientUpdateTimestampUtc;
        private DateTime _lastPublishTimestampUtc;
        private bool _failedInitialize;

        public DiscordRichPresenceController(Game1 game)
        {
            _game = game;
            _applicationId = game.ResolveDiscordApplicationId();
            _stateStartTimestampUtc = DateTime.UtcNow;
        }

        public void Update()
        {
            if (string.IsNullOrWhiteSpace(_applicationId) || _failedInitialize)
            {
                return;
            }

            var nowUtc = DateTime.UtcNow;
            var stateKey = _game.BuildDiscordRichPresenceState();
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

            var presence = _game.BuildDiscordRichPresencePayload(_stateStartTimestampUtc);
            var shouldPublish = ShouldPublishPresence(presence);
            if (!shouldPublish)
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

            public void Trace(string message, params object[] args)
            {
            }

            public void Info(string message, params object[] args)
            {
            }

            public void Warning(string message, params object[] args)
            {
            }

            public void Error(string message, params object[] args)
            {
            }
        }
    }
#endif
}
