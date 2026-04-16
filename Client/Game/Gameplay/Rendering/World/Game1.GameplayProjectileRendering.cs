#nullable enable

using OpenGarrison.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace OpenGarrison.Client;

public partial class Game1
{
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

        foreach (var flame in _world.Flames)
        {
            DrawFlameProjectile(flame, cameraPosition);
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

    private void DrawFlameProjectile(FlameProjectileEntity flame, Vector2 cameraPosition)
    {
        var renderPosition = GetRenderPosition(flame.Id, flame.X, flame.Y);
        var flameColor = Color.White;
        var flameSprite = GetResolvedSprite("FlameS");
        if (flameSprite is not null && flameSprite.Frames.Count > 0)
        {
            var flameAgeTicks = flame.IsAttached
                ? FlameProjectileEntity.AttachedLifetimeTicks - flame.TicksRemaining
                : FlameProjectileEntity.AirLifetimeTicks - flame.TicksRemaining;
            var frameIndex = Math.Clamp(
                Math.Abs(flameAgeTicks) % flameSprite.Frames.Count,
                0,
                flameSprite.Frames.Count - 1);
            DrawLoadedSpriteFrame(
                flameSprite.Frames[frameIndex],
                new Vector2(renderPosition.X - cameraPosition.X, renderPosition.Y - cameraPosition.Y),
                null,
                flameColor,
                0f,
                flameSprite.Origin.ToVector2(),
                Vector2.One,
                SpriteEffects.None,
                0f);
            return;
        }

        var flameSize = flame.IsAttached ? 8 : 6;
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
