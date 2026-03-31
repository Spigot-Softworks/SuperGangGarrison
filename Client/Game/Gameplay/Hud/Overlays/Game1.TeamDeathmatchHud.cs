#nullable enable

using Microsoft.Xna.Framework;
using System.Globalization;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void DrawTeamDeathmatchHud()
    {
        var viewportWidth = ViewportWidth;
        var viewportHeight = ViewportHeight;
        var centerX = viewportWidth / 2f;
        if (!TryDrawScreenSprite("CTFHUDS", 0, new Vector2(centerX + 1f, viewportHeight + 100f), Color.White, new Vector2(3f, 3f)))
        {
            DrawCenteredHudSprite("ScorePanelS", 0, new Vector2(centerX, viewportHeight - 57.5f), Color.White, new Vector2(3f, 3f));
        }

        DrawHudTextCentered(_world.RedCaps.ToString(CultureInfo.InvariantCulture), new Vector2(centerX - 135f, viewportHeight - 30f), Color.Black, 2f);
        DrawHudTextCentered(_world.BlueCaps.ToString(CultureInfo.InvariantCulture), new Vector2(centerX + 130f, viewportHeight - 30f), Color.Black, 2f);
        DrawScorePanelCapLimit(centerX, viewportHeight);
        DrawMatchTimerHud(centerX);
    }
}
