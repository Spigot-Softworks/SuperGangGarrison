namespace OpenGarrison.Core;

/// <summary>
/// Maps exported entity coordinates to runtime room-object top-left positions.
/// Garrison Builder stores sprite anchor coordinates (top-left when sprite origin is 0,0).
/// Legacy GameMaker instances also use top-left. Only opt-in center conversion is supported for tests.
/// </summary>
public static class CustomMapEntityPlacementAnchor
{
    /// <summary>
    /// Modern builder packages use the same top-left anchor as the editor; never treat as center.
    /// </summary>
    public static bool UsesCenterOrigin(IReadOnlyDictionary<string, string>? metadata)
    {
        _ = metadata;
        return false;
    }

    public static (float X, float Y) ToTopLeft(float x, float y, float width, float height, bool useCenterOrigin)
    {
        return useCenterOrigin
            ? (x - (width / 2f), y - (height / 2f))
            : (x, y);
    }
}
