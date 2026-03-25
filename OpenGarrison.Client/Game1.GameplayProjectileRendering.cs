#nullable enable

using OpenGarrison.Core;
using Microsoft.Xna.Framework;
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
        var beamColor = healTarget.Team == PlayerTeam.Blue
            ? new Color(100, 170, 255, 77)
            : new Color(255, 110, 110, 77);
        DrawWorldLine(
            medic.X + MathF.Cos(aimRadians) * 25f,
            medic.Y + MathF.Sin(aimRadians) * 24f,
            healTarget.X,
            healTarget.Y,
            cameraPosition,
            beamColor,
            5f);
    }

    private void DrawGameplayEffectsAndProjectiles(Vector2 cameraPosition)
    {
        DrawExplosionVisuals(cameraPosition);
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
        DrawMedicBeams(cameraPosition);

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
        var flameColor = flame.Team == PlayerTeam.Blue
            ? new Color(120, 200, 255)
            : new Color(255, 170, 90);
        if (flame.IsAttached)
        {
            flameColor = new Color(255, 120, 60);
        }

        if (!TryDrawSprite("FlameS", 0, renderPosition.X, renderPosition.Y, cameraPosition, flameColor, GetVelocityRotation(flame.VelocityX, flame.VelocityY)))
        {
            var flameSize = flame.IsAttached ? 8 : 6;
            var flameRectangle = new Rectangle(
                (int)(renderPosition.X - flameSize / 2f - cameraPosition.X),
                (int)(renderPosition.Y - flameSize / 2f - cameraPosition.Y),
                flameSize,
                flameSize);
            _spriteBatch.Draw(_pixel, flameRectangle, flameColor);
        }
    }

    private void DrawRocketProjectile(RocketProjectileEntity rocket, Vector2 cameraPosition)
    {
        var renderPosition = GetRenderPosition(rocket.Id, rocket.X, rocket.Y);
        var rocketColor = rocket.Team == PlayerTeam.Blue
            ? new Color(120, 180, 255)
            : new Color(255, 110, 90);
        var rocketFrame = rocket.Team == PlayerTeam.Blue ? 0 : 1;
        if (!TryDrawSprite("RocketS", rocketFrame, renderPosition.X, renderPosition.Y, cameraPosition, rocketColor, rocket.DirectionRadians))
        {
            var rocketRectangle = new Rectangle(
                (int)(renderPosition.X - 5f - cameraPosition.X),
                (int)(renderPosition.Y - 3f - cameraPosition.Y),
                10,
                6);
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
