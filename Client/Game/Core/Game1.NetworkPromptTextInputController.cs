#nullable enable

using Microsoft.Xna.Framework;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class NetworkPromptTextInputController
    {
        private readonly Game1 _game;

        public NetworkPromptTextInputController(Game1 game)
        {
            _game = game;
        }

        public bool TryHandle(TextInputEventArgs e)
        {
            return TryHandle(e.Character);
        }

        public bool TryHandle(char character)
        {
            if (!_game._passwordPromptOpen)
            {
                return false;
            }

            switch (character)
            {
                case '\b':
                    if (_game._passwordEditBuffer.Length > 0)
                    {
                        _game._passwordEditBuffer = _game._passwordEditBuffer[..^1];
                    }
                    break;
                case '\r':
                case '\n':
                    if (!string.IsNullOrEmpty(_game._passwordEditBuffer))
                    {
                        _game._passwordPromptMessage = "Submitting...";
                        _game._networkClient.SendPassword(_game._passwordEditBuffer);
                    }
                    else
                    {
                        _game._passwordPromptMessage = "Password required.";
                    }
                    break;
                default:
                    if (!char.IsControl(character) && _game._passwordEditBuffer.Length < 32)
                    {
                        _game._passwordEditBuffer += character;
                        _game._passwordPromptMessage = string.Empty;
                    }
                    break;
            }

            return true;
        }
    }
}
