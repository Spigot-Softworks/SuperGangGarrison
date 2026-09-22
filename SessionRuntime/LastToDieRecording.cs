using System.Buffers.Binary;
using System.Reflection;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenGarrison.Core;
using OpenGarrison.Core.LastToDie;
using OpenGarrison.Protocol;

namespace OpenGarrison.SessionRuntime;

public enum RunRecordingEventKind { Connect, Packet, Disconnect, Advance }

public sealed record RunRecordingEvent(
    RunRecordingEventKind Kind, ulong PeerId = 0, bool Local = false,
    byte Slot = 0, Guid ClientId = default, double Seconds = 0, byte[]? Payload = null);

public sealed record LastToDieRecording(
    string Ruleset, EmbeddedSessionOptions Options, bool RequireAdmission,
    int RunIndex, IReadOnlyList<RunRecordingEvent> Events)
{
    public LastToDieRunOutcome? ExpectedOutcome { get; init; }

    // Use the simulation assembly, not the entry executable (test runners and browser shells differ).
    public static string CurrentRuleset { get; } = "ltd-input-v1:"
        + (typeof(LastToDieRecording).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "dev")
        + ":" + ProtocolVersion.Current;
    public const int MaximumCompressedBytes = 16 * 1024 * 1024;
    public const int MaximumDecodedBytes = 128 * 1024 * 1024;
    public const int MaximumEvents = 2_000_000;
    public const double MaximumSeconds = 8 * 60 * 60;

    public byte[] Compress()
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true))
            JsonSerializer.Serialize(gzip, this, RunRecordingJsonContext.Default.LastToDieRecording);
        if (output.Length > MaximumCompressedBytes) throw new InvalidDataException("Run recording is too large to upload.");
        return output.ToArray();
    }

    public static LastToDieRecording Read(byte[] compressed)
    {
        if (compressed.Length > MaximumCompressedBytes) throw new InvalidDataException("Run upload is too large.");
        using var input = new MemoryStream(compressed);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var decoded = new MemoryStream();
        var buffer = new byte[8192];
        int read;
        while ((read = gzip.Read(buffer)) > 0)
        {
            if (decoded.Length + read > MaximumDecodedBytes) throw new InvalidDataException("Expanded run is too large.");
            decoded.Write(buffer, 0, read);
        }
        return JsonSerializer.Deserialize(decoded.ToArray(), RunRecordingJsonContext.Default.LastToDieRecording)
            ?? throw new InvalidDataException("Missing run recording.");
    }
}

internal static class RunRecordingPackets
{
    private static readonly Protocol64SchemaRegistry Registry = Protocol64SchemaRegistryFactory.CreateDefault();

    public static bool IsGameplayPacket(byte[] payload)
    {
        if (payload.Length is < 1 or > 65536) return false;
        object? message;
        if (payload.Length >= 4 && BinaryPrimitives.ReadUInt32LittleEndian(payload) == Protocol64FrameHeader.Magic)
        {
            var decoded = Protocol64FrameCodec.Decode(payload, Registry,
                new Protocol64FrameDecodeOptions { ExpectedDirection = Protocol64Direction.ClientToServer });
            if (!decoded.Succeeded) return false;
            message = decoded.Event;
        }
        else
        {
            if (!ProtocolCodec.TryDeserialize(payload, out var legacy)) return false;
            message = legacy;
        }

        // Account credentials, chat commands, plugins, voice and uploaded assets never enter a proof.
        return message is HelloMessage or InputStateMessage or Protocol64InputCommand
            or LastToDieCommandMessage or LastToDieRunSnapshotAckMessage
            or SnapshotAckMessage or PingRequestMessage or Protocol64InputCommandResultAck
            or Protocol64StateResyncRequest;
    }
}

public static class LastToDieRecordingVerifier
{
    public static LastToDieRunOutcome Verify(LastToDieRecording recording, CancellationToken cancellationToken = default)
    {
        if (recording.Ruleset != LastToDieRecording.CurrentRuleset)
            throw new InvalidDataException("This worker does not support the run's ruleset.");
        var options = recording.Options;
        options.Validate();
        if (!options.LastToDie || options.RunIdentity == Guid.Empty || options.Seed is null
            || options.RedBots != 0 || options.BlueBots != 0 || !options.SpecialAbilities
            || options.MapArea != 1 || options.TimeLimitMinutes != 15 || options.CaptureLimit != 5
            || options.RespawnSeconds != 5
            || !OpenGarrisonStockMapCatalog.TryGetDefinition(options.Map, out _))
            throw new InvalidDataException("The run uses unsupported match settings.");
        if (recording.RunIndex is < 1 or > 100 || recording.Events is null
            || recording.Events.Count is < 1 or > LastToDieRecording.MaximumEvents)
            throw new InvalidDataException("Invalid run event count.");

        using var host = new EmbeddedSessionHost(options, recording.RequireAdmission, recordRuns: false);
        var peers = new Dictionary<ulong, EmbeddedSessionPeer>();
        LastToDieRunOutcome? outcome = null;
        var completed = 0;
        host.RunCompleted += result => { if (++completed == recording.RunIndex) outcome = result; };
        double seconds = 0;
        var connections = 0;
        foreach (var entry in recording.Events)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (outcome is not null) throw new InvalidDataException("Events continue after the submitted run ended.");
            switch (entry.Kind)
            {
                case RunRecordingEventKind.Connect:
                    if (++connections > 256 || entry.PeerId == 0 || peers.ContainsKey(entry.PeerId))
                        throw new InvalidDataException("Invalid recorded connection.");
                    peers.Add(entry.PeerId, host.CreatePeer(entry.Local, entry.Slot, entry.ClientId));
                    break;
                case RunRecordingEventKind.Packet:
                    if (!peers.TryGetValue(entry.PeerId, out var peer) || peer.IsClosed
                        || entry.Payload is null || !RunRecordingPackets.IsGameplayPacket(entry.Payload))
                        throw new InvalidDataException("Invalid recorded gameplay input.");
                    peer.Send(entry.Payload);
                    break;
                case RunRecordingEventKind.Disconnect:
                    if (!peers.Remove(entry.PeerId, out var closing)) throw new InvalidDataException("Unknown recorded connection.");
                    closing.Dispose();
                    break;
                case RunRecordingEventKind.Advance:
                    if (!double.IsFinite(entry.Seconds) || entry.Seconds < 0 || entry.Seconds > 0.25
                        || (seconds += entry.Seconds) > LastToDieRecording.MaximumSeconds)
                        throw new InvalidDataException("Invalid run duration.");
                    host.Advance(entry.Seconds);
                    foreach (var connection in peers.Values) while (connection.TryReceive(out _)) { }
                    break;
                default:
                    throw new InvalidDataException("Unknown run event.");
            }
        }
        if (outcome is null) throw new InvalidDataException("The recording did not complete a run.");
        var expected = recording.ExpectedOutcome;
        if (expected is null || expected.AttemptId != outcome.AttemptId
            || expected.CompletedFrame != outcome.CompletedFrame || expected.CompletedRounds != outcome.CompletedRounds
            || expected.Difficulty != outcome.Difficulty || expected.Participants is null
            || !expected.Participants.SequenceEqual(outcome.Participants))
            throw new InvalidDataException("The replay disagrees with the recorded result.");
        // The expected result is only a consistency check; publish the simulation's outcome.
        return outcome;
    }
}

[JsonSerializable(typeof(LastToDieRecording))]
internal sealed partial class RunRecordingJsonContext : JsonSerializerContext { }
