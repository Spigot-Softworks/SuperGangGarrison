#nullable enable

using Microsoft.Xna.Framework;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class MenuTextInputController
    {
        private readonly Game1 _game;

        public MenuTextInputController(Game1 game)
        {
            _game = game;
        }

        public bool TryHandleManualConnect(TextInputEventArgs e)
        {
            switch (e.Character)
            {
                case '\b':
                    if (_game._editingConnectPort)
                    {
                        if (_game._connectPortBuffer.Length > 0)
                        {
                            _game._connectPortBuffer = _game._connectPortBuffer[..^1];
                        }
                    }
                    else if (_game._connectHostBuffer.Length > 0)
                    {
                        _game._connectHostBuffer = _game._connectHostBuffer[..^1];
                    }
                    break;
                case '\t':
                    _game._connectionFlowController.ToggleManualConnectEditingField();
                    break;
                case '\r':
                case '\n':
                    _game.TryConnectFromMenu();
                    break;
                default:
                    if (char.IsControl(e.Character))
                    {
                        break;
                    }

                    if (_game._editingConnectPort)
                    {
                        if (char.IsDigit(e.Character) && _game._connectPortBuffer.Length < 5)
                        {
                            _game._connectPortBuffer += e.Character;
                        }
                    }
                    else if (_game._connectHostBuffer.Length < 64)
                    {
                        _game._connectHostBuffer += e.Character;
                    }
                    break;
            }

            return true;
        }

        public bool TryHandlePlayerNameEdit(TextInputEventArgs e)
        {
            switch (e.Character)
            {
                case '\b':
                    if (_game._playerNameEditBuffer.Length > 0)
                    {
                        _game._playerNameEditBuffer = _game._playerNameEditBuffer[..^1];
                    }
                    break;
                case '\r':
                case '\n':
                    _game.SetLocalPlayerNameFromSettings(_game._playerNameEditBuffer);
                    _game._editingPlayerName = false;
                    break;
                default:
                    if (!char.IsControl(e.Character) && _game._playerNameEditBuffer.Length < 20 && e.Character != '#')
                    {
                        _game._playerNameEditBuffer += e.Character;
                    }
                    break;
            }

            return true;
        }
    }
}
