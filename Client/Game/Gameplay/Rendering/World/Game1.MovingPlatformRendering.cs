#nullable enable

using Microsoft.Xna.Framework;

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
            var rectangle = new Rectangle(
                (int)MathF.Round(platform.X - cameraPosition.X),
                (int)MathF.Round(platform.Y - cameraPosition.Y),
                Math.Max(1, (int)MathF.Round(platform.Width)),
                Math.Max(1, (int)MathF.Round(platform.Height)));
            if (TryGetRuntimeCustomMapVisualResourceTexture(platform.ResourceName, out var texture))
            {
                _spriteBatch.Draw(texture, rectangle, Color.White);
                continue;
            }

            _spriteBatch.Draw(_pixel, rectangle, fill);
            _spriteBatch.Draw(_pixel, new Rectangle(rectangle.X, rectangle.Y, rectangle.Width, 1), edge);
        }
    }
}
