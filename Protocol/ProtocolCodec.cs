using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace OpenGarrison.Protocol;

public static partial class ProtocolCodec
{
    public const int MaxServerPlayerTitleBytes = 64;
    public const int MaxPlayerNameBytes = 80;
    private const int MaxServerNameBytes = 128;
    private const int MaxLevelNameBytes = 64;
    private const int MaxMapUrlBytes = 512;
    private const int MaxMapHashBytes = 96;
    public const int MaxReasonBytes = 128;
    private const int MaxPasswordBytes = 64;
    public const int MaxChatBytes = 180;
    public const int MaxFriendCodeBytes = 40;
    public const int MaxPlayerCardBytes = 1024;
    public const int MaxPluginIdBytes = 80;
    public const int MaxPluginMessageTypeBytes = 80;
    public const int MaxPluginPayloadBytes = 1024;
    public const int CustomBubbleRgba64PayloadBytes = 72 * 58 * 8;
    public const int MaxAssetNameBytes = 64;
    public const int MaxKillMessageBytes = 160;
    private const int MaxGameplayIdBytes = 96;
    private const int MaxGameplayTokenBytes = 256;
    private const int MaxVoteSubjectBytes = 160;
    private const int MaxVoteMessageBytes = 256;
    private const int MaxVoteMenuEntries = 128;
    private const int MaxServerDetailsRosterEntries = 64;
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    // Position quantization: 0.25 pixel precision using int16 (-8192 to +8191 pixels range)
    private const float PositionQuantizationStep = 0.25f;
    private const float InverseQuantizationStep = 1.0f / PositionQuantizationStep; // 4.0

    private static short QuantizePosition(float position)
    {
        return (short)Math.Clamp((int)(position * InverseQuantizationStep), short.MinValue, short.MaxValue);
    }

    private static float DequantizePosition(short quantized)
    {
        return quantized * PositionQuantizationStep;
    }

    /// <summary>
    /// Serialize a protocol message with optional compression.
    /// Uses default compression settings (compression enabled for snapshots).
    /// </summary>
    public static byte[] Serialize(IProtocolMessage message)
    {
        return Serialize(message, ProtocolCompressionSettings.Default);
    }

    /// <summary>
    /// Serialize a protocol message with custom compression settings.
    /// </summary>
    public static byte[] Serialize(IProtocolMessage message, ProtocolCompressionSettings compressionSettings)
    {
        var measuredSize = MeasureSerializedSize(message);
        return Serialize(message, measuredSize, compressionSettings);
    }

    public static byte[] Serialize(IProtocolMessage message, int measuredSize)
    {
        return Serialize(message, measuredSize, ProtocolCompressionSettings.Default);
    }

    public static byte[] Serialize(IProtocolMessage message, int measuredSize, ProtocolCompressionSettings compressionSettings)
    {
        var uncompressed = SerializeUncompressed(message, measuredSize);

        // Check if compression should be attempted
        if (!compressionSettings.EnableCompression)
        {
            return ProtocolCodecCompression.PrependEncoding(uncompressed, MessageEncoding.None);
        }

        // Only compress snapshots if configured that way
        if (compressionSettings.CompressOnlySnapshots && message is not SnapshotMessage)
        {
            return ProtocolCodecCompression.PrependEncoding(uncompressed, MessageEncoding.None);
        }

        // Try to compress
        var compressed = ProtocolCodecCompression.TryCompress(uncompressed, compressionSettings);
        if (compressed != null)
        {
            return ProtocolCodecCompression.PrependEncoding(compressed, MessageEncoding.LZ4);
        }

        // Compression not beneficial - use uncompressed
        return ProtocolCodecCompression.PrependEncoding(uncompressed, MessageEncoding.None);
    }

    private static byte[] SerializeUncompressed(IProtocolMessage message)
    {
        var size = MeasureSerializedSize(message);
        return SerializeUncompressed(message, size);
    }

    private static byte[] SerializeUncompressed(IProtocolMessage message, int measuredSize)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentOutOfRangeException.ThrowIfNegative(measuredSize);

        using var stream = new MemoryStream(measuredSize);
        using var writer = new BinaryWriter(stream, Utf8, leaveOpen: true);
        WriteMessage(writer, message);
        writer.Flush();
        if (stream.TryGetBuffer(out var buffer)
            && buffer.Offset == 0
            && buffer.Array is not null
            && buffer.Count == buffer.Array.Length)
        {
            return buffer.Array;
        }

        return stream.ToArray();
    }

    public static int MeasureSerializedSize(IProtocolMessage message)
    {
        using var stream = new CountingStream();
        using var writer = new BinaryWriter(stream, Utf8, leaveOpen: true);
        WriteMessage(writer, message);
        writer.Flush();
        return checked((int)stream.Length);
    }

    /// <summary>
    /// Deserialize a protocol message, automatically decompressing if needed.
    /// </summary>
    public static bool TryDeserialize(byte[] payload, out IProtocolMessage? message)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (payload.Length < 2) // Need at least encoding byte + message type
        {
            message = null;
            return false;
        }

        try
        {
            // Read encoding and extract actual payload
            var (encoding, actualPayload) = ProtocolCodecCompression.ReadEncoding(payload);

            // Decompress if needed
            byte[] decompressed = encoding switch
            {
                MessageEncoding.None => actualPayload,
                MessageEncoding.LZ4 => ProtocolCodecCompression.Decompress(actualPayload),
                _ => throw new InvalidDataException($"Unknown message encoding: {encoding}")
            };

            using var stream = new MemoryStream(decompressed, 0, decompressed.Length, writable: false, publiclyVisible: true);
            return TryDeserializeCore(stream, out message);
        }
        catch (IOException)
        {
            message = null;
            return false;
        }
        catch (InvalidDataException)
        {
            message = null;
            return false;
        }
    }

    /// <summary>
    /// Deserialize a protocol message from a span, automatically decompressing if needed.
    /// </summary>
    public static bool TryDeserialize(ReadOnlySpan<byte> payload, out IProtocolMessage? message)
    {
        if (payload.Length < 2) // Need at least encoding byte + message type
        {
            message = null;
            return false;
        }

        try
        {
            // Convert span to array for processing (required for compression)
            var payloadArray = payload.ToArray();
            return TryDeserialize(payloadArray, out message);
        }
        catch (IOException)
        {
            message = null;
            return false;
        }
    }

    private static bool TryDeserializeCore(Stream stream, out IProtocolMessage? message)
    {
        message = null;
        try
        {
            using var reader = new BinaryReader(stream, Utf8, leaveOpen: true);
            var type = (MessageType)reader.ReadByte();

            message = type switch
            {
                MessageType.Hello => new HelloMessage(
                    ReadString(reader, MaxPlayerNameBytes),
                    reader.ReadInt32(),
                    reader.ReadUInt64(),
                    ReadString(reader, MaxFriendCodeBytes),
                    ReadString(reader, MaxPlayerCardBytes),
                    stream.Position < stream.Length ? (ConnectionIntent)reader.ReadByte() : ConnectionIntent.Join,
                    stream.Position < stream.Length ? new Guid(reader.ReadBytes(16)) : Guid.Empty),
                MessageType.Welcome => new WelcomeMessage(
                    ReadString(reader, MaxServerNameBytes),
                    reader.ReadInt32(),
                    reader.ReadInt32(),
                    ReadString(reader, MaxLevelNameBytes),
                    reader.ReadByte(),
                    reader.ReadInt32(),
                    reader.ReadBoolean(),
                    ReadString(reader, MaxMapUrlBytes),
                    ReadString(reader, MaxMapHashBytes),
                    reader.ReadSingle(),
                    stream.Position < stream.Length && reader.ReadBoolean()),
                MessageType.ConnectionDenied => new ConnectionDeniedMessage(ReadString(reader, MaxReasonBytes)),
                MessageType.PasswordRequest => new PasswordRequestMessage(),
                MessageType.PasswordSubmit => new PasswordSubmitMessage(ReadString(reader, MaxPasswordBytes)),
                MessageType.PasswordResult => new PasswordResultMessage(reader.ReadBoolean(), ReadString(reader, MaxReasonBytes)),
                MessageType.ChatSubmit => new ChatSubmitMessage(ReadString(reader, MaxChatBytes), reader.ReadBoolean()),
                MessageType.ChatRelay => new ChatRelayMessage(
                    reader.ReadByte(),
                    ReadString(reader, MaxPlayerNameBytes),
                    ReadString(reader, MaxChatBytes),
                    reader.ReadBoolean(),
                    stream.Position < stream.Length ? reader.ReadByte() : (byte)0),
                MessageType.AutoBalanceNotice => new AutoBalanceNoticeMessage(
                    (AutoBalanceNoticeKind)reader.ReadByte(),
                    ReadString(reader, MaxPlayerNameBytes),
                    reader.ReadByte(),
                    reader.ReadByte(),
                    reader.ReadInt32()),
                MessageType.SessionSlotChanged => new SessionSlotChangedMessage(reader.ReadByte()),
                MessageType.ServerStatusRequest => new ServerStatusRequestMessage(),
                MessageType.ServerStatusResponse => new ServerStatusResponseMessage(
                    ReadString(reader, MaxServerNameBytes),
                    ReadString(reader, MaxLevelNameBytes),
                    reader.ReadByte(),
                    reader.ReadInt32(),
                    reader.ReadInt32(),
                    reader.ReadInt32()),
                MessageType.ServerDetailsRequest => new ServerDetailsRequestMessage(),
                MessageType.ServerDetailsResponse => ReadServerDetailsResponse(reader),
                MessageType.InputState => new InputStateMessage(
                    reader.ReadUInt32(),
                    (InputButtons)reader.ReadUInt32(),
                    reader.ReadSingle(),
                    reader.ReadSingle(),
                    reader.ReadInt32(),
                    reader.ReadBoolean(),
                    reader.ReadSingle(),
                    reader.ReadSingle(),
                    stream.Position < stream.Length ? reader.ReadInt32() : -1),
                MessageType.ControlCommand => new ControlCommandMessage(
                    reader.ReadUInt32(),
                    (ControlCommandKind)reader.ReadByte(),
                    reader.ReadByte(),
                    ReadString(reader, MaxGameplayIdBytes)),
                MessageType.ControlAck => new ControlAckMessage(
                    reader.ReadUInt32(),
                    (ControlCommandKind)reader.ReadByte(),
                    reader.ReadBoolean()),
                MessageType.SnapshotAck => new SnapshotAckMessage(reader.ReadUInt64()),
                MessageType.PingRequest => new PingRequestMessage(reader.ReadUInt32()),
                MessageType.PingResponse => new PingResponseMessage(reader.ReadUInt32()),
                MessageType.PlayerProfileUpdate => new PlayerProfileUpdateMessage(
                    ReadString(reader, MaxPlayerNameBytes),
                    reader.ReadUInt64(),
                    ReadString(reader, MaxFriendCodeBytes),
                    ReadString(reader, MaxPlayerCardBytes)),
                MessageType.ClientPluginMessage => new ClientPluginMessage(
                    ReadString(reader, MaxPluginIdBytes),
                    ReadString(reader, MaxPluginIdBytes),
                    ReadString(reader, MaxPluginMessageTypeBytes),
                    ReadString(reader, MaxPluginPayloadBytes),
                    (PluginMessagePayloadFormat)reader.ReadByte(),
                    reader.ReadUInt16()),
                MessageType.ServerPluginMessage => new ServerPluginMessage(
                    ReadString(reader, MaxPluginIdBytes),
                    ReadString(reader, MaxPluginIdBytes),
                    ReadString(reader, MaxPluginMessageTypeBytes),
                    ReadString(reader, MaxPluginPayloadBytes),
                    (PluginMessagePayloadFormat)reader.ReadByte(),
                    reader.ReadUInt16()),
                MessageType.PlayerSocialProfileUpdate => ReadPlayerSocialProfileUpdate(reader),
                MessageType.CustomBubbleUpload => new CustomBubbleUploadMessage(
                    reader.ReadByte(),
                    reader.ReadUInt32(),
                    ReadCustomBubblePixels(reader)),
                MessageType.CustomBubbleState => new CustomBubbleStateMessage(
                    reader.ReadByte(),
                    reader.ReadByte(),
                    reader.ReadUInt32(),
                    ReadCustomBubblePixels(reader)),
                MessageType.CustomBubbleClear => new CustomBubbleClearMessage(reader.ReadByte()),
                MessageType.GameplayAccountAttachRequest => new GameplayAccountAttachRequestMessage(
                    reader.ReadUInt64(),
                    ReadString(reader, MaxGameplayTokenBytes)),
                MessageType.GameplayAccountAttachResult => new GameplayAccountAttachResultMessage(
                    reader.ReadUInt64(),
                    reader.ReadBoolean(),
                    ReadString(reader, MaxReasonBytes),
                    ReadString(reader, MaxFriendCodeBytes),
                    ReadString(reader, MaxPlayerNameBytes),
                    reader.ReadInt64(),
                    reader.ReadInt64(),
                    reader.ReadInt64()),
                MessageType.PlayerPointsState => new PlayerPointsStateMessage(
                    reader.ReadInt64(),
                    reader.ReadInt64(),
                    reader.ReadInt32(),
                    reader.ReadInt64()),
                MessageType.VoteCommand => new VoteCommandMessage(
                    (VoteCommandKind)reader.ReadByte(),
                    ReadString(reader, MaxVoteSubjectBytes),
                    reader.ReadInt32(),
                    reader.ReadByte(),
                    reader.ReadByte(),
                    reader.ReadUInt64(),
                    ReadString(reader, MaxVoteSubjectBytes)),
                MessageType.VoteState => ReadVoteState(reader),
                MessageType.VoteMenu => ReadVoteMenu(reader),
                MessageType.VoiceSubmit => new VoiceSubmitMessage(reader.ReadBoolean(), ReadAudioPacket(reader)),
                MessageType.AudioRelay => ReadAudioRelay(reader),
                MessageType.ServerAudioState => new ServerAudioStateMessage(reader.ReadUInt32(), reader.ReadBoolean(), reader.ReadBoolean(), reader.ReadUInt32(), reader.ReadBoolean(), reader.ReadBoolean(), ReadString(reader, AudioWireFormat.MaxTrackBytes), reader.ReadBoolean(), reader.ReadBoolean(), reader.ReadUInt32()),
                MessageType.VoiceChannelMembership => new VoiceChannelMembershipMessage(reader.ReadUInt32(), reader.ReadBoolean()),
                MessageType.LastToDieCommand => ReadLastToDieCommand(reader),
                MessageType.LastToDieCommandResult => ReadLastToDieCommandResult(reader),
                MessageType.LastToDieRunSnapshot => ReadLastToDieRunSnapshot(reader),
                MessageType.LastToDieRunSnapshotAck => ReadLastToDieRunSnapshotAck(reader),
                MessageType.Snapshot => ReadSnapshot(reader),
                _ => null,
            };

            if (message is null || stream.Position != stream.Length)
            {
                message = null;
                return false;
            }

            return true;
        }
        catch (EndOfStreamException)
        {
            return false;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static void WriteString(BinaryWriter writer, string value, int maxBytes, string fieldName)
    {
        var bytes = Utf8.GetBytes(value);
        if (bytes.Length > ushort.MaxValue)
        {
            throw new InvalidOperationException("Protocol string exceeds ushort length limit.");
        }
        if (bytes.Length > maxBytes)
        {
            throw new InvalidOperationException($"{fieldName} exceeds protocol string limit of {maxBytes} bytes.");
        }

        writer.Write((ushort)bytes.Length);
        writer.Write(bytes);
    }

    private static string ReadString(BinaryReader reader, int maxBytes)
    {
        var length = reader.ReadUInt16();
        if (length > maxBytes)
        {
            throw new IOException($"Protocol string exceeds configured limit of {maxBytes} bytes.");
        }

        var bytes = reader.ReadBytes(length);
        if (bytes.Length != length)
        {
            throw new EndOfStreamException();
        }

        return Utf8.GetString(bytes);
    }

    public static string TruncateUtf8(string value, int maxBytes)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (maxBytes <= 0 || value.Length == 0)
        {
            return string.Empty;
        }

        if (Utf8.GetByteCount(value) <= maxBytes)
        {
            return value;
        }

        var length = value.Length;
        while (length > 0)
        {
            length -= 1;
            var candidate = value[..length];
            if (Utf8.GetByteCount(candidate) <= maxBytes)
            {
                return candidate;
            }
        }

        return string.Empty;
    }

    private static void WriteMessage(BinaryWriter writer, IProtocolMessage message)
    {
        writer.Write((byte)message.Type);

        switch (message)
        {
            case HelloMessage hello:
                WriteString(writer, hello.Name, MaxPlayerNameBytes, nameof(hello.Name));
                writer.Write(hello.Version);
                writer.Write(hello.BadgeMask);
                WriteString(writer, hello.FriendCode ?? string.Empty, MaxFriendCodeBytes, nameof(hello.FriendCode));
                WriteString(writer, hello.PlayerCardJson ?? string.Empty, MaxPlayerCardBytes, nameof(hello.PlayerCardJson));
                writer.Write((byte)hello.Intent);
                writer.Write(hello.ClientInstanceId.ToByteArray());
                break;
            case WelcomeMessage welcome:
                WriteString(writer, welcome.ServerName, MaxServerNameBytes, nameof(welcome.ServerName));
                writer.Write(welcome.Version);
                writer.Write(welcome.TickRate);
                WriteString(writer, welcome.LevelName, MaxLevelNameBytes, nameof(welcome.LevelName));
                writer.Write(welcome.PlayerSlot);
                writer.Write(welcome.MaxPlayerCount);
                writer.Write(welcome.IsCustomMap);
                WriteString(writer, welcome.MapDownloadUrl, MaxMapUrlBytes, nameof(welcome.MapDownloadUrl));
                WriteString(writer, welcome.MapContentHash, MaxMapHashBytes, nameof(welcome.MapContentHash));
                writer.Write(welcome.MapScale);
                writer.Write(welcome.LocalPredictionEnabled);
                break;
            case ConnectionDeniedMessage denied:
                WriteString(writer, denied.Reason, MaxReasonBytes, nameof(denied.Reason));
                break;
            case PasswordRequestMessage:
                break;
            case PasswordSubmitMessage passwordSubmit:
                WriteString(writer, passwordSubmit.Password, MaxPasswordBytes, nameof(passwordSubmit.Password));
                break;
            case PasswordResultMessage passwordResult:
                writer.Write(passwordResult.Accepted);
                WriteString(writer, passwordResult.Reason, MaxReasonBytes, nameof(passwordResult.Reason));
                break;
            case ChatSubmitMessage chatSubmit:
                WriteString(writer, chatSubmit.Text, MaxChatBytes, nameof(chatSubmit.Text));
                writer.Write(chatSubmit.TeamOnly);
                break;
            case ChatRelayMessage chatRelay:
                writer.Write(chatRelay.Team);
                WriteString(writer, chatRelay.PlayerName, MaxPlayerNameBytes, nameof(chatRelay.PlayerName));
                WriteString(writer, chatRelay.Text, MaxChatBytes, nameof(chatRelay.Text));
                writer.Write(chatRelay.TeamOnly);
                writer.Write(chatRelay.PlayerSlot);
                break;
            case AutoBalanceNoticeMessage notice:
                writer.Write((byte)notice.Kind);
                WriteString(writer, notice.PlayerName, MaxPlayerNameBytes, nameof(notice.PlayerName));
                writer.Write(notice.FromTeam);
                writer.Write(notice.ToTeam);
                writer.Write(notice.DelaySeconds);
                break;
            case SessionSlotChangedMessage slotChanged:
                writer.Write(slotChanged.PlayerSlot);
                break;
            case ServerStatusRequestMessage:
                break;
            case ServerStatusResponseMessage status:
                WriteString(writer, status.ServerName, MaxServerNameBytes, nameof(status.ServerName));
                WriteString(writer, status.LevelName, MaxLevelNameBytes, nameof(status.LevelName));
                writer.Write(status.GameMode);
                writer.Write(status.PlayerCount);
                writer.Write(status.MaxPlayerCount);
                writer.Write(status.SpectatorCount);
                break;
            case ServerDetailsRequestMessage:
                break;
            case ServerDetailsResponseMessage details:
                WriteServerDetailsResponse(writer, details);
                break;
            case InputStateMessage input:
                writer.Write(input.Sequence);
                writer.Write((uint)input.Buttons);
                writer.Write(input.AimRelX);
                writer.Write(input.AimRelY);
                writer.Write(input.ChatBubbleFrameIndex);
                writer.Write(input.IsUsingBinoculars);
                writer.Write(input.BinocularsFocusX);
                writer.Write(input.BinocularsFocusY);
                writer.Write(input.PingMilliseconds);
                break;
            case ControlCommandMessage command:
                writer.Write(command.Sequence);
                writer.Write((byte)command.Kind);
                writer.Write(command.Value);
                WriteString(writer, command.TextValue ?? string.Empty, MaxGameplayIdBytes, nameof(command.TextValue));
                break;
            case ControlAckMessage ack:
                writer.Write(ack.Sequence);
                writer.Write((byte)ack.Kind);
                writer.Write(ack.Accepted);
                break;
            case SnapshotAckMessage snapshotAck:
                writer.Write(snapshotAck.Frame);
                break;
            case PingRequestMessage pingRequest:
                writer.Write(pingRequest.Sequence);
                break;
            case PingResponseMessage pingResponse:
                writer.Write(pingResponse.Sequence);
                break;
            case PlayerProfileUpdateMessage profileUpdate:
                WriteString(writer, profileUpdate.Name, MaxPlayerNameBytes, nameof(profileUpdate.Name));
                writer.Write(profileUpdate.BadgeMask);
                WriteString(writer, profileUpdate.FriendCode ?? string.Empty, MaxFriendCodeBytes, nameof(profileUpdate.FriendCode));
                WriteString(writer, profileUpdate.PlayerCardJson ?? string.Empty, MaxPlayerCardBytes, nameof(profileUpdate.PlayerCardJson));
                break;
            case ClientPluginMessage clientPluginMessage:
                WriteString(writer, clientPluginMessage.SourcePluginId, MaxPluginIdBytes, nameof(clientPluginMessage.SourcePluginId));
                WriteString(writer, clientPluginMessage.TargetPluginId, MaxPluginIdBytes, nameof(clientPluginMessage.TargetPluginId));
                WriteString(writer, clientPluginMessage.MessageTypeName, MaxPluginMessageTypeBytes, nameof(clientPluginMessage.MessageTypeName));
                WriteString(writer, clientPluginMessage.Payload, MaxPluginPayloadBytes, nameof(clientPluginMessage.Payload));
                writer.Write((byte)clientPluginMessage.PayloadFormat);
                writer.Write(clientPluginMessage.SchemaVersion);
                break;
            case ServerPluginMessage serverPluginMessage:
                WriteString(writer, serverPluginMessage.SourcePluginId, MaxPluginIdBytes, nameof(serverPluginMessage.SourcePluginId));
                WriteString(writer, serverPluginMessage.TargetPluginId, MaxPluginIdBytes, nameof(serverPluginMessage.TargetPluginId));
                WriteString(writer, serverPluginMessage.MessageTypeName, MaxPluginMessageTypeBytes, nameof(serverPluginMessage.MessageTypeName));
                WriteString(writer, serverPluginMessage.Payload, MaxPluginPayloadBytes, nameof(serverPluginMessage.Payload));
                writer.Write((byte)serverPluginMessage.PayloadFormat);
                writer.Write(serverPluginMessage.SchemaVersion);
                break;
            case PlayerSocialProfileUpdateMessage socialProfileUpdate:
                WritePlayerSocialProfileUpdate(writer, socialProfileUpdate);
                break;
            case CustomBubbleUploadMessage customBubbleUpload:
                writer.Write(customBubbleUpload.Slot);
                writer.Write(customBubbleUpload.Revision);
                WriteCustomBubblePixels(writer, customBubbleUpload.Rgba64Pixels, nameof(customBubbleUpload.Rgba64Pixels));
                break;
            case CustomBubbleStateMessage customBubbleState:
                writer.Write(customBubbleState.PlayerSlot);
                writer.Write(customBubbleState.Slot);
                writer.Write(customBubbleState.Revision);
                WriteCustomBubblePixels(writer, customBubbleState.Rgba64Pixels, nameof(customBubbleState.Rgba64Pixels));
                break;
            case CustomBubbleClearMessage customBubbleClear:
                writer.Write(customBubbleClear.PlayerSlot);
                break;
            case GameplayAccountAttachRequestMessage attachRequest:
                writer.Write(attachRequest.RequestId);
                WriteString(writer, attachRequest.GameplayToken, MaxGameplayTokenBytes, nameof(attachRequest.GameplayToken));
                break;
            case GameplayAccountAttachResultMessage attachResult:
                writer.Write(attachResult.RequestId);
                writer.Write(attachResult.Attached);
                WriteString(writer, attachResult.Reason, MaxReasonBytes, nameof(attachResult.Reason));
                WriteString(writer, attachResult.FriendCode, MaxFriendCodeBytes, nameof(attachResult.FriendCode));
                WriteString(writer, attachResult.DisplayName, MaxPlayerNameBytes, nameof(attachResult.DisplayName));
                writer.Write(attachResult.LifetimePoints);
                writer.Write(attachResult.WalletBalance);
                writer.Write(attachResult.ProfileRevision);
                break;
            case PlayerPointsStateMessage pointsState:
                writer.Write(pointsState.LifetimePoints);
                writer.Write(pointsState.WalletBalance);
                writer.Write(pointsState.GlobalRank);
                writer.Write(pointsState.ProfileRevision);
                break;
            case VoteCommandMessage voteCommand:
                writer.Write((byte)voteCommand.Command);
                WriteString(writer, voteCommand.Target, MaxVoteSubjectBytes, nameof(voteCommand.Target));
                writer.Write(voteCommand.AreaIndex);
                writer.Write(voteCommand.TargetSlot);
                writer.Write(voteCommand.Team);
                writer.Write(voteCommand.VoteId);
                WriteString(writer, voteCommand.Argument, MaxVoteSubjectBytes, nameof(voteCommand.Argument));
                break;
            case VoteStateMessage voteState:
                WriteVoteState(writer, voteState);
                break;
            case VoteMenuMessage voteMenu:
                WriteVoteMenu(writer, voteMenu);
                break;
            case VoiceSubmitMessage voice:
                writer.Write(voice.TeamOnly);
                WriteAudioPacket(writer, voice.Packet);
                break;
            case AudioRelayMessage audio:
                WriteAudioRelay(writer, audio);
                break;
            case ServerAudioStateMessage audioState:
                writer.Write(audioState.Revision);
                writer.Write(audioState.VoiceEnabled);
                writer.Write(audioState.TeamOnly);
                writer.Write(audioState.JukeboxStreamId);
                writer.Write(audioState.JukeboxPlaying);
                writer.Write(audioState.JukeboxPaused);
                WriteString(writer, audioState.TrackName, AudioWireFormat.MaxTrackBytes, nameof(audioState.TrackName));
                writer.Write(audioState.VoiceChannelRequiresJoin);
                writer.Write(audioState.VoiceChannelJoined);
                writer.Write(audioState.VoiceChannelRevision);
                break;
            case VoiceChannelMembershipMessage membership:
                writer.Write(membership.Revision);
                writer.Write(membership.Joined);
                break;
            case LastToDieCommandMessage lastToDieCommand:
                WriteLastToDieCommand(writer, lastToDieCommand);
                break;
            case LastToDieCommandResultMessage lastToDieCommandResult:
                WriteLastToDieCommandResult(writer, lastToDieCommandResult);
                break;
            case LastToDieRunSnapshotMessage lastToDieRunSnapshot:
                WriteLastToDieRunSnapshot(writer, lastToDieRunSnapshot);
                break;
            case LastToDieRunSnapshotAckMessage lastToDieRunSnapshotAck:
                WriteLastToDieRunSnapshotAck(writer, lastToDieRunSnapshotAck);
                break;
            case SnapshotMessage snapshot:
                WriteSnapshot(writer, snapshot);
                break;
            default:
                throw new InvalidOperationException($"Unsupported protocol message type: {message.GetType().Name}");
        }
    }

    private static VoteStateMessage ReadVoteState(BinaryReader reader)
    {
        return new VoteStateMessage(
            reader.ReadUInt64(),
            reader.ReadUInt32(),
            (ServerVoteEventKind)reader.ReadByte(),
            (ServerVoteKind)reader.ReadByte(),
            ReadString(reader, MaxVoteSubjectBytes),
            ReadString(reader, MaxPlayerNameBytes),
            ReadString(reader, MaxPlayerNameBytes),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            ReadString(reader, MaxVoteMessageBytes));
    }

    private static void WriteVoteState(BinaryWriter writer, VoteStateMessage state)
    {
        writer.Write(state.VoteId);
        writer.Write(state.Revision);
        writer.Write((byte)state.Event);
        writer.Write((byte)state.Kind);
        WriteString(writer, state.Subject, MaxVoteSubjectBytes, nameof(state.Subject));
        WriteString(writer, state.InitiatorName, MaxPlayerNameBytes, nameof(state.InitiatorName));
        WriteString(writer, state.ActorName, MaxPlayerNameBytes, nameof(state.ActorName));
        writer.Write(state.YesVotes);
        writer.Write(state.NoVotes);
        writer.Write(state.RequiredYesVotes);
        writer.Write(state.EligibleVoters);
        writer.Write(state.RemainingTicks);
        WriteString(writer, state.Message, MaxVoteMessageBytes, nameof(state.Message));
    }

    private static VoteMenuMessage ReadVoteMenu(BinaryReader reader)
    {
        var mapCount = reader.ReadUInt16();
        if (mapCount > MaxVoteMenuEntries)
        {
            throw new IOException("Vote menu map list exceeds protocol limits.");
        }

        var maps = new List<VoteMenuMapEntry>(mapCount);
        for (var index = 0; index < mapCount; index += 1)
        {
            maps.Add(new VoteMenuMapEntry(
                ReadString(reader, MaxLevelNameBytes),
                ReadString(reader, MaxVoteSubjectBytes),
                reader.ReadInt32()));
        }

        var playerCount = reader.ReadUInt16();
        if (playerCount > MaxVoteMenuEntries)
        {
            throw new IOException("Vote menu player list exceeds protocol limits.");
        }

        var players = new List<VoteMenuPlayerEntry>(playerCount);
        for (var index = 0; index < playerCount; index += 1)
        {
            players.Add(new VoteMenuPlayerEntry(
                reader.ReadByte(),
                ReadString(reader, MaxPlayerNameBytes),
                reader.ReadByte(),
                reader.ReadBoolean()));
        }

        var customCount = reader.ReadUInt16();
        if (customCount > MaxVoteMenuEntries)
        {
            throw new IOException("Vote menu custom vote list exceeds protocol limits.");
        }

        var customVotes = new List<VoteMenuCustomEntry>(customCount);
        for (var index = 0; index < customCount; index += 1)
        {
            customVotes.Add(new VoteMenuCustomEntry(
                ReadString(reader, MaxVoteSubjectBytes),
                ReadString(reader, MaxVoteSubjectBytes),
                ReadString(reader, MaxVoteMessageBytes),
                reader.ReadByte()));
        }

        return new VoteMenuMessage(
            maps,
            players,
            reader.ReadBoolean(),
            reader.ReadBoolean(),
            reader.ReadInt32(),
            reader.ReadUInt64(),
            reader.ReadBoolean(),
            reader.ReadBoolean(),
            reader.ReadBoolean(),
            customVotes);
    }

    private static void WriteVoteMenu(BinaryWriter writer, VoteMenuMessage menu)
    {
        if (menu.Maps.Count > MaxVoteMenuEntries
            || menu.Players.Count > MaxVoteMenuEntries
            || menu.CustomVotes.Count > MaxVoteMenuEntries)
        {
            throw new InvalidOperationException("Vote menu exceeds protocol collection limits.");
        }

        writer.Write((ushort)menu.Maps.Count);
        for (var index = 0; index < menu.Maps.Count; index += 1)
        {
            var map = menu.Maps[index];
            WriteString(writer, map.LevelName, MaxLevelNameBytes, nameof(map.LevelName));
            WriteString(writer, map.DisplayName, MaxVoteSubjectBytes, nameof(map.DisplayName));
            writer.Write(map.AreaCount);
        }

        writer.Write((ushort)menu.Players.Count);
        for (var index = 0; index < menu.Players.Count; index += 1)
        {
            var player = menu.Players[index];
            writer.Write(player.Slot);
            WriteString(writer, player.DisplayName, MaxPlayerNameBytes, nameof(player.DisplayName));
            writer.Write(player.Team);
            writer.Write(player.IsMuted);
        }

        writer.Write((ushort)menu.CustomVotes.Count);
        for (var index = 0; index < menu.CustomVotes.Count; index += 1)
        {
            var customVote = menu.CustomVotes[index];
            WriteString(writer, customVote.Id, MaxVoteSubjectBytes, nameof(customVote.Id));
            WriteString(writer, customVote.DisplayName, MaxVoteSubjectBytes, nameof(customVote.DisplayName));
            WriteString(writer, customVote.Description, MaxVoteMessageBytes, nameof(customVote.Description));
            writer.Write(customVote.TargetKind);
        }

        writer.Write(menu.VipVoteAvailable);
        writer.Write(menu.VoteActive);
        writer.Write(menu.CooldownTicksRemaining);
        writer.Write(menu.ActiveVoteId);
        writer.Write(menu.KickVoteAvailable);
        writer.Write(menu.MuteVoteAvailable);
        writer.Write(menu.ScrambleVoteAvailable);
    }

    private static void WriteCustomBubblePixels(BinaryWriter writer, byte[]? pixels, string fieldName)
    {
        if (pixels is null || pixels.Length != CustomBubbleRgba64PayloadBytes)
        {
            throw new InvalidOperationException($"{fieldName} must be exactly {CustomBubbleRgba64PayloadBytes} bytes.");
        }

        writer.Write((ushort)pixels.Length);
        writer.Write(pixels);
    }

    private static byte[] ReadCustomBubblePixels(BinaryReader reader)
    {
        var length = reader.ReadUInt16();
        if (length != CustomBubbleRgba64PayloadBytes)
        {
            throw new IOException($"Custom bubble payload must be exactly {CustomBubbleRgba64PayloadBytes} bytes.");
        }

        var pixels = reader.ReadBytes(length);
        if (pixels.Length != length)
        {
            throw new EndOfStreamException();
        }

        return pixels;
    }

    private static void WritePlayerSocialProfileUpdate(BinaryWriter writer, PlayerSocialProfileUpdateMessage update)
    {
        var profileCount = update.Profiles?.Count ?? 0;
        var removedCount = update.RemovedSlots?.Count ?? 0;
        var titleCount = update.Titles?.Count ?? 0;
        if (profileCount > byte.MaxValue || removedCount > byte.MaxValue || titleCount > byte.MaxValue)
        {
            throw new InvalidOperationException("Player social profile update exceeds protocol collection limits.");
        }

        writer.Write((byte)profileCount);
        for (var index = 0; index < profileCount; index += 1)
        {
            var profile = update.Profiles![index];
            writer.Write(profile.Slot);
            WriteString(writer, profile.DisplayName ?? string.Empty, MaxPlayerNameBytes, nameof(profile.DisplayName));
            WriteString(writer, profile.FriendCode ?? string.Empty, MaxFriendCodeBytes, nameof(profile.FriendCode));
            WriteString(writer, profile.PlayerCardJson ?? string.Empty, MaxPlayerCardBytes, nameof(profile.PlayerCardJson));
        }

        writer.Write((byte)removedCount);
        for (var index = 0; index < removedCount; index += 1)
        {
            writer.Write(update.RemovedSlots![index]);
        }

        // Appended to preserve decoding compatibility with older profile-update payloads.
        writer.Write((byte)titleCount);
        for (var index = 0; index < titleCount; index += 1)
        {
            var title = update.Titles![index];
            writer.Write(title.Slot);
            WriteString(writer, title.Text ?? string.Empty, MaxServerPlayerTitleBytes, nameof(title.Text));
            writer.Write(title.ColorRgb & 0xFFFFFFu);
            writer.Write(title.Rainbow);
        }
    }

    private static PlayerSocialProfileUpdateMessage ReadPlayerSocialProfileUpdate(BinaryReader reader)
    {
        var profileCount = reader.ReadByte();
        var profiles = new List<PlayerSocialProfileState>(profileCount);
        for (var index = 0; index < profileCount; index += 1)
        {
            profiles.Add(new PlayerSocialProfileState(
                reader.ReadByte(),
                ReadString(reader, MaxPlayerNameBytes),
                ReadString(reader, MaxFriendCodeBytes),
                ReadString(reader, MaxPlayerCardBytes)));
        }

        var removedCount = reader.ReadByte();
        var removedSlots = new byte[removedCount];
        for (var index = 0; index < removedSlots.Length; index += 1)
        {
            removedSlots[index] = reader.ReadByte();
        }

        if (reader.BaseStream.Position >= reader.BaseStream.Length)
        {
            return new PlayerSocialProfileUpdateMessage(profiles, removedSlots, Array.Empty<PlayerServerTitleState>());
        }

        var titleCount = reader.ReadByte();
        var titles = new List<PlayerServerTitleState>(titleCount);
        for (var index = 0; index < titleCount; index += 1)
        {
            titles.Add(new PlayerServerTitleState(
                reader.ReadByte(),
                ReadString(reader, MaxServerPlayerTitleBytes),
                reader.ReadUInt32() & 0xFFFFFFu,
                reader.ReadBoolean()));
        }

        return new PlayerSocialProfileUpdateMessage(profiles, removedSlots, titles);
    }

    private static void WriteServerDetailsResponse(BinaryWriter writer, ServerDetailsResponseMessage details)
    {
        WriteString(writer, details.ServerName, MaxServerNameBytes, nameof(details.ServerName));
        WriteString(writer, details.LevelName, MaxLevelNameBytes, nameof(details.LevelName));
        writer.Write(details.GameMode);
        writer.Write(details.PlayerCount);
        writer.Write(details.MaxPlayerCount);
        writer.Write(details.SpectatorCount);
        writer.Write(details.RedScore);
        writer.Write(details.BlueScore);
        writer.Write(details.TimeRemainingTicks);
        writer.Write(details.TimeLimitTicks);
        writer.Write(details.TickRate);

        var rosterCount = details.Roster?.Count ?? 0;
        if (rosterCount > MaxServerDetailsRosterEntries)
        {
            throw new InvalidOperationException("Server details roster exceeds protocol collection limits.");
        }

        writer.Write((byte)rosterCount);
        for (var index = 0; index < rosterCount; index += 1)
        {
            var entry = details.Roster![index];
            writer.Write(entry.Slot);
            WriteString(writer, entry.Name ?? string.Empty, MaxPlayerNameBytes, nameof(entry.Name));
            writer.Write(entry.Team);
            writer.Write(entry.ClassId);
            writer.Write(entry.IsSpectator);
            writer.Write(entry.IsAlive);
            writer.Write(entry.IsAwaitingJoin);
            writer.Write(entry.Health);
            writer.Write(entry.MaxHealth);
            writer.Write(entry.Kills);
            writer.Write(entry.Deaths);
            writer.Write(entry.Assists);
            writer.Write(entry.Caps);
            writer.Write(entry.Points);
        }
    }

    private static ServerDetailsResponseMessage ReadServerDetailsResponse(BinaryReader reader)
    {
        var serverName = ReadString(reader, MaxServerNameBytes);
        var levelName = ReadString(reader, MaxLevelNameBytes);
        var gameMode = reader.ReadByte();
        var playerCount = reader.ReadInt32();
        var maxPlayerCount = reader.ReadInt32();
        var spectatorCount = reader.ReadInt32();
        var redScore = reader.ReadInt32();
        var blueScore = reader.ReadInt32();
        var timeRemainingTicks = reader.ReadInt32();
        var timeLimitTicks = reader.ReadInt32();
        var tickRate = reader.ReadInt32();
        var rosterCount = reader.ReadByte();
        if (rosterCount > MaxServerDetailsRosterEntries)
        {
            throw new IOException("Server details roster exceeds protocol collection limits.");
        }

        var roster = new List<ServerDetailsRosterEntry>(rosterCount);
        for (var index = 0; index < rosterCount; index += 1)
        {
            roster.Add(new ServerDetailsRosterEntry(
                reader.ReadByte(),
                ReadString(reader, MaxPlayerNameBytes),
                reader.ReadByte(),
                reader.ReadByte(),
                reader.ReadBoolean(),
                reader.ReadBoolean(),
                reader.ReadBoolean(),
                reader.ReadInt16(),
                reader.ReadInt16(),
                reader.ReadInt16(),
                reader.ReadInt16(),
                reader.ReadInt16(),
                reader.ReadInt16(),
                reader.ReadSingle()));
        }

        return new ServerDetailsResponseMessage(
            serverName,
            levelName,
            gameMode,
            playerCount,
            maxPlayerCount,
            spectatorCount,
            redScore,
            blueScore,
            timeRemainingTicks,
            timeLimitTicks,
            tickRate,
            roster);
    }

    private sealed class CountingStream : Stream
    {
        private long _position;

        public override bool CanRead => false;
        public override bool CanSeek => true;
        public override bool CanWrite => true;
        public override long Length => _position;
        public override long Position
        {
            get => _position;
            set => _position = value;
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            _position = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => _position + offset,
                SeekOrigin.End => Length + offset,
                _ => throw new ArgumentOutOfRangeException(nameof(origin)),
            };
            return _position;
        }

        public override void SetLength(long value)
        {
            _position = value;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _position += count;
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            _position += buffer.Length;
        }

        public override void WriteByte(byte value)
        {
            _position += 1;
        }
    }
}
