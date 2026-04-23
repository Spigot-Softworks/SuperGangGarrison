#nullable enable

using OpenGarrison.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace OpenGarrison.Client;

public partial class Game1
{
    private readonly System.Collections.Generic.Dictionary<LoadedSpriteFrame, Vector2> _spriteFrameCenterOfMassCache = new();

    private void DrawMedicBeams(Vector2 cameraPosition)
    {
        foreach (var player in EnumerateRenderablePlayers())
        {
            DrawMedicBeamForPlayer(player, cameraPosition);
        }
    }

    private void DrawMedicBeamForPlayer(PlayerEntity medic, Vector2 cameraPosition)
    {
        if (medic.ClassId != PlayerClass.Medic
            || !medic.IsMedicHealing
            || !medic.MedicHealTargetId.HasValue)
        {
            return;
        }

        var healTarget = FindPlayerById(medic.MedicHealTargetId.Value);
        if (healTarget is null || !healTarget.IsAlive)
        {
            return;
        }

        var aimRadians = MathF.PI * medic.AimDirectionDegrees / 180f;
        var aimDirection = new Vector2(MathF.Cos(aimRadians), MathF.Sin(aimRadians));
        var beamOrigin = GetMedicBeamOrigin(medic);
        var beamColor = healTarget.Team == PlayerTeam.Blue
            ? new Color(0, 20, 180, 90)
            : new Color(120, 5, 5, 90);
        var beamStartColor = healTarget.Team == PlayerTeam.Blue
            ? new Color(80, 160, 255, 240)
            : new Color(255, 95, 95, 245);
        // Helix uses an even lighter start to distinguish it from the main beam
        var helixStartColor = healTarget.Team == PlayerTeam.Blue
            ? new Color(140, 195, 255, 190)
            : new Color(255, 175, 175, 200);
        var beamStartX = beamOrigin.X + aimDirection.X * 24f;
        var beamStartY = beamOrigin.Y + aimDirection.Y * 24f;
        DrawCurvedWorldLine(
            beamStartX,
            beamStartY,
            healTarget.X,
            healTarget.Y,
            cameraPosition,
            beamStartColor,
            beamColor,
            nozzleThickness: 4f,
            maxThickness: 8f,
            tailThickness: 2f,
            rampDistPixels: 8f,
            aimDirection);
        DrawMedicBeamHelix(
            beamStartX,
            beamStartY,
            healTarget.X,
            healTarget.Y,
            cameraPosition,
            aimDirection,
            helixStartColor,
            beamColor);
    }

    private void DrawMedicBeamHelix(
        float startX, float startY,
        float endX, float endY,
        Vector2 cameraPosition,
        Vector2 aimDirection,
        Color helixStartColor,
        Color helixEndColor)
    {
        const int steps = 64;
        const float maxRadius = 6f;
        const float helixTurns = 2.5f;
        const float helixFrequency = helixTurns * MathF.PI * 2f;
        const float pixelSize = 2f;

        var start = new Vector2(startX - cameraPosition.X, startY - cameraPosition.Y);
        var end = new Vector2(endX - cameraPosition.X, endY - cameraPosition.Y);
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
            var offsetAmount = distToTarget * 0.12f * (turnAngle / MathF.PI);
            controlPoint += perpToAim * offsetAmount;
        }

        var phaseAtOrigin = _medigunBeamHelixPhase;
        // cell → (alpha, color): keep entry with highest alpha
        var pixelatedCells = new System.Collections.Generic.Dictionary<(int, int), (float alpha, Color color)>();

        // Two strands, 180° apart
        for (int strand = 0; strand < 2; strand++)
        {
            var strandOffset = strand * MathF.PI;
            Vector2? prevPos = null;
            float prevAlpha = 0f;
            float prevT = 0f;

            for (int i = 0; i <= steps; i++)
            {
                var t = (float)i / steps;
                var oneMinusT = 1f - t;

                var bezierPos = oneMinusT * oneMinusT * start
                              + 2f * oneMinusT * t * controlPoint
                              + t * t * end;

                var tangent = 2f * oneMinusT * (controlPoint - start) + 2f * t * (end - controlPoint);
                var tangentLen = tangent.Length();
                if (tangentLen < 0.01f) { prevPos = null; continue; }
                tangent /= tangentLen;
                var perp = new Vector2(-tangent.Y, tangent.X);

                var radius = maxRadius * oneMinusT;
                var alpha = oneMinusT;

                // Pin to beam centre at origin via sin difference
                var angle = phaseAtOrigin + t * helixFrequency + strandOffset;
                var angleAtOrigin = phaseAtOrigin + strandOffset;
                var sineOffset = MathF.Sin(angle) - MathF.Sin(angleAtOrigin);

                var pos = bezierPos + perp * (sineOffset * radius);

                // Interpolate from previous sample to fill any skipped grid cells
                if (prevPos.HasValue)
                {
                    var delta = pos - prevPos.Value;
                    var segLen = delta.Length();
                    int substeps = Math.Max(1, (int)MathF.Ceiling(segLen / pixelSize));
                    for (int s = 0; s <= substeps; s++)
                    {
                        var frac = (float)s / substeps;
                        var interp = prevPos.Value + delta * frac;
                        var interpT = prevT + (t - prevT) * frac;
                        var interpAlpha = prevAlpha + (alpha - prevAlpha) * frac;
                        var cellColor = Color.Lerp(helixStartColor, helixEndColor, interpT) * interpAlpha;
                        var gx = (int)MathF.Floor(interp.X / pixelSize);
                        var gy = (int)MathF.Floor(interp.Y / pixelSize);
                        var key = (gx, gy);
                        if (!pixelatedCells.TryGetValue(key, out var existing) || interpAlpha > existing.alpha)
                            pixelatedCells[key] = (interpAlpha, cellColor);
                    }
                }
                else
                {
                    var cellColor = Color.Lerp(helixStartColor, helixEndColor, t) * alpha;
                    var gx = (int)MathF.Floor(pos.X / pixelSize);
                    var gy = (int)MathF.Floor(pos.Y / pixelSize);
                    var key = (gx, gy);
                    if (!pixelatedCells.TryGetValue(key, out var existing) || alpha > existing.alpha)
                        pixelatedCells[key] = (alpha, cellColor);
                }

                prevPos = pos;
                prevAlpha = alpha;
                prevT = t;
            }
        }

        foreach (var ((gridX, gridY), (_, cellColor)) in pixelatedCells)
        {
            var pixelRect = new Rectangle(
                gridX * (int)pixelSize,
                gridY * (int)pixelSize,
                (int)pixelSize,
                (int)pixelSize);
            _spriteBatch.Draw(_pixel, pixelRect, cellColor);
        }
    }

    private Vector2 GetMedicBeamOrigin(PlayerEntity medic)
    {
        var renderPosition = GetRenderPosition(medic, allowInterpolation: !ReferenceEquals(medic, _world.LocalPlayer));
        var roundedOrigin = GetRoundedPlayerSpriteOrigin(renderPosition);
        var weaponDefinition = GetWeaponRenderDefinition(medic);
        if (weaponDefinition.NormalSpriteName is null)
        {
            return roundedOrigin;
        }

        var sprite = GetResolvedSprite(weaponDefinition.NormalSpriteName);
        if (sprite is null)
        {
            return roundedOrigin;
        }

        var bodySelection = GetPlayerBodySpriteSelection(medic);
        var anchorOrigin = GetWeaponAnchorOrigin(weaponDefinition, sprite);
        var facingScale = GetPlayerFacingScale(medic);
        return new Vector2(
            roundedOrigin.X + (weaponDefinition.XOffset + anchorOrigin.X) * facingScale,
            roundedOrigin.Y + weaponDefinition.YOffset + bodySelection.EquipmentOffset + anchorOrigin.Y);
    }

    private void DrawGameplayEffectsAndProjectiles(Vector2 cameraPosition)
    {
        DrawExplosionVisuals(cameraPosition);
        DrawImpactVisuals(cameraPosition);
        DrawLooseSheetVisuals(cameraPosition);
        if (_gibLevel > 0)
        {
            DrawBloodVisuals(cameraPosition);
        }

        DrawShellVisuals(cameraPosition);

        if (_particleMode != 1)
        {
            DrawRocketSmokeVisuals(cameraPosition);
            DrawMineTrailVisuals(cameraPosition);
            DrawWallspinDustVisuals(cameraPosition);
            DrawBlastJumpFlameVisuals(cameraPosition);
            DrawFlameSmokeVisuals(cameraPosition);
        }

        DrawSniperTracers(cameraPosition);

        foreach (var shot in _world.Shots)
        {
            DrawShotProjectile(shot, cameraPosition, new Color(130, 185, 255), new Color(255, 210, 140));
        }

        foreach (var bubble in _world.Bubbles)
        {
            DrawBubbleProjectile(bubble, cameraPosition);
        }

        foreach (var blade in _world.Blades)
        {
            DrawBladeProjectile(blade, cameraPosition);
        }

        foreach (var shot in _world.RevolverShots)
        {
            DrawShotProjectile(shot, cameraPosition, new Color(140, 210, 255), new Color(255, 235, 170));
        }

        foreach (var stabMask in _world.StabMasks)
        {
            DrawStabMask(stabMask, cameraPosition);
        }

        foreach (var needle in _world.Needles)
        {
            DrawNeedleProjectile(needle, cameraPosition);
        }

        if (_flameRenderMode == 0)
        {
            DrawFlameProjectiles(cameraPosition);
        }
        else
        {
            foreach (var flame in _world.Flames)
            {
                DrawFlameProjectile(flame, cameraPosition);
            }
        }

        foreach (var flare in _world.Flares)
        {
            DrawFlareProjectile(flare, cameraPosition);
        }

        foreach (var rocket in _world.Rockets)
        {
            DrawRocketProjectile(rocket, cameraPosition);
        }

        foreach (var mine in _world.Mines)
        {
            DrawMineProjectile(mine, cameraPosition);
        }
    }

    private void DrawShotProjectile(ShotProjectileEntity shot, Vector2 cameraPosition, Color blueColor, Color redColor)
    {
        var renderPosition = GetRenderPosition(shot.Id, shot.X, shot.Y);
        var shotColor = shot.Team == PlayerTeam.Blue ? blueColor : redColor;
        if (!TryDrawSprite("ShotS", 0, renderPosition.X, renderPosition.Y, cameraPosition, shotColor, GetVelocityRotation(shot.VelocityX, shot.VelocityY)))
        {
            var shotRectangle = new Rectangle(
                (int)(renderPosition.X - 2f - cameraPosition.X),
                (int)(renderPosition.Y - 2f - cameraPosition.Y),
                4,
                4);
            _spriteBatch.Draw(_pixel, shotRectangle, shotColor);
        }
    }

    private void DrawShotProjectile(RevolverProjectileEntity shot, Vector2 cameraPosition, Color blueColor, Color redColor)
    {
        var renderPosition = GetRenderPosition(shot.Id, shot.X, shot.Y);
        var shotColor = shot.Team == PlayerTeam.Blue ? blueColor : redColor;
        if (!TryDrawSprite("ShotS", 0, renderPosition.X, renderPosition.Y, cameraPosition, shotColor, GetVelocityRotation(shot.VelocityX, shot.VelocityY)))
        {
            var shotRectangle = new Rectangle(
                (int)(renderPosition.X - 2f - cameraPosition.X),
                (int)(renderPosition.Y - 2f - cameraPosition.Y),
                4,
                4);
            _spriteBatch.Draw(_pixel, shotRectangle, shotColor);
        }
    }

    private void DrawNeedleProjectile(NeedleProjectileEntity needle, Vector2 cameraPosition)
    {
        var renderPosition = GetRenderPosition(needle.Id, needle.X, needle.Y);
        var needleColor = needle.Team == PlayerTeam.Blue
            ? new Color(150, 220, 255)
            : new Color(240, 240, 220);
        if (!TryDrawSprite("NeedleS", 0, renderPosition.X, renderPosition.Y, cameraPosition, needleColor, GetVelocityRotation(needle.VelocityX, needle.VelocityY)))
        {
            var needleRectangle = new Rectangle(
                (int)(renderPosition.X - 3f - cameraPosition.X),
                (int)(renderPosition.Y - 1f - cameraPosition.Y),
                6,
                2);
            _spriteBatch.Draw(_pixel, needleRectangle, needleColor);
        }
    }

    private void DrawBubbleProjectile(BubbleProjectileEntity bubble, Vector2 cameraPosition)
    {
        var renderPosition = GetRenderPosition(bubble.Id, bubble.X, bubble.Y);
        var bubbleColor = bubble.Team == PlayerTeam.Blue
            ? new Color(170, 225, 255)
            : new Color(245, 245, 255);
        if (!TryDrawSprite("BubbleS", 0, renderPosition.X, renderPosition.Y, cameraPosition, bubbleColor))
        {
            var bubbleRectangle = new Rectangle(
                (int)(renderPosition.X - 5f - cameraPosition.X),
                (int)(renderPosition.Y - 5f - cameraPosition.Y),
                10,
                10);
            _spriteBatch.Draw(_pixel, bubbleRectangle, bubbleColor * 0.85f);
        }
    }

    private void DrawBladeProjectile(BladeProjectileEntity blade, Vector2 cameraPosition)
    {
        var renderPosition = GetRenderPosition(blade.Id, blade.X, blade.Y);
        var bladeColor = blade.Team == PlayerTeam.Blue
            ? new Color(180, 220, 255)
            : new Color(255, 235, 170);
        var bladeFrameIndex = Math.Max(0, PlayerEntity.QuoteBladeLifetimeTicks - blade.TicksRemaining) % 4;
        if (!TryDrawSprite("BladeProjectileS", bladeFrameIndex, renderPosition.X, renderPosition.Y, cameraPosition, bladeColor, GetVelocityRotation(blade.VelocityX, blade.VelocityY)))
        {
            var bladeRectangle = new Rectangle(
                (int)(renderPosition.X - 6f - cameraPosition.X),
                (int)(renderPosition.Y - 2f - cameraPosition.Y),
                12,
                4);
            _spriteBatch.Draw(_pixel, bladeRectangle, bladeColor);
        }
    }

    // -----------------------------------------------------------------------
    // Custom procedural flame particle
    // -----------------------------------------------------------------------

    private void DrawFlameProjectiles(Vector2 cameraPosition)
    {
        var cells = new System.Collections.Generic.Dictionary<(int, int), float>();

        foreach (var flame in _world.Flames)
        {
            AccumulateFlameParticle(cells, flame);
        }

        DrawProceduralFlameCells(cells, cameraPosition);
    }

    private void DrawProceduralFlameCells(
        System.Collections.Generic.Dictionary<(int, int), float> cells,
        Vector2 cameraPosition,
        bool topOutlineOnly = false)
    {
        const float cellSize = 2f;

        static bool IsBoundaryCell(System.Collections.Generic.Dictionary<(int, int), float> map, int gx, int gy)
        {
            for (var offsetY = -1; offsetY <= 1; offsetY += 1)
            {
                for (var offsetX = -1; offsetX <= 1; offsetX += 1)
                {
                    if (offsetX == 0 && offsetY == 0)
                    {
                        continue;
                    }

                    if (!map.ContainsKey((gx + offsetX, gy + offsetY)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        static bool IsTopBoundaryCell(System.Collections.Generic.Dictionary<(int, int), float> map, int gx, int gy)
        {
            return !map.ContainsKey((gx - 1, gy - 1))
                || !map.ContainsKey((gx, gy - 1))
                || !map.ContainsKey((gx + 1, gy - 1));
        }

        foreach (var ((gx, gy), retainedAlpha) in cells)
        {
            // Thresholding already happened during accumulation, so coloring only sees
            // the merged depth of cells that survived the 50% opacity cut.
            var hasOutline = topOutlineOnly
                ? IsTopBoundaryCell(cells, gx, gy)
                : IsBoundaryCell(cells, gx, gy);
            Color pixelColor;
            if (hasOutline)
                pixelColor = new Color(255,  75,   0);  // deep orange outline
            else if (retainedAlpha < 0.75f)
                pixelColor = new Color(255,  75,   0);  // deep orange
            else if (retainedAlpha < 1.30f)
                pixelColor = new Color(255, 145,   5);  // orange-yellow
            else if (retainedAlpha < 2.10f)
                pixelColor = new Color(255, 210,  25);  // yellow
            else
                pixelColor = new Color(255, 242, 130);  // near-white (overlap core)

            var rect = new Rectangle(
                (int)MathF.Round((gx * cellSize) - cameraPosition.X),
                (int)MathF.Round((gy * cellSize) - cameraPosition.Y),
                (int)cellSize,
                (int)cellSize);
            _spriteBatch.Draw(_pixel, rect, pixelColor);
        }
    }

    private void DrawProceduralFlameParticles(
        System.Collections.Generic.Dictionary<(int, int), float> cells,
        Vector2 cameraPosition,
        bool topOutlineOnly = false)
    {
        DrawProceduralFlameCells(cells, cameraPosition, topOutlineOnly);
    }

    private void AccumulateFlameParticle(
        System.Collections.Generic.Dictionary<(int, int), float> cells,
        FlameProjectileEntity flame)
    {
        var renderPosition = GetRenderPosition(flame.Id, flame.X, flame.Y);
        var scale = GetFlameProjectileScale(flame);
        AccumulateProceduralFlameParticle(
            cells,
            flame.Id,
            renderPosition.X,
            renderPosition.Y,
            scale,
            alphaScale: 1f,
            motionX: flame.VelocityX,
            motionY: flame.VelocityY,
            trajectoryStretch: 1.5f);
    }

    private void AccumulateProceduralFlameParticle(
        System.Collections.Generic.Dictionary<(int, int), float> cells,
        int seed,
        float centerX,
        float centerY,
        float scale,
        float alphaScale,
        float motionX = 0f,
        float motionY = 0f,
        float trajectoryStretch = 1f)
    {
        const float cellSize = 2f;

        var clampedAlphaScale = Math.Clamp(alphaScale, 0f, 1f);

        // Horn waving – both horns sway side-to-side together, driven by a
        // per-flame deterministic phase/speed so every flame looks independent.
        var hornPhase    = GetDeterministicUnitFloat(seed, salt: 97)  * MathF.PI * 2f;
        var hornSpeed    = MathHelper.Lerp(0.07f, 0.13f, GetDeterministicUnitFloat(seed, salt: 113));
        var swayAmp      = MathHelper.Lerp(1.5f,  3.0f,  GetDeterministicUnitFloat(seed, salt: 127)) * scale;
        var hornSway     = MathF.Sin(_world.Frame * hornSpeed + hornPhase) * swayAmp;

        // --- circle centres & radii (world-space) ---
        var cx    = centerX;
        var cy    = centerY;
        var baseR = 6f * scale;

        var motionLengthSquared = (motionX * motionX) + (motionY * motionY);
        var oppositeMotionDirection = motionLengthSquared > 0.0001f
            ? Vector2.Normalize(new Vector2(-motionX, -motionY))
            : new Vector2(0f, -1f);
        var trajectoryDirection = motionLengthSquared > 0.0001f
            ? Vector2.Normalize(new Vector2(motionX, motionY))
            : new Vector2(1f, 0f);
        var clampedStretch = Math.Clamp(trajectoryStretch, 1f, 2.25f);
        var hornAxis = new Vector2(0f, -1f) * 1.2f + oppositeMotionDirection * 0.65f;
        if (hornAxis.LengthSquared() <= 0.0001f)
        {
            hornAxis = new Vector2(0f, -1f);
        }
        else
        {
            hornAxis.Normalize();
        }

        var hornPerpendicular = new Vector2(-hornAxis.Y, hornAxis.X);
        if (hornPerpendicular.LengthSquared() <= 0.0001f)
        {
            hornPerpendicular = new Vector2(1f, 0f);
        }
        else
        {
            hornPerpendicular.Normalize();
        }

        // Per-flame random: which horn (left=1, right=2) is the big one.
        var bigOnLeft = GetDeterministicUnitFloat(seed, salt: 151) < 0.5f;
        var hornBigR   = 5.5f * scale;
        var hornSmallLargeR = 2.8f * scale;
        var h1LargeR = bigOnLeft ? hornBigR : hornSmallLargeR;
        var h2LargeR = bigOnLeft ? hornSmallLargeR : hornBigR;

        // Horn base circles stay biased upward, but their axis tilts backward against motion.
        // Perpendicular separation stays asymmetric so the big horn sits more central.
        var h1SepX  = (bigOnLeft ? 2.5f : 3.5f) * scale;
        var h2SepX  = (bigOnLeft ? 3.5f : 2.5f) * scale;
        var hornRiseY = 5f * scale;
        var hornWaveX = hornPerpendicular.X * hornSway;
        var hornWaveY = hornPerpendicular.Y * hornSway;
        var h1lCx = cx + hornAxis.X * hornRiseY - hornPerpendicular.X * h1SepX + hornWaveX;
        var h1lCy = cy + hornAxis.Y * hornRiseY - hornPerpendicular.Y * h1SepX + hornWaveY;
        var h2lCx = cx + hornAxis.X * hornRiseY + hornPerpendicular.X * h2SepX + hornWaveX;
        var h2lCy = cy + hornAxis.Y * hornRiseY + hornPerpendicular.Y * h2SepX + hornWaveY;

        // noiseRadius: how far outside the hard boundary the edge fuzz extends.
        var noiseRadius = 2f * scale;

        var maxCircleRadius = MathF.Max(baseR, MathF.Max(h1LargeR, h2LargeR));
        var extraStretchRadius = (clampedStretch - 1f) * maxCircleRadius;
        var minX = MathF.Min(cx - baseR, MathF.Min(h1lCx - h1LargeR, h2lCx - h2LargeR)) - noiseRadius - extraStretchRadius;
        var maxX = MathF.Max(cx + baseR, MathF.Max(h1lCx + h1LargeR, h2lCx + h2LargeR)) + noiseRadius + extraStretchRadius;
        var minY = MathF.Min(cy - baseR, MathF.Min(h1lCy - h1LargeR, h2lCy - h2LargeR)) - noiseRadius - extraStretchRadius;
        var maxY = MathF.Max(cy + baseR, MathF.Max(h1lCy + h1LargeR, h2lCy + h2LargeR)) + noiseRadius + extraStretchRadius;

        var minGX = (int)MathF.Floor(minX / cellSize);
        var maxGX = (int)MathF.Floor(maxX / cellSize);
        var minGY = (int)MathF.Floor(minY / cellSize);
        var maxGY = (int)MathF.Floor(maxY / cellSize);

        // Noise seed: unique per flame, stable across frames (no temporal shimmer).
        var noiseSeed = seed * 1234567 ^ 0x5EED_ABCD;

        // Per-cell SDF helper (static – no capture).
        static float CircleSDF(
            float px,
            float py,
            float circleCx,
            float circleCy,
            float r,
            Vector2 trajectoryDirection,
            float stretch)
        {
            var dx = px - circleCx;
            var dy = py - circleCy;
            var alongTrajectory = (dx * trajectoryDirection.X) + (dy * trajectoryDirection.Y);
            var perpX = dx - (alongTrajectory * trajectoryDirection.X);
            var perpY = dy - (alongTrajectory * trajectoryDirection.Y);
            var scaledAlong = alongTrajectory / stretch;
            return MathF.Sqrt((scaledAlong * scaledAlong) + (perpX * perpX) + (perpY * perpY)) - r;
        }

        var noiseRadiusTimes2 = noiseRadius * 2f;

        for (var gy = minGY; gy <= maxGY; gy++)
        {
            var cellCY = (gy * cellSize) + (cellSize * 0.5f);
            for (var gx = minGX; gx <= maxGX; gx++)
            {
                var cellCX = (gx * cellSize) + (cellSize * 0.5f);

                // Minimum signed distance to the compound shape.
                var sdf = MathF.Min(
                    CircleSDF(cellCX, cellCY, cx,    cy,    baseR, trajectoryDirection, clampedStretch),
                    MathF.Min(
                        CircleSDF(cellCX, cellCY, h1lCx, h1lCy, h1LargeR, trajectoryDirection, clampedStretch),
                            CircleSDF(cellCX, cellCY, h2lCx, h2lCy, h2LargeR, trajectoryDirection, clampedStretch)));

                // Skip cells well outside the fuzz zone.
                if (sdf > noiseRadius)
                {
                    continue;
                }

                // rawAlpha: 1.0 deep inside shape, 0.5 at boundary, 0.0 at noiseRadius outside.
                var rawAlpha = 0.5f - (sdf / noiseRadiusTimes2);

                // Edge noise: stronger further from centre (i.e. higher at the boundary/outside).
                var noise       = GetFlameEdgeNoise(gx, gy, noiseSeed);
                var clampedRaw  = Math.Clamp(rawAlpha, 0f, 1f);
                var noiseEffect = (noise - 0.5f) * (1f - clampedRaw) * 0.7f;
                var noisedAlpha = rawAlpha + noiseEffect;
                rawAlpha *= clampedAlphaScale;
                noisedAlpha *= clampedAlphaScale;

                // Apply the shape cut before any overlap accumulation so coloring is
                // driven only by merged cells that survive the opacity threshold.
                if (noisedAlpha < 0.5f)
                {
                    continue;
                }

                var key = (gx, gy);
                if (cells.TryGetValue(key, out var existing))
                {
                    // Surviving cells now merge additively, and the merged depth alone
                    // decides the final colour stage.
                    cells[key] = MathF.Min(4.0f, existing + rawAlpha);
                }
                else
                {
                    cells[key] = rawAlpha;
                }
            }
        }
    }

    private static float GetFlameEdgeNoise(int gx, int gy, int seed)
    {
        // 3x3 Gaussian kernel to soften edge noise and reduce jagged pixel transitions.
        var weightedNoise =
            (GetFlameEdgeNoiseSample(gx - 1, gy - 1, seed) * 1f) +
            (GetFlameEdgeNoiseSample(gx,     gy - 1, seed) * 2f) +
            (GetFlameEdgeNoiseSample(gx + 1, gy - 1, seed) * 1f) +
            (GetFlameEdgeNoiseSample(gx - 1, gy,     seed) * 2f) +
            (GetFlameEdgeNoiseSample(gx,     gy,     seed) * 4f) +
            (GetFlameEdgeNoiseSample(gx + 1, gy,     seed) * 2f) +
            (GetFlameEdgeNoiseSample(gx - 1, gy + 1, seed) * 1f) +
            (GetFlameEdgeNoiseSample(gx,     gy + 1, seed) * 2f) +
            (GetFlameEdgeNoiseSample(gx + 1, gy + 1, seed) * 1f);
        return weightedNoise / 16f;
    }

    private static float GetFlameEdgeNoiseSample(int gx, int gy, int seed)
    {
        unchecked
        {
            var hash = (uint)seed;
            hash ^= (uint)(gx * 374761393);
            hash ^= (uint)(gy * 668265263);
            hash = (hash ^ (hash >> 13)) * 1274126177u;
            hash ^= hash >> 16;
            return (hash & 1023u) / 1023f;
        }
    }

    // -----------------------------------------------------------------------
    // Legacy sprite-based flame draw (kept for reference, no longer called)
    // -----------------------------------------------------------------------

    private void DrawFlameProjectile(FlameProjectileEntity flame, Vector2 cameraPosition)
    {
        var renderPosition = GetRenderPosition(flame.Id, flame.X, flame.Y);
        var flameColor = Color.White;
        var flameSprite = GetResolvedSprite("FlameS");
        var flameScale = GetFlameProjectileScale(flame);

        if (flameSprite is not null && flameSprite.Frames.Count > 0)
        {
            var frameIndex = GetFlameProjectileFrameIndex(flame, flameSprite.Frames.Count);
            DrawLoadedSpriteFrame(
                flameSprite.Frames[frameIndex],
                new Vector2(renderPosition.X - cameraPosition.X, renderPosition.Y - cameraPosition.Y),
                null,
                flameColor,
                0f,
                flameSprite.Origin.ToVector2(),
                new Vector2(flameScale, flameScale),
                SpriteEffects.None,
                0f);
            return;
        }

        var baseFlameSize = flame.IsAttached ? 8f : 6f;
        var flameSize = Math.Max(2, (int)MathF.Round(baseFlameSize * flameScale));
        var fallbackColor = flame.IsAttached
            ? new Color(255, 120, 60)
            : new Color(255, 170, 90);
        var flameRectangle = new Rectangle(
            (int)(renderPosition.X - flameSize / 2f - cameraPosition.X),
            (int)(renderPosition.Y - flameSize / 2f - cameraPosition.Y),
            flameSize,
            flameSize);
        _spriteBatch.Draw(_pixel, flameRectangle, fallbackColor);
    }

    private float GetFlameProjectileScale(FlameProjectileEntity flame)
    {
        var flameLifetimeTicks = flame.IsAttached
            ? FlameProjectileEntity.AttachedLifetimeTicks
            : FlameProjectileEntity.AirLifetimeTicks;
        var flameAgeTicks = flameLifetimeTicks - flame.TicksRemaining;
        var lifeProgress = float.Clamp(flameAgeTicks / (float)Math.Max(1, flameLifetimeTicks), 0f, 1f);

        // Deterministic per-flame variation avoids flicker while giving each flame a unique growth profile.
        var randomGrowthRate = MathHelper.Lerp(0.72f, 1.28f, GetDeterministicUnitFloat(flame.Id, salt: 11));
        var randomMaxScale = MathHelper.Lerp(1.18f, 1.4f, GetDeterministicUnitFloat(flame.Id, salt: 29));
        var startScale = MathHelper.Lerp(0.42f, 0.58f, GetDeterministicUnitFloat(flame.Id, salt: 47));
        var growthProgress = MathF.Pow(lifeProgress, randomGrowthRate);
        return MathHelper.Lerp(startScale, randomMaxScale, growthProgress);
    }

    private static int GetFlameProjectileFrameIndex(FlameProjectileEntity flame, int frameCount)
    {
        if (frameCount <= 0)
        {
            return 0;
        }

        var flameLifetimeTicks = flame.IsAttached
            ? FlameProjectileEntity.AttachedLifetimeTicks
            : FlameProjectileEntity.AirLifetimeTicks;
        var flameAgeTicks = flameLifetimeTicks - flame.TicksRemaining;
        return Math.Clamp(Math.Abs(flameAgeTicks) % frameCount, 0, frameCount - 1);
    }

    private Vector2 GetFlameScaledCenterOfMassWorldPosition(FlameProjectileEntity flame)
    {
        // Geometric centre-of-mass of the compound particle (base + 2 large horn + 2 small tip circles).
        // Weighted by circle area (∝ r²): base r=6, horn large r=4 ×2, horn small r=2.5 ×2.
        // Symmetric in X, net Y offset ≈ -3.4 * scale upward from base centre.
        var scale = GetFlameProjectileScale(flame);
        return new Vector2(flame.X, flame.Y - 3.4f * scale);
    }

    private Vector2 GetSpriteFrameCenterOfMass(LoadedSpriteFrame frame)
    {
        if (_spriteFrameCenterOfMassCache.TryGetValue(frame, out var cachedCenterOfMass))
        {
            return cachedCenterOfMass;
        }

        var sourceRectangle = frame.SourceRectangle ?? new Rectangle(0, 0, frame.Texture.Width, frame.Texture.Height);
        if (sourceRectangle.Width <= 0 || sourceRectangle.Height <= 0)
        {
            var emptyCenter = Vector2.Zero;
            _spriteFrameCenterOfMassCache[frame] = emptyCenter;
            return emptyCenter;
        }

        var pixels = new Color[sourceRectangle.Width * sourceRectangle.Height];
        frame.Texture.GetData(0, sourceRectangle, pixels, 0, pixels.Length);

        double weightedX = 0d;
        double weightedY = 0d;
        double totalWeight = 0d;
        for (var y = 0; y < sourceRectangle.Height; y += 1)
        {
            for (var x = 0; x < sourceRectangle.Width; x += 1)
            {
                var alpha = pixels[(y * sourceRectangle.Width) + x].A;
                if (alpha <= 0)
                {
                    continue;
                }

                var weight = alpha / 255d;
                weightedX += (x + 0.5d) * weight;
                weightedY += (y + 0.5d) * weight;
                totalWeight += weight;
            }
        }

        var centerOfMass = totalWeight > 0d
            ? new Vector2((float)(weightedX / totalWeight), (float)(weightedY / totalWeight))
            : new Vector2(sourceRectangle.Width * 0.5f, sourceRectangle.Height * 0.5f);
        _spriteFrameCenterOfMassCache[frame] = centerOfMass;
        return centerOfMass;
    }

    private static float GetDeterministicUnitFloat(int seed, int salt)
    {
        unchecked
        {
            uint value = (uint)(seed * 73856093) ^ (uint)(salt * 19349663);
            value ^= value >> 16;
            value *= 2246822519u;
            value ^= value >> 13;
            value *= 3266489917u;
            value ^= value >> 16;
            return (value & 0x00FFFFFFu) / 16777215f;
        }
    }

    private void DrawFlareProjectile(FlareProjectileEntity flare, Vector2 cameraPosition)
    {
        var renderPosition = GetRenderPosition(flare.Id, flare.X, flare.Y);
        if (!TryDrawSprite("FlareS", 0, renderPosition.X, renderPosition.Y, cameraPosition, Color.White, GetVelocityRotation(flare.VelocityX, flare.VelocityY)))
        {
            var flareRectangle = new Rectangle(
                (int)(renderPosition.X - 4f - cameraPosition.X),
                (int)(renderPosition.Y - 2f - cameraPosition.Y),
                8,
                4);
            _spriteBatch.Draw(_pixel, flareRectangle, Color.White);
        }
    }

    private void DrawRocketProjectile(RocketProjectileEntity rocket, Vector2 cameraPosition)
    {
        var renderPosition = GetRenderPosition(rocket.Id, rocket.X, rocket.Y);
        var rocketColor = rocket.Team == PlayerTeam.Blue
            ? new Color(120, 180, 255)
            : new Color(255, 110, 90);
        var rocketFrame = rocket.Team == PlayerTeam.Blue ? 0 : 1;
        var rocketScale = rocket.EnableExperimentalStingerTracking ? 1.4f : 1f;
        if (!TryDrawSprite("RocketS", rocketFrame, renderPosition.X, renderPosition.Y, cameraPosition, rocketColor, rocket.DirectionRadians, scale: rocketScale))
        {
            var halfWidth = (int)MathF.Round(5f * rocketScale);
            var halfHeight = (int)MathF.Round(3f * rocketScale);
            var rocketRectangle = new Rectangle(
                (int)(renderPosition.X - halfWidth - cameraPosition.X),
                (int)(renderPosition.Y - halfHeight - cameraPosition.Y),
                Math.Max(1, halfWidth * 2),
                Math.Max(1, halfHeight * 2));
            _spriteBatch.Draw(_pixel, rocketRectangle, rocketColor);
        }
    }

    private void DrawMineProjectile(MineProjectileEntity mine, Vector2 cameraPosition)
    {
        var renderPosition = GetRenderPosition(mine.Id, mine.X, mine.Y);
        var frameIndex = (mine.Team == PlayerTeam.Blue ? 2 : 0)
            + (mine.IsStickied ? 1 : 0);
        if (!TryDrawSprite("MineS", frameIndex, renderPosition.X, renderPosition.Y, cameraPosition, Color.White))
        {
            var mineRectangle = new Rectangle(
                (int)(renderPosition.X - 5f - cameraPosition.X),
                (int)(renderPosition.Y - 5f - cameraPosition.Y),
                10,
                10);
            _spriteBatch.Draw(_pixel, mineRectangle, Color.White);
        }
    }
}
