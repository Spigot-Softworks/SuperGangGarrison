#nullable enable

using Microsoft.Xna.Framework;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void ResetGameplayRuntimeState()
    {
        ResetClientTimingState();
        _lastAppliedSnapshotFrame = 0;
        _lastBufferedSnapshotFrame = 0;
        _hasReceivedSnapshot = false;
        _lastSnapshotReceivedTimeSeconds = -1d;
        _latestSnapshotServerTimeSeconds = -1d;
        _latestSnapshotReceivedClockSeconds = -1d;
        _networkSnapshotInterpolationDurationSeconds = 1f / _config.TicksPerSecond;
        _smoothedSnapshotIntervalSeconds = 1f / _config.TicksPerSecond;
        _smoothedSnapshotJitterSeconds = 0f;
        _remotePlayerInterpolationBackTimeSeconds = RemotePlayerMinimumInterpolationBackTimeSeconds;
        _remotePlayerRenderTimeSeconds = 0d;
        _lastRemotePlayerRenderTimeClockSeconds = -1d;
        _hasRemotePlayerRenderTime = false;
        _pendingNetworkVisualEvents.Clear();
        _pendingNetworkDamageEvents.Clear();
        ResetBackstabVisuals();
        _hasPredictedLocalPlayerPosition = false;
        _hasSmoothedLocalPlayerRenderPosition = false;
        _predictedLocalPlayerRenderCorrectionOffset = Vector2.Zero;
        _lastPredictedRenderSmoothingTimeSeconds = -1d;
        _pendingPredictedInputs.Clear();
        _localPlayerSnapshotEntityId = null;
        _entityInterpolationTracks.Clear();
        _intelInterpolationTracks.Clear();
        _entitySnapshotHistories.Clear();
        _intelSnapshotHistories.Clear();
        _remotePlayerSnapshotHistories.Clear();
        ResetSnapshotStateHistory();
        _interpolatedEntityPositions.Clear();
        _interpolatedIntelPositions.Clear();
    }

    private void ResetGameplayTransitionEffects()
    {
        StopLocalRapidFireWeaponAudio();
        StopIngameMusic();
        ResetTransientPresentationEffects();
        ResetProcessedNetworkEventHistory();
    }

    private void CloseGameplayOverlayState()
    {
        _practiceSetupOpen = false;
        _lastToDieMenuOpen = false;
        _lastToDiePerkMenuOpen = false;
        _clientPowersOpen = false;
        _clientPowersOpenedFromGameplay = false;
        _optionsMenuOpen = false;
        _optionsMenuOpenedFromGameplay = false;
        _pluginOptionsMenuOpen = false;
        _pluginOptionsMenuOpenedFromGameplay = false;
        _controlsMenuOpen = false;
        _controlsMenuOpenedFromGameplay = false;
        _inGameMenuOpen = false;
        _quitPromptOpen = false;
        _quitPromptHoverIndex = -1;
        _pendingControlsBinding = null;
        _teamSelectOpen = false;
        _classSelectOpen = false;
        _pendingClassSelectTeam = null;
        _consoleOpen = false;
        _scoreboardOpen = false;
        ResetChatInputState();
        _bubbleMenuKind = BubbleMenuKind.None;
        _bubbleMenuClosing = false;
        _passwordPromptOpen = false;
        _passwordEditBuffer = string.Empty;
        _passwordPromptMessage = string.Empty;
    }

    private void CloseMainMenuOverlayState()
    {
        CloseLobbyBrowser(clearStatus: false);
        _manualConnectOpen = false;
        _hostSetupOpen = false;
        _hostSetupEditField = HostSetupEditField.None;
        _creditsOpen = false;
        _editingConnectHost = false;
        _editingConnectPort = false;
    }

    private void EnterGameplaySession(GameplaySessionKind sessionKind, bool openJoinMenus, string? statusMessage = null)
    {
        _gameplaySessionKind = sessionKind;
        _pendingHostedConnectTicks = -1;
        _pendingHostedConnectPort = 8190;
        _mainMenuOpen = false;
        CloseMainMenuOverlayState();
        CloseGameplayOverlayState();
        _teamSelectOpen = openJoinMenus;
        _menuStatusMessage = statusMessage ?? string.Empty;
    }

    private void ResetToMainMenuState(string? statusMessage)
    {
        _pendingHostedConnectTicks = -1;
        _pendingHostedConnectPort = 8190;
        _mainMenuOpen = true;
        _mainMenuPage = MainMenuPage.Root;
        _mainMenuHoverIndex = -1;
        _mainMenuBottomBarHover = false;
        _optionsPageIndex = 0;
        CloseMainMenuOverlayState();
        CloseGameplayOverlayState();
        _editingPlayerName = false;
        _gameplaySessionKind = GameplaySessionKind.None;
        _menuStatusMessage = statusMessage ?? string.Empty;
        _autoBalanceNoticeText = string.Empty;
        _autoBalanceNoticeTicks = 0;
    }
}
