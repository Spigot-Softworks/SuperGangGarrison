using Microsoft.Xna.Framework;

namespace OpenGarrison.Client;

internal readonly record struct ConquestPointHudLayout(int Count, int Columns, int Rows, float Scale, Vector2 Origin, Rectangle Bounds)
{
    internal Vector2 GetPosition(int index)
    {
        var row = index / Columns;
        var rowCount = Math.Min(Columns, Count - row * Columns);
        return Origin + new Vector2((index % Columns - (rowCount - 1) / 2f) * 60f * Scale,
            -(Rows - 1 - row) * 60f * Scale);
    }

    internal static ConquestPointHudLayout Create(int count, Vector2 origin, int width, int height, float requestedScale)
    {
        if (count <= 0) return new(0, 1, 0, requestedScale, origin, Rectangle.Empty);
        var availableWidth = Math.Max(1f, 2f * (Math.Min(origin.X, width - origin.X) - 16f));
        var availableHeight = Math.Max(1f, Math.Min(180f, Math.Min(origin.Y - 16f, height - 32f)));
        (int Columns, int Rows, float Width, float Height) Measure(float scale)
        {
            var columns = Math.Min(count, Math.Max(1, (int)MathF.Floor((availableWidth - 72f * scale) / (60f * scale)) + 1));
            var rows = (count - 1) / columns + 1;
            return (columns, rows, ((columns - 1) * 60f + 72f) * scale, ((rows - 1) * 60f + 64f) * scale);
        }
        var scale = requestedScale;
        var measured = Measure(scale);
        if (measured.Width > availableWidth || measured.Height > availableHeight)
        {
            var low = 0f;
            var high = scale;
            for (var step = 0; step < 32; step++)
            {
                var candidate = (low + high) / 2f;
                var size = Measure(candidate);
                if (size.Width <= availableWidth && size.Height <= availableHeight) low = candidate;
                else high = candidate;
            }
            scale = low;
            measured = Measure(scale);
        }
        var bounds = new Rectangle((int)MathF.Floor(origin.X - measured.Width / 2f),
            (int)MathF.Floor(origin.Y - (measured.Rows - 1) * 60f * scale - 24f * scale),
            (int)MathF.Ceiling(measured.Width), (int)MathF.Ceiling(measured.Height));
        return new(count, measured.Columns, measured.Rows, scale, origin, bounds);
    }
}
