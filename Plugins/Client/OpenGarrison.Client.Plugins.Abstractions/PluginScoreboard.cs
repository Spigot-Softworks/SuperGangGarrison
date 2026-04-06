using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace OpenGarrison.Client.Plugins;

public enum ClientScoreboardPanelLocation
{
    HeaderLeft = 0,
    HeaderRight = 1,
    Footer = 2,
}

public sealed record ClientScoreboardRenderState(
    Rectangle ScoreboardBounds,
    float Alpha,
    string ServerLabel,
    string MapLabel,
    int RedPlayerCount,
    int BluePlayerCount,
    string RedCenterText,
    string BlueCenterText);

public interface IOpenGarrisonClientScoreboardCanvas : IOpenGarrisonClientHudCanvas
{
    void DrawBitmapTextRightAligned(string text, Vector2 position, Color color, float scale = 1f);
}

public interface IOpenGarrisonClientScoreboardHooks
{
    ClientScoreboardPanelLocation ScoreboardPanelLocation => ClientScoreboardPanelLocation.Footer;

    int ScoreboardPanelOrder => 0;

    void OnScoreboardDraw(IOpenGarrisonClientScoreboardCanvas canvas, ClientScoreboardRenderState state);
}
