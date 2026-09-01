#nullable enable

using System;
using System.Globalization;
using System.IO;
using System.Text;
using OpenGarrison.Core;
using OpenGarrison.Protocol;

namespace OpenGarrison.Client;

public partial class Game1
{
    private bool TryStartDemoRecording(string requestedPath, out string status, out string error)
    {
        status = string.Empty;
        error = string.Empty;

        if (_networkClient.IsReplayConnection)
        {
            error = "demo recording is unavailable while playing a replay or demo.";
            return false;
        }

        var resolvedPath = ResolveDemoRecordingOutputPath(requestedPath);
        byte[]? initialWelcomePayload = null;
        var remoteDescription = _networkClient.ServerDescription ?? "demo-recording";
        if (_networkClient.IsConnected && !_networkClient.IsAwaitingWelcome)
        {
            initialWelcomePayload = ProtocolCodec.Serialize(BuildSyntheticDemoRecordingWelcome());
        }

        return _networkClient.TryStartDemoRecording(resolvedPath, remoteDescription, initialWelcomePayload, out status, out error);
    }

    private void ToggleAlwaysRecordGames()
    {
        if (OperatingSystem.IsBrowser())
        {
            return;
        }

        _clientSettings.AlwaysRecordGames = !_clientSettings.AlwaysRecordGames;
        if (_clientSettings.AlwaysRecordGames)
        {
            try
            {
                _ = RuntimePaths.ReplaysDirectory;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                _clientSettings.AlwaysRecordGames = false;
                _menuStatusMessage = $"Replay recording failed: {ex.Message}";
                PersistClientSettings();
                return;
            }

            if (_networkClient.IsConnected
                && !_networkClient.IsReplayConnection
                && _networkClient.IsAwaitingWelcome
                && !_networkClient.IsDemoRecordingActive)
            {
                TryStartAutomaticDemoRecording(
                    _networkClient.ServerDescription ?? "server",
                    "session",
                    out var status,
                    out var error);
                _menuStatusMessage = string.IsNullOrWhiteSpace(error) ? status : $"Replay recording failed: {error}";
            }
            else if (_networkClient.IsConnected
                && !_networkClient.IsReplayConnection
                && !_networkClient.IsDemoRecordingActive)
            {
                // A recording that starts after the welcome may receive delta snapshots whose
                // baseline predates the file. Start cleanly with the next connection instead.
                _menuStatusMessage = "Always Record Games enabled. Recording starts with the next game.";
            }
        }
        else if (_networkClient.IsAutomaticDemoRecordingActive)
        {
            if (_networkClient.TryStopDemoRecording(saveRecording: true, out var status, out var error))
            {
                _menuStatusMessage = status;
            }
            else if (!string.IsNullOrWhiteSpace(error))
            {
                _menuStatusMessage = $"Replay recording failed: {error}";
            }
        }

        PersistClientSettings();
    }

    private void EnsureAutomaticDemoRecordingForConnection(string serverLabel)
    {
        if (!_clientSettings.AlwaysRecordGames
            || OperatingSystem.IsBrowser()
            || _networkClient.IsReplayConnection
            || _networkClient.IsDemoRecordingActive)
        {
            return;
        }

        if (!TryStartAutomaticDemoRecording(serverLabel, "session", out var status, out var error))
        {
            AddNetworkConsoleLine($"automatic demo recording failed: {error}");
            return;
        }

        AddNetworkConsoleLine(status);
    }

    private bool TryStartAutomaticDemoRecording(
        string serverName,
        string levelName,
        out string status,
        out string error)
    {
        status = string.Empty;
        error = string.Empty;
        if (_networkClient.IsReplayConnection || _networkClient.IsDemoRecordingActive)
        {
            return false;
        }

        if (_networkClient.IsConnected && !_networkClient.IsAwaitingWelcome)
        {
            error = "automatic recording must start before the server welcome; it will start with the next game.";
            return false;
        }

        try
        {
            var outputPath = ResolveAvailableDemoRecordingOutputPath(serverName, levelName);
            return _networkClient.TryStartDemoRecording(
                outputPath,
                _networkClient.ServerDescription ?? serverName,
                initialWelcomePayload: null,
                out status,
                out error,
                automatic: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            error = ex.Message;
            return false;
        }
    }

    private WelcomeMessage BuildSyntheticDemoRecordingWelcome()
    {
        return new WelcomeMessage(
            ServerName: string.IsNullOrWhiteSpace(_networkClient.ServerDescription) ? "Recorded Server" : _networkClient.ServerDescription.Trim(),
            Version: ProtocolVersion.Current,
            TickRate: _config.TicksPerSecond,
            LevelName: _world.Level.Name,
            PlayerSlot: _networkClient.LocalPlayerSlot,
            MaxPlayerCount: _networkClient.ServerMaxPlayerCount > 0
                ? _networkClient.ServerMaxPlayerCount
                : SimulationWorld.MaxPlayableNetworkPlayers,
            IsCustomMap: false,
            MapDownloadUrl: string.Empty,
            MapContentHash: string.Empty,
            MapScale: _world.Level.MapScale);
    }

    private string ResolveDemoRecordingOutputPath(string requestedPath)
    {
        if (!string.IsNullOrWhiteSpace(requestedPath))
        {
            return requestedPath.Trim();
        }

        return Path.Combine(RuntimePaths.ReplaysDirectory, BuildDefaultDemoRecordingFileName());
    }

    private string BuildDefaultDemoRecordingFileName()
    {
        var levelName = string.IsNullOrWhiteSpace(_world.Level.Name) ? "unknown-map" : _world.Level.Name;
        var serverName = string.IsNullOrWhiteSpace(_networkClient.ServerDescription) ? "server" : _networkClient.ServerDescription;
        return BuildDemoRecordingFileName(serverName, levelName);
    }

    private static string BuildDemoRecordingFileName(string serverName, string levelName)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss-fff", CultureInfo.InvariantCulture);
        var builder = new StringBuilder();
        builder.Append(timestamp);
        builder.Append(' ');
        builder.Append(SanitizeDemoRecordingPathSegment(serverName));
        builder.Append(' ');
        builder.Append(SanitizeDemoRecordingPathSegment(levelName));
        builder.Append(".ogdemo");
        return builder.ToString();
    }

    private static string ResolveAvailableDemoRecordingOutputPath(string serverName, string levelName)
    {
        var replayDirectory = RuntimePaths.ReplaysDirectory;
        var fileName = BuildDemoRecordingFileName(serverName, levelName);
        var candidate = Path.Combine(replayDirectory, fileName);
        if (!File.Exists(candidate) && !File.Exists(candidate + ".recording"))
        {
            return candidate;
        }

        var stem = Path.GetFileNameWithoutExtension(fileName);
        for (var suffix = 2; suffix < int.MaxValue; suffix += 1)
        {
            candidate = Path.Combine(replayDirectory, $"{stem} ({suffix}).ogdemo");
            if (!File.Exists(candidate) && !File.Exists(candidate + ".recording"))
            {
                return candidate;
            }
        }

        throw new IOException("Could not allocate a unique demo recording filename.");
    }

    private static string SanitizeDemoRecordingPathSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "session";
        }

        var invalidCharacters = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Trim())
        {
            if (Array.IndexOf(invalidCharacters, character) >= 0)
            {
                builder.Append('_');
                continue;
            }

            builder.Append(char.IsWhiteSpace(character) ? ' ' : character);
        }

        var sanitized = string.IsNullOrWhiteSpace(builder.ToString()) ? "session" : builder.ToString().Trim();
        return sanitized.Length <= 80 ? sanitized : sanitized[..80].TrimEnd();
    }

    private void PublishCompletedDemoRecordingNoticeIfAvailable()
    {
        if (_networkClient.TryConsumeCompletedDemoRecordingNotice(out var notice))
        {
            AddNetworkConsoleLine(notice);
        }
    }
}
