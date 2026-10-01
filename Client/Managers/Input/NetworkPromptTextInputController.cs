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

        public TextEditState Edit { get; } = new();

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
                    if (!string.IsNullOrEmpty(Edit.Text))
                    {
                        _context._passwordPromptMessage = "Submitting...";
                        _context._networkClient.SendPassword(Edit.Text);
                    }
                    else
                    {
                        _context._passwordPromptMessage = "Password required.";
                    }
                    break;
                default:
                    if (!char.IsControl(character) && Edit.Text.Length < 32)
                    {
                        var result = _context.InsertTextCharacterAtCursor(
                            Edit.Text,
                            character,
                            Edit.CursorIndex,
                            Edit.SelectionStart,
                            32);
                        Edit.Text = result.Text;
                        Edit.CursorIndex = result.CursorIndex;
                        Edit.SelectionStart = result.SelectionStart;
                        _context._passwordPromptMessage = string.Empty;
                    }
                    break;
            }

            return true;
        }
}
