namespace OpenGarrison.Tools.BrowserAssetBuilder;

internal sealed record BrowserAssetBuildReport(
    int GeneratedAtlasCount,
    int GeneratedAtlasPageCount,
    int GeneratedSpriteCount,
    IReadOnlyList<string> GeneratedFiles,
    IReadOnlyList<string> Warnings);
