#nullable enable

using Microsoft.Xna.Framework;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class ConsoleTextInputController
    {
        private readonly Game1 _game;

        public ConsoleTextInputController(Game1 game)
        {
            _game = game;
        }

        public void Handle(TextInputEventArgs e)
        {
            Handle(e.Character);
        }

        public void Handle(char character)
        {
            if (!_game._consoleOpen)
            {
                return;
            }

            switch (character)
            {
                case '\b':
                {
                    var result = _game.DeleteTextSelectionOrBackspace(
                        _game._consoleInput,
                        _game._consoleInputCursorIndex,
                        _game._consoleInputSelectionStart);
                    _game._consoleInput = result.Text;
                    _game._consoleInputCursorIndex = result.CursorIndex;
                    _game._consoleInputSelectionStart = result.SelectionStart;
                    break;
                }
                case '\r':
                    _game.ExecuteConsoleCommand();
                    break;
                case '`':
                case '~':
                    break;
                default:
                    if (!char.IsControl(character))
                    {
                        var result = _game.InsertTextCharacterAtCursor(
                            _game._consoleInput,
                            character,
                            _game._consoleInputCursorIndex,
                            _game._consoleInputSelectionStart,
                            int.MaxValue);
                        _game._consoleInput = result.Text;
                        _game._consoleInputCursorIndex = result.CursorIndex;
                        _game._consoleInputSelectionStart = result.SelectionStart;
                    }
                    break;
            }
        }
    }
}
