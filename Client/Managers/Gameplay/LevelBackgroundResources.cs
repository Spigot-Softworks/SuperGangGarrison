#nullable enable

using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public sealed class LevelBackgroundResources
{
    public Texture2D? Texture { get; set; }

    public string? TexturePath { get; set; }

    public string? FailedPath { get; set; }

    public SimpleLevel? TextureLevel { get; set; }
}
