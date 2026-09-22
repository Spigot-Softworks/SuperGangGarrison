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

    private Rectangle GetLastToDieRewardRerollBounds(LastToDieChoiceMenuLayout layout, int index)
    {
        var card = layout.CardBounds[index];
        const float textScale = 1f;
        var width = Math.Max(74, (int)MathF.Ceiling(MeasureBitmapFontWidth("Reroll", textScale)) + 20);
        return new Rectangle(card.Center.X - (width / 2), card.Bottom + 10, width, 28);
    }

    private bool UpdateLastToDieRewardInput(LastToDieRewardInput input,
        LastToDieChoiceMenuLayout layout, KeyboardState keyboard, MouseState mouse,
        System.Func<int, bool> selectable,
        System.Action<int>? reroll = null,
        System.Func<int, bool>? rerollAvailable = null)
    {
        var released = mouse.LeftButton == ButtonState.Released
            && mouse.RightButton == ButtonState.Released
            && keyboard.IsKeyUp(Keys.Enter) && keyboard.IsKeyUp(Keys.Space)
            && keyboard.IsKeyUp(Keys.D1) && keyboard.IsKeyUp(Keys.D2) && keyboard.IsKeyUp(Keys.D3)
            && keyboard.IsKeyUp(Keys.NumPad1) && keyboard.IsKeyUp(Keys.NumPad2) && keyboard.IsKeyUp(Keys.NumPad3);
        if (!input.Update(System.Environment.TickCount64, released)) return false;

        var clicked = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton != ButtonState.Pressed;
        if (clicked && reroll is not null && !input.Submitted)
        {
            for (var rerollIndex = 0; rerollIndex < layout.CardBounds.Length; rerollIndex += 1)
            {
                if (GetLastToDieRewardRerollBounds(layout, rerollIndex).Contains(mouse.Position))
                {
                    if (rerollAvailable is not null && !rerollAvailable(rerollIndex))
                    {
                        return false;
                    }
                    input.ClearSelection();
                    reroll(rerollIndex);
                    return false;
                }
            }
        }

        // Confirmation is checked before selection: one frame cannot do both.
        if ((clicked && GetLastToDieRewardConfirmBounds(layout).Contains(mouse.Position))
            || IsKeyPressed(keyboard, Keys.Enter))
        {
            if (!input.TrySubmit()) return false;
            TryPlaySound(_runtimeAssets?.GetSound("UberChargedSnd"), 1f, 0f, 0f);
            return true;
        }

        var index = -1;
        if (!TryGetLastToDieChoiceHotkeySelection(keyboard, layout.CardBounds.Length, out index) && clicked)
            index = GetLastToDieChoiceHoverIndex(mouse.Position, layout);
        if (index >= 0 && selectable(index)) input.Select(index);
        return false;
    }

    private void DrawLastToDieRewardReroll(LastToDieChoiceMenuLayout layout, int index, bool enabled)
    {
        var bounds = GetLastToDieRewardRerollBounds(layout, index);
        _spriteBatch.Draw(_pixel, bounds, enabled ? new Color(92, 38, 38) : new Color(50, 50, 50));
        var textPosition = new Vector2(
            MathF.Round(bounds.Center.X - (MeasureBitmapFontWidth("Reroll", 1f) * 0.5f)),
            MathF.Round(bounds.Y + ((bounds.Height - MeasureBitmapFontHeight(1f)) * 0.5f)));
        DrawBitmapFontText("Reroll", textPosition, enabled ? Color.White : Color.Gray, 1f);
    }

    private void DrawLastToDieRewardConfirm(LastToDieRewardInput input, LastToDieChoiceMenuLayout layout)
    {
        var bounds = GetLastToDieRewardConfirmBounds(layout);
        var enabled = input.Ready && input.SelectedIndex >= 0;
        _spriteBatch.Draw(_pixel, bounds, enabled ? new Color(130, 38, 38) : new Color(50, 50, 50));
        DrawHudTextCentered(input.Submitted ? "Confirming..." : "Confirm",
            bounds.Center.ToVector2(), enabled ? Color.White : Color.Gray, 1f);
    }
}
