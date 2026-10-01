#nullable enable

using Microsoft.Xna.Framework;

using static OpenGarrison.Client.Game1;

namespace OpenGarrison.Client;

public sealed class ConsoleTextInputController
    {
        private readonly IInputContext _context;

        public ConsoleTextInputController(IInputContext context)
        {
            _context = context;
        }

        public TextEditState Edit { get; } = new();

        public void Handle(TextInputEventArgs e)
        {
            Handle(e.Character);
        }

        public void Handle(char character)
        {
            if (!_context._consoleOpen)
            {
                return;
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
                    _context.ExecuteConsoleCommand();
                    break;
                case '`':
                case '~':
                    break;
                default:
                    if (!char.IsControl(character))
                    {
                        var result = _context.InsertTextCharacterAtCursor(
                            Edit.Text,
                            character,
                            Edit.CursorIndex,
                            Edit.SelectionStart,
                            int.MaxValue);
                        Edit.Text = result.Text;
                        Edit.CursorIndex = result.CursorIndex;
                        Edit.SelectionStart = result.SelectionStart;
                    }
                    break;
            }
        }
}
