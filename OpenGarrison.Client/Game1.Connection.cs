#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using OpenGarrison.Core;
using Microsoft.Xna.Framework;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed record HostedServerLaunchTarget(string FileName, string ArgumentsPrefix, string WorkingDirectory);

    private int _pendingHostedConnectTicks = -1;
    private int _pendingHostedConnectPort = 8190;
    private Process? _hostedServerProcess;
    private readonly object _hostedServerLogSync = new();
    private string? _hostedServerLastOutputLine;
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

        if (_hostedServerProcess is not null && _hostedServerProcess.HasExited)
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
        _hostSetupOpen = false;
        _hostSetupEditField = HostSetupEditField.None;
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
        error = string.Empty;

        StopHostedServer();
        HostedServerSessionInfo.Delete();

        if (!IsUdpPortAvailable(port))
        {
            error = $"UDP port {port} is already in use.";
            AppendHostedServerLog("launcher", error);
            return false;
        }

        var serverLaunchTarget = FindServerLaunchTarget();
        if (serverLaunchTarget is null)
        {
            error = "Could not find OpenGarrison.Server. Build the server first.";
            return false;
        }

        try
        {
            InitializeHostedServerConsole(reset: false);
            _hostedServerLastOutputLine = null;

            var arguments = BuildHostedServerLaunchArguments(
                serverLaunchTarget,
                serverName,
                port,
                maxPlayers,
                password,
                timeLimitMinutes,
                capLimit,
                respawnSeconds,
                lobbyAnnounce,
                autoBalance);
            var startInfo = new ProcessStartInfo(
                serverLaunchTarget.FileName,
                arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = serverLaunchTarget.WorkingDirectory,
            };
            startInfo.Environment["OPENGARRISON_LAUNCH_MODE"] = "launcher";
            AppendHostedServerLog("launcher", $"Starting {serverLaunchTarget.FileName} {arguments}");
            var process = Process.Start(startInfo);
            if (process is null)
            {
                error = "Failed to start local server process.";
                return false;
            }

            process.EnableRaisingEvents = true;
            process.Exited += (_, _) =>
            {
                try
                {
                    AppendHostedServerLog("launcher", $"Server process exited with code {process.ExitCode}.");
                }
                catch
                {
                }
            };
            _hostedServerProcess = process;

            return true;
        }
        catch (Exception ex)
        {
            error = $"Failed to start local server: {ex.Message}";
            return false;
        }
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
        error = string.Empty;

        if (!IsUdpPortAvailable(port))
        {
            error = $"UDP port {port} is already in use.";
            return false;
        }

        var serverLaunchTarget = FindServerLaunchTarget();
        if (serverLaunchTarget is null)
        {
            error = "Could not find OpenGarrison.Server. Build the server first.";
            return false;
        }

        try
        {
            HostedServerSessionInfo.Delete();
            InitializeHostedServerConsole(reset: true);
            var arguments = BuildHostedServerLaunchArguments(
                serverLaunchTarget,
                serverName,
                port,
                maxPlayers,
                password,
                timeLimitMinutes,
                capLimit,
                respawnSeconds,
                lobbyAnnounce,
                autoBalance);
            var startInfo = new ProcessStartInfo(
                serverLaunchTarget.FileName,
                arguments)
            {
                UseShellExecute = true,
                WorkingDirectory = serverLaunchTarget.WorkingDirectory,
            };
            Process.Start(startInfo);
            return true;
        }
        catch (Exception ex)
        {
            error = $"Failed to start dedicated server terminal: {ex.Message}";
            return false;
        }
    }

    private void StopHostedServer()
    {
        var session = _hostedServerSession;
        if (session is null && _hostedServerProcess is null)
        {
            return;
        }

        try
        {
            if (session is not null)
            {
                AppendHostedServerLog("launcher", "Stop requested for hosted server.");
                if (!TrySendHostedServerAdminCommand("shutdown", out _, out var shutdownError))
                {
                    AppendHostedServerLog("launcher", shutdownError);
                }

                if (TryGetHostedServerProcess(session.ProcessId, out var processToStop)
                    && processToStop is not null
                    && !processToStop.WaitForExit(2000)
                    && _hostedServerProcess is not null
                    && _hostedServerProcess.Id == session.ProcessId)
                {
                    AppendHostedServerLog("launcher", "Hosted server did not exit after shutdown; terminating process tree.");
                    _hostedServerProcess.Kill(entireProcessTree: true);
                    _hostedServerProcess.WaitForExit(1000);
                }
            }
            else if (_hostedServerProcess is not null && !_hostedServerProcess.HasExited)
            {
                _hostedServerProcess.Kill(entireProcessTree: true);
                _hostedServerProcess.WaitForExit(1000);
            }
        }
        catch
        {
        }
        finally
        {
            _hostedServerProcess?.Dispose();
            _hostedServerProcess = null;
            _hostedServerSession = null;
            HostedServerSessionInfo.Delete();
        }
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

    private static HostedServerLaunchTarget? FindServerLaunchTarget()
    {
        foreach (var candidate in EnumerateServerAppHostCandidates())
        {
            if (File.Exists(candidate))
            {
                return new HostedServerLaunchTarget(candidate, string.Empty, Path.GetDirectoryName(candidate) ?? AppContext.BaseDirectory);
            }
        }

        foreach (var candidate in EnumerateServerAssemblyCandidates())
        {
            if (File.Exists(candidate))
            {
                return new HostedServerLaunchTarget("dotnet", $"\"{candidate}\"", Path.GetDirectoryName(candidate) ?? AppContext.BaseDirectory);
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateServerAppHostCandidates()
    {
        var appHostFileNames = OperatingSystem.IsWindows()
            ? new[] { "OpenGarrison.Server.exe", "OpenGarrison.Server" }
            : new[] { "OpenGarrison.Server", "OpenGarrison.Server.exe" };
        var directCandidates = new[]
        {
            AppContext.BaseDirectory,
            Directory.GetCurrentDirectory(),
        };
        foreach (var directory in directCandidates)
        {
            foreach (var fileName in appHostFileNames)
            {
                yield return Path.Combine(directory, fileName);
            }
        }

        var probes = new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
        var relativeDirectories = new[]
        {
            Path.Combine("Source", "OpenGarrison.CSharp", "src", "OpenGarrison.Server", "bin", "Debug", "net10.0"),
            Path.Combine("Source", "OpenGarrison.CSharp", "src", "OpenGarrison.Server", "bin", "Release", "net10.0"),
            Path.Combine("src", "OpenGarrison.Server", "bin", "Debug", "net10.0"),
            Path.Combine("src", "OpenGarrison.Server", "bin", "Release", "net10.0"),
        };

        foreach (var probe in probes)
        {
            var directory = new DirectoryInfo(probe);
            while (directory is not null)
            {
                foreach (var relativeDirectory in relativeDirectories)
                {
                    foreach (var fileName in appHostFileNames)
                    {
                        yield return Path.Combine(directory.FullName, relativeDirectory, fileName);
                    }
                }

                directory = directory.Parent;
            }
        }
    }

    private static IEnumerable<string> EnumerateServerAssemblyCandidates()
    {
        var directCandidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "OpenGarrison.Server.dll"),
            Path.Combine(Directory.GetCurrentDirectory(), "OpenGarrison.Server.dll"),
        };
        foreach (var candidate in directCandidates)
        {
            yield return candidate;
        }

        var probes = new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
        var relativePaths = new[]
        {
            Path.Combine("Source", "OpenGarrison.CSharp", "src", "OpenGarrison.Server", "bin", "Debug", "net10.0", "OpenGarrison.Server.dll"),
            Path.Combine("Source", "OpenGarrison.CSharp", "src", "OpenGarrison.Server", "bin", "Release", "net10.0", "OpenGarrison.Server.dll"),
            Path.Combine("src", "OpenGarrison.Server", "bin", "Debug", "net10.0", "OpenGarrison.Server.dll"),
            Path.Combine("src", "OpenGarrison.Server", "bin", "Release", "net10.0", "OpenGarrison.Server.dll"),
        };

        foreach (var probe in probes)
        {
            var directory = new DirectoryInfo(probe);
            while (directory is not null)
            {
                foreach (var relativePath in relativePaths)
                {
                    yield return Path.Combine(directory.FullName, relativePath);
                }

                directory = directory.Parent;
            }
        }
    }

    private void AppendHostedServerLog(string source, string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{source}] {message}{Environment.NewLine}";
        lock (_hostedServerLogSync)
        {
            _hostedServerLastOutputLine = message;
            _hostedServerConsoleLines.Add(line.TrimEnd('\r', '\n'));
            while (_hostedServerConsoleLines.Count > 240)
            {
                _hostedServerConsoleLines.RemoveAt(0);
            }

            UpdateHostedServerConsoleStatusUnsafe(source, message);
        }
    }

    private void InitializeHostedServerConsole(bool reset)
    {
        lock (_hostedServerLogSync)
        {
            if (reset)
            {
                ResetHostedServerConsoleStateUnsafe();
            }
        }
    }

    private static bool IsUdpPortAvailable(int port)
    {
        try
        {
            using var probe = new UdpClient(port);
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    private string BuildHostedServerExitMessage()
    {
        var details = string.IsNullOrWhiteSpace(_hostedServerLastOutputLine)
            ? "No additional server output."
            : _hostedServerLastOutputLine;
        return $"Dedicated server exited. {details}";
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
        lock (_hostedServerLogSync)
        {
            _hostedServerCommandInput = string.Empty;
            _hostedServerStatusName = serverName;
            _hostedServerStatusPort = port.ToString(CultureInfo.InvariantCulture);
            _hostedServerStatusPlayers = $"0/{maxPlayers}";
            _hostedServerStatusLobby = lobbyAnnounce ? "Enabled" : "Disabled";
            _hostedServerStatusMap = GetSelectedHostMapEntry()?.DisplayName ?? "Waiting for map bootstrap";
            _hostedServerStatusRules = $"{timeLimitMinutes} min | cap {capLimit} | respawn {respawnSeconds}s | auto-balance {(autoBalance ? "on" : "off")}";
            _hostedServerStatusRuntime = "Launching dedicated server...";
            _hostedServerStatusWorld = "Waiting for world bootstrap";
        }
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

        lock (_hostedServerLogSync)
        {
            foreach (var line in responseLines)
            {
                UpdateHostedServerConsoleStatusUnsafe("server", line);
            }
        }

        _hostedServerCommandInput = string.Empty;
        AppendHostedServerLog("launcher", $"> {trimmed}");
        return true;
    }

    private void ClearHostedServerConsoleView()
    {
        lock (_hostedServerLogSync)
        {
            _hostedServerConsoleLines.Clear();
            _hostedServerLastOutputLine = null;
        }
    }

    private List<string> GetHostedServerConsoleLinesSnapshot()
    {
        lock (_hostedServerLogSync)
        {
            return _hostedServerConsoleLines.ToList();
        }
    }

    private void ResetHostedServerConsoleStateUnsafe()
    {
        _hostedServerConsoleLines.Clear();
        _hostedServerLastOutputLine = null;
        _hostedServerStatusName = "Offline";
        _hostedServerStatusPort = "--";
        _hostedServerStatusPlayers = "0/0";
        _hostedServerStatusLobby = "Lobby unknown";
        _hostedServerStatusMap = "Map unknown";
        _hostedServerStatusRules = "Rules unknown";
        _hostedServerStatusRuntime = "No live server output yet.";
        _hostedServerStatusWorld = "World bounds unknown";
    }

    private void UpdateHostedServerConsoleStatusUnsafe(string source, string message)
    {
        if (source.StartsWith("launcher", StringComparison.OrdinalIgnoreCase))
        {
            if (message.Contains("initialized", StringComparison.OrdinalIgnoreCase))
            {
                _hostedServerStatusRuntime = "Launcher ready.";
            }
            else if (message.StartsWith("Start Server pressed", StringComparison.OrdinalIgnoreCase)
                || message.StartsWith("Starting ", StringComparison.OrdinalIgnoreCase))
            {
                _hostedServerStatusRuntime = "Launching dedicated server...";
            }
            else if (message.StartsWith("> ", StringComparison.Ordinal))
            {
                _hostedServerStatusRuntime = $"Sent command {message[2..]}";
            }
            else if (message.Contains("exited", StringComparison.OrdinalIgnoreCase)
                || message.Contains("stopped", StringComparison.OrdinalIgnoreCase)
                || message.Contains("terminating", StringComparison.OrdinalIgnoreCase))
            {
                _hostedServerStatusRuntime = message;
            }
        }

        if (TryParseHostedServerKeyValues(message, "[server] status | ", out var statusValues))
        {
            if (statusValues.TryGetValue("name", out var name))
            {
                _hostedServerStatusName = name;
            }

            if (statusValues.TryGetValue("port", out var port))
            {
                _hostedServerStatusPort = port;
            }

            if (statusValues.TryGetValue("players", out var players))
            {
                var spectatorsSuffix = statusValues.TryGetValue("spectators", out var spectators)
                    ? $" ({spectators} spectators)"
                    : string.Empty;
                _hostedServerStatusPlayers = players + spectatorsSuffix;
            }

            if (statusValues.TryGetValue("lobby", out var lobby))
            {
                _hostedServerStatusLobby = lobby;
            }

            if (statusValues.TryGetValue("map", out var map))
            {
                _hostedServerStatusMap = map;
            }

            var runtimeParts = new List<string>();
            if (statusValues.TryGetValue("mode", out var mode))
            {
                runtimeParts.Add(mode);
            }

            if (statusValues.TryGetValue("phase", out var phase))
            {
                runtimeParts.Add(phase);
            }

            if (statusValues.TryGetValue("score", out var score))
            {
                runtimeParts.Add($"score {score}");
            }

            if (statusValues.TryGetValue("uptime", out var uptime))
            {
                runtimeParts.Add($"uptime {uptime}");
            }

            if (runtimeParts.Count > 0)
            {
                _hostedServerStatusRuntime = string.Join(" | ", runtimeParts);
            }

            return;
        }

        if (TryParseHostedServerKeyValues(message, "[server] rules | ", out var ruleValues))
        {
            var ruleParts = new List<string>();
            if (ruleValues.TryGetValue("timeLimit", out var timeLimit))
            {
                ruleParts.Add($"{timeLimit} min");
            }

            if (ruleValues.TryGetValue("capLimit", out var capLimit))
            {
                ruleParts.Add($"cap {capLimit}");
            }

            if (ruleValues.TryGetValue("respawn", out var respawn))
            {
                ruleParts.Add($"respawn {respawn}s");
            }

            if (ruleValues.TryGetValue("autoBalance", out var autoBalance))
            {
                ruleParts.Add($"auto-balance {autoBalance}");
            }

            if (ruleParts.Count > 0)
            {
                _hostedServerStatusRules = string.Join(" | ", ruleParts);
            }

            return;
        }

        if (TryParseHostedServerKeyValues(message, "[server] lobby | ", out var lobbyValues))
        {
            var enabled = lobbyValues.TryGetValue("enabled", out var enabledValue) ? enabledValue : "unknown";
            if (enabled.Equals("enabled", StringComparison.OrdinalIgnoreCase)
                && lobbyValues.TryGetValue("host", out var host)
                && lobbyValues.TryGetValue("port", out var lobbyPort))
            {
                _hostedServerStatusLobby = $"Enabled ({host}:{lobbyPort})";
            }
            else
            {
                _hostedServerStatusLobby = enabled.Equals("disabled", StringComparison.OrdinalIgnoreCase)
                    ? "Disabled"
                    : enabled;
            }

            return;
        }

        if (TryParseHostedServerKeyValues(message, "[server] map | ", out var mapValues))
        {
            if (mapValues.TryGetValue("name", out var mapName))
            {
                var area = mapValues.TryGetValue("area", out var areaValue) ? $" area {areaValue}" : string.Empty;
                var mode = mapValues.TryGetValue("mode", out var modeValue) ? $" | {modeValue}" : string.Empty;
                _hostedServerStatusMap = mapName + area + mode;
            }

            return;
        }

        if (TryParseHostedServerKeyValues(message, "[server] world | ", out var worldValues))
        {
            if (worldValues.TryGetValue("bounds", out var bounds))
            {
                _hostedServerStatusWorld = bounds;
            }

            return;
        }

        if (TryParseHostedServerKeyValues(message, "[server] rotation | ", out var rotationValues))
        {
            if (rotationValues.TryGetValue("current", out var current)
                && rotationValues.TryGetValue("source", out var rotationSource))
            {
                _hostedServerStatusRuntime = $"Rotation {current} from {rotationSource}";
            }

            return;
        }

        if (message.StartsWith("[server] frame=", StringComparison.OrdinalIgnoreCase))
        {
            _hostedServerStatusRuntime = message[9..];
        }
    }

    private bool TryResumeHostedServerSession(bool loadExistingLog, int? expectedProcessId = null)
    {
        var session = HostedServerSessionInfo.Load();
        if (session is null)
        {
            return false;
        }

        if (expectedProcessId.HasValue && session.ProcessId != expectedProcessId.Value)
        {
            return false;
        }

        if (!TryGetHostedServerProcess(session.ProcessId, out _))
        {
            HostedServerSessionInfo.Delete();
            return false;
        }

        _hostedServerSession = session;
        _hostedServerStatusName = string.IsNullOrWhiteSpace(session.ServerName) ? _hostedServerStatusName : session.ServerName;
        _hostedServerStatusPort = session.Port > 0 ? session.Port.ToString(CultureInfo.InvariantCulture) : _hostedServerStatusPort;

        if (!TrySendHostedServerAdminCommand("__ping", out _, out _))
        {
            return false;
        }
        _ = loadExistingLog;

        TrySendHostedServerAdminCommand("__snapshot", out var snapshotLines, out _);
        lock (_hostedServerLogSync)
        {
            foreach (var line in snapshotLines)
            {
                UpdateHostedServerConsoleStatusUnsafe("server", line);
            }
        }

        return true;
    }

    private static bool TryGetHostedServerProcess(int processId, out Process? process)
    {
        process = null;
        try
        {
            process = Process.GetProcessById(processId);
            if (process.HasExited)
            {
                process.Dispose();
                process = null;
                return false;
            }

            return true;
        }
        catch
        {
            process?.Dispose();
            process = null;
            return false;
        }
    }

    private bool TrySendHostedServerAdminCommand(string command, out List<string> responseLines, out string error)
    {
        responseLines = new List<string>();
        error = string.Empty;
        if (_hostedServerSession is null || string.IsNullOrWhiteSpace(_hostedServerSession.PipeName))
        {
            error = "Dedicated server control channel is unavailable.";
            return false;
        }

        try
        {
            using var pipe = new NamedPipeClientStream(".", _hostedServerSession.PipeName, PipeDirection.InOut, PipeOptions.None);
            pipe.Connect(1000);
            using var writer = new StreamWriter(pipe, Encoding.UTF8, bufferSize: 1024, leaveOpen: true)
            {
                AutoFlush = true,
            };
            using var reader = new StreamReader(pipe, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
            writer.WriteLine(command);
            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                if (string.Equals(line, "__END__", StringComparison.Ordinal))
                {
                    break;
                }

                responseLines.Add(line);
            }

            return true;
        }
        catch (Exception ex)
        {
            error = $"Dedicated server control channel failed: {ex.Message}";
            return false;
        }
    }

    private static string BuildHostedServerLaunchArguments(
        HostedServerLaunchTarget serverLaunchTarget,
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
        var configArg = $" --config \"{RuntimePaths.GetConfigPath(OpenGarrisonPreferencesDocument.DefaultFileName)}\"";
        var portArg = port > 0 ? $" --port {port}" : string.Empty;
        var nameArg = string.IsNullOrWhiteSpace(serverName) ? string.Empty : $" --name \"{serverName}\"";
        var maxPlayersArg = maxPlayers > 0 ? $" --max-players {maxPlayers}" : string.Empty;
        var passwordArg = string.IsNullOrWhiteSpace(password) ? string.Empty : $" --password \"{password}\"";
        var timeLimitArg = timeLimitMinutes > 0 ? $" --time-limit {timeLimitMinutes}" : string.Empty;
        var capLimitArg = capLimit > 0 ? $" --cap-limit {capLimit}" : string.Empty;
        var respawnArg = respawnSeconds >= 0 ? $" --respawn-seconds {respawnSeconds}" : string.Empty;
        var lobbyArg = lobbyAnnounce ? " --lobby" : " --no-lobby";
        var autoBalanceArg = autoBalance ? " --auto-balance" : " --no-auto-balance";
        return $"{serverLaunchTarget.ArgumentsPrefix}{configArg}{portArg}{nameArg}{maxPlayersArg}{passwordArg}{timeLimitArg}{capLimitArg}{respawnArg}{lobbyArg}{autoBalanceArg}".Trim();
    }

    private static bool TryParseHostedServerKeyValues(
        string message,
        string prefix,
        out Dictionary<string, string> values)
    {
        values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!message.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var segments = message[prefix.Length..].Split(" | ", StringSplitOptions.RemoveEmptyEntries);
        foreach (var segment in segments)
        {
            var separatorIndex = segment.IndexOf('=');
            if (separatorIndex <= 0 || separatorIndex >= segment.Length - 1)
            {
                continue;
            }

            var key = segment[..separatorIndex].Trim();
            var value = segment[(separatorIndex + 1)..].Trim();
            if (key.Length > 0)
            {
                values[key] = value;
            }
        }

        return values.Count > 0;
    }
}
