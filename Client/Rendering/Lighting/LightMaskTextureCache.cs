#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace OpenGarrison.Client;

/// <summary>
/// Turns a map image resource into a light shape ("mask") for a placed light. The light
/// passes where the image is solid and is blocked where it is transparent, so a black (or
/// any colour) shape on a transparent background works; an image with no transparency at
/// all uses its brightness instead (white = lit, black = dark). Soft edges and partial
/// alpha give partial light. Textures are built once per resource's bytes and cached.
/// </summary>
internal sealed class LightMaskTextureCache : IDisposable
{
    private const int MaxCachedMasks = 64;

    /// <summary>Masks unused for this long may be evicted once the cache is full.</summary>
    private const long EvictAfterMs = 5000;

    // Keyed by image content, not by array: the same image often arrives in a new byte
    // array (a resource re-read after a save or quick test), and keying by reference made a
    // new texture every frame and then disposed them all, dropping masks mid-use.
    private readonly Dictionary<string, (Texture2D? Texture, long LastUsedMs)> _textures = new(StringComparer.Ordinal);
    private readonly ConditionalWeakTable<byte[], string> _keys = new();
    private readonly bool _alphaShape;

    /// <param name="alphaShape">
    /// Build shapes from alpha alone (solid = white), for jungle foliage that keeps lights
    /// off itself, instead of the light-mask rule (brightness for opaque images).
    /// </param>
    public LightMaskTextureCache(bool alphaShape = false)
    {
        _alphaShape = alphaShape;
    }

    public Texture2D? Get(GraphicsDevice device, byte[] bytes)
    {
        if (!_keys.TryGetValue(bytes, out var key))
        {
            key = Convert.ToHexString(SHA256.HashData(bytes));
            _keys.AddOrUpdate(bytes, key);
        }

        var now = Environment.TickCount64;
        if (_textures.TryGetValue(key, out var cached) && cached.Texture is not { IsDisposed: true })
        {
            _textures[key] = (cached.Texture, now);
            return cached.Texture;
        }

        if (_textures.Count >= MaxCachedMasks)
        {
            EvictStale(now);
        }

        Texture2D? texture = null;
        try
        {
            var decoded = TextureDecodeUtility.DecodeTextureData(bytes, false);
            var pixels = _alphaShape ? BuildAlphaShapePixels(decoded.PixelData) : BuildMaskPixels(decoded.PixelData);
            texture = TextureDecodeUtility.CreateTexture(device, pixels, decoded.Width, decoded.Height);
        }
        catch
        {
            // Not a readable image: the light falls back to its normal shape.
        }

        _textures[key] = (texture, now);
        return texture;
    }

    /// <summary>Disposes masks not drawn recently; ones in use this frame are never touched.</summary>
    private void EvictStale(long now)
    {
        var stale = new List<string>();
        foreach (var (key, entry) in _textures)
        {
            if (now - entry.LastUsedMs > EvictAfterMs)
            {
                stale.Add(key);
            }
        }

        foreach (var key in stale)
        {
            _textures[key].Texture?.Dispose();
            _textures.Remove(key);
        }
    }

    /// <summary>
    /// Premultiplied image pixels to light-map pixels: white scaled by how much light passes
    /// (alpha when the image has transparency, else brightness), in premultiplied form so
    /// it adds cleanly into the light map like the built-in light shapes.
    /// </summary>
    internal static Color[] BuildMaskPixels(Color[] premultiplied)
    {
        var hasTransparency = false;
        foreach (var pixel in premultiplied)
        {
            if (pixel.A < 250)
            {
                hasTransparency = true;
                break;
            }
        }

        var result = new Color[premultiplied.Length];
        for (var index = 0; index < premultiplied.Length; index += 1)
        {
            var pixel = premultiplied[index];
            var value = hasTransparency
                ? pixel.A
                : (byte)Math.Clamp((int)MathF.Round((pixel.R * 0.299f) + (pixel.G * 0.587f) + (pixel.B * 0.114f)), 0, 255);
            result[index] = new Color(value, value, value, value);
        }

        return result;
    }

    /// <summary>White where the image is solid, by its alpha (premultiplied).</summary>
    internal static Color[] BuildAlphaShapePixels(Color[] premultiplied)
    {
        var result = new Color[premultiplied.Length];
        for (var index = 0; index < premultiplied.Length; index += 1)
        {
            var alpha = premultiplied[index].A;
            result[index] = new Color(alpha, alpha, alpha, alpha);
        }

        return result;
    }

    public void Dispose()
    {
        foreach (var entry in _textures.Values)
        {
            entry.Texture?.Dispose();
        }

        _textures.Clear();
        _keys.Clear();
    }
}
