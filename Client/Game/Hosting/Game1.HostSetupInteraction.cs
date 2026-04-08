#nullable enable

using Microsoft.Xna.Framework.Input;
using System;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void UpdateHostSetupMenu(MouseState mouse)
    {
        _hostSetupFlowController.UpdateHostSetupMenu(mouse);
    }

    private void CloseHostSetupMenuFromBackAction()
    {
        _hostSetupFlowController.CloseHostSetupMenuFromBackAction();
    }
}
