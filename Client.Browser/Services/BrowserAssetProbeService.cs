using OpenGarrison.ClientShared;

namespace OpenGarrison.Client.Browser.Services;

public sealed class BrowserAssetProbeService(ClientRuntimeComposition runtimeComposition)
{
    private readonly ClientRuntimeComposition _runtimeComposition = runtimeComposition;

    public async Task<BrowserAssetProbeResult> ProbeGameplaySpriteAsync(string spriteName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(spriteName);

        try
        {
            var spriteAsset = await _runtimeComposition.GameplayPackSpriteAssets.GetRequired("stock.gg2").TryLoadAsync(spriteName);
            if (spriteAsset is null)
            {
                return new BrowserAssetProbeResult(false, "Sprite definition could not be deserialized.", null, null, null, 0, 0, 0);
            }

            var firstFrame = spriteAsset.SourceSet.SourceImages.Count > 0
                ? spriteAsset.SourceSet.SourceImages[0]
                : null;
            var firstFrameByteCount = firstFrame?.Bytes.Length ?? 0;
            var firstFramePreviewUrl = firstFrame is null || firstFrame.Bytes.Length == 0
                ? null
                : $"data:image/png;base64,{Convert.ToBase64String(firstFrame.Bytes)}";

            var message = firstFramePreviewUrl is null
                ? $"Loaded sprite definition \"{spriteAsset.Definition.Definition.Id}\", but the first frame bytes were empty."
                : $"Loaded sprite definition \"{spriteAsset.Definition.Definition.Id}\" and its first frame bytes.";

            return new BrowserAssetProbeResult(
                firstFramePreviewUrl is not null,
                message,
                spriteAsset.Definition.Definition.Id,
                spriteAsset.Definition.FirstFrameContentPath,
                firstFramePreviewUrl,
                firstFrameByteCount,
                spriteAsset.Definition.Definition.OriginX,
                spriteAsset.Definition.Definition.OriginY);
        }
        catch (Exception ex)
        {
            return new BrowserAssetProbeResult(false, ex.Message, null, null, null, 0, 0, 0);
        }
    }
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
