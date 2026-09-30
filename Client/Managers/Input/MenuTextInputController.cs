#nullable enable

using Microsoft.Xna.Framework;

using static OpenGarrison.Client.Game1;

namespace OpenGarrison.Client;

public sealed class MenuTextInputController
    {
        private readonly IInputContext _context;

        public MenuTextInputController(IInputContext context)
        {
            _context = context;
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
                    if (_context._editingConnectPort)
                    {
                        var result = _context.DeleteTextSelectionOrBackspace(
                            _context._connectPortBuffer,
                            _context._connectPortCursorIndex,
                            _context._connectPortSelectionStart);
                        _context._connectPortBuffer = result.Text;
                        _context._connectPortCursorIndex = result.CursorIndex;
                        _context._connectPortSelectionStart = result.SelectionStart;
                    }
                    else
                    {
                        var result = _context.DeleteTextSelectionOrBackspace(
                            _context._connectHostBuffer,
                            _context._connectHostCursorIndex,
                            _context._connectHostSelectionStart);
                        _context._connectHostBuffer = result.Text;
                        _context._connectHostCursorIndex = result.CursorIndex;
                        _context._connectHostSelectionStart = result.SelectionStart;
                    }
                    break;
                }
                case '\t':
                    _context.Session.Connection.ToggleManualConnectEditingField();
                    break;
                case '\r':
                case '\n':
                    _context.TryConnectFromMenu();
                    break;
                default:
                    if (char.IsControl(character))
                    {
                        break;
                    }

                    if (_context._editingConnectPort)
                    {
                        if (char.IsDigit(character))
                        {
                            var result = _context.InsertTextCharacterAtCursor(
                                _context._connectPortBuffer,
                                character,
                                _context._connectPortCursorIndex,
                                _context._connectPortSelectionStart,
                                5);
                            _context._connectPortBuffer = result.Text;
                            _context._connectPortCursorIndex = result.CursorIndex;
                            _context._connectPortSelectionStart = result.SelectionStart;
                        }
                    }
                    else
                    {
                        var result = _context.InsertTextCharacterAtCursor(
                            _context._connectHostBuffer,
                            character,
                            _context._connectHostCursorIndex,
                            _context._connectHostSelectionStart,
                            64);
                        _context._connectHostBuffer = result.Text;
                        _context._connectHostCursorIndex = result.CursorIndex;
                        _context._connectHostSelectionStart = result.SelectionStart;
                    }
                    break;
            }

            return true;
        }

        public bool TryHandlePlayerNameEdit(TextInputEventArgs e)
        {
            return TryHandlePlayerNameEdit(e.Character);
        }

        public bool TryHandleFriendCodeEdit(char character)
        {
            switch (character)
            {
                case '\b':
                {
                    var result = _context.DeleteTextSelectionOrBackspace(
                        _context._friendCodeInputBuffer,
                        _context._friendCodeCursorIndex,
                        _context._friendCodeSelectionStart);
                    _context._friendCodeInputBuffer = result.Text;
                    _context._friendCodeCursorIndex = result.CursorIndex;
                    _context._friendCodeSelectionStart = result.SelectionStart;
                    break;
                }
                case '\r':
                case '\n':
                    _context.TrySendFriendRequestFromInput();
                    break;
                default:
                    if (char.IsAsciiLetterOrDigit(character) || character == '-')
                    {
                        var result = _context.InsertTextCharacterAtCursor(
                            _context._friendCodeInputBuffer,
                            char.ToUpperInvariant(character),
                            _context._friendCodeCursorIndex,
                            _context._friendCodeSelectionStart,
                            20);
                        _context._friendCodeInputBuffer = result.Text;
                        _context._friendCodeCursorIndex = result.CursorIndex;
                        _context._friendCodeSelectionStart = result.SelectionStart;
                    }
                    break;
            }

            return true;
        }

        public bool TryHandleFriendNicknameEdit(char character)
        {
            switch (character)
            {
                case '\b':
                {
                    var result = _context.DeleteTextSelectionOrBackspace(
                        _context._friendNicknameInputBuffer,
                        _context._friendNicknameCursorIndex,
                        _context._friendNicknameSelectionStart);
                    _context._friendNicknameInputBuffer = result.Text;
                    _context._friendNicknameCursorIndex = result.CursorIndex;
                    _context._friendNicknameSelectionStart = result.SelectionStart;
                    break;
                }
                case '\r':
                case '\n':
                    _context.SaveFriendNicknameFromInput();
                    break;
                default:
                    if (!char.IsControl(character) && character != '#')
                    {
                        var result = _context.InsertTextCharacterAtCursor(
                            _context._friendNicknameInputBuffer,
                            character,
                            _context._friendNicknameCursorIndex,
                            _context._friendNicknameSelectionStart,
                            20);
                        _context._friendNicknameInputBuffer = result.Text;
                        _context._friendNicknameCursorIndex = result.CursorIndex;
                        _context._friendNicknameSelectionStart = result.SelectionStart;
                    }
                    break;
            }

            return true;
        }

        public bool TryHandleFriendMessageEdit(char character)
        {
            switch (character)
            {
                case '\b':
                {
                    var result = _context.DeleteTextSelectionOrBackspace(
                        _context._friendMessageInputBuffer,
                        _context._friendMessageCursorIndex,
                        _context._friendMessageSelectionStart);
                    _context._friendMessageInputBuffer = result.Text;
                    _context._friendMessageCursorIndex = result.CursorIndex;
                    _context._friendMessageSelectionStart = result.SelectionStart;
                    break;
                }
                case '\r':
                case '\n':
                    _context.TrySendSelectedFriendDirectMessageFromInput();
                    break;
                default:
                    if (!char.IsControl(character))
                    {
                        var result = _context.InsertTextCharacterAtCursor(
                            _context._friendMessageInputBuffer,
                            character,
                            _context._friendMessageCursorIndex,
                            _context._friendMessageSelectionStart,
                            500);
                        _context._friendMessageInputBuffer = result.Text;
                        _context._friendMessageCursorIndex = result.CursorIndex;
                        _context._friendMessageSelectionStart = result.SelectionStart;
                    }
                    break;
            }

            return true;
        }

        public bool TryHandlePlayerNameEdit(char character)
        {
            switch (character)
            {
                case '\b':
                {
                    var result = _context.DeleteTextSelectionOrBackspace(
                        _context._playerNameEditBuffer,
                        _context._playerNameEditCursorIndex,
                        _context._playerNameEditSelectionStart);
                    _context._playerNameEditBuffer = result.Text;
                    _context._playerNameEditCursorIndex = result.CursorIndex;
                    _context._playerNameEditSelectionStart = result.SelectionStart;
                    break;
                }
                case '\r':
                case '\n':
                    if (_context._namePromptOpen)
                    {
                        _context.CommitPlayerNamePrompt();
                    }
                    else
                    {
                        _context.SetLocalPlayerNameFromSettings(_context._playerNameEditBuffer);
                        _context._editingPlayerName = false;
                    }
                    break;
                default:
                    if (!char.IsControl(character) && character != '#' && _context._playerNameEditBuffer.Length < 20)
                    {
                        var result = _context.InsertTextCharacterAtCursor(
                            _context._playerNameEditBuffer,
                            character,
                            _context._playerNameEditCursorIndex,
                            _context._playerNameEditSelectionStart,
                            20);
                        _context._playerNameEditBuffer = result.Text;
                        _context._playerNameEditCursorIndex = result.CursorIndex;
                        _context._playerNameEditSelectionStart = result.SelectionStart;
                    }
                    break;
            }

            return true;
        }
}
