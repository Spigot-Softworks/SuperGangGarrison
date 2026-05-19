#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private readonly Dictionary<LoadedSpriteFrame, Texture2D> _spriteFrameAlphaMaskCache = new();
    private static readonly BlendState _multiplyColorBlendState = new()
    {
        ColorSourceBlend = Blend.DestinationColor,
        ColorDestinationBlend = Blend.InverseSourceAlpha,
        ColorBlendFunction = BlendFunction.Add,
        AlphaSourceBlend = Blend.Zero,
        AlphaDestinationBlend = Blend.One,
        AlphaBlendFunction = BlendFunction.Add,
    };

    private static readonly BlendState _screenColorBlendState = new()
    {
        ColorSourceBlend = Blend.SourceAlpha,
        ColorDestinationBlend = Blend.One,
        ColorBlendFunction = BlendFunction.Add,
        AlphaSourceBlend = Blend.One,
        AlphaDestinationBlend = Blend.One,
        AlphaBlendFunction = BlendFunction.Add,
    };

    private static float RoundToSourcePixel(float value)
    {
        return MathF.Round(value, MidpointRounding.AwayFromZero);
    }

    private static Vector2 RoundToSourcePixels(Vector2 value)
    {
        return new Vector2(
            RoundToSourcePixel(value.X),
            RoundToSourcePixel(value.Y));
    }

    private void DrawSniperTracers(Vector2 cameraPosition)
    {
        // Draw particles first (behind the tracer lines)
        foreach (var particle in _sniperTracerParticles)
        {
            var t = particle.TicksRemaining / (float)SniperTracerParticle.LifetimeTicks;
            var alpha = t * t * t; // Cubic ease-out: very fast fade at start, very slow at end
            var size = 3f;
            var drawColor = particle.Color * alpha;
            var drawPos = new Vector2(particle.X - cameraPosition.X - size / 2f, particle.Y - cameraPosition.Y - size / 2f);
            _spriteBatch.Draw(_pixel, new Rectangle((int)drawPos.X, (int)drawPos.Y, (int)size, (int)size), drawColor);
        }

        // Draw tracer lines
        foreach (var trace in _world.CombatTraces)
        {
            if (!trace.IsSniperTracer)
            {
                continue;
            }

            var alpha = 0.8f * (trace.TicksRemaining / 3f);
            var color = trace.Team == PlayerTeam.Blue
                ? Color.Blue * alpha
                : Color.Red * alpha;
            DrawWorldLine(trace.StartX, trace.StartY, trace.EndX, trace.EndY, cameraPosition, color, 2f);

            // Spawn particles at the end of new traces (first frame only)
            if (trace.TicksRemaining == 3)
            {
                var dirX = trace.EndX - trace.StartX;
                var dirY = trace.EndY - trace.StartY;
                var length = MathF.Sqrt(dirX * dirX + dirY * dirY);
                if (length > 0.01f)
                {
                    dirX /= length;
                    dirY /= length;

                    var particleColor = trace.Team == PlayerTeam.Blue ? new Color(100, 150, 255) : new Color(255, 100, 100);
                    var rng = new Random((int)(trace.EndX * 1000 + trace.EndY));

                    for (int i = 0; i < 4; i++)
                    {
                        var angle = rng.NextDouble() * Math.PI * 2;
                        var spread = 0.4f + (float)rng.NextDouble() * 0.3f;
                        var speed = 5f + (float)rng.NextDouble() * 5f;
                        var velX = -dirX * speed + (float)Math.Cos(angle) * spread * 3f;
                        var velY = -dirY * speed + (float)Math.Sin(angle) * spread * 3f;

                        _sniperTracerParticles.Add(new SniperTracerParticle(
                            trace.EndX,
                            trace.EndY,
                            velX,
                            velY,
                            particleColor));
                    }
                }
            }
        }
    }

    private void DrawSniperAimIndicators(Vector2 cameraPosition)
    {
        if (_clientSniperAimIndicators.Count == 0)
        {
            return;
        }

        // Prepare indicator data for batch rendering
        var indicatorData = new System.Collections.Generic.List<(int x, int y, Color outlineColor, Color centerColor)>(_clientSniperAimIndicators.Count);

        foreach (var kvp in _clientSniperAimIndicators)
        {
            var indicator = kvp.Value;

            // Get the team color
            var baseColor = indicator.Team == PlayerTeam.Blue ? Color.Blue : Color.Red;

            // Calculate fade-out multiplier based on remaining ticks
            var fadeMultiplier = indicator.TicksRemaining / (float)SniperAimIndicatorFadeTicks;

            // Apply transparency with fade-out
            var alpha = indicator.BaseTransparency * fadeMultiplier;

            // Outline uses base team color
            var outlineColor = baseColor * alpha;

            // Blue center needs to be brighter with more saturation for visibility
            Color centerColor;
            if (indicator.Team == PlayerTeam.Blue)
            {
                // Use a brighter, more saturated blue
                var brightBlue = new Color(100, 180, 255);
                // Less white blending to keep it more saturated
                var lighterBlue = Color.Lerp(brightBlue, Color.White, 0.4f);
                centerColor = lighterBlue * alpha;
            }
            else
            {
                var lighterRed = Color.Lerp(baseColor, Color.White, 0.6f);
                centerColor = lighterRed * alpha;
            }

            // Draw position centered for a 3x3 pixel indicator (1px outline + 1x1 center, rounded)
            var drawPosX = (int)(indicator.X - cameraPosition.X - 1f);
            var drawPosY = (int)(indicator.Y - cameraPosition.Y - 1f);

            indicatorData.Add((drawPosX, drawPosY, outlineColor, centerColor));
        }

        // Draw all rounded outlines with normal blending
        // Outline pattern (1px thick, corners removed for rounded appearance):
        //   . # .
        //   # O #
        //   . # .
        foreach (var (x, y, outlineColor, _) in indicatorData)
        {
            // Top pixel
            _spriteBatch.Draw(_pixel, new Rectangle(x + 1, y, 1, 1), outlineColor);

            // Middle row: left and right pixels
            _spriteBatch.Draw(_pixel, new Rectangle(x, y + 1, 1, 1), outlineColor);
            _spriteBatch.Draw(_pixel, new Rectangle(x + 2, y + 1, 1, 1), outlineColor);

            // Bottom pixel
            _spriteBatch.Draw(_pixel, new Rectangle(x + 1, y + 2, 1, 1), outlineColor);
        }

        // Draw all 1x1 center pixels with lighter color
        foreach (var (x, y, _, centerColor) in indicatorData)
        {
            _spriteBatch.Draw(_pixel, new Rectangle(x + 1, y + 1, 1, 1), centerColor);
        }
    }

    private bool DrawLevelBackground(Rectangle worldRectangle)
    {
        var backgroundName = _world.Level.BackgroundAssetName;
        if (TryDrawLevelBackgroundFile(backgroundName, worldRectangle))
        {
            return true;
        }

        if (_runtimeAssets is null)
        {
            _spriteBatch.Draw(_pixel, worldRectangle, new Color(34, 44, 60));
            return false;
        }

        var background = string.IsNullOrWhiteSpace(backgroundName)
            ? null
            : _runtimeAssets.GetBackground(backgroundName);
        if (background is null)
        {
            _spriteBatch.Draw(_pixel, worldRectangle, new Color(34, 44, 60));
            return false;
        }

        _spriteBatch.Draw(background, worldRectangle, Color.White);
        return true;
    }

    private bool TryDrawLevelBackgroundFile(string? backgroundName, Rectangle worldRectangle)
    {
        if (string.IsNullOrWhiteSpace(backgroundName))
        {
            return false;
        }

        if (!Path.IsPathRooted(backgroundName) && !backgroundName.Contains(Path.DirectorySeparatorChar) && !backgroundName.Contains(Path.AltDirectorySeparatorChar))
        {
            return false;
        }

        if (string.Equals(_levelBackgroundFileFailedPath, backgroundName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.Equals(_levelBackgroundFileTexturePath, backgroundName, StringComparison.OrdinalIgnoreCase))
        {
            _levelBackgroundFileTexture?.Dispose();
            _levelBackgroundFileTexture = null;
            _levelBackgroundFileTexturePath = null;

            try
            {
                byte[]? bytes = null;
                if (File.Exists(backgroundName))
                {
                    bytes = File.ReadAllBytes(backgroundName);
                }
                else if (BrowserContentCatalog.TryGetBinaryForPath(backgroundName, out var browserBytes))
                {
                    bytes = browserBytes;
                }

                if (bytes is null || bytes.Length == 0)
                {
                    _levelBackgroundFileFailedPath = backgroundName;
                    return false;
                }

                _levelBackgroundFileTexture = TextureDecodeUtility.LoadTexture(GraphicsDevice, bytes, applyLegacyChromaKey: false);
                _levelBackgroundFileTexturePath = backgroundName;
                _levelBackgroundFileFailedPath = null;
            }
            catch (IOException)
            {
                _levelBackgroundFileFailedPath = backgroundName;
                return false;
            }
            catch (InvalidOperationException)
            {
                _levelBackgroundFileFailedPath = backgroundName;
                return false;
            }
            catch (NotSupportedException)
            {
                _levelBackgroundFileFailedPath = backgroundName;
                return false;
            }
        }

        if (_levelBackgroundFileTexture is null)
        {
            return false;
        }

        _spriteBatch.Draw(_levelBackgroundFileTexture, worldRectangle, Color.White);
        return true;
    }

    private void DrawWorldLine(float startX, float startY, float endX, float endY, Vector2 cameraPosition, Color color, float thickness)
    {
        var start = new Vector2(startX - cameraPosition.X, startY - cameraPosition.Y);
        var end = new Vector2(endX - cameraPosition.X, endY - cameraPosition.Y);
        var edge = end - start;
        var angle = MathF.Atan2(edge.Y, edge.X);
        var length = edge.Length();
        if (length <= 0.01f)
        {
            return;
        }

        _spriteBatch.Draw(
            _pixel,
            start,
            null,
            color,
            angle,
            Vector2.Zero,
            new Vector2(length, thickness),
            SpriteEffects.None,
            0f);
    }

    private void DrawCurvedWorldLine(float startX, float startY, float endX, float endY, Vector2 cameraPosition, Color color, float thickness, Vector2 aimDirection)
    {
        if (!AreFinite(startX, startY, endX, endY, thickness)
            || !IsFiniteVector(cameraPosition)
            || !IsFiniteVector(aimDirection)
            || aimDirection.LengthSquared() <= 0.0001f)
        {
            return;
        }

        var start = new Vector2(startX - cameraPosition.X, startY - cameraPosition.Y);
        var end = new Vector2(endX - cameraPosition.X, endY - cameraPosition.Y);
        var toTarget = end - start;
        var distToTarget = toTarget.Length();

        if (distToTarget <= 0.01f)
        {
            return;
        }

        // Normalize directions
        var aimDir = aimDirection;
        aimDir.Normalize();

        var targetDir = toTarget;
        targetDir.Normalize();

        // Calculate alignment between aim direction and target direction
        var alignment = Vector2.Dot(aimDir, targetDir);

        // If already pointing at target, draw straight line
        if (alignment > 0.98f)
        {
            DrawWorldLine(startX, startY, endX, endY, cameraPosition, color, thickness);
            return;
        }

        // Calculate control point:
        // The beam should start in the weapon's aim direction and curve toward the target,
        // leveling out around the halfway point
        var controlDist = distToTarget * 0.5f;
        var controlPoint = start + aimDir * controlDist;

        // Calculate perpendicular offset to curve from aim direction to target direction
        // perpendicular to aim direction
        var perpToAim = new Vector2(-aimDir.Y, aimDir.X);

        // Check which direction to offset based on target location
        if (Vector2.Dot(perpToAim, targetDir) < 0)
        {
            perpToAim = -perpToAim;
        }

        // Offset strength: how much curvature we need based on angle difference
        var turnAngle = MathF.Acos(MathF.Max(-1f, MathF.Min(1f, alignment)));
        var offsetAmount = distToTarget * 0.12f * (turnAngle / MathF.PI);
        controlPoint += perpToAim * offsetAmount;

        // Draw pixellated quadratic Bezier curve with 4-pixel width
        const float pixelSize = 2f;
        const float beamWidth = 4f; // 4 pixels wide (2 blocks)
        const int segments = 32;
        var pixelatedCells = new System.Collections.Generic.HashSet<(int, int)>();

        var curvePoints = new System.Collections.Generic.List<Vector2>(segments + 1);
        for (int i = 0; i <= segments; i++)
        {
            var t = (float)i / segments;
            var oneMinusT = 1f - t;
            var point = oneMinusT * oneMinusT * start
                      + 2f * oneMinusT * t * controlPoint
                      + t * t * end;
            curvePoints.Add(point);
        }

        // For each segment of the curve, fill all grid cells that the thick line passes through
        for (int i = 0; i < curvePoints.Count - 1; i++)
        {
            var segStart = curvePoints[i];
            var segEnd = curvePoints[i + 1];
            var segDir = segEnd - segStart;
            var segLength = segDir.Length();

            if (segLength < 0.01f) continue;

            segDir /= segLength;
            var perpDir = new Vector2(-segDir.Y, segDir.X);

            // Fill cells perpendicular to the line at each sample point
            for (int j = 0; j <= (int)MathF.Ceiling(segLength); j++)
            {
                var samplePoint = segStart + segDir * j;

                // Draw perpendicular thickness around the point
                for (float offset = -beamWidth / 2f; offset <= beamWidth / 2f; offset += pixelSize)
                {
                    var thickPoint = samplePoint + perpDir * offset;
                    var gridX = (int)MathF.Floor(thickPoint.X / pixelSize);
                    var gridY = (int)MathF.Floor(thickPoint.Y / pixelSize);
                    pixelatedCells.Add((gridX, gridY));
                }
            }
        }

        // Draw all pixelated cells as 2x2 rectangles
        foreach (var (gridX, gridY) in pixelatedCells)
        {
            var pixelRect = new Rectangle(
                (int)(gridX * pixelSize),
                (int)(gridY * pixelSize),
                (int)pixelSize,
                (int)pixelSize);
            _spriteBatch.Draw(_pixel, pixelRect, color);
        }
    }

    // nozzleThickness : width at t=0 (gun end)
    // maxThickness    : peak width reached after rampDistancePixels world-pixels
    // tailThickness   : width at t=1 (target end)
    // rampDistPixels  : world-pixel distance over which the beam widens from nozzle to max
    private void DrawCurvedWorldLine(
        float startX, float startY, float endX, float endY,
        Vector2 cameraPosition,
        Color startColor, Color endColor,
        float nozzleThickness, float maxThickness, float tailThickness, float rampDistPixels,
        Vector2 aimDirection)
    {
        if (!AreFinite(startX, startY, endX, endY, nozzleThickness, maxThickness, tailThickness, rampDistPixels)
            || !IsFiniteVector(cameraPosition)
            || !IsFiniteVector(aimDirection)
            || aimDirection.LengthSquared() <= 0.0001f)
        {
            return;
        }

        var start = new Vector2(startX - cameraPosition.X, startY - cameraPosition.Y);
        var end   = new Vector2(endX   - cameraPosition.X, endY   - cameraPosition.Y);
        var toTarget = end - start;
        var distToTarget = toTarget.Length();
        if (distToTarget <= 0.01f) return;

        var aimDir = aimDirection;
        aimDir.Normalize();
        var targetDir = toTarget / distToTarget;
        var alignment = Vector2.Dot(aimDir, targetDir);

        Vector2 controlPoint;
        if (alignment > 0.98f)
        {
            controlPoint = (start + end) * 0.5f;
        }
        else
        {
            var controlDist = distToTarget * 0.5f;
            controlPoint = start + aimDir * controlDist;
            var perpToAim = new Vector2(-aimDir.Y, aimDir.X);
            if (Vector2.Dot(perpToAim, targetDir) < 0)
                perpToAim = -perpToAim;
            var turnAngle = MathF.Acos(MathF.Max(-1f, MathF.Min(1f, alignment)));
            controlPoint += perpToAim * (distToTarget * 0.12f * (turnAngle / MathF.PI));
        }

        const float pixelSize = 2f;
        const int segments = 32;

        // First-write-wins: cell colour corresponds to its earliest-t segment
        var pixelatedCells = new System.Collections.Generic.Dictionary<(int, int), Color>();

        var curvePoints = new System.Collections.Generic.List<(Vector2 pos, float t)>(segments + 1);
        for (int i = 0; i <= segments; i++)
        {
            var t = (float)i / segments;
            var oneMinusT = 1f - t;
            var point = oneMinusT * oneMinusT * start
                      + 2f * oneMinusT * t * controlPoint
                      + t * t * end;
            curvePoints.Add((point, t));
        }

        var safeRamp = MathF.Max(rampDistPixels, 0.01f);
        var safeTail = MathF.Max(distToTarget - safeRamp, 0.01f);

        for (int i = 0; i < curvePoints.Count - 1; i++)
        {
            var (segStart, tStart) = curvePoints[i];
            var (segEnd,   _)      = curvePoints[i + 1];
            var segDir = segEnd - segStart;
            var segLength = segDir.Length();
            if (segLength < 0.01f) continue;
            segDir /= segLength;
            var perpDir = new Vector2(-segDir.Y, segDir.X);

            // Envelope: nozzle → ramp up to max → taper to tail
            var dist = tStart * distToTarget;
            float beamWidth;
            if (dist < safeRamp)
                beamWidth = nozzleThickness + (maxThickness - nozzleThickness) * (dist / safeRamp);
            else
                beamWidth = maxThickness + (tailThickness - maxThickness) * ((dist - safeRamp) / safeTail);

            var color = Color.Lerp(startColor, endColor, tStart);

            for (int j = 0; j <= (int)MathF.Ceiling(segLength); j++)
            {
                var samplePoint = segStart + segDir * j;
                for (float offset = -beamWidth / 2f; offset <= beamWidth / 2f; offset += pixelSize)
                {
                    var thickPoint = samplePoint + perpDir * offset;
                    var gridX = (int)MathF.Floor(thickPoint.X / pixelSize);
                    var gridY = (int)MathF.Floor(thickPoint.Y / pixelSize);
                    var key = (gridX, gridY);
                    if (!pixelatedCells.ContainsKey(key))
                        pixelatedCells[key] = color;
                }
            }
        }

        foreach (var ((gridX, gridY), cellColor) in pixelatedCells)
        {
            var pixelRect = new Rectangle(
                (int)(gridX * pixelSize),
                (int)(gridY * pixelSize),
                (int)pixelSize,
                (int)pixelSize);
            _spriteBatch.Draw(_pixel, pixelRect, cellColor);
        }
    }

    private bool TryDrawSprite(string spriteName, int frameIndex, float worldX, float worldY, Vector2 cameraPosition, Color tint, float rotation = 0f)
    {
        return TryDrawSprite(spriteName, frameIndex, worldX, worldY, cameraPosition, tint, rotation, Vector2.One);
    }

    private bool TryDrawSprite(string spriteName, int frameIndex, float worldX, float worldY, Vector2 cameraPosition, Color tint, float rotation, float scale)
    {
        return TryDrawSprite(spriteName, frameIndex, worldX, worldY, cameraPosition, tint, rotation, new Vector2(scale, scale));
    }

    private bool TryDrawSprite(string spriteName, int frameIndex, float worldX, float worldY, Vector2 cameraPosition, Color tint, float rotation, Vector2 scale)
    {
        var sprite = GetResolvedSprite(spriteName);
        if (sprite is null || sprite.Frames.Count == 0)
        {
            return false;
        }

        var clampedFrameIndex = Math.Clamp(frameIndex, 0, sprite.Frames.Count - 1);
        DrawSpriteFrameWithOptionalShadow(
            sprite.Frames[clampedFrameIndex],
            new Vector2(worldX - cameraPosition.X, worldY - cameraPosition.Y),
            tint,
            rotation,
            sprite.Origin.ToVector2(),
            scale);
        return true;
    }

    private void DrawSpriteFrameWithOptionalShadow(
        LoadedSpriteFrame frame,
        Vector2 position,
        Color tint,
        float rotation,
        Vector2 origin,
        Vector2 scale,
        SpriteEffects effects = SpriteEffects.None)
    {
        if (!OperatingSystem.IsBrowser() && _spriteDropShadowEnabled && tint.A > 0)
        {
            var shadowAlpha = ((tint.A / 255f) * 0.32f);
            var shadowTint = new Color(0, 0, 0) * shadowAlpha;
            _spriteBatch.Draw(
                frame.Texture,
                position + new Vector2(1f, 1f),
                frame.SourceRectangle,
                shadowTint,
                rotation,
                origin,
                scale,
                effects,
                0f);
        }

        _spriteBatch.Draw(
            frame.Texture,
            position,
            frame.SourceRectangle,
            tint,
            rotation,
            origin,
            scale,
            effects,
            0f);
    }

    private void DrawSpriteFrame(
        LoadedSpriteFrame frame,
        Vector2 position,
        Color tint,
        float rotation,
        Vector2 origin,
        Vector2 scale,
        SpriteEffects effects = SpriteEffects.None)
    {
        _spriteBatch.Draw(
            frame.Texture,
            position,
            frame.SourceRectangle,
            tint,
            rotation,
            origin,
            scale,
            effects,
            0f);
    }

    private void DrawSpriteFrameShadow(
        LoadedSpriteFrame frame,
        Vector2 position,
        Color tint,
        float rotation,
        Vector2 origin,
        Vector2 scale,
        SpriteEffects effects = SpriteEffects.None)
    {
        if (!OperatingSystem.IsBrowser() && _spriteDropShadowEnabled && tint.A > 0)
        {
            var shadowAlpha = ((tint.A / 255f) * 0.32f);
            var shadowTint = new Color(0, 0, 0) * shadowAlpha;
            _spriteBatch.Draw(
                frame.Texture,
                position + new Vector2(1f, 1f),
                frame.SourceRectangle,
                shadowTint,
                rotation,
                origin,
                scale,
                effects,
                0f);
        }
    }

    private void DrawSpriteFrameOutline(
        LoadedSpriteFrame frame,
        Vector2 position,
        Color outlineTint,
        float rotation,
        Vector2 origin,
        Vector2 scale,
        SpriteEffects effects = SpriteEffects.None)
    {
        var mask = GetSpriteFrameAlphaMask(frame);
        var outlineOffsets = new[]
        {
            new Vector2(0f, -2f),
            new Vector2(0f, 2f),
            new Vector2(-2f, 0f),
            new Vector2(2f, 0f),
            new Vector2(-2f, -2f),
            new Vector2(-2f, 2f),
            new Vector2(2f, -2f),
            new Vector2(2f, 2f),
        };

        foreach (var offset in outlineOffsets)
        {
            _spriteBatch.Draw(
                mask,
                position + offset,
                null,
                outlineTint,
                rotation,
                origin,
                scale,
                effects,
                0f);
        }
    }

    private void DrawSpriteFrameFlatColor(
        LoadedSpriteFrame frame,
        Vector2 position,
        Color tint,
        float rotation,
        Vector2 origin,
        Vector2 scale,
        SpriteEffects effects = SpriteEffects.None)
    {
        var mask = GetSpriteFrameAlphaMask(frame);
        _spriteBatch.Draw(
            mask,
            position,
            null,
            tint,
            rotation,
            origin,
            scale,
            effects,
            0f);
    }

    private void DrawSpriteFrameMultiplyColor(
        LoadedSpriteFrame frame,
        Vector2 position,
        Color tint,
        float rotation,
        Vector2 origin,
        Vector2 scale,
        SpriteEffects effects = SpriteEffects.None)
    {
        var mask = GetSpriteFrameAlphaMask(frame);
        _spriteBatch.End();
        _spriteBatch.Begin(
            SpriteSortMode.Deferred,
            _multiplyColorBlendState,
            samplerState: SamplerState.PointClamp,
            rasterizerState: RasterizerState.CullNone);
        _spriteBatch.Draw(
            mask,
            position,
            null,
            tint,
            rotation,
            origin,
            scale,
            effects,
            0f);
        _spriteBatch.End();
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp, rasterizerState: RasterizerState.CullNone);
    }

    private void DrawSpriteFrameScreenColor(
        LoadedSpriteFrame frame,
        Vector2 position,
        Color tint,
        float rotation,
        Vector2 origin,
        Vector2 scale,
        SpriteEffects effects = SpriteEffects.None)
    {
        var mask = GetSpriteFrameAlphaMask(frame);
        _spriteBatch.End();
        _spriteBatch.Begin(
            SpriteSortMode.Deferred,
            _screenColorBlendState,
            samplerState: SamplerState.PointClamp,
            rasterizerState: RasterizerState.CullNone);
        _spriteBatch.Draw(
            mask,
            position,
            null,
            tint,
            rotation,
            origin,
            scale,
            effects,
            0f);
        _spriteBatch.End();
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp, rasterizerState: RasterizerState.CullNone);
    }

    private Texture2D GetSpriteFrameAlphaMask(LoadedSpriteFrame frame)
    {
        if (_spriteFrameAlphaMaskCache.TryGetValue(frame, out var cachedMask))
        {
            return cachedMask;
        }

        var sourceRectangle = frame.SourceRectangle ?? new Rectangle(0, 0, frame.Texture.Width, frame.Texture.Height);
        var mask = new Texture2D(GraphicsDevice, sourceRectangle.Width, sourceRectangle.Height);
        var pixels = new Color[sourceRectangle.Width * sourceRectangle.Height];
        if (!frame.TryCopyPixelData(pixels))
        {
            frame.Texture.GetData(0, sourceRectangle, pixels, 0, pixels.Length);
        }

        for (var i = 0; i < pixels.Length; i += 1)
        {
            pixels[i] = pixels[i].A == byte.MaxValue
                ? new Color(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue)
                : Color.Transparent;
        }

        mask.SetData(pixels);
        _spriteFrameAlphaMaskCache[frame] = mask;
        return mask;
    }

    private void DrawLoadedSpriteFrame(
        LoadedSpriteFrame frame,
        Vector2 position,
        Rectangle? sourceRectangle,
        Color tint,
        float rotation,
        Vector2 origin,
        Vector2 scale,
        SpriteEffects effects,
        float layerDepth)
    {
        _spriteBatch.Draw(
            frame.Texture,
            position,
            CombineSourceRectangles(frame.SourceRectangle, sourceRectangle),
            tint,
            rotation,
            origin,
            scale,
            effects,
            layerDepth);
    }

    private void DrawLoadedSpriteFrame(LoadedSpriteFrame frame, Rectangle destinationRectangle, Color tint)
    {
        _spriteBatch.Draw(
            frame.Texture,
            destinationRectangle,
            frame.SourceRectangle,
            tint);
    }

    private static Rectangle? CombineSourceRectangles(Rectangle? frameSourceRectangle, Rectangle? requestedSourceRectangle)
    {
        if (requestedSourceRectangle is null)
        {
            return frameSourceRectangle;
        }

        if (frameSourceRectangle is null)
        {
            return requestedSourceRectangle;
        }

        var requested = requestedSourceRectangle.Value;
        var frameSource = frameSourceRectangle.Value;
        return new Rectangle(
            frameSource.X + requested.X,
            frameSource.Y + requested.Y,
            requested.Width,
            requested.Height);
    }

    private static float GetVelocityRotation(float velocityX, float velocityY)
    {
        return MathF.Atan2(velocityY, velocityX);
    }

    private static float GetTravelRotation(float previousX, float previousY, float x, float y)
    {
        return MathF.Atan2(y - previousY, x - previousX);
    }
}
