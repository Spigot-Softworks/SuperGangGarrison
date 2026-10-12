#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private bool _builderBloodPaletteOpen;
    private Rectangle _builderBloodPaletteAnchor;

    private Rectangle GetGarrisonBuilderBloodPaletteBounds()
    {
        var rows = (GarrisonBuilderLightPalette.Length + 5) / 6;
        var width = 6 * 26 + 12;
        var height = rows * 26 + 58;
        return new Rectangle(Math.Clamp(_builderBloodPaletteAnchor.Right - width, 4, Math.Max(4, BuilderViewportWidth - width - 4)),
            Math.Clamp(_builderBloodPaletteAnchor.Bottom + 2, 4, Math.Max(4, BuilderViewportHeight - height - 4)), width, height);
    }

    private static Rectangle GetGarrisonBuilderBloodPaletteCell(Rectangle panel, int index) =>
        new(panel.X + 6 + index % 6 * 26, panel.Y + 52 + index / 6 * 26, 23, 23);

    private bool UpdateGarrisonBuilderBloodPalette(KeyboardState keyboard, MouseState mouse)
    {
        if (!_builderBloodPaletteOpen) return false;
        if (IsKeyPressed(keyboard, Keys.Escape)) { _builderBloodPaletteOpen = false; return true; }
        if (mouse.LeftButton != ButtonState.Pressed || _previousMouse.LeftButton != ButtonState.Released) return true;
        var panel = GetGarrisonBuilderBloodPaletteBounds();
        if (!panel.Contains(mouse.Position)) { _builderBloodPaletteOpen = false; return true; }
        var original = new Rectangle(panel.X + 6, panel.Y + 24, 75, 22);
        var team = new Rectangle(original.Right + 4, original.Y, 75, 22);
        if (original.Contains(mouse.Position) || team.Contains(mouse.Position))
        {
            _builderLightingDraft = _builderLightingDraft with { BloodColor = null, BloodTeamColor = team.Contains(mouse.Position) };
            _builderBloodPaletteOpen = false;
            return true;
        }
        for (var index = 0; index < GarrisonBuilderLightPalette.Length; index++)
        {
            if (!GetGarrisonBuilderBloodPaletteCell(panel, index).Contains(mouse.Position)) continue;
            MapLightingColor.TryParse(GarrisonBuilderLightPalette[index].Hex, out var color);
            _builderLightingDraft = _builderLightingDraft with { BloodColor = color, BloodTeamColor = false };
            _builderBloodPaletteOpen = false;
            break;
        }
        return true;
    }

    private void DrawGarrisonBuilderBloodPalette(MouseState mouse)
    {
        if (!_builderBloodPaletteOpen) return;
        var panel = GetGarrisonBuilderBloodPaletteBounds();
        DrawMenuPanelBackdrop(panel, 0.98f);
        DrawBitmapFontText("Blood color", new Vector2(panel.X + 6, panel.Y + 4), Color.White, 1f);
        var original = new Rectangle(panel.X + 6, panel.Y + 24, 75, 22);
        var team = new Rectangle(original.Right + 4, original.Y, 75, 22);
        DrawBuilderMenuButton(original, "Original", original.Contains(mouse.Position));
        DrawBuilderMenuButton(team, "Team color", team.Contains(mouse.Position));
        for (var index = 0; index < GarrisonBuilderLightPalette.Length; index++)
        {
            MapLightingColor.TryParse(GarrisonBuilderLightPalette[index].Hex, out var color);
            var cell = GetGarrisonBuilderBloodPaletteCell(panel, index);
            _spriteBatch.Draw(_pixel, cell, new Color(color.R, color.G, color.B));
            if (cell.Contains(mouse.Position))
            {
                _spriteBatch.Draw(_pixel, new Rectangle(cell.X, cell.Y, cell.Width, 1), Color.White);
                _spriteBatch.Draw(_pixel, new Rectangle(cell.X, cell.Bottom - 1, cell.Width, 1), Color.White);
                _spriteBatch.Draw(_pixel, new Rectangle(cell.X, cell.Y, 1, cell.Height), Color.White);
                _spriteBatch.Draw(_pixel, new Rectangle(cell.Right - 1, cell.Y, 1, cell.Height), Color.White);
            }
        }
    }
}
