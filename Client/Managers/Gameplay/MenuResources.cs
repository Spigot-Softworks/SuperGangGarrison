#nullable enable

using System.Collections.Generic;

namespace OpenGarrison.Client;

public sealed class MenuResources
{
    public LoadedSpriteFrame? BackgroundTexture;
    public string? BackgroundTexturePath;
    public int ImageFrame;
    public LoadedSpriteFrame? CursorTexture;
    public LoadedSpriteFrame? BitmapFontTexture;
    public readonly Dictionary<char, Game1.MenuBitmapGlyph> BitmapFontGlyphs = new();
    public int BitmapFontLineHeight;
    public int BitmapFontSpacing = 1;
    public LoadedSpriteFrame? PlaqueTexture;
    public LoadedSpriteFrame? PlaqueTallTexture;
    public LoadedSpriteFrame? TextBoxTopTexture;
    public LoadedSpriteFrame? TextBoxMiddleTexture;
    public LoadedSpriteFrame? TextBoxBottomTexture;
    public LoadedSpriteFrame? TextBoxSoloTexture;
    public LoadedSpriteFrame? LastToDieMenuPlaqueTexture;
    public LoadedSpriteFrame? LastToDieMenuTextBoxSoloTexture;
}
