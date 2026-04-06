#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private static readonly JsonSerializerOptions MenuBitmapFontJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private sealed class MenuBitmapFontData
    {
        public int LineHeight { get; set; }

        public int Spacing { get; set; }

        public List<MenuBitmapGlyphData> Glyphs { get; set; } = [];
    }

    private sealed class MenuBitmapGlyphData
    {
        public int Character { get; set; }

        public int X { get; set; }

        public int Y { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }

        public int Advance { get; set; }
    }

    private readonly record struct MenuBitmapGlyph(Rectangle SourceRect, int Advance);

    private readonly record struct MenuPageAction(string Label, Action Activate);

    private readonly record struct MenuPageButton(string Label, Rectangle Bounds, Action Activate, bool IsBottomBarButton = false);

    private readonly record struct PlaqueMenuLayout(
        Rectangle PlaqueBounds,
        Rectangle[] StackedButtonBounds,
        Rectangle SoloButtonBounds,
        Rectangle? BottomBarBounds,
        Rectangle? BottomBarButtonBounds,
        float Scale);

    private void LoadMenuPlaqueTextures()
    {
        _menuPlaqueTexture = LoadMenuTexture("Sprites", "Menu", "Plaques", "MenuPlaque.png");
        _menuPlaqueTallTexture = LoadMenuTexture("Sprites", "Menu", "Plaques", "MenuPlaqueTall.png");
        _menuTextBoxTopTexture = LoadMenuTexture("Sprites", "Menu", "Plaques", "MenuTextBoxTop.png");
        _menuTextBoxMiddleTexture = LoadMenuTexture("Sprites", "Menu", "Plaques", "MenuTextBoxMiddle.png");
        _menuTextBoxBottomTexture = LoadMenuTexture("Sprites", "Menu", "Plaques", "MenuTextBoxBottom.png");
        _menuTextBoxSoloTexture = LoadMenuTexture("Sprites", "Menu", "Plaques", "MenuTextBoxSolo.png");
    }

    private void LoadMenuBitmapFont()
    {
        _menuBitmapFontTexture?.Dispose();
        _menuBitmapFontTexture = null;
        _menuBitmapFontGlyphs.Clear();
        _menuBitmapFontLineHeight = 0;
        _menuBitmapFontSpacing = 1;

        if (!TryLoadMenuBitmapFont("MenuBuildFontAtlas.png", "MenuBuildFontAtlas.json"))
        {
            TryLoadMenuBitmapFont("MenuFontAtlas.png", "MenuFontAtlas.json");
        }
    }

    private bool TryLoadMenuBitmapFont(string textureFileName, string metadataFileName)
    {
        var texturePath = ContentRoot.GetPath("Sprites", "Menu", "Fonts", textureFileName);
        var metadataPath = ContentRoot.GetPath("Sprites", "Menu", "Fonts", metadataFileName);
        if (string.IsNullOrWhiteSpace(texturePath)
            || string.IsNullOrWhiteSpace(metadataPath)
            || !File.Exists(texturePath)
            || !File.Exists(metadataPath))
        {
            return false;
        }

        using (var stream = File.OpenRead(texturePath))
        {
            _menuBitmapFontTexture = Texture2D.FromStream(GraphicsDevice, stream);
        }

        var metadata = JsonSerializer.Deserialize<MenuBitmapFontData>(File.ReadAllText(metadataPath), MenuBitmapFontJsonOptions);
        if (metadata is null)
        {
            return false;
        }

        _menuBitmapFontLineHeight = Math.Max(1, metadata.LineHeight);
        _menuBitmapFontSpacing = Math.Max(0, metadata.Spacing);
        for (var index = 0; index < metadata.Glyphs.Count; index += 1)
        {
            var glyph = metadata.Glyphs[index];
            var character = (char)glyph.Character;
            var sourceRect = new Rectangle(glyph.X, glyph.Y, Math.Max(0, glyph.Width), Math.Max(0, glyph.Height));
            var advance = Math.Max(1, glyph.Advance);
            _menuBitmapFontGlyphs[character] = new MenuBitmapGlyph(sourceRect, advance);
        }

        return _menuBitmapFontTexture is not null && _menuBitmapFontGlyphs.Count > 0;
    }

    private Texture2D? LoadMenuTexture(params string[] pathParts)
    {
        var path = ContentRoot.GetPath(pathParts);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        using var stream = File.OpenRead(path);
        return Texture2D.FromStream(GraphicsDevice, stream);
    }

    private PlaqueMenuLayout GetCenteredPlaqueMenuLayout(bool tall, int stackedButtonCount, bool includeBottomBarButton)
    {
        var backgroundTexture = tall ? _menuPlaqueTallTexture : _menuPlaqueTexture;
        var soloTexture = _menuTextBoxSoloTexture;
        if (backgroundTexture is null || soloTexture is null || stackedButtonCount <= 0)
        {
            return new PlaqueMenuLayout(Rectangle.Empty, [], Rectangle.Empty, null, null, 1f);
        }

        var topOffset = 24f;
        var stackedGap = 14f;
        var soloGap = 55f;
        var sideInset = 10f;
        var availableHeight = ViewportHeight - (includeBottomBarButton ? 110f : 48f);
        var scale = MathF.Min(1f, availableHeight / backgroundTexture.Height) * 0.68f;
        if (scale <= 0f)
        {
            scale = 0.68f;
        }

        var plaqueWidth = MathF.Round(backgroundTexture.Width * scale);
        var plaqueHeight = MathF.Round(backgroundTexture.Height * scale);
        var leftMargin = MathF.Max(20f, ViewportWidth * 0.04f);
        var plaqueX = MathF.Round(leftMargin);
        var plaqueY = MathF.Round(MathF.Max(18f, (ViewportHeight - plaqueHeight - (includeBottomBarButton ? 74f : 0f)) * 0.5f));
        var plaqueBounds = new Rectangle((int)plaqueX, (int)plaqueY, (int)plaqueWidth, (int)plaqueHeight);

        var stackedBounds = new Rectangle[stackedButtonCount];
        var currentY = plaqueBounds.Y + (int)MathF.Round(topOffset * scale);
        for (var index = 0; index < stackedButtonCount; index += 1)
        {
            var texture = GetMenuStackedButtonTexture(index, stackedButtonCount);
            if (texture is null)
            {
                continue;
            }

            var buttonWidth = (int)MathF.Round(texture.Width * scale);
            var buttonHeight = (int)MathF.Round(texture.Height * scale);
            var buttonX = plaqueBounds.X + (int)MathF.Round(sideInset * scale);
            stackedBounds[index] = new Rectangle(buttonX, currentY, buttonWidth, buttonHeight);
            currentY += buttonHeight + (int)MathF.Round(stackedGap * scale);
        }

        var soloWidth = (int)MathF.Round(soloTexture.Width * scale);
        var soloHeight = (int)MathF.Round(soloTexture.Height * scale);
        var soloX = plaqueBounds.X + (int)MathF.Round(sideInset * scale);
        var soloY = currentY + (int)MathF.Round((soloGap - stackedGap) * scale);
        var soloBounds = new Rectangle(soloX, soloY, soloWidth, soloHeight);

        Rectangle? bottomBarBounds = null;
        Rectangle? bottomBarButtonBounds = null;
        if (includeBottomBarButton)
        {
            const int bottomBarHeight = 76;
            var barY = ViewportHeight - bottomBarHeight;
            bottomBarBounds = new Rectangle(0, barY, ViewportWidth, bottomBarHeight);
            var buttonWidth = (int)MathF.Round(soloTexture.Width * scale);
            var buttonHeight = (int)MathF.Round(soloTexture.Height * scale);
            bottomBarButtonBounds = new Rectangle(
                plaqueBounds.X + (int)MathF.Round(sideInset * scale),
                barY + (bottomBarHeight - buttonHeight) / 2,
                buttonWidth,
                buttonHeight);
        }

        return new PlaqueMenuLayout(plaqueBounds, stackedBounds, soloBounds, bottomBarBounds, bottomBarButtonBounds, scale);
    }

    private Texture2D? GetMenuStackedButtonTexture(int index, int count)
    {
        return count switch
        {
            <= 1 => _menuTextBoxTopTexture,
            _ when index == 0 => _menuTextBoxTopTexture,
            _ when index == count - 1 => _menuTextBoxBottomTexture,
            _ => _menuTextBoxMiddleTexture,
        };
    }

    private void DrawPlaqueMenuLayout(
        PlaqueMenuLayout layout,
        IReadOnlyList<MenuPageAction> stackedActions,
        MenuPageAction soloAction,
        bool drawBottomBarButton,
        string bottomBarLabel,
        int hoveredStackedIndex,
        bool soloHovered,
        bool bottomBarHovered,
        float textScaleMultiplier = 1f)
    {
        if (layout.PlaqueBounds == Rectangle.Empty)
        {
            return;
        }

        var backgroundTexture = layout.PlaqueBounds.Height >= (_menuPlaqueTallTexture?.Height ?? int.MaxValue) * layout.Scale - 0.5f
            ? _menuPlaqueTallTexture
            : _menuPlaqueTexture;
        if (backgroundTexture is not null)
        {
            _spriteBatch.Draw(backgroundTexture, layout.PlaqueBounds, Color.White);
        }

        for (var index = 0; index < stackedActions.Count && index < layout.StackedButtonBounds.Length; index += 1)
        {
            var texture = GetMenuStackedButtonTexture(index, stackedActions.Count);
            DrawPlaqueMenuButton(texture, layout.StackedButtonBounds[index], stackedActions[index].Label, hoveredStackedIndex == index, layout.Scale, textScaleMultiplier);
        }

        DrawPlaqueMenuButton(_menuTextBoxSoloTexture, layout.SoloButtonBounds, soloAction.Label, soloHovered, layout.Scale, textScaleMultiplier);

        if (drawBottomBarButton && layout.BottomBarBounds.HasValue && layout.BottomBarButtonBounds.HasValue)
        {
            _spriteBatch.Draw(_pixel, layout.BottomBarBounds.Value, new Color(0x57, 0x4f, 0x47));
            DrawPlaqueMenuButton(_menuTextBoxSoloTexture, layout.BottomBarButtonBounds.Value, bottomBarLabel, bottomBarHovered, layout.Scale, textScaleMultiplier);
        }
    }

    private void DrawPlaqueMenuButton(Texture2D? texture, Rectangle bounds, string label, bool hovered, float plaqueScale, float textScaleMultiplier)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        if (texture is not null)
        {
            _spriteBatch.Draw(texture, bounds, hovered ? new Color(210, 210, 210) : Color.White);
        }
        else
        {
            _spriteBatch.Draw(_pixel, bounds, hovered ? new Color(194, 186, 168) : new Color(224, 216, 194));
        }

        DrawCenteredMenuFontText(label, bounds, new Color(82, 71, 59), plaqueScale, textScaleMultiplier);
    }

    private void DrawCenteredMenuFontText(string text, Rectangle bounds, Color color, float plaqueScale, float textScaleMultiplier)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var textScale = GetMenuFontScaleToFit(text, bounds.Width - MathF.Max(16f, 28f * plaqueScale), bounds.Height - MathF.Max(8f, 14f * plaqueScale)) * textScaleMultiplier;
        var maxTextScale = GetMenuFontScaleToFit(text, bounds.Width - 2f, bounds.Height - 2f);
        textScale = MathF.Min(textScale, maxTextScale);
        var measuredWidth = MeasureMenuBitmapFontWidth(text, textScale);
        var lineHeight = MeasureMenuBitmapFontHeight(textScale);
        var position = new Vector2(
            bounds.X + ((bounds.Width - measuredWidth) * 0.5f),
            bounds.Y + ((bounds.Height - lineHeight) * 0.5f));
        DrawMenuBitmapFontText(text, position, color, textScale);
    }

    private float GetMenuFontScaleToFit(string text, float maxWidth, float maxHeight, float baseScale = 1f)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return baseScale;
        }

        var measuredWidth = MeasureMenuBitmapFontWidth(text, 1f);
        var measuredHeight = MeasureMenuBitmapFontHeight(1f);
        if (measuredWidth <= 0f || measuredHeight <= 0f)
        {
            return baseScale;
        }

        var widthScale = maxWidth / measuredWidth;
        var heightScale = maxHeight / measuredHeight;
        return MathF.Max(0.4f, MathF.Min(baseScale, MathF.Min(widthScale, heightScale)));
    }

    private void DrawMenuBitmapFontText(string text, Vector2 position, Color color, float scale)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        if (_menuBitmapFontTexture is null || _menuBitmapFontGlyphs.Count == 0)
        {
            _spriteBatch.DrawString(_menuFont, text, position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            return;
        }

        var cursor = new Vector2(MathF.Round(position.X), MathF.Round(position.Y));
        for (var index = 0; index < text.Length; index += 1)
        {
            var character = text[index];
            if (!_menuBitmapFontGlyphs.TryGetValue(character, out var glyph))
            {
                if (_menuBitmapFontGlyphs.TryGetValue(' ', out var spaceGlyph))
                {
                    cursor.X += (spaceGlyph.Advance + _menuBitmapFontSpacing) * scale;
                }

                continue;
            }

            if (glyph.SourceRect.Width > 0 && glyph.SourceRect.Height > 0)
            {
                _spriteBatch.Draw(
                    _menuBitmapFontTexture,
                    new Rectangle(
                        (int)MathF.Round(cursor.X),
                        (int)MathF.Round(cursor.Y),
                        Math.Max(1, (int)MathF.Round(glyph.SourceRect.Width * scale)),
                        Math.Max(1, (int)MathF.Round(glyph.SourceRect.Height * scale))),
                    glyph.SourceRect,
                    color);
            }
            cursor.X += (glyph.Advance + _menuBitmapFontSpacing) * scale;
        }
    }

    private float MeasureMenuBitmapFontWidth(string text, float scale)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0f;
        }

        if (_menuBitmapFontGlyphs.Count == 0)
        {
            return _menuFont.MeasureString(text).X * scale;
        }

        var width = 0f;
        for (var index = 0; index < text.Length; index += 1)
        {
            if (!_menuBitmapFontGlyphs.TryGetValue(text[index], out var glyph))
            {
                if (_menuBitmapFontGlyphs.TryGetValue(' ', out var spaceGlyph))
                {
                    width += (spaceGlyph.Advance + _menuBitmapFontSpacing) * scale;
                }

                continue;
            }

            width += (glyph.Advance + _menuBitmapFontSpacing) * scale;
        }

        return Math.Max(0f, width - (_menuBitmapFontSpacing * scale));
    }

    private float MeasureMenuBitmapFontHeight(float scale)
    {
        if (_menuBitmapFontLineHeight <= 0)
        {
            return _menuFont.LineSpacing * scale;
        }

        return _menuBitmapFontLineHeight * scale;
    }
}
