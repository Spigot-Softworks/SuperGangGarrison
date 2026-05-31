using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using OpenGarrison.Core;

namespace OpenGarrison.Server;

internal static class ServerMapDownloadEndpoint
{
    public const string RoutePrefix = "/opengarrison/maps";

    public static void Map(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapMethods($"{RoutePrefix}/{{**path}}", new[] { "OPTIONS" }, HandleOptionsAsync);
        app.MapGet($"{RoutePrefix}/{{levelName}}/{{**relativePath}}", HandlePackageFileAsync);
        app.MapGet($"{RoutePrefix}/{{fileName}}", HandleLegacyFileAsync);
    }

    public static string BuildAdvertisedDownloadUrl(
        CustomMapDescriptor descriptor,
        string? publicWebSocketUrl,
        string? publicHost,
        int port,
        bool preferHttps = false)
    {
        var relativeUrl = BuildRelativeDownloadUrl(descriptor);
        return TryCreatePublicBaseUri(publicWebSocketUrl, publicHost, port, preferHttps, out var publicBaseUri)
            ? new Uri(publicBaseUri, relativeUrl).ToString()
            : relativeUrl;
    }

    public static string BuildRelativeDownloadUrl(CustomMapDescriptor descriptor)
    {
        return descriptor.SourceKind == CustomMapSourceKind.Package
            ? $"{RoutePrefix}/{EscapeSegment(descriptor.LevelName)}/{EscapeRelativePath(Path.GetFileName(descriptor.LocalFilePath))}"
            : $"{RoutePrefix}/{EscapeSegment(descriptor.LevelName)}.png";
    }

    private static async Task HandlePackageFileAsync(HttpContext context, string levelName, string? relativePath)
    {
        ApplyDownloadHeaders(context);
        if (!CustomMapLocatorStore.TryNormalizeLevelName(levelName, out var normalizedLevelName)
            || string.IsNullOrWhiteSpace(relativePath)
            || !CustomMapDescriptorResolver.TryResolve(normalizedLevelName, out var descriptor)
            || descriptor.SourceKind != CustomMapSourceKind.Package)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var normalizedRequestPath = NormalizeRequestRelativePath(relativePath);
        var files = CustomMapPackageImporter.GetPackageContentFiles(descriptor.LocalFilePath);
        var file = files.FirstOrDefault(candidate =>
            candidate.RelativePath.Equals(normalizedRequestPath, StringComparison.OrdinalIgnoreCase));
        if (file is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await SendFileAsync(context, file.FullPath, GetContentType(file.RelativePath)).ConfigureAwait(false);
    }

    private static async Task HandleLegacyFileAsync(HttpContext context, string fileName)
    {
        ApplyDownloadHeaders(context);
        if (!Path.GetExtension(fileName).Equals(".png", StringComparison.OrdinalIgnoreCase)
            || !CustomMapLocatorStore.TryNormalizeLevelName(Path.GetFileNameWithoutExtension(fileName), out var normalizedLevelName)
            || !CustomMapDescriptorResolver.TryResolve(normalizedLevelName, out var descriptor)
            || descriptor.SourceKind != CustomMapSourceKind.LegacyPng)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await SendFileAsync(context, descriptor.LocalFilePath, "image/png").ConfigureAwait(false);
    }

    private static Task HandleOptionsAsync(HttpContext context)
    {
        ApplyDownloadHeaders(context);
        context.Response.StatusCode = StatusCodes.Status204NoContent;
        return Task.CompletedTask;
    }

    private static async Task SendFileAsync(HttpContext context, string fullPath, string contentType)
    {
        ApplyDownloadHeaders(context);
        if (!File.Exists(fullPath))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        context.Response.ContentType = contentType;
        context.Response.Headers.CacheControl = "no-store";
        await context.Response.SendFileAsync(fullPath, context.RequestAborted).ConfigureAwait(false);
    }

    private static void ApplyDownloadHeaders(HttpContext context)
    {
        context.Response.Headers["Access-Control-Allow-Origin"] = "*";
        context.Response.Headers["Access-Control-Allow-Methods"] = "GET, OPTIONS";
        context.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type, Range";
        context.Response.Headers["Access-Control-Expose-Headers"] = "Content-Length, Content-Type";
        context.Response.Headers["Cross-Origin-Resource-Policy"] = "cross-origin";
        context.Response.Headers.CacheControl = "no-store";
    }

    private static string GetContentType(string relativePath)
    {
        return Path.GetExtension(relativePath).Equals(".json", StringComparison.OrdinalIgnoreCase)
            ? "application/json"
            : "image/png";
    }

    private static string NormalizeRequestRelativePath(string value)
    {
        return value.Trim().TrimStart('/', '\\').Replace('\\', '/');
    }

    private static string EscapeRelativePath(string relativePath)
    {
        var segments = relativePath
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
        return string.Join('/', segments.Select(EscapeSegment));
    }

    private static string EscapeSegment(string value)
    {
        return Uri.EscapeDataString(value);
    }

    private static bool TryCreatePublicBaseUri(
        string? publicWebSocketUrl,
        string? publicHost,
        int port,
        bool preferHttps,
        out Uri baseUri)
    {
        if (TryCreateBaseUriFromPublicWebSocketUrl(publicWebSocketUrl, out baseUri))
        {
            return true;
        }

        if (TryCreateBaseUriFromPublicHost(publicHost, port, preferHttps, out baseUri))
        {
            return true;
        }

        baseUri = null!;
        return false;
    }

    private static bool TryCreateBaseUriFromPublicWebSocketUrl(string? publicWebSocketUrl, out Uri baseUri)
    {
        baseUri = null!;
        if (string.IsNullOrWhiteSpace(publicWebSocketUrl)
            || !Uri.TryCreate(publicWebSocketUrl.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != "ws" && uri.Scheme != "wss"))
        {
            return false;
        }

        var builder = new UriBuilder(uri)
        {
            Scheme = uri.Scheme == "wss" ? Uri.UriSchemeHttps : Uri.UriSchemeHttp,
            Path = string.Empty,
            Query = string.Empty,
            Fragment = string.Empty,
        };
        baseUri = EnsureTrailingSlash(builder.Uri);
        return true;
    }

    private static bool TryCreateBaseUriFromPublicHost(string? publicHost, int port, bool preferHttps, out Uri baseUri)
    {
        baseUri = null!;
        if (string.IsNullOrWhiteSpace(publicHost))
        {
            return false;
        }

        var candidate = publicHost.Trim();
        if (!candidate.Contains("://", StringComparison.Ordinal))
        {
            candidate = $"{(preferHttps ? Uri.UriSchemeHttps : Uri.UriSchemeHttp)}://{candidate}";
        }

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || string.IsNullOrWhiteSpace(uri.Host))
        {
            return false;
        }

        var builder = new UriBuilder(uri)
        {
            Path = string.Empty,
            Query = string.Empty,
            Fragment = string.Empty,
        };
        if (uri.IsDefaultPort && port is > 0 and <= 65535)
        {
            builder.Port = port;
        }

        baseUri = EnsureTrailingSlash(builder.Uri);
        return true;
    }

    private static Uri EnsureTrailingSlash(Uri uri)
    {
        var text = uri.ToString();
        return text.Length > 0 && text[^1] == '/'
            ? uri
            : new Uri($"{text}/", UriKind.Absolute);
    }
}
