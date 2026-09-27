using OpenGarrison.ClientShared;

namespace OpenGarrison.Client.Browser.Services;

public sealed class BrowserAssetProbeService(ClientRuntimeComposition runtimeComposition)
{
    private readonly ClientRuntimeComposition _runtimeComposition = runtimeComposition;

}

public sealed record BrowserAssetProbeResult(
    bool Success,
    string Message,
    string? SpriteId,
    string? FirstFrameContentPath,
    string? FirstFramePreviewUrl,
    int FirstFrameByteCount,
    int OriginX,
    int OriginY);
