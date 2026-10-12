namespace OpenGarrison.ClientShared;

/// <summary>A binary source-pixel mask, with history grouped by brush stroke.</summary>
public sealed class LightMaskPaintCanvas
{
    private readonly byte[] _pixels;
    private readonly Dictionary<int, (byte Before, byte After)> _stroke = [];
    private readonly List<Dictionary<int, (byte Before, byte After)>> _undo = [];
    private readonly List<Dictionary<int, (byte Before, byte After)>> _redo = [];

    public LightMaskPaintCanvas(int width, int height, byte[]? pixels = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(width, 4002);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(height, 4002);
        Width = width;
        Height = height;
        if (pixels is not null && pixels.Length != width * height)
            throw new ArgumentException("Pixel count must match the canvas.", nameof(pixels));
        _pixels = pixels is null ? new byte[width * height] : (byte[])pixels.Clone();
    }

    public int Width { get; }
    public int Height { get; }
    public ReadOnlySpan<byte> Pixels => _pixels;

    /// <summary>Expands around the existing centre, retaining pixels and undo/redo without cropping or resampling.</summary>
    public LightMaskPaintCanvas EnsureMinimumSize(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        var newWidth = Math.Max(Width, width);
        var newHeight = Math.Max(Height, height);
        // Symmetric whole-pixel padding keeps the centre and world-grid origin aligned,
        // including older non-square or odd-sized masks.
        newWidth = checked(newWidth + ((newWidth - Width) & 1));
        newHeight = checked(newHeight + ((newHeight - Height) & 1));
        if (newWidth == Width && newHeight == Height) return this;
        var expanded = new LightMaskPaintCanvas(newWidth, newHeight);
        var left = (newWidth - Width) / 2;
        var top = (newHeight - Height) / 2;
        for (var y = 0; y < Height; y++)
            _pixels.AsSpan(y * Width, Width).CopyTo(expanded._pixels.AsSpan((y + top) * newWidth + left, Width));
        CopyChanges(_stroke, expanded._stroke);
        foreach (var changes in _undo) expanded._undo.Add(Remap(changes));
        foreach (var changes in _redo) expanded._redo.Add(Remap(changes));
        return expanded;

        Dictionary<int, (byte Before, byte After)> Remap(Dictionary<int, (byte Before, byte After)> changes)
        {
            var mapped = new Dictionary<int, (byte Before, byte After)>(changes.Count);
            CopyChanges(changes, mapped);
            return mapped;
        }
        void CopyChanges(Dictionary<int, (byte Before, byte After)> changes, Dictionary<int, (byte Before, byte After)> mapped)
        {
            foreach (var (pixel, change) in changes)
                mapped[(pixel / Width + top) * newWidth + pixel % Width + left] = change;
        }
    }

    /// <summary>Promotes an older coarse mask without moving or losing any painted pixels.</summary>
    public static LightMaskPaintCanvas ExpandSourcePixels(int width, int height, byte[] pixels, int scale,
        int extraWidth = 0, int extraHeight = 0)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(scale, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(scale, 4);
        ArgumentOutOfRangeException.ThrowIfNegative(extraWidth);
        ArgumentOutOfRangeException.ThrowIfNegative(extraHeight);
        if (pixels.Length != width * height) throw new ArgumentException("Pixel count must match the canvas.", nameof(pixels));
        var canvas = new LightMaskPaintCanvas(checked(width * scale + extraWidth), checked(height * scale + extraHeight));
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                for (var dy = 0; dy < scale; dy++)
                    for (var dx = 0; dx < scale; dx++)
                        canvas._pixels[(y * scale + dy) * canvas.Width + x * scale + dx] = pixels[y * width + x];
        return canvas;
    }

    public bool Paint(int pixel, bool erase)
    {
        if (pixel < 0 || pixel >= _pixels.Length) return false;
        var after = erase ? (byte)0 : (byte)255;
        if (_pixels[pixel] == after) return false;
        var before = _stroke.TryGetValue(pixel, out var change) ? change.Before : _pixels[pixel];
        _stroke[pixel] = (before, after);
        _pixels[pixel] = after;
        return true;
    }

    public bool PaintBrush(int pixel, bool erase, int size)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(size, 1);
        if (pixel < 0 || pixel >= _pixels.Length) return false;
        var left = pixel % Width;
        var top = pixel / Width;
        var changed = false;
        for (var y = top; y < Math.Min(Height, top + size); y++)
            for (var x = left; x < Math.Min(Width, left + size); x++) changed |= Paint(y * Width + x, erase);
        return changed;
    }

    public bool PaintLine(int start, int end, bool erase, int brushSize = 1)
    {
        if (start < 0 || end < 0 || start >= _pixels.Length || end >= _pixels.Length) return false;
        var changed = false;
        foreach (var pixel in PixelPaintLine.Enumerate(start, end, Width)) changed |= PaintBrush(pixel, erase, brushSize);
        return changed;
    }

    public void CommitStroke()
    {
        if (_stroke.Count == 0) return;
        var changes = _stroke.Where(pair => pair.Value.Before != pair.Value.After)
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        _stroke.Clear();
        if (changes.Count == 0) return;
        _undo.Add(changes);
        if (_undo.Count > 100) _undo.RemoveAt(0);
        _redo.Clear();
    }

    public bool Undo()
    {
        CommitStroke();
        return MoveHistory(_undo, _redo, false);
    }

    public bool Redo() => MoveHistory(_redo, _undo, true);

    private bool MoveHistory(List<Dictionary<int, (byte Before, byte After)>> from,
        List<Dictionary<int, (byte Before, byte After)>> to, bool forward)
    {
        if (from.Count == 0) return false;
        var changes = from[^1];
        from.RemoveAt(from.Count - 1);
        foreach (var (pixel, change) in changes) _pixels[pixel] = forward ? change.After : change.Before;
        to.Add(changes);
        return true;
    }
}
