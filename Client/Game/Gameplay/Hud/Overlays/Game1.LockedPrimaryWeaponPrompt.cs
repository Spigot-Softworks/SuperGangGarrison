#nullable enable

using Microsoft.Xna.Framework;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void DrawLockedPrimaryWeaponSwapPrompt(Vector2 cameraPosition)
    {
        var player = _world.LocalPlayer;
        if (IsLocalSpectatorPresentationActive()
            || !player.IsAlive
            || !player.HasAlternatePrimaryWeapons
            || !_world.IsNearPrimaryWeaponSwapStation(player))
        {
            return;
        }

        var alternateWeapon = player.GetNextGameplayPrimaryItemDisplayName();
        if (string.IsNullOrWhiteSpace(alternateWeapon))
        {
            return;
        }

        var prompt = $"Press {GetSwapWeaponsBindingLabel()} to swap to {alternateWeapon}";
        var renderPosition = GetRenderPosition(player);
        var screenPosition = new Vector2(
            renderPosition.X - cameraPosition.X,
            renderPosition.Y - cameraPosition.Y - 42f);
        var textWidth = MeasureBitmapFontWidth(prompt, 1f);
        var centeredPosition = new Vector2(screenPosition.X - (textWidth / 2f), screenPosition.Y);

        DrawBitmapFontText(prompt, centeredPosition + new Vector2(2f, 2f), Color.Black * 0.9f);
        DrawBitmapFontText(prompt, centeredPosition, Color.White);
    }
}
