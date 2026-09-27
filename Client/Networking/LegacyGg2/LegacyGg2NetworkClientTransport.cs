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

namespace OpenGarrison.Client;

/// <summary>Translates a stock GG2 TCP session into SGG's existing client messages.</summary>
internal sealed class LegacyGg2NetworkClientTransport : INetworkClientMessageTransport
{
    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly object _sendLock = new();
    private readonly ConcurrentQueue<byte[]> _inbound = new();
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
                case InputStateMessage input when Volatile.Read(ref _joined) != 0:
                    SendStockInputEdges(input);
                    WriteRaw(LegacyGg2Wire.CreateInputState(input));
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
            _client.Dispose();
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

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
            var firstByte = reader.ReadByte();
            if (firstByte == LegacyGg2Wire.PasswordRequest)
            {
                throw new NotSupportedException("Password-protected GG2 servers are not supported yet.");
            }

            if (firstByte == LegacyGg2Wire.IncompatibleProtocol)
            {
                throw new InvalidDataException("The server uses a different GG2 protocol UUID.");
            }

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
            session.ReadLiveJoinState(reader);

            var welcomed = false;
            byte localSlot = 0;
            while (Volatile.Read(ref _disposed) == 0)
            {
                var messageType = reader.ReadByte();
                if (messageType == LegacyGg2ChatWire.PluginPacket)
                {
                    ReadPluginPacket(reader, session);
                    continue;
                }

                session.ApplyLiveMessage(reader, messageType);
                if (messageType == LegacyGg2Wire.ChangeMap)
                {
                    var changedLevelName = string.IsNullOrEmpty(session.LegacyMapHash)
                        ? null
                        : DownloadChangedMap(session.LegacyMapName, session.LegacyMapHash);
                    session.ActivateLiveMapChange(changedLevelName);
                    Volatile.Write(ref _currentMapName, session.LegacyMapName);
                    Volatile.Write(ref _currentMapHash, session.LegacyMapHash);
                }
                if (!welcomed && session.LocalPlayerIndex >= 0 && session.PlayerCount > session.LocalPlayerIndex)
                {
                    localSlot = session.GetLocalPlayerSlot();
                    UpdateLocalStockState(session);
                    if (Volatile.Read(ref _chatPluginId) is var pluginId and >= 0)
                    {
                        WriteRaw(LegacyGg2ChatWire.CreateHello(checked((byte)pluginId)));
                    }

                    Volatile.Write(ref _joined, 1);
                    welcomed = true;
                    Enqueue(session.CreateWelcomeMessage(localSlot));
                    Enqueue(session.CreateSnapshotMessage());

                    continue;
                }

                if (!welcomed)
                {
                    continue;
                }

                if (session.LocalPlayerIndex < 0)
                {
                    throw new InvalidDataException("GG2 server removed the local player.");
                }

                var currentSlot = session.GetLocalPlayerSlot();
                UpdateLocalStockState(session);
                if (currentSlot != localSlot)
                {
                    localSlot = currentSlot;
                    Enqueue(new SessionSlotChangedMessage(localSlot));
                }

                if (ReDsmReplayTransport.ReDsmReplayTranslator.LegacyReplaySession.IsLiveSnapshotBoundary(messageType))
                {
                    Enqueue(session.CreateSnapshotMessage());
                }
            }
        }
        catch (Exception ex) when (ex is IOException or SocketException or EndOfStreamException or InvalidDataException or NotSupportedException or ObjectDisposedException or UnauthorizedAccessException or OperationCanceledException)
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
                _client.Dispose();
            }
        }
    }

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

    private void ReadPluginPacket(BinaryReader reader, ReDsmReplayTransport.ReDsmReplayTranslator.LegacyReplaySession session)
    {
        var packetLength = reader.ReadUInt16();
        if (packetLength == 0)
        {
            throw new InvalidDataException("GG2 plugin packet is empty.");
        }

        var packet = reader.ReadBytes(packetLength);
        if (packet.Length != packetLength)
        {
            throw new EndOfStreamException("GG2 plugin packet was truncated.");
        }

        if (packet[0] != Volatile.Read(ref _chatPluginId) || packetLength <= 1)
        {
            return;
        }

        // Vote packets are part of the chat plugin but do not carry chat text.
        if (packet[1] is >= 200 and <= 204)
        {
            return;
        }

        var chat = LegacyGg2ChatWire.ReadChatMessage(packet.AsSpan(1));
        var senderName = chat.Sender < 200 ? session.GetPlayerName(chat.Sender) : "Server";
        var senderSlot = chat.Sender < 127 ? checked((byte)(chat.Sender + 1)) : (byte)0;
        Enqueue(new ChatRelayMessage(chat.Team, senderName, chat.Text, chat.Team != 0, senderSlot));
    }

    private void SendControl(ControlCommandMessage command)
    {
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
            WriteRaw(wire);
        }

        Enqueue(new ControlAckMessage(command.Sequence, command.Kind, wire is not null));
    }

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
