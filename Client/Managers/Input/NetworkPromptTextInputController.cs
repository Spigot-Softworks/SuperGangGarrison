#nullable enable

using Microsoft.Xna.Framework;

using static OpenGarrison.Client.Game1;

namespace OpenGarrison.Client;

public sealed class NetworkPromptTextInputController
    {
        private readonly IInputContext _context;

        public NetworkPromptTextInputController(IInputContext context)
        {
            _context = context;
        }

        public bool TryHandle(TextInputEventArgs e)
        {
            return TryHandle(e.Character);
        }

        public bool TryHandle(char character)
        {
            if (!_context._passwordPromptOpen)
            {
                return false;
            }

            switch (character)
            {
                case '\b':
                {
                    var result = _context.DeleteTextSelectionOrBackspace(
                        _context._passwordEditBuffer,
                        _context._passwordEditCursorIndex,
                        _context._passwordEditSelectionStart);
                    _context._passwordEditBuffer = result.Text;
                    _context._passwordEditCursorIndex = result.CursorIndex;
                    _context._passwordEditSelectionStart = result.SelectionStart;
                    break;
                }
                case '\r':
                case '\n':
                    if (!string.IsNullOrEmpty(_context._passwordEditBuffer))
                    {
                        _context._passwordPromptMessage = "Submitting...";
                        _context._networkClient.SendPassword(_context._passwordEditBuffer);
                    }
                    else
                    {
                        _context._passwordPromptMessage = "Password required.";
                    }
                    break;
                default:
                    if (!char.IsControl(character) && _context._passwordEditBuffer.Length < 32)
                    {
                        var result = _context.InsertTextCharacterAtCursor(
                            _context._passwordEditBuffer,
                            character,
                            _context._passwordEditCursorIndex,
                            _context._passwordEditSelectionStart,
                            32);
                        _context._passwordEditBuffer = result.Text;
                        _context._passwordEditCursorIndex = result.CursorIndex;
                        _context._passwordEditSelectionStart = result.SelectionStart;
                        _context._passwordPromptMessage = string.Empty;
                    }
                    break;
            }

            return true;
        }
}
