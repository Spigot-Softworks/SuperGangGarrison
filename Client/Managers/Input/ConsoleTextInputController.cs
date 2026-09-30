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
                        _context._consoleInput,
                        _context._consoleInputCursorIndex,
                        _context._consoleInputSelectionStart);
                    _context._consoleInput = result.Text;
                    _context._consoleInputCursorIndex = result.CursorIndex;
                    _context._consoleInputSelectionStart = result.SelectionStart;
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
                            _context._consoleInput,
                            character,
                            _context._consoleInputCursorIndex,
                            _context._consoleInputSelectionStart,
                            int.MaxValue);
                        _context._consoleInput = result.Text;
                        _context._consoleInputCursorIndex = result.CursorIndex;
                        _context._consoleInputSelectionStart = result.SelectionStart;
                    }
                    break;
            }
        }
}
