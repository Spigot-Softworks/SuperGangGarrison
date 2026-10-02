using System.Net;
using System.Net.Sockets;
using OpenGarrison.Client;
using OpenGarrison.Protocol;
using OpenGarrison.Server;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class UdpFragmentationTests
{
    [Fact]
    public void ReassemblesOutOfOrderAndDuplicateChunksExactly()
    {
        var payload = Enumerable.Range(0, 9000).Select(index => (byte)(index * 31)).ToArray();
        var frames = UdpFragmentation.Fragment(payload, 41);
        Assert.All(frames, frame => Assert.True(frame.Length <= UdpFragmentation.RecommendedDatagramBytes));
        var receiver = new UdpFragmentReassembler();
        byte[]? completed = null;

        foreach (var index in Enumerable.Range(0, frames.Count).Reverse())
        {
            Assert.True(receiver.TryAccept("peer-a", frames[index], index, out var result));
            if (result is not null) completed = result;
            if (index == 2) Assert.True(receiver.TryAccept("peer-a", frames[index], index, out _));
        }

        Assert.Equal(payload, completed);
        Assert.Equal(0, receiver.BufferedBytes);
    }

    [Fact]
    public void NackRequestsOnlyMissingChunksAndCacheRepairsOnlyForOwningPeer()
    {
        var payload = Enumerable.Range(0, 14000).Select(index => (byte)index).ToArray();
        var sender = new UdpFragmentSendCache();
        var frames = sender.CacheAndFragment("peer-a", payload, 100).ToArray();
        var receiver = new UdpFragmentReassembler();
        Assert.True(receiver.TryAccept("peer-a", frames[0], 100, out _));
        Assert.Empty(receiver.GetDueNacks(100 + UdpFragmentReassembler.NackIntervalMilliseconds - 1));
        var nack = Assert.Single(receiver.GetDueNacks(100 + UdpFragmentReassembler.NackIntervalMilliseconds));

        Assert.Empty(sender.TryRepair("peer-b", nack.Packet, 220));
        var repair = sender.TryRepair("peer-a", nack.Packet, 220);
        Assert.Equal(UdpFragmentation.MaxRepairChunksPerNack, repair.Count);
        Assert.All(repair, frame => Assert.True(UdpFragmentation.TryDecode(frame, out var decoded) && !decoded.IsNack));
        Assert.Empty(sender.TryRepair("peer-a", nack.Packet, 221));
    }

    [Fact]
    public void RejectsMalformedHeadersAndOversizedDeclarationsWithoutAllocating()
    {
        var valid = UdpFragmentation.Fragment(new byte[3000], 9)[0];
        var receiver = new UdpFragmentReassembler();
        var oversized = (byte[])valid.Clone();
        BitConverter.GetBytes(UdpFragmentation.MaxMessageBytes + 1).CopyTo(oversized, 16);
        Assert.False(receiver.TryAccept("peer-a", oversized, 0, out _));
        Assert.Equal(0, receiver.BufferedBytes);

        var invalidVersion = (byte[])valid.Clone();
        invalidVersion[4]++;
        Assert.False(receiver.TryAccept("peer-a", invalidVersion, 0, out _));
        Assert.False(UdpFragmentation.TryDecode(new byte[UdpFragmentation.HeaderBytes - 1], out _));
    }

    [Fact]
    public void ReassemblerRejectsControlFramesWithoutRetainingAssemblies()
    {
        var receiver = new UdpFragmentReassembler();
        Assert.False(receiver.TryAccept("peer", UdpFragmentation.CreateMtuProbe(10, 1200), 0, out _));
        Assert.False(receiver.TryAccept("peer", UdpFragmentation.CreateMtuAck(10, 1200), 0, out _));
        Assert.Equal(0, receiver.BufferedBytes);
    }

    [Fact]
    public void BoundsIncompleteAssembliesAndExpiresThem()
    {
        var payload = new byte[5000];
        var frames = UdpFragmentation.Fragment(payload, 17);
        var receiver = new UdpFragmentReassembler();
        for (var index = 0; index < UdpFragmentReassembler.MaxAssembliesPerPeer; index++)
            Assert.True(receiver.TryAccept("peer-a", UdpFragmentation.Fragment(payload, (ulong)(index + 17))[0], 0, out _));
        Assert.False(receiver.TryAccept("peer-a", UdpFragmentation.Fragment(payload, 99)[0], 0, out _));
        Assert.True(receiver.TryAccept("peer-b", frames[0], 0, out _));
        Assert.True(receiver.TryAccept("peer-a", UdpFragmentation.Fragment(payload, 99)[0], UdpFragmentReassembler.AssemblyLifetimeMilliseconds, out _));
        Assert.InRange(receiver.BufferedBytes, 1, UdpFragmentReassembler.MaxBufferedBytes);
    }

    [Fact]
    public void ExpirationIsAbsoluteAndCompletedMessagesAreDeduplicated()
    {
        var frames = UdpFragmentation.Fragment(new byte[4000], 61);
        var receiver = new UdpFragmentReassembler();
        Assert.True(receiver.TryAccept("peer", frames[0], 1, out _));
        Assert.True(receiver.TryAccept("peer", frames[0], 4000, out _));
        Assert.Equal(0, receiver.BufferedBytes);

        var completedReceiver = new UdpFragmentReassembler();
        byte[]? complete = null;
        foreach (var frame in frames)
            if (completedReceiver.TryAccept("peer", frame, 10, out var result) && result is not null) complete = result;
        Assert.NotNull(complete);
        Assert.True(completedReceiver.TryAccept("peer", frames[0], 11, out var duplicate));
        Assert.Null(duplicate);
        Assert.Equal(0, completedReceiver.BufferedBytes);
    }

    [Fact]
    public void RepairQueueDeduplicatesAndPacesPerPeerWithinBounds()
    {
        var frames = UdpFragmentation.Fragment(new byte[3000], 80);
        var pacer = new UdpRepairPacer();
        Assert.Equal(2, pacer.Enqueue("peer", [frames[0], frames[0], frames[1]], 100));
        Assert.True(pacer.TryDequeueDue(100, out var peer, out var first));
        Assert.Equal("peer", peer);
        Assert.NotNull(first);
        Assert.False(pacer.TryDequeueDue(119, out _, out _));
        Assert.True(pacer.TryDequeueDue(120, out _, out _));

        var perPeer = Enumerable.Range(0, UdpRepairPacer.MaxQueuedPerPeer + 1)
            .Select(index => new byte[] { 1, (byte)index }).ToArray();
        Assert.Equal(UdpRepairPacer.MaxQueuedPerPeer, pacer.Enqueue("bounded", perPeer, 100));
    }

    [Fact]
    public void ExpiredRepairsAndNacksAreDiscardedFromThePacerQueue()
    {
        var frames = UdpFragmentation.Fragment(new byte[3000], 810);
        var pacer = new UdpRepairPacer();
        Assert.Equal(2, pacer.Enqueue("peer", [frames[0], frames[1]], 100));

        Assert.False(pacer.TryDequeueDue(100 + UdpRepairPacer.QueuedPacketLifetimeMilliseconds, out _, out _));
        Assert.Equal(0, pacer.Count);
    }

    [Fact]
    public void SeveralIncompleteSnapshotsDoNotBlockAnIntactSnapshot()
    {
        var receiver = new UdpFragmentReassembler();
        for (ulong id = 1; id <= 8; id++)
        {
            var incomplete = UdpFragmentation.Fragment(new byte[5000], id);
            Assert.True(receiver.TryAccept("peer", incomplete[0], 0, out _));
        }

        var freshPayload = Enumerable.Range(0, 4000).Select(index => (byte)(index * 3)).ToArray();
        byte[]? completed = null;
        foreach (var frame in UdpFragmentation.Fragment(freshPayload, 99))
            if (receiver.TryAccept("peer", frame, 1, out var result) && result is not null) completed = result;

        Assert.Equal(freshPayload, completed);
        Assert.InRange(receiver.BufferedBytes, freshPayload.Length, UdpFragmentReassembler.MaxBufferedBytesPerPeer);
    }

    [Fact]
    public void PathMtuRequiresNonceMatchedConfirmationAndUsesAConfirmedSmallerFallback()
    {
        var upward = new UdpPathMtuDiscovery();
        Assert.True(upward.TryCreateProbe("peer", 0, out var baselineProbe));
        Assert.True(UdpFragmentation.TryDecode(baselineProbe!, out var baseline));
        Assert.True(baseline.IsMtuProbe);
        Assert.Equal(1200, baseline.TotalLength);
        Assert.False(upward.TryAcceptAck("peer", UdpFragmentation.TryDecode(UdpFragmentation.CreateMtuAck(baseline.MessageId + 1, 1200), out var wrongAck) ? wrongAck : default, 1, out _));
        Assert.True(upward.TryAcceptAck("peer", UdpFragmentation.TryDecode(UdpFragmentation.CreateMtuAck(baseline.MessageId, 1200), out var baselineAck) ? baselineAck : default, 1, out _));
        Assert.Equal(1200, upward.GetDatagramLimit("peer", 1));
        Assert.True(upward.TryCreateProbe("peer", 5001, out var largerProbe));
        Assert.True(UdpFragmentation.TryDecode(largerProbe!, out var larger));
        Assert.Equal(1240, larger.TotalLength);
        Assert.False(upward.TryCreateProbe("peer", 6501, out _));
        Assert.Equal(1200, upward.GetDatagramLimit("peer", 6501));

        var downward = new UdpPathMtuDiscovery();
        Assert.True(downward.TryCreateProbe("peer", 0, out _));
        Assert.False(downward.TryCreateProbe("peer", UdpPathMtuDiscovery.ProbeTimeoutMilliseconds, out _));
        Assert.True(downward.TryCreateProbe("peer", UdpPathMtuDiscovery.ProbeTimeoutMilliseconds + UdpPathMtuDiscovery.ProbeIntervalMilliseconds, out var secondBaselineProbe));
        Assert.True(UdpFragmentation.TryDecode(secondBaselineProbe!, out var secondBaseline));
        Assert.Equal(1200, secondBaseline.TotalLength);
        Assert.True(downward.TryCreateProbe("peer", 2 * UdpPathMtuDiscovery.ProbeTimeoutMilliseconds + UdpPathMtuDiscovery.ProbeIntervalMilliseconds, out var smallProbe));
        Assert.True(UdpFragmentation.TryDecode(smallProbe!, out var smaller));
        Assert.Equal(UdpPathMtuDiscovery.SmallerConfirmationDatagramBytes, smaller.TotalLength);
        var smallAckBytes = UdpFragmentation.CreateMtuAck(smaller.MessageId, smaller.TotalLength);
        Assert.True(UdpFragmentation.TryDecode(smallAckBytes, out var smallAck));
        Assert.True(downward.TryAcceptAck("peer", smallAck, 2 * UdpPathMtuDiscovery.ProbeTimeoutMilliseconds + UdpPathMtuDiscovery.ProbeIntervalMilliseconds + 1, out var confirmed));
        Assert.Equal(UdpPathMtuDiscovery.SmallerConfirmationDatagramBytes, confirmed);
        Assert.Equal(confirmed, downward.GetDatagramLimit("peer", 9000));
    }

    [Fact]
    public void SmallerCeilingReframesOnlyRequestedMessageAndStaleNacksDoNotAdvanceGenerationAgain()
    {
        var payload = Enumerable.Range(0, 5000).Select(index => (byte)(index * 11)).ToArray();
        var cache = new UdpFragmentSendCache();
        var original = cache.CacheAndFragment("peer", payload, 0).ToArray();
        var receiver = new UdpFragmentReassembler();
        Assert.True(receiver.TryAccept("peer", original[0], 0, out _));
        var oldNack = Assert.Single(receiver.GetDueNacks(UdpFragmentReassembler.NackIntervalMilliseconds)).Packet;

        var firstGenerationOneBatch = cache.TryRepair("peer", oldNack, 120, UdpPathMtuDiscovery.SmallerConfirmationDatagramBytes);
        Assert.NotEmpty(firstGenerationOneBatch);
        Assert.All(firstGenerationOneBatch, packet =>
        {
            Assert.True(packet.Length <= UdpPathMtuDiscovery.SmallerConfirmationDatagramBytes);
            Assert.True(UdpFragmentation.TryDecode(packet, out var frame));
            Assert.Equal((ushort)1, frame.Generation);
        });

        foreach (var packet in firstGenerationOneBatch) Assert.True(receiver.TryAccept("peer", packet, 121, out _));
        var currentNack = Assert.Single(receiver.GetDueNacks(121 + UdpFragmentReassembler.NackIntervalMilliseconds)).Packet;
        Assert.True(UdpFragmentation.TryDecode(currentNack, out var currentFrame));
        Assert.Equal((ushort)1, currentFrame.Generation);
        var remaining = cache.TryRepair("peer", currentNack, 241, UdpPathMtuDiscovery.SmallerConfirmationDatagramBytes);
        Assert.NotEmpty(remaining);
        Assert.All(remaining, packet =>
        {
            Assert.True(UdpFragmentation.TryDecode(packet, out var frame));
            Assert.Equal((ushort)1, frame.Generation);
            Assert.True(frame.ChunkIndex >= 8);
        });

        var staleNackRepairs = cache.TryRepair("peer", oldNack, 361, UdpPathMtuDiscovery.SmallerConfirmationDatagramBytes);
        Assert.NotEmpty(staleNackRepairs);
        Assert.All(staleNackRepairs, packet =>
        {
            Assert.True(UdpFragmentation.TryDecode(packet, out var frame));
            Assert.Equal((ushort)1, frame.Generation);
        });
    }

    [Fact]
    public void SendCacheRemainsRepairableWhenInitialSocketSendFails()
    {
        var payload = Enumerable.Range(0, 5000).Select(index => (byte)(index * 7)).ToArray();
        var cache = new UdpFragmentSendCache();
        var frames = cache.CacheAndFragment("peer", payload, 10);
        var sent = new List<byte[]>();
        Assert.Throws<InvalidOperationException>(() =>
        {
            foreach (var frame in frames)
            {
                if (sent.Count == 1) throw new InvalidOperationException("simulated UDP send failure");
                sent.Add(frame);
            }
        });
        Assert.Single(sent);

        var receiver = new UdpFragmentReassembler();
        Assert.True(receiver.TryAccept("peer", sent[0], 10, out _));
        var nack = Assert.Single(receiver.GetDueNacks(130));
        Assert.NotEmpty(cache.TryRepair("peer", nack.Packet, 130));
    }

    [Fact]
    public void ClientReceivePumpDrainsAllFragmentsInOneFrame()
    {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var endpoint = (IPEndPoint)server.Client.LocalEndPoint!;
        Assert.True(UdpNetworkClientMessageTransport.TryConnect("127.0.0.1", endpoint.Port, out var transport, out var error), error);
        using var client = new NetworkGameClient();
        Assert.True(client.Connect(transport!, "Tester", 0, out error), error);
        IPEndPoint remote = new(IPAddress.Any, 0);
        _ = server.Receive(ref remote); // consume the client's Hello and learn its local endpoint

        var malformedButLargePayload = Enumerable.Range(0, 9000).Select(index => (byte)(index * 13)).ToArray();
        foreach (var frame in UdpFragmentation.Fragment(malformedButLargePayload, 77))
            server.Send(frame, frame.Length, remote);

        Assert.Empty(client.ReceiveMessages());
        Assert.Equal(UdpFragmentation.Fragment(malformedButLargePayload, 78).Count,
            client.LastReceiveDiagnostics.PacketsRead);
    }

    [Fact]
    public void LoopbackClientAndServerTransportsExchangeLargeLogicalPayloadsExactly()
    {
        using var serverSocket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var serverEndpoint = (IPEndPoint)serverSocket.Client.LocalEndPoint!;
        var server = new UdpServerMessageTransport(serverSocket, new ServerTransportDiagnosticsAccumulator());
        Assert.True(serverSocket.Client.DontFragment);
        Assert.True(UdpNetworkClientMessageTransport.TryConnect("127.0.0.1", serverEndpoint.Port, out var client, out var error), error);
        using (client!)
        {
            var hello = new byte[] { 1, 2, 3, 4 };
            client!.Send(hello);
            Assert.True(server.HasPendingMessages);
            var helloPacket = server.Receive();
            Assert.Equal(hello, helloPacket.Payload);

            var serverPayload = Enumerable.Range(0, 18_000).Select(index => (byte)(index * 17)).ToArray();
            server.Send(helloPacket.RemotePeer, serverPayload, MessageType.Snapshot);
            byte[]? serverReceived = null;
            while (client.HasPendingMessages)
            {
                Assert.True(client.TryReceive(out var payload));
                if (payload.SequenceEqual(serverPayload)) serverReceived = payload;
            }
            Assert.Equal(serverPayload, serverReceived);

            while (server.HasPendingMessages)
                _ = server.Receive(); // consume the client's probe and return its ACK
            while (client.HasPendingMessages)
                _ = client.TryReceive(out _); // consume the server's probe ACK/control

            var clientPayload = Enumerable.Range(0, 15_000).Select(index => (byte)(index * 29)).ToArray();
            client.Send(clientPayload);
            byte[]? clientReceived = null;
            while (server.HasPendingMessages)
            {
                var packet = server.Receive();
                if (packet.Payload.SequenceEqual(clientPayload)) clientReceived = packet.Payload;
            }
            Assert.Equal(clientPayload, clientReceived);
        }
    }

    [Fact]
    public void UnadmittedPeerCanConfirmSmallPathAndSubmitOnlyAReassembledHello()
    {
        using var serverSocket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var serverEndpoint = (IPEndPoint)serverSocket.Client.LocalEndPoint!;
        var server = new UdpServerMessageTransport(serverSocket, new ServerTransportDiagnosticsAccumulator());
        using var clientSocket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        clientSocket.Client.ReceiveTimeout = 1000;
        var clientEndpoint = (IPEndPoint)clientSocket.Client.LocalEndPoint!;

        var probe = UdpFragmentation.CreateMtuProbe(7001, UdpPathMtuDiscovery.SmallerConfirmationDatagramBytes);
        clientSocket.Send(probe, probe.Length, serverEndpoint);
        Assert.True(server.HasPendingMessages);
        Assert.Empty(server.Receive().Payload);
        IPEndPoint responder = new(IPAddress.Any, 0);
        var ackBytes = clientSocket.Receive(ref responder);
        Assert.Equal(serverEndpoint, responder);
        Assert.True(UdpFragmentation.TryDecode(ackBytes, out var ack));
        Assert.True(ack.IsMtuAck);
        Assert.Equal(7001UL, ack.MessageId);
        Assert.Equal(0, server.KnownPeerCount);

        var repeatedProbe = UdpFragmentation.CreateMtuProbe(7002, UdpPathMtuDiscovery.SmallerConfirmationDatagramBytes);
        clientSocket.Send(repeatedProbe, repeatedProbe.Length, serverEndpoint);
        Assert.True(server.HasPendingMessages);
        Assert.Empty(server.Receive().Payload);
        Assert.Equal(0, server.KnownPeerCount);
        Assert.Equal(0, clientSocket.Available); // rate-limited; no second ACK amplification
        var hello = new HelloMessage(
            new string('N', ProtocolCodec.MaxPlayerNameBytes),
            ProtocolVersion.Current,
            ulong.MaxValue,
            new string('F', 40),
            "{\"bio\":\"" + new string('C', ProtocolCodec.MaxPlayerCardBytes - 10) + "\"}",
            ConnectionIntent.Join,
            Guid.NewGuid());
        var helloPayload = ProtocolCodec.Serialize(hello);
        Assert.True(ProtocolCodec.TryDeserialize(helloPayload, out var decodedHello));
        Assert.Equal(hello, decodedHello);
        Assert.True(helloPayload.Length < UdpPathMtuDiscovery.InitialDatagramBytes);
        byte[]? reassembledPayload = null;
        foreach (var datagram in UdpFragmentation.Fragment(helloPayload, 7010, UdpPathMtuDiscovery.SmallerConfirmationDatagramBytes))
        {
            clientSocket.Send(datagram, datagram.Length, serverEndpoint);
            Assert.True(server.HasPendingMessages);
            var received = server.Receive();
            if (received.Payload.Length > 0)
            {
                reassembledPayload = received.Payload;
            }
        }

        Assert.Equal(helloPayload, reassembledPayload);
        Assert.True(ProtocolCodec.TryDeserialize(reassembledPayload!, out var admittedHello));
        Assert.Equal(hello, admittedHello);
        Assert.Equal(0, server.KnownPeerCount); // dispatcher admission happens after transport returns the Hello

        using var detailsSocket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        detailsSocket.Client.ReceiveTimeout = 1000;
        var detailsEndpoint = (IPEndPoint)detailsSocket.Client.LocalEndPoint!;
        server.Send(
            ServerTransportPeer.FromUdpEndPoint(detailsEndpoint),
            new byte[100],
            MessageType.ServerStatusResponse);
        Assert.Equal(0, server.KnownPeerCount); // small one-off responses do not activate probes
        var smallResponse = detailsSocket.Receive(ref responder);
        Assert.Equal(new byte[100], smallResponse);

        server.Send(
            ServerTransportPeer.FromUdpEndPoint(detailsEndpoint),
            new byte[1300],
            MessageType.ServerDetailsResponse);
        Assert.Equal(1, server.KnownPeerCount); // large details responses enable bounded path discovery
        byte[]? firstStatusFrame = null;
        var foundProbe = false;
        while (detailsSocket.Available > 0)
        {
            var statusFrame = detailsSocket.Receive(ref responder);
            if (UdpFragmentation.TryDecode(statusFrame, out var decoded) && decoded.IsMtuProbe)
                foundProbe = true;
            else
                firstStatusFrame ??= statusFrame;
        }
        Assert.True(foundProbe);
        Assert.True(UdpFragmentation.TryDecode(firstStatusFrame!, out var statusHeader));
        var detailsNack = UdpFragmentation.CreateNack(
            statusHeader.MessageId,
            statusHeader.TotalLength,
            statusHeader.ChunkSize,
            statusHeader.ChunkCount,
            statusHeader.Checksum,
            [(ushort)1],
            statusHeader.Generation);
        detailsSocket.Send(detailsNack, detailsNack.Length, serverEndpoint);
        Assert.True(server.HasPendingMessages);
        Assert.Empty(server.Receive().Payload);
        _ = server.HasPendingMessages; // repair queue emits the matching cached chunk at its bounded pace
        var repairedStatusFrame = detailsSocket.Receive(ref responder);
        Assert.Equal(serverEndpoint, responder);
        Assert.True(UdpFragmentation.TryDecode(repairedStatusFrame, out var repairedStatusHeader));
        Assert.Equal(statusHeader.MessageId, repairedStatusHeader.MessageId);
        Assert.Equal(1, repairedStatusHeader.ChunkIndex);
        Assert.Equal(1, server.KnownPeerCount);
    }
}
