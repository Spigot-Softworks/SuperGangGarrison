#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;

namespace OpenGarrison.Client;

public partial class Game1
{
    private List<(ControlsMenuBinding Binding, string Label, InputBinding Input)> GetControlsMenuBindings()
    {
        var bubbleMenuBindingPrefix = GetBubbleMenuBindingPrefix();
        return
        [
            (ControlsMenuBinding.MoveUp, "Jump:", _inputBindings.MoveUp),
            (ControlsMenuBinding.MoveLeft, "Move Left:", _inputBindings.MoveLeft),
            (ControlsMenuBinding.MoveRight, "Move Right:", _inputBindings.MoveRight),
            (ControlsMenuBinding.MoveDown, "Move Down:", _inputBindings.MoveDown),
            (ControlsMenuBinding.Taunt, "Taunt:", _inputBindings.Taunt),
            (ControlsMenuBinding.CallMedic, "Call Medic:", _inputBindings.CallMedic),
            (ControlsMenuBinding.UseAbility, "Utility Ability:", _inputBindings.UseAbility),
            (ControlsMenuBinding.SwapWeaponsCustom, "Swap Weapons Custom:", _inputBindings.SwapWeaponsCustomKey),
            (ControlsMenuBinding.InteractWeapon, "Interact Weapon:", _inputBindings.InteractWeapon),
            (ControlsMenuBinding.ChangeTeam, "Change Team:", _inputBindings.ChangeTeam),
            (ControlsMenuBinding.ChangeClass, "Change Class:", _inputBindings.ChangeClass),
            (ControlsMenuBinding.ShowScoreboard, "Show Scores:", _inputBindings.ShowScoreboard),
            (ControlsMenuBinding.ToggleConsole, "Console:", _inputBindings.ToggleConsole),
            (ControlsMenuBinding.OpenBubbleMenuZ, $"{bubbleMenuBindingPrefix} Z:", _inputBindings.OpenBubbleMenuZ),
            (ControlsMenuBinding.OpenBubbleMenuX, $"{bubbleMenuBindingPrefix} X:", _inputBindings.OpenBubbleMenuX),
            (ControlsMenuBinding.OpenBubbleMenuC, $"{bubbleMenuBindingPrefix} C:", _inputBindings.OpenBubbleMenuC),
            (ControlsMenuBinding.CustomBubble, "Custom Bubble:", _inputBindings.CustomBubble),
        ];
    }

    private void ApplyControlsBinding(ControlsMenuBinding binding, InputBinding input)
    {
        switch (binding)
        {
            case ControlsMenuBinding.MoveUp:
                _inputBindings.MoveUp = input;
                break;
            case ControlsMenuBinding.MoveLeft:
                _inputBindings.MoveLeft = input;
                break;
            case ControlsMenuBinding.MoveRight:
                _inputBindings.MoveRight = input;
                break;
            case ControlsMenuBinding.MoveDown:
                _inputBindings.MoveDown = input;
                break;
            case ControlsMenuBinding.Taunt:
                _inputBindings.Taunt = input;
                break;
            case ControlsMenuBinding.CallMedic:
                _inputBindings.CallMedic = input;
                break;
            case ControlsMenuBinding.UseAbility:
                _inputBindings.UseAbility = input;
                break;
            case ControlsMenuBinding.SwapWeaponsCustom:
                _inputBindings.SwapWeaponsCustomKey = input;
                _inputBindings.SwapWeaponsBinding = WeaponSwapBindingMode.Custom;
                break;
            case ControlsMenuBinding.InteractWeapon:
                _inputBindings.InteractWeapon = input;
                break;
            case ControlsMenuBinding.ChangeTeam:
                _inputBindings.ChangeTeam = input;
                break;
            case ControlsMenuBinding.ChangeClass:
                _inputBindings.ChangeClass = input;
                break;
            case ControlsMenuBinding.ShowScoreboard:
                _inputBindings.ShowScoreboard = input;
                break;
            case ControlsMenuBinding.ToggleConsole:
                _inputBindings.ToggleConsole = input;
                break;
            case ControlsMenuBinding.OpenBubbleMenuZ:
                _inputBindings.OpenBubbleMenuZ = input;
                break;
            case ControlsMenuBinding.OpenBubbleMenuX:
                _inputBindings.OpenBubbleMenuX = input;
                break;
            case ControlsMenuBinding.OpenBubbleMenuC:
                _inputBindings.OpenBubbleMenuC = input;
                break;
            case ControlsMenuBinding.CustomBubble:
                _inputBindings.CustomBubble = input;
                break;
        }
    }

    private string GetControlsBindingLabel(ControlsMenuBinding binding)
    {
        var bubbleMenuBindingPrefix = GetBubbleMenuBindingPrefix();
        return binding switch
        {
            ControlsMenuBinding.MoveUp => "Jump",
            ControlsMenuBinding.MoveLeft => "Move Left",
            ControlsMenuBinding.MoveRight => "Move Right",
            ControlsMenuBinding.MoveDown => "Move Down",
            ControlsMenuBinding.Taunt => "Taunt",
            ControlsMenuBinding.CallMedic => "Call Medic",
            ControlsMenuBinding.UseAbility => "Utility Ability",
            ControlsMenuBinding.SwapWeaponsCustom => "Swap Weapons Custom",
            ControlsMenuBinding.InteractWeapon => "Interact Weapon",
            ControlsMenuBinding.ChangeTeam => "Change Team",
            ControlsMenuBinding.ChangeClass => "Change Class",
            ControlsMenuBinding.ShowScoreboard => "Show Scores",
            ControlsMenuBinding.ToggleConsole => "Console",
            ControlsMenuBinding.OpenBubbleMenuZ => $"{bubbleMenuBindingPrefix} Z",
            ControlsMenuBinding.OpenBubbleMenuX => $"{bubbleMenuBindingPrefix} X",
            ControlsMenuBinding.OpenBubbleMenuC => $"{bubbleMenuBindingPrefix} C",
            ControlsMenuBinding.CustomBubble => "Custom Bubble",
            _ => "Binding",
        };
    }

    private string GetBubbleMenuBindingPrefix()
    {
        return HasClientPluginBubbleMenuOverride()
            ? "Bubble Wheel"
            : "Bubble Menu";
    }

    private static string GetBindingDisplayName(InputBinding input)
    {
        return input.Kind switch
        {
            InputBindingKind.Keyboard => GetKeyBindingDisplayName(input.Key),
            InputBindingKind.Mouse => GetMouseBindingDisplayName(input.MouseButton),
            _ => "Unbound",
        };
    }

    private static string GetKeyBindingDisplayName(Keys key)
    {
        return key switch
        {
            Keys.LeftShift => "LShift",
            Keys.RightShift => "RShift",
            Keys.LeftControl => "LCtrl",
            Keys.RightControl => "RCtrl",
            Keys.LeftAlt => "LAlt",
            Keys.RightAlt => "RAlt",
            Keys.OemTilde => "~",
            Keys.OemComma => ",",
            Keys.OemPeriod => ".",
            Keys.OemQuestion => "/",
            Keys.OemSemicolon => ";",
            Keys.OemQuotes => "'",
            Keys.OemOpenBrackets => "[",
            Keys.OemCloseBrackets => "]",
            Keys.OemPipe => "\\",
            Keys.Space => "Space",
            Keys.PageUp => "PgUp",
            Keys.PageDown => "PgDn",
            _ => key.ToString(),
        };
    }

    private static string GetMouseBindingDisplayName(InputMouseButton button)
    {
        return button switch
        {
            InputMouseButton.Left => "Mouse 1",
            InputMouseButton.Right => "Mouse 2",
            InputMouseButton.Middle => "Mouse 3",
            InputMouseButton.XButton1 => "Mouse 4",
            InputMouseButton.XButton2 => "Mouse 5",
            _ => "Mouse",
        };
    }
}
