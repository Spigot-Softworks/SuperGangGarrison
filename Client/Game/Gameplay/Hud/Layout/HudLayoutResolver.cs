#nullable enable

using Microsoft.Xna.Framework;

namespace OpenGarrison.Client;

internal static class HudLayoutResolver
{
    public static Vector2 ResolveOrigin(HudAnchor anchor, Vector2 offset, int viewportWidth, int viewportHeight)
    {
        return GetAnchorPoint(anchor, viewportWidth, viewportHeight) + offset;
    }

    public static Vector2 OffsetFromOrigin(HudAnchor anchor, Vector2 origin, int viewportWidth, int viewportHeight)
    {
        return origin - GetAnchorPoint(anchor, viewportWidth, viewportHeight);
    }

    public static Vector2 GetAnchorPoint(HudAnchor anchor, int viewportWidth, int viewportHeight)
    {
        return anchor switch
        {
            HudAnchor.TopLeft => Vector2.Zero,
            HudAnchor.TopCenter => new Vector2(viewportWidth / 2f, 0f),
            HudAnchor.TopRight => new Vector2(viewportWidth, 0f),
            HudAnchor.CenterLeft => new Vector2(0f, viewportHeight / 2f),
            HudAnchor.Center => new Vector2(viewportWidth / 2f, viewportHeight / 2f),
            HudAnchor.CenterRight => new Vector2(viewportWidth, viewportHeight / 2f),
            HudAnchor.BottomLeft => new Vector2(0f, viewportHeight),
            HudAnchor.BottomCenter => new Vector2(viewportWidth / 2f, viewportHeight),
            HudAnchor.BottomRight => new Vector2(viewportWidth, viewportHeight),
            _ => Vector2.Zero,
        };
    }

    public static Vector2 ResolveLegacySourcePoint(float sourceX, float sourceY, int viewportWidth, int viewportHeight)
    {
        return new Vector2(viewportWidth - 800f + sourceX, viewportHeight - 600f + sourceY);
    }
}
