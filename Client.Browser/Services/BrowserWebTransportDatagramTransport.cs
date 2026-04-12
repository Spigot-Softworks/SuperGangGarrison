using System.Collections.Concurrent;
using Microsoft.JSInterop;
using OpenGarrison.Client;

namespace OpenGarrison.Client.Browser.Services;

internal sealed class BrowserWebTransportDatagramTransport : INetworkClientDatagramTransport
{
    private readonly IJSInProcessRuntime _jsRuntime;
    private readonly DotNetObjectReference<BrowserWebTransportDatagramTransport> _callbackReference;
    private readonly ConcurrentQueue<byte[]> _inboundDatagrams = new();
    private readonly ConcurrentQueue<byte[]> _pendingOutboundDatagrams = new();

    private readonly object _stateGate = new();
    private readonly string _clientId;
    private readonly string _remoteDescription;

    private string? _disconnectReason;
    private bool _isReady;
    private bool _isDisposed;

    private BrowserWebTransportDatagramTransport(IJSInProcessRuntime jsRuntime, string remoteDescription, string clientId)
    {
        _jsRuntime = jsRuntime;
        _remoteDescription = remoteDescription;
        _clientId = clientId;
        _callbackReference = DotNetObjectReference.Create(this);
    }

    public bool HasPendingDatagrams => !_inboundDatagrams.IsEmpty;
    public bool IsLoopbackConnection => false;
    public string RemoteDescription => _remoteDescription;

    public static bool TryConnect(
        IJSRuntime jsRuntime,
        string host,
        int port,
        out INetworkClientDatagramTransport? transport,
        out string error)
    {
        transport = null;
        error = string.Empty;

        if (jsRuntime is not IJSInProcessRuntime inProcessRuntime)
        {
            error = "Browser networking requires in-process JS interop.";
            return false;
        }

        var remoteDescription = $"{host}:{port}";
        var pendingTransport = new BrowserWebTransportDatagramTransport(inProcessRuntime, remoteDescription, Guid.NewGuid().ToString("N"));
        try
        {
            pendingTransport.Initialize(host, port);
            transport = pendingTransport;
            return true;
        }
        catch (JSException ex)
        {
            pendingTransport.Dispose();
            error = ex.Message;
            return false;
        }
        catch (InvalidOperationException ex)
        {
            pendingTransport.Dispose();
            error = ex.Message;
            return false;
        }
    }

    public bool TryReceive(out byte[] payload)
    {
        return _inboundDatagrams.TryDequeue(out payload!);
    }

    public bool TryConsumeDisconnectReason(out string reason)
    {
        lock (_stateGate)
        {
            if (string.IsNullOrWhiteSpace(_disconnectReason))
            {
                reason = string.Empty;
                return false;
            }

            reason = _disconnectReason;
            _disconnectReason = null;
            return true;
        }
    }

    public void Send(byte[] payload)
    {
        if (payload.Length == 0)
        {
            return;
        }

        lock (_stateGate)
        {
            if (_isDisposed)
            {
                return;
            }

            if (!_isReady)
            {
                _pendingOutboundDatagrams.Enqueue(payload);
                return;
            }
        }

        SendNow(payload);
    }

    public void Dispose()
    {
        lock (_stateGate)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
        }

        try
        {
            _jsRuntime.InvokeVoid("OpenGarrisonBrowserHost.closeWebTransportDatagramClient", _clientId);
        }
        catch
        {
        }

        _callbackReference.Dispose();
    }

    [JSInvokable("HandleWebTransportReady")]
    public void HandleWebTransportReady()
    {
        lock (_stateGate)
        {
            if (_isDisposed)
            {
                return;
            }

            _isReady = true;
        }

        FlushPendingOutboundDatagrams();
    }

    [JSInvokable("HandleWebTransportDatagram")]
    public void HandleWebTransportDatagram(byte[] payload)
    {
        if (payload.Length > 0)
        {
            _inboundDatagrams.Enqueue(payload);
        }
    }

    [JSInvokable("HandleWebTransportDisconnect")]
    public void HandleWebTransportDisconnect(string? reason)
    {
        lock (_stateGate)
        {
            _disconnectReason ??= string.IsNullOrWhiteSpace(reason)
                ? "WebTransport connection closed."
                : reason.Trim();
        }
    }

    private void Initialize(string host, int port)
    {
        _jsRuntime.InvokeVoid(
            "OpenGarrisonBrowserHost.openWebTransportDatagramClient",
            _clientId,
            host,
            port,
            _callbackReference);
    }

    private void FlushPendingOutboundDatagrams()
    {
        while (_pendingOutboundDatagrams.TryDequeue(out var payload))
        {
            SendNow(payload);
        }
    }

    private void SendNow(byte[] payload)
    {
        try
        {
            _jsRuntime.InvokeVoid("OpenGarrisonBrowserHost.sendWebTransportDatagram", _clientId, payload);
        }
        catch (JSException ex)
        {
            HandleWebTransportDisconnect(ex.Message);
        }
    }
}
