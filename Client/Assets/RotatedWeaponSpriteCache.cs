#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenGarrison.Client;

/// <summary>
/// Loads and caches the pre-baked rotation PNG frames produced by the WeaponRotationBaker tool.
/// Each sprite has 24 baked angles covering –90° to +82.5° (7.5° step) at 0.5× source scale.
/// The caller should draw the returned frame at 2× player scale with rotation = 0.
/// </summary>
public sealed class RotatedWeaponSpriteCache : IDisposable
{
    private const int AngleCount = 24;
    private const float AngleStartDeg = -90f;
    private const float AngleStepDeg = 180f / AngleCount; // = 7.5°

    private readonly GraphicsDevice _graphicsDevice;
    private readonly string _rotatedSpritesRoot;

    // null entry = already attempted & sprite has no baked data
    private readonly Dictionary<string, BakedSprite?> _sprites = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public RotatedWeaponSpriteCache(GraphicsDevice graphicsDevice, string rotatedSpritesRoot)
    {
        _graphicsDevice = graphicsDevice;
        _rotatedSpritesRoot = rotatedSpritesRoot;
    }

    /// <summary>
    /// Tries to get the pre-baked frame and origin for the given sprite, frame, and rotation.
    /// Returns false if no baked data exists for this sprite (caller should fall back to original).
    /// </summary>
    /// <param name="spriteName">Sprite name (e.g. "RocketlauncherS")</param>
    /// <param name="frameIndex">Animation frame index (clamped automatically).</param>
    /// <param name="rotationRadians">
    ///   Runtime rotation value in radians. For left-facing weapons this already has +π applied
    ///   by <c>GetWeaponRotationFromAim</c>, so it sits in the baked [–π/2, +π/2] range.
    /// </param>
    /// <param name="frame">Pre-baked frame (D×D canvas at 0.5× source scale).</param>
    /// <param name="origin">Origin within the baked canvas to pass as SpriteBatch origin.</param>
    public bool TryGetBakedFrame(
        string spriteName,
        int frameIndex,
        float rotationRadians,
        out LoadedSpriteFrame frame,
        out Vector2 origin)
    {
        frame = null!;
        origin = default;

        if (_disposed)
            return false;

        if (!_sprites.TryGetValue(spriteName, out var cached))
        {
            cached = TryLoad(spriteName);
            _sprites[spriteName] = cached;
        }

        if (cached is null)
            return false;

        var fi = Math.Clamp(frameIndex, 0, cached.Frames.Length - 1);

        // Snap to nearest baked angle
        var angleDeg = rotationRadians * (180f / MathF.PI);
        var rawIndex = (angleDeg - AngleStartDeg) / AngleStepDeg;
        var angleIndex = Math.Clamp((int)MathF.Round(rawIndex), 0, AngleCount - 1);

        var bakedFrame = cached.Frames[fi];
        frame = bakedFrame.Angles[angleIndex].Frame;
        origin = bakedFrame.Angles[angleIndex].Origin;
        return true;
    }

    private BakedSprite? TryLoad(string spriteName)
    {
        var spriteDir = Path.Combine(_rotatedSpritesRoot, spriteName);
        var manifestPath = Path.Combine(spriteDir, "manifest.json");
        if (!File.Exists(manifestPath))
            return null;

        SpriteManifest manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<SpriteManifest>(
                File.ReadAllText(manifestPath),
                JsonOptions)!;
        }
        catch
        {
            return null;
        }

        if (manifest?.Frames is null || manifest.Frames.Count == 0)
            return null;

        var frames = new BakedFrame[manifest.Frames.Count];
        for (var fi = 0; fi < manifest.Frames.Count; fi++)
        {
            var frameManifest = manifest.Frames[fi];
            var D = frameManifest.CanvasSize;

            // Each animation frame is stored as a horizontal strip: AngleCount columns of D×D.
            var pngPath = Path.Combine(spriteDir, $"frame{fi:D2}.png");
            if (!File.Exists(pngPath))
                return null; // incomplete bake — skip this sprite entirely

            Texture2D sheet;
            using (var stream = File.OpenRead(pngPath))
                sheet = Texture2D.FromStream(_graphicsDevice, stream);

            var angles = new BakedAngle[AngleCount];
            for (var ai = 0; ai < AngleCount; ai++)
            {
                var ao = frameManifest.Angles is not null && ai < frameManifest.Angles.Count
                    ? frameManifest.Angles[ai]
                    : new AngleOrigin { OriginX = D / 2f, OriginY = D / 2f };

                // Each angle lives in column ai of the strip; OwnsTexture=false so only
                // the BakedFrame disposes the shared sheet texture.
                var srcRect = new Rectangle(ai * D, 0, D, D);
                angles[ai] = new BakedAngle(
                    new LoadedSpriteFrame(sheet, SourceRectangle: srcRect, OwnsTexture: false),
                    new Vector2(ao.OriginX, ao.OriginY));
            }

            frames[fi] = new BakedFrame(sheet, angles);
        }

        return new BakedSprite(frames);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        foreach (var sprite in _sprites.Values)
        {
            if (sprite is null)
                continue;
            foreach (var frame in sprite.Frames)
                frame.Dispose();
        }

        _sprites.Clear();
    }

    // ── JSON options ──────────────────────────────────────────────────────────
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    // ── Internal data types ───────────────────────────────────────────────────
    private sealed class BakedSprite(BakedFrame[] frames)
    {
        public BakedFrame[] Frames { get; } = frames;
    }

    private sealed class BakedFrame
    {
        private readonly Microsoft.Xna.Framework.Graphics.Texture2D _sheet;
        public BakedAngle[] Angles { get; }

        public BakedFrame(Microsoft.Xna.Framework.Graphics.Texture2D sheet, BakedAngle[] angles)
        {
            _sheet = sheet;
            Angles = angles;
        }

        public void Dispose() => _sheet.Dispose();
    }

    private sealed class BakedAngle(LoadedSpriteFrame frame, Vector2 origin)
    {
        public LoadedSpriteFrame Frame { get; } = frame;
        public Vector2 Origin { get; } = origin;
    }

    // ── Manifest deserialization types ────────────────────────────────────────
    private sealed class SpriteManifest
    {
        [JsonPropertyName("frameCount")] public int FrameCount { get; init; }
        [JsonPropertyName("frames")] public List<FrameManifest>? Frames { get; init; }
    }

    private sealed class FrameManifest
    {
        [JsonPropertyName("canvasSize")] public int CanvasSize { get; init; }
        [JsonPropertyName("angles")] public List<AngleOrigin>? Angles { get; init; }
    }

    private sealed class AngleOrigin
    {
        [JsonPropertyName("originX")] public float OriginX { get; init; }
        [JsonPropertyName("originY")] public float OriginY { get; init; }
    }
}
