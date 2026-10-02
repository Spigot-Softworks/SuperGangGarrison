#nullable enable

using Microsoft.Xna.Framework;
using System;

namespace OpenGarrison.Client;

/// <summary>
/// Sub-pixel camera presentation for the gameplay world pass.
/// </summary>
/// <remarks>
/// <para>
/// World draw code rounds positions in camera space, so it needs a whole-pixel
/// camera to keep the map, players and attachments on one consistent grid. A
/// whole-pixel camera, however, cannot follow motion that averages a fractional
/// distance per frame: at ~3.45 world px per 60 Hz frame it alternates 3 and 4
/// px steps, which the 1.125 zoom and the logical-canvas upscale amplify into a
/// visible big/small/big/small judder that reads as 30 fps.
/// </para>
/// <para>
/// The world pass therefore draws with the whole-pixel camera, exactly as
/// before, and this class supplies the remaining fraction as one translation in
/// the world sprite-batch transform. The pass is rendered at presentation
/// (window) resolution rather than the logical canvas, so the fraction survives
/// as whole screen pixels. The locally controlled player, whose interpolated
/// position also carries a fraction that per-sprite rounding would drop, gets
/// its own residual so it stays fixed relative to the camera that follows it.
/// </para>
/// </remarks>
internal sealed class SubpixelWorldPresentation
{
    public const string LegacyEnvironmentVariable = "OG_CLIENT_LEGACY_WORLD_PRESENTATION";

    public static bool IsDisabledByEnvironment { get; } =
        Environment.GetEnvironmentVariable(LegacyEnvironmentVariable) is "1" or "true" or "TRUE";

    /// <summary>Exact camera minus the whole-pixel draw camera, in world pixels, in [0, 1).</summary>
    public Vector2 CameraResidual { get; private set; }

    /// <summary>Extra world-space translation applied while drawing one object (the local player).</summary>
    public Vector2 ObjectResidual { get; private set; }

    /// <summary>Logical-canvas pixels to render-target pixels for the active world pass.</summary>
    public Vector2 TargetScale { get; private set; } = Vector2.One;

    public bool IsWorldPassActive { get; private set; }

    /// <summary>True while the active world pass applies the sub-pixel camera.</summary>
    public bool IsSubpixelPassActive => IsWorldPassActive && LastWorldPassUsedSubpixel;

    /// <summary>Whether the most recent world pass applied the sub-pixel camera (read by HUD anchoring).</summary>
    public bool LastWorldPassUsedSubpixel { get; private set; }

    /// <summary>True when the active world pass draws into the presentation-resolution world target.</summary>
    public bool RendersToWorldTarget { get; private set; }

    /// <summary>Splits an exact camera into the whole-pixel draw camera and a residual in [0, 1).</summary>
    public static Vector2 SplitCamera(Vector2 exactCamera, out Vector2 residual)
    {
        if (!IsFinite(exactCamera))
        {
            residual = Vector2.Zero;
            return Vector2.Zero;
        }

        var drawCamera = new Vector2(MathF.Floor(exactCamera.X), MathF.Floor(exactCamera.Y));
        residual = exactCamera - drawCamera;
        return drawCamera;
    }

    /// <summary>
    /// Fraction that per-sprite rounding removes from an object drawn at
    /// <paramref name="renderPosition"/> against a whole-pixel camera.
    /// Must match <c>Game1.RoundToSourcePixels</c> (midpoint away from zero).
    /// </summary>
    public static Vector2 GetObjectResidual(Vector2 renderPosition)
    {
        if (!IsFinite(renderPosition))
        {
            return Vector2.Zero;
        }

        return new Vector2(
            renderPosition.X - MathF.Round(renderPosition.X, MidpointRounding.AwayFromZero),
            renderPosition.Y - MathF.Round(renderPosition.Y, MidpointRounding.AwayFromZero));
    }

    public void SetCameraResidual(Vector2 residual)
    {
        CameraResidual = IsFinite(residual) ? residual : Vector2.Zero;
    }

    public void SetObjectResidual(Vector2 residual)
    {
        ObjectResidual = IsSubpixelPassActive && IsFinite(residual) ? residual : Vector2.Zero;
    }

    public void BeginWorldPass(bool useSubpixel, Vector2 targetScale, bool rendersToWorldTarget)
    {
        IsWorldPassActive = true;
        LastWorldPassUsedSubpixel = useSubpixel;
        TargetScale = useSubpixel && IsFinite(targetScale) && targetScale.X > 0f && targetScale.Y > 0f
            ? targetScale
            : Vector2.One;
        RendersToWorldTarget = useSubpixel && rendersToWorldTarget;
        ObjectResidual = Vector2.Zero;
    }

    public void EndWorldPass()
    {
        IsWorldPassActive = false;
        RendersToWorldTarget = false;
        ObjectResidual = Vector2.Zero;
    }

    /// <summary>Sprite-batch transform for world-pass coordinates (pre-zoom, camera-relative).</summary>
    public Matrix GetWorldTransform(float zoom)
    {
        if (!IsSubpixelPassActive)
        {
            return Matrix.CreateScale(zoom, zoom, 1f);
        }

        var shift = ObjectResidual - CameraResidual;
        return Matrix.CreateTranslation(shift.X, shift.Y, 0f)
            * Matrix.CreateScale(zoom * TargetScale.X, zoom * TargetScale.Y, 1f);
    }

    /// <summary>Maps a world-pass rectangle into render-target pixels (for scissor rectangles).</summary>
    public Rectangle TransformWorldRectangle(Rectangle worldPassRectangle, float zoom)
    {
        var transform = GetWorldTransform(zoom);
        var topLeft = Vector2.Transform(new Vector2(worldPassRectangle.Left, worldPassRectangle.Top), transform);
        var bottomRight = Vector2.Transform(new Vector2(worldPassRectangle.Right, worldPassRectangle.Bottom), transform);
        var left = (int)MathF.Floor(MathF.Min(topLeft.X, bottomRight.X));
        var top = (int)MathF.Floor(MathF.Min(topLeft.Y, bottomRight.Y));
        var right = (int)MathF.Ceiling(MathF.Max(topLeft.X, bottomRight.X));
        var bottom = (int)MathF.Ceiling(MathF.Max(topLeft.Y, bottomRight.Y));
        return new Rectangle(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }

    /// <summary>
    /// Offset, in logical-canvas pixels, that keeps world-anchored HUD elements
    /// aligned with a world pass that applied the camera residual.
    /// </summary>
    public Vector2 GetHudOffset(float zoom)
        => LastWorldPassUsedSubpixel ? -CameraResidual * zoom : Vector2.Zero;

    private static bool IsFinite(Vector2 value)
        => float.IsFinite(value.X) && float.IsFinite(value.Y);
}
