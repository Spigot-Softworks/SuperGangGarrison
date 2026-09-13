#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class GameplayUpdateController
    {
        private readonly Game1 _game;
        private readonly GameplayInputUpdateController _inputUpdateController;
        private readonly GameplayPresentationUpdateController _presentationUpdateController;

        public GameplayUpdateController(Game1 game)
        {
            _game = game;
            _inputUpdateController = new GameplayInputUpdateController(game);
            _presentationUpdateController = new GameplayPresentationUpdateController(game);
        }

        public void UpdateFrame(GameTime gameTime, KeyboardState keyboard, MouseState mouse, MouseState rawMouse, int clientTicks)
        {
            if (_game.IsPracticeNavigationWarmupBlockingGameplay()
                && _game.UpdatePracticeNavigationWarmup())
            {
                return;
            }

            if (_game._networkClient.IsConnected)
            {
                _game.ProcessNetworkMessages();
            }

            var networkInput = _inputUpdateController.PrepareFrame(gameTime, keyboard, mouse, rawMouse);
            _game._prePredictionFlameCount = _game._world.Flames.Count;
            if (_game._networkClient.IsConnected)
            {
                // Emit from the received positions before local projectile prediction
                // can consume a short-lived flame at a nearby wall. Otherwise packet
                // arrival phase can erase every flame before the particle tick sees it.
                for (var tick = 0; tick < clientTicks; tick += 1)
                {
                    _game.AdvanceFlameSmokeVisuals();
                }
            }
            _game.AdvanceGameplaySimulation(gameTime, networkInput);
            _presentationUpdateController.FinalizeFrame(gameTime, keyboard, mouse, clientTicks);
        }
    }
}
