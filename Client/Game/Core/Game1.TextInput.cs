#nullable enable

using Microsoft.Xna.Framework;

namespace OpenGarrison.Client;

public partial class Game1
{
    public void OnWindowTextInput(object? sender, TextInputEventArgs e)
    {
        _inputManager.WindowTextInput.Handle(e);
    }

    public void HandleBrowserTextInput(char character)
    {
        _inputManager.WindowTextInput.Handle(character);
    }
}
