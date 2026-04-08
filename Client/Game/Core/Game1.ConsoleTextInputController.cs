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
            if (!_game._consoleOpen)
            {
                return;
            }

            switch (e.Character)
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
                    if (!char.IsControl(e.Character))
                    {
                        _game._consoleInput += e.Character;
                    }
                    break;
            }
        }
    }
}
