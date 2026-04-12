using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Hosting;

namespace OpenGarrison.Server;

internal sealed class WebTransportServerHost : IDisposable
{
    private readonly int _port;
    private readonly string _certificatePath;
    private readonly string? _certificatePassword;
    private readonly CompositeServerDatagramTransport _transport;
    private readonly Action<string> _log;
    private WebApplication? _application;

    public WebTransportServerHost(
        int port,
        string certificatePath,
        string? certificatePassword,
        CompositeServerDatagramTransport transport,
        Action<string> log)
    {
        _port = port;
        _certificatePath = certificatePath;
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

        AppContext.SetSwitch("Microsoft.AspNetCore.Server.Kestrel.Experimental.WebTransportAndH3Datagrams", true);
        var certificate = LoadCertificate();
        LogCertificateDiagnostics(certificate);
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            ContentRootPath = AppContext.BaseDirectory,
            ApplicationName = typeof(WebTransportServerHost).Assembly.GetName().Name,
        });
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.ListenAnyIP(_port, listenOptions =>
            {
                listenOptions.Protocols = HttpProtocols.Http1AndHttp2AndHttp3;
                listenOptions.UseHttps(certificate);
            });
        });

        var app = builder.Build();
        app.Map("/.well-known/opengarrison/wt", HandleWebTransportAsync);
        app.MapGet("/", static context =>
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return Task.CompletedTask;
        });
        app.StartAsync().GetAwaiter().GetResult();
        _application = app;
        _log($"[server] WebTransport listener enabled on https://0.0.0.0:{_port}/.well-known/opengarrison/wt");
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

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA2252:This API requires opting into preview features", Justification = "WebTransport is an intentional preview dependency for the browser transport path.")]
    private async Task HandleWebTransportAsync(HttpContext context)
    {
        _log($"[server] WebTransport endpoint hit method={context.Request.Method} protocol={context.Request.Protocol} remote={context.Connection.RemoteIpAddress}:{context.Connection.RemotePort}");
        var webTransportFeature = context.Features.Get<IHttpWebTransportFeature>();
        if (webTransportFeature?.IsWebTransportRequest != true)
        {
            _log($"[server] WebTransport endpoint rejected non-WebTransport request method={context.Request.Method} protocol={context.Request.Protocol}");
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync("WebTransport request required.", context.RequestAborted).ConfigureAwait(false);
            return;
        }

        var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var session = await webTransportFeature.AcceptAsync(context.RequestAborted).ConfigureAwait(false);
        if (session is null)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return;
        }

        var description = $"wt:{remoteIp}#{session.SessionId}";
        _log($"[server] WebTransport session accepted {description}");
        var stream = await session.AcceptStreamAsync(context.RequestAborted).ConfigureAwait(false);
        if (stream is null)
        {
            session.Abort(0x100);
            _log($"[server] WebTransport session {description} closed before creating a stream.");
            return;
        }

        await _transport.RunWebTransportPeerAsync(session.SessionId, description, stream.Transport.Input, stream.Transport.Output, _log, context.RequestAborted).ConfigureAwait(false);
        _log($"[server] WebTransport session ended {description}");
    }

    private X509Certificate2 LoadCertificate()
    {
        if (!File.Exists(_certificatePath))
        {
            throw new FileNotFoundException($"WebTransport certificate file was not found: {_certificatePath}", _certificatePath);
        }

        return X509CertificateLoader.LoadPkcs12FromFile(_certificatePath, _certificatePassword);
    }

    private void LogCertificateDiagnostics(X509Certificate2 certificate)
    {
        _log($"[server] WebTransport certificate subject={certificate.Subject} issuer={certificate.Issuer} thumbprint={certificate.Thumbprint}");
        var certificateHash = Convert.ToHexStringLower(SHA256.HashData(certificate.RawData));
        _log($"[server] WebTransport certificate sha256={certificateHash}");
        _log("[server] WebTransport dev hint: set localStorage['opengarrison.webtransport.cert.sha256'] to that hex value in Chromium to test certificate-pinned local sessions.");
        if (string.Equals(certificate.Subject, certificate.Issuer, StringComparison.OrdinalIgnoreCase))
        {
            _log("[server] WebTransport warning: browsers commonly reject self-signed certificates for HTTP/3/WebTransport. Use a trusted CA-issued certificate for browser connectivity tests.");
        }
    }
}
