#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void DrawIntel(TeamIntelligenceState intelState, Vector2 cameraPosition)
    {
        if (intelState.IsCarried)
        {
            return;
        }

        var renderPosition = GetRenderIntelPosition(intelState);
        var spriteName = intelState.Team == PlayerTeam.Blue ? "IntelligenceBlueS" : "IntelligenceRedS";
        if (!TryDrawSprite(spriteName, 0, renderPosition.X, renderPosition.Y, cameraPosition, Color.White))
        {
            var fallbackColor = intelState.Team == PlayerTeam.Blue
                ? new Color(130, 185, 255)
                : new Color(255, 135, 135);
            var intelRectangle = new Rectangle(
                (int)(renderPosition.X - 8f - cameraPosition.X),
                (int)(renderPosition.Y - 8f - cameraPosition.Y),
                16,
                16);
            _spriteBatch.Draw(_pixel, intelRectangle, fallbackColor);
        }

        if (!intelState.IsDropped)
        {
            return;
        }

        var timerFrame = Math.Clamp((int)MathF.Floor((intelState.ReturnTicksRemaining / 900f) * 12f), 0, 12);
        if (intelState.Team == PlayerTeam.Blue)
        {
            timerFrame += 12;
        }

        var timerSprite = _runtimeAssets.GetSprite("IntelTimerS");
        if (timerSprite is not null && timerSprite.Frames.Count > 0)
        {
            var clampedFrameIndex = Math.Clamp(timerFrame, 0, timerSprite.Frames.Count - 1);
            _spriteBatch.Draw(
                timerSprite.Frames[clampedFrameIndex],
                new Vector2(renderPosition.X + 2f - cameraPosition.X, renderPosition.Y - 25f - cameraPosition.Y),
                null,
                Color.White,
                0f,
                timerSprite.Origin.ToVector2(),
                new Vector2(2f, 2f),
                SpriteEffects.None,
                0f);
            return;
        }

        var timerWidth = Math.Max(4, (int)(20f * intelState.ReturnTicksRemaining / 900f));
        var timerRectangle = new Rectangle(
            (int)(renderPosition.X - 10f - cameraPosition.X),
            (int)(renderPosition.Y - 18f - cameraPosition.Y),
            timerWidth,
            4);
        _spriteBatch.Draw(_pixel, timerRectangle, new Color(255, 235, 120));
    }

    private void DrawArenaControlPoint(Vector2 cameraPosition)
    {
        var pointMarker = _world.Level.GetFirstRoomObject(RoomObjectType.ArenaControlPoint);
        if (!pointMarker.HasValue)
        {
            return;
        }

        var spriteName = _world.ArenaPointTeam switch
        {
            PlayerTeam.Red => "ControlPointRedS",
            PlayerTeam.Blue => "ControlPointBlueS",
            _ => "ControlPointNeutralS",
        };

        var pulseAlpha = 0.5f + (0.5f * MathF.Sin((float)_world.Frame * 0.1f));
        TryDrawSprite(spriteName, 0, pointMarker.Value.X, pointMarker.Value.Y, cameraPosition, Color.White);
        TryDrawSprite(spriteName, 1, pointMarker.Value.X, pointMarker.Value.Y, cameraPosition, Color.White * pulseAlpha);
    }

    private void DrawControlPoints(Vector2 cameraPosition)
    {
        if (_world.ControlPoints.Count == 0)
        {
            return;
        }

        var pulseAlpha = 0.5f + (0.5f * MathF.Sin((float)_world.Frame * 0.1f));
        for (var index = 0; index < _world.ControlPoints.Count; index += 1)
        {
            var point = _world.ControlPoints[index];
            DrawControlPointHealingAura(point, cameraPosition);
            var spriteName = point.Team switch
            {
                PlayerTeam.Red => "ControlPointRedS",
                PlayerTeam.Blue => "ControlPointBlueS",
                _ => "ControlPointNeutralS",
            };

            TryDrawSprite(spriteName, 0, point.Marker.X, point.Marker.Y, cameraPosition, Color.White);
            if (point.CappingTicks > 0f)
            {
                TryDrawSprite(spriteName, 1, point.Marker.X, point.Marker.Y, cameraPosition, Color.White * pulseAlpha);
            }
        }
    }

    private void DrawControlPointHealingAura(ControlPointState point, Vector2 cameraPosition)
    {
        if (!point.HasHealingAura)
        {
            return;
        }

        var animationFrame = (int)((_world.Frame / 9) % 4);
        var pulseAlpha = 0.12f + (animationFrame * 0.03f);
        var outlineColor = new Color(72, 184, 96);
        var highlightColor = new Color(156, 248, 180);
        var accentColor = new Color(224, 255, 224);
        var center = new Vector2(point.HealingAuraCenterX, point.HealingAuraCenterY);
        var baseWidth = MathF.Max(36f, point.HealingAuraWidth * 0.82f);
        var baseHeight = MathF.Max(24f, point.HealingAuraHeight * 0.72f);

        for (var ringIndex = 0; ringIndex < 2; ringIndex += 1)
        {
            var step = (animationFrame + (ringIndex * 2)) % 4;
            var ringWidth = baseWidth + (step * 12f);
            var ringHeight = baseHeight + (step * 8f);
            var ringAlpha = ringIndex == 0
                ? 0.24f + (pulseAlpha * 0.35f)
                : 0.12f + (pulseAlpha * 0.2f);
            DrawWorldRectOutline(center, ringWidth, ringHeight, cameraPosition, outlineColor * ringAlpha, thickness: 2);
        }

        DrawWorldRectOutline(center, baseWidth - 10f, baseHeight - 8f, cameraPosition, highlightColor * 0.22f, thickness: 2);
        DrawWorldLine(center.X - 5f, center.Y, center.X + 5f, center.Y, cameraPosition, accentColor * 0.2f, 2f);
        DrawWorldLine(center.X, center.Y - 5f, center.X, center.Y + 5f, cameraPosition, accentColor * 0.2f, 2f);
        TryDrawSprite("ControlPointNeutralS", 1, point.Marker.X, point.Marker.Y, cameraPosition, accentColor * (0.24f + pulseAlpha));
    }

    private void DrawWorldRectOutline(Vector2 center, float width, float height, Vector2 cameraPosition, Color color, int thickness)
    {
        if (width <= 1f || height <= 1f || thickness <= 0)
        {
            return;
        }

        var roundedWidth = Math.Max(2, (int)MathF.Round(width));
        var roundedHeight = Math.Max(2, (int)MathF.Round(height));
        var screenX = (int)MathF.Round(center.X - (roundedWidth / 2f) - cameraPosition.X);
        var screenY = (int)MathF.Round(center.Y - (roundedHeight / 2f) - cameraPosition.Y);
        var top = new Rectangle(screenX, screenY, roundedWidth, thickness);
        var bottom = new Rectangle(screenX, screenY + roundedHeight - thickness, roundedWidth, thickness);
        var left = new Rectangle(screenX, screenY, thickness, roundedHeight);
        var right = new Rectangle(screenX + roundedWidth - thickness, screenY, thickness, roundedHeight);
        _spriteBatch.Draw(_pixel, top, color);
        _spriteBatch.Draw(_pixel, bottom, color);
        _spriteBatch.Draw(_pixel, left, color);
        _spriteBatch.Draw(_pixel, right, color);
    }

    private void DrawGenerators(Vector2 cameraPosition)
    {
        if (_world.Generators.Count == 0)
        {
            return;
        }

        for (var index = 0; index < _world.Generators.Count; index += 1)
        {
            var generator = _world.Generators[index];
            if (generator.IsDestroyed)
            {
                continue;
            }

            var spriteName = generator.Team == PlayerTeam.Blue ? "GeneratorBlueS" : "GeneratorRedS";
            var frameIndex = GetGeneratorAnimationFrame(generator);
            if (TryDrawSprite(spriteName, frameIndex, generator.Marker.CenterX, generator.Marker.CenterY, cameraPosition, Color.White))
            {
                continue;
            }

            var width = Math.Max(10, (int)generator.Marker.Width);
            var height = Math.Max(10, (int)generator.Marker.Height);
            var rectangle = new Rectangle(
                (int)(generator.Marker.CenterX - (width / 2f) - cameraPosition.X),
                (int)(generator.Marker.CenterY - (height / 2f) - cameraPosition.Y),
                width,
                height);
            var fallbackColor = generator.Team == PlayerTeam.Blue
                ? new Color(100, 160, 235)
                : new Color(220, 110, 90);
            _spriteBatch.Draw(_pixel, rectangle, fallbackColor);
        }
    }

    private int GetGeneratorAnimationFrame(GeneratorState generator)
    {
        const int framesPerDamageStage = 16;
        var stageOffset = generator.DamageStage * framesPerDamageStage;
        var animationFrame = (int)MathF.Floor((_world.Frame * 0.3f) % framesPerDamageStage);
        return stageOffset + animationFrame;
    }
}
