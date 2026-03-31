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

        DrawKothPointStatus(centerX);
        DrawKothTeamTimer(centerX - 132f, viewportHeight - 28f, PlayerTeam.Red, _world.KothRedTimerTicksRemaining, IsKothTimerActive(PlayerTeam.Red));
        DrawKothTeamTimer(centerX + 132f, viewportHeight - 28f, PlayerTeam.Blue, _world.KothBlueTimerTicksRemaining, IsKothTimerActive(PlayerTeam.Blue));

        if (_world.KothUnlockTicksRemaining > 0)
        {
            DrawHudTextCentered($"Unlock {FormatHudTimerText(_world.KothUnlockTicksRemaining)}", new Vector2(centerX, 30f), Color.White, 1f);
        }
    }

    private void DrawKothPointStatus(float centerX)
    {
        if (_world.ControlPoints.Count == 0)
        {
            return;
        }

        var drawX = centerX - MathF.Floor(_world.ControlPoints.Count / 2f) * 48f;
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

            TryDrawScreenSprite("ControlPointStatusS", progressFrame, new Vector2(drawX, 560f), Color.White, new Vector2(3f, 3f));

            if (point.IsLocked)
            {
                TryDrawScreenSprite("ControlPointLockS", 0, new Vector2(drawX, 560f), Color.White, new Vector2(3f, 3f));
            }

            if (point.Cappers > 0 && !point.IsLocked)
            {
                TryDrawScreenSprite("ControlPointCappersS", 0, new Vector2(drawX, 560f), Color.White, new Vector2(3f, 3f));
                DrawHudTextCentered(point.Cappers.ToString(CultureInfo.InvariantCulture), new Vector2(drawX + 13f, 563f), Color.Black, 1.5f);
            }

            drawX += 60f;
        }
    }

    private void DrawKothTeamTimer(float centerX, float y, PlayerTeam team, int ticksRemaining, bool isActive)
    {
        var color = team == PlayerTeam.Red
            ? new Color(220, 110, 90)
            : new Color(100, 160, 235);
        var label = team == PlayerTeam.Red ? "RED" : "BLU";
        var textColor = isActive ? color : Color.White;

        DrawHudTextCentered(FormatHudTimerText(ticksRemaining), new Vector2(centerX, y), textColor, 2f);
        DrawHudTextCentered(label, new Vector2(centerX, y - 18f), textColor * 0.95f, 1f);
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
