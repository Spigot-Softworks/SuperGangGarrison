#nullable enable

using Microsoft.Xna.Framework;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class WindowTextInputController
    {
        private readonly Game1 _game;

        public WindowTextInputController(Game1 game)
        {
            _game = game;
        }

        public void Handle(TextInputEventArgs e)
        {
            if (_game.HandleNavEditorTextInput(e))
            {
                return;
            }

            if (_game._networkPromptTextInputController.TryHandle(e))
            {
                return;
            }

            if (_game._mainMenuOpen && _game._manualConnectOpen && _game._menuTextInputController.TryHandleManualConnect(e))
            {
                return;
            }

            if (_game._mainMenuOpen && _game._hostSetupOpen && _game._hostSetupFlowController.HandleHostSetupTextInput(e))
            {
                return;
            }

            if (_game._optionsMenuOpen && _game._editingPlayerName && _game._menuTextInputController.TryHandlePlayerNameEdit(e))
            {
                return;
            }

            if (_game._chatTextInputController.TryHandle(e))
            {
                return;
            }

            _game._consoleTextInputController.Handle(e);
        }
    }
}
