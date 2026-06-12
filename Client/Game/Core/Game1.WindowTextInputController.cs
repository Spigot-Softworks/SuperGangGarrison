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
            Handle(e.Character);
        }

        public void Handle(char character)
        {
            if (_game.HandleGarrisonBuilderTextInput(character))
            {
                return;
            }

            if (_game.HandleNavEditorTextInput(character))
            {
                return;
            }

            if (_game._networkPromptTextInputController.TryHandle(character))
            {
                return;
            }

            if (_game._practiceSetupOpen && _game.TryHandlePracticeMapBrowserTextInput(character))
            {
                return;
            }

            if (_game._mainMenuOpen && _game._manualConnectOpen && _game._menuTextInputController.TryHandleManualConnect(character))
            {
                return;
            }

            if (_game._friendsMenuOpen && _game._editingFriendNickname && _game._menuTextInputController.TryHandleFriendNicknameEdit(character))
            {
                return;
            }

            if (_game._friendsMenuOpen && _game._editingFriendCode && _game._menuTextInputController.TryHandleFriendCodeEdit(character))
            {
                return;
            }

            if (_game._friendsMenuOpen && _game._editingFriendMessage && _game._menuTextInputController.TryHandleFriendMessageEdit(character))
            {
                return;
            }

            if (_game._mainMenuOpen && _game._hostSetupOpen && _game._hostSetupFlowController.HandleHostSetupTextInput(character))
            {
                return;
            }

            if (_game._optionsMenuOpen && _game._editingPlayerName && _game._menuTextInputController.TryHandlePlayerNameEdit(character))
            {
                return;
            }

            if (_game._chatTextInputController.TryHandle(character))
            {
                return;
            }

            _game._consoleTextInputController.Handle(character);
        }
    }
}
