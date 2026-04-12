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
                    if (_game._chatInput.Length > 0)
                    {
                        _game._chatInput = _game._chatInput[..^1];
                    }
                    break;
                case '\r':
                case '\n':
                    _game.SubmitChatMessage();
                    break;
                default:
                    if (!char.IsControl(character) && _game._chatInput.Length < 120)
                    {
                        _game._chatInput += character;
                    }
                    break;
            }

            return true;
        }
    }
}
