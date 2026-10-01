#nullable enable

using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace OpenGarrison.Client;

public sealed class SpriteFrameCacheResources
{
    public Dictionary<LoadedSpriteFrame, LoadedSpriteFrame> NeutralSpriteFrameCache { get; } = new();

    public Dictionary<LoadedSpriteFrame, Rectangle> SpriteFontOpaqueBoundsCache { get; } = new();
}
