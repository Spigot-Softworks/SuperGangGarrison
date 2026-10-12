#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

internal static class TeamFireLighting
{
    internal static bool HasTeam(PlayerTeam team) => team is PlayerTeam.Red or PlayerTeam.Blue;

    internal static Color ResolveLightColor(bool enabled, PlayerTeam team, Color original) => !enabled ? original : team switch
    {
        PlayerTeam.Red => new Color(255, 70, 50),
        PlayerTeam.Blue => new Color(70, 145, 255),
        _ => original,
    };

    internal static Color ResolveFlameColor(PlayerTeam team, float depth, bool outline)
    {
        if (team == PlayerTeam.Blue)
            return outline ? new Color(0, 80, 200) : depth < 0.75f ? new Color(20, 100, 220)
                : depth < 1.30f ? new Color(80, 150, 255) : depth < 2.10f ? new Color(150, 200, 255) : new Color(220, 240, 255);
        return outline ? new Color(180, 0, 0) : depth < 0.75f ? new Color(220, 20, 20)
            : depth < 1.30f ? new Color(255, 80, 80) : depth < 2.10f ? new Color(255, 150, 150) : new Color(255, 220, 220);
    }

    internal static Color RecolorSpritePixel(PlayerTeam team, Color pixel)
    {
        if (!HasTeam(team) || pixel.A == 0) return pixel;
        // Orange artwork has a full red channel even on its dark edges. Use its
        // warmth to retain the outer flame / bright core instead of tinting orange blue.
        var heat = Math.Clamp((pixel.G + pixel.B * 0.5f) / (pixel.A * 1.5f), 0f, 1f);
        var depth = heat < 0.2f ? 0.5f : heat < 0.45f ? 1f : heat < 0.75f ? 1.5f : 3f;
        var color = ResolveFlameColor(team, depth, false);
        var shade = Math.Max(pixel.R, Math.Max(pixel.G, pixel.B)) / 255f;
        return new Color((byte)(color.R * shade), (byte)(color.G * shade), (byte)(color.B * shade), pixel.A);
    }
}

public partial class Game1
{
    private readonly Dictionary<(LoadedSpriteFrame Frame, PlayerTeam Team), LoadedSpriteFrame> _teamFireFrames = new();
    internal bool UsesTeamFireColor(PlayerTeam team) => _world.Level.Lighting.IsActive && _world.Level.Lighting.FireTeamColor && TeamFireLighting.HasTeam(team);

    internal LoadedSpriteFrame GetTeamFireSpriteFrame(LoadedSpriteFrame frame, PlayerTeam team)
    {
        if (!UsesTeamFireColor(team)) return frame;
        if (_teamFireFrames.TryGetValue((frame, team), out var cached) && !cached.Texture.IsDisposed) return cached;
        var pixels = new Color[frame.Width * frame.Height];
        if (!frame.TryCopyPixelData(pixels))
            frame.Texture.GetData(0, frame.SourceRectangle, pixels, 0, pixels.Length);
        for (var i = 0; i < pixels.Length; i++) pixels[i] = TeamFireLighting.RecolorSpritePixel(team, pixels[i]);
        var texture = new Texture2D(frame.Texture.GraphicsDevice, frame.Width, frame.Height);
        texture.SetData(pixels);
        cached = new LoadedSpriteFrame(texture, OpaqueBounds: frame.OpaqueBounds,
            PixelSource: new LoadedSpriteFramePixelSource(pixels, frame.Width, frame.Height));
        _teamFireFrames[(frame, team)] = cached;
        return cached;
    }

    private bool TryDrawTeamFireSprite(string name, int frameIndex, float x, float y, Vector2 camera, PlayerTeam team, float rotation)
    {
        if (!UsesTeamFireColor(team)) return TryDrawSprite(name, frameIndex, x, y, camera, Color.White, rotation);
        var sprite = GetResolvedSprite(name);
        if (sprite is null || sprite.Frames.Count == 0) return false;
        var frame = GetTeamFireSpriteFrame(sprite.Frames[Math.Clamp(frameIndex, 0, sprite.Frames.Count - 1)], team);
        DrawSpriteFrameWithOptionalShadow(frame, new Vector2(x, y) - camera, Color.White, rotation, sprite.Origin.ToVector2(), Vector2.One);
        return true;
    }

    private void DisposeTeamFireSpriteFrames()
    {
        foreach (var frame in _teamFireFrames.Values) frame.Dispose();
        _teamFireFrames.Clear();
    }
}
