#nullable enable

using Microsoft.Xna.Framework.Input;

namespace OpenGarrison.Client;

public static class BrowserInputBridge
{
    private static readonly object Sync = new();
    private static readonly HashSet<Keys> PressedKeys = [];
    private static readonly Queue<char> PendingTextInput = [];
    private static int _x;
    private static int _y;
    private static int _sampledX;
    private static int _sampledY;
    private static int _wheel;
    private static int _pendingWheelDelta;
    private static ButtonState _leftButton;
    private static ButtonState _middleButton;
    private static ButtonState _rightButton;
    private static ButtonState _xButton1;
    private static ButtonState _xButton2;
    private static bool _leftButtonPressPending;
    private static bool _middleButtonPressPending;
    private static bool _rightButtonPressPending;
    private static bool _xButton1PressPending;
    private static bool _xButton2PressPending;
    private static ButtonState _sampledLeftButton;
    private static ButtonState _sampledMiddleButton;
    private static ButtonState _sampledRightButton;
    private static ButtonState _sampledXButton1;
    private static ButtonState _sampledXButton2;
    private static bool _focused;
    private static bool _userActivationObserved;

    public static void SetFocus(bool focused)
    {
        lock (Sync)
        {
            _focused = focused;
            if (!focused)
            {
                PressedKeys.Clear();
                PendingTextInput.Clear();
                _leftButton = ButtonState.Released;
                _middleButton = ButtonState.Released;
                _rightButton = ButtonState.Released;
                _xButton1 = ButtonState.Released;
                _xButton2 = ButtonState.Released;
                _leftButtonPressPending = false;
                _middleButtonPressPending = false;
                _rightButtonPressPending = false;
                _xButton1PressPending = false;
                _xButton2PressPending = false;
                _sampledLeftButton = ButtonState.Released;
                _sampledMiddleButton = ButtonState.Released;
                _sampledRightButton = ButtonState.Released;
                _sampledXButton1 = ButtonState.Released;
                _sampledXButton2 = ButtonState.Released;
            }
        }
    }

    public static void SetMousePosition(int x, int y)
    {
        lock (Sync)
        {
            _x = x;
            _y = y;
        }
    }

    public static void SetMouseButton(int button, bool pressed)
    {
        var state = pressed ? ButtonState.Pressed : ButtonState.Released;
        lock (Sync)
        {
            switch (button)
            {
                case 0:
                    SetButtonState(ref _leftButton, ref _leftButtonPressPending, state);
                    break;
                case 1:
                    SetButtonState(ref _middleButton, ref _middleButtonPressPending, state);
                    break;
                case 2:
                    SetButtonState(ref _rightButton, ref _rightButtonPressPending, state);
                    break;
                case 3:
                    SetButtonState(ref _xButton1, ref _xButton1PressPending, state);
                    break;
                case 4:
                    SetButtonState(ref _xButton2, ref _xButton2PressPending, state);
                    break;
            }
        }
    }

    public static void AddWheelDelta(int delta)
    {
        lock (Sync)
        {
            _pendingWheelDelta += delta;
        }
    }

    public static void SetKey(Keys key, bool pressed)
    {
        lock (Sync)
        {
            if (pressed)
            {
                _userActivationObserved = true;
                PressedKeys.Add(key);
            }
            else
            {
                PressedKeys.Remove(key);
            }
        }
    }

    public static void EnqueueTextInput(char character)
    {
        lock (Sync)
        {
            PendingTextInput.Enqueue(character);
        }
    }

    public static bool IsFocused
    {
        get
        {
            lock (Sync)
            {
                return _focused;
            }
        }
    }

    public static bool HasUserActivation
    {
        get
        {
            lock (Sync)
            {
                return _userActivationObserved;
            }
        }
    }

    public static KeyboardState GetKeyboardState()
    {
        lock (Sync)
        {
            return new KeyboardState(PressedKeys.ToArray());
        }
    }

    public static string[] GetPressedKeyNamesSnapshot()
    {
        lock (Sync)
        {
            var keys = PressedKeys.Select(static key => key.ToString()).ToArray();
            Array.Sort(keys, StringComparer.Ordinal);
            return keys;
        }
    }

    public static void BeginFrame()
    {
        lock (Sync)
        {
            _sampledX = _x;
            _sampledY = _y;
            _wheel += _pendingWheelDelta;
            _pendingWheelDelta = 0;
            _sampledLeftButton = SampleButtonState(_leftButton, ref _leftButtonPressPending);
            _sampledMiddleButton = SampleButtonState(_middleButton, ref _middleButtonPressPending);
            _sampledRightButton = SampleButtonState(_rightButton, ref _rightButtonPressPending);
            _sampledXButton1 = SampleButtonState(_xButton1, ref _xButton1PressPending);
            _sampledXButton2 = SampleButtonState(_xButton2, ref _xButton2PressPending);
        }
    }

    public static MouseState GetMouseState()
    {
        lock (Sync)
        {
            return new MouseState(_sampledX, _sampledY, _wheel, _sampledLeftButton, _sampledMiddleButton, _sampledRightButton, _sampledXButton1, _sampledXButton2);
        }
    }

    public static char[] DrainTextInput()
    {
        lock (Sync)
        {
            if (PendingTextInput.Count == 0)
            {
                return [];
            }

            var characters = PendingTextInput.ToArray();
            PendingTextInput.Clear();
            return characters;
        }
    }

    private static void SetButtonState(ref ButtonState currentState, ref bool pressPending, ButtonState nextState)
    {
        if (currentState == nextState)
        {
            return;
        }

        currentState = nextState;
        if (nextState == ButtonState.Pressed)
        {
            _userActivationObserved = true;
            pressPending = true;
        }
    }

    private static ButtonState SampleButtonState(ButtonState currentState, ref bool pressPending)
    {
        if (pressPending)
        {
            pressPending = false;
            return ButtonState.Pressed;
        }

        return currentState;
    }
}
