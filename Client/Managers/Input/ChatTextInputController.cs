#nullable enable

using Microsoft.Xna.Framework;

using static OpenGarrison.Client.Game1;

namespace OpenGarrison.Client;

public sealed class ChatTextInputController
    {
        private readonly IInputContext _context;

        public ChatTextInputController(IInputContext context)
        {
            _context = context;
        }

        public bool TryHandle(TextInputEventArgs e)
        {
            return TryHandle(e.Character);
        }

        public bool TryHandle(char character)
        {
            if (!_context._chatOpen)
            {
                return false;
            }

            switch (character)
            {
                case '\b':
                {
                    var result = _context.DeleteTextSelectionOrBackspace(
                        _context._chatInput,
                        _context._chatInputCursorIndex,
                        _context._chatInputSelectionStart);
                    _context._chatInput = result.Text;
                    _context._chatInputCursorIndex = result.CursorIndex;
                    _context._chatInputSelectionStart = result.SelectionStart;
                    break;
                }
                case '\r':
                case '\n':
                    _context.SubmitChatMessage();
                    break;
                default:
                    if (!char.IsControl(character) && _context._chatInput.Length < 120)
                    {
                        var result = _context.InsertTextCharacterAtCursor(
                            _context._chatInput,
                            character,
                            _context._chatInputCursorIndex,
                            _context._chatInputSelectionStart,
                            120);
                        _context._chatInput = result.Text;
                        _context._chatInputCursorIndex = result.CursorIndex;
                        _context._chatInputSelectionStart = result.SelectionStart;
                    }
                    break;
            }

            return true;
        }
}
