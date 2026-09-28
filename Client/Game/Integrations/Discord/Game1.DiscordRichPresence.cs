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
    public const double DiscordMenuPumpIntervalSeconds = 15d;
    public const double DiscordOnlinePumpIntervalSeconds = 5d;
    public const double DiscordOfflinePumpIntervalSeconds = 300d;
    public const string DefaultDiscordApplicationId = "1500219198834737273";
    public static readonly TimeSpan DiscordMenuClientUpdateInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan DiscordOnlineClientUpdateInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DiscordOfflineClientUpdateInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan DiscordPresenceHeartbeatInterval = TimeSpan.FromMinutes(5);

#if !BROWSER_KNI
    private DiscordRichPresenceController? _discordRichPresenceController;
#endif
    public int _practiceSessionElapsedTicks;

#if BROWSER_KNI
    private void UpdateDiscordRichPresence()
    {
    }

    private void PumpDiscordRichPresence(double elapsedSeconds)
    {
    }

    public void InvalidateDiscordRichPresenceRefresh()
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

    public void InvalidateDiscordRichPresenceRefresh()
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

        if (IsPracticeSessionActive || IsLastToDieSessionActive || IsJumpSessionActive)
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

    public string ResolveDiscordApplicationId()
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

    public string BuildDiscordRichPresenceState()
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

        if (IsJumpSessionActive)
        {
            return $"jump:{_world.Level.Name}";
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

    public RichPresence BuildDiscordRichPresencePayload(DateTime startTimestampUtc)
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

        if (IsJumpSessionActive)
        {
            return new RichPresence
            {
                Details = "Jump",
                State = _world.Level.Name,
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


#endif
}
