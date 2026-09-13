using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenGarrison.Client;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class PeerDataConnectionTests
{
    [Fact]
    public async Task ThreeGuestsExchangeIndependentFragmentedPacketsWithOneNativeHost()
    {
        using var diagnostics = new PeerLogFactory();
        SIPSorcery.LogFactory.Set(diagnostics);
        using var ice = JsonDocument.Parse("[]");
        var connections = new List<IPeerDataConnection>();
        try
        {
            var routes = Enumerable.Range(2, 3).Select(slot =>
            {
                var offers = new ConcurrentQueue<string>(); var answers = new ConcurrentQueue<string>();
                var hostPackets = new ConcurrentQueue<byte[]>(); var guestPackets = new ConcurrentQueue<byte[]>();
                var host = PeerDataConnectionFactory.Create(true, ice.RootElement, offers.Enqueue, hostPackets.Enqueue);
                var guest = PeerDataConnectionFactory.Create(false, ice.RootElement, answers.Enqueue, guestPackets.Enqueue);
                connections.Add(host); connections.Add(guest);
                return (slot, offers, answers, hostPackets, guestPackets, host, guest);
            }).ToArray();
            var deadline = Environment.TickCount64 + 15000;
            while (Environment.TickCount64 < deadline && routes.Any(r => !r.host.IsOpen || !r.guest.IsOpen))
            {
                foreach (var r in routes)
                {
                    while (r.offers.TryDequeue(out var offer)) r.guest.ApplySignal(offer);
                    while (r.answers.TryDequeue(out var answer)) r.host.ApplySignal(answer);
                }
                await Task.Delay(20);
            }
            Assert.True(connections.All(connection => connection.IsOpen),
                string.Join("\n", connections) + "\n" + string.Join("\n", diagnostics.Messages.TakeLast(100)));
            foreach (var r in routes)
            {
                Assert.True(r.host.TrySend(Enumerable.Repeat((byte)r.slot, 90000).ToArray()));
                Assert.True(r.guest.TrySend(Enumerable.Repeat((byte)(r.slot + 10), 65000).ToArray()));
            }
            deadline = Environment.TickCount64 + 10000;
            while (Environment.TickCount64 < deadline && routes.Any(r => r.hostPackets.IsEmpty || r.guestPackets.IsEmpty)) await Task.Delay(20);
            foreach (var r in routes)
            {
                Assert.True(r.hostPackets.TryDequeue(out var atHost));
                Assert.Equal(Enumerable.Repeat((byte)(r.slot + 10), 65000), atHost!);
                Assert.True(r.guestPackets.TryDequeue(out var atGuest));
                Assert.Equal(Enumerable.Repeat((byte)r.slot, 90000), atGuest!);
            }
        }
        finally
        {
            foreach (var connection in connections) connection.Dispose();
            SIPSorcery.LogFactory.Set(Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
        }
    }

    private sealed class PeerLogFactory : ILoggerFactory, ILogger
    {
        public ConcurrentQueue<string> Messages { get; } = new();
        public ILogger CreateLogger(string categoryName) => this;
        public void AddProvider(ILoggerProvider provider) { }
        public void Dispose() { }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => level >= LogLevel.Debug;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        { if (IsEnabled(level)) Messages.Enqueue(formatter(state, exception)); }
    }

    [Fact]
    public void PacketFramingRejectsInvalidLengthsAndDoesNotJoinDifferentPackets()
    {
        var completed = new List<byte[]>(); var chunks = new List<byte[]>();
        var sender = new PeerPacketFraming(_ => { }); var receiver = new PeerPacketFraming(completed.Add);
        sender.Send(new byte[33000], chunks.Add);
        receiver.Receive(chunks[1]); receiver.Receive(chunks[2]);
        Assert.Empty(completed);
        foreach (var chunk in chunks) receiver.Receive(chunk);
        Assert.Single(completed); Assert.Equal(33000, completed[0].Length);
        Assert.Throws<ArgumentOutOfRangeException>(() => sender.Send(new byte[4 * 1024 * 1024 + 1], _ => { }));
    }
}
