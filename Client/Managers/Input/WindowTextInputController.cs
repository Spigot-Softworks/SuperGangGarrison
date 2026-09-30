#nullable enable

using Microsoft.Xna.Framework;

using static OpenGarrison.Client.Game1;

namespace OpenGarrison.Client;

public sealed class WindowTextInputController
    {
        private readonly IInputContext _context;

        public WindowTextInputController(IInputContext context)
        {
            _context = context;
        }

        public void Handle(TextInputEventArgs e)
        {
            Handle(e.Character);
        }

        public void Handle(char character)
        {
            if (!_context.IsWindowInputActive)
            {
                return;
            }

            if (_context.HandleManagedRoomText(character)) return;

            if (_context.HandleGarrisonBuilderTextInput(character))
            {
                return;
            }

            if (_context.Input.NetworkPromptTextInput.TryHandle(character))
            {
                return;
            }

            if (_context._practiceSetupOpen && _context.TryHandlePracticeMapBrowserTextInput(character))
            {
                return;
            }

            if (_context._mainMenuOpen && _context._manualConnectOpen && _context.Input.MenuTextInput.TryHandleManualConnect(character))
            {
                return;
            }

            if (_context._mainMenuOpen && _context._namePromptOpen && _context.Input.MenuTextInput.TryHandlePlayerNameEdit(character))
            {
                return;
            }

            if (_context._friendsMenuOpen && _context._editingFriendNickname && _context.Input.MenuTextInput.TryHandleFriendNicknameEdit(character))
            {
                return;
            }

            if (_context._friendsMenuOpen && _context._playerCardEditorOpen && _context.TryHandlePlayerCardBioTextInput(character))
            {
                return;
            }

            if (_context._friendsMenuOpen && _context._editingFriendCode && _context.Input.MenuTextInput.TryHandleFriendCodeEdit(character))
            {
                return;
            }

            if (_context._friendsMenuOpen && _context._editingFriendMessage && _context.Input.MenuTextInput.TryHandleFriendMessageEdit(character))
            {
                return;
            }

            if (_context._mainMenuOpen && _context._hostSetupOpen && _context.Hosting.HostSetup.HandleHostSetupTextInput(character))
            {
                return;
            }

            if (_context._optionsMenuOpen && _context._editingPlayerName && _context.Input.MenuTextInput.TryHandlePlayerNameEdit(character))
            {
                return;
            }

            if (_context._optionsMenuOpen && _context.TryHandleAccountDialogTextInput(character))
            {
                return;
            }

            if (_context.Input.ChatTextInput.TryHandle(character))
            {
                return;
            }

            _context.Input.ConsoleTextInput.Handle(character);
        }
}
