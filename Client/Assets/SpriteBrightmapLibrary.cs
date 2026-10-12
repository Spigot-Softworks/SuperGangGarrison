#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

/// <summary>
/// Brightmaps for sprite frames: greyscale masks (GZDoom style) saying how much of each
/// pixel keeps its own colour in the dark. Each one is baked once, when its sprite is first
/// used, into a "glow" image: the frame multiplied by its brightmap. The renderer adds that
/// glow back over the lit character, scaled by how dark it is where the character stands.
/// </summary>
/// <remarks>
/// <para>Brightmaps are listed in <c>Content/Brightmaps/brightmaps.json</c>. Each entry
/// names the sprites it applies to (both team versions, usually) and one image per frame;
/// a missing or null image means that frame has none. With <c>reuseForMatchingFrames</c>,
/// a frame without its own image borrows one from a frame of the same sprite with exactly the
/// same silhouette, as it is or moved by a few pixels (a torso bobbing up on the run, say).</para>
/// <para>A brightmap may be smaller than its frame by a whole factor (the stock characters
/// store each art pixel as a 2x2 block, so their brightmaps are drawn at half size); it is
/// scaled up without filtering.</para>
/// </remarks>
internal sealed class SpriteBrightmapLibrary : IDisposable
{
    public const string ManifestFileName = "brightmaps.json";

    private readonly Dictionary<string, BrightmapEntry> _entriesBySprite = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, LoadedGameMakerSprite> _bakedSprites = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<(Texture2D Texture, Rectangle Source), Texture2D> _glowByFrame = new();
    private readonly List<Texture2D> _ownedTextures = new();
    private readonly Dictionary<string, Color[]?> _imageCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (int Width, int Height)> _imageSizes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Action<string>? _log;
    private string _root = string.Empty;
    private bool _loaded;
    private bool _disposed;

    public SpriteBrightmapLibrary(Action<string>? log = null)
    {
        _log = log;
    }

    /// <summary>True when the manifest lists at least one brightmap.</summary>
    public bool HasAny
    {
        get
        {
            EnsureLoaded();
            return _entriesBySprite.Count > 0;
        }
    }

    /// <summary>
    /// Bakes a sprite's brightmaps the first time it is resolved. Cheap for every other call
    /// (one dictionary lookup), so it can sit on the sprite lookup path.
    /// </summary>
    public void Observe(GraphicsDevice device, string spriteName, LoadedGameMakerSprite sprite)
    {
        if (_disposed)
        {
            return;
        }

        EnsureLoaded();
        if (!_entriesBySprite.TryGetValue(spriteName, out var entry)
            || (_bakedSprites.TryGetValue(spriteName, out var baked) && ReferenceEquals(baked, sprite)))
        {
            return;
        }

        // Baked once per loaded sprite; a reloaded sprite (new textures) is baked again.
        _bakedSprites[spriteName] = sprite;

        try
        {
            Bake(device, spriteName, sprite, entry);
        }
        catch (Exception ex)
        {
            _log?.Invoke($"brightmaps for {spriteName} failed ({ex.GetType().Name}: {ex.Message})");
        }
    }

    /// <summary>The baked glow image for a frame (same size as the frame), if it has a brightmap.</summary>
    public bool TryGetGlow(Texture2D texture, Rectangle? source, out Texture2D glow)
    {
        var key = (texture, source ?? new Rectangle(0, 0, texture.Width, texture.Height));
        if (_glowByFrame.TryGetValue(key, out var found))
        {
            glow = found;
            return true;
        }

        glow = null!;
        return false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var texture in _ownedTextures)
        {
            texture.Dispose();
        }

        _ownedTextures.Clear();
        _glowByFrame.Clear();
    }

    private void EnsureLoaded()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        if (OperatingSystem.IsBrowser())
        {
            return;
        }

        _root = ContentRoot.GetPath("Brightmaps");
        var manifestPath = Path.Combine(_root, ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath), new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            });
            if (!document.RootElement.TryGetProperty("brightmaps", out var list) || list.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            foreach (var item in list.EnumerateArray())
            {
                var entry = BrightmapEntry.Parse(item);
                if (entry is null)
                {
                    continue;
                }

                foreach (var sprite in entry.Sprites)
                {
                    _entriesBySprite[sprite] = entry;
                }
            }
        }
        catch (Exception ex)
        {
            _log?.Invoke($"brightmap list unreadable ({ex.GetType().Name}: {ex.Message})");
        }
    }

    private void Bake(GraphicsDevice device, string spriteName, LoadedGameMakerSprite sprite, BrightmapEntry entry)
    {
        var frames = sprite.Frames;
        var framePixels = new Color[frames.Count][];
        for (var index = 0; index < frames.Count; index += 1)
        {
            framePixels[index] = ReadFrame(frames[index]);
        }

        // Which brightmap image each frame uses: its own, or (optionally) one borrowed from a
        // frame with the same silhouette, possibly moved a few pixels (a torso bobbing on the run).
        var images = new (string Image, int ShiftX, int ShiftY)?[frames.Count];
        for (var index = 0; index < frames.Count; index += 1)
        {
            images[index] = index < entry.Frames.Count && entry.Frames[index] is { } own ? (own, 0, 0) : null;
        }

        if (entry.ReuseForMatchingFrames)
        {
            for (var index = 0; index < frames.Count; index += 1)
            {
                if (images[index] is not null)
                {
                    continue;
                }

                for (var donor = 0; donor < frames.Count && donor < entry.Frames.Count && images[index] is null; donor += 1)
                {
                    if (entry.Frames[donor] is { } image
                        && frames[donor].Width == frames[index].Width
                        && frames[donor].Height == frames[index].Height
                        && TryFindSilhouetteShift(framePixels[donor], framePixels[index], frames[index].Width, frames[index].Height, out var shiftX, out var shiftY))
                    {
                        images[index] = (image, shiftX, shiftY);
                    }
                }
            }
        }

        var baked = 0;
        for (var index = 0; index < frames.Count; index += 1)
        {
            if (images[index] is not { } use)
            {
                continue;
            }

            var image = use.Image;

            var frame = frames[index];
            var mask = LoadImage(image, out var maskWidth, out var maskHeight);
            if (mask is null)
            {
                continue;
            }

            if (!TryGetUpscale(frame.Width, frame.Height, maskWidth, maskHeight, out var upscale))
            {
                _log?.Invoke($"brightmap {image} is {maskWidth}x{maskHeight}, which does not fit {spriteName} frame {index} ({frame.Width}x{frame.Height})");
                continue;
            }

            var glow = BakeGlow(framePixels[index], frame.Width, frame.Height, mask, maskWidth, maskHeight, upscale, use.ShiftX, use.ShiftY);
            if (glow is null)
            {
                continue; // an all-black brightmap adds nothing
            }

            var texture = new Texture2D(device, frame.Width, frame.Height, false, SurfaceFormat.Color);
            texture.SetData(glow);
            _ownedTextures.Add(texture);
            var source = frame.SourceRectangle ?? new Rectangle(0, 0, frame.Texture.Width, frame.Texture.Height);
            _glowByFrame[(frame.Texture, source)] = texture;
            baked += 1;
        }

        if (baked == 0)
        {
            _log?.Invoke($"no brightmaps applied to {spriteName}");
        }
    }

    /// <summary>
    /// The glow image: each pixel of the frame multiplied by its brightmap value, per channel
    /// (keeping the frame's alpha; transparent where the frame is). The brightmap is scaled up
    /// by <paramref name="upscale"/> and moved by the shift (in frame pixels). Null when nothing
    /// would glow.
    /// </summary>
    internal static Color[]? BakeGlow(
        Color[] frame,
        int width,
        int height,
        Color[] mask,
        int maskWidth,
        int maskHeight,
        int upscale,
        int shiftX = 0,
        int shiftY = 0)
    {
        var glow = new Color[width * height];
        var any = false;
        for (var y = 0; y < height; y += 1)
        {
            var maskY = y - shiftY;
            for (var x = 0; x < width; x += 1)
            {
                var pixel = frame[(y * width) + x];
                var maskX = x - shiftX;
                if (pixel.A == 0 || maskX < 0 || maskY < 0 || maskX >= maskWidth * upscale || maskY >= maskHeight * upscale)
                {
                    continue;
                }

                // Both images are premultiplied, so a transparent brightmap pixel is black.
                var bright = mask[((maskY / upscale) * maskWidth) + (maskX / upscale)];
                var r = pixel.R * (bright.R / 255f);
                var g = pixel.G * (bright.G / 255f);
                var b = pixel.B * (bright.B / 255f);
                var value = new Color((byte)MathF.Round(r), (byte)MathF.Round(g), (byte)MathF.Round(b), pixel.A);
                glow[(y * width) + x] = value;
                any |= value.R > 0 || value.G > 0 || value.B > 0;
            }
        }

        return any ? glow : null;
    }

    /// <summary>The whole factor a brightmap is scaled up by to cover its frame (1 when the same size).</summary>
    internal static bool TryGetUpscale(int frameWidth, int frameHeight, int maskWidth, int maskHeight, out int upscale)
    {
        upscale = 0;
        if (maskWidth <= 0 || maskHeight <= 0 || frameWidth % maskWidth != 0 || frameHeight % maskHeight != 0)
        {
            return false;
        }

        upscale = frameWidth / maskWidth;
        return upscale >= 1 && upscale == frameHeight / maskHeight;
    }

    /// <summary>Furthest a borrowed brightmap is moved to fit a frame, in frame pixels.</summary>
    private const int MaxSilhouetteShift = 8;

    /// <summary>
    /// Whether <paramref name="frame"/> has exactly the donor's silhouette, as it is or moved
    /// by up to <see cref="MaxSilhouetteShift"/> pixels (smallest move first).
    /// </summary>
    internal static bool TryFindSilhouetteShift(Color[] donor, Color[] frame, int width, int height, out int shiftX, out int shiftY)
    {
        shiftX = 0;
        shiftY = 0;
        if (donor.Length != width * height || frame.Length != width * height)
        {
            return false;
        }

        // A shift must preserve the whole silhouette, rather than hiding part of
        // a larger donor outside the frame and accepting the remaining fragment.
        if (donor.Count(pixel => pixel.A > 0) != frame.Count(pixel => pixel.A > 0))
            return false;

        for (var distance = 0; distance <= MaxSilhouetteShift; distance += 1)
        {
            for (var dy = -distance; dy <= distance; dy += 1)
            {
                for (var dx = -distance; dx <= distance; dx += 1)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != distance || !MatchesShifted(donor, frame, width, height, dx, dy))
                    {
                        continue;
                    }

                    shiftX = dx;
                    shiftY = dy;
                    return true;
                }
            }
        }

        return false;
    }

    private static bool MatchesShifted(Color[] donor, Color[] frame, int width, int height, int dx, int dy)
    {
        for (var y = 0; y < height; y += 1)
        {
            var sourceY = y - dy;
            for (var x = 0; x < width; x += 1)
            {
                var sourceX = x - dx;
                var donorSolid = sourceX >= 0 && sourceY >= 0 && sourceX < width && sourceY < height
                    && donor[(sourceY * width) + sourceX].A > 0;
                if (donorSolid != (frame[(y * width) + x].A > 0))
                {
                    return false;
                }
            }
        }

        // A blank frame matches nothing.
        return Array.Exists(frame, pixel => pixel.A > 0);
    }

    private static Color[] ReadFrame(LoadedSpriteFrame frame)
    {
        var pixels = new Color[frame.Width * frame.Height];
        if (frame.TryCopyPixelData(pixels))
        {
            return pixels;
        }

        if (frame.SourceRectangle is { } source)
        {
            frame.Texture.GetData(0, source, pixels, 0, pixels.Length);
        }
        else
        {
            frame.Texture.GetData(pixels);
        }

        return pixels;
    }

    private Color[]? LoadImage(string relativePath, out int width, out int height)
    {
        if (_imageCache.TryGetValue(relativePath, out var cached))
        {
            (width, height) = cached is null ? (0, 0) : _imageSizes[relativePath];
            return cached;
        }

        width = 0;
        height = 0;
        Color[]? pixels = null;
        var path = Path.GetFullPath(Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(Path.GetFullPath(_root), StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
        {
            _log?.Invoke($"brightmap image missing: {relativePath}");
        }
        else
        {
            try
            {
                var decoded = TextureDecodeUtility.DecodeTextureData(File.ReadAllBytes(path), applyLegacyChromaKey: false);
                pixels = decoded.PixelData;
                width = decoded.Width;
                height = decoded.Height;
            }
            catch (Exception ex)
            {
                _log?.Invoke($"brightmap image unreadable: {relativePath} ({ex.GetType().Name})");
                pixels = null;
            }
        }

        _imageCache[relativePath] = pixels;
        if (pixels is not null)
        {
            _imageSizes[relativePath] = (width, height);
        }

        return pixels;
    }

    private sealed record BrightmapEntry(IReadOnlyList<string> Sprites, IReadOnlyList<string?> Frames, bool ReuseForMatchingFrames)
    {
        public static BrightmapEntry? Parse(JsonElement item)
        {
            if (item.ValueKind != JsonValueKind.Object
                || !item.TryGetProperty("sprites", out var spritesElement)
                || !item.TryGetProperty("frames", out var framesElement)
                || spritesElement.ValueKind != JsonValueKind.Array
                || framesElement.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var sprites = new List<string>();
            foreach (var sprite in spritesElement.EnumerateArray())
            {
                if (sprite.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(sprite.GetString()))
                {
                    sprites.Add(sprite.GetString()!.Trim());
                }
            }

            var frames = new List<string?>();
            foreach (var frame in framesElement.EnumerateArray())
            {
                frames.Add(frame.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(frame.GetString())
                    ? frame.GetString()!.Trim()
                    : null);
            }

            var reuse = item.TryGetProperty("reuseForMatchingFrames", out var reuseElement)
                && reuseElement.ValueKind == JsonValueKind.True;
            return sprites.Count == 0 || frames.Count == 0 ? null : new BrightmapEntry(sprites, frames, reuse);
        }
    }
}
