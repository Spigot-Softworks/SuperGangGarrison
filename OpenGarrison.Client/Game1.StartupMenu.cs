#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void UpdateStartupSplash(KeyboardState keyboard, MouseState mouse)
    {
        StopMenuMusic();
        StopIngameMusic();
        EnsureFaucetMusicPlaying();

        _startupSplashTicks += 1;
        if (_startupSplashTicks >= 30 && _startupSplashFrame < 21f)
        {
            _startupSplashFrame = MathF.Min(21f, _startupSplashFrame + 0.2f);
        }

        var anyKeyPressed = keyboard.GetPressedKeys().Length > 0;
        var leftClickPressed = mouse.LeftButton == ButtonState.Pressed;
        if (_startupSplashTicks >= 240 || anyKeyPressed || leftClickPressed)
        {
            _startupSplashOpen = false;
            _mainMenuOpen = true;
            StopFaucetMusic();
        }
    }

    private void DrawStartupSplash()
    {
        var viewportWidth = ViewportWidth;
        var viewportHeight = ViewportHeight;
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black);

        var sprite = _runtimeAssets.GetSprite("FaucetLogoS");
        if (sprite is null || sprite.Frames.Count == 0)
        {
            DrawBitmapFontText("Faucet Software", new Vector2(viewportWidth / 2f - 120f, viewportHeight / 2f), Color.White, 2f);
            return;
        }

        var frameIndex = Math.Clamp((int)MathF.Floor(_startupSplashFrame), 0, sprite.Frames.Count - 1);
        _spriteBatch.Draw(
            sprite.Frames[frameIndex],
            new Vector2(viewportWidth / 2f, viewportHeight / 2f),
            null,
            Color.White,
            0f,
            sprite.Origin.ToVector2(),
            new Vector2(4f, 4f),
            SpriteEffects.None,
            0f);
    }
}
