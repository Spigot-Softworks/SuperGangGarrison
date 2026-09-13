using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using OpenGarrison.Client;
using OpenGarrison.Protocol;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class ClientTransportFailureTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PermissionFailureDuringInitialHelloReturnsAnErrorInsteadOfClosingTheGame(bool protocol64)
    {
        using var client = new NetworkGameClient();
        var transport = new FaultingTransport(protocol64) { SendError = SocketError.AccessDenied };
        Assert.False(client.Connect(transport, "Tester", 0, out var error));
        Assert.Contains("AccessDenied", error);
        Assert.False(client.IsConnected);
        Assert.True(transport.Disposed);
    }

    [Theory]
    [InlineData("send")]
    [InlineData("available")]
    [InlineData("receive")]
    public void SocketFailureWhileWaitingForLocalServerBecomesADisconnectReason(string operation)
    {
        using var client = new NetworkGameClient();
        var transport = new FaultingTransport(false);
        Assert.True(client.Connect(transport, "Tester", 0, out var error), error);
        if (operation == "send")
        {
            transport.SendError = SocketError.AccessDenied;
            typeof(NetworkGameClient).GetField("_lastHelloSentAtMilliseconds", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(client, -1L);
        }
        else if (operation == "available") transport.AvailableError = SocketError.NetworkDown;
        else transport.ReceiveError = SocketError.AccessDenied;

        Assert.Empty(client.ReceiveMessages());
        Assert.False(client.IsConnected);
        Assert.True(client.TryConsumeDisconnectReason(out var reason));
        Assert.Contains(operation == "available" ? "NetworkDown" : "AccessDenied", reason);
        Assert.True(transport.Disposed);
    }

    [Theory]
    [InlineData(false, "voice")]
    [InlineData(true, "voice")]
    [InlineData(false, "command")]
    [InlineData(true, "command")]
    [InlineData(false, "delayed")]
    public void FailureAfterWelcomeStopsSafelyAndCanReconnect(bool protocol64, string operation)
    {
        using var client = new NetworkGameClient();
        var transport = new FaultingTransport(protocol64);
        Assert.True(client.Connect(transport, "Tester", 0, out var error), error);
        client.SetLocalPlayerSlot(1);
        transport.SendError = SocketError.ConnectionReset;
        transport.ThrowOnDispose = true;
        if (operation == "voice") client.SendVoice(false, new(1, [new byte[] { 0x78 }]));
        else if (operation == "command") client.SendVoiceChannelMembership(new(1, true));
        else
        {
            client.SetSimulatedLatency(10000);
            client.SendVoiceChannelMembership(new(1, true));
            client.SetSimulatedLatency(0);
        }
        Assert.Empty(client.ReceiveMessages());
        Assert.False(client.IsConnected);
        Assert.True(client.TryConsumeDisconnectReason(out var reason));
        Assert.Contains("ConnectionReset", reason);
        var replacement = new FaultingTransport(protocol64);
        Assert.True(client.Connect(replacement, "Tester", 0, out error), error);
        Assert.Empty(client.ReceiveMessages());
        Assert.True(client.IsConnected);
        Assert.Equal(1, replacement.SentCount);
    }

    [Theory]
    [InlineData("send")]
    [InlineData("available")]
    [InlineData("receive")]
    public void NonblockingSocketRacesAllowRetryInsteadOfDisconnecting(string operation)
    {
        using var client = new NetworkGameClient();
        var transport = new FaultingTransport(false);
        Assert.True(client.Connect(transport, "Tester", 0, out var error), error);
        if (operation == "send")
        {
            transport.SendError = SocketError.WouldBlock;
            typeof(NetworkGameClient).GetField("_lastHelloSentAtMilliseconds", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(client, -1L);
        }
        else if (operation == "available") transport.AvailableError = SocketError.WouldBlock;
        else transport.ReceiveError = SocketError.WouldBlock;
        Assert.Empty(client.ReceiveMessages());
        Assert.True(client.IsConnected);
        Assert.False(client.TryConsumeDisconnectReason(out _));
        transport.SendError = transport.AvailableError = transport.ReceiveError = null;
        typeof(NetworkGameClient).GetField("_lastHelloSentAtMilliseconds", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(client, -1L);
        Assert.Empty(client.ReceiveMessages());
        Assert.Equal(2, transport.SentCount);
        Assert.True(client.IsConnected);
    }

    [Fact]
    public void StartupSocketCleanupFailureDoesNotHideOriginalPermissionError()
    {
        using var client = new NetworkGameClient();
        var transport = new FaultingTransport(false) { SendError = SocketError.AccessDenied, ThrowOnDispose = true };
        Assert.False(client.Connect(transport, "Tester", 0, out var error));
        Assert.Contains("AccessDenied", error);
        Assert.True(transport.Disposed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WelcomeReceiptKeepsConnectionAliveWhileMapLoadingDefersTheLocalSlot(bool protocol64)
    {
        using var client = new NetworkGameClient();
        var transport = new FaultingTransport(protocol64);
        Assert.True(client.Connect(transport, "Tester", 0, out var error), error);
        client.AcknowledgeWelcomeReceipt();
        Assert.Equal(0, client.LocalPlayerSlot);
        Assert.False(client.IsAwaitingWelcome);
        Assert.Empty(client.ReceiveMessages());
        Assert.True(client.IsConnected);
        Assert.Equal(2, transport.SentCount); // Initial hello, then a keepalive ping.
        client.SetLocalPlayerSlot(2);
        Assert.Equal(2, client.LocalPlayerSlot);
        client.Disconnect();
        Assert.True(client.Connect(new FaultingTransport(protocol64), "Tester", 0, out error), error);
        Assert.True(client.IsAwaitingWelcome);
    }

    [Fact]
    public void LastToDieRetryFailureDoesNotClearThePendingCollectionDuringEnumeration()
    {
        using var client = new NetworkGameClient();
        var transport = new FaultingTransport(false);
        Assert.True(client.Connect(transport, "Tester", 0, out var error), error);
        client.SetLocalPlayerSlot(1);
        Assert.True(client.LastToDieState.ApplySnapshot(new(Guid.NewGuid(), 1, 42, 1,
            LastToDieWireDifficulty.Standard, LastToDieWirePhase.Lobby, 0, 0, 0, "", 0, 0, 0, [])).Applied);
        Assert.NotEqual(0UL, client.SendLastToDieCommand(LastToDieCommandKind.Ready));
        Assert.NotEqual(0UL, client.SendLastToDieCommand(LastToDieCommandKind.RequestStart));
        var pending = (IDictionary)typeof(NetworkGameClient).GetField("_pendingLastToDieCommands", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(client)!;
        foreach (var command in pending.Values) command!.GetType().GetProperty("LastSentAtMilliseconds")!.SetValue(command, -1000L);
        transport.SendError = SocketError.AccessDenied;
        Assert.Empty(client.ReceiveMessages());
        Assert.False(client.IsConnected);
        Assert.True(client.TryConsumeDisconnectReason(out var reason));
        Assert.Contains("AccessDenied", reason);
        Assert.Empty(pending);
    }

    [Fact]
    public async Task LocalUdpHandshakeCanRetryAfterTheServerBindsItsPortLater()
    {
        int port;
        using (var probe = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0))) port = ((IPEndPoint)probe.Client.LocalEndPoint!).Port;
        using var client = new NetworkGameClient();
        Assert.True(client.Connect("127.0.0.1", port, "Tester", 0, out var error), error);
        Assert.Empty(client.ReceiveMessages());
        Assert.True(client.IsConnected);
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
        typeof(NetworkGameClient).GetField("_lastHelloSentAtMilliseconds", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(client, -1L);
        Assert.Empty(client.ReceiveMessages());
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var packet = await server.ReceiveAsync(deadline.Token);
        Assert.True(ProtocolCodec.TryDeserialize(packet.Buffer, out var message));
        Assert.IsType<HelloMessage>(message);
        Assert.True(client.IsConnected);
        Assert.False(client.TryConsumeDisconnectReason(out _));
    }

    private sealed class FaultingTransport(bool protocol64) : INetworkClientMessageTransport, INetworkClientAudioMessageTransport
    {
        public SocketError? SendError { get; set; }
        public SocketError? AvailableError { get; set; }
        public SocketError? ReceiveError { get; set; }
        public bool Disposed { get; private set; }
        public bool ThrowOnDispose { get; set; }
        public int SentCount { get; private set; }
        public bool IsLoopbackConnection => true;
        public string RemoteDescription => protocol64 ? "ws64://127.0.0.1:8190" : "127.0.0.1:8190";
        public bool HasPendingMessages
        {
            get
            {
                if (AvailableError is { } error) throw new SocketException((int)error);
                return ReceiveError.HasValue;
            }
        }
        public bool TryReceive(out byte[] payload)
        {
            payload = [];
            if (ReceiveError is { } error) throw new SocketException((int)error);
            return false;
        }
        public void Send(byte[] payload)
        {
            if (SendError is { } error) throw new SocketException((int)error);
            SentCount++;
        }
        public void SendAudio(byte[] payload) => Send(payload);
        public bool TryConsumeDisconnectReason(out string reason) { reason = ""; return false; }
        public void Dispose()
        {
            Disposed = true;
            if (ThrowOnDispose) throw new SocketException((int)SocketError.NotSocket);
        }
    }
}
