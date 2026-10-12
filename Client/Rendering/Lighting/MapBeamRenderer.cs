#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

/// <summary>A beam after clipping at walls and players, ready to draw.</summary>
/// <param name="Length">Clipped length in world pixels.</param>
/// <param name="Hit">True when something (wall or player) stopped the beam: draw the impact.</param>
internal readonly record struct ResolvedMapBeam(MapLightBeam Beam, float Length, bool Hit)
{
    public Vector2 Origin => new(Beam.X, Beam.Y);

    public Vector2 Direction
    {
        get
        {
            var (x, y) = MapLightMetadata.GetDirectionVector(Beam.Direction);
            return new Vector2(x, y);
        }
    }

    public Vector2 End => Origin + (Direction * Length);

    /// <summary>Width of a searchlight shaft at its far end.</summary>
    public float EndWidth => Beam.Width + (2f * Length * MathF.Tan(Beam.Spread * (MathF.PI / 360f)));
}

/// <summary>
/// Draws searchlight shafts and laser beams in the air, additively, at world resolution,
/// over the lit world (after the light map). How visible a beam is depends on haze (the
/// map's beam haze times the light's own); damaging lasers always show their core. The
/// lens glow and the impact where a beam lands show regardless of haze.
/// </summary>
internal sealed class MapBeamRenderer : IDisposable
{
    private const int ProfileLength = 8;
    private const int ProfileHeight = 64;
    private const int ShaftLength = 128;
    private const int ShaftHeight = 64;
    private const int DotSize = 32;

    /// <summary>Gradient beams are drawn in this many slices (SpriteBatch has no per-vertex colour).</summary>
    private const int GradientSlices = 6;

    /// <summary>Lens width as a fraction of the shaft texture's height at its start.</summary>
    private const float ShaftLensFraction = 0.08f;

    private Texture2D? _profile;
    private Texture2D? _shaft;
    private Texture2D? _dot;

    /// <summary>Laser body: flat and solid across, with only a thin soft edge.</summary>
    private Texture2D? _core;
    private Texture2D? _bandedCore;

    // Retro variants: the same shapes stepped into flat bands (draw with point sampling).
    private Texture2D? _bandedProfile;
    private Texture2D? _bandedShaft;
    private Texture2D? _bandedDot;

    /// <summary>Bands in the retro beam textures.</summary>
    private const int RetroBands = 4;

    /// <summary>
    /// Draws the beams. The batch must be begun with additive blending, and a linear
    /// sampler (point sampling when <paramref name="banded"/>, see <see cref="GetSampler"/>).
    /// </summary>
    /// <param name="mapHaze">The map's beam haze, 0..1.</param>
    /// <param name="scale">World to batch scale; positions map as world * scale + offset.</param>
    /// <param name="banded">Retro maps: beams stepped into flat bands like the map's lights.</param>
    public void Draw(GraphicsDevice device, SpriteBatch batch, IReadOnlyList<ResolvedMapBeam> beams, float mapHaze, float scale, Vector2 offset, float intensity = 1f, bool banded = false)
    {
        if (beams.Count == 0)
        {
            return;
        }

        EnsureTextures(device);
        _drawBanded = banded;
        intensity = Math.Clamp(intensity, 0f, 1f);
        for (var index = 0; index < beams.Count; index += 1)
        {
            var resolved = beams[index];
            var haze = Math.Clamp(mapHaze * resolved.Beam.Haze, 0f, 1f);
            if (resolved.Beam.Kind == MapLightKind.Laser)
            {
                DrawLaser(batch, resolved, haze, scale, offset, intensity);
            }
            else
            {
                DrawShaft(batch, resolved, haze, scale, offset, intensity);
            }
        }
    }

    /// <summary>The sampler to begin the beam batch with.</summary>
    public static SamplerState GetSampler(bool banded) => banded ? SamplerState.PointClamp : SamplerState.LinearClamp;

    public void Dispose()
    {
        _profile?.Dispose();
        _shaft?.Dispose();
        _dot?.Dispose();
        _bandedProfile?.Dispose();
        _bandedShaft?.Dispose();
        _bandedDot?.Dispose();
        _core?.Dispose();
        _bandedCore?.Dispose();
        _core = null;
        _bandedCore = null;
        _profile = null;
        _shaft = null;
        _dot = null;
        _bandedProfile = null;
        _bandedShaft = null;
        _bandedDot = null;
    }

    private bool _drawBanded;

    private Texture2D ProfileTexture => _drawBanded ? _bandedProfile! : _profile!;

    private Texture2D ShaftTexture => _drawBanded ? _bandedShaft! : _shaft!;

    private Texture2D DotTexture => _drawBanded ? _bandedDot! : _dot!;

    private Texture2D CoreTexture => _drawBanded ? _bandedCore! : _core!;

    /// <summary>
    /// A laser is a solid beam whatever the haze: a saturated coloured body with a
    /// white-hot centre line and its own glow around it. Haze only adds the wide, misty
    /// scatter you see when a laser cuts through smoke.
    /// </summary>
    private void DrawLaser(SpriteBatch batch, in ResolvedMapBeam resolved, float haze, float scale, Vector2 offset, float intensity)
    {
        var beam = resolved.Beam;
        var strength = beam.Intensity * intensity;
        var origin = (resolved.Origin * scale) + offset;
        var rotation = -beam.Direction * (MathF.PI / 180f);
        var length = resolved.Length * scale;
        var width = MathF.Max(1f, beam.Width);
        if (length > 0.5f && strength > 0.004f)
        {
            var color = ToColor(beam.Color);
            var endColor = ToColor(beam.EndColor);

            // Atmospheric scatter: only in haze (smoke, rain, dust).
            if (haze > 0.004f)
            {
                DrawSliced(batch, ProfileTexture, origin, rotation, length, MathF.Max(14f, width * 10f) * scale, color, endColor, strength * 0.3f * haze);
            }

            // The laser's own glow, always there; stronger in haze.
            var glow = 0.45f + (0.35f * haze);
            DrawSliced(batch, ProfileTexture, origin, rotation, length, MathF.Max(5f, width * 3.5f) * scale, color, endColor, strength * glow);

            // Solid body: full colour across the beam's width, drawn twice so it
            // saturates to a solid bar even over bright backgrounds.
            var body = MathF.Min(1f, strength * 1.25f);
            DrawSliced(batch, CoreTexture, origin, rotation, length, width * scale, color, endColor, body);
            DrawSliced(batch, CoreTexture, origin, rotation, length, width * scale, color, endColor, body * 0.75f);

            // White-hot centre line.
            DrawSliced(batch, CoreTexture, origin, rotation, length, MathF.Max(1f, width * 0.4f) * scale, Whiten(beam.Color), Whiten(beam.EndColor), MathF.Min(1f, strength * 1.1f));
        }

        // Emitter and impact glints.
        var lens = (8f + (width * 2.5f)) * scale;
        DrawDot(batch, origin, lens * 1.6f, ToColor(beam.Color) * (strength * 0.5f));
        DrawDot(batch, origin, lens, Whiten(beam.Color) * strength);
        if (resolved.Hit)
        {
            var end = (resolved.End * scale) + offset;
            var impact = (10f + (width * 4f)) * scale;
            DrawDot(batch, end, impact * 1.8f, ToColor(beam.EndColor) * (strength * 0.6f));
            DrawDot(batch, end, impact, Whiten(beam.EndColor) * strength);
        }
    }

    private void DrawShaft(SpriteBatch batch, in ResolvedMapBeam resolved, float haze, float scale, Vector2 offset, float intensity)
    {
        var beam = resolved.Beam;
        var strength = beam.Intensity * intensity;
        var origin = (resolved.Origin * scale) + offset;
        var rotation = -beam.Direction * (MathF.PI / 180f);
        if (haze > 0.004f && resolved.Length > 1f)
        {
            var length = resolved.Length * scale;
            // The texture's far end spans its full height; its start is the lens.
            var height = MathF.Max(resolved.EndWidth, beam.Width * 2f) * scale;
            DrawSliced(batch, ShaftTexture, origin, rotation, length, height, ToColor(beam.Color), ToColor(beam.EndColor), strength * 0.34f * haze);
        }

        var lens = (10f + (beam.Width * 2.5f)) * scale;
        DrawDot(batch, origin, lens * 1.6f, ToColor(beam.Color) * (strength * 0.45f));
        DrawDot(batch, origin, lens, Whiten(beam.Color) * (strength * 0.75f));
    }

    /// <summary>
    /// Draws a texture stretched along a beam (texture x = along, y = across), in slices so
    /// the colour can run from start to end.
    /// </summary>
    private static void DrawSliced(SpriteBatch batch, Texture2D texture, Vector2 origin, float rotation, float length, float height, Color start, Color end, float alpha)
    {
        if (alpha <= 0.002f)
        {
            return;
        }

        var slices = start == end ? 1 : GradientSlices;
        var along = new Vector2(MathF.Cos(rotation), MathF.Sin(rotation));
        var sliceTexels = texture.Width / (float)slices;
        for (var slice = 0; slice < slices; slice += 1)
        {
            var x0 = (int)MathF.Round(slice * sliceTexels);
            var x1 = (int)MathF.Round((slice + 1) * sliceTexels);
            if (x1 <= x0)
            {
                continue;
            }

            var t = slices == 1 ? 0f : (slice + 0.5f) / slices;
            var color = Color.Lerp(start, end, t) * alpha;
            var position = origin + (along * (length * x0 / texture.Width));
            batch.Draw(
                texture,
                position,
                new Rectangle(x0, 0, x1 - x0, texture.Height),
                color,
                rotation,
                new Vector2(0f, texture.Height * 0.5f),
                new Vector2(length / texture.Width, height / texture.Height),
                SpriteEffects.None,
                0f);
        }
    }

    private void DrawDot(SpriteBatch batch, Vector2 center, float diameter, Color color)
    {
        if (diameter <= 0.5f || color.A == 0 && color.R == 0 && color.G == 0 && color.B == 0)
        {
            return;
        }

        batch.Draw(DotTexture, center, null, color, 0f, new Vector2(DotSize * 0.5f), diameter / DotSize, SpriteEffects.None, 0f);
    }

    private static Color ToColor(MapLightingColor color) => new(color.R, color.G, color.B);

    /// <summary>Laser cores and lenses burn toward white.</summary>
    private static Color Whiten(MapLightingColor color) => Color.Lerp(ToColor(color), Color.White, 0.55f);

    private void EnsureTextures(GraphicsDevice device)
    {
        _profile ??= CreateProfile(device, banded: false);
        _shaft ??= CreateShaft(device, banded: false);
        _dot ??= CreateDot(device, banded: false);
        _bandedProfile ??= CreateProfile(device, banded: true);
        _bandedShaft ??= CreateShaft(device, banded: true);
        _bandedDot ??= CreateDot(device, banded: true);
        _core ??= CreateCore(device, banded: false);
        _bandedCore ??= CreateCore(device, banded: true);
    }

    /// <summary>
    /// Laser body cross-section: solid through the middle 70%, falling off over a thin
    /// edge so it stays crisp without aliasing (a hard edge in retro style).
    /// </summary>
    private static Texture2D CreateCore(GraphicsDevice device, bool banded)
    {
        var pixels = new Color[ProfileLength * ProfileHeight];
        for (var y = 0; y < ProfileHeight; y += 1)
        {
            var across = MathF.Abs(((y + 0.5f) / ProfileHeight * 2f) - 1f);
            var value = banded
                ? (across <= 0.8f ? 1f : 0f)
                : 1f - SmoothStep(0.7f, 1f, across);
            var channel = (byte)Math.Clamp((int)MathF.Round(value * 255f), 0, 255);
            for (var x = 0; x < ProfileLength; x += 1)
            {
                pixels[(y * ProfileLength) + x] = new Color(channel, channel, channel, channel);
            }
        }

        var texture = new Texture2D(device, ProfileLength, ProfileHeight);
        texture.SetData(pixels);
        return texture;
    }

    /// <summary>Steps a 0..1 value into flat retro bands (zero stays zero).</summary>
    private static float Band(float value, bool banded) =>
        banded ? (value <= 0.02f ? 0f : MathF.Ceiling(value * RetroBands) / RetroBands) : value;

    /// <summary>Laser cross-section: a soft gaussian across, flat along (premultiplied white).</summary>
    private static Texture2D CreateProfile(GraphicsDevice device, bool banded)
    {
        var pixels = new Color[ProfileLength * ProfileHeight];
        for (var y = 0; y < ProfileHeight; y += 1)
        {
            var across = ((y + 0.5f) / ProfileHeight * 2f) - 1f;
            var value = Band(MathF.Exp(-across * across * 5f), banded);
            var channel = (byte)Math.Clamp((int)MathF.Round(value * 255f), 0, 255);
            for (var x = 0; x < ProfileLength; x += 1)
            {
                pixels[(y * ProfileLength) + x] = new Color(channel, channel, channel, channel);
            }
        }

        var texture = new Texture2D(device, ProfileLength, ProfileHeight);
        texture.SetData(pixels);
        return texture;
    }

    /// <summary>
    /// Searchlight shaft: a wedge widening from the lens to full height at the far end,
    /// soft at its edges, brightest in its throat and fading slowly with distance, with a
    /// faint grain of dust in the air.
    /// </summary>
    private static Texture2D CreateShaft(GraphicsDevice device, bool banded)
    {
        var pixels = new Color[ShaftLength * ShaftHeight];
        for (var x = 0; x < ShaftLength; x += 1)
        {
            var u = (x + 0.5f) / ShaftLength;
            var halfWidth = ShaftLensFraction + ((1f - ShaftLensFraction) * u);
            var along = MathF.Pow(1f - u, 0.7f) * (1f + (1.4f * MathF.Pow(1f - u, 8f)));
            along *= Math.Clamp(u / 0.02f, 0f, 1f);
            for (var y = 0; y < ShaftHeight; y += 1)
            {
                var across = MathF.Abs(((y + 0.5f) / ShaftHeight * 2f) - 1f) / halfWidth;
                var edge = 1f - SmoothStep(0.55f, 1f, across);
                var core = 0.45f * MathF.Pow(MathF.Max(0f, 1f - across), 2f);
                var dust = 0.88f + (0.12f * MapLightAnimation.SmoothNoise((x * 0.37d) + (y * 3.1d)));
                var value = Band(Math.Clamp(along * (edge + core) * (banded ? 1f : dust) * 0.7f, 0f, 1f), banded);
                var channel = (byte)Math.Clamp((int)MathF.Round(value * 255f), 0, 255);
                pixels[(y * ShaftLength) + x] = new Color(channel, channel, channel, channel);
            }
        }

        var texture = new Texture2D(device, ShaftLength, ShaftHeight);
        texture.SetData(pixels);
        return texture;
    }

    /// <summary>A round glint: hot centre, soft falloff.</summary>
    private static Texture2D CreateDot(GraphicsDevice device, bool banded)
    {
        var pixels = new Color[DotSize * DotSize];
        var half = DotSize * 0.5f;
        for (var y = 0; y < DotSize; y += 1)
        {
            for (var x = 0; x < DotSize; x += 1)
            {
                var dx = (x + 0.5f - half) / half;
                var dy = (y + 0.5f - half) / half;
                var distance = MathF.Sqrt((dx * dx) + (dy * dy));
                var value = Band(MathF.Pow(MathF.Max(0f, 1f - distance), 2.2f), banded);
                var channel = (byte)Math.Clamp((int)MathF.Round(value * 255f), 0, 255);
                pixels[(y * DotSize) + x] = new Color(channel, channel, channel, channel);
            }
        }

        var texture = new Texture2D(device, DotSize, DotSize);
        texture.SetData(pixels);
        return texture;
    }

    private static float SmoothStep(float edge0, float edge1, float x)
    {
        var t = Math.Clamp((x - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - (2f * t));
    }
}
