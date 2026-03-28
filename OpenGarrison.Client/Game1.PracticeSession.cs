#nullable enable

using Microsoft.Xna.Framework;
using OpenGarrison.BotAI;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private bool IsPracticeSessionActive => _gameplaySessionKind == GameplaySessionKind.Practice;

    private void TryStartPracticeFromSetup()
    {
        var selectedMap = GetSelectedPracticeMapEntry();
        if (selectedMap is null)
        {
            _menuStatusMessage = "Select a local map before starting Practice.";
            return;
        }

        BeginPracticeSession(selectedMap.LevelName);
    }

    private void RestartPracticeSession()
    {
        if (!IsPracticeSessionActive)
        {
            return;
        }

        var levelName = _world.Level.Name;
        _practiceMapEntries = BuildPracticeMapEntries();
        _ = SelectPracticeMapEntry(levelName);
        BeginPracticeSession(levelName);
    }

    private void BeginPracticeSession(string levelName)
    {
        ResetPracticeBotManagerState(releaseWorldSlots: true);
        ResetPracticeNavigationState();
        _botDiagnosticLatestSnapshot = BotControllerDiagnosticsSnapshot.Empty;
        ResetBotDiagnosticSample();
        _networkClient.Disconnect();
        _networkClient.ClearPendingTeamSelection();
        _networkClient.ClearPendingClassSelection();
        StopHostedServer();
        StopLocalRapidFireWeaponAudio();
        StopMenuMusic();
        StopFaucetMusic();
        StopIngameMusic();
        ResetTransientPresentationEffects();
        ResetProcessedNetworkEventHistory();
        ResetClientTimingState();
        ResetSnapshotStateHistory();
        ResetBackstabVisuals();
        _lastAppliedSnapshotFrame = 0;
        _lastBufferedSnapshotFrame = 0;
        _hasReceivedSnapshot = false;
        _localPlayerSnapshotEntityId = null;
        _hasPredictedLocalPlayerPosition = false;
        _hasSmoothedLocalPlayerRenderPosition = false;
        _predictedLocalPlayerRenderCorrectionOffset = Vector2.Zero;
        _lastPredictedRenderSmoothingTimeSeconds = -1d;
        _pendingPredictedInputs.Clear();
        _pendingNetworkVisualEvents.Clear();
        _pendingNetworkDamageEvents.Clear();

        ReinitializeSimulationForTickRate(_practiceTickRate);
        _world.ConfigureMatchDefaults(
            timeLimitMinutes: _practiceTimeLimitMinutes,
            capLimit: _practiceCapLimit,
            respawnSeconds: _practiceRespawnSeconds);
        if (!_world.TryLoadLevel(levelName))
        {
            _menuStatusMessage = $"Failed to load practice map: {levelName}";
            return;
        }

        _world.PrepareLocalPlayerJoin();
        _gameplaySessionKind = GameplaySessionKind.Practice;
        LoadPracticeNavigationAssetsForCurrentLevel();
        _pendingHostedConnectTicks = -1;
        _pendingHostedConnectPort = 8190;
        _practiceSetupOpen = false;
        _mainMenuOpen = false;
        _manualConnectOpen = false;
        _hostSetupOpen = false;
        _hostSetupEditField = HostSetupEditField.None;
        _creditsOpen = false;
        _optionsMenuOpen = false;
        _optionsMenuOpenedFromGameplay = false;
        _pluginOptionsMenuOpen = false;
        _pluginOptionsMenuOpenedFromGameplay = false;
        _controlsMenuOpen = false;
        _controlsMenuOpenedFromGameplay = false;
        _inGameMenuOpen = false;
        _quitPromptOpen = false;
        _quitPromptHoverIndex = -1;
        _consoleOpen = false;
        _passwordPromptOpen = false;
        _passwordEditBuffer = string.Empty;
        _passwordPromptMessage = string.Empty;
        _teamSelectOpen = true;
        _classSelectOpen = false;
        _pendingClassSelectTeam = null;
        _menuStatusMessage = string.Empty;
        InitializePracticeBotNamePoolForMatch();
        ApplyPracticeDummyPreferencesBeforeJoin();
        AddConsoleLine($"practice started on {levelName} tickrate={_practiceTickRate}");
    }

    private void ApplyPracticeTeamSelection(PlayerTeam localTeam)
    {
        if (!IsPracticeSessionActive)
        {
            return;
        }

        if (_practiceEnemyDummyEnabled)
        {
            _world.SpawnEnemyDummy();
            _world.SetEnemyPlayerTeam(GetOpposingTeam(localTeam));
        }
        else
        {
            _world.DespawnEnemyDummy();
        }

        SyncPracticeBotRoster(localTeam);
        _world.DespawnFriendlyDummy();
    }

    private void ApplyPracticeDummyPreferencesBeforeJoin()
    {
        if (!IsPracticeSessionActive)
        {
            return;
        }

        if (_practiceEnemyDummyEnabled)
        {
            _world.SpawnEnemyDummy();
            _world.SetEnemyPlayerTeam(GetOpposingTeam(_world.LocalPlayerTeam));
        }
        else
        {
            _world.DespawnEnemyDummy();
        }

        _world.DespawnFriendlyDummy();
    }

    private void ApplyPracticeDummyPreferencesAfterJoin()
    {
        if (!IsPracticeSessionActive)
        {
            return;
        }

        SyncPracticeBotRoster(_world.LocalPlayerTeam);
        if (_practiceEnemyDummyEnabled)
        {
            _world.SpawnEnemyDummy();
            _world.SetEnemyPlayerTeam(GetOpposingTeam(_world.LocalPlayerTeam));
        }
        else
        {
            _world.DespawnEnemyDummy();
        }

        if (_practiceFriendlyDummyEnabled && !_world.LocalPlayerAwaitingJoin && _world.LocalPlayer.IsAlive)
        {
            _world.SpawnFriendlyDummy();
        }
        else
        {
            _world.DespawnFriendlyDummy();
        }
    }

    private string GetGameplayExitStatusMessage()
    {
        return IsPracticeSessionActive ? "Practice ended." : "Disconnected.";
    }

    private string GetOfflineSpectateUnavailableMessage()
    {
        return IsPracticeSessionActive
            ? "Spectator mode is not available in Practice."
            : "Spectator mode requires a network session.";
    }

    private static PlayerTeam GetOpposingTeam(PlayerTeam localTeam)
    {
        return localTeam == PlayerTeam.Blue ? PlayerTeam.Red : PlayerTeam.Blue;
    }
}
