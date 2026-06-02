#nullable enable

using Microsoft.Xna.Framework.Input;

namespace OpenGarrison.Client;

internal static class InputBindingInput
{
    public static bool IsDown(InputBinding binding, KeyboardState keyboard, MouseState mouse)
    {
        return binding.Kind switch
        {
            InputBindingKind.Keyboard => keyboard.IsKeyDown(binding.Key),
            InputBindingKind.Mouse => IsMouseButtonDown(binding.MouseButton, mouse),
            _ => false,
        };
    }

    public static bool IsPressed(
        InputBinding binding,
        KeyboardState keyboard,
        KeyboardState previousKeyboard,
        MouseState mouse,
        MouseState previousMouse)
    {
        return binding.Kind switch
        {
            InputBindingKind.Keyboard => keyboard.IsKeyDown(binding.Key) && !previousKeyboard.IsKeyDown(binding.Key),
            InputBindingKind.Mouse => IsMouseButtonDown(binding.MouseButton, mouse) && !IsMouseButtonDown(binding.MouseButton, previousMouse),
            _ => false,
        };
    }

    public static bool TryGetPressedBindableMouseButton(MouseState mouse, MouseState previousMouse, out InputBinding binding)
    {
        if (IsMouseButtonDown(InputMouseButton.Middle, mouse) && !IsMouseButtonDown(InputMouseButton.Middle, previousMouse))
        {
            binding = InputBinding.FromMouse(InputMouseButton.Middle);
            return true;
        }

        if (IsMouseButtonDown(InputMouseButton.XButton1, mouse) && !IsMouseButtonDown(InputMouseButton.XButton1, previousMouse))
        {
            binding = InputBinding.FromMouse(InputMouseButton.XButton1);
            return true;
        }

        if (IsMouseButtonDown(InputMouseButton.XButton2, mouse) && !IsMouseButtonDown(InputMouseButton.XButton2, previousMouse))
        {
            binding = InputBinding.FromMouse(InputMouseButton.XButton2);
            return true;
        }

        binding = default;
        return false;
    }

    public static bool IsMouseButtonDown(InputMouseButton button, MouseState mouse)
    {
        return button switch
        {
            InputMouseButton.Left => mouse.LeftButton == ButtonState.Pressed,
            InputMouseButton.Middle => mouse.MiddleButton == ButtonState.Pressed,
            InputMouseButton.Right => mouse.RightButton == ButtonState.Pressed,
            InputMouseButton.XButton1 => mouse.XButton1 == ButtonState.Pressed,
            InputMouseButton.XButton2 => mouse.XButton2 == ButtonState.Pressed,
            _ => false,
        };
    }
}
