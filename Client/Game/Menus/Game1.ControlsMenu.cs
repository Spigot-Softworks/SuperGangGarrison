#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void UpdateControlsMenu(KeyboardState keyboard, MouseState mouse)
    {
        const float xbegin = 40f;
        const float ybegin = 150f;
        const float spacing = 28f;
        const float width = 360f;
        var bindingItems = GetControlsMenuBindings();
        var items = bindingItems.Count + 1;

        if (_pendingControlsBinding.HasValue)
        {
            if (IsKeyPressed(keyboard, Keys.Escape))
            {
                _pendingControlsBinding = null;
                return;
            }

            foreach (var key in keyboard.GetPressedKeys())
            {
                if (_previousKeyboard.IsKeyDown(key))
                {
                    continue;
                }

                ApplyControlsBinding(_pendingControlsBinding.Value, key);
                PersistInputBindings();
                _pendingControlsBinding = null;
                return;
            }

            return;
        }

        if (IsKeyPressed(keyboard, Keys.Escape))
        {
            CloseControlsMenu();
            return;
        }

        if (mouse.X > xbegin && mouse.X < xbegin + width)
        {
            _controlsHoverIndex = (int)MathF.Round((mouse.Y - ybegin) / spacing);
            if (_controlsHoverIndex < 0 || _controlsHoverIndex >= items)
            {
                _controlsHoverIndex = -1;
            }
        }
        else
        {
            _controlsHoverIndex = -1;
        }

        var clickPressed = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton != ButtonState.Pressed;
        if (!clickPressed || _controlsHoverIndex < 0)
        {
            return;
        }

        if (_controlsHoverIndex == bindingItems.Count)
        {
            CloseControlsMenu();
            return;
        }

        _pendingControlsBinding = bindingItems[_controlsHoverIndex].Binding;
    }

    private void DrawControlsMenu()
    {
        var viewportWidth = ViewportWidth;
        var viewportHeight = ViewportHeight;
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black * 0.82f);

        var title = _pendingControlsBinding.HasValue
            ? $"Press a key for {GetControlsBindingLabel(_pendingControlsBinding.Value)}"
            : "Controls";
        DrawBitmapFontText(title, new Vector2(40f, 110f), Color.White, 1.2f);

        var items = GetControlsMenuBindings();
        const float xbegin = 40f;
        const float ybegin = 150f;
        const float spacing = 28f;
        const float width = 360f;
        DrawMenuPanelBackdrop(new Rectangle((int)xbegin - 12, (int)ybegin - 36, (int)width + 44, (items.Count + 2) * (int)spacing), 0.82f);
        DrawMenuPlaqueRows(new Vector2(xbegin, ybegin), items.Count + 1, spacing, width, 0.72f);

        var position = new Vector2(xbegin, ybegin);
        for (var index = 0; index < items.Count; index += 1)
        {
            var item = items[index];
            var color = _pendingControlsBinding == item.Binding
                ? Color.Orange
                : index == _controlsHoverIndex ? Color.Red : Color.White;
            DrawBitmapFontText(item.Label, position, color, 1f);
            DrawBitmapFontText(GetBindingDisplayName(item.Key), new Vector2(280f, position.Y), color, 1f);
            position.Y += spacing;
        }

        var backColor = items.Count == _controlsHoverIndex ? Color.Red : Color.White;
        DrawBitmapFontText("Back", position, backColor, 1f);
    }

    private List<(ControlsMenuBinding Binding, string Label, Keys Key)> GetControlsMenuBindings()
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
            (ControlsMenuBinding.FireSecondaryWeapon, "Secondary Weapon:", _inputBindings.FireSecondaryWeapon),
            (ControlsMenuBinding.InteractWeapon, "Interact Weapon:", _inputBindings.InteractWeapon),
            (ControlsMenuBinding.ChangeTeam, "Change Team:", _inputBindings.ChangeTeam),
            (ControlsMenuBinding.ChangeClass, "Change Class:", _inputBindings.ChangeClass),
            (ControlsMenuBinding.ShowScoreboard, "Show Scores:", _inputBindings.ShowScoreboard),
            (ControlsMenuBinding.ToggleConsole, "Console:", _inputBindings.ToggleConsole),
            (ControlsMenuBinding.OpenBubbleMenuZ, $"{bubbleMenuBindingPrefix} Z:", _inputBindings.OpenBubbleMenuZ),
            (ControlsMenuBinding.OpenBubbleMenuX, $"{bubbleMenuBindingPrefix} X:", _inputBindings.OpenBubbleMenuX),
            (ControlsMenuBinding.OpenBubbleMenuC, $"{bubbleMenuBindingPrefix} C:", _inputBindings.OpenBubbleMenuC),
        ];
    }

    private void ApplyControlsBinding(ControlsMenuBinding binding, Keys key)
    {
        switch (binding)
        {
            case ControlsMenuBinding.MoveUp:
                _inputBindings.MoveUp = key;
                break;
            case ControlsMenuBinding.MoveLeft:
                _inputBindings.MoveLeft = key;
                break;
            case ControlsMenuBinding.MoveRight:
                _inputBindings.MoveRight = key;
                break;
            case ControlsMenuBinding.MoveDown:
                _inputBindings.MoveDown = key;
                break;
            case ControlsMenuBinding.Taunt:
                _inputBindings.Taunt = key;
                break;
            case ControlsMenuBinding.CallMedic:
                _inputBindings.CallMedic = key;
                break;
            case ControlsMenuBinding.FireSecondaryWeapon:
                _inputBindings.FireSecondaryWeapon = key;
                break;
            case ControlsMenuBinding.InteractWeapon:
                _inputBindings.InteractWeapon = key;
                break;
            case ControlsMenuBinding.ChangeTeam:
                _inputBindings.ChangeTeam = key;
                break;
            case ControlsMenuBinding.ChangeClass:
                _inputBindings.ChangeClass = key;
                break;
            case ControlsMenuBinding.ShowScoreboard:
                _inputBindings.ShowScoreboard = key;
                break;
            case ControlsMenuBinding.ToggleConsole:
                _inputBindings.ToggleConsole = key;
                break;
            case ControlsMenuBinding.OpenBubbleMenuZ:
                _inputBindings.OpenBubbleMenuZ = key;
                break;
            case ControlsMenuBinding.OpenBubbleMenuX:
                _inputBindings.OpenBubbleMenuX = key;
                break;
            case ControlsMenuBinding.OpenBubbleMenuC:
                _inputBindings.OpenBubbleMenuC = key;
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
            ControlsMenuBinding.FireSecondaryWeapon => "Secondary Weapon",
            ControlsMenuBinding.InteractWeapon => "Interact Weapon",
            ControlsMenuBinding.ChangeTeam => "Change Team",
            ControlsMenuBinding.ChangeClass => "Change Class",
            ControlsMenuBinding.ShowScoreboard => "Show Scores",
            ControlsMenuBinding.ToggleConsole => "Console",
            ControlsMenuBinding.OpenBubbleMenuZ => $"{bubbleMenuBindingPrefix} Z",
            ControlsMenuBinding.OpenBubbleMenuX => $"{bubbleMenuBindingPrefix} X",
            ControlsMenuBinding.OpenBubbleMenuC => $"{bubbleMenuBindingPrefix} C",
            _ => "Binding",
        };
    }

    private string GetBubbleMenuBindingPrefix()
    {
        return HasClientPluginBubbleMenuOverride()
            ? "Bubble Wheel"
            : "Bubble Menu";
    }

    private static string GetBindingDisplayName(Keys key)
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
}
