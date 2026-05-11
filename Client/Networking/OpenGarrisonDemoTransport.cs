#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using OpenGarrison.Core;
using OpenGarrison.Protocol;

namespace OpenGarrison.Client;

public sealed class OpenGarrisonDemoTransport : IPlaybackMessageTransport
{
    private readonly List<ScheduledDemoPayload> _payloads;
    private readonly long _ticksPerSecond;
    private readonly string _remoteDescription;
    private readonly string _completionReason;
    private readonly string _playbackDisplayName;
    private readonly string _playbackServerName;
    private readonly string _playbackMapName;
    private readonly DateTime? _playbackDateUtc;
    private int _nextPayloadIndex;
    private bool _disconnectAvailable;
    private bool _disconnectConsumed;
    private double _accumulatedPlaybackMilliseconds;
    private long _lastPlaybackTimestamp;
    private float _playbackRate = 1f;
    private bool _isPaused;

    private OpenGarrisonDemoTransport(
        string remoteDescription,
        string completionReason,
        string playbackDisplayName,
        string playbackServerName,
        string playbackMapName,
        DateTime? playbackDateUtc,
        List<ScheduledDemoPayload> payloads)
    {
        _remoteDescription = remoteDescription;
        _completionReason = completionReason;
        _playbackDisplayName = playbackDisplayName;
        _playbackServerName = playbackServerName;
        _playbackMapName = playbackMapName;
        _playbackDateUtc = playbackDateUtc;
        _payloads = payloads;
        _ticksPerSecond = Stopwatch.Frequency;
        _lastPlaybackTimestamp = Stopwatch.GetTimestamp();
    }

    public bool HasPendingMessages
    {
        get
        {
            if (_nextPayloadIndex >= _payloads.Count)
            {
                MarkPlaybackEnded();
                return false;
            }

            return GetCurrentPlaybackMilliseconds() >= _payloads[_nextPayloadIndex].DueMilliseconds;
        }
    }

    public bool IsLoopbackConnection => true;
    public string RemoteDescription => _remoteDescription;
    public bool IsPaused => _isPaused;
    public float PlaybackRate => _playbackRate;
    public int CurrentTick => _nextPayloadIndex <= 0 ? 0 : Math.Max(0, _nextPayloadIndex - 1);
    public int TotalTicks => Math.Max(0, _payloads.Count - 1);
    public string PlaybackDisplayName => _playbackDisplayName;
    public string PlaybackServerName => _playbackServerName;
    public string PlaybackMapName => _playbackMapName;
    public DateTime? PlaybackDateUtc => _playbackDateUtc;

    public bool TryReceive(out byte[] payload)
    {
        payload = [];
        if (_nextPayloadIndex >= _payloads.Count || !HasPendingMessages)
        {
            return false;
        }

        payload = _payloads[_nextPayloadIndex].Payload;
        _nextPayloadIndex += 1;
        if (_nextPayloadIndex >= _payloads.Count)
        {
            MarkPlaybackEnded();
        }

        return true;
    }

    public bool TryConsumeDisconnectReason(out string reason)
    {
        if (_disconnectAvailable && !_disconnectConsumed)
        {
            _disconnectConsumed = true;
            reason = _completionReason;
            return true;
        }

        reason = string.Empty;
        return false;
    }

    public void Send(byte[] payload)
    {
        // Demo playback is server-authoritative and ignores client sends.
    }

    public void SetPaused(bool paused)
    {
        if (_isPaused == paused)
        {
            return;
        }

        SynchronizePlaybackClock();
        _isPaused = paused;
    }

    public void TogglePaused()
    {
        SetPaused(!_isPaused);
    }

    public void SetPlaybackRate(float playbackRate)
    {
        if (!float.IsFinite(playbackRate))
        {
            throw new ArgumentOutOfRangeException(nameof(playbackRate), "Demo playback rate must be finite.");
        }

        SynchronizePlaybackClock();
        _playbackRate = Math.Clamp(playbackRate, 0.1f, 8f);
    }

    public void Dispose()
    {
    }

    public static bool TryCreate(string demoPath, out INetworkClientMessageTransport? transport, out string error)
    {
        transport = null;
        error = string.Empty;

        try
        {
            var resolvedPath = ResolveDemoPath(demoPath);
            var demo = OpenGarrisonDemoFile.Read(resolvedPath);
            transport = CreateTransport(resolvedPath, demo);
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException)
        {
            error = ex.Message;
            return false;
        }
    }

    public static bool TryVerifyPlaybackSummary(string demoPath, out string summary, out string error)
    {
        summary = string.Empty;
        error = string.Empty;

        try
        {
            var resolvedPath = ResolveDemoPath(demoPath);
            var demo = OpenGarrisonDemoFile.Read(resolvedPath);
            var verification = VerifyPlayback(demo.Messages);
            summary =
                $"Demo={Path.GetFileName(resolvedPath)} " +
                $"verifiedMessages={verification.VerifiedMessageCount} appliedSnapshots={verification.AppliedSnapshotCount} " +
                $"lastFrame={verification.LastAppliedFrame} level={verification.LevelName} " +
                $"players={verification.PlayablePlayerCount} alive={verification.AlivePlayablePlayerCount} " +
                $"redCaps={verification.RedCaps} blueCaps={verification.BlueCaps}";
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException)
        {
            error = ex.Message;
            return false;
        }
    }

    private static OpenGarrisonDemoTransport CreateTransport(string resolvedPath, OpenGarrisonDemoFile demo)
    {
        var payloads = new List<ScheduledDemoPayload>(demo.Messages.Count);
        for (var index = 0; index < demo.Messages.Count; index += 1)
        {
            var message = demo.Messages[index];
            payloads.Add(new ScheduledDemoPayload(message.DueMilliseconds, message.Payload));
        }

        var remoteDescription = string.IsNullOrWhiteSpace(demo.RemoteDescription)
            ? $"demo:{Path.GetFileName(resolvedPath)}"
            : demo.RemoteDescription.Trim();
        var completionReason = string.IsNullOrWhiteSpace(demo.CompletionReason)
            ? "Demo ended."
            : demo.CompletionReason.Trim();
        TryResolveWelcomeMetadata(demo.Messages, out var serverName, out var mapName);
        if (string.IsNullOrWhiteSpace(serverName))
        {
            serverName = remoteDescription;
        }

        return new OpenGarrisonDemoTransport(
            remoteDescription,
            completionReason,
            Path.GetFileName(resolvedPath),
            serverName.Trim(),
            mapName.Trim(),
            File.GetLastWriteTimeUtc(resolvedPath),
            payloads);
    }

    private static void TryResolveWelcomeMetadata(
        IReadOnlyList<OpenGarrisonDemoMessage> messages,
        out string serverName,
        out string mapName)
    {
        serverName = string.Empty;
        mapName = string.Empty;
        foreach (var message in messages)
        {
            if (!ProtocolCodec.TryDeserialize(message.Payload, out var protocolMessage)
                || protocolMessage is not WelcomeMessage welcome)
            {
                continue;
            }

            serverName = welcome.ServerName;
            mapName = welcome.LevelName;
            return;
        }
    }

    private static string ResolveDemoPath(string demoPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(demoPath);
        var trimmed = demoPath.Trim().Trim('"');
        var fullPath = Path.GetFullPath(trimmed);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Demo file was not found.", fullPath);
        }

        return fullPath;
    }

    private static PlaybackVerificationResult VerifyPlayback(IReadOnlyList<OpenGarrisonDemoMessage> messages)
    {
        SimulationWorld? world = null;
        WelcomeMessage? welcome = null;
        SnapshotMessage? lastSnapshot = null;
        var verifiedMessageCount = 0;
        var appliedSnapshotCount = 0;

        for (var index = 0; index < messages.Count; index += 1)
        {
            var payload = messages[index].Payload;
            if (!ProtocolCodec.TryDeserialize(payload, out var message) || message is null)
            {
                throw new InvalidDataException($"Demo payload {index} could not be deserialized by the current protocol codec.");
            }

            verifiedMessageCount += 1;
            switch (message)
            {
                case WelcomeMessage nextWelcome:
                    if (welcome is not null)
                    {
                        throw new InvalidDataException("Demo emitted more than one welcome message.");
                    }

                    if (nextWelcome.Version != ProtocolVersion.Current)
                    {
                        throw new InvalidDataException(
                            $"Demo welcome uses protocol {nextWelcome.Version}, expected {ProtocolVersion.Current}.");
                    }

                    world = new SimulationWorld();
                    if (!world.TryLoadLevel(nextWelcome.LevelName, mapAreaIndex: 1, preservePlayerStats: false, mapScale: nextWelcome.MapScale))
                    {
                        throw new InvalidDataException($"Demo map '{nextWelcome.LevelName}' could not be loaded.");
                    }

                    welcome = nextWelcome;
                    break;
                case SnapshotMessage snapshot:
                    if (welcome is null || world is null)
                    {
                        throw new InvalidDataException("Demo emitted a snapshot before the welcome message.");
                    }

                    if (!world.ApplySnapshot(snapshot, welcome.PlayerSlot))
                    {
                        throw new InvalidDataException($"Demo snapshot frame {snapshot.Frame} could not be applied to SimulationWorld.");
                    }

                    appliedSnapshotCount += 1;
                    lastSnapshot = snapshot;
                    break;
            }
        }

        if (welcome is null || world is null || lastSnapshot is null)
        {
            throw new InvalidDataException("Demo did not contain a welcome message followed by at least one snapshot.");
        }

        return new PlaybackVerificationResult(
            verifiedMessageCount,
            appliedSnapshotCount,
            lastSnapshot.Frame,
            world.Level.Name,
            lastSnapshot.Players.Count(player => !player.IsSpectator),
            lastSnapshot.Players.Count(player => !player.IsSpectator && player.IsAlive),
            world.RedCaps,
            world.BlueCaps);
    }

    private double GetCurrentPlaybackMilliseconds()
    {
        if (_isPaused)
        {
            return _accumulatedPlaybackMilliseconds;
        }

        var now = Stopwatch.GetTimestamp();
        var elapsedTicks = now - _lastPlaybackTimestamp;
        return _accumulatedPlaybackMilliseconds + ((elapsedTicks * 1000d / _ticksPerSecond) * _playbackRate);
    }

    private void SynchronizePlaybackClock()
    {
        var now = Stopwatch.GetTimestamp();
        if (!_isPaused)
        {
            var elapsedTicks = now - _lastPlaybackTimestamp;
            _accumulatedPlaybackMilliseconds += (elapsedTicks * 1000d / _ticksPerSecond) * _playbackRate;
        }

        _lastPlaybackTimestamp = now;
    }

    private void MarkPlaybackEnded()
    {
        if (_disconnectAvailable)
        {
            return;
        }

        _disconnectAvailable = true;
    }

    private readonly record struct ScheduledDemoPayload(int DueMilliseconds, byte[] Payload);
    private readonly record struct PlaybackVerificationResult(
        int VerifiedMessageCount,
        int AppliedSnapshotCount,
        ulong LastAppliedFrame,
        string LevelName,
        int PlayablePlayerCount,
        int AlivePlayablePlayerCount,
        int RedCaps,
        int BlueCaps);
}
