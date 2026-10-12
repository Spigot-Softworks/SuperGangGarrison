#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

/// <summary>
/// Animated images (GIFs, animated PNGs) in the builder: every frame is decoded and the
/// builder draws the current one, as the game does. GG2 "foreground" / "foreground_scale"
/// entities also show their image in the builder (top-left anchored, at xscale/yscale,
/// stepping animationspeed frames per 30 Hz tick) instead of a placeholder icon.
/// </summary>
public partial class Game1
{
    private sealed record GarrisonBuilderDecodedImage(
        TextureDecodeUtility.DecodedTextureData Still,
        List<(TextureDecodeUtility.DecodedTextureData Frame, int DelayMs)>? Frames);

    private sealed record GarrisonBuilderResourceAnimation(Texture2D[] Frames, int[] FrameEndsMs, int TotalMs);

    private readonly Dictionary<string, GarrisonBuilderResourceAnimation> _builderResourceAnimations = new(StringComparer.OrdinalIgnoreCase);

    private static GarrisonBuilderDecodedImage DecodeGarrisonBuilderImage(byte[] bytes)
    {
        var still = TextureDecodeUtility.DecodeTextureData(bytes, false);
        List<(TextureDecodeUtility.DecodedTextureData Frame, int DelayMs)>? frames = null;
        try
        {
            frames = TextureDecodeUtility.DecodeAnimationFrames(bytes);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // A broken animation still shows its first frame.
            frames = null;
        }

        return new GarrisonBuilderDecodedImage(still, frames is { Count: > 1 } ? frames : null);
    }

    private void StoreGarrisonBuilderResourceTexture(string resourceName, GarrisonBuilderDecodedImage image)
    {
        DisposeGarrisonBuilderResourceAnimation(resourceName);
        if (image.Frames is { } frames)
        {
            var textures = new Texture2D[frames.Count];
            var frameEnds = new int[frames.Count];
            var totalMs = 0;
            for (var index = 0; index < frames.Count; index += 1)
            {
                var (frame, delayMs) = frames[index];
                textures[index] = TextureDecodeUtility.CreateTexture(GraphicsDevice, frame.PixelData, frame.Width, frame.Height);
                totalMs += Math.Max(1, delayMs);
                frameEnds[index] = totalMs;
            }

            // The first frame stands for the image (sizes, previews); the rest belong to the animation.
            _builderResourceTextureCache[resourceName] = textures[0];
            _builderResourceAnimations[resourceName] = new GarrisonBuilderResourceAnimation(textures, frameEnds, Math.Max(1, totalMs));
            return;
        }

        _builderResourceTextureCache[resourceName] = TextureDecodeUtility.CreateTexture(
            GraphicsDevice,
            image.Still.PixelData,
            image.Still.Width,
            image.Still.Height);
    }

    /// <summary>
    /// The frame to draw now: the image itself when still, otherwise the current frame at the
    /// image's own timing, or at a fixed rate when one is given (0 holds the first frame).
    /// </summary>
    private Texture2D GetGarrisonBuilderAnimationFrame(string resourceName, Texture2D still, float? framesPerSecond = null)
    {
        if (_builderResourceAnimations.Count == 0
            || !_builderResourceAnimations.TryGetValue(resourceName, out var animation))
        {
            return still;
        }

        if (framesPerSecond is { } fps && float.IsFinite(fps))
        {
            if (fps <= 0f)
            {
                return animation.Frames[0];
            }

            var frame = (long)Math.Floor(_weatherClock.Elapsed.TotalSeconds * fps);
            return animation.Frames[(int)(frame % animation.Frames.Length)];
        }

        var timeMs = (int)(_weatherClock.Elapsed.TotalMilliseconds % animation.TotalMs);
        for (var index = 0; index < animation.Frames.Length; index += 1)
        {
            if (timeMs < animation.FrameEndsMs[index])
            {
                return animation.Frames[index];
            }
        }

        return animation.Frames[^1];
    }

    /// <summary>Disposes an animation's extra frames; the first frame lives in the texture cache.</summary>
    private void DisposeGarrisonBuilderResourceAnimation(string resourceName)
    {
        if (!_builderResourceAnimations.Remove(resourceName, out var animation))
        {
            return;
        }

        for (var index = 1; index < animation.Frames.Length; index += 1)
        {
            animation.Frames[index].Dispose();
        }
    }

    private void ClearGarrisonBuilderResourceAnimations()
    {
        foreach (var animation in _builderResourceAnimations.Values)
        {
            for (var index = 1; index < animation.Frames.Length; index += 1)
            {
                animation.Frames[index].Dispose();
            }
        }

        _builderResourceAnimations.Clear();
    }

    private static bool IsGarrisonBuilderGg2ForegroundEntityType(string type)
    {
        return type.Equals("foreground", StringComparison.OrdinalIgnoreCase)
            || type.Equals("foreground_scale", StringComparison.OrdinalIgnoreCase);
    }

    private static float ParseGarrisonBuilderEntityFloat(CustomMapBuilderEntity entity, string key, float fallback)
    {
        return entity.Properties.TryGetValue(key, out var value)
            && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            && float.IsFinite(parsed)
            ? parsed
            : fallback;
    }

    /// <summary>
    /// GG2 foreground entities: their resource image at the entity's top-left corner, scaled
    /// and animated like the game draws them. Falls back to the catalog icon until the image
    /// has loaded (or when it has none).
    /// </summary>
    private bool TryDrawGarrisonBuilderGg2ForegroundEntity(
        CustomMapBuilderEntityDefinition definition,
        CustomMapBuilderEntity entity,
        Color tint)
    {
        if (!IsGarrisonBuilderGg2ForegroundEntityType(definition.Type)
            || !entity.Properties.TryGetValue("resource", out var rawResource)
            || string.IsNullOrWhiteSpace(rawResource))
        {
            return false;
        }

        var resourceName = rawResource.Trim();
        if (!_builderResourceTextureCache.TryGetValue(resourceName, out var still))
        {
            return false;
        }

        float xScale;
        float yScale;
        if (definition.Type.Equals("foreground_scale", StringComparison.OrdinalIgnoreCase)
            && entity.Properties.ContainsKey("scale"))
        {
            xScale = yScale = NormalizeGarrisonBuilderEntityScale(ParseGarrisonBuilderEntityFloat(entity, "scale", 1f));
        }
        else
        {
            xScale = NormalizeGarrisonBuilderEntityScale(entity.XScale);
            yScale = NormalizeGarrisonBuilderEntityScale(entity.YScale);
        }

        var framesPerSecond = MathF.Max(0f, ParseGarrisonBuilderEntityFloat(entity, "animationspeed", 0f))
            * Gg2ForegroundFramesPerTick;
        var texture = GetGarrisonBuilderAnimationFrame(resourceName, still, framesPerSecond);
        var screen = _builderUseModernUi
            ? BuilderWorldToScreen(new Vector2(entity.X, entity.Y))
            : new Vector2(entity.X - _builderCamera.X, entity.Y - _builderCamera.Y);
        var visualScale = _builderUseModernUi ? GetGarrisonBuilderMapVisualScale() : 1f;
        var opacity = Math.Clamp(ParseGarrisonBuilderEntityFloat(entity, "opacity", 1f), 0.15f, 1f);
        _spriteBatch.Draw(
            texture,
            screen,
            null,
            GetGarrisonBuilderEntityDrawTint(definition, entity, tint) * opacity,
            0f,
            Vector2.Zero,
            new Vector2(xScale * visualScale, yScale * visualScale),
            SpriteEffects.None,
            0f);
        return true;
    }

    /// <summary>GG2 steps foreground animations by animationspeed frames per 30 Hz tick.</summary>
    private const float Gg2ForegroundFramesPerTick = 30f;
}
