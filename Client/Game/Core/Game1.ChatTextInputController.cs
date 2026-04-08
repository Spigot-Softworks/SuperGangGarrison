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
            if (!_game._chatOpen)
            {
                return false;
            }

            switch (e.Character)
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
                    if (!char.IsControl(e.Character) && _game._chatInput.Length < 120)
                    {
                        _game._chatInput += e.Character;
                    }
                    break;
            }

            return true;
        }
    }
}
