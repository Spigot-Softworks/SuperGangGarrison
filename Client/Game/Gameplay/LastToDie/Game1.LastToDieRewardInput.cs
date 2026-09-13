using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace OpenGarrison.Client;

public partial class Game1
{
    private readonly LastToDieRewardInput _localRewardInput = new();
    private readonly LastToDieRewardInput _hostedRewardInput = new();
    private ulong _rewardInputCommandId;

    private static Rectangle GetLastToDieRewardConfirmBounds(LastToDieChoiceMenuLayout layout)
        => new(layout.Panel.Right - 188, layout.Panel.Y + 90, 160, 32);

    private bool UpdateLastToDieRewardInput(LastToDieRewardInput input,
        LastToDieChoiceMenuLayout layout, KeyboardState keyboard, MouseState mouse,
        System.Func<int, bool> selectable)
    {
        var released = mouse.LeftButton == ButtonState.Released
            && mouse.RightButton == ButtonState.Released
            && keyboard.IsKeyUp(Keys.Enter) && keyboard.IsKeyUp(Keys.Space)
            && keyboard.IsKeyUp(Keys.D1) && keyboard.IsKeyUp(Keys.D2) && keyboard.IsKeyUp(Keys.D3)
            && keyboard.IsKeyUp(Keys.NumPad1) && keyboard.IsKeyUp(Keys.NumPad2) && keyboard.IsKeyUp(Keys.NumPad3);
        if (!input.Update(System.Environment.TickCount64, released)) return false;

        var clicked = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton != ButtonState.Pressed;
        // Confirmation is checked before selection: one frame cannot do both.
        if ((clicked && GetLastToDieRewardConfirmBounds(layout).Contains(mouse.Position))
            || IsKeyPressed(keyboard, Keys.Enter))
            return input.TrySubmit();

        var index = -1;
        if (!TryGetLastToDieChoiceHotkeySelection(keyboard, layout.CardBounds.Length, out index) && clicked)
            index = GetLastToDieChoiceHoverIndex(mouse.Position, layout);
        if (index >= 0 && selectable(index)) input.Select(index);
        return false;
    }

    private void DrawLastToDieRewardConfirm(LastToDieRewardInput input, LastToDieChoiceMenuLayout layout)
    {
        var bounds = GetLastToDieRewardConfirmBounds(layout);
        var enabled = input.Ready && input.SelectedIndex >= 0;
        _spriteBatch.Draw(_pixel, bounds, enabled ? new Color(130, 38, 38) : new Color(50, 50, 50));
        DrawHudTextCentered(input.Submitted ? "Confirming..." : "Confirm",
            new Vector2(bounds.Center.X, bounds.Y + 8), enabled ? Color.White : Color.Gray, 0.85f);
    }
}
