using Microsoft.Xna.Framework;

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

    bool TryDrawScreenSprite(string spriteName, int frameIndex, Vector2 position, Color tint, Vector2 scale);

    bool TryDrawWorldSprite(string spriteName, int frameIndex, Vector2 worldPosition, Color tint, float rotation = 0f);
}
