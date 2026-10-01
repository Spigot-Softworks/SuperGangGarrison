#nullable enable

using System.Collections.Generic;

namespace OpenGarrison.Client;

public sealed class GameplayLoadoutResources
{
    public LoadedSpriteFrame? ClassStripTexture;
    public LoadedSpriteFrame? ClassSelectionTexture;
    public LoadedSpriteFrame? BackgroundBarTexture;
    public LoadedSpriteFrame? DescriptionBoardTexture;
    public LoadedSpriteFrame? SelectionAtlasTexture;
    public readonly List<LoadedSpriteFrame> SelectionAtlasChunks = [];
    public LoadedSpriteFrame? SelectionTexture;
    public LoadedSpriteFrame? ScrollerTexture;
    public LoadedSpriteFrame? PageTexture;
    public LoadedSpriteFrame? BackButtonTexture;
    public LoadedSpriteFrame? HelmetTexture;
    public LoadedSpriteFrame? DogTagsTexture;
}
