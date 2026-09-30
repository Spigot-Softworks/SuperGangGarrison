using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenGarrison.Client;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class PeerDataConnectionTests
{

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
