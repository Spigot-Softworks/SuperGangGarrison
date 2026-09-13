using System;
using OpenGarrison.SessionRuntime;

namespace OpenGarrison.Client;

internal sealed class EmbeddedSessionClientTransport(EmbeddedSessionPeer peer) : INetworkClientMessageTransport
{
    private bool _reportedClose;
    public bool HasPendingMessages => peer.HasPendingMessages;
    public bool IsLoopbackConnection => true;
    public string RemoteDescription => "ws64://embedded/private-session";
    public bool TryReceive(out byte[] payload) => peer.TryReceive(out payload);
    public void Send(byte[] payload) => peer.Send(payload);
    public bool TryConsumeDisconnectReason(out string reason)
    {
        reason = peer.CloseReason;
        if (_reportedClose || !peer.IsClosed) return false;
        _reportedClose = true;
        return true;
    }
    public void Dispose() => peer.Dispose();
}
