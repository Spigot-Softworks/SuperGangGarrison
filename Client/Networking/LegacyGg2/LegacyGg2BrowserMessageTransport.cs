#nullable enable

namespace OpenGarrison.Client;

/// <summary>Identifies translated GG2 messages carried by the browser WebSocket relay.</summary>
internal sealed class LegacyGg2BrowserMessageTransport : INetworkClientMessageTransport
{
    private readonly INetworkClientMessageTransport _inner;
    private readonly string _description;

    internal LegacyGg2BrowserMessageTransport(INetworkClientMessageTransport inner, string host, int port)
    {
        _inner = inner;
        _description = $"gg2:{host}:{port}";
    }

    public bool HasPendingMessages => _inner.HasPendingMessages;
    public bool IsLoopbackConnection => false;
    public string RemoteDescription => _description;
    public int ReceiveTimeoutMilliseconds => 15000;
    public bool TryReceive(out byte[] payload) => _inner.TryReceive(out payload!);
    public bool TryConsumeDisconnectReason(out string reason) => _inner.TryConsumeDisconnectReason(out reason);
    public void Send(byte[] payload) => _inner.Send(payload);
    public void Dispose() => _inner.Dispose();
}
