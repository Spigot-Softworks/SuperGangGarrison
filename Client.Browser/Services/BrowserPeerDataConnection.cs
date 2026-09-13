using System.Text.Json;
using Microsoft.JSInterop;
using OpenGarrison.Client;

namespace OpenGarrison.Client.Browser.Services;

internal sealed class BrowserPeerDataConnection : IPeerDataConnection
{
    private readonly IJSInProcessRuntime _js;
    private readonly DotNetObjectReference<BrowserPeerDataConnection> _reference;
    private readonly string _id = Guid.NewGuid().ToString("N");
    private readonly Action<string> _signal;
    private readonly PeerPacketFraming _framing;
    private bool _disposed;
    public bool IsOpen { get; private set; }
    public BrowserPeerDataConnection(IJSInProcessRuntime js, bool offerer, JsonElement ice,
        Action<string> signal, Action<byte[]> receive)
    {
        _js = js; _signal = signal; _framing = new(receive);
        _reference = DotNetObjectReference.Create(this);
        _js.InvokeVoid("OpenGarrisonPeers.create", _id, offerer, ice, _reference);
    }
    [JSInvokable] public void Signal(string signal) { if (!_disposed) _signal(signal); }
    [JSInvokable] public void State(bool open) => IsOpen = !_disposed && open;
    [JSInvokable] public void Receive(byte[] bytes) { if (!_disposed) _framing.Receive(bytes); }
    public void ApplySignal(string signal) { if (!_disposed) _js.InvokeVoid("OpenGarrisonPeers.signal", _id, signal); }
    public bool TrySend(byte[] packet)
    {
        if (!IsOpen || !_js.Invoke<bool>("OpenGarrisonPeers.canSend", _id, packet.Length)) return false;
        try { _framing.Send(packet, bytes => _js.InvokeVoid("OpenGarrisonPeers.send", _id, bytes)); return true; }
        catch (JSException) { Dispose(); return false; }
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; IsOpen = false;
        _js.InvokeVoid("OpenGarrisonPeers.close", _id);
        _reference.Dispose();
    }
}
