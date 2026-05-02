#nullable enable

using Microsoft.Xna.Framework;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class ChatTextInputController
    {
        private readonly Game1 _game;

        public ChatTextInputController(Game1 game)
        {
            _game = game;
        }

        public bool TryHandle(TextInputEventArgs e)
        {
            return TryHandle(e.Character);
        }

        public bool TryHandle(char character)
        {
            if (!_game._chatOpen)
            {
                return false;
            }

            switch (character)
            {
                case '\b':
                {
                    var result = _game.DeleteTextSelectionOrBackspace(
                        _game._chatInput,
                        _game._chatInputCursorIndex,
                        _game._chatInputSelectionStart);
                    _game._chatInput = result.Text;
                    _game._chatInputCursorIndex = result.CursorIndex;
                    _game._chatInputSelectionStart = result.SelectionStart;
                    break;
                }
                case '\r':
                case '\n':
                    _game.SubmitChatMessage();
                    break;
                default:
                    if (!char.IsControl(character) && _game._chatInput.Length < 120)
                    {
                        var result = _game.InsertTextCharacterAtCursor(
                            _game._chatInput,
                            character,
                            _game._chatInputCursorIndex,
                            _game._chatInputSelectionStart,
                            120);
                        _game._chatInput = result.Text;
                        _game._chatInputCursorIndex = result.CursorIndex;
                        _game._chatInputSelectionStart = result.SelectionStart;
                    }
                    break;
            }

            return true;
        }
    }
}
