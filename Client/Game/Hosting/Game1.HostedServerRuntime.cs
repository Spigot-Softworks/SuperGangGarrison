#nullable enable

using System.Collections.Generic;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private static HostedServerLaunchOptions CreateHostedServerLaunchOptions(
        string serverName,
        int port,
        int maxPlayers,
        string password,
        int timeLimitMinutes,
        int capLimit,
        int respawnSeconds,
        bool lobbyAnnounce,
        bool autoBalance,
        string? requestedMap,
        string? mapRotationFile)
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
            autoBalance,
            requestedMap,
            mapRotationFile);
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
        string? requestedMap,
        string? mapRotationFile,
        out string error)
    {
        return TryStartHostedServerBackground(
            serverName,
            port,
            maxPlayers,
            password,
            timeLimitMinutes,
            capLimit,
            respawnSeconds,
            lobbyAnnounce,
            autoBalance,
            requestedMap,
            mapRotationFile,
            resetConsole: false,
            out error);
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
        string? requestedMap,
        string? mapRotationFile,
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
            autoBalance,
            requestedMap,
            mapRotationFile);
        return _hostedServerRuntime.TryStartInTerminal(launchOptions, out error);
    }

    private void StopHostedServer()
    {
        _hostedServerRuntime.Stop();
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
