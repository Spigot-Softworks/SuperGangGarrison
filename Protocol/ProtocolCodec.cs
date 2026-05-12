using System;
using System.IO;
using System.Text;

namespace OpenGarrison.Protocol;

public static partial class ProtocolCodec
{
    private const int MaxPlayerNameBytes = 80;
    private const int MaxServerNameBytes = 128;
    private const int MaxLevelNameBytes = 64;
    private const int MaxMapUrlBytes = 512;
    private const int MaxMapHashBytes = 96;
    public const int MaxReasonBytes = 128;
    private const int MaxPasswordBytes = 64;
    public const int MaxChatBytes = 180;
    public const int MaxPluginIdBytes = 80;
    public const int MaxPluginMessageTypeBytes = 80;
    public const int MaxPluginPayloadBytes = 1024;
    private const int MaxAssetNameBytes = 64;
    private const int MaxKillMessageBytes = 160;
    private const int MaxGameplayIdBytes = 96;
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
        var uncompressed = SerializeUncompressed(message);

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
        return Serialize(message, size);
    }

    public static byte[] Serialize(IProtocolMessage message, int measuredSize)
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
                MessageType.Hello => new HelloMessage(ReadString(reader, MaxPlayerNameBytes), reader.ReadInt32(), reader.ReadUInt64()),
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
                    reader.ReadSingle()),
                MessageType.ConnectionDenied => new ConnectionDeniedMessage(ReadString(reader, MaxReasonBytes)),
                MessageType.PasswordRequest => new PasswordRequestMessage(),
                MessageType.PasswordSubmit => new PasswordSubmitMessage(ReadString(reader, MaxPasswordBytes)),
                MessageType.PasswordResult => new PasswordResultMessage(reader.ReadBoolean(), ReadString(reader, MaxReasonBytes)),
                MessageType.ChatSubmit => new ChatSubmitMessage(ReadString(reader, MaxChatBytes), reader.ReadBoolean()),
                MessageType.ChatRelay => new ChatRelayMessage(
                    reader.ReadByte(),
                    ReadString(reader, MaxPlayerNameBytes),
                    ReadString(reader, MaxChatBytes),
                    reader.ReadBoolean()),
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
                MessageType.InputState => new InputStateMessage(
                    reader.ReadUInt32(),
                    (InputButtons)reader.ReadUInt16(),
                    reader.ReadSingle(),
                    reader.ReadSingle(),
                    reader.ReadInt32(),
                    reader.ReadBoolean(),
                    reader.ReadSingle(),
                    reader.ReadSingle()),
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
                MessageType.PlayerProfileUpdate => new PlayerProfileUpdateMessage(
                    ReadString(reader, MaxPlayerNameBytes),
                    reader.ReadUInt64()),
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
            case InputStateMessage input:
                writer.Write(input.Sequence);
                writer.Write((ushort)input.Buttons);
                writer.Write(input.AimWorldX);
                writer.Write(input.AimWorldY);
                writer.Write(input.ChatBubbleFrameIndex);
                writer.Write(input.IsUsingBinoculars);
                writer.Write(input.BinocularsFocusX);
                writer.Write(input.BinocularsFocusY);
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
            case PlayerProfileUpdateMessage profileUpdate:
                WriteString(writer, profileUpdate.Name, MaxPlayerNameBytes, nameof(profileUpdate.Name));
                writer.Write(profileUpdate.BadgeMask);
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
            case SnapshotMessage snapshot:
                WriteSnapshot(writer, snapshot);
                break;
            default:
                throw new InvalidOperationException($"Unsupported protocol message type: {message.GetType().Name}");
        }
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
