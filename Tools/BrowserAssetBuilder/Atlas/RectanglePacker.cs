namespace OpenGarrison.Tools.BrowserAssetBuilder.Atlas;

internal sealed class RectanglePacker(int maxWidth, int maxHeight)
{
    private readonly int _maxWidth = maxWidth;
    private readonly int _maxHeight = maxHeight;
    private int _cursorX;
    private int _cursorY;
    private int _rowHeight;
    private int _usedWidth;
    private int _usedHeight;

    public bool TryPlace(int width, int height, out int x, out int y)
    {
        x = 0;
        y = 0;
        if (width <= 0 || height <= 0 || width > _maxWidth || height > _maxHeight)
        {
            return false;
        }

        if (_cursorX + width > _maxWidth)
        {
            _cursorX = 0;
            _cursorY += _rowHeight;
            _rowHeight = 0;
        }

        if (_cursorY + height > _maxHeight)
        {
            return false;
        }

        x = _cursorX;
        y = _cursorY;
        _cursorX += width;
        _rowHeight = Math.Max(_rowHeight, height);
        _usedWidth = Math.Max(_usedWidth, _cursorX);
        _usedHeight = Math.Max(_usedHeight, _cursorY + _rowHeight);
        return true;
    }

    public int UsedWidth => _usedWidth;

    public int UsedHeight => _usedHeight;
}
