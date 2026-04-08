#nullable enable

using Microsoft.Xna.Framework;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void OnWindowTextInput(object? sender, TextInputEventArgs e)
    {
        _windowTextInputController.Handle(e);
    }
}
