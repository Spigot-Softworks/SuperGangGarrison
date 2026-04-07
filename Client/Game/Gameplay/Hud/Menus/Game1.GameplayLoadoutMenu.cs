#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenGarrison.Client;

public partial class Game1
{
    private bool CanOpenGameplayLoadoutMenu()
    {
        if (_networkClient.IsSpectator || _world.LocalPlayerAwaitingJoin)
        {
            return false;
        }

        return GetOrderedGameplayLoadoutsForLocalPlayer().Count > 1;
    }

    private void UpdateGameplayLoadoutMenu(KeyboardState keyboard, MouseState mouse)
    {
        if (!_gameplayLoadoutMenuOpen)
        {
            _gameplayLoadoutMenuHoverIndex = -1;
            return;
        }

        if (_gameplayLoadoutMenuAwaitingEscapeRelease)
        {
            if (!keyboard.IsKeyDown(Keys.Escape))
            {
                _gameplayLoadoutMenuAwaitingEscapeRelease = false;
            }
        }
        else if (IsKeyPressed(keyboard, Keys.Escape))
        {
            CloseGameplayLoadoutMenu();
            OpenInGameMenu();
            return;
        }

        var buttons = BuildGameplayLoadoutMenuButtons();
        _gameplayLoadoutMenuHoverIndex = GetHoveredGameplayLoadoutMenuButtonIndex(mouse, buttons);

        var pressedDigit = GetPressedDigit(keyboard);
        if (pressedDigit.HasValue && pressedDigit.Value >= 1 && pressedDigit.Value <= 9)
        {
            var loadoutIndex = pressedDigit.Value - 1;
            var loadouts = GetOrderedGameplayLoadoutsForLocalPlayer();
            if (loadoutIndex < loadouts.Count)
            {
                ApplyGameplayLoadoutSelection(loadoutIndex, loadouts[loadoutIndex].Id);
                return;
            }
        }

        var clickPressed = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton != ButtonState.Pressed;
        if (!clickPressed || _gameplayLoadoutMenuHoverIndex < 0 || _gameplayLoadoutMenuHoverIndex >= buttons.Count)
        {
            return;
        }

        buttons[_gameplayLoadoutMenuHoverIndex].Activate();
    }

    private void DrawGameplayLoadoutMenu()
    {
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, ViewportWidth, ViewportHeight), Color.Black * 0.74f);

        var loadouts = GetOrderedGameplayLoadoutsForLocalPlayer();
        if (loadouts.Count == 0)
        {
            return;
        }

        var layout = GetCenteredPlaqueMenuLayout(tall: loadouts.Count > 4, stackedButtonCount: loadouts.Count, includeBottomBarButton: false);
        var stackedActions = loadouts
            .Select(static loadout => new MenuPageAction(loadout.DisplayName, static () => { }))
            .ToArray();
        var hoveredStackedIndex = _gameplayLoadoutMenuHoverIndex >= 0 && _gameplayLoadoutMenuHoverIndex < loadouts.Count
            ? _gameplayLoadoutMenuHoverIndex
            : -1;
        var soloHovered = _gameplayLoadoutMenuHoverIndex == loadouts.Count;
        DrawPlaqueMenuLayout(
            layout,
            stackedActions,
            new MenuPageAction("Back", static () => { }),
            drawBottomBarButton: false,
            bottomBarLabel: string.Empty,
            hoveredStackedIndex,
            soloHovered,
            bottomBarHovered: false,
            textScaleMultiplier: 1.08f);

        DrawGameplayLoadoutMenuHeader(layout.PlaqueBounds);
        var detailIndex = hoveredStackedIndex >= 0
            ? hoveredStackedIndex
            : Math.Clamp(GetSelectedGameplayLoadoutIndex(loadouts), 0, loadouts.Count - 1);
        DrawGameplayLoadoutMenuDetails(layout.PlaqueBounds, loadouts[detailIndex]);
    }

    private List<MenuPageButton> BuildGameplayLoadoutMenuButtons()
    {
        var buttons = new List<MenuPageButton>();
        var loadouts = GetOrderedGameplayLoadoutsForLocalPlayer();
        if (loadouts.Count == 0)
        {
            return buttons;
        }

        var layout = GetCenteredPlaqueMenuLayout(tall: loadouts.Count > 4, stackedButtonCount: loadouts.Count, includeBottomBarButton: false);
        for (var index = 0; index < loadouts.Count && index < layout.StackedButtonBounds.Length; index += 1)
        {
            var selectedIndex = index;
            var selectedLoadoutId = loadouts[index].Id;
            buttons.Add(new MenuPageButton(
                loadouts[index].DisplayName,
                layout.StackedButtonBounds[index],
                () => ApplyGameplayLoadoutSelection(selectedIndex, selectedLoadoutId)));
        }

        buttons.Add(new MenuPageButton("Back", layout.SoloButtonBounds, () =>
        {
            CloseGameplayLoadoutMenu();
            OpenInGameMenu();
        }));
        return buttons;
    }

    private int GetHoveredGameplayLoadoutMenuButtonIndex(MouseState mouse, IReadOnlyList<MenuPageButton> buttons)
    {
        var mousePoint = new Point(mouse.X, mouse.Y);
        for (var index = 0; index < buttons.Count; index += 1)
        {
            if (buttons[index].Bounds.Contains(mousePoint))
            {
                return index;
            }
        }

        return -1;
    }

    private void ApplyGameplayLoadoutSelection(int loadoutIndex, string loadoutId)
    {
        if (_networkClient.IsConnected)
        {
            _networkClient.QueueGameplayLoadoutSelection((byte)(loadoutIndex + 1));
            SetNetworkStatus("Switching loadout...");
        }
        else
        {
            _world.TrySetNetworkPlayerGameplayLoadout(SimulationWorld.LocalPlayerSlot, loadoutId);
        }

        CloseGameplayLoadoutMenu();
    }

    private IReadOnlyList<GameplayClassLoadoutDefinition> GetOrderedGameplayLoadoutsForLocalPlayer()
    {
        return GameplayLoadoutSelectionResolver.GetOrderedLoadouts(_world.LocalPlayer.ClassId);
    }

    private int GetSelectedGameplayLoadoutIndex(IReadOnlyList<GameplayClassLoadoutDefinition> loadouts)
    {
        var currentLoadoutId = _world.LocalPlayer.GameplayLoadoutState.LoadoutId;
        for (var index = 0; index < loadouts.Count; index += 1)
        {
            if (string.Equals(loadouts[index].Id, currentLoadoutId, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return 0;
    }

    private void DrawGameplayLoadoutMenuHeader(Rectangle plaqueBounds)
    {
        DrawMenuBitmapFontText("Loadout", new Vector2(plaqueBounds.X + 18f, plaqueBounds.Y - 54f), new Color(233, 225, 212), 1.3f);
        DrawMenuBitmapFontText(CharacterClassCatalog.GetDefinition(_world.LocalPlayer.ClassId).DisplayName, new Vector2(plaqueBounds.X + 18f, plaqueBounds.Y - 24f), new Color(189, 177, 159), 0.82f);
    }

    private void DrawGameplayLoadoutMenuDetails(Rectangle plaqueBounds, GameplayClassLoadoutDefinition selectedLoadout)
    {
        var runtimeRegistry = CharacterClassCatalog.RuntimeRegistry;
        var primaryItem = runtimeRegistry.GetRequiredItem(selectedLoadout.PrimaryItemId);
        var secondaryItem = string.IsNullOrWhiteSpace(selectedLoadout.SecondaryItemId)
            ? null
            : runtimeRegistry.GetRequiredItem(selectedLoadout.SecondaryItemId);
        var utilityItem = string.IsNullOrWhiteSpace(selectedLoadout.UtilityItemId)
            ? null
            : runtimeRegistry.GetRequiredItem(selectedLoadout.UtilityItemId);

        var panelBounds = new Rectangle(
            plaqueBounds.Right + 42,
            plaqueBounds.Y + 42,
            Math.Max(280, ViewportWidth - plaqueBounds.Right - 82),
            Math.Min(300, ViewportHeight - plaqueBounds.Y - 120));
        _spriteBatch.Draw(_pixel, panelBounds, new Color(44, 38, 32) * 0.92f);
        _spriteBatch.Draw(_pixel, new Rectangle(panelBounds.X, panelBounds.Y, panelBounds.Width, 3), new Color(163, 139, 111));
        _spriteBatch.Draw(_pixel, new Rectangle(panelBounds.X, panelBounds.Bottom - 3, panelBounds.Width, 3), new Color(25, 21, 18));

        var textX = panelBounds.X + 20f;
        var textY = panelBounds.Y + 18f;
        DrawMenuBitmapFontText(selectedLoadout.DisplayName, new Vector2(textX, textY), new Color(233, 225, 212), 1.08f);
        textY += 34f;
        DrawMenuBitmapFontText($"Primary  {primaryItem.DisplayName}", new Vector2(textX, textY), new Color(219, 209, 193), 0.84f);
        textY += 24f;

        if (secondaryItem is not null)
        {
            DrawMenuBitmapFontText($"Secondary  {secondaryItem.DisplayName}", new Vector2(textX, textY), new Color(219, 209, 193), 0.84f);
            textY += 24f;
        }

        if (utilityItem is not null)
        {
            DrawMenuBitmapFontText($"Utility  {utilityItem.DisplayName}", new Vector2(textX, textY), new Color(219, 209, 193), 0.84f);
            textY += 24f;
        }

        textY += 14f;
        var currentLoadoutId = _world.LocalPlayer.GameplayLoadoutState.LoadoutId;
        var statusLabel = string.Equals(currentLoadoutId, selectedLoadout.Id, StringComparison.Ordinal)
            ? "Equipped"
            : "Available";
        DrawMenuBitmapFontText($"Status  {statusLabel}", new Vector2(textX, textY), new Color(196, 168, 127), 0.82f);
        textY += 32f;
        DrawMenuBitmapFontText("Selections are validated by the server.", new Vector2(textX, textY), new Color(170, 160, 148), 0.72f);
        textY += 20f;
        DrawMenuBitmapFontText("Press 1-9 or click a plaque to switch.", new Vector2(textX, textY), new Color(146, 138, 128), 0.68f);
    }
}
