using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using OpenGarrison.Core;

namespace OpenGarrison.Server;

/// <summary>
/// Serves plugin pack art to clients that don't have the plugin, next to custom map downloads:
/// GET /opengarrison/plugin-packs/{packId}/{hash}/manifest.json and the files it lists.
/// </summary>
internal static class ServerPluginPackDownloadEndpoint
{
    private static readonly ServerPluginPackFileResolver Resolver = new(CharacterClassCatalog.RuntimeRegistry);

    public static void Map(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var prefix = GameplayPackAssetManifest.RoutePrefix;
        app.MapMethods($"{prefix}/{{**path}}", new[] { "OPTIONS" }, HandleOptionsAsync);
        app.MapGet($"{prefix}/{{packId}}/{{hash}}/{GameplayPackAssetManifest.ManifestFileName}", HandleManifestAsync);
        app.MapGet($"{prefix}/{{packId}}/{{hash}}/{{**relativePath}}", HandleFileAsync);
    }

    private static async Task HandleManifestAsync(HttpContext context, string packId, string hash)
    {
        ApplyDownloadHeaders(context);
        if (!Resolver.TryGetManifest(packId, hash, out var manifest, out _))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        context.Response.ContentType = "application/json";
        await context.Response.Body.WriteAsync(manifest.SerializeToUtf8Json(), context.RequestAborted).ConfigureAwait(false);
    }

    private static async Task HandleFileAsync(HttpContext context, string packId, string hash, string? relativePath)
    {
        ApplyDownloadHeaders(context);
        if (string.IsNullOrWhiteSpace(relativePath)
            || !Resolver.TryResolveFile(packId, hash, relativePath, out var fullPath)
            || !File.Exists(fullPath))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        context.Response.ContentType = Path.GetExtension(fullPath).ToLowerInvariant() switch
        {
            ".wav" => "audio/wav",
            ".ogg" => "audio/ogg",
            _ => "image/png",
        };
        await context.Response.SendFileAsync(fullPath, context.RequestAborted).ConfigureAwait(false);
    }

    private static Task HandleOptionsAsync(HttpContext context)
    {
        ApplyDownloadHeaders(context);
        context.Response.StatusCode = StatusCodes.Status204NoContent;
        return Task.CompletedTask;
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
}
