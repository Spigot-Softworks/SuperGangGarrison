#nullable enable

using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.ClientShared;

namespace OpenGarrison.Client;

public partial class Game1
{
    public bool ShouldUseSoftwareMenuCursor()
    {
        return _menuResources.CursorTexture is not null
            || IsScreenFillingDisplayMode(_menuManager.DisplaySettings.DisplayMode);
    }

    public void LoadMenuCursorTexture()
    {
        _menuResources.CursorTexture?.Dispose();
        _menuResources.CursorTexture = null;

        using var resource = typeof(Game1).Assembly.GetManifestResourceStream("OpenGarrison.MenuCursor.png");
        if (resource is null)
        {
            return;
        }

        using var buffer = new MemoryStream();
        resource.CopyTo(buffer);
        _menuResources.CursorTexture = TextureDecodeUtility.LoadSpriteFrame(
            GraphicsDevice,
            buffer.ToArray(),
            applyLegacyChromaKey: false);
    }

    public bool ShouldDrawSoftwareMenuCursor()
    {
        if (!ShouldUseSoftwareMenuCursor())
        {
            return false;
        }

        return _mainMenuOpen
            || _passwordPromptOpen
            || ShouldShowGameplayMouseCursor();
    }

    public void DrawSoftwareMenuCursor(MouseState mouse)
    {
        var cursorSizePercent = ClientSettings.NormalizeCursorSizePercent(_hudManager.RuntimeSettings.CursorSizePercent);
        if (_menuResources.CursorTexture is { } cursorTexture)
        {
            var cursorScale = ClientSettings.GetCursorScale(cursorSizePercent)
                / ClientSettings.GetCursorScale(ClientSettings.DefaultCursorSizePercent);
            _spriteBatch.Draw(
                cursorTexture.Texture,
                new Vector2(mouse.X, mouse.Y),
                cursorTexture.SourceRectangle,
                Color.White,
                0f,
                Vector2.Zero,
                new Vector2(cursorScale, cursorScale),
                SpriteEffects.None,
                0f);
            return;
        }

        var x = mouse.X;
        var y = mouse.Y;
        var fillColor = new Color(92, 213, 255);
        var shadowColor = Color.Black;

        DrawCursorSpan(x, y, 1, 1, 1, 9, shadowColor * 0.85f, cursorSizePercent);
        DrawCursorSpan(x, y, 2, 2, 2, 1, shadowColor * 0.85f, cursorSizePercent);
        DrawCursorSpan(x, y, 2, 3, 3, 1, shadowColor * 0.85f, cursorSizePercent);
        DrawCursorSpan(x, y, 2, 4, 4, 1, shadowColor * 0.85f, cursorSizePercent);
        DrawCursorSpan(x, y, 2, 5, 5, 1, shadowColor * 0.85f, cursorSizePercent);
        DrawCursorSpan(x, y, 2, 6, 6, 1, shadowColor * 0.85f, cursorSizePercent);
        DrawCursorSpan(x, y, 4, 7, 4, 1, shadowColor * 0.85f, cursorSizePercent);
        DrawCursorSpan(x, y, 4, 8, 3, 1, shadowColor * 0.85f, cursorSizePercent);
        DrawCursorSpan(x, y, 4, 9, 2, 1, shadowColor * 0.85f, cursorSizePercent);
        DrawCursorSpan(x, y, 4, 10, 1, 1, shadowColor * 0.85f, cursorSizePercent);

        DrawCursorSpan(x, y, 0, 0, 1, 8, fillColor, cursorSizePercent);
        DrawCursorSpan(x, y, 1, 1, 2, 1, fillColor, cursorSizePercent);
        DrawCursorSpan(x, y, 1, 2, 3, 1, fillColor, cursorSizePercent);
        DrawCursorSpan(x, y, 1, 3, 4, 1, fillColor, cursorSizePercent);
        DrawCursorSpan(x, y, 1, 4, 5, 1, fillColor, cursorSizePercent);
        DrawCursorSpan(x, y, 1, 5, 6, 1, fillColor, cursorSizePercent);
        DrawCursorSpan(x, y, 3, 6, 4, 1, fillColor, cursorSizePercent);
        DrawCursorSpan(x, y, 3, 7, 3, 1, fillColor, cursorSizePercent);
        DrawCursorSpan(x, y, 3, 8, 2, 1, fillColor, cursorSizePercent);
    }

    private void DrawCursorSpan(int originX, int originY, int offsetX, int offsetY, int width, int height, Color color, int sizePercent)
    {
        var scaledOffsetX = ScaleCursorPixels(offsetX, sizePercent);
        var scaledOffsetY = ScaleCursorPixels(offsetY, sizePercent);
        var scaledEndX = ScaleCursorPixels(offsetX + width, sizePercent);
        var scaledEndY = ScaleCursorPixels(offsetY + height, sizePercent);

        _spriteBatch.Draw(
            _pixel,
            new Rectangle(
                originX + scaledOffsetX,
                originY + scaledOffsetY,
                Math.Max(1, scaledEndX - scaledOffsetX),
                Math.Max(1, scaledEndY - scaledOffsetY)),
            color);
    }

    private static int ScaleCursorPixels(int pixels, int sizePercent)
    {
        // 100% is the former 2x cursor, so 50% maps to the original 1x pixel grid.
        return (int)MathF.Round(pixels * ClientSettings.GetCursorScale(sizePercent));
    }
}
