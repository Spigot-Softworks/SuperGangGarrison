using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework.Input;

namespace OpenGarrison.Client;

// Discard background input and require controls held during activation to be
// released before they can act. Rebasing edge detectors alone cannot stop held fire.
internal sealed class WindowInputFilter
{
    private bool _active;
    private bool _controllerReady;
    private readonly HashSet<Keys> _blockedKeys = [];
    private int _blockedButtons;
    private int _rawWheel;
    private MouseState _mouse;

    public void LoseFocus()
    {
        _active = false;
        _controllerReady = false;
        _blockedKeys.Clear();
        _blockedButtons = 0;
    }

    public void Filter(bool active, ref KeyboardState keyboard, ref MouseState mouse)
    {
        var buttons = ButtonMask(mouse);
        if (!active)
        {
            LoseFocus();
            keyboard = default;
            _rawWheel = mouse.ScrollWheelValue;
            mouse = _mouse = ReleasedMouse(_mouse);
            return;
        }

        if (!_active)
        {
            _blockedKeys.UnionWith(keyboard.GetPressedKeys());
            _blockedButtons = buttons;
            _rawWheel = mouse.ScrollWheelValue;
        }
        _active = true;
        foreach (var key in _blockedKeys.ToArray())
            if (keyboard.IsKeyUp(key)) _blockedKeys.Remove(key);
        if (_blockedKeys.Count > 0)
            keyboard = new KeyboardState(keyboard.GetPressedKeys().Where(key => !_blockedKeys.Contains(key)).ToArray());
        _blockedButtons &= buttons;
        var allowed = buttons & ~_blockedButtons;
        var wheel = _mouse.ScrollWheelValue + mouse.ScrollWheelValue - _rawWheel;
        _rawWheel = mouse.ScrollWheelValue;
        ButtonState State(int bit) => (allowed & bit) != 0 ? ButtonState.Pressed : ButtonState.Released;
        mouse = _mouse = new MouseState(mouse.X, mouse.Y, wheel, State(1), State(2), State(4), State(8), State(16));
    }

    public GamePadState FilterController(bool active, GamePadState state, bool hasActivity)
    {
        if (!active) _controllerReady = false;
        else if (!hasActivity) _controllerReady = true;
        return active && _controllerReady ? state : default;
    }

    private static MouseState ReleasedMouse(MouseState mouse) => new(mouse.X, mouse.Y, mouse.ScrollWheelValue,
        ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);

    private static int ButtonMask(MouseState mouse) =>
        (mouse.LeftButton == ButtonState.Pressed ? 1 : 0)
        | (mouse.MiddleButton == ButtonState.Pressed ? 2 : 0)
        | (mouse.RightButton == ButtonState.Pressed ? 4 : 0)
        | (mouse.XButton1 == ButtonState.Pressed ? 8 : 0)
        | (mouse.XButton2 == ButtonState.Pressed ? 16 : 0);
}

internal static class DesktopInputFocus
{
    public static bool IsCurrentProcessForeground()
    {
        if (!OperatingSystem.IsWindows()) return true;
        // SDL/MonoGame activation events can lag behind the OS foreground window.
        var window = GetForegroundWindow();
        if (window == IntPtr.Zero) return false;
        _ = GetWindowThreadProcessId(window, out var processId);
        return processId == (uint)Environment.ProcessId;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
}
