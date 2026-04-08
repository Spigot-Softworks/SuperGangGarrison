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

    public static byte[] Serialize(IProtocolMessage message)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Utf8, leaveOpen: true);
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
                writer.Write(welcome.IsCustomMap);
                WriteString(writer, welcome.MapDownloadUrl, MaxMapUrlBytes, nameof(welcome.MapDownloadUrl));
                WriteString(writer, welcome.MapContentHash, MaxMapHashBytes, nameof(welcome.MapContentHash));
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

        writer.Flush();
        return stream.ToArray();
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> payload, out IProtocolMessage? message)
    {
        message = null;
        if (payload.Length < 1)
        {
            return false;
        }

        try
        {
            using var stream = new MemoryStream(payload.ToArray(), writable: false);
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
                    reader.ReadBoolean(),
                    ReadString(reader, MaxMapUrlBytes),
                    ReadString(reader, MaxMapHashBytes)),
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
                    reader.ReadInt32()),
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
}
