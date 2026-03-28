#nullable enable

using System.Globalization;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

internal readonly record struct HostedServerConsoleSnapshot(
    string CommandInput,
    IReadOnlyList<string> ConsoleLines,
    string StatusName,
    string StatusPort,
    string StatusPlayers,
    string StatusLobby,
    string StatusMap,
    string StatusRules,
    string StatusRuntime,
    string StatusWorld);

internal sealed class HostedServerConsoleState
{
    private const int ConsoleLineLimit = 240;

    private readonly object _sync = new();
    private readonly List<string> _consoleLines = new();
    private string _commandInput = string.Empty;
    private string? _lastOutputLine;
    private string _statusName = "Offline";
    private string _statusPort = "--";
    private string _statusPlayers = "0/0";
    private string _statusLobby = "Lobby unknown";
    private string _statusMap = "Map unknown";
    private string _statusRules = "Rules unknown";
    private string _statusRuntime = "No live server output yet.";
    private string _statusWorld = "World bounds unknown";

    public HostedServerConsoleSnapshot CreateSnapshot()
    {
        lock (_sync)
        {
            return new HostedServerConsoleSnapshot(
                _commandInput,
                _consoleLines.ToArray(),
                _statusName,
                _statusPort,
                _statusPlayers,
                _statusLobby,
                _statusMap,
                _statusRules,
                _statusRuntime,
                _statusWorld);
        }
    }

    public void Reset()
    {
        lock (_sync)
        {
            _consoleLines.Clear();
            _commandInput = string.Empty;
            _lastOutputLine = null;
            _statusName = "Offline";
            _statusPort = "--";
            _statusPlayers = "0/0";
            _statusLobby = "Lobby unknown";
            _statusMap = "Map unknown";
            _statusRules = "Rules unknown";
            _statusRuntime = "No live server output yet.";
            _statusWorld = "World bounds unknown";
        }
    }

    public void ClearView()
    {
        lock (_sync)
        {
            _consoleLines.Clear();
            _lastOutputLine = null;
        }
    }

    public void Prime(
        string serverName,
        int port,
        int maxPlayers,
        int timeLimitMinutes,
        int capLimit,
        int respawnSeconds,
        bool lobbyAnnounce,
        bool autoBalance,
        string? selectedMapDisplayName)
    {
        lock (_sync)
        {
            _commandInput = string.Empty;
            _statusName = serverName;
            _statusPort = port.ToString(CultureInfo.InvariantCulture);
            _statusPlayers = $"0/{maxPlayers}";
            _statusLobby = lobbyAnnounce ? "Enabled" : "Disabled";
            _statusMap = selectedMapDisplayName ?? "Waiting for map bootstrap";
            _statusRules = $"{timeLimitMinutes} min | cap {capLimit} | respawn {respawnSeconds}s | auto-balance {(autoBalance ? "on" : "off")}";
            _statusRuntime = "Launching dedicated server...";
            _statusWorld = "Waiting for world bootstrap";
        }
    }

    public void ApplySessionInfo(HostedServerSessionInfo session)
    {
        lock (_sync)
        {
            if (!string.IsNullOrWhiteSpace(session.ServerName))
            {
                _statusName = session.ServerName;
            }

            if (session.Port > 0)
            {
                _statusPort = session.Port.ToString(CultureInfo.InvariantCulture);
            }
        }
    }

    public void AppendLog(string source, string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{source}] {message}";
        lock (_sync)
        {
            _lastOutputLine = message;
            _consoleLines.Add(line);
            while (_consoleLines.Count > ConsoleLineLimit)
            {
                _consoleLines.RemoveAt(0);
            }

            UpdateStatusUnsafe(source, message);
        }
    }

    public void ApplyServerMessages(IEnumerable<string> messages)
    {
        lock (_sync)
        {
            foreach (var message in messages)
            {
                UpdateStatusUnsafe("server", message);
            }
        }
    }

    public void BackspaceCommandInput()
    {
        lock (_sync)
        {
            if (_commandInput.Length > 0)
            {
                _commandInput = _commandInput[..^1];
            }
        }
    }

    public void AppendCommandInput(char character, int maxLength)
    {
        lock (_sync)
        {
            if (!char.IsControl(character) && _commandInput.Length < maxLength)
            {
                _commandInput += character;
            }
        }
    }

    public void ClearCommandInput()
    {
        lock (_sync)
        {
            _commandInput = string.Empty;
        }
    }

    public string BuildExitMessage()
    {
        lock (_sync)
        {
            var details = string.IsNullOrWhiteSpace(_lastOutputLine)
                ? "No additional server output."
                : _lastOutputLine;
            return $"Dedicated server exited. {details}";
        }
    }

    private void UpdateStatusUnsafe(string source, string message)
    {
        if (source.StartsWith("launcher", StringComparison.OrdinalIgnoreCase))
        {
            if (message.Contains("initialized", StringComparison.OrdinalIgnoreCase))
            {
                _statusRuntime = "Launcher ready.";
            }
            else if (message.StartsWith("Start Server pressed", StringComparison.OrdinalIgnoreCase)
                || message.StartsWith("Starting ", StringComparison.OrdinalIgnoreCase))
            {
                _statusRuntime = "Launching dedicated server...";
            }
            else if (message.StartsWith("> ", StringComparison.Ordinal))
            {
                _statusRuntime = $"Sent command {message[2..]}";
            }
            else if (message.Contains("exited", StringComparison.OrdinalIgnoreCase)
                || message.Contains("stopped", StringComparison.OrdinalIgnoreCase)
                || message.Contains("terminating", StringComparison.OrdinalIgnoreCase))
            {
                _statusRuntime = message;
            }
        }

        if (TryParseKeyValues(message, "[server] status | ", out var statusValues))
        {
            if (statusValues.TryGetValue("name", out var name))
            {
                _statusName = name;
            }

            if (statusValues.TryGetValue("port", out var port))
            {
                _statusPort = port;
            }

            if (statusValues.TryGetValue("players", out var players))
            {
                var spectatorsSuffix = statusValues.TryGetValue("spectators", out var spectators)
                    ? $" ({spectators} spectators)"
                    : string.Empty;
                _statusPlayers = players + spectatorsSuffix;
            }

            if (statusValues.TryGetValue("lobby", out var lobby))
            {
                _statusLobby = lobby;
            }

            if (statusValues.TryGetValue("map", out var map))
            {
                _statusMap = map;
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
                _statusRuntime = string.Join(" | ", runtimeParts);
            }

            return;
        }

        if (TryParseKeyValues(message, "[server] rules | ", out var ruleValues))
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
                _statusRules = string.Join(" | ", ruleParts);
            }

            return;
        }

        if (TryParseKeyValues(message, "[server] lobby | ", out var lobbyValues))
        {
            var enabled = lobbyValues.TryGetValue("enabled", out var enabledValue) ? enabledValue : "unknown";
            if (enabled.Equals("enabled", StringComparison.OrdinalIgnoreCase)
                && lobbyValues.TryGetValue("host", out var host)
                && lobbyValues.TryGetValue("port", out var lobbyPort))
            {
                _statusLobby = $"Enabled ({host}:{lobbyPort})";
            }
            else
            {
                _statusLobby = enabled.Equals("disabled", StringComparison.OrdinalIgnoreCase)
                    ? "Disabled"
                    : enabled;
            }

            return;
        }

        if (TryParseKeyValues(message, "[server] map | ", out var mapValues))
        {
            if (mapValues.TryGetValue("name", out var mapName))
            {
                var area = mapValues.TryGetValue("area", out var areaValue) ? $" area {areaValue}" : string.Empty;
                var mode = mapValues.TryGetValue("mode", out var modeValue) ? $" | {modeValue}" : string.Empty;
                _statusMap = mapName + area + mode;
            }

            return;
        }

        if (TryParseKeyValues(message, "[server] world | ", out var worldValues))
        {
            if (worldValues.TryGetValue("bounds", out var bounds))
            {
                _statusWorld = bounds;
            }

            return;
        }

        if (TryParseKeyValues(message, "[server] rotation | ", out var rotationValues))
        {
            if (rotationValues.TryGetValue("current", out var current)
                && rotationValues.TryGetValue("source", out var rotationSource))
            {
                _statusRuntime = $"Rotation {current} from {rotationSource}";
            }

            return;
        }

        if (message.StartsWith("[server] frame=", StringComparison.OrdinalIgnoreCase))
        {
            _statusRuntime = message[9..];
        }
    }

    private static bool TryParseKeyValues(string message, string prefix, out Dictionary<string, string> values)
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
