#nullable enable

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using OpenGarrison.Protocol.Gg2;

namespace OpenGarrison.Client;

/// <summary>
/// A stock GG2 TCP session. The handshake (hello, password, map download, slot
/// reservation) reads the socket directly; after joining, every server message is framed
/// by the GG2 codec (<see cref="Gg2ServerMessageReader"/>) before anything interprets it,
/// so a message the session can't fully apply never desynchronises the stream. The
/// session then turns GG2 state into SGG client messages.
/// </summary>
internal sealed class LegacyGg2NetworkClientTransport : INetworkClientMessageTransport
{
    private const int ReceiveChunkBytes = 16 * 1024;

    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly object _sendLock = new();
    private readonly ConcurrentQueue<byte[]> _inbound = new();
    private readonly Dictionary<ControlCommandKind, uint> _lastControlCommandSequences = new();
    private readonly TaskCompletionSource<string> _passwordSubmission = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Gg2InputAckEstimator _inputAcks = new();
    private readonly string _host;
    private readonly int _port;
    private readonly string? _mapCacheDirectory;
    private string? _disconnectReason;
    private int _disposed;
    private int _helloStarted;
    private int _joined;
    private int _chatPluginId = -1;
    private int _localClassId = (int)PlayerClass.Scout;
    private int _localHasCharacter;
    private int _localHasSentry;
    private string _currentMapName = string.Empty;
    private string _currentMapHash = string.Empty;
    private bool _wasSecondaryHeld;
    private bool _wasDropIntelHeld;
    private bool _welcomed;
    private byte _localSlot;
    private bool _joinMapApplied;

    private LegacyGg2NetworkClientTransport(TcpClient client, string host, int port, string? mapCacheDirectory)
    {
        _client = client;
        _stream = client.GetStream();
        _host = host;
        _port = port;
        _mapCacheDirectory = mapCacheDirectory;
    }

    public bool HasPendingMessages => !_inbound.IsEmpty;
    public bool IsLoopbackConnection => IPAddress.TryParse(_host, out var address) && IPAddress.IsLoopback(address);
    public string RemoteDescription => $"gg2:{_host}:{_port}";
    public int ReceiveTimeoutMilliseconds => 15000;
    internal string CurrentMapName => Volatile.Read(ref _currentMapName);
    internal string CurrentMapHash => Volatile.Read(ref _currentMapHash);

    /// <summary>Messages the session could not apply and skipped (the stream stayed in sync).</summary>
    internal int SkippedMessageCount { get; private set; }

    public static bool TryConnect(string host, int port, out INetworkClientMessageTransport? transport, out string error)
        => TryConnect(host, port, null, out transport, out error);

    internal static bool TryConnect(string host, int port, string? mapCacheDirectory,
        out INetworkClientMessageTransport? transport, out string error)
    {
        transport = null;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(host) || port is < 1 or > 65535)
        {
            error = "A GG2 host and valid TCP port are required.";
            return false;
        }

        var client = new TcpClient();
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(4));
            client.ConnectAsync(host.Trim(), port, timeout.Token).GetAwaiter().GetResult();
            client.NoDelay = true;
            client.ReceiveTimeout = 12000;
            client.SendTimeout = 5000;
            transport = new LegacyGg2NetworkClientTransport(client, host.Trim(), port, mapCacheDirectory);
            return true;
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException or IOException)
        {
            client.Dispose();
            error = ex is OperationCanceledException ? "GG2 TCP connection timed out." : ex.Message;
            return false;
        }
    }

    public bool TryReceive(out byte[] payload) => _inbound.TryDequeue(out payload!);

    public bool TryConsumeDisconnectReason(out string reason)
    {
        reason = Interlocked.Exchange(ref _disconnectReason, null) ?? string.Empty;
        return reason.Length > 0;
    }

    public void Send(byte[] payload)
    {
        if (Volatile.Read(ref _disposed) != 0
            || !ProtocolCodec.TryDeserialize(payload, out var message)
            || message is null)
        {
            return;
        }

        try
        {
            switch (message)
            {
                case HelloMessage hello when Interlocked.Exchange(ref _helloStarted, 1) == 0:
                    _ = Task.Run(() => ReadServerLoop(hello.Name));
                    break;
                case PasswordSubmitMessage submit:
                    _passwordSubmission.TrySetResult(submit.Password ?? string.Empty);
                    break;
                case InputStateMessage input when Volatile.Read(ref _joined) != 0:
                    if (input.BundledControlCommands is { } bundledCommands)
                    {
                        for (var index = 0; index < bundledCommands.Count; index += 1)
                        {
                            SendControl(bundledCommands[index]);
                        }
                    }

                    SendStockInputEdges(input);
                    SendChatBubble(input.ChatBubbleFrameIndex);
                    var wire = LegacyGg2Wire.CreateInputState(input);
                    WriteRaw(wire);
                    _inputAcks.RecordSent(input.Sequence, wire[1]);
                    break;
                case ControlCommandMessage command when Volatile.Read(ref _joined) != 0:
                    SendControl(command);
                    break;
                case ChatSubmitMessage chat when Volatile.Read(ref _chatPluginId) >= 0:
                    SendChat(chat);
                    break;
                case PingRequestMessage ping:
                    Enqueue(new PingResponseMessage(ping.Sequence));
                    break;
            }
        }
        catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
        {
            Interlocked.CompareExchange(ref _disconnectReason, $"GG2 connection closed: {ex.Message}", null);
            Interlocked.Exchange(ref _disposed, 1);
            _passwordSubmission.TrySetCanceled();
            _client.Dispose();
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _passwordSubmission.TrySetCanceled();
        if (Volatile.Read(ref _joined) != 0)
        {
            try { WriteRaw([LegacyGg2Wire.PlayerLeave]); }
            catch (IOException) { }
            catch (SocketException) { }
            catch (ObjectDisposedException) { }
        }

        _client.Dispose();
    }

    private void ReadServerLoop(string playerName)
    {
        try
        {
            using var reader = new BinaryReader(_stream, Encoding.Latin1, leaveOpen: true);
            WriteRaw(LegacyGg2Wire.CreateHello());
            var firstByte = ReadHelloReply(reader);
            var hello = LegacyGg2Wire.ReadServerHello(reader, firstByte);
            Volatile.Write(ref _currentMapName, hello.MapName);
            Volatile.Write(ref _currentMapHash, hello.MapMd5);
            var listedPlugins = hello.PluginList.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (hello.PluginsRequired && listedPlugins.Any(plugin => plugin != LegacyGg2ChatWire.SupportedPlugin))
            {
                throw new NotSupportedException($"GG2 server requires an unsupported plugin: {hello.PluginList}");
            }

            var externalLevelName = string.IsNullOrEmpty(hello.MapMd5)
                ? null
                : LegacyGg2MapCache.EnsureMapAvailable(
                    reader, WriteRaw, hello.MapName, hello.MapMd5, _mapCacheDirectory);

            var session = ReDsmReplayTransport.ReDsmReplayTranslator.LegacyReplaySession.CreateLive(
                hello, externalLevelName);
            if (LegacyGg2ChatWire.TryGetPluginId(hello.PluginList, out var chatPluginId))
            {
                Volatile.Write(ref _chatPluginId, chatPluginId);
            }

            WriteRaw(LegacyGg2Wire.CreateReserveSlot(playerName));
            var reservationReply = reader.ReadByte();
            if (reservationReply == LegacyGg2Wire.ServerFull)
            {
                throw new InvalidDataException("GG2 server is full.");
            }

            if (reservationReply != LegacyGg2Wire.ReserveSlot)
            {
                throw new InvalidDataException($"GG2 server rejected slot reservation (0x{reservationReply:X2}).");
            }

            WriteRaw([LegacyGg2Wire.PlayerJoin]);
            ReceiveFramedMessages(session);
        }
        catch (Exception ex) when (ex is IOException or SocketException or EndOfStreamException or InvalidDataException or NotSupportedException or ObjectDisposedException or UnauthorizedAccessException or OperationCanceledException or InvalidOperationException)
        {
            if (Volatile.Read(ref _disposed) == 0)
            {
                Interlocked.CompareExchange(ref _disconnectReason, ex.Message, null);
            }
        }
        finally
        {
            lock (_sendLock)
            {
                Interlocked.Exchange(ref _disposed, 1);
                _passwordSubmission.TrySetCanceled();
                _client.Dispose();
            }
        }
    }

    /// <summary>
    /// The first byte of the server's reply to HELLO. A password-protected server asks
    /// first: the client's password prompt answers through <see cref="PasswordSubmitMessage"/>,
    /// and the server then either says hello or PASSWORD_WRONG.
    /// </summary>
    private byte ReadHelloReply(BinaryReader reader)
    {
        var firstByte = reader.ReadByte();
        if (firstByte == LegacyGg2Wire.IncompatibleProtocol)
        {
            throw new InvalidDataException("The server uses a different GG2 protocol UUID.");
        }

        if (firstByte != LegacyGg2Wire.PasswordRequest)
        {
            return firstByte;
        }

        Enqueue(new PasswordRequestMessage());
        string password;
        try
        {
            // The prompt waits on the player; the socket's own timeout doesn't apply here.
            password = _passwordSubmission.Task.WaitAsync(TimeSpan.FromMinutes(5)).GetAwaiter().GetResult();
        }
        catch (TimeoutException)
        {
            throw new InvalidDataException("No GG2 server password was entered.");
        }
        catch (TaskCanceledException)
        {
            throw new OperationCanceledException("GG2 connection closed while waiting for a password.");
        }

        WriteRaw(Gg2ClientMessages.Password(password));
        var reply = reader.ReadByte();
        if (reply == LegacyGg2Wire.PasswordWrong)
        {
            Enqueue(new PasswordResultMessage(false, "Incorrect GG2 server password."));
            throw new InvalidDataException("Incorrect GG2 server password.");
        }

        Enqueue(new PasswordResultMessage(true, string.Empty));
        return reply;
    }

    private void ReceiveFramedMessages(ReDsmReplayTransport.ReDsmReplayTranslator.LegacyReplaySession session)
    {
        var framer = new Gg2ServerMessageReader();
        var buffer = new byte[ReceiveChunkBytes * 4];
        var length = 0;
        while (Volatile.Read(ref _disposed) == 0)
        {
            if (buffer.Length - length < ReceiveChunkBytes)
            {
                Array.Resize(ref buffer, Math.Max(buffer.Length * 2, length + ReceiveChunkBytes));
            }

            var read = _stream.Read(buffer, length, ReceiveChunkBytes);
            if (read <= 0)
            {
                throw new EndOfStreamException("The GG2 server closed the connection.");
            }

            length += read;
            var offset = 0;
            while (offset < length && Volatile.Read(ref _disposed) == 0)
            {
                if (framer.TryRead(buffer.AsSpan(offset, length - offset), out var message, out var consumed) == Gg2ReadStatus.NeedMoreData)
                {
                    break;
                }

                HandleServerMessage(session, framer, message!, buffer, offset, consumed);
                offset += consumed;
            }

            if (offset > 0)
            {
                Buffer.BlockCopy(buffer, offset, buffer, 0, length - offset);
                length -= offset;
            }
        }
    }

    private void HandleServerMessage(
        ReDsmReplayTransport.ReDsmReplayTranslator.LegacyReplaySession session,
        Gg2ServerMessageReader framer,
        Gg2ServerMessage message,
        byte[] buffer,
        int offset,
        int length)
    {
        switch (message)
        {
            case Gg2PluginPacketMessage plugin:
                HandlePluginPacket(plugin, session);
                return;
            case Gg2KickMessage kick:
                throw new InvalidDataException(DescribeKick(kick.Reason));
            case Gg2SignalMessage { SignalType: Gg2Protocol.ServerFull }:
                throw new InvalidDataException("GG2 server is full.");
            case Gg2SignalMessage { SignalType: Gg2Protocol.Ping or Gg2Protocol.ReserveSlot }:
            case Gg2RewardChallengeMessage:
                // GG2 clients without a reward key never answer challenges.
                return;
        }

        ApplyToSession(session, message.Type, buffer, offset, length);
        if (message is Gg2ChangeMapMessage)
        {
            if (!_joinMapApplied)
            {
                // The join state repeats the map the hello named; it is already loaded.
                _joinMapApplied = true;
            }
            else
            {
                var changedLevelName = string.IsNullOrEmpty(session.LegacyMapHash)
                    ? null
                    : DownloadChangedMap(session.LegacyMapName, session.LegacyMapHash);
                session.ActivateLiveMapChange(changedLevelName);
                Volatile.Write(ref _currentMapName, session.LegacyMapName);
                Volatile.Write(ref _currentMapHash, session.LegacyMapHash);
            }

            framer.SetMapLayout(Gg2MapLayoutResolver.Resolve(session.GameMode, session.CurrentLevel));
        }

        AfterSessionMessage(session, message.Type);
    }

    /// <summary>
    /// Hands one framed message to the session. Roster and movement messages must apply or
    /// the session no longer matches the server, so their failures end the connection; a
    /// failure anywhere else (an event or a HUD block the session reads differently) skips
    /// just that message.
    /// </summary>
    private void ApplyToSession(
        ReDsmReplayTransport.ReDsmReplayTranslator.LegacyReplaySession session,
        byte type,
        byte[] buffer,
        int offset,
        int length)
    {
        try
        {
            using var payload = new MemoryStream(buffer, offset + 1, length - 1, writable: false);
            using var payloadReader = new BinaryReader(payload, Encoding.Latin1, leaveOpen: false);
            session.ApplyLiveMessage(payloadReader, type);
        }
        catch (InvalidDataException) when (!MustApply(type))
        {
            SkippedMessageCount += 1;
        }
    }

    private static bool MustApply(byte type)
        => type is Gg2Protocol.JoinUpdate
            or Gg2Protocol.ChangeMap
            or Gg2Protocol.PlayerJoin
            or Gg2Protocol.PlayerLeave
            or Gg2Protocol.PlayerChangeTeam
            or Gg2Protocol.PlayerChangeClass
            or Gg2Protocol.Balance
            or Gg2Protocol.InputState
            or Gg2Protocol.QuickUpdate;

    private void AfterSessionMessage(ReDsmReplayTransport.ReDsmReplayTranslator.LegacyReplaySession session, byte type)
    {
        if (!_welcomed)
        {
            if (session.LocalPlayerIndex < 0 || session.PlayerCount <= session.LocalPlayerIndex)
            {
                return;
            }

            _localSlot = session.GetLocalPlayerSlot();
            UpdateLocalStockState(session);
            if (Volatile.Read(ref _chatPluginId) is var pluginId and >= 0)
            {
                WriteRaw(LegacyGg2ChatWire.CreateHello(checked((byte)pluginId)));
            }

            Volatile.Write(ref _joined, 1);
            _welcomed = true;
            Enqueue(session.CreateWelcomeMessage(_localSlot, localPredictionEnabled: true));
            Enqueue(session.CreateSnapshotMessage());
            return;
        }

        if (session.LocalPlayerIndex < 0)
        {
            throw new InvalidDataException("GG2 server removed the local player.");
        }

        var currentSlot = session.GetLocalPlayerSlot();
        UpdateLocalStockState(session);
        if (currentSlot != _localSlot)
        {
            _localSlot = currentSlot;
            Enqueue(new SessionSlotChangedMessage(_localSlot));
        }

        if (type == Gg2Protocol.InputState && session.LocalPlayerHasCharacter)
        {
            _inputAcks.ObserveEcho(session.LocalPlayerKeyState);
        }

        if (ReDsmReplayTransport.ReDsmReplayTranslator.LegacyReplaySession.IsLiveSnapshotBoundary(type))
        {
            Enqueue(session.CreateSnapshotMessage(_inputAcks.GetAcknowledgedSequence()));
        }
    }

    private static string DescribeKick(byte reason) => reason switch
    {
        Gg2Protocol.KickName => "Kicked by the GG2 server: name exploit.",
        Gg2Protocol.KickBadPluginPacket => "Kicked by the GG2 server: invalid plugin packet.",
        Gg2Protocol.KickMultiClient => "Kicked by the GG2 server: too many connections from your IP.",
        _ => "Kicked by the GG2 server.",
    };

    private string DownloadChangedMap(string mapName, string mapHash)
    {
        // GG2's own client reconnects to request a new external map. Use a
        // short auxiliary connection so the gameplay slot and chat stay live.
        using var mapClient = new TcpClient();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(6));
        mapClient.ConnectAsync(_host, _port, timeout.Token).GetAwaiter().GetResult();
        mapClient.ReceiveTimeout = 12000;
        mapClient.SendTimeout = 5000;
        using var mapStream = mapClient.GetStream();
        using var mapReader = new BinaryReader(mapStream, Encoding.Latin1, leaveOpen: true);
        mapStream.Write(LegacyGg2Wire.CreateHello());
        var firstByte = mapReader.ReadByte();
        if (firstByte == LegacyGg2Wire.PasswordRequest && _passwordSubmission.Task.IsCompletedSuccessfully)
        {
            mapStream.Write(Gg2ClientMessages.Password(_passwordSubmission.Task.Result));
            firstByte = mapReader.ReadByte();
        }

        if (firstByte != LegacyGg2Wire.Hello)
        {
            throw new InvalidDataException($"GG2 rejected the map download connection (0x{firstByte:X2}).");
        }

        var hello = LegacyGg2Wire.ReadServerHello(mapReader, firstByte);
        if (!string.Equals(hello.MapName, mapName, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(hello.MapMd5, mapHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("GG2 rotated maps again before the new map could be downloaded.");
        }

        return LegacyGg2MapCache.EnsureMapAvailable(
            mapReader, payload => mapStream.Write(payload), mapName, mapHash, _mapCacheDirectory);
    }

    private void HandlePluginPacket(Gg2PluginPacketMessage packet, ReDsmReplayTransport.ReDsmReplayTranslator.LegacyReplaySession session)
    {
        if (packet.PluginId != Volatile.Read(ref _chatPluginId) || packet.Data.Length == 0)
        {
            return;
        }

        // Vote packets are part of the chat plugin but do not carry chat text.
        if (packet.Data[0] is >= 200 and <= 204)
        {
            return;
        }

        LegacyGg2ChatMessage chat;
        try
        {
            chat = LegacyGg2ChatWire.ReadChatMessage(packet.Data);
        }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException)
        {
            SkippedMessageCount += 1;
            return;
        }

        var senderName = chat.Sender < 200 ? session.GetPlayerName(chat.Sender) : "Server";
        var senderSlot = chat.Sender < 127 ? checked((byte)(chat.Sender + 1)) : (byte)0;
        Enqueue(new ChatRelayMessage(chat.Team, senderName, chat.Text, chat.Team != 0, senderSlot));
    }

    private void SendControl(ControlCommandMessage command)
    {
        lock (_sendLock)
        {
            if (_lastControlCommandSequences.TryGetValue(command.Kind, out var lastSequence))
            {
                if (command.Sequence == lastSequence)
                {
                    Enqueue(new ControlAckMessage(command.Sequence, command.Kind, Accepted: true));
                    return;
                }

                if (!IsControlCommandSequenceNewer(command.Sequence, lastSequence))
                {
                    Enqueue(new ControlAckMessage(command.Sequence, command.Kind, Accepted: false));
                    return;
                }
            }

            byte[]? wire = command.Kind switch
            {
                ControlCommandKind.SelectTeam => LegacyGg2Wire.CreateTeamSelection((PlayerTeam)command.Value),
                ControlCommandKind.Spectate => LegacyGg2Wire.CreateTeamSelection(PlayerTeam.Neutral),
                ControlCommandKind.SelectClass when TryResolveStockClass(command, out var playerClass)
                    => LegacyGg2Wire.CreateClassSelection(playerClass),
                _ => null,
            };
            if (wire is not null)
            {
                _stream.Write(wire);
                _lastControlCommandSequences[command.Kind] = command.Sequence;
            }

            Enqueue(new ControlAckMessage(command.Sequence, command.Kind, wire is not null));
        }
    }

    private static bool IsControlCommandSequenceNewer(uint candidate, uint previous)
        => unchecked((int)(candidate - previous)) > 0;

    private void SendStockInputEdges(InputStateMessage input)
    {
        var secondaryHeld = (input.Buttons & InputButtons.FireSecondary) != 0;
        var dropIntelHeld = (input.Buttons & InputButtons.DropIntel) != 0;
        if (Volatile.Read(ref _localHasCharacter) != 0)
        {
            if (secondaryHeld && !_wasSecondaryHeld
                && LegacyGg2Wire.GetStockSpecialCommand(
                    (PlayerClass)Volatile.Read(ref _localClassId),
                    Volatile.Read(ref _localHasSentry) != 0) is { } specialCommand)
            {
                WriteRaw([specialCommand]);
            }

            if (dropIntelHeld && !_wasDropIntelHeld)
            {
                WriteRaw([LegacyGg2Wire.DropIntel]);
            }
        }

        _wasSecondaryHeld = secondaryHeld;
        _wasDropIntelHeld = dropIntelHeld;
    }

    /// <summary>GG2's CHAT_BUBBLE command for stock bubbles; SGG's custom bubbles have no GG2 form.</summary>
    private void SendChatBubble(int frameIndex)
    {
        if (frameIndex is < 0 or > byte.MaxValue
            || ChatBubbleFrameCatalog.TryGetCustomBubbleSlot(frameIndex, out _))
        {
            return;
        }

        WriteRaw(Gg2ClientMessages.ChatBubble((byte)frameIndex));
    }

    private void UpdateLocalStockState(ReDsmReplayTransport.ReDsmReplayTranslator.LegacyReplaySession session)
    {
        Volatile.Write(ref _localClassId, (int)session.GetLocalPlayerClass());
        Volatile.Write(ref _localHasCharacter, session.LocalPlayerHasCharacter ? 1 : 0);
        Volatile.Write(ref _localHasSentry, session.LocalPlayerHasSentry ? 1 : 0);
    }

    private static bool TryResolveStockClass(ControlCommandMessage command, out PlayerClass playerClass)
    {
        if (!string.IsNullOrWhiteSpace(command.TextValue))
        {
            if (CharacterClassCatalog.RuntimeRegistry.TryGetClassBinding(command.TextValue, out var binding)
                && binding.BindsLegacyPlayerClass)
            {
                playerClass = binding.PlayerClass;
                return true;
            }

            playerClass = default;
            return false;
        }

        playerClass = (PlayerClass)command.Value;
        return Enum.IsDefined(playerClass);
    }

    private void SendChat(ChatSubmitMessage chat)
    {
        var pluginId = Volatile.Read(ref _chatPluginId);
        if (pluginId < 0)
        {
            return;
        }

        var text = chat.Text ?? string.Empty;
        while (text.Length > 0)
        {
            var chunkLength = Math.Min(text.Length, 255);
            WriteRaw(LegacyGg2ChatWire.CreateChat(checked((byte)pluginId), text[..chunkLength], chat.TeamOnly));
            text = text[chunkLength..];
        }
    }

    private void Enqueue(IProtocolMessage message) => _inbound.Enqueue(ProtocolCodec.Serialize(message));

    private void WriteRaw(byte[] payload)
    {
        lock (_sendLock)
        {
            _stream.Write(payload);
        }
    }
}
