#nullable enable

using Microsoft.Xna.Framework;
using System;
using System.Globalization;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void DrawTeamDeathmatchHud()
    {
        var viewportHeight = ViewportHeight;
        var panelOrigin = new Vector2(ViewportWidth / 2f, viewportHeight);
        var panelScale = 1f;
        if (TryResolveHudElement(HudElementId.MatchCtfPanel, out var ctfPanel))
        {
            panelOrigin = ctfPanel.Origin;
            panelScale = ctfPanel.Layout.Scale;
        }

        Rectangle scorePanelBounds;
        if (!TryDrawTeamDeathmatchScorePanelSprite(panelOrigin, panelScale, out scorePanelBounds))
        {
            DrawFallbackScorePanelHud(panelOrigin, panelScale);
            scorePanelBounds = new Rectangle(
                (int)MathF.Round(panelOrigin.X - (168f * panelScale)),
                (int)MathF.Round(panelOrigin.Y - (72f * panelScale)),
                Math.Max(1, (int)MathF.Round(336f * panelScale)),
                Math.Max(1, (int)MathF.Round(54f * panelScale)));
        }
        else if (TryResolveHudElement(HudElementId.MatchCtfPanel, out _))
        {
            UpdateHudElementBounds(HudElementId.MatchCtfPanel, scorePanelBounds);
        }

        var scoreScale = 2f * panelScale;
        DrawHudTextCentered(
            _world.RedCaps.ToString(CultureInfo.InvariantCulture),
            GetTeamDeathmatchScorePanelScorePosition(scorePanelBounds, redTeam: true),
            Color.Black,
            scoreScale);
        DrawHudTextCentered(
            _world.BlueCaps.ToString(CultureInfo.InvariantCulture),
            GetTeamDeathmatchScorePanelScorePosition(scorePanelBounds, redTeam: false),
            Color.Black,
            scoreScale);
        DrawScorePanelCapLimit(panelOrigin, panelScale);
        DrawMatchTimerHud(ViewportWidth / 2f);
    }
}
