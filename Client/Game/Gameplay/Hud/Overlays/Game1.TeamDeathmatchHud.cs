#nullable enable

using Microsoft.Xna.Framework;
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
            UpdateHudElementBounds(
                HudElementId.MatchCtfPanel,
                new Rectangle(
                    (int)MathF.Round(panelOrigin.X - (180f * panelScale)),
                    (int)MathF.Round(panelOrigin.Y - (72f * panelScale)),
                    Math.Max(1, (int)MathF.Round(360f * panelScale)),
                    Math.Max(1, (int)MathF.Round(78f * panelScale))));
        }

        Vector2 PanelPoint(float xOffset, float yOffset) => panelOrigin + new Vector2(xOffset * panelScale, yOffset * panelScale);

        if (!TryDrawScreenSprite("CTFHUDS", 0, PanelPoint(1f, 100f), Color.White, new Vector2(3f * panelScale, 3f * panelScale)))
        {
            DrawFallbackScorePanelHud(panelOrigin, panelScale);
        }

        DrawHudTextCentered(_world.RedCaps.ToString(CultureInfo.InvariantCulture), PanelPoint(-135f, -30f), Color.Black, 2f * panelScale);
        DrawHudTextCentered(_world.BlueCaps.ToString(CultureInfo.InvariantCulture), PanelPoint(130f, -30f), Color.Black, 2f * panelScale);
        DrawScorePanelCapLimit(panelOrigin, panelScale);
        DrawMatchTimerHud(ViewportWidth / 2f);
    }
}
