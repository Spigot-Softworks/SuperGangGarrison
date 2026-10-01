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
    public static readonly JsonSerializerOptions MenuBitmapFontJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public sealed class MenuBitmapFontData
    {
        public int LineHeight { get; set; }

        public int Spacing { get; set; }

        public List<MenuBitmapGlyphData> Glyphs { get; set; } = [];
    }

    public sealed class MenuBitmapGlyphData
    {
        public int Character { get; set; }

        public int X { get; set; }

        public int Y { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }

        public int Advance { get; set; }
    }

    public readonly record struct MenuBitmapGlyph(Rectangle SourceRect, int Advance);

    public readonly record struct MenuPageAction(string Label, Action Activate);

    public readonly record struct MenuPageButton(
        string Label,
        Rectangle Bounds,
        Action Activate,
        bool IsBottomBarButton = false,
        bool IsBottomBarCenterButton = false,
        bool IsBottomBarRightButton = false);

    public readonly record struct PlaqueMenuLayout(
        Rectangle PlaqueBounds,
        Rectangle[] StackedButtonBounds,
        Rectangle SoloButtonBounds,
        Rectangle? BottomBarBounds,
        Rectangle? BottomBarButtonBounds,
        float Scale);

    public void LoadMenuPlaqueTextures()
    {
        _menuResources.PlaqueTexture = LoadMenuTexture("Sprites", "Menu", "Plaques", "MenuPlaque.png");
        _menuResources.PlaqueTallTexture = LoadMenuTexture("Sprites", "Menu", "Plaques", "MenuPlaqueTall.png");
        _menuResources.TextBoxTopTexture = LoadMenuTexture("Sprites", "Menu", "Plaques", "MenuTextBoxTop.png");
        _menuResources.TextBoxMiddleTexture = LoadMenuTexture("Sprites", "Menu", "Plaques", "MenuTextBoxMiddle.png");
        _menuResources.TextBoxBottomTexture = LoadMenuTexture("Sprites", "Menu", "Plaques", "MenuTextBoxBottom.png");
        _menuResources.TextBoxSoloTexture = LoadMenuTexture("Sprites", "Menu", "Plaques", "MenuTextBoxSolo.png");
        _menuResources.LastToDieMenuPlaqueTexture = LoadMenuTexture("Sprites", "Menu", "Plaques", "LTD_MenuPlaque.png");
        _menuResources.LastToDieMenuTextBoxSoloTexture = LoadMenuTexture("Sprites", "Menu", "Plaques", "LTD_MenuTextBoxSolo.png");
    }

    public void LoadMenuBitmapFont()
    {
        _menuResources.BitmapFontTexture?.Dispose();
        _menuResources.BitmapFontTexture = null;
        _menuResources.BitmapFontGlyphs.Clear();
        _menuResources.BitmapFontLineHeight = 0;
        _menuResources.BitmapFontSpacing = 1;

        if (!TryLoadMenuBitmapFont("MenuBuildFontAtlas.png", "MenuBuildFontAtlas.json"))
        {
            TryLoadMenuBitmapFont("MenuFontAtlas.png", "MenuFontAtlas.json");
        }
    }

    public bool TryLoadMenuBitmapFont(string textureFileName, string metadataFileName)
    {
        var texturePath = ContentRoot.GetPath("Sprites", "Menu", "Fonts", textureFileName);
        var metadataPath = ContentRoot.GetPath("Sprites", "Menu", "Fonts", metadataFileName);
        if (string.IsNullOrWhiteSpace(texturePath)
            || string.IsNullOrWhiteSpace(metadataPath))
        {
            return false;
        }

        _menuResources.BitmapFontTexture = LoadSpriteFrameFromPath(texturePath);
        if (_menuResources.BitmapFontTexture is null)
        {
            return false;
        }

        string? metadataJson = TryGetBrowserContentText(metadataPath, out var browserMetadata)
            ? browserMetadata
            : System.IO.File.Exists(metadataPath)
                ? System.IO.File.ReadAllText(metadataPath)
                : null;
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            _menuResources.BitmapFontTexture.Dispose();
            _menuResources.BitmapFontTexture = null;
            return false;
        }

        metadataJson = NormalizeMenuBitmapFontMetadataJson(metadataJson);
        var metadata = JsonSerializer.Deserialize<MenuBitmapFontData>(metadataJson, MenuBitmapFontJsonOptions);
        if (metadata is null)
        {
            _menuResources.BitmapFontTexture.Dispose();
            _menuResources.BitmapFontTexture = null;
            return false;
        }

        _menuResources.BitmapFontLineHeight = Math.Max(1, metadata.LineHeight);
        _menuResources.BitmapFontSpacing = Math.Max(0, metadata.Spacing);
        for (var index = 0; index < metadata.Glyphs.Count; index += 1)
        {
            var glyph = metadata.Glyphs[index];
            var character = (char)glyph.Character;
            var sourceRect = new Rectangle(glyph.X, glyph.Y, Math.Max(0, glyph.Width), Math.Max(0, glyph.Height));
            var advance = Math.Max(1, glyph.Advance);
            _menuResources.BitmapFontGlyphs[character] = new MenuBitmapGlyph(sourceRect, advance);
        }

        return _menuResources.BitmapFontTexture is not null && _menuResources.BitmapFontGlyphs.Count > 0;
    }

    public static string NormalizeMenuBitmapFontMetadataJson(string metadataJson)
    {
        if (string.IsNullOrEmpty(metadataJson))
        {
            return string.Empty;
        }

        return metadataJson.Length > 0 && metadataJson[0] == '\uFEFF'
            ? metadataJson[1..]
            : metadataJson;
    }

    public LoadedSpriteFrame? LoadMenuTexture(params string[] pathParts)
    {
        var path = ContentRoot.GetPath(pathParts);
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (!OperatingSystem.IsBrowser() && File.Exists(path))
        {
            return new LoadedSpriteFrame(TextureDecodeUtility.LoadTexture(GraphicsDevice, File.ReadAllBytes(path), applyLegacyChromaKey: false));
        }

        return LoadSpriteFrameFromPath(path);
    }

    public PlaqueMenuLayout GetCenteredPlaqueMenuLayout(bool tall, int stackedButtonCount, bool includeSoloButton, bool includeBottomBarButton)
    {
        var backgroundTexture = tall ? _menuResources.PlaqueTallTexture : _menuResources.PlaqueTexture;
        var soloTexture = _menuResources.TextBoxSoloTexture;
        if (backgroundTexture is null || soloTexture is null || stackedButtonCount <= 0)
        {
            return new PlaqueMenuLayout(Rectangle.Empty, [], Rectangle.Empty, null, null, 1f);
        }

        var topOffset = 24f;
        var stackedGap = 14f;
        var soloGap = includeSoloButton ? 55f : 22f;
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
        var estimatedStackHeight = (topOffset * scale)
            + (stackedButtonCount * (soloTexture.Height * scale))
            + Math.Max(0, stackedButtonCount - 1) * (stackedGap * scale)
            + ((includeSoloButton ? soloGap + soloTexture.Height : soloGap) * scale);
        if (!includeSoloButton)
        {
            plaqueHeight = MathF.Min(plaqueHeight, MathF.Round(estimatedStackHeight));
        }

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

        var soloBounds = Rectangle.Empty;
        if (includeSoloButton)
        {
            var soloWidth = (int)MathF.Round(soloTexture.Width * scale);
            var soloHeight = (int)MathF.Round(soloTexture.Height * scale);
            var soloX = plaqueBounds.X + (int)MathF.Round(sideInset * scale);
            var soloY = currentY + (int)MathF.Round((soloGap - stackedGap) * scale);
            soloBounds = new Rectangle(soloX, soloY, soloWidth, soloHeight);
        }

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

    public Rectangle GetBottomRightPlaqueButtonBounds(PlaqueMenuLayout layout)
    {
        if (!layout.BottomBarBounds.HasValue || _menuResources.TextBoxSoloTexture is null)
        {
            return Rectangle.Empty;
        }

        var buttonWidth = (int)MathF.Round(_menuResources.TextBoxSoloTexture.Width * layout.Scale);
        var buttonHeight = (int)MathF.Round(_menuResources.TextBoxSoloTexture.Height * layout.Scale);
        var rightMargin = MathF.Max(20f, ViewportWidth * 0.04f);
        return new Rectangle(
            ViewportWidth - (int)MathF.Round(rightMargin) - buttonWidth,
            layout.BottomBarBounds.Value.Y + (layout.BottomBarBounds.Value.Height - buttonHeight) / 2,
            buttonWidth,
            buttonHeight);
    }

    public Rectangle GetBottomCenterPlaqueButtonBounds(PlaqueMenuLayout layout)
    {
        if (!layout.BottomBarBounds.HasValue || _menuResources.TextBoxSoloTexture is null)
        {
            return Rectangle.Empty;
        }

        var buttonWidth = (int)MathF.Round(_menuResources.TextBoxSoloTexture.Width * layout.Scale);
        var buttonHeight = (int)MathF.Round(_menuResources.TextBoxSoloTexture.Height * layout.Scale);
        var x = (ViewportWidth - buttonWidth) / 2;
        return new Rectangle(
            x,
            layout.BottomBarBounds.Value.Y + (layout.BottomBarBounds.Value.Height - buttonHeight) / 2,
            buttonWidth,
            buttonHeight);
    }

    public void DrawBottomCenterPlaqueButton(PlaqueMenuLayout layout, string label, bool hovered, float textScaleMultiplier)
    {
        var bounds = GetBottomCenterPlaqueButtonBounds(layout);
        if (bounds == Rectangle.Empty)
        {
            return;
        }

        DrawPlaqueMenuButton(_menuResources.TextBoxSoloTexture, bounds, label, hovered, layout.Scale, textScaleMultiplier);
    }

    public void DrawBottomRightPlaqueButton(PlaqueMenuLayout layout, string label, bool hovered, float textScaleMultiplier)
    {
        var bounds = GetBottomRightPlaqueButtonBounds(layout);
        if (bounds == Rectangle.Empty)
        {
            return;
        }

        DrawPlaqueMenuButton(_menuResources.TextBoxSoloTexture, bounds, label, hovered, layout.Scale, textScaleMultiplier);
    }

    public LoadedSpriteFrame? GetMenuStackedButtonTexture(int index, int count)
    {
        return count switch
        {
            <= 1 => _menuResources.TextBoxTopTexture,
            _ when index == 0 => _menuResources.TextBoxTopTexture,
            _ when index == count - 1 => _menuResources.TextBoxBottomTexture,
            _ => _menuResources.TextBoxMiddleTexture,
        };
    }

    public void DrawPlaqueMenuLayout(
        PlaqueMenuLayout layout,
        IReadOnlyList<MenuPageAction> stackedActions,
        MenuPageAction? soloAction,
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

        var backgroundTexture = layout.PlaqueBounds.Height >= (_menuResources.PlaqueTallTexture?.Height ?? int.MaxValue) * layout.Scale - 0.5f
            ? _menuResources.PlaqueTallTexture
            : _menuResources.PlaqueTexture;
        if (backgroundTexture is not null)
        {
            DrawLoadedSpriteFrame(backgroundTexture, layout.PlaqueBounds, Color.White);
        }

        for (var index = 0; index < stackedActions.Count && index < layout.StackedButtonBounds.Length; index += 1)
        {
            var texture = GetMenuStackedButtonTexture(index, stackedActions.Count);
            DrawPlaqueMenuButton(texture, layout.StackedButtonBounds[index], stackedActions[index].Label, hoveredStackedIndex == index, layout.Scale, textScaleMultiplier);
        }

        if (soloAction.HasValue)
        {
            DrawPlaqueMenuButton(_menuResources.TextBoxSoloTexture, layout.SoloButtonBounds, soloAction.Value.Label, soloHovered, layout.Scale, textScaleMultiplier);
        }

        // Always draw bottom bar and runners in animated mode (outside gameplay)
        if (layout.BottomBarBounds.HasValue && _gameplayManager.RuntimeSettings.MenuBackgroundMode != MenuBackgroundMode.Static)
        {
            _spriteBatch.Draw(_pixel, layout.BottomBarBounds.Value, new Color(0x57, 0x4f, 0x47));
            
            // Draw running character silhouettes on the bottom bar
            _menuManager.MenuBottomBarRunners.Draw(layout.BottomBarBounds.Value);
        }

        // Draw bottom bar button only if explicitly requested
        if (drawBottomBarButton && layout.BottomBarButtonBounds.HasValue)
        {
            DrawPlaqueMenuButton(_menuResources.TextBoxSoloTexture, layout.BottomBarButtonBounds.Value, bottomBarLabel, bottomBarHovered, layout.Scale, textScaleMultiplier);
        }
    }

    public void DrawPlaqueMenuButton(LoadedSpriteFrame? texture, Rectangle bounds, string label, bool hovered, float plaqueScale, float textScaleMultiplier)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        if (texture is not null)
        {
            DrawLoadedSpriteFrame(texture, bounds, hovered ? new Color(172, 166, 154) : Color.White);
        }
        else
        {
            _spriteBatch.Draw(_pixel, bounds, hovered ? new Color(170, 162, 146) : new Color(224, 216, 194));
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
        return NormalizeUiTextScale(MathF.Max(0.4f, MathF.Min(baseScale, MathF.Min(widthScale, heightScale))));
    }

    public void DrawMenuBitmapFontText(string text, Vector2 position, Color color, float scale)
    {
        scale = NormalizeUiTextScale(scale);
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var drawColor = ApplyCurrentHudElementOpacity(color);
        if (_menuResources.BitmapFontTexture is null || _menuResources.BitmapFontGlyphs.Count == 0)
        {
            _spriteBatch.DrawString(_menuFont, text, position, drawColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            return;
        }

        var cursor = new Vector2(MathF.Round(position.X), MathF.Round(position.Y));
        for (var index = 0; index < text.Length; index += 1)
        {
            var character = text[index];
            if (!_menuResources.BitmapFontGlyphs.TryGetValue(character, out var glyph))
            {
                if (_menuResources.BitmapFontGlyphs.TryGetValue(' ', out var spaceGlyph))
                {
                    cursor.X += (spaceGlyph.Advance + _menuResources.BitmapFontSpacing) * scale;
                }

                continue;
            }

            if (glyph.SourceRect.Width > 0 && glyph.SourceRect.Height > 0)
            {
                _spriteBatch.Draw(
                    _menuResources.BitmapFontTexture.Texture,
                    new Rectangle(
                        (int)MathF.Round(cursor.X),
                        (int)MathF.Round(cursor.Y),
                        Math.Max(1, (int)MathF.Round(glyph.SourceRect.Width * scale)),
                        Math.Max(1, (int)MathF.Round(glyph.SourceRect.Height * scale))),
                    CombineSourceRectangles(_menuResources.BitmapFontTexture.SourceRectangle, glyph.SourceRect),
                    drawColor);
            }
            cursor.X += (glyph.Advance + _menuResources.BitmapFontSpacing) * scale;
        }
    }

    public float MeasureMenuBitmapFontWidth(string text, float scale)
    {
        scale = NormalizeUiTextScale(scale);
        if (string.IsNullOrEmpty(text))
        {
            return 0f;
        }

        if (_menuResources.BitmapFontGlyphs.Count == 0)
        {
            return _menuFont.MeasureString(text).X * scale;
        }

        var width = 0f;
        for (var index = 0; index < text.Length; index += 1)
        {
            if (!_menuResources.BitmapFontGlyphs.TryGetValue(text[index], out var glyph))
            {
                if (_menuResources.BitmapFontGlyphs.TryGetValue(' ', out var spaceGlyph))
                {
                    width += (spaceGlyph.Advance + _menuResources.BitmapFontSpacing) * scale;
                }

                continue;
            }

            width += (glyph.Advance + _menuResources.BitmapFontSpacing) * scale;
        }

        return Math.Max(0f, width - (_menuResources.BitmapFontSpacing * scale));
    }

    public float MeasureMenuBitmapFontHeight(float scale)
    {
        scale = NormalizeUiTextScale(scale);
        if (_menuResources.BitmapFontLineHeight <= 0)
        {
            return _menuFont.LineSpacing * scale;
        }

        return _menuResources.BitmapFontLineHeight * scale;
    }
}
