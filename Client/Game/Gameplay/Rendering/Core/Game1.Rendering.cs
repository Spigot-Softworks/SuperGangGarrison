#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    public readonly Dictionary<LoadedSpriteFrame, Texture2D> _spriteFrameAlphaMaskCache = new();
    public static readonly BlendState _multiplyColorBlendState = new()
    {
        ColorSourceBlend = Blend.DestinationColor,
        ColorDestinationBlend = Blend.InverseSourceAlpha,
        ColorBlendFunction = BlendFunction.Add,
        AlphaSourceBlend = Blend.Zero,
        AlphaDestinationBlend = Blend.One,
        AlphaBlendFunction = BlendFunction.Add,
    };

    public static readonly BlendState _screenColorBlendState = new()
    {
        ColorSourceBlend = Blend.SourceAlpha,
        ColorDestinationBlend = Blend.One,
        ColorBlendFunction = BlendFunction.Add,
        AlphaSourceBlend = Blend.One,
        AlphaDestinationBlend = Blend.One,
        AlphaBlendFunction = BlendFunction.Add,
    };

    public static float RoundToSourcePixel(float value)
    {
        return MathF.Round(value, MidpointRounding.AwayFromZero);
    }

    private static Vector2 RoundToSourcePixels(Vector2 value)
    {
        return new Vector2(
            RoundToSourcePixel(value.X),
            RoundToSourcePixel(value.Y));
    }

    private Vector2 GetWorldScreenPosition(float worldX, float worldY, Vector2 cameraPosition)
    {
        var position = new Vector2(worldX - cameraPosition.X, worldY - cameraPosition.Y);
        return _smoothCameraRenderingActive
            ? position
            : RoundToSourcePixels(position);
    }

    private Vector2 GetWorldScreenPosition(Vector2 worldPosition, Vector2 cameraPosition)
    {
        return GetWorldScreenPosition(worldPosition.X, worldPosition.Y, cameraPosition);
    }

    /// <summary>
    /// World → screen for HUD-layer draws. World sprite batches already apply
    /// <see cref="GameplayCameraZoom"/> via transform; HUD batches do not.
    /// </summary>
    public Vector2 GetWorldHudScreenPosition(float worldX, float worldY, Vector2 cameraPosition)
        => (GetWorldScreenPosition(worldX, worldY, cameraPosition) * GameplayCameraZoom)
            + SubpixelWorld.GetHudOffset(GameplayCameraZoom);

    public Vector2 GetWorldHudScreenPosition(Vector2 worldPosition, Vector2 cameraPosition)
        => GetWorldHudScreenPosition(worldPosition.X, worldPosition.Y, cameraPosition);

    private void DrawScreenPixelRectangle(Vector2 position, float width, float height, Color color)
    {
        if (width <= 0f || height <= 0f)
        {
            return;
        }

        _spriteBatch.Draw(
            _pixel,
            position,
            null,
            color,
            0f,
            Vector2.Zero,
            new Vector2(width, height),
            SpriteEffects.None,
            0f);
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

    private bool DrawLevelBackground(Vector2 cameraPosition)
    {
        var position = GetWorldScreenPosition(Vector2.Zero, cameraPosition);
        var scale = new Vector2(_world.Bounds.Width, _world.Bounds.Height);
        if (TryGetLevelBackgroundTexture(out var background))
        {
            scale = new Vector2(
                background.Width > 0 ? _world.Bounds.Width / background.Width : 1f,
                background.Height > 0 ? _world.Bounds.Height / background.Height : 1f);
            _spriteBatch.Draw(
                background,
                position,
                null,
                Color.White,
                0f,
                Vector2.Zero,
                scale,
                SpriteEffects.None,
                0f);
            return true;
        }

        DrawScreenPixelRectangle(position, scale.X, scale.Y, new Color(34, 44, 60));
        return false;
    }

    public bool TryGetLevelBackgroundTexture(out Texture2D texture)
    {
        var backgroundName = _world.Level.BackgroundAssetName;
        if (TryGetLevelBackgroundFileTexture(backgroundName, out texture))
        {
            return true;
        }

        if (_runtimeAssets is not null
            && !string.IsNullOrWhiteSpace(backgroundName)
            && _runtimeAssets.GetBackground(backgroundName) is { } runtimeTexture)
        {
            texture = runtimeTexture;
            return true;
        }

        texture = null!;
        return false;
    }


    private bool TryGetLevelBackgroundFileTexture(string? backgroundName, out Texture2D texture)
    {
        EnsureLevelBackgroundFileCacheLevel(_world.Level);
        if (string.IsNullOrWhiteSpace(backgroundName))
        {
            texture = null!;
            return false;
        }

        if (!Path.IsPathRooted(backgroundName) && !backgroundName.Contains(Path.DirectorySeparatorChar) && !backgroundName.Contains(Path.AltDirectorySeparatorChar))
        {
            texture = null!;
            return false;
        }

        if (string.Equals(_levelBackgroundResources.FailedPath, backgroundName, StringComparison.OrdinalIgnoreCase))
        {
            texture = null!;
            return false;
        }

        if (!string.Equals(_levelBackgroundResources.TexturePath, backgroundName, StringComparison.OrdinalIgnoreCase))
        {
            _levelBackgroundResources.Texture?.Dispose();
            _levelBackgroundResources.Texture = null;
            _levelBackgroundResources.TexturePath = null;

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
                    _levelBackgroundResources.FailedPath = backgroundName;
                    texture = null!;
                    return false;
                }

                _levelBackgroundResources.Texture = TextureDecodeUtility.LoadTexture(GraphicsDevice, bytes, applyLegacyChromaKey: false);
                _levelBackgroundResources.TexturePath = backgroundName;
                _levelBackgroundResources.FailedPath = null;
            }
            catch (IOException)
            {
                _levelBackgroundResources.FailedPath = backgroundName;
                texture = null!;
                return false;
            }
            catch (InvalidOperationException)
            {
                _levelBackgroundResources.FailedPath = backgroundName;
                texture = null!;
                return false;
            }
            catch (NotSupportedException)
            {
                _levelBackgroundResources.FailedPath = backgroundName;
                texture = null!;
                return false;
            }
        }

        if (_levelBackgroundResources.Texture is null)
        {
            texture = null!;
            return false;
        }

        texture = _levelBackgroundResources.Texture;
        return true;
    }

    private void EnsureLevelBackgroundFileCacheLevel(SimpleLevel level)
    {
        if (!ShouldInvalidateLevelBackgroundFileCache(_levelBackgroundResources.TextureLevel, level))
        {
            return;
        }

        _levelBackgroundResources.Texture?.Dispose();
        _levelBackgroundResources.Texture = null;
        _levelBackgroundResources.TexturePath = null;
        _levelBackgroundResources.FailedPath = null;
        _levelBackgroundResources.TextureLevel = level;
    }

    internal static bool ShouldInvalidateLevelBackgroundFileCache(SimpleLevel? cachedLevel, SimpleLevel currentLevel)
    {
        return !ReferenceEquals(cachedLevel, currentLevel);
    }

    /// <summary>
    /// Loads a background for a temporary map preview without touching the
    /// single cached texture used by the active gameplay map.
    /// </summary>
    private bool TryLoadIndependentLevelBackgroundTexture(string? backgroundName, out Texture2D texture)
    {
        if (string.IsNullOrWhiteSpace(backgroundName)
            || (!Path.IsPathRooted(backgroundName)
                && !backgroundName.Contains(Path.DirectorySeparatorChar)
                && !backgroundName.Contains(Path.AltDirectorySeparatorChar)))
        {
            texture = null!;
            return false;
        }

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
                texture = null!;
                return false;
            }

            texture = TextureDecodeUtility.LoadTexture(GraphicsDevice, bytes, applyLegacyChromaKey: false);
            return true;
        }
        catch (IOException)
        {
            texture = null!;
            return false;
        }
        catch (InvalidOperationException)
        {
            texture = null!;
            return false;
        }
        catch (NotSupportedException)
        {
            texture = null!;
            return false;
        }
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
        if (!UseReducedBrowserEffects
            && !_world.Level.IsTopDown
            && _gameplayManager.RuntimeSettings.SpriteDropShadowEnabled
            && tint.A > 0
            && !ShouldReplaceSpriteDropShadow)
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
        ObserveCharacterSpriteDraw(frame.Texture, frame.SourceRectangle, position, tint, rotation, origin, scale, effects);
    }

    public void DrawSpriteFrame(
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
        ObserveCharacterSpriteDraw(frame.Texture, frame.SourceRectangle, position, tint, rotation, origin, scale, effects);
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
        if (!UseReducedBrowserEffects
            && !_world.Level.IsTopDown
            && _gameplayManager.RuntimeSettings.SpriteDropShadowEnabled
            && tint.A > 0
            && !ShouldReplaceSpriteDropShadow)
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

    private void DrawTopDownPlayerShadow(
        PlayerEntity player,
        Vector2 renderPosition,
        Vector2 cameraPosition,
        Color tint)
    {
        if (UseReducedBrowserEffects
            || !_world.Level.IsTopDown
            || !_gameplayManager.RuntimeSettings.SpriteDropShadowEnabled
            || tint.A <= 0)
        {
            return;
        }

        var playerScale = player.PlayerScale;
        var shadowWidth = Math.Max(2, (int)MathF.Round(player.Width * 1.15f));
        var shadowHeight = Math.Max(2, (int)MathF.Round(5f * playerScale));
        var feet = GetPlayerSpriteScreenOrigin(
            new Vector2(
                renderPosition.X,
                renderPosition.Y
                    + (player.Height * 0.5f)
                    + (shadowHeight * 0.5f)
                    + 9f),
            cameraPosition);
        var shadowRectangle = new Rectangle(
            (int)MathF.Round(feet.X - (shadowWidth * 0.5f)),
            (int)MathF.Round(feet.Y - shadowHeight),
            shadowWidth,
            shadowHeight);
        var shadowAlpha = (tint.A / 255f) * 0.32f;
        _spriteBatch.Draw(_pixel, shadowRectangle, Color.Black * shadowAlpha);
    }

    private static readonly Vector2[] SpriteFrameOutlineOffsets =
    {
        new(0f, -2f),
        new(0f, 2f),
        new(-2f, 0f),
        new(2f, 0f),
        new(-2f, -2f),
        new(-2f, 2f),
        new(2f, -2f),
        new(2f, 2f),
    };

    private void DrawSpriteFrameOutline(
        LoadedSpriteFrame frame,
        Vector2 position,
        Color outlineTint,
        float rotation,
        Vector2 origin,
        Vector2 scale,
        SpriteEffects effects = SpriteEffects.None,
        IReadOnlyList<Vector2>? outlineOffsets = null)
    {
        var mask = GetSpriteFrameAlphaMask(frame);
        var offsets = outlineOffsets ?? SpriteFrameOutlineOffsets;

        foreach (var offset in offsets)
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
        if (!IsSpriteFrameVisibleInCurrentViewport(frame, position, rotation, origin, scale))
        {
            return;
        }

        var mask = GetSpriteFrameAlphaMask(frame);
        _spriteBatch.End();
        _spriteBatch.Begin(
            SpriteSortMode.Deferred,
            _multiplyColorBlendState,
            samplerState: SamplerState.PointClamp,
            rasterizerState: RasterizerState.CullNone,
            transformMatrix: GetActiveGameplayWorldSpriteBatchTransform());
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
        _spriteBatch.Begin(
            samplerState: SamplerState.PointClamp,
            rasterizerState: RasterizerState.CullNone,
            transformMatrix: GetActiveGameplayWorldSpriteBatchTransform());
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
        if (!IsSpriteFrameVisibleInCurrentViewport(frame, position, rotation, origin, scale))
        {
            return;
        }

        var mask = GetSpriteFrameAlphaMask(frame);
        _spriteBatch.End();
        _spriteBatch.Begin(
            SpriteSortMode.Deferred,
            _screenColorBlendState,
            samplerState: SamplerState.PointClamp,
            rasterizerState: RasterizerState.CullNone,
            transformMatrix: GetActiveGameplayWorldSpriteBatchTransform());
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
        _spriteBatch.Begin(
            samplerState: SamplerState.PointClamp,
            rasterizerState: RasterizerState.CullNone,
            transformMatrix: GetActiveGameplayWorldSpriteBatchTransform());
    }

    private bool IsSpriteFrameVisibleInCurrentViewport(
        LoadedSpriteFrame frame,
        Vector2 position,
        float rotation,
        Vector2 origin,
        Vector2 scale)
    {
        var viewport = GraphicsDevice.Viewport;
        return SpriteBatchQuadViewportVisibility.Intersects(
            position,
            frame.Width,
            frame.Height,
            rotation,
            origin,
            scale,
            GetActiveGameplayWorldSpriteBatchTransform(),
            viewport);
    }

    private Texture2D GetSpriteFrameAlphaMask(LoadedSpriteFrame frame)
    {
        if (_spriteFrameAlphaMaskCache.TryGetValue(frame, out var cachedMask))
        {
            return cachedMask;
        }

        var assetTraceStartTimestamp = ClientAssetLoadDiagnostics.StartTimestamp();
        var sourceRectangle = frame.SourceRectangle ?? new Rectangle(0, 0, frame.Texture.Width, frame.Texture.Height);
        var mask = new Texture2D(GraphicsDevice, sourceRectangle.Width, sourceRectangle.Height);
        var pixels = new Color[sourceRectangle.Width * sourceRectangle.Height];
        var usedGpuReadback = false;
        if (!frame.TryCopyPixelData(pixels))
        {
            usedGpuReadback = true;
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
        if (assetTraceStartTimestamp > 0L)
        {
            ClientAssetLoadDiagnostics.RecordOnce(
                "alpha-mask",
                GetSpriteFrameAssetTraceIdentity(frame),
                FormattableString.Invariant($"size={sourceRectangle.Width}x{sourceRectangle.Height} pixelSource={(usedGpuReadback ? "gpu-readback" : "retained-cpu")}"),
                assetTraceStartTimestamp);
        }

        return mask;
    }

    private LoadedSpriteFrame GetNeutralSpriteFrame(LoadedSpriteFrame frame)
    {
        if (_spriteFrameCacheResources.NeutralSpriteFrameCache.TryGetValue(frame, out var cachedFrame))
        {
            return cachedFrame;
        }

        var assetTraceStartTimestamp = ClientAssetLoadDiagnostics.StartTimestamp();
        var sourceRectangle = frame.SourceRectangle ?? new Rectangle(0, 0, frame.Texture.Width, frame.Texture.Height);
        var pixels = new Color[sourceRectangle.Width * sourceRectangle.Height];
        var usedGpuReadback = false;
        if (!frame.TryCopyPixelData(pixels))
        {
            usedGpuReadback = true;
            frame.Texture.GetData(0, sourceRectangle, pixels, 0, pixels.Length);
        }

        for (var index = 0; index < pixels.Length; index += 1)
        {
            var pixel = pixels[index];
            var luminance = (byte)Math.Clamp(
                (int)MathF.Round((pixel.R * 0.299f) + (pixel.G * 0.587f) + (pixel.B * 0.114f)),
                0,
                byte.MaxValue);
            pixels[index] = new Color(luminance, luminance, luminance, pixel.A);
        }

        var texture = new Texture2D(GraphicsDevice, sourceRectangle.Width, sourceRectangle.Height);
        texture.SetData(pixels);
        var neutralFrame = new LoadedSpriteFrame(
            texture,
            PixelSource: new LoadedSpriteFramePixelSource(pixels, sourceRectangle.Width, sourceRectangle.Height));
        _spriteFrameCacheResources.NeutralSpriteFrameCache[frame] = neutralFrame;
        if (assetTraceStartTimestamp > 0L)
        {
            ClientAssetLoadDiagnostics.RecordOnce(
                "neutral-frame",
                GetSpriteFrameAssetTraceIdentity(frame),
                FormattableString.Invariant($"size={sourceRectangle.Width}x{sourceRectangle.Height} pixelSource={(usedGpuReadback ? "gpu-readback" : "retained-cpu")}"),
                assetTraceStartTimestamp);
        }

        return neutralFrame;
    }

    private static string GetSpriteFrameAssetTraceIdentity(LoadedSpriteFrame frame)
    {
        var source = frame.SourceRectangle is { } rectangle
            ? $"{rectangle.X},{rectangle.Y},{rectangle.Width},{rectangle.Height}"
            : "full";
        return $"frame-{RuntimeHelpers.GetHashCode(frame):X8}:{source}";
    }

    public void DrawLoadedSpriteFrame(
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
            ApplyCurrentHudElementOpacity(tint),
            rotation,
            origin,
            scale,
            effects,
            layerDepth);
    }

    public void DrawLoadedSpriteFrame(LoadedSpriteFrame frame, Rectangle destinationRectangle, Color tint)
    {
        _spriteBatch.Draw(
            frame.Texture,
            destinationRectangle,
            frame.SourceRectangle,
            ApplyCurrentHudElementOpacity(tint));
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

}

/// <summary>
/// Conservative viewport test for the transformed quad SpriteBatch draws.
/// Coordinates are relative to the viewport origin: MonoGame's SpriteEffect
/// projection spans (0, 0) to (Viewport.Width, Viewport.Height), while the
/// rasterizer applies the viewport's X/Y offset afterward.
/// </summary>
internal static class SpriteBatchQuadViewportVisibility
{
    private const float ViewportSafetyMarginPixels = 2f;

    internal static bool Intersects(
        Vector2 position,
        int width,
        int height,
        float rotation,
        Vector2 origin,
        Vector2 scale,
        Matrix? transformMatrix,
        Viewport viewport)
    {
        // Invalid dimensions or coordinates should retain the existing draw
        // path. This helper is only allowed to reject a quad when disjointness
        // is certain.
        if (width <= 0 || height <= 0
            || viewport.Width <= 0 || viewport.Height <= 0
            || !IsFinite(position) || !IsFinite(origin) || !IsFinite(scale)
            || !float.IsFinite(rotation))
        {
            return true;
        }

        var matrix = transformMatrix ?? Matrix.Identity;
        if (!IsFiniteTransform(matrix) || !IsTwoDimensionalAffineTransform(matrix))
        {
            return true;
        }

        var sin = MathF.Sin(rotation);
        var cos = MathF.Cos(rotation);
        var left = -origin.X * scale.X;
        var top = -origin.Y * scale.Y;
        var right = left + (width * scale.X);
        var bottom = top + (height * scale.Y);

        // Match SpriteBatch's rotation around position and its signed scale.
        // SpriteEffects only flips texture coordinates, not quad geometry.
        Span<Vector2> corners = stackalloc Vector2[4]
        {
            TransformCorner(position, left, top, sin, cos, matrix),
            TransformCorner(position, right, top, sin, cos, matrix),
            TransformCorner(position, left, bottom, sin, cos, matrix),
            TransformCorner(position, right, bottom, sin, cos, matrix),
        };

        var minX = float.PositiveInfinity;
        var minY = float.PositiveInfinity;
        var maxX = float.NegativeInfinity;
        var maxY = float.NegativeInfinity;
        foreach (var corner in corners)
        {
            if (!IsFinite(corner))
            {
                return true;
            }

            minX = MathF.Min(minX, corner.X);
            minY = MathF.Min(minY, corner.Y);
            maxX = MathF.Max(maxX, corner.X);
            maxY = MathF.Max(maxY, corner.Y);
        }

        return !(maxX < -ViewportSafetyMarginPixels
            || minX > viewport.Width + ViewportSafetyMarginPixels
            || maxY < -ViewportSafetyMarginPixels
            || minY > viewport.Height + ViewportSafetyMarginPixels);
    }

    private static Vector2 TransformCorner(
        Vector2 position,
        float offsetX,
        float offsetY,
        float sin,
        float cos,
        Matrix transformMatrix)
    {
        var corner = new Vector2(
            position.X + (offsetX * cos) - (offsetY * sin),
            position.Y + (offsetX * sin) + (offsetY * cos));
        return Vector2.Transform(corner, transformMatrix);
    }

    private static bool IsFiniteTransform(Matrix matrix)
        => float.IsFinite(matrix.M11)
            && float.IsFinite(matrix.M12)
            && float.IsFinite(matrix.M13)
            && float.IsFinite(matrix.M14)
            && float.IsFinite(matrix.M21)
            && float.IsFinite(matrix.M22)
            && float.IsFinite(matrix.M23)
            && float.IsFinite(matrix.M24)
            && float.IsFinite(matrix.M31)
            && float.IsFinite(matrix.M32)
            && float.IsFinite(matrix.M33)
            && float.IsFinite(matrix.M34)
            && float.IsFinite(matrix.M41)
            && float.IsFinite(matrix.M42)
            && float.IsFinite(matrix.M43)
            && float.IsFinite(matrix.M44);

    private static bool IsTwoDimensionalAffineTransform(Matrix matrix)
        => matrix.M13 == 0f
            && matrix.M14 == 0f
            && matrix.M23 == 0f
            && matrix.M24 == 0f
            && matrix.M31 == 0f
            && matrix.M32 == 0f
            && matrix.M33 == 1f
            && matrix.M34 == 0f
            && matrix.M43 == 0f
            && matrix.M44 == 1f;

    private static bool IsFinite(Vector2 value)
        => float.IsFinite(value.X) && float.IsFinite(value.Y);
}
