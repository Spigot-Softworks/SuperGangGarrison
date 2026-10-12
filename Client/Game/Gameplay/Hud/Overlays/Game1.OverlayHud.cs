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

    private void DrawBuildHudTextCenteredWithShadow(string text, Vector2 position, Color color, float scale)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var shadowOffset = Math.Max(1f, scale * 0.5f);
        DrawBuildHudTextCentered(text, position + new Vector2(shadowOffset, shadowOffset), Color.Black * 0.55f, scale);
        DrawBuildHudTextCentered(text, position, color, scale);
    }

    private void DrawScorePanelHud()
    {
        if (UsesGg2Presentation && TryDrawGg2ScorePanelHud())
        {
            return;
        }

        if (_world.MatchRules.Mode == GameModeKind.Arena)
        {
            DrawArenaHud();
            return;
        }
        if (_world.MatchRules.Mode is GameModeKind.ControlPoint or GameModeKind.Vip)
        {
            DrawControlPointHud();
            return;
        }

        if (_world.MatchRules.Mode is GameModeKind.KingOfTheHill or GameModeKind.DoubleKingOfTheHill or GameModeKind.Conquest)
        {
            DrawKothHud();
            return;
        }
        if (_world.MatchRules.Mode == GameModeKind.Generator)
        {
            DrawGeneratorHud();
            return;
        }
        if (_world.MatchRules.Mode is GameModeKind.TeamDeathmatch or GameModeKind.Scr)
        {
            DrawTeamDeathmatchHud();
            if (_world.Level.ShowControlPoints)
            {
                DrawAuxiliaryControlPointHudIfNeeded();
            }

            return;
        }

        if (_world.Level.ShowControlPoints)
        {
            DrawAuxiliaryControlPointHudIfNeeded();
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

        if (UsesGg2Presentation)
        {
            // CTFHUD: caps in black at 2x.
            DrawHudTextCentered(_world.RedCaps.ToString(CultureInfo.InvariantCulture), PanelPoint(-135f, -30f), Color.Black, 2f * panelScale);
            DrawHudTextCentered(_world.BlueCaps.ToString(CultureInfo.InvariantCulture), PanelPoint(130f, -30f), Color.Black, 2f * panelScale);
        }
        else if (_world.MatchRules.Mode == GameModeKind.CaptureTheFlag)
        {
            DrawBuildHudTextCenteredWithShadow(_world.RedCaps.ToString(CultureInfo.InvariantCulture), PanelPoint(-135f, -30f), CtfBuildScoreTextColor, 2f * panelScale);
            DrawBuildHudTextCenteredWithShadow(_world.BlueCaps.ToString(CultureInfo.InvariantCulture), PanelPoint(130f, -30f), CtfBuildScoreTextColor, 2f * panelScale);
        }
        else
        {
            DrawHudTextCentered(_world.RedCaps.ToString(CultureInfo.InvariantCulture), PanelPoint(-135f, -30f), Color.Black, 2f * panelScale);
            DrawHudTextCentered(_world.BlueCaps.ToString(CultureInfo.InvariantCulture), PanelPoint(130f, -30f), Color.Black, 2f * panelScale);
        }

        DrawScorePanelCapLimit(panelOrigin, panelScale);

        DrawIntelPanelElement(_world.ObjectiveRules.RedIntel, PanelPoint(-65f, -50f), panelScale);
        DrawIntelPanelElement(_world.ObjectiveRules.BlueIntel, PanelPoint(60f, -50f), panelScale);
        DrawMatchTimerHud(ViewportWidth / 2f);
    }

    /// <summary>GG2 mode: the objective HUDs that differ from ours; CTF falls through.</summary>
    private bool TryDrawGg2ScorePanelHud()
    {
        switch (_world.MatchRules.Mode)
        {
            case GameModeKind.Arena:
                DrawGg2ArenaHud();
                return true;
            case GameModeKind.ControlPoint or GameModeKind.Vip:
                DrawGg2ControlPointHud();
                return true;
            case GameModeKind.KingOfTheHill or GameModeKind.DoubleKingOfTheHill or GameModeKind.Conquest:
                DrawGg2KothHud();
                return true;
            case GameModeKind.Generator:
                DrawGg2GeneratorHud();
                return true;
            case GameModeKind.TeamDeathmatch or GameModeKind.Scr:
                DrawGg2TeamDeathmatchHud();
                return true;
            default:
                return false;
        }
    }
}
