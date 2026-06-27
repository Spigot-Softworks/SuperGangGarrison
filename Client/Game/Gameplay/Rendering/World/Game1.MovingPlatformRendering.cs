#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void DrawMovingPlatforms(Vector2 cameraPosition)
    {
        if (_world.MovingPlatforms.Count == 0)
        {
            return;
        }

        var fill = new Color(126, 142, 154, 215);
        var edge = new Color(232, 238, 242, 230);
        foreach (var platform in _world.MovingPlatforms)
        {
            var position = GetWorldScreenPosition(platform.X, platform.Y, cameraPosition);
            var width = MathF.Max(1f, platform.Width);
            var height = MathF.Max(1f, platform.Height);
            if (TryGetRuntimeCustomMapVisualResourceTexture(platform.ResourceName, out var texture))
            {
                _spriteBatch.Draw(
                    texture,
                    position,
                    null,
                    Color.White,
                    0f,
                    Vector2.Zero,
                    new Vector2(width / texture.Width, height / texture.Height),
                    SpriteEffects.None,
                    0f);
                continue;
            }

            DrawScreenPixelRectangle(position, width, height, fill);
            DrawScreenPixelRectangle(position, width, 1f, edge);
        }
    }
}
