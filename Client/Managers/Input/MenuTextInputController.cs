#nullable enable

using System.Globalization;
using Microsoft.Xna.Framework;
using OpenGarrison.Core;

using static OpenGarrison.Client.Game1;

namespace OpenGarrison.Client;

public sealed class MenuTextInputController
    {
        private readonly IInputContext _context;

        public MenuTextInputController(IInputContext context)
        {
            _context = context;
        }

        public TextEditState ConnectHostEdit { get; } = new("127.0.0.1");

        public TextEditState ConnectPortEdit { get; } = new(OpenGarrisonPreferencesDocument.DefaultServerPort.ToString(CultureInfo.InvariantCulture));

        public TextEditState FriendNicknameEdit { get; } = new();

        public TextEditState FriendCodeEdit { get; } = new();

        public TextEditState FriendMessageEdit { get; } = new();

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
                            ConnectPortEdit.Text,
                            ConnectPortEdit.CursorIndex,
                            ConnectPortEdit.SelectionStart);
                        ConnectPortEdit.Text = result.Text;
                        ConnectPortEdit.CursorIndex = result.CursorIndex;
                        ConnectPortEdit.SelectionStart = result.SelectionStart;
                    }
                    else
                    {
                        var result = _context.DeleteTextSelectionOrBackspace(
                            ConnectHostEdit.Text,
                            ConnectHostEdit.CursorIndex,
                            ConnectHostEdit.SelectionStart);
                        ConnectHostEdit.Text = result.Text;
                        ConnectHostEdit.CursorIndex = result.CursorIndex;
                        ConnectHostEdit.SelectionStart = result.SelectionStart;
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
                                ConnectPortEdit.Text,
                                character,
                                ConnectPortEdit.CursorIndex,
                                ConnectPortEdit.SelectionStart,
                                5);
                            ConnectPortEdit.Text = result.Text;
                            ConnectPortEdit.CursorIndex = result.CursorIndex;
                            ConnectPortEdit.SelectionStart = result.SelectionStart;
                        }
                    }
                    else
                    {
                        var result = _context.InsertTextCharacterAtCursor(
                            ConnectHostEdit.Text,
                            character,
                            ConnectHostEdit.CursorIndex,
                            ConnectHostEdit.SelectionStart,
                            64);
                        ConnectHostEdit.Text = result.Text;
                        ConnectHostEdit.CursorIndex = result.CursorIndex;
                        ConnectHostEdit.SelectionStart = result.SelectionStart;
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
                        FriendCodeEdit.Text,
                        FriendCodeEdit.CursorIndex,
                        FriendCodeEdit.SelectionStart);
                    FriendCodeEdit.Text = result.Text;
                    FriendCodeEdit.CursorIndex = result.CursorIndex;
                    FriendCodeEdit.SelectionStart = result.SelectionStart;
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
                            FriendCodeEdit.Text,
                            char.ToUpperInvariant(character),
                            FriendCodeEdit.CursorIndex,
                            FriendCodeEdit.SelectionStart,
                            20);
                        FriendCodeEdit.Text = result.Text;
                        FriendCodeEdit.CursorIndex = result.CursorIndex;
                        FriendCodeEdit.SelectionStart = result.SelectionStart;
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
                        FriendNicknameEdit.Text,
                        FriendNicknameEdit.CursorIndex,
                        FriendNicknameEdit.SelectionStart);
                    FriendNicknameEdit.Text = result.Text;
                    FriendNicknameEdit.CursorIndex = result.CursorIndex;
                    FriendNicknameEdit.SelectionStart = result.SelectionStart;
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
                            FriendNicknameEdit.Text,
                            character,
                            FriendNicknameEdit.CursorIndex,
                            FriendNicknameEdit.SelectionStart,
                            20);
                        FriendNicknameEdit.Text = result.Text;
                        FriendNicknameEdit.CursorIndex = result.CursorIndex;
                        FriendNicknameEdit.SelectionStart = result.SelectionStart;
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
                        FriendMessageEdit.Text,
                        FriendMessageEdit.CursorIndex,
                        FriendMessageEdit.SelectionStart);
                    FriendMessageEdit.Text = result.Text;
                    FriendMessageEdit.CursorIndex = result.CursorIndex;
                    FriendMessageEdit.SelectionStart = result.SelectionStart;
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
                            FriendMessageEdit.Text,
                            character,
                            FriendMessageEdit.CursorIndex,
                            FriendMessageEdit.SelectionStart,
                            500);
                        FriendMessageEdit.Text = result.Text;
                        FriendMessageEdit.CursorIndex = result.CursorIndex;
                        FriendMessageEdit.SelectionStart = result.SelectionStart;
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
