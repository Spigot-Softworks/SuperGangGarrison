#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

using static OpenGarrison.Client.Game1;

namespace OpenGarrison.Client;

public sealed class GameplayUpdateController
    {
        private readonly IGameplayContext _context;
        private int _prePredictionFlameCount;

        public GameplayUpdateController(IGameplayContext context)
        {
            _context = context;
        }

        internal int PrePredictionFlameCount => _prePredictionFlameCount;

        public void UpdateFrame(GameTime gameTime, KeyboardState keyboard, MouseState mouse, MouseState rawMouse, int clientTicks)
        {
            if (_context.IsPracticeNavigationWarmupBlockingGameplay()
                && _context.UpdatePracticeNavigationWarmup())
            {
                return;
            }

            if (_context._networkClient.IsConnected)
            {
                _context.ProcessNetworkMessages();
            }

            var networkInput = _context.Gameplay.InputUpdate.PrepareFrame(gameTime, keyboard, mouse, rawMouse);
            _prePredictionFlameCount = _context._world.Flames.Count;
            if (_context._networkClient.IsConnected)
            {
                // Emit from the received positions before local projectile prediction
                // can consume a short-lived flame at a nearby wall. Otherwise packet
                // arrival phase can erase every flame before the particle tick sees it.
                for (var tick = 0; tick < clientTicks; tick += 1)
                {
                    _context.AdvanceFlameSmokeVisuals();
                }
            }
            _context.AdvanceGameplaySimulation(gameTime, networkInput);
            _context.Gameplay.PresentationUpdate.FinalizeFrame(gameTime, keyboard, mouse, clientTicks);
        }
}
