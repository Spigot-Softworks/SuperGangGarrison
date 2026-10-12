#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

internal static class BloodLighting
{
    internal static Color ResolveColor(MapLighting lighting, PlayerTeam team, Color original)
    {
        if (!lighting.IsActive || (!lighting.BloodTeamColor && lighting.BloodColor is null)) return original;
        var color = lighting.BloodTeamColor
            ? team == PlayerTeam.Blue ? new Color(70, 145, 255) : new Color(255, 70, 50)
            : new Color(lighting.BloodColor!.Value.R, lighting.BloodColor.Value.G, lighting.BloodColor.Value.B);
        // Retain the original fill/outline shades and premultiplied alpha.
        var shade = Math.Max(original.R, Math.Max(original.G, original.B)) / 255f;
        return new Color((byte)(color.R * shade), (byte)(color.G * shade), (byte)(color.B * shade), original.A);
    }

    internal static void MakeNeutralPixels(Color[] pixels)
    {
        for (var index = 0; index < pixels.Length; index++)
        {
            var pixel = pixels[index];
            var shade = Math.Max(pixel.R, Math.Max(pixel.G, pixel.B));
            pixels[index] = new Color(shade, shade, shade, pixel.A);
        }
    }
}

public partial class Game1
{
    private readonly Dictionary<LoadedSpriteFrame, LoadedSpriteFrame> _bloodNeutralFrames = new(ReferenceEqualityComparer.Instance);

    internal LoadedSpriteFrame GetBloodSpriteFrame(LoadedSpriteFrame frame)
    {
        var lighting = _world.Level.Lighting;
        if (!lighting.IsActive || (!lighting.BloodTeamColor && lighting.BloodColor is null)) return frame;
        if (_bloodNeutralFrames.TryGetValue(frame, out var cached) && !cached.Texture.IsDisposed) return cached;
        var pixels = new Color[frame.Width * frame.Height];
        if (!frame.TryCopyPixelData(pixels))
            frame.Texture.GetData(0, frame.SourceRectangle, pixels, 0, pixels.Length);
        BloodLighting.MakeNeutralPixels(pixels);
        var texture = new Texture2D(frame.Texture.GraphicsDevice, frame.Width, frame.Height);
        texture.SetData(pixels);
        cached = new LoadedSpriteFrame(texture, OpaqueBounds: frame.OpaqueBounds,
            PixelSource: new LoadedSpriteFramePixelSource(pixels, frame.Width, frame.Height));
        _bloodNeutralFrames[frame] = cached;
        return cached;
    }

    private void DisposeBloodSpriteFrames()
    {
        foreach (var frame in _bloodNeutralFrames.Values) frame.Dispose();
        _bloodNeutralFrames.Clear();
    }
}
