using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace OpenGarrison.Client.Plugins;

/// <summary>
/// Drawing surface plugins use to render HUD content.
/// </summary>
public interface IOpenGarrisonClientHudCanvas
{
    /// <summary>
    /// Gets the viewport width in pixels.
    /// </summary>
    int ViewportWidth { get; }

    /// <summary>
    /// Gets the viewport height in pixels.
    /// </summary>
    int ViewportHeight { get; }

    /// <summary>
    /// Gets the camera's top-left position in world coordinates.
    /// </summary>
    Vector2 CameraTopLeft { get; }

    /// <summary>
    /// Converts a world position to screen coordinates.
    /// </summary>
    /// <param name="worldPosition">The world position.</param>
    /// <returns>The screen position.</returns>
    Vector2 WorldToScreen(Vector2 worldPosition);

    /// <summary>
    /// Measures the width of bitmap text at the given scale.
    /// </summary>
    /// <param name="text">The text to measure.</param>
    /// <param name="scale">The text scale.</param>
    /// <returns>The measured width in pixels.</returns>
    float MeasureBitmapTextWidth(string text, float scale);

    /// <summary>
    /// Measures the height of bitmap text at the given scale.
    /// </summary>
    /// <param name="scale">The text scale.</param>
    /// <returns>The measured height in pixels.</returns>
    float MeasureBitmapTextHeight(float scale);

    /// <summary>
    /// Draws bitmap text at a screen position.
    /// </summary>
    /// <param name="text">The text to draw.</param>
    /// <param name="position">The screen position.</param>
    /// <param name="color">The text color.</param>
    /// <param name="scale">The text scale.</param>
    void DrawBitmapText(string text, Vector2 position, Color color, float scale = 1f);

    /// <summary>
    /// Draws bitmap text centered on a screen position.
    /// </summary>
    /// <param name="text">The text to draw.</param>
    /// <param name="position">The screen position to center on.</param>
    /// <param name="color">The text color.</param>
    /// <param name="scale">The text scale.</param>
    void DrawBitmapTextCentered(string text, Vector2 position, Color color, float scale = 1f);

    /// <summary>
    /// Fills a screen rectangle with a solid color.
    /// </summary>
    /// <param name="rectangle">The rectangle in screen coordinates.</param>
    /// <param name="color">The fill color.</param>
    void FillScreenRectangle(Rectangle rectangle, Color color);

    /// <summary>
    /// Draws the outline of a screen rectangle.
    /// </summary>
    /// <param name="rectangle">The rectangle in screen coordinates.</param>
    /// <param name="color">The outline color.</param>
    /// <param name="thickness">The outline thickness in pixels.</param>
    void DrawScreenRectangleOutline(Rectangle rectangle, Color color, int thickness = 1);

    /// <summary>
    /// Draws a line between two screen positions.
    /// </summary>
    /// <param name="start">The line start in screen coordinates.</param>
    /// <param name="endPoint">The line end in screen coordinates.</param>
    /// <param name="color">The line color.</param>
    /// <param name="thickness">The line thickness in pixels.</param>
    void DrawScreenLine(Vector2 start, Vector2 endPoint, Color color, float thickness = 1f);

    /// <summary>
    /// Tries to draw a named sprite frame at a screen position.
    /// </summary>
    /// <param name="spriteName">The sprite name.</param>
    /// <param name="frameIndex">The frame index.</param>
    /// <param name="position">The screen position.</param>
    /// <param name="tint">The tint color.</param>
    /// <param name="scale">The scale.</param>
    /// <returns>True when the sprite was found and drawn.</returns>
    bool TryDrawScreenSprite(string spriteName, int frameIndex, Vector2 position, Color tint, Vector2 scale);

    /// <summary>
    /// Tries to draw a named sprite frame at a world position.
    /// </summary>
    /// <param name="spriteName">The sprite name.</param>
    /// <param name="frameIndex">The frame index.</param>
    /// <param name="worldPosition">The world position.</param>
    /// <param name="tint">The tint color.</param>
    /// <param name="rotation">The rotation in radians.</param>
    /// <returns>True when the sprite was found and drawn.</returns>
    bool TryDrawWorldSprite(string spriteName, int frameIndex, Vector2 worldPosition, Color tint, float rotation = 0f);

    /// <summary>
    /// Tries to get the level background texture.
    /// </summary>
    /// <param name="texture">The background texture.</param>
    /// <returns>True when a background texture is available.</returns>
    bool TryGetLevelBackgroundTexture(out Texture2D texture);

    /// <summary>
    /// Draws a texture at a screen position.
    /// </summary>
    /// <param name="texture">The texture to draw.</param>
    /// <param name="position">The screen position.</param>
    /// <param name="tint">The tint color.</param>
    /// <param name="scale">The scale.</param>
    /// <param name="sourceRectangle">The optional source rectangle.</param>
    /// <param name="rotation">The rotation in radians.</param>
    /// <param name="origin">The optional rotation origin.</param>
    void DrawScreenTexture(
        Texture2D texture,
        Vector2 position,
        Color tint,
        Vector2 scale,
        Rectangle? sourceRectangle = null,
        float rotation = 0f,
        Vector2? origin = null);

    /// <summary>
    /// Draws a texture at a world position.
    /// </summary>
    /// <param name="texture">The texture to draw.</param>
    /// <param name="worldPosition">The world position.</param>
    /// <param name="tint">The tint color.</param>
    /// <param name="scale">The scale.</param>
    /// <param name="sourceRectangle">The optional source rectangle.</param>
    /// <param name="rotation">The rotation in radians.</param>
    /// <param name="origin">The optional rotation origin.</param>
    void DrawWorldTexture(
        Texture2D texture,
        Vector2 worldPosition,
        Color tint,
        Vector2 scale,
        Rectangle? sourceRectangle = null,
        float rotation = 0f,
        Vector2? origin = null);
}
