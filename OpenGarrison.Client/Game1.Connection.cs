#nullable enable

using System;
using System.Collections.Generic;
using OpenGarrison.Core;
using Microsoft.Xna.Framework;

namespace OpenGarrison.Client;

public partial class Game1
{
    private int _pendingHostedConnectTicks = -1;
    private int _pendingHostedConnectPort = 8190;
    private string? _recentConnectHost;
    private int _recentConnectPort;

    private void BeginHostedGame(
        string serverName,
        int port,
        int maxPlayers,
        string password,
        int timeLimitMinutes,
        int capLimit,
        int respawnSeconds,
        bool lobbyAnnounce,
        bool autoBalance)
    {
        CloseManualConnectMenu(clearStatus: true);
        CloseLobbyBrowser(clearStatus: true);
        _optionsMenuOpen = false;
        _pluginOptionsMenuOpen = false;
        _hostSetupOpen = false;
        _hostSetupEditField = HostSetupEditField.None;
        _editingPlayerName = false;
        _networkClient.Disconnect();

        if (!TryStartHostedServer(
                serverName,
                port,
                maxPlayers,
                password,
                timeLimitMinutes,
                capLimit,
                respawnSeconds,
                lobbyAnnounce,
                autoBalance,
                out var error))
        {
            _menuStatusMessage = error;
            return;
        }

        _pendingHostedConnectPort = port;
        _pendingHostedConnectTicks = 20;
        _menuStatusMessage = "Starting local server...";
    }

    private void UpdatePendingHostedConnect()
    {
        if (_pendingHostedConnectTicks < 0)
        {
            return;
        }

        if (_networkClient.IsConnected)
        {
            _pendingHostedConnectTicks = -1;
            return;
        }

        if (_hostedServerRuntime.HasTrackedProcessExited)
        {
            _pendingHostedConnectTicks = -1;
            _menuStatusMessage = "Local server exited before connect.";
            return;
        }

        if (_pendingHostedConnectTicks > 0)
        {
            _pendingHostedConnectTicks -= 1;
            return;
        }

        _pendingHostedConnectTicks = -1;
        TryConnectToServer("127.0.0.1", _pendingHostedConnectPort, addConsoleFeedback: false);
    }

    private void TryConnectFromMenu()
    {
        var host = _connectHostBuffer.Trim();
        if (string.IsNullOrWhiteSpace(host))
        {
            _menuStatusMessage = "Host is required.";
            return;
        }

        if (!int.TryParse(_connectPortBuffer.Trim(), out var port) || port is <= 0 or > 65535)
        {
            _menuStatusMessage = "Port must be 1-65535.";
            return;
        }

        TryConnectToServer(host, port, addConsoleFeedback: false);
    }

    private bool TryConnectToServer(string host, int port, bool addConsoleFeedback)
    {
        if (_networkClient.Connect(host, port, _world.LocalPlayer.DisplayName, out var error))
        {
            RecordRecentConnection(host, port);
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
            CloseLobbyBrowser(clearStatus: false);
            _menuStatusMessage = $"Connecting to {host}:{port}...";
            if (addConsoleFeedback)
            {
                AddConsoleLine($"connecting to {host}:{port} over udp");
            }

            return true;
        }

        _menuStatusMessage = $"Connect failed: {error}";
        if (addConsoleFeedback)
        {
            AddConsoleLine($"connect failed: {error}");
        }

        return false;
    }

    private void ReturnToMainMenu(string? statusMessage = null)
    {
        ResetPracticeBotManagerState(releaseWorldSlots: true);
        ResetPracticeNavigationState();
        _networkClient.Disconnect();
        StopLocalRapidFireWeaponAudio();
        StopIngameMusic();
        ResetTransientPresentationEffects();
        ResetProcessedNetworkEventHistory();
        ReinitializeSimulationForTickRate(SimulationConfig.DefaultTicksPerSecond);
        ResetClientTimingState();
        _lastAppliedSnapshotFrame = 0;
        _lastBufferedSnapshotFrame = 0;
        StopHostedServer();
        _pendingHostedConnectTicks = -1;
        _pendingHostedConnectPort = 8190;
        _mainMenuOpen = true;
        _optionsMenuOpen = false;
        _optionsMenuOpenedFromGameplay = false;
        _pluginOptionsMenuOpen = false;
        _pluginOptionsMenuOpenedFromGameplay = false;
        _hostSetupOpen = false;
        _hostSetupEditField = HostSetupEditField.None;
        _practiceSetupOpen = false;
        CloseLobbyBrowser(clearStatus: false);
        _manualConnectOpen = false;
        _creditsOpen = false;
        _inGameMenuOpen = false;
        _quitPromptOpen = false;
        _quitPromptHoverIndex = -1;
        _controlsMenuOpen = false;
        _controlsMenuOpenedFromGameplay = false;
        _pendingControlsBinding = null;
        _teamSelectOpen = false;
        _classSelectOpen = false;
        _pendingClassSelectTeam = null;
        _editingPlayerName = false;
        _editingConnectHost = false;
        _editingConnectPort = false;
        _consoleOpen = false;
        _scoreboardOpen = false;
        ResetChatInputState();
        _bubbleMenuKind = BubbleMenuKind.None;
        _bubbleMenuClosing = false;
        _passwordPromptOpen = false;
        _passwordEditBuffer = string.Empty;
        _passwordPromptMessage = string.Empty;
        _localPlayerSnapshotEntityId = null;
        _hasPredictedLocalPlayerPosition = false;
        _hasSmoothedLocalPlayerRenderPosition = false;
        _predictedLocalPlayerRenderCorrectionOffset = Vector2.Zero;
        _lastPredictedRenderSmoothingTimeSeconds = -1d;
        _pendingPredictedInputs.Clear();
        _gameplaySessionKind = GameplaySessionKind.None;
        ResetSnapshotStateHistory();
        _menuStatusMessage = statusMessage ?? string.Empty;
        _autoBalanceNoticeText = string.Empty;
        _autoBalanceNoticeTicks = 0;
    }

    private void ShowAutoBalanceNotice(string text, int seconds)
    {
        _autoBalanceNoticeText = text;
        _autoBalanceNoticeTicks = Math.Max(1, seconds * _config.TicksPerSecond);
    }

    private static HostedServerLaunchOptions CreateHostedServerLaunchOptions(
        string serverName,
        int port,
        int maxPlayers,
        string password,
        int timeLimitMinutes,
        int capLimit,
        int respawnSeconds,
        bool lobbyAnnounce,
        bool autoBalance)
    {
        return new HostedServerLaunchOptions(
            RuntimePaths.GetConfigPath(OpenGarrisonPreferencesDocument.DefaultFileName),
            serverName,
            port,
            maxPlayers,
            password,
            timeLimitMinutes,
            capLimit,
            respawnSeconds,
            lobbyAnnounce,
            autoBalance);
    }

    private bool TryStartHostedServer(
        string serverName,
        int port,
        int maxPlayers,
        string password,
        int timeLimitMinutes,
        int capLimit,
        int respawnSeconds,
        bool lobbyAnnounce,
        bool autoBalance,
        out string error)
    {
        var launchOptions = CreateHostedServerLaunchOptions(
            serverName,
            port,
            maxPlayers,
            password,
            timeLimitMinutes,
            capLimit,
            respawnSeconds,
            lobbyAnnounce,
            autoBalance);
        InitializeHostedServerConsole(reset: false);
        return _hostedServerRuntime.TryStartBackground(launchOptions, out error);
    }

    private bool TryStartHostedServerInTerminal(
        string serverName,
        int port,
        int maxPlayers,
        string password,
        int timeLimitMinutes,
        int capLimit,
        int respawnSeconds,
        bool lobbyAnnounce,
        bool autoBalance,
        out string error)
    {
        var launchOptions = CreateHostedServerLaunchOptions(
            serverName,
            port,
            maxPlayers,
            password,
            timeLimitMinutes,
            capLimit,
            respawnSeconds,
            lobbyAnnounce,
            autoBalance);
        return _hostedServerRuntime.TryStartInTerminal(launchOptions, out error);
    }

    private void StopHostedServer()
    {
        _hostedServerRuntime.Stop();
    }

    private void CloseManualConnectMenu(bool clearStatus)
    {
        _manualConnectOpen = false;
        _editingConnectHost = false;
        _editingConnectPort = false;
        if (clearStatus)
        {
            _menuStatusMessage = string.Empty;
        }
    }

    private void AppendHostedServerLog(string source, string message)
    {
        _hostedServerConsole.AppendLog(source, message);
    }

    private void InitializeHostedServerConsole(bool reset)
    {
        if (reset)
        {
            _hostedServerConsole.Reset();
        }
    }

    private string BuildHostedServerExitMessage()
    {
        return _hostedServerConsole.BuildExitMessage();
    }

    private void PrimeHostedServerConsoleState(
        string serverName,
        int port,
        int maxPlayers,
        int timeLimitMinutes,
        int capLimit,
        int respawnSeconds,
        bool lobbyAnnounce,
        bool autoBalance)
    {
        _hostedServerConsole.Prime(
            serverName,
            port,
            maxPlayers,
            timeLimitMinutes,
            capLimit,
            respawnSeconds,
            lobbyAnnounce,
            autoBalance,
            GetSelectedHostMapEntry()?.DisplayName);
    }

    private bool TrySendHostedServerCommand(string command, out string error)
    {
        var trimmed = command.Trim();
        if (trimmed.Length == 0)
        {
            error = "Type a server command first.";
            return false;
        }

        if (!IsHostedServerRunning)
        {
            error = "Dedicated server is not running.";
            return false;
        }

        if (!TrySendHostedServerAdminCommand(trimmed, out var responseLines, out error))
        {
            return false;
        }

        _hostedServerConsole.ApplyServerMessages(responseLines);
        _hostedServerConsole.ClearCommandInput();
        AppendHostedServerLog("launcher", $"> {trimmed}");
        return true;
    }

    private void ClearHostedServerConsoleView()
    {
        _hostedServerConsole.ClearView();
    }

    private HostedServerConsoleSnapshot GetHostedServerConsoleSnapshot()
    {
        return _hostedServerConsole.CreateSnapshot();
    }

    private bool TryResumeHostedServerSession(bool loadExistingLog, int? expectedProcessId = null)
    {
        return _hostedServerRuntime.TryResumeSession(loadExistingLog, expectedProcessId);
    }

    private bool TrySendHostedServerAdminCommand(string command, out List<string> responseLines, out string error)
    {
        return _hostedServerRuntime.TrySendCommand(command, out responseLines, out error);
    }
}
