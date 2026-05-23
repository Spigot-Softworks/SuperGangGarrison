#nullable enable

using Microsoft.Xna.Framework;
using System.Globalization;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private static readonly Color CtfBuildScoreTextColor = new(217, 217, 183);

    private void DrawBuildHudTextCentered(string text, Vector2 position, Color color, float scale)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var width = MeasureMenuBitmapFontWidth(text, scale);
        var height = MeasureMenuBitmapFontHeight(scale);
        DrawMenuBitmapFontText(text, new Vector2(position.X - (width / 2f), position.Y - (height / 2f)), color, scale);
    }

    private void DrawScorePanelHud()
    {
        if (_world.MatchRules.Mode == GameModeKind.Arena)
        {
            DrawArenaHud();
            return;
        }
        if (_world.MatchRules.Mode == GameModeKind.ControlPoint)
        {
            DrawControlPointHud();
            return;
        }
        if (_world.MatchRules.Mode is GameModeKind.KingOfTheHill or GameModeKind.DoubleKingOfTheHill)
        {
            DrawKothHud();
            return;
        }
        if (_world.MatchRules.Mode == GameModeKind.Generator)
        {
            DrawGeneratorHud();
            return;
        }
        if (_world.MatchRules.Mode == GameModeKind.TeamDeathmatch)
        {
            DrawTeamDeathmatchHud();
            return;
        }

        var viewportHeight = ViewportHeight;
        var defaultPanelOrigin = new Vector2(ViewportWidth / 2f, viewportHeight);
        var panelOrigin = defaultPanelOrigin;
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

        if (_world.MatchRules.Mode == GameModeKind.CaptureTheFlag)
        {
            DrawBuildHudTextCentered(_world.RedCaps.ToString(CultureInfo.InvariantCulture), PanelPoint(-135f, -30f), CtfBuildScoreTextColor, 2f * panelScale);
            DrawBuildHudTextCentered(_world.BlueCaps.ToString(CultureInfo.InvariantCulture), PanelPoint(130f, -30f), CtfBuildScoreTextColor, 2f * panelScale);
        }
        else
        {
            DrawHudTextCentered(_world.RedCaps.ToString(CultureInfo.InvariantCulture), PanelPoint(-135f, -30f), Color.Black, 2f * panelScale);
            DrawHudTextCentered(_world.BlueCaps.ToString(CultureInfo.InvariantCulture), PanelPoint(130f, -30f), Color.Black, 2f * panelScale);
        }

        DrawScorePanelCapLimit(panelOrigin, panelScale);

        DrawIntelPanelElement(_world.RedIntel, PanelPoint(-65f, -50f), panelScale);
        DrawIntelPanelElement(_world.BlueIntel, PanelPoint(60f, -50f), panelScale);
        DrawMatchTimerHud(ViewportWidth / 2f);
    }
}
