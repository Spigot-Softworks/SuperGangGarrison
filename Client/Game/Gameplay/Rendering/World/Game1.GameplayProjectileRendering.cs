#nullable enable

using OpenGarrison.Core;
using OpenGarrison.GameplayModding;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace OpenGarrison.Client;

public partial class Game1
{
    private readonly System.Collections.Generic.Dictionary<LoadedSpriteFrame, Vector2> _spriteFrameCenterOfMassCache = new();

    private Color ResolveProjectileTint(PlayerTeam team, Color blueColor, Color redColor, Color neutralColor)
    {
        if (!_projectileTeamTintEnabled)
        {
            return neutralColor;
        }

        return team == PlayerTeam.Blue ? blueColor : redColor;
    }

    private Color GetCriticalProjectileOverlayColor(PlayerTeam team)
    {
        return GameplayPlayerStatusEffectRenderController.GetUberOverlayColor(team);
    }

    private void DrawCriticalProjectileOverlay(string spriteName, int frameIndex, float worldX, float worldY, Vector2 cameraPosition, PlayerTeam team, float rotation = 0f, Vector2? scale = null)
    {
        var sprite = GetResolvedSprite(spriteName);
        if (sprite is null || sprite.Frames.Count == 0)
        {
            return;
        }

        var clampedFrameIndex = Math.Clamp(frameIndex, 0, sprite.Frames.Count - 1);
        var teamColor = GetCriticalProjectileOverlayColor(team);
        var position = new Vector2(worldX - cameraPosition.X, worldY - cameraPosition.Y);
        var spriteScale = scale ?? Vector2.One;

        // Draw screen blend overlay (on top of sprite)
        DrawSpriteFrameScreenColor(
            sprite.Frames[clampedFrameIndex],
            position,
            teamColor * 0.5f,
            rotation,
            sprite.Origin.ToVector2(),
            spriteScale);
    }

    private void DrawCriticalProjectileOutline(string spriteName, int frameIndex, float worldX, float worldY, Vector2 cameraPosition, PlayerTeam team, float rotation = 0f, Vector2? scale = null)
    {
        var sprite = GetResolvedSprite(spriteName);
        if (sprite is null || sprite.Frames.Count == 0 || !_uberOutlineEnabled)
        {
            return;
        }

        var clampedFrameIndex = Math.Clamp(frameIndex, 0, sprite.Frames.Count - 1);
        var teamColor = GetCriticalProjectileOverlayColor(team);
        var outlineTint = Color.Lerp(teamColor, Color.White, 0.75f);
        var position = new Vector2(worldX - cameraPosition.X, worldY - cameraPosition.Y);
        var spriteScale = scale ?? Vector2.One;

        // Draw outline (behind sprite)
        DrawSpriteFrameOutline(sprite.Frames[clampedFrameIndex], position, outlineTint, rotation, sprite.Origin.ToVector2(), spriteScale);
    }

    private void DrawMedicBeams(Vector2 cameraPosition)
    {
        foreach (var player in EnumerateRenderablePlayers())
        {
            DrawMedicBeamForPlayer(player, cameraPosition);
        }
    }

    private void DrawMedicBeamForPlayer(PlayerEntity medic, Vector2 cameraPosition)
    {
        if (!medic.IsMedicHealing
            || !medic.MedicHealTargetId.HasValue)
        {
            return;
        }

        var healTarget = FindPlayerById(medic.MedicHealTargetId.Value);
        if (healTarget is null || !healTarget.IsAlive)
        {
            return;
        }

        var healTargetRenderPosition = GetRenderPosition(healTarget);
        var beamOrigin = GetMedicBeamOrigin(medic, out var weaponForwardDirection);
        if (!IsFiniteVector(healTargetRenderPosition)
            || !IsFiniteVector(beamOrigin)
            || !IsFiniteVector(weaponForwardDirection))
        {
            return;
        }

        var toTarget = healTargetRenderPosition - beamOrigin;
        if (!IsFiniteVector(toTarget) || toTarget.LengthSquared() <= 0.0001f)
        {
            return;
        }

        var aimDirection = weaponForwardDirection;
        if (!IsFiniteVector(aimDirection) || aimDirection.LengthSquared() <= 0.0001f)
        {
            aimDirection = Vector2.Normalize(toTarget);
        }

        var isCritMedigun = medic.HasEquippedBehavior(BuiltInGameplayBehaviorIds.MedigunCrit);
        var isFreezeRayBeam = medic.IsExperimentalEngineerFreezeRayPresented;
        var isEssenceExtractorBeam = medic.IsExperimentalEngineerEssenceExtractorPresented && !isFreezeRayBeam;
        var beamColor = isFreezeRayBeam
            ? new Color(44, 118, 255, 94)
            : isEssenceExtractorBeam
                ? new Color(150, 8, 8, 92)
                : healTarget.Team == PlayerTeam.Blue
                    ? new Color(0, 20, 180, 90)
                    : new Color(120, 5, 5, 90);
        var beamStartColor = isFreezeRayBeam
            ? new Color(160, 225, 255, 240)
            : isEssenceExtractorBeam
                ? new Color(255, 72, 72, 245)
                : healTarget.Team == PlayerTeam.Blue
                    ? new Color(80, 160, 255, 240)
                    : new Color(255, 95, 95, 245);
        var helixStartColor = isFreezeRayBeam
            ? new Color(208, 242, 255, 196)
            : isEssenceExtractorBeam
                ? new Color(255, 140, 140, 200)
                : healTarget.Team == PlayerTeam.Blue
                    ? new Color(140, 195, 255, 190)
                    : new Color(255, 175, 175, 200);
        var beamStartX = beamOrigin.X;
        var beamStartY = beamOrigin.Y;
        DrawMedicBeamSegment(healTarget);
        TryDrawAdditionalMedicBeamSegment(medic.ExperimentalAdditionalMedicBeamTargetPlayerId1);
        TryDrawAdditionalMedicBeamSegment(medic.ExperimentalAdditionalMedicBeamTargetPlayerId2);

        void TryDrawAdditionalMedicBeamSegment(int? targetPlayerId)
        {
            if (!targetPlayerId.HasValue)
            {
                return;
            }

            var target = FindPlayerById(targetPlayerId.Value);
            if (target is null || !target.IsAlive)
            {
                return;
            }

            DrawMedicBeamSegment(target);
        }

        void DrawMedicBeamSegment(PlayerEntity target)
        {
            var targetRenderPosition = GetRenderPosition(target);
            if (!IsFiniteVector(targetRenderPosition))
            {
                return;
            }

            DrawCurvedWorldLine(
                beamStartX,
                beamStartY,
                targetRenderPosition.X,
                targetRenderPosition.Y,
                cameraPosition,
                beamStartColor,
                beamColor,
                nozzleThickness: 4f,
                maxThickness: 8f,
                tailThickness: 2f,
                rampDistPixels: 8f,
                aimDirection);
            if (isCritMedigun)
            {
                DrawMedicBeamCritHelix(
                    beamStartX,
                    beamStartY,
                    targetRenderPosition.X,
                    targetRenderPosition.Y,
                    cameraPosition,
                    aimDirection,
                    helixStartColor,
                    beamColor);
                return;
            }

            DrawMedicBeamHelix(
                beamStartX,
                beamStartY,
                targetRenderPosition.X,
                targetRenderPosition.Y,
                cameraPosition,
                aimDirection,
                helixStartColor,
                beamColor);
        }
    }

    private void DrawMedicBeamHelix(
        float startX, float startY,
        float endX, float endY,
        Vector2 cameraPosition,
        Vector2 aimDirection,
        Color helixStartColor,
        Color helixEndColor)
    {
        if (!AreFinite(startX, startY, endX, endY)
            || !IsFiniteVector(cameraPosition)
            || !IsFiniteVector(aimDirection)
            || aimDirection.LengthSquared() <= 0.0001f)
        {
            return;
        }

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

    private void DrawMedicBeamCritHelix(
        float startX, float startY,
        float endX, float endY,
        Vector2 cameraPosition,
        Vector2 aimDirection,
        Color helixStartColor,
        Color helixEndColor)
    {
        if (!AreFinite(startX, startY, endX, endY)
            || !IsFiniteVector(cameraPosition)
            || !IsFiniteVector(aimDirection)
            || aimDirection.LengthSquared() <= 0.0001f)
        {
            return;
        }

        const int steps = 64;
        const float maxRadius = 8f;
        const float helixTurns = 3f;
        const float helixFrequency = helixTurns * MathF.PI * 2f;
        const float pixelSize = 2f;
        const float mainWaveAmplitude = 0.6f;
        const float noiseAmplitude = 0.8f;

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
        var pixelatedCells = new System.Collections.Generic.Dictionary<(int, int), (float alpha, Color color)>();

        // Use a simple hash-based noise function for consistent jaggedness
        float GetNoise(int seed)
        {
            var hash = seed * 2654435761;
            hash = ((hash >> 16) ^ hash) * 0x45d9f3b;
            hash = ((hash >> 16) ^ hash) * 0x45d9f3b;
            hash = (hash >> 16) ^ hash;
            return ((hash & 0xFFFF) / 65536f) * 2f - 1f;
        }

        // Two strands, 180° apart, with jagged noise
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

                // Jagged wave with random noise
                var angle = phaseAtOrigin + t * helixFrequency + strandOffset;
                var angleAtOrigin = phaseAtOrigin + strandOffset;
                var baseWave = (MathF.Sin(angle) - MathF.Sin(angleAtOrigin)) * mainWaveAmplitude;

                // Add jaggedness with multiple noise samples
                var noiseSeed = (int)(t * 100) + strand * 1000 + (int)(_medigunBeamHelixPhase * 10);
                var noise1 = GetNoise(noiseSeed);
                var noise2 = GetNoise(noiseSeed + 123);
                var noise3 = GetNoise(noiseSeed + 456);

                // Combine base wave with noisy variations
                var jaggedOffset = baseWave + (noise1 * noiseAmplitude) + (noise2 * noiseAmplitude * 0.5f) + (noise3 * noiseAmplitude * 0.25f);

                var pos = bezierPos + perp * (jaggedOffset * radius);

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

    private Vector2 GetMedicBeamOrigin(PlayerEntity medic, out Vector2 weaponForwardDirection)
    {
        weaponForwardDirection = Vector2.Zero;
        var renderPosition = GetRenderPosition(medic);
        if (!IsFiniteVector(renderPosition))
        {
            return Vector2.Zero;
        }

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

        if (sprite.Frames.Count == 0)
        {
            return roundedOrigin;
        }

        var bodySelection = GetPlayerBodySpriteSelection(medic);
        var anchorOrigin = GetWeaponAnchorOrigin(weaponDefinition, sprite);
        var renderAim = GetRenderAimWorldPosition(medic);
        if (!IsFiniteVector(renderAim))
        {
            weaponForwardDirection = medic.FacingDirectionX < 0f ? new Vector2(-1f, 0f) : new Vector2(1f, 0f);
            return roundedOrigin;
        }

        var playerScale = medic.PlayerScale;

        var facingScale = MathF.Abs(renderAim.X - roundedOrigin.X) > 0.001f
            ? (renderAim.X < roundedOrigin.X ? -1f : 1f)
            : (medic.FacingDirectionX < 0f ? -1f : 1f);

        var drawX = roundedOrigin.X + ((weaponDefinition.XOffset + anchorOrigin.X) * facingScale * playerScale);
        var drawY = roundedOrigin.Y + ((weaponDefinition.YOffset + bodySelection.EquipmentOffset + anchorOrigin.Y) * playerScale);

        var aimDeltaX = renderAim.X - drawX;
        var aimDeltaY = renderAim.Y - drawY;
        var aimLength = MathF.Sqrt((aimDeltaX * aimDeltaX) + (aimDeltaY * aimDeltaY));
        if (aimLength <= 0.001f)
        {
            weaponForwardDirection = new Vector2(facingScale, 0f);
            return new Vector2(drawX, drawY);
        }

        var aimDirectionX = aimDeltaX / aimLength;
        var aimDirectionY = aimDeltaY / aimLength;
        weaponForwardDirection = new Vector2(aimDirectionX, aimDirectionY);

        var tipDistance = Math.Max(0f, (sprite.Frames[0].Width - anchorOrigin.X) * playerScale);
        return new Vector2(
            drawX + aimDirectionX * tipDistance,
            drawY + aimDirectionY * tipDistance);
    }

    private static bool IsFiniteVector(Vector2 value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y);
    }

    private static bool AreFinite(params float[] values)
    {
        foreach (var value in values)
        {
            if (!float.IsFinite(value))
            {
                return false;
            }
        }

        return true;
    }

    private void DrawGameplayEffectsAndProjectiles(Vector2 cameraPosition)
    {
        WriteGameplayRenderTrace("effects before explosions");
        DrawExplosionVisuals(cameraPosition);
        WriteGameplayRenderTrace("effects before impacts");
        DrawImpactVisuals(cameraPosition);
        WriteGameplayRenderTrace("effects before loose-sheets");
        DrawLooseSheetVisuals(cameraPosition);
        if (_gibLevel > 0)
        {
            WriteGameplayRenderTrace("effects before blood");
            DrawBloodVisuals(cameraPosition);
        }

        WriteGameplayRenderTrace("effects before shells");
        DrawShellVisuals(cameraPosition);

        if (_particleMode != 1)
        {
            WriteGameplayRenderTrace("effects before rocket-smoke");
            DrawRocketSmokeVisuals(cameraPosition);
            WriteGameplayRenderTrace("effects before wallspin-dust");
            DrawWallspinDustVisuals(cameraPosition);
            WriteGameplayRenderTrace("effects before blastjump-flame");
            DrawBlastJumpFlameVisuals(cameraPosition);
            WriteGameplayRenderTrace("effects before flame-smoke");
            DrawFlameSmokeVisuals(cameraPosition);
        }

        WriteGameplayRenderTrace("effects before projectile-sprites");
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
            WriteGameplayRenderTrace("effects before procedural-flames");
            DrawFlameProjectiles(cameraPosition);
        }
        else
        {
            WriteGameplayRenderTrace("effects before sprite-flames");
            foreach (var flame in _world.Flames)
            {
                DrawFlameProjectile(flame, cameraPosition);
            }
        }

        WriteGameplayRenderTrace("effects before flares-rockets");
        foreach (var flare in _world.Flares)
        {
            DrawFlareProjectile(flare, cameraPosition);
        }

        foreach (var rocket in _world.Rockets)
        {
            DrawRocketProjectile(rocket, cameraPosition);
        }

        if (_particleMode != 1)
        {
            WriteGameplayRenderTrace("effects before mine-trails");
            DrawMineTrailVisuals(cameraPosition);
        }

        WriteGameplayRenderTrace("effects before mines");
        foreach (var mine in _world.Mines)
        {
            DrawMineProjectile(mine, cameraPosition);
        }

        WriteGameplayRenderTrace("effects done");
    }

    private void DrawShotProjectile(ShotProjectileEntity shot, Vector2 cameraPosition, Color blueColor, Color redColor)
    {
        var renderPosition = GetRenderPosition(shot.Id, shot.X, shot.Y);
        var shotColor = ResolveProjectileTint(shot.Team, blueColor, redColor, new Color(235, 228, 210));
        var rotation = GetVelocityRotation(shot.VelocityX, shot.VelocityY);

        // Draw outline first (behind sprite) if critical
        if (shot.IsCritical)
        {
            DrawCriticalProjectileOutline("ShotS", 0, renderPosition.X, renderPosition.Y, cameraPosition, shot.Team, rotation);
        }

        // Draw main sprite
        if (!TryDrawSprite("ShotS", 0, renderPosition.X, renderPosition.Y, cameraPosition, shotColor, rotation))
        {
            var shotRectangle = new Rectangle(
                (int)(renderPosition.X - 2f - cameraPosition.X),
                (int)(renderPosition.Y - 2f - cameraPosition.Y),
                4,
                4);
            _spriteBatch.Draw(_pixel, shotRectangle, shotColor);
            if (shot.IsCritical)
            {
                var overlayColor = GetCriticalProjectileOverlayColor(shot.Team) * 0.3f;
                _spriteBatch.End();
                _spriteBatch.Begin(
                    SpriteSortMode.Deferred,
                    BlendState.Additive,
                    samplerState: SamplerState.PointClamp,
                    rasterizerState: RasterizerState.CullNone);
                _spriteBatch.Draw(_pixel, shotRectangle, overlayColor);
                _spriteBatch.End();
                _spriteBatch.Begin(samplerState: SamplerState.PointClamp, rasterizerState: RasterizerState.CullNone);
            }
        }
        else if (shot.IsCritical)
        {
            // Draw screen blend overlay on top
            DrawCriticalProjectileOverlay("ShotS", 0, renderPosition.X, renderPosition.Y, cameraPosition, shot.Team, rotation);
        }
    }

    private void DrawShotProjectile(RevolverProjectileEntity shot, Vector2 cameraPosition, Color blueColor, Color redColor)
    {
        var renderPosition = GetRenderPosition(shot.Id, shot.X, shot.Y);
        var shotColor = ResolveProjectileTint(shot.Team, blueColor, redColor, new Color(245, 236, 214));
        var rotation = GetVelocityRotation(shot.VelocityX, shot.VelocityY);

        // Draw outline first (behind sprite) if critical
        if (shot.IsCritical)
        {
            DrawCriticalProjectileOutline("ShotS", 0, renderPosition.X, renderPosition.Y, cameraPosition, shot.Team, rotation);
        }

        // Draw main sprite
        if (!TryDrawSprite("ShotS", 0, renderPosition.X, renderPosition.Y, cameraPosition, shotColor, rotation))
        {
            var shotRectangle = new Rectangle(
                (int)(renderPosition.X - 2f - cameraPosition.X),
                (int)(renderPosition.Y - 2f - cameraPosition.Y),
                4,
                4);
            _spriteBatch.Draw(_pixel, shotRectangle, shotColor);
            if (shot.IsCritical)
            {
                var overlayColor = GetCriticalProjectileOverlayColor(shot.Team) * 0.3f;
                _spriteBatch.End();
                _spriteBatch.Begin(
                    SpriteSortMode.Deferred,
                    BlendState.Additive,
                    samplerState: SamplerState.PointClamp,
                    rasterizerState: RasterizerState.CullNone);
                _spriteBatch.Draw(_pixel, shotRectangle, overlayColor);
                _spriteBatch.End();
                _spriteBatch.Begin(samplerState: SamplerState.PointClamp, rasterizerState: RasterizerState.CullNone);
            }
        }
        else if (shot.IsCritical)
        {
            DrawCriticalProjectileOverlay("ShotS", 0, renderPosition.X, renderPosition.Y, cameraPosition, shot.Team, rotation);
        }
    }

    private void DrawNeedleProjectile(NeedleProjectileEntity needle, Vector2 cameraPosition)
    {
        var renderPosition = GetRenderPosition(needle.Id, needle.X, needle.Y);
        var needleColor = ResolveProjectileTint(
            needle.Team,
            new Color(150, 220, 255),
            new Color(240, 240, 220),
            new Color(236, 236, 228));
        var rotation = GetVelocityRotation(needle.VelocityX, needle.VelocityY);

        // Flip needle sprite vertically when traveling left to match weapon mirroring
        var needleScale = needle.VelocityX < 0f ? new Vector2(1f, -1f) : Vector2.One;

        // Draw outline first (behind sprite) if critical
        if (needle.IsCritical)
        {
            DrawCriticalProjectileOutline("NeedleS", 0, renderPosition.X, renderPosition.Y, cameraPosition, needle.Team, rotation, needleScale);
        }

        // Draw main sprite
        if (!TryDrawSprite("NeedleS", 0, renderPosition.X, renderPosition.Y, cameraPosition, needleColor, rotation, needleScale))
        {
            var needleRectangle = new Rectangle(
                (int)(renderPosition.X - 3f - cameraPosition.X),
                (int)(renderPosition.Y - 1f - cameraPosition.Y),
                6,
                2);
            _spriteBatch.Draw(_pixel, needleRectangle, needleColor);
            if (needle.IsCritical)
            {
                var overlayColor = GetCriticalProjectileOverlayColor(needle.Team) * 0.3f;
                _spriteBatch.End();
                _spriteBatch.Begin(
                    SpriteSortMode.Deferred,
                    BlendState.Additive,
                    samplerState: SamplerState.PointClamp,
                    rasterizerState: RasterizerState.CullNone);
                _spriteBatch.Draw(_pixel, needleRectangle, overlayColor);
                _spriteBatch.End();
                _spriteBatch.Begin(samplerState: SamplerState.PointClamp, rasterizerState: RasterizerState.CullNone);
            }
        }
        else if (needle.IsCritical)
        {
            // Draw screen blend overlay on top
            DrawCriticalProjectileOverlay("NeedleS", 0, renderPosition.X, renderPosition.Y, cameraPosition, needle.Team, rotation, needleScale);
        }
    }

    private void DrawBubbleProjectile(BubbleProjectileEntity bubble, Vector2 cameraPosition)
    {
        var renderPosition = GetRenderPosition(bubble.Id, bubble.X, bubble.Y);
        var bubbleColor = ResolveProjectileTint(
            bubble.Team,
            new Color(170, 225, 255),
            new Color(245, 245, 255),
            new Color(242, 242, 248));
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
        var bladeColor = ResolveProjectileTint(
            blade.Team,
            new Color(180, 220, 255),
            new Color(255, 235, 170),
            new Color(244, 232, 208));
        var bladeFrameIndex = Math.Max(0, PlayerEntity.QuoteBladeLifetimeTicks - blade.TicksRemaining) % 4;
        var rotation = GetVelocityRotation(blade.VelocityX, blade.VelocityY);

        // Draw outline first (behind sprite) if critical
        if (blade.IsCritical)
        {
            DrawCriticalProjectileOutline("BladeProjectileS", bladeFrameIndex, renderPosition.X, renderPosition.Y, cameraPosition, blade.Team, rotation);
        }

        // Draw main sprite
        if (!TryDrawSprite("BladeProjectileS", bladeFrameIndex, renderPosition.X, renderPosition.Y, cameraPosition, bladeColor, rotation))
        {
            var bladeRectangle = new Rectangle(
                (int)(renderPosition.X - 6f - cameraPosition.X),
                (int)(renderPosition.Y - 2f - cameraPosition.Y),
                12,
                4);
            _spriteBatch.Draw(_pixel, bladeRectangle, bladeColor);
            if (blade.IsCritical)
            {
                var overlayColor = GetCriticalProjectileOverlayColor(blade.Team) * 0.3f;
                _spriteBatch.End();
                _spriteBatch.Begin(
                    SpriteSortMode.Deferred,
                    BlendState.Additive,
                    samplerState: SamplerState.PointClamp,
                    rasterizerState: RasterizerState.CullNone);
                _spriteBatch.Draw(_pixel, bladeRectangle, overlayColor);
                _spriteBatch.End();
                _spriteBatch.Begin(samplerState: SamplerState.PointClamp, rasterizerState: RasterizerState.CullNone);
            }
        }
        else if (blade.IsCritical)
        {
            // Draw screen blend overlay on top
            DrawCriticalProjectileOverlay("BladeProjectileS", bladeFrameIndex, renderPosition.X, renderPosition.Y, cameraPosition, blade.Team, rotation);
        }
    }

    // -----------------------------------------------------------------------
    // Custom procedural flame particle
    // -----------------------------------------------------------------------

    private void DrawFlameProjectiles(Vector2 cameraPosition)
    {
        var normalCells = new System.Collections.Generic.Dictionary<(int, int), float>();
        var criticalBlueCells = new System.Collections.Generic.Dictionary<(int, int), float>();
        var criticalRedCells = new System.Collections.Generic.Dictionary<(int, int), float>();

        foreach (var flame in _world.Flames)
        {
            if (flame.IsCritical)
            {
                var critCells = flame.Team == PlayerTeam.Blue ? criticalBlueCells : criticalRedCells;
                AccumulateFlameParticle(critCells, flame);
            }
            else
            {
                AccumulateFlameParticle(normalCells, flame);
            }
        }

        DrawProceduralFlameCells(normalCells, cameraPosition, useCriticalColors: false, criticalTeam: PlayerTeam.Blue);
        DrawProceduralFlameCells(criticalBlueCells, cameraPosition, useCriticalColors: true, criticalTeam: PlayerTeam.Blue);
        DrawProceduralFlameCells(criticalRedCells, cameraPosition, useCriticalColors: true, criticalTeam: PlayerTeam.Red);
    }

    private void DrawProceduralFlameCells(
        System.Collections.Generic.Dictionary<(int, int), float> cells,
        Vector2 cameraPosition,
        bool topOutlineOnly = false,
        bool useFlareColors = false,
        bool useCriticalColors = false,
        PlayerTeam criticalTeam = PlayerTeam.Blue)
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
            if (useCriticalColors)
            {
                // Critical flames/flares use team colors with fire-like gradients
                if (criticalTeam == PlayerTeam.Blue)
                {
                    if (hasOutline)
                        pixelColor = new Color(0, 80, 200);      // deep blue outline
                    else if (retainedAlpha < 0.75f)
                        pixelColor = new Color(20, 100, 220);    // blue
                    else if (retainedAlpha < 1.30f)
                        pixelColor = new Color(80, 150, 255);    // bright blue
                    else if (retainedAlpha < 2.10f)
                        pixelColor = new Color(150, 200, 255);   // light blue
                    else
                        pixelColor = new Color(220, 240, 255);   // white-blue core
                }
                else // Red team
                {
                    if (hasOutline)
                        pixelColor = new Color(180, 0, 0);       // deep red outline
                    else if (retainedAlpha < 0.75f)
                        pixelColor = new Color(220, 20, 20);     // red
                    else if (retainedAlpha < 1.30f)
                        pixelColor = new Color(255, 80, 80);     // bright red
                    else if (retainedAlpha < 2.10f)
                        pixelColor = new Color(255, 150, 150);   // light red
                    else
                        pixelColor = new Color(255, 220, 220);   // white-red core
                }
            }
            else if (useFlareColors)
            {
                if (hasOutline)
                    pixelColor = new Color(255,  40,  10);  // deep red outline
                else if (retainedAlpha < 0.75f)
                    pixelColor = new Color(255,  90,  55);  // red-orange
                else if (retainedAlpha < 1.30f)
                    pixelColor = new Color(255, 130,  90);  // orange-pink
                else if (retainedAlpha < 2.10f)
                    pixelColor = new Color(255, 200, 150);  // warm yellow-orange
                else
                    pixelColor = new Color(255, 245, 190);  // yellow-white core
            }
            else
            {
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
            }

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
        DrawProceduralFlameCells(cells, cameraPosition, topOutlineOnly, useFlareColors: false, useCriticalColors: false);
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
        float trajectoryStretch = 1f,
        bool includeHornAccent = false)
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

        float accentCircleRadius = 0f;
        float accentCircleCx = 0f;
        float accentCircleCy = 0f;
        float h1LargeR = 0f;
        float h2LargeR = 0f;
        float h1lCx = 0f;
        float h1lCy = 0f;
        float h2lCx = 0f;
        float h2lCy = 0f;
        float h3LargeR = 0f;
        float h3Cx = 0f;
        float h3Cy = 0f;

        if (includeHornAccent)
        {
            accentCircleRadius = 4.0f * scale;
            var accentOffset = (baseR * 0.9f) + (accentCircleRadius * 0.35f);
            accentCircleCx = cx + oppositeMotionDirection.X * accentOffset;
            accentCircleCy = cy + oppositeMotionDirection.Y * accentOffset;
        }
        else
        {
            // Per-flame random: which horn (left=1, right=2) is the big one.
            var bigOnLeft = GetDeterministicUnitFloat(seed, salt: 151) < 0.5f;
            var hornBigR   = 5.5f * scale;
            var hornSmallLargeR = 2.8f * scale;
            h1LargeR = bigOnLeft ? hornBigR : hornSmallLargeR;
            h2LargeR = bigOnLeft ? hornSmallLargeR : hornBigR;

            // Horn base circles stay biased upward, but their axis tilts backward against motion.
            // Perpendicular separation stays asymmetric so the big horn sits more central.
            var h1SepX  = (bigOnLeft ? 2.5f : 3.5f) * scale;
            var h2SepX  = (bigOnLeft ? 3.5f : 2.5f) * scale;
            var hornRiseY = 5f * scale;
            var hornWaveX = hornPerpendicular.X * hornSway;
            var hornWaveY = hornPerpendicular.Y * hornSway;
            h1lCx = cx + hornAxis.X * hornRiseY - hornPerpendicular.X * h1SepX + hornWaveX;
            h1lCy = cy + hornAxis.Y * hornRiseY - hornPerpendicular.Y * h1SepX + hornWaveY;
            h2lCx = cx + hornAxis.X * hornRiseY + hornPerpendicular.X * h2SepX + hornWaveX;
            h2lCy = cy + hornAxis.Y * hornRiseY + hornPerpendicular.Y * h2SepX + hornWaveY;

            h3LargeR = 2.6f * scale;
            h3Cx = cx + hornAxis.X * (hornRiseY + (2.0f * scale));
            h3Cy = cy + hornAxis.Y * (hornRiseY + (2.0f * scale));
        }

        // noiseRadius: how far outside the hard boundary the edge fuzz extends.
        var noiseRadius = 2f * scale;

        var maxCircleRadius = includeHornAccent
            ? MathF.Max(baseR, accentCircleRadius)
            : MathF.Max(baseR, MathF.Max(h1LargeR, h2LargeR));
        var extraStretchRadius = (clampedStretch - 1f) * maxCircleRadius;
        var minX = cx - baseR;
        var maxX = cx + baseR;
        var minY = cy - baseR;
        var maxY = cy + baseR;

        if (includeHornAccent)
        {
            minX = MathF.Min(minX, accentCircleCx - accentCircleRadius);
            maxX = MathF.Max(maxX, accentCircleCx + accentCircleRadius);
            minY = MathF.Min(minY, accentCircleCy - accentCircleRadius);
            maxY = MathF.Max(maxY, accentCircleCy + accentCircleRadius);
        }
        else
        {
            minX = MathF.Min(minX, MathF.Min(h1lCx - h1LargeR, h2lCx - h2LargeR));
            maxX = MathF.Max(maxX, MathF.Max(h1lCx + h1LargeR, h2lCx + h2LargeR));
            minY = MathF.Min(minY, MathF.Min(h1lCy - h1LargeR, h2lCy - h2LargeR));
            maxY = MathF.Max(maxY, MathF.Max(h1lCy + h1LargeR, h2lCy + h2LargeR));
        }

        minX -= noiseRadius + extraStretchRadius;
        maxX += noiseRadius + extraStretchRadius;
        minY -= noiseRadius + extraStretchRadius;
        maxY += noiseRadius + extraStretchRadius;

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
                    includeHornAccent
                        ? CircleSDF(cellCX, cellCY, accentCircleCx, accentCircleCy, accentCircleRadius, trajectoryDirection, clampedStretch)
                        : MathF.Min(
                            CircleSDF(cellCX, cellCY, h1lCx, h1lCy, h1LargeR, trajectoryDirection, clampedStretch),
                            MathF.Min(
                                CircleSDF(cellCX, cellCY, h2lCx, h2lCy, h2LargeR, trajectoryDirection, clampedStretch),
                                CircleSDF(cellCX, cellCY, h3Cx, h3Cy, h3LargeR, trajectoryDirection, clampedStretch))));

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
            var position = new Vector2(renderPosition.X - cameraPosition.X, renderPosition.Y - cameraPosition.Y);
            var scale = new Vector2(flameScale, flameScale);

            // Draw outline first (behind sprite) if critical
            if (flame.IsCritical)
            {
                var teamColor = GetCriticalProjectileOverlayColor(flame.Team);
                var outlineTint = Color.Lerp(teamColor, Color.White, 0.75f);

                if (_uberOutlineEnabled)
                {
                    DrawSpriteFrameOutline(flameSprite.Frames[frameIndex], position, outlineTint, 0f, flameSprite.Origin.ToVector2(), scale);
                }
            }

            // Draw main sprite
            DrawLoadedSpriteFrame(
                flameSprite.Frames[frameIndex],
                position,
                null,
                flameColor,
                0f,
                flameSprite.Origin.ToVector2(),
                scale,
                SpriteEffects.None,
                0f);

            // Draw screen blend overlay on top if critical
            if (flame.IsCritical)
            {
                var teamColor = GetCriticalProjectileOverlayColor(flame.Team);
                DrawSpriteFrameScreenColor(
                    flameSprite.Frames[frameIndex],
                    position,
                    teamColor * 0.5f,
                    0f,
                    flameSprite.Origin.ToVector2(),
                    scale);
            }
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
        if (flame.IsCritical)
        {
            // Additive blend for critical effect on fallback rectangle
            var overlayColor = GetCriticalProjectileOverlayColor(flame.Team) * 0.3f;
            _spriteBatch.End();
            _spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.Additive,
                samplerState: SamplerState.PointClamp,
                rasterizerState: RasterizerState.CullNone);
            _spriteBatch.Draw(_pixel, flameRectangle, overlayColor);
            _spriteBatch.End();
            _spriteBatch.Begin(samplerState: SamplerState.PointClamp, rasterizerState: RasterizerState.CullNone);
        }
    }

    private static float GetFlameProjectileScale(FlameProjectileEntity flame)
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
        // Use interpolated render position for smoke spawning consistency
        var renderPosition = GetRenderPosition(flame.Id, flame.X, flame.Y);
        return new Vector2(renderPosition.X, renderPosition.Y - 3.4f * scale);
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
        if (!frame.TryCopyPixelData(pixels))
        {
            frame.Texture.GetData(0, sourceRectangle, pixels, 0, pixels.Length);
        }

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
        if (_flameRenderMode == 0)
        {
            DrawFlareProjectileAsParticle(flare, cameraPosition);
            return;
        }

        var renderPosition = GetRenderPosition(flare.Id, flare.X, flare.Y);
        var rotation = GetVelocityRotation(flare.VelocityX, flare.VelocityY);

        // Draw outline first (behind sprite) if critical
        if (flare.IsCritical)
        {
            DrawCriticalProjectileOutline("FlareS", 0, renderPosition.X, renderPosition.Y, cameraPosition, flare.Team, rotation);
        }

        // Draw main sprite
        if (!TryDrawSprite("FlareS", 0, renderPosition.X, renderPosition.Y, cameraPosition, Color.White, rotation))
        {
            var flareRectangle = new Rectangle(
                (int)(renderPosition.X - 4f - cameraPosition.X),
                (int)(renderPosition.Y - 2f - cameraPosition.Y),
                8,
                4);
            _spriteBatch.Draw(_pixel, flareRectangle, Color.White);
            if (flare.IsCritical)
            {
                var overlayColor = GetCriticalProjectileOverlayColor(flare.Team) * 0.3f;
                _spriteBatch.End();
                _spriteBatch.Begin(
                    SpriteSortMode.Deferred,
                    BlendState.Additive,
                    samplerState: SamplerState.PointClamp,
                    rasterizerState: RasterizerState.CullNone);
                _spriteBatch.Draw(_pixel, flareRectangle, overlayColor);
                _spriteBatch.End();
                _spriteBatch.Begin(samplerState: SamplerState.PointClamp, rasterizerState: RasterizerState.CullNone);
            }
        }
        else if (flare.IsCritical)
        {
            // Draw screen blend overlay on top
            DrawCriticalProjectileOverlay("FlareS", 0, renderPosition.X, renderPosition.Y, cameraPosition, flare.Team, rotation);
        }
    }

    private void DrawFlareProjectileAsParticle(FlareProjectileEntity flare, Vector2 cameraPosition)
    {
        var cells = new System.Collections.Generic.Dictionary<(int, int), float>();
        AccumulateFlareParticle(cells, flare);
        DrawProceduralFlameCells(cells, cameraPosition, useFlareColors: !flare.IsCritical, useCriticalColors: flare.IsCritical, criticalTeam: flare.Team);
    }

    private void AccumulateFlareParticle(
        System.Collections.Generic.Dictionary<(int, int), float> cells,
        FlareProjectileEntity flare)
    {
        var renderPosition = GetRenderPosition(flare.Id, flare.X, flare.Y);
        var scale = GetFlareProjectileScale(flare);
        AccumulateProceduralFlameParticle(
            cells,
            flare.Id,
            renderPosition.X,
            renderPosition.Y,
            scale,
            alphaScale: 1f,
            motionX: flare.VelocityX,
            motionY: flare.VelocityY,
            trajectoryStretch: 1.65f,
            includeHornAccent: true);
    }

    private float GetFlareProjectileScale(FlareProjectileEntity flare)
    {
        var lifeProgress = Math.Clamp(
            (FlareProjectileEntity.LifetimeTicks - flare.TicksRemaining) / (float)Math.Max(1, FlareProjectileEntity.LifetimeTicks),
            0f,
            1f);

        var randomGrowthRate = MathHelper.Lerp(0.9f, 1.1f, GetDeterministicUnitFloat(flare.Id, salt: 19));
        var startScale = MathHelper.Lerp(0.78f, 0.92f, GetDeterministicUnitFloat(flare.Id, salt: 23));
        var maxScale = MathHelper.Lerp(1.00f, 1.16f, GetDeterministicUnitFloat(flare.Id, salt: 29));
        var growthProgress = MathF.Pow(lifeProgress, randomGrowthRate);
        return MathHelper.Lerp(startScale, maxScale, growthProgress);
    }

    private void DrawRocketProjectile(RocketProjectileEntity rocket, Vector2 cameraPosition)
    {
        var renderPosition = GetRenderPosition(rocket.Id, rocket.X, rocket.Y);
        var rocketColor = ResolveProjectileTint(
            rocket.Team,
            new Color(120, 180, 255),
            new Color(255, 110, 90),
            new Color(230, 220, 210));
        var rocketFrame = rocket.Team == PlayerTeam.Blue ? 0 : 1;
        var rocketScale = rocket.ExperimentalVisualScale;

        // Draw outline first (behind sprite) if critical
        if (rocket.IsCritical)
        {
            DrawCriticalProjectileOutline("RocketS", rocketFrame, renderPosition.X, renderPosition.Y, cameraPosition, rocket.Team, rocket.DirectionRadians, new Vector2(rocketScale, rocketScale));
        }

        // Draw main sprite
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
            if (rocket.IsCritical)
            {
                var overlayColor = GetCriticalProjectileOverlayColor(rocket.Team) * 0.3f;
                _spriteBatch.End();
                _spriteBatch.Begin(
                    SpriteSortMode.Deferred,
                    BlendState.Additive,
                    samplerState: SamplerState.PointClamp,
                    rasterizerState: RasterizerState.CullNone);
                _spriteBatch.Draw(_pixel, rocketRectangle, overlayColor);
                _spriteBatch.End();
                _spriteBatch.Begin(samplerState: SamplerState.PointClamp, rasterizerState: RasterizerState.CullNone);
            }
        }
        else if (rocket.IsCritical)
        {
            // Draw screen blend overlay on top
            DrawCriticalProjectileOverlay("RocketS", rocketFrame, renderPosition.X, renderPosition.Y, cameraPosition, rocket.Team, rocket.DirectionRadians, new Vector2(rocketScale, rocketScale));
        }
    }

    private void DrawMineProjectile(MineProjectileEntity mine, Vector2 cameraPosition)
    {
        var renderPosition = GetRenderPosition(mine.Id, mine.X, mine.Y);
        var frameIndex = (mine.Team == PlayerTeam.Blue ? 2 : 0)
            + (mine.IsStickied ? 1 : 0);

        // Draw outline first (behind sprite) if critical
        if (mine.IsCritical)
        {
            DrawCriticalProjectileOutline("MineS", frameIndex, renderPosition.X, renderPosition.Y, cameraPosition, mine.Team);
        }

        // Draw main sprite
        if (!TryDrawSprite("MineS", frameIndex, renderPosition.X, renderPosition.Y, cameraPosition, Color.White))
        {
            var mineRectangle = new Rectangle(
                (int)(renderPosition.X - 5f - cameraPosition.X),
                (int)(renderPosition.Y - 5f - cameraPosition.Y),
                10,
                10);
            _spriteBatch.Draw(_pixel, mineRectangle, Color.White);
            if (mine.IsCritical)
            {
                var overlayColor = GetCriticalProjectileOverlayColor(mine.Team) * 0.3f;
                _spriteBatch.End();
                _spriteBatch.Begin(
                    SpriteSortMode.Deferred,
                    BlendState.Additive,
                    samplerState: SamplerState.PointClamp,
                    rasterizerState: RasterizerState.CullNone);
                _spriteBatch.Draw(_pixel, mineRectangle, overlayColor);
                _spriteBatch.End();
                _spriteBatch.Begin(samplerState: SamplerState.PointClamp, rasterizerState: RasterizerState.CullNone);
            }
        }
        else if (mine.IsCritical)
        {
            // Draw screen blend overlay on top
            DrawCriticalProjectileOverlay("MineS", frameIndex, renderPosition.X, renderPosition.Y, cameraPosition, mine.Team);
        }
    }
}
