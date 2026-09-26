namespace OpenGarrison.Core;

/// <summary>
/// Pixel alpha mask for melee swing areas, authored as a pack sprite whose origin
/// aligns with the torso/weapon anchor (facing-right art; flipped when facing left).
/// </summary>
public sealed class MeleeHitboxMask
{
    public const byte DefaultAlphaThreshold = 12;

    /// <summary>
    /// Matches client torso-replacement sit-down (2 source pixels × typical 2× pixel scale).
    /// Used by full torso replacements (e.g. Eyelander). Companion-offset weapons apply
    /// <c>WeaponOffsetY</c> against the body origin instead and should omit this.
    /// </summary>
    public const float TorsoSitDownWorldOffset = 4f;

    public MeleeHitboxMask(
        int width,
        int height,
        int originX,
        int originY,
        byte[] alphaSamples,
        byte alphaThreshold = DefaultAlphaThreshold)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Mask dimensions must be positive.");
        }

        ArgumentNullException.ThrowIfNull(alphaSamples);
        if (alphaSamples.Length < width * height)
        {
            throw new ArgumentException("Alpha sample buffer is smaller than width * height.", nameof(alphaSamples));
        }

        Width = width;
        Height = height;
        OriginX = originX;
        OriginY = originY;
        AlphaSamples = alphaSamples;
        AlphaThreshold = alphaThreshold;
        MaxReachFromOrigin = ComputeMaxReach(width, height, originX, originY, alphaSamples, alphaThreshold);
    }

    public int Width { get; }

    public int Height { get; }

    public int OriginX { get; }

    public int OriginY { get; }

    public byte[] AlphaSamples { get; }

    public byte AlphaThreshold { get; }

    public float MaxReachFromOrigin { get; }

    public bool IsOpaqueAtPixel(int pixelX, int pixelY)
    {
        if (pixelX < 0 || pixelY < 0 || pixelX >= Width || pixelY >= Height)
        {
            return false;
        }

        return AlphaSamples[(pixelY * Width) + pixelX] > AlphaThreshold;
    }

    /// <summary>
    /// Same facing-aware aim rotation as weapon sprites: mirror when aiming left, then
    /// rotate by aim (+π when left) so left aim is a horizontal mirror, not an upside-down flip.
    /// </summary>
    public static float ResolveAimDrawRotation(float aimRadians, bool facingLeft)
        => facingLeft ? aimRadians + MathF.PI : aimRadians;

    public bool ContainsWorldPoint(
        float worldX,
        float worldY,
        float anchorX,
        float anchorY,
        bool facingLeft,
        float geometryScale,
        float? rotationRadians = null)
    {
        var scale = MathF.Max(0.1f, geometryScale);
        var localX = (worldX - anchorX) / scale;
        var localY = (worldY - anchorY) / scale;
        WorldDeltaToMaskLocal(ref localX, ref localY, facingLeft, rotationRadians);

        var pixelX = (int)MathF.Floor(OriginX + localX);
        var pixelY = (int)MathF.Floor(OriginY + localY);
        return IsOpaqueAtPixel(pixelX, pixelY);
    }

    public bool OverlapsRectangle(
        float left,
        float top,
        float right,
        float bottom,
        float anchorX,
        float anchorY,
        bool facingLeft,
        float geometryScale,
        float? rotationRadians = null)
    {
        if (right <= left || bottom <= top)
        {
            return false;
        }

        var scale = MathF.Max(0.1f, geometryScale);
        GetWorldBounds(
            anchorX,
            anchorY,
            facingLeft,
            scale,
            out var maskLeft,
            out var maskTop,
            out var maskRight,
            out var maskBottom,
            rotationRadians);
        if (right <= maskLeft || left >= maskRight || bottom <= maskTop || top >= maskBottom)
        {
            return false;
        }

        for (var pixelY = 0; pixelY < Height; pixelY += 1)
        {
            for (var pixelX = 0; pixelX < Width; pixelX += 1)
            {
                if (!IsOpaqueAtPixel(pixelX, pixelY))
                {
                    continue;
                }

                PixelToWorld(pixelX, pixelY, anchorX, anchorY, facingLeft, scale, out var worldX, out var worldY, rotationRadians);
                if (worldX >= left && worldX < right && worldY >= top && worldY < bottom)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public bool OverlapsCircle(
        float centerX,
        float centerY,
        float radius,
        float anchorX,
        float anchorY,
        bool facingLeft,
        float geometryScale,
        float? rotationRadians = null)
    {
        var safeRadius = MathF.Max(0f, radius);
        return OverlapsRectangle(
            centerX - safeRadius,
            centerY - safeRadius,
            centerX + safeRadius,
            centerY + safeRadius,
            anchorX,
            anchorY,
            facingLeft,
            geometryScale,
            rotationRadians);
    }

    public void GetWorldBounds(
        float anchorX,
        float anchorY,
        bool facingLeft,
        float geometryScale,
        out float left,
        out float top,
        out float right,
        out float bottom,
        float? rotationRadians = null)
    {
        var scale = MathF.Max(0.1f, geometryScale);
        if (rotationRadians is float)
        {
            var reach = MaxReachFromOrigin * scale;
            left = anchorX - reach;
            right = anchorX + reach;
            top = anchorY - reach;
            bottom = anchorY + reach;
            _ = facingLeft;
            return;
        }

        var leftOffset = -OriginX * scale;
        var rightOffset = (Width - OriginX) * scale;
        if (facingLeft)
        {
            left = anchorX - rightOffset;
            right = anchorX - leftOffset;
        }
        else
        {
            left = anchorX + leftOffset;
            right = anchorX + rightOffset;
        }

        top = anchorY - (OriginY * scale);
        bottom = anchorY + ((Height - OriginY) * scale);
    }

    public void PixelToWorld(
        int pixelX,
        int pixelY,
        float anchorX,
        float anchorY,
        bool facingLeft,
        float scale,
        out float worldX,
        out float worldY,
        float? rotationRadians = null)
    {
        var localX = (pixelX + 0.5f) - OriginX;
        var localY = (pixelY + 0.5f) - OriginY;
        MaskLocalToWorldDelta(ref localX, ref localY, facingLeft, rotationRadians);
        worldX = anchorX + (localX * scale);
        worldY = anchorY + (localY * scale);
    }

    private static void MaskLocalToWorldDelta(
        ref float localX,
        ref float localY,
        bool facingLeft,
        float? rotationRadians)
    {
        if (rotationRadians is float aimRadians)
        {
            // Match weapon sprites: scaleX = facing, rotation = aim (+π when left).
            var facingScale = facingLeft ? -1f : 1f;
            localX *= facingScale;
            var rotation = ResolveAimDrawRotation(aimRadians, facingLeft);
            var cos = MathF.Cos(rotation);
            var sin = MathF.Sin(rotation);
            var rotatedX = (localX * cos) - (localY * sin);
            var rotatedY = (localX * sin) + (localY * cos);
            localX = rotatedX;
            localY = rotatedY;
            return;
        }

        if (facingLeft)
        {
            localX = -localX;
        }
    }

    private static void WorldDeltaToMaskLocal(
        ref float localX,
        ref float localY,
        bool facingLeft,
        float? rotationRadians)
    {
        if (rotationRadians is float aimRadians)
        {
            var facingScale = facingLeft ? -1f : 1f;
            var rotation = ResolveAimDrawRotation(aimRadians, facingLeft);
            var cos = MathF.Cos(-rotation);
            var sin = MathF.Sin(-rotation);
            var unrotatedX = (localX * cos) - (localY * sin);
            var unrotatedY = (localX * sin) + (localY * cos);
            localX = unrotatedX * facingScale;
            localY = unrotatedY;
            return;
        }

        if (facingLeft)
        {
            localX = -localX;
        }
    }

    private static float ComputeMaxReach(
        int width,
        int height,
        int originX,
        int originY,
        byte[] alphaSamples,
        byte alphaThreshold)
    {
        var maxReachSquared = 0f;
        for (var y = 0; y < height; y += 1)
        {
            var row = y * width;
            for (var x = 0; x < width; x += 1)
            {
                if (alphaSamples[row + x] <= alphaThreshold)
                {
                    continue;
                }

                var dx = (x + 0.5f) - originX;
                var dy = (y + 0.5f) - originY;
                var reachSquared = (dx * dx) + (dy * dy);
                if (reachSquared > maxReachSquared)
                {
                    maxReachSquared = reachSquared;
                }
            }
        }

        return MathF.Sqrt(maxReachSquared);
    }
}
