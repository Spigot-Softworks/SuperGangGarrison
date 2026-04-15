using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Hosting;
using System.Security.Cryptography.X509Certificates;

namespace OpenGarrison.Server;

internal sealed class WebSocketServerHost : IDisposable
{
    private readonly int _port;
    private readonly string? _certificatePath;
    private readonly string? _certificatePassword;
    private readonly CompositeServerMessageTransport _transport;
    private readonly Action<string> _log;
    private WebApplication? _application;

    public WebSocketServerHost(
        int port,
        string? certificatePath,
        string? certificatePassword,
        CompositeServerMessageTransport transport,
        Action<string> log)
    {
        _port = port;
        _certificatePath = string.IsNullOrWhiteSpace(certificatePath) ? null : certificatePath;
        _certificatePassword = string.IsNullOrWhiteSpace(certificatePassword) ? null : certificatePassword;
        _transport = transport;
        _log = log;
    }

    public void Start()
    {
        if (_application is not null)
        {
            return;
        }

        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            ContentRootPath = AppContext.BaseDirectory,
            ApplicationName = typeof(WebSocketServerHost).Assembly.GetName().Name,
        });
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.ListenAnyIP(_port, listenOptions =>
            {
                listenOptions.Protocols = HttpProtocols.Http1;
                if (_certificatePath is not null)
                {
                    listenOptions.UseHttps(LoadCertificate());
                }
            });
        });

        var app = builder.Build();
        app.UseWebSockets(new WebSocketOptions
        {
            KeepAliveInterval = TimeSpan.FromSeconds(20),
        });
        app.Map("/opengarrison/ws", HandleWebSocketAsync);
        app.MapGet("/", static context =>
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return Task.CompletedTask;
        });
        app.StartAsync().GetAwaiter().GetResult();
        _application = app;
        var scheme = _certificatePath is null ? "ws" : "wss";
        _log($"[server] WebSocket listener enabled on {scheme}://0.0.0.0:{_port}/opengarrison/ws");
    }

    public void Dispose()
    {
        if (_application is null)
        {
            return;
        }

        try
        {
            _application.StopAsync().GetAwaiter().GetResult();
        }
        catch
        {
        }

        try
        {
            _application.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        catch
        {
        }

        _application = null;
    }

    private async Task HandleWebSocketAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync("WebSocket request required.", context.RequestAborted).ConfigureAwait(false);
            return;
        }

        var remoteIp = context.Connection.RemoteIpAddress;
        var remotePort = context.Connection.RemotePort;
        using var webSocket = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
        _log($"[server] WebSocket session accepted remote={remoteIp}:{remotePort}");
        await _transport.RunWebSocketPeerAsync(webSocket, remoteIp, remotePort, _log, context.RequestAborted).ConfigureAwait(false);
        _log($"[server] WebSocket session ended remote={remoteIp}:{remotePort}");
    }

    private X509Certificate2 LoadCertificate()
    {
        if (_certificatePath is null)
        {
            throw new InvalidOperationException("WebSocket certificate path is not configured.");
        }

        if (!File.Exists(_certificatePath))
        {
            throw new FileNotFoundException($"WebSocket certificate file was not found: {_certificatePath}", _certificatePath);
        }

        return X509CertificateLoader.LoadPkcs12FromFile(_certificatePath, _certificatePassword);
    }
}
