#nullable enable

namespace OpenGarrison.Client;

public sealed class TextEditState
{
    public TextEditState(string text = "")
    {
        Text = text;
    }

    public string Text { get; set; }

    public int CursorIndex { get; set; }

    public int SelectionStart { get; set; }
}
