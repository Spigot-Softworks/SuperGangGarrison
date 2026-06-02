#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;
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

    private List<(ControllerControlsMenuBinding Binding, string Label, ControllerButtonBinding Input)> GetControllerControlsMenuBindings()
    {
        return
        [
            (ControllerControlsMenuBinding.Jump, "Jump:", _clientSettings.ControllerJumpButton),
            (ControllerControlsMenuBinding.PrimaryFire, "Primary Fire:", _clientSettings.ControllerPrimaryFireButton),
            (ControllerControlsMenuBinding.SecondaryFire, "Secondary Fire:", _clientSettings.ControllerSecondaryFireButton),
            (ControllerControlsMenuBinding.UseAbility, "Utility Ability:", _clientSettings.ControllerUseAbilityButton),
            (ControllerControlsMenuBinding.Interact, "Interact:", _clientSettings.ControllerInteractButton),
            (ControllerControlsMenuBinding.SwapWeapon, "Swap Weapon:", _clientSettings.ControllerSwapWeaponButton),
            (ControllerControlsMenuBinding.Scoreboard, "Show Scores:", _clientSettings.ControllerScoreboardButton),
            (ControllerControlsMenuBinding.Pause, "Pause/Menu:", _clientSettings.ControllerPauseButton),
            (ControllerControlsMenuBinding.AimDistance, "Aim Distance:", _clientSettings.ControllerAimDistanceButton),
            (ControllerControlsMenuBinding.ChangeTeam, "Change Team:", _clientSettings.ControllerChangeTeamButton),
            (ControllerControlsMenuBinding.ChangeClass, "Change Class:", _clientSettings.ControllerChangeClassButton),
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

    private void ApplyControllerControlsBinding(ControllerControlsMenuBinding binding, ControllerButtonBinding input)
    {
        input = OpenGarrisonPreferencesDocument.NormalizeControllerButtonBinding(input);
        switch (binding)
        {
            case ControllerControlsMenuBinding.Jump:
                _clientSettings.ControllerJumpButton = input;
                break;
            case ControllerControlsMenuBinding.PrimaryFire:
                _clientSettings.ControllerPrimaryFireButton = input;
                break;
            case ControllerControlsMenuBinding.SecondaryFire:
                _clientSettings.ControllerSecondaryFireButton = input;
                break;
            case ControllerControlsMenuBinding.UseAbility:
                _clientSettings.ControllerUseAbilityButton = input;
                break;
            case ControllerControlsMenuBinding.Interact:
                _clientSettings.ControllerInteractButton = input;
                break;
            case ControllerControlsMenuBinding.SwapWeapon:
                _clientSettings.ControllerSwapWeaponButton = input;
                break;
            case ControllerControlsMenuBinding.Scoreboard:
                _clientSettings.ControllerScoreboardButton = input;
                break;
            case ControllerControlsMenuBinding.Pause:
                _clientSettings.ControllerPauseButton = input;
                break;
            case ControllerControlsMenuBinding.AimDistance:
                _clientSettings.ControllerAimDistanceButton = input;
                break;
            case ControllerControlsMenuBinding.ChangeTeam:
                _clientSettings.ControllerChangeTeamButton = input;
                break;
            case ControllerControlsMenuBinding.ChangeClass:
                _clientSettings.ControllerChangeClassButton = input;
                break;
        }

        PersistClientSettings();
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

    private static string GetControllerControlsBindingLabel(ControllerControlsMenuBinding binding)
    {
        return binding switch
        {
            ControllerControlsMenuBinding.Jump => "Jump",
            ControllerControlsMenuBinding.PrimaryFire => "Primary Fire",
            ControllerControlsMenuBinding.SecondaryFire => "Secondary Fire",
            ControllerControlsMenuBinding.UseAbility => "Utility Ability",
            ControllerControlsMenuBinding.Interact => "Interact",
            ControllerControlsMenuBinding.SwapWeapon => "Swap Weapon",
            ControllerControlsMenuBinding.Scoreboard => "Show Scores",
            ControllerControlsMenuBinding.Pause => "Pause/Menu",
            ControllerControlsMenuBinding.AimDistance => "Aim Distance",
            ControllerControlsMenuBinding.ChangeTeam => "Change Team",
            ControllerControlsMenuBinding.ChangeClass => "Change Class",
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
