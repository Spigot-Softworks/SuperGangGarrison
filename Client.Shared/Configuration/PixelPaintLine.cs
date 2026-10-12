namespace OpenGarrison.ClientShared;

/// <summary>Inclusive, one-pixel-wide lines shared by the bubble and light-mask painters.</summary>
public static class PixelPaintLine
{
    public static IEnumerable<int> Enumerate(int start, int end, int width)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfNegative(end);
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        var x = start % width;
        var y = start / width;
        var endX = end % width;
        var endY = end / width;
        var dx = Math.Abs(endX - x);
        var dy = -Math.Abs(endY - y);
        var stepX = x < endX ? 1 : -1;
        var stepY = y < endY ? 1 : -1;
        var error = dx + dy;
        while (true)
        {
            yield return (y * width) + x;
            if (x == endX && y == endY) yield break;
            var twiceError = 2 * error;
            if (twiceError >= dy) { error += dy; x += stepX; }
            if (twiceError <= dx) { error += dx; y += stepY; }
        }
    }
}
