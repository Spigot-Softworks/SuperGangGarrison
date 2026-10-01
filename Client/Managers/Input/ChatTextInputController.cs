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

        public TextEditState Edit { get; } = new();

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
                        Edit.Text,
                        Edit.CursorIndex,
                        Edit.SelectionStart);
                    Edit.Text = result.Text;
                    Edit.CursorIndex = result.CursorIndex;
                    Edit.SelectionStart = result.SelectionStart;
                    break;
                }
                case '\r':
                case '\n':
                    _context.SubmitChatMessage();
                    break;
                default:
                    if (!char.IsControl(character) && Edit.Text.Length < 120)
                    {
                        var result = _context.InsertTextCharacterAtCursor(
                            Edit.Text,
                            character,
                            Edit.CursorIndex,
                            Edit.SelectionStart,
                            120);
                        Edit.Text = result.Text;
                        Edit.CursorIndex = result.CursorIndex;
                        Edit.SelectionStart = result.SelectionStart;
                    }
                    break;
            }

            return true;
        }
}
