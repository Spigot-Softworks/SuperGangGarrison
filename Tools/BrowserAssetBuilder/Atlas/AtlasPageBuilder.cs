namespace OpenGarrison.Tools.BrowserAssetBuilder.Atlas;

internal sealed class AtlasPageBuilder(BrowserAtlasGroup group, int pageIndex)
{
    private readonly RectanglePacker _packer = new(group.MaxWidth, group.MaxHeight);

    public BrowserAtlasGroup Group { get; } = group;

    public int PageIndex { get; } = pageIndex;

    public List<AtlasPlacedFrame> Frames { get; } = [];

    public bool TryPlace(AtlasFrameSource frameSource, out AtlasPlacedFrame placedFrame)
    {
        var allocatedWidth = frameSource.Width + (Group.Padding * 2);
        var allocatedHeight = frameSource.Height + (Group.Padding * 2);
        if (!_packer.TryPlace(allocatedWidth, allocatedHeight, out var x, out var y))
        {
            placedFrame = null!;
            return false;
        }

        placedFrame = new AtlasPlacedFrame(
            frameSource,
            x + Group.Padding,
            y + Group.Padding,
            frameSource.Width,
            frameSource.Height);
        Frames.Add(placedFrame);
        return true;
    }

    public int OutputWidth => Math.Max(1, _packer.UsedWidth);

    public int OutputHeight => Math.Max(1, _packer.UsedHeight);
}

internal sealed record AtlasFrameSource(
    string SpriteId,
    string GroupId,
    string SourcePath,
    int SourceImageIndex,
    int SourceFrameIndex,
    int Width,
    int Height,
    byte[] PixelBytes);

internal sealed record AtlasPlacedFrame(
    AtlasFrameSource Source,
    int X,
    int Y,
    int Width,
    int Height);
