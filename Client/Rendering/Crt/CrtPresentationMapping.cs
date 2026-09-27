#nullable enable

using Microsoft.Xna.Framework;
using System;

namespace OpenGarrison.Client.Rendering.Crt;

/// <summary>
/// Maps a point in the presentation rectangle back into the logical source raster.
/// The returned coordinates are deliberately not clamped: points outside [0, 1]
/// are outside the visible curved picture and should not receive input.
/// </summary>
public readonly struct CrtPresentationMapping
{
    public CrtPresentationMapping(int outputWidth, int outputHeight, float curvature)
    {
        OutputWidth = outputWidth > 0 ? outputWidth : 1;
        OutputHeight = outputHeight > 0 ? outputHeight : 1;
        Curvature = float.IsFinite(curvature) ? MathHelper.Clamp(curvature, 0f, 0.08f) : 0f;
        var shortSide = Math.Min(OutputWidth, OutputHeight);
        AspectScale = new Vector2(OutputWidth / (float)shortSide, OutputHeight / (float)shortSide);
    }

    public int OutputWidth { get; }

    public int OutputHeight { get; }

    public float Curvature { get; }

    public Vector2 AspectScale { get; }

    public Vector2 MapOutputToSource(Vector2 outputUv)
    {
        return MapOutputToSource(outputUv, AspectScale, Curvature);
    }

    /// <summary>
    /// Shared CPU form of the shader mapping. Curvature expands source coordinates
    /// toward the edges; the source image therefore fits inside the curved picture
    /// and the portions beyond its boundary map outside the source rectangle.
    /// </summary>
    public static Vector2 MapOutputToSource(Vector2 outputUv, Vector2 aspectScale, float curvature)
    {
        if (!float.IsFinite(curvature) || curvature <= 0f)
        {
            return outputUv;
        }

        var physical = (outputUv * 2f - Vector2.One) * aspectScale;
        var radiusSquared = Vector2.Dot(physical, physical);
        var curved = physical * (1f + curvature * radiusSquared);
        return new Vector2(
            0.5f + 0.5f * (curved.X / aspectScale.X),
            0.5f + 0.5f * (curved.Y / aspectScale.Y));
    }

    public bool ContainsVisiblePoint(Vector2 outputUv)
    {
        var sourceUv = MapOutputToSource(outputUv);
        return sourceUv.X >= 0f && sourceUv.X <= 1f
            && sourceUv.Y >= 0f && sourceUv.Y <= 1f;
    }
}
