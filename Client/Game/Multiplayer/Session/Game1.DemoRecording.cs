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

        var demosDirectory = Path.Combine(RuntimePaths.ConfigDirectory, "demos");
        Directory.CreateDirectory(demosDirectory);
        return Path.Combine(demosDirectory, BuildDefaultDemoRecordingFileName());
    }

    private string BuildDefaultDemoRecordingFileName()
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss", CultureInfo.InvariantCulture);
        var levelName = string.IsNullOrWhiteSpace(_world.Level.Name) ? "unknown-map" : _world.Level.Name;
        var serverName = string.IsNullOrWhiteSpace(_networkClient.ServerDescription) ? "server" : _networkClient.ServerDescription;
        var builder = new StringBuilder();
        builder.Append(timestamp);
        builder.Append(' ');
        builder.Append(SanitizeDemoRecordingPathSegment(serverName));
        builder.Append(' ');
        builder.Append(SanitizeDemoRecordingPathSegment(levelName));
        builder.Append(".ogdemo");
        return builder.ToString();
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

        return string.IsNullOrWhiteSpace(builder.ToString()) ? "session" : builder.ToString().Trim();
    }

    private void PublishCompletedDemoRecordingNoticeIfAvailable()
    {
        if (_networkClient.TryConsumeCompletedDemoRecordingNotice(out var notice))
        {
            AddNetworkConsoleLine(notice);
        }
    }
}
