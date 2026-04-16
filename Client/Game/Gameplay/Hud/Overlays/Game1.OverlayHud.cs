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

        var viewportWidth = ViewportWidth;
        var viewportHeight = ViewportHeight;
        var centerX = viewportWidth / 2f;
        if (!TryDrawScreenSprite("CTFHUDS", 0, new Vector2(centerX + 1f, viewportHeight + 100f), Color.White, new Vector2(3f, 3f)))
        {
            DrawFallbackScorePanelHud(centerX, viewportHeight);
        }

        if (_world.MatchRules.Mode == GameModeKind.CaptureTheFlag)
        {
            DrawBuildHudTextCentered(_world.RedCaps.ToString(CultureInfo.InvariantCulture), new Vector2(centerX - 135f, viewportHeight - 30f), CtfBuildScoreTextColor, 2f);
            DrawBuildHudTextCentered(_world.BlueCaps.ToString(CultureInfo.InvariantCulture), new Vector2(centerX + 130f, viewportHeight - 30f), CtfBuildScoreTextColor, 2f);
        }
        else
        {
            DrawHudTextCentered(_world.RedCaps.ToString(CultureInfo.InvariantCulture), new Vector2(centerX - 135f, viewportHeight - 30f), Color.Black, 2f);
            DrawHudTextCentered(_world.BlueCaps.ToString(CultureInfo.InvariantCulture), new Vector2(centerX + 130f, viewportHeight - 30f), Color.Black, 2f);
        }

        DrawScorePanelCapLimit(centerX, viewportHeight);

        DrawIntelPanelElement(_world.RedIntel, new Vector2(centerX - 65f, viewportHeight - 50f));
        DrawIntelPanelElement(_world.BlueIntel, new Vector2(centerX + 60f, viewportHeight - 50f));
        DrawMatchTimerHud(centerX);
    }
}
