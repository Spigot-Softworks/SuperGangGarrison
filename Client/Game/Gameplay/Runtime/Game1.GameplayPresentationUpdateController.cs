#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class GameplayPresentationUpdateController
    {
        private readonly Game1 _game;

        public GameplayPresentationUpdateController(Game1 game)
        {
            _game = game;
        }

        public void FinalizeFrame(GameTime gameTime, KeyboardState keyboard, MouseState mouse, int clientTicks)
        {
            _game.DispatchClientSemanticGameplayEvents();
            _game.UpdateGameplayPresentation(gameTime, keyboard, mouse, clientTicks);
            _game.UpdateGameplayWindowState();
            _game.FinalizeGameplayFrame(keyboard, mouse);
        }
    }
}
