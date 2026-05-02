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
            return TryHandleManualConnect(e.Character);
        }

        public bool TryHandleManualConnect(char character)
        {
            switch (character)
            {
                case '\b':
                {
                    if (_game._editingConnectPort)
                    {
                        var result = _game.DeleteTextSelectionOrBackspace(
                            _game._connectPortBuffer,
                            _game._connectPortCursorIndex,
                            _game._connectPortSelectionStart);
                        _game._connectPortBuffer = result.Text;
                        _game._connectPortCursorIndex = result.CursorIndex;
                        _game._connectPortSelectionStart = result.SelectionStart;
                    }
                    else
                    {
                        var result = _game.DeleteTextSelectionOrBackspace(
                            _game._connectHostBuffer,
                            _game._connectHostCursorIndex,
                            _game._connectHostSelectionStart);
                        _game._connectHostBuffer = result.Text;
                        _game._connectHostCursorIndex = result.CursorIndex;
                        _game._connectHostSelectionStart = result.SelectionStart;
                    }
                    break;
                }
                case '\t':
                    _game._connectionFlowController.ToggleManualConnectEditingField();
                    break;
                case '\r':
                case '\n':
                    _game.TryConnectFromMenu();
                    break;
                default:
                    if (char.IsControl(character))
                    {
                        break;
                    }

                    if (_game._editingConnectPort)
                    {
                        if (char.IsDigit(character))
                        {
                            var result = _game.InsertTextCharacterAtCursor(
                                _game._connectPortBuffer,
                                character,
                                _game._connectPortCursorIndex,
                                _game._connectPortSelectionStart,
                                5);
                            _game._connectPortBuffer = result.Text;
                            _game._connectPortCursorIndex = result.CursorIndex;
                            _game._connectPortSelectionStart = result.SelectionStart;
                        }
                    }
                    else
                    {
                        var result = _game.InsertTextCharacterAtCursor(
                            _game._connectHostBuffer,
                            character,
                            _game._connectHostCursorIndex,
                            _game._connectHostSelectionStart,
                            64);
                        _game._connectHostBuffer = result.Text;
                        _game._connectHostCursorIndex = result.CursorIndex;
                        _game._connectHostSelectionStart = result.SelectionStart;
                    }
                    break;
            }

            return true;
        }

        public bool TryHandlePlayerNameEdit(TextInputEventArgs e)
        {
            return TryHandlePlayerNameEdit(e.Character);
        }

        public bool TryHandlePlayerNameEdit(char character)
        {
            switch (character)
            {
                case '\b':
                {
                    var result = _game.DeleteTextSelectionOrBackspace(
                        _game._playerNameEditBuffer,
                        _game._playerNameEditCursorIndex,
                        _game._playerNameEditSelectionStart);
                    _game._playerNameEditBuffer = result.Text;
                    _game._playerNameEditCursorIndex = result.CursorIndex;
                    _game._playerNameEditSelectionStart = result.SelectionStart;
                    break;
                }
                case '\r':
                case '\n':
                    _game.SetLocalPlayerNameFromSettings(_game._playerNameEditBuffer);
                    _game._editingPlayerName = false;
                    break;
                default:
                    if (!char.IsControl(character) && character != '#' && _game._playerNameEditBuffer.Length < 20)
                    {
                        var result = _game.InsertTextCharacterAtCursor(
                            _game._playerNameEditBuffer,
                            character,
                            _game._playerNameEditCursorIndex,
                            _game._playerNameEditSelectionStart,
                            20);
                        _game._playerNameEditBuffer = result.Text;
                        _game._playerNameEditCursorIndex = result.CursorIndex;
                        _game._playerNameEditSelectionStart = result.SelectionStart;
                    }
                    break;
            }

            return true;
        }
    }
}
