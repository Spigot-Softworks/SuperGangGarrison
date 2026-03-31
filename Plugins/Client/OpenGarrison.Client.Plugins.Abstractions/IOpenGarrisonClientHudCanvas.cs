using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace OpenGarrison.Client.Plugins;

public interface IOpenGarrisonClientHudCanvas
{
    int ViewportWidth { get; }

    int ViewportHeight { get; }

    Vector2 CameraTopLeft { get; }

    Vector2 WorldToScreen(Vector2 worldPosition);

    float MeasureBitmapTextWidth(string text, float scale);

    float MeasureBitmapTextHeight(float scale);

    void DrawBitmapText(string text, Vector2 position, Color color, float scale = 1f);

    void DrawBitmapTextCentered(string text, Vector2 position, Color color, float scale = 1f);

    void FillScreenRectangle(Rectangle rectangle, Color color);

    void DrawScreenRectangleOutline(Rectangle rectangle, Color color, int thickness = 1);

    void DrawScreenLine(Vector2 start, Vector2 endPoint, Color color, float thickness = 1f);

    bool TryDrawScreenSprite(string spriteName, int frameIndex, Vector2 position, Color tint, Vector2 scale);

    bool TryDrawWorldSprite(string spriteName, int frameIndex, Vector2 worldPosition, Color tint, float rotation = 0f);

    bool TryGetLevelBackgroundTexture(out Texture2D texture);

    void DrawScreenTexture(
        Texture2D texture,
        Vector2 position,
        Color tint,
        Vector2 scale,
        Rectangle? sourceRectangle = null,
        float rotation = 0f,
        Vector2? origin = null);

    void DrawWorldTexture(
        Texture2D texture,
        Vector2 worldPosition,
        Color tint,
        Vector2 scale,
        Rectangle? sourceRectangle = null,
        float rotation = 0f,
        Vector2? origin = null);
}
