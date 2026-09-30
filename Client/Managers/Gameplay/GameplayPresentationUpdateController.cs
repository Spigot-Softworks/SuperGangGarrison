#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

using static OpenGarrison.Client.Game1;

namespace OpenGarrison.Client;

public sealed class GameplayPresentationUpdateController
    {
        private readonly IGameplayContext _context;

        public GameplayPresentationUpdateController(IGameplayContext context)
        {
            _context = context;
        }

        public void FinalizeFrame(GameTime gameTime, KeyboardState keyboard, MouseState mouse, int clientTicks)
        {
            _context.ObservePendingWorldHealingEventsForHealingCharacterEffects();
            _context.DispatchClientSemanticGameplayEvents();
            _context.UpdateGameplayPresentation(gameTime, keyboard, mouse, clientTicks);
            _context.UpdateGameplayWindowState();
            _context.FinalizeGameplayFrame(keyboard, mouse);
        }
}
