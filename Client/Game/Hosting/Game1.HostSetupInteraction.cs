#nullable enable

using Microsoft.Xna.Framework.Input;
using System;

namespace OpenGarrison.Client;

public partial class Game1
{
    public void UpdateHostSetupMenu(MouseState mouse)
    {
        _hostingManager.HostSetup.UpdateHostSetupMenu(mouse);
    }

    private void CloseHostSetupMenuFromBackAction()
    {
        _hostingManager.HostSetup.CloseHostSetupMenuFromBackAction();
    }
}
