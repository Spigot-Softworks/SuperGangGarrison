using Microsoft.Xna.Framework;

namespace OpenGarrison.Client;

internal static class WeaponHudIconLayout
{
    // Source HUD coordinates relative to the plaque anchor. Leave the right side
    // for the ammo count and the bottom for the reload bar.
    internal static readonly Vector2 TopLeft = new(-52.8f, -14.4f);
    internal static readonly Vector2 Size = new(73.8f, 16.4f);

    public static (Vector2 Origin, Vector2 Offset, float Scale) Fit(LoadedSpriteFrame frame)
    {
        var bounds = frame.OpaqueBounds ?? new Rectangle(0, 0, frame.Width, frame.Height);
        var origin = new Vector2(bounds.X + bounds.Width / 2f, bounds.Y + bounds.Height / 2f);
        var scale = MathF.Min(2.4f, MathF.Min(Size.X / Math.Max(1, bounds.Width), Size.Y / Math.Max(1, bounds.Height)));
        return (origin, TopLeft + Size / 2f, scale);
    }
}
