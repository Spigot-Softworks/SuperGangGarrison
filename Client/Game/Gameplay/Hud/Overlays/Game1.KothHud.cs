#nullable enable

using Microsoft.Xna.Framework;
using System.Globalization;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void DrawKothHud()
    {
        var viewportWidth = ViewportWidth;
        var viewportHeight = ViewportHeight;
        var centerX = viewportWidth / 2f;

        DrawKothPointStatus();
        DrawKothTeamTimer(HudElementId.MatchKothRedTimer, centerX - 132f, viewportHeight - 28f, PlayerTeam.Red, _world.KothRedTimerTicksRemaining, IsKothTimerActive(PlayerTeam.Red));
        DrawKothTeamTimer(HudElementId.MatchKothBlueTimer, centerX + 132f, viewportHeight - 28f, PlayerTeam.Blue, _world.KothBlueTimerTicksRemaining, IsKothTimerActive(PlayerTeam.Blue));

        if (_world.KothUnlockTicksRemaining > 0)
        {
            DrawHudTextCentered($"Unlock {FormatHudTimerText(_world.KothUnlockTicksRemaining)}", new Vector2(centerX, 30f), Color.White, 1f);
        }
        else
        {
            DrawMatchTimerHud(centerX);
        }
    }

    private void DrawKothPointStatus()
    {
        if (_world.ControlPoints.Count == 0)
        {
            return;
        }

        if (!TryResolveHudElement(HudElementId.MatchObjectiveStatus, out var resolved))
        {
            return;
        }

        var origin = resolved.Origin;
        var scale = resolved.Layout.Scale;
        var objectiveHudY = origin.Y;
        var objectiveHudCounterY = origin.Y + (3f * scale);
        var drawX = origin.X - MathF.Floor(_world.ControlPoints.Count / 2f) * 48f * scale;
        var firstX = drawX;
        for (var index = 0; index < _world.ControlPoints.Count; index += 1)
        {
            var point = _world.ControlPoints[index];
            var teamOffset = point.Team switch
            {
                PlayerTeam.Red => 60,
                PlayerTeam.Blue => 90,
                _ => point.CappingTeam == PlayerTeam.Red ? 30 : 0,
            };

            var progressFrame = teamOffset;
            if (point.CappingTicks > 0f && point.CapTimeTicks > 0)
            {
                var progress = point.CappingTicks / point.CapTimeTicks;
                progressFrame = teamOffset + Math.Clamp((int)MathF.Floor(progress * 30f), 0, 30);
            }

            TryDrawScreenSprite("ControlPointStatusS", progressFrame, new Vector2(drawX, objectiveHudY), Color.White, new Vector2(3f * scale, 3f * scale));

            if (point.IsLocked)
            {
                TryDrawScreenSprite("ControlPointLockS", 0, new Vector2(drawX, objectiveHudY), Color.White, new Vector2(3f * scale, 3f * scale));
            }

            if (point.Cappers > 0 && !point.IsLocked)
            {
                TryDrawScreenSprite("ControlPointCappersS", 0, new Vector2(drawX, objectiveHudY), Color.White, new Vector2(3f * scale, 3f * scale));
                DrawHudTextCentered(point.Cappers.ToString(CultureInfo.InvariantCulture), new Vector2(drawX + (13f * scale), objectiveHudCounterY), Color.Black, 1.5f * scale);
            }

            drawX += 60f * scale;
        }

        var lastX = drawX - (60f * scale);
        UpdateHudElementBounds(
            HudElementId.MatchObjectiveStatus,
            new Rectangle(
                (int)MathF.Round(firstX - (24f * scale)),
                (int)MathF.Round(origin.Y - (24f * scale)),
                Math.Max(1, (int)MathF.Round((lastX - firstX) + (72f * scale))),
                Math.Max(1, (int)MathF.Round(64f * scale))));
    }

    private void DrawKothTeamTimer(string elementId, float defaultCenterX, float defaultY, PlayerTeam team, int ticksRemaining, bool isActive)
    {
        var center = new Vector2(defaultCenterX, defaultY);
        var scale = 1f;
        if (TryResolveHudElement(elementId, out var resolved))
        {
            center = resolved.Origin;
            scale = resolved.Layout.Scale;
            UpdateHudElementBounds(
                elementId,
                new Rectangle(
                    (int)MathF.Round(center.X - (54f * scale)),
                    (int)MathF.Round(center.Y - (24f * scale)),
                    Math.Max(1, (int)MathF.Round(108f * scale)),
                    Math.Max(1, (int)MathF.Round(46f * scale))));
        }

        var color = team == PlayerTeam.Red
            ? new Color(220, 110, 90)
            : new Color(100, 160, 235);
        var label = team == PlayerTeam.Red ? "RED" : "BLU";
        var textColor = isActive ? color : Color.White;

        DrawHudTextCentered(FormatHudTimerText(ticksRemaining), center, textColor, 2f * scale);
        DrawHudTextCentered(label, center + new Vector2(0f, -18f * scale), textColor * 0.95f, 1f * scale);
    }

    private bool IsKothTimerActive(PlayerTeam team)
    {
        if (_world.MatchRules.Mode == GameModeKind.KingOfTheHill)
        {
            for (var index = 0; index < _world.ControlPoints.Count; index += 1)
            {
                var point = _world.ControlPoints[index];
                if (point.Marker.IsSingleKothControlPoint())
                {
                    return point.Team == team;
                }
            }

            return false;
        }

        for (var index = 0; index < _world.ControlPoints.Count; index += 1)
        {
            var point = _world.ControlPoints[index];
            if (team == PlayerTeam.Red && point.Marker.IsBlueKothControlPoint())
            {
                return point.Team == PlayerTeam.Red;
            }

            if (team == PlayerTeam.Blue && point.Marker.IsRedKothControlPoint())
            {
                return point.Team == PlayerTeam.Blue;
            }
        }

        return false;
    }
}
