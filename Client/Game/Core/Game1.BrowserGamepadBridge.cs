#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace OpenGarrison.Client;

/// <summary>
/// The browser has no native gamepad backend in the game's input layer, so the page polls
/// the Gamepad API (navigator.getGamepads, "standard" mapping) every animation frame and
/// hands the first connected pad here. The controller code reads it as player one.
/// </summary>
public static class BrowserGamepadBridge
{
    // Bits of the packed button mask, in the Gamepad API's standard-mapping order.
    private const int ButtonA = 1 << 0;
    private const int ButtonB = 1 << 1;
    private const int ButtonX = 1 << 2;
    private const int ButtonY = 1 << 3;
    private const int ButtonLeftShoulder = 1 << 4;
    private const int ButtonRightShoulder = 1 << 5;
    private const int ButtonLeftTrigger = 1 << 6;
    private const int ButtonRightTrigger = 1 << 7;
    private const int ButtonBack = 1 << 8;
    private const int ButtonStart = 1 << 9;
    private const int ButtonLeftStick = 1 << 10;
    private const int ButtonRightStick = 1 << 11;
    private const int ButtonDPadUp = 1 << 12;
    private const int ButtonDPadDown = 1 << 13;
    private const int ButtonDPadLeft = 1 << 14;
    private const int ButtonDPadRight = 1 << 15;
    private const int ButtonGuide = 1 << 16;

    private static readonly object Sync = new();
    private static GamePadState _state;
    private static bool _connected;

    /// <summary>
    /// Latest pad state from the page. Sticks are in Gamepad API axes (y down); triggers 0..1.
    /// </summary>
    public static void SetState(bool connected, int buttons, float leftX, float leftY, float rightX, float rightY, float leftTrigger, float rightTrigger)
    {
        lock (Sync)
        {
            _connected = connected;
            if (!connected)
            {
                _state = default;
                return;
            }

            var flags = (Buttons)0;
            void Map(int bit, Buttons button)
            {
                if ((buttons & bit) != 0)
                {
                    flags |= button;
                }
            }

            Map(ButtonA, Buttons.A);
            Map(ButtonB, Buttons.B);
            Map(ButtonX, Buttons.X);
            Map(ButtonY, Buttons.Y);
            Map(ButtonLeftShoulder, Buttons.LeftShoulder);
            Map(ButtonRightShoulder, Buttons.RightShoulder);
            Map(ButtonBack, Buttons.Back);
            Map(ButtonStart, Buttons.Start);
            Map(ButtonLeftStick, Buttons.LeftStick);
            Map(ButtonRightStick, Buttons.RightStick);
            Map(ButtonGuide, Buttons.BigButton);

            // Some pads report triggers only as buttons; count a pressed trigger as fully down.
            var left = MathHelper.Clamp((buttons & ButtonLeftTrigger) != 0 ? MathHelper.Max(leftTrigger, 1f) : leftTrigger, 0f, 1f);
            var right = MathHelper.Clamp((buttons & ButtonRightTrigger) != 0 ? MathHelper.Max(rightTrigger, 1f) : rightTrigger, 0f, 1f);
            static ButtonState Pressed(bool down) => down ? ButtonState.Pressed : ButtonState.Released;
            _state = new GamePadState(
                new GamePadThumbSticks(
                    new Vector2(Clamp(leftX), -Clamp(leftY)),
                    new Vector2(Clamp(rightX), -Clamp(rightY))),
                new GamePadTriggers(left, right),
                new GamePadButtons(flags),
                new GamePadDPad(
                    Pressed((buttons & ButtonDPadUp) != 0),
                    Pressed((buttons & ButtonDPadDown) != 0),
                    Pressed((buttons & ButtonDPadLeft) != 0),
                    Pressed((buttons & ButtonDPadRight) != 0)));
        }
    }

    public static bool TryGetState(out GamePadState state)
    {
        lock (Sync)
        {
            state = _state;
            return _connected && state.IsConnected;
        }
    }

    private static float Clamp(float value) => float.IsFinite(value) ? MathHelper.Clamp(value, -1f, 1f) : 0f;
}
