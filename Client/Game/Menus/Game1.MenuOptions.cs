#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using OpenGarrison.Client.Plugins;

namespace OpenGarrison.Client;

public partial class Game1
{
    private List<(string Label, string Value, Action OnClick)> GetControlsBindingOptions()
    {
        var bindings = GetControlsMenuBindings();
        var items = new List<(string Label, string Value, Action OnClick)>(bindings.Count);
        foreach (var (binding, label, key) in bindings)
        {
            items.Add((
                Label: label,
                Value: key.ToString(),
                OnClick: () => BeginControlsRebind(binding)));
        }

        return items;
    }

    private void BeginControlsRebind(ControlsMenuBinding binding)
    {
        _pendingControlsBinding = binding;
        _menuStatusMessage = $"Press a key to bind {GetControlsBindingLabel(binding)}.";
    }
}
