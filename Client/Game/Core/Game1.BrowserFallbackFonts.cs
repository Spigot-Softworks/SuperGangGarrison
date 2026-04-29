#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private SpriteFont LoadInitialSpriteFont(string assetName)
    {
        if (OperatingSystem.IsBrowser())
        {
            if (TryCreateBrowserSpriteFontFromAtlas(assetName, out var browserAtlasFont))
            {
                AddConsoleLine($"browser font atlas fallback for {assetName}");
                return browserAtlasFont;
            }

            AddConsoleLine($"browser font fallback for {assetName}: atlas unavailable");
            return CreateBrowserFallbackSpriteFont();
        }

        return Content.Load<SpriteFont>(assetName);
    }

    private void RefreshBrowserSpriteFontsIfPossible()
    {
        if (!OperatingSystem.IsBrowser())
        {
            return;
        }

        if (TryCreateBrowserSpriteFontFromAtlas("ConsoleFont", out var consoleFont))
        {
            _consoleFont = consoleFont;
        }

        if (TryCreateBrowserSpriteFontFromAtlas("MenuFont", out var menuFont))
        {
            _menuFont = menuFont;
        }
    }

    private bool TryCreateBrowserSpriteFontFromAtlas(string assetName, out SpriteFont spriteFont)
    {
        spriteFont = null!;
        var candidates = GetBrowserSpriteFontAtlasCandidates(assetName);
        for (var index = 0; index < candidates.Length; index += 1)
        {
            if (TryCreateSpriteFontFromAtlas(candidates[index].TexturePath, candidates[index].MetadataPath, out spriteFont))
            {
                return true;
            }
        }

        return false;
    }

    private static (string TexturePath, string MetadataPath)[] GetBrowserSpriteFontAtlasCandidates(string assetName)
    {
        return assetName switch
        {
            "MenuFont" =>
            [
                (
                    ContentRoot.GetPath("Sprites", "Menu", "Fonts", "MenuBuildFontAtlas.png"),
                    ContentRoot.GetPath("Sprites", "Menu", "Fonts", "MenuBuildFontAtlas.json")
                ),
                (
                    ContentRoot.GetPath("Sprites", "Menu", "Fonts", "MenuFontAtlas.png"),
                    ContentRoot.GetPath("Sprites", "Menu", "Fonts", "MenuFontAtlas.json")
                ),
            ],
            "ConsoleFont" =>
            [
                (
                    ContentRoot.GetPath("Sprites", "Menu", "Fonts", "MenuFontAtlas.png"),
                    ContentRoot.GetPath("Sprites", "Menu", "Fonts", "MenuFontAtlas.json")
                ),
                (
                    ContentRoot.GetPath("Sprites", "Menu", "Fonts", "MenuBuildFontAtlas.png"),
                    ContentRoot.GetPath("Sprites", "Menu", "Fonts", "MenuBuildFontAtlas.json")
                ),
            ],
            _ => [],
        };
    }

    private bool TryCreateSpriteFontFromAtlas(string? texturePath, string? metadataPath, out SpriteFont spriteFont)
    {
        spriteFont = null!;
        if (string.IsNullOrWhiteSpace(texturePath) || string.IsNullOrWhiteSpace(metadataPath))
        {
            return false;
        }

        var texture = LoadSpriteFrameFromPath(texturePath);
        if (texture is null)
        {
            return false;
        }

        try
        {
            string? metadataJson = TryGetBrowserContentText(metadataPath, out var browserMetadata)
                ? browserMetadata
                : File.Exists(metadataPath)
                    ? File.ReadAllText(metadataPath)
                    : null;
            if (string.IsNullOrWhiteSpace(metadataJson))
            {
                texture.Dispose();
                return false;
            }

            var metadata = JsonSerializer.Deserialize<MenuBitmapFontData>(metadataJson, MenuBitmapFontJsonOptions);
            if (metadata is null || metadata.Glyphs.Count == 0)
            {
                texture.Dispose();
                return false;
            }

            var characters = new List<char>(metadata.Glyphs.Count);
            var glyphBounds = new List<Rectangle>(metadata.Glyphs.Count);
            var croppings = new List<Rectangle>(metadata.Glyphs.Count);
            var kerning = new List<Vector3>(metadata.Glyphs.Count);

            for (var index = 0; index < metadata.Glyphs.Count; index += 1)
            {
                var glyph = metadata.Glyphs[index];
                var width = Math.Max(0, glyph.Width);
                var height = Math.Max(0, glyph.Height);
                var sourceOffsetX = texture.SourceRectangle?.X ?? 0;
                var sourceOffsetY = texture.SourceRectangle?.Y ?? 0;
                characters.Add((char)glyph.Character);
                glyphBounds.Add(new Rectangle(sourceOffsetX + glyph.X, sourceOffsetY + glyph.Y, width, height));
                croppings.Add(new Rectangle(0, 0, width, height));
                kerning.Add(new Vector3(0f, Math.Max(1, glyph.Advance), 0f));
            }

            spriteFont = new SpriteFont(
                texture.Texture,
                glyphBounds,
                croppings,
                characters,
                Math.Max(1, metadata.LineHeight),
                Math.Max(0f, metadata.Spacing),
                kerning,
                '?');
            return true;
        }
        catch
        {
            texture.Dispose();
            return false;
        }
    }

    private SpriteFont CreateBrowserFallbackSpriteFont()
    {
        const int glyphWidth = 6;
        const int glyphHeight = 10;

        var texture = new Texture2D(GraphicsDevice, glyphWidth, glyphHeight);
        var pixels = new Color[glyphWidth * glyphHeight];
        for (var index = 0; index < pixels.Length; index += 1)
        {
            pixels[index] = Color.White;
        }

        texture.SetData(pixels);

        var source = new Rectangle(0, 0, glyphWidth, glyphHeight);
        var crop = new Rectangle(0, 0, glyphWidth, glyphHeight);
        var characters = new List<char>();
        var glyphBounds = new List<Rectangle>();
        var croppings = new List<Rectangle>();
        var kerning = new List<Vector3>();

        for (var codePoint = 32; codePoint <= 126; codePoint += 1)
        {
            characters.Add((char)codePoint);
            glyphBounds.Add(source);
            croppings.Add(crop);
            kerning.Add(new Vector3(0f, glyphWidth, 1f));
        }

        return new SpriteFont(
            texture,
            glyphBounds,
            croppings,
            characters,
            glyphHeight + 2,
            1f,
            kerning,
            '?');
    }
}
