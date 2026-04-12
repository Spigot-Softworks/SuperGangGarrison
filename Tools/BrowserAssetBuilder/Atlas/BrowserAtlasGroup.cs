namespace OpenGarrison.Tools.BrowserAssetBuilder.Atlas;

internal sealed record BrowserAtlasGroup(
    string Id,
    int MaxWidth = 2048,
    int MaxHeight = 2048,
    int Padding = 2);
