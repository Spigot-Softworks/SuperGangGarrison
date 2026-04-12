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
                    if (_game._consoleInput.Length > 0)
                    {
                        _game._consoleInput = _game._consoleInput[..^1];
                    }
                    break;
                case '\r':
                    _game.ExecuteConsoleCommand();
                    break;
                case '`':
                case '~':
                    break;
                default:
                    if (!char.IsControl(character))
                    {
                        _game._consoleInput += character;
                    }
                    break;
            }
        }
    }
}
