#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class OptionsMenuController
    {
        private readonly Game1 _game;

        public OptionsMenuController(Game1 game)
        {
            _game = game;
        }

        public void OpenOptionsMenu(bool fromGameplay)
        {
            _game._optionsMenuOpen = true;
            _game._optionsMenuOpenedFromGameplay = fromGameplay;
            _game._optionsPageIndex = 0;
            _game._pluginOptionsMenuOpen = false;
            _game._pendingPluginOptionsKeyItem = null;
            _game._selectedPluginOptionsPluginId = null;
            _game._controlsMenuOpen = false;
            _game._pendingControlsBinding = null;
            _game._optionsHoverIndex = -1;
            _game._pluginOptionsHoverIndex = -1;
            _game._controlsHoverIndex = -1;
            _game._editingPlayerName = false;
            _game._playerNameEditBuffer = _game._world.LocalPlayer.DisplayName;
        }

        public void CloseOptionsMenu()
        {
            var reopenInGameMenu = _game._optionsMenuOpenedFromGameplay && !_game._mainMenuOpen;
            _game._optionsMenuOpen = false;
            _game._optionsMenuOpenedFromGameplay = false;
            _game._pluginOptionsMenuOpen = false;
            _game._pluginOptionsMenuOpenedFromGameplay = false;
            _game._pendingPluginOptionsKeyItem = null;
            _game._selectedPluginOptionsPluginId = null;
            _game._optionsHoverIndex = -1;
            _game._pluginOptionsHoverIndex = -1;
            _game._editingPlayerName = false;
            _game._playerNameEditBuffer = _game._world.LocalPlayer.DisplayName;
            if (reopenInGameMenu)
            {
                _game.OpenInGameMenu();
            }
        }

        public void OpenPluginOptionsMenu(bool fromGameplay)
        {
            _game._pluginOptionsMenuOpen = true;
            _game._pluginOptionsMenuOpenedFromGameplay = fromGameplay;
            _game._pendingPluginOptionsKeyItem = null;
            _game._selectedPluginOptionsPluginId = null;
            _game._optionsMenuOpen = false;
            _game._pluginOptionsHoverIndex = -1;
            _game._pluginOptionsScrollOffset = 0;
            _game._optionsHoverIndex = -1;
            _game._editingPlayerName = false;
            _game._playerNameEditBuffer = _game._world.LocalPlayer.DisplayName;
        }

        public void ClosePluginOptionsMenu()
        {
            var reopenFromGameplay = _game._pluginOptionsMenuOpenedFromGameplay;
            _game._pluginOptionsMenuOpen = false;
            _game._pluginOptionsMenuOpenedFromGameplay = false;
            _game._pendingPluginOptionsKeyItem = null;
            _game._selectedPluginOptionsPluginId = null;
            _game._pluginOptionsHoverIndex = -1;
            _game._pluginOptionsScrollOffset = 0;
            OpenOptionsMenu(reopenFromGameplay);
        }

        public void OpenControlsMenu(bool fromGameplay)
        {
            _game._controlsMenuOpen = true;
            _game._controlsMenuOpenedFromGameplay = fromGameplay;
            _game._controlsHoverIndex = -1;
            _game._pendingControlsBinding = null;
            _game._optionsMenuOpen = false;
            _game._pluginOptionsMenuOpen = false;
            _game._editingPlayerName = false;
        }

        public void CloseControlsMenu()
        {
            var reopenInGameMenu = _game._controlsMenuOpenedFromGameplay && !_game._mainMenuOpen;
            _game._controlsMenuOpen = false;
            _game._controlsMenuOpenedFromGameplay = false;
            _game._controlsHoverIndex = -1;
            _game._pendingControlsBinding = null;

            if (_game._mainMenuOpen || reopenInGameMenu)
            {
                OpenOptionsMenu(reopenInGameMenu);
            }
        }

        public void UpdateOptionsMenu(KeyboardState keyboard, MouseState mouse)
        {
            if (_game.IsKeyPressed(keyboard, Keys.Escape))
            {
                if (_game._editingPlayerName)
                {
                    _game._editingPlayerName = false;
                    _game._playerNameEditBuffer = _game._world.LocalPlayer.DisplayName;
                    return;
                }

                CloseOptionsMenu();
                return;
            }

            if (_game._editingPlayerName)
            {
                return;
            }

            var buttons = BuildOptionsMenuButtons();
            _game._optionsHoverIndex = -1;
            for (var index = 0; index < buttons.Count; index += 1)
            {
                if (!buttons[index].Bounds.Contains(mouse.Position))
                {
                    continue;
                }

                _game._optionsHoverIndex = index;
                break;
            }

            var clickPressed = mouse.LeftButton == ButtonState.Pressed && _game._previousMouse.LeftButton != ButtonState.Pressed;
            if (!clickPressed || _game._optionsHoverIndex < 0)
            {
                return;
            }

            buttons[_game._optionsHoverIndex].Activate();
        }

        public void DrawOptionsMenu()
        {
            var viewportWidth = _game.ViewportWidth;
            var viewportHeight = _game.ViewportHeight;
            _game._spriteBatch.Draw(_game._pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black * 0.8f);
            var actions = GetVisibleOptionsMenuActions(out _);
            var stackedActions = new List<MenuPageAction>(actions)
            {
                new("Next Page", AdvanceOptionsPage),
            };
            var layout = _game.GetCenteredPlaqueMenuLayout(tall: true, stackedActions.Count, includeBottomBarButton: false);
            var hoveredStackedIndex = _game._optionsHoverIndex >= 0 && _game._optionsHoverIndex < stackedActions.Count
                ? _game._optionsHoverIndex
                : -1;
            var soloHovered = _game._optionsHoverIndex == stackedActions.Count;
            _game.DrawPlaqueMenuLayout(layout, stackedActions, new MenuPageAction("Back", CloseOptionsMenu), false, string.Empty, hoveredStackedIndex, soloHovered, false);
        }

        private List<MenuPageButton> BuildOptionsMenuButtons()
        {
            var buttons = new List<MenuPageButton>();
            var visibleActions = GetVisibleOptionsMenuActions(out _);
            var stackedCount = visibleActions.Count + 1;
            var layout = _game.GetCenteredPlaqueMenuLayout(tall: true, stackedCount, includeBottomBarButton: false);

            for (var index = 0; index < visibleActions.Count && index < layout.StackedButtonBounds.Length; index += 1)
            {
                buttons.Add(new MenuPageButton(visibleActions[index].Label, layout.StackedButtonBounds[index], visibleActions[index].Activate));
            }

            if (visibleActions.Count < layout.StackedButtonBounds.Length)
            {
                buttons.Add(new MenuPageButton("Next Page", layout.StackedButtonBounds[visibleActions.Count], AdvanceOptionsPage));
            }

            buttons.Add(new MenuPageButton("Back", layout.SoloButtonBounds, CloseOptionsMenu));
            return buttons;
        }

        private List<MenuPageAction> GetVisibleOptionsMenuActions(out int pageCount)
        {
            const int itemsPerPage = 5;
            var allActions = BuildOptionsMenuActions();
            pageCount = Math.Max(1, (int)Math.Ceiling(allActions.Count / (float)itemsPerPage));
            _game._optionsPageIndex = ((_game._optionsPageIndex % pageCount) + pageCount) % pageCount;
            var startIndex = _game._optionsPageIndex * itemsPerPage;
            var count = Math.Min(itemsPerPage, Math.Max(0, allActions.Count - startIndex));
            return count > 0
                ? allActions.GetRange(startIndex, count)
                : [];
        }

        private List<MenuPageAction> BuildOptionsMenuActions()
        {
            var actions = new List<MenuPageAction>
            {
                new($"Player Name: {(_game._editingPlayerName ? _game._playerNameEditBuffer + "_" : _game._world.LocalPlayer.DisplayName)}", _game.BeginEditingPlayerName),
                new($"Fullscreen: {(_game._graphics.IsFullScreen ? "On" : "Off")}", _game.ToggleFullscreenSetting),
                new($"Music: {GetMusicModeLabel(_game._musicMode)}", _game.CycleMusicModeSetting),
                new($"Aspect Ratio: {Game1.GetIngameResolutionLabel(_game._ingameResolution)}", _game.CycleIngameResolutionSetting),
                new($"Particles: {GetParticleModeLabel(_game._particleMode)}", _game.CycleParticleModeSetting),
                new($"Gibs: {GetGibLevelLabel(_game._gibLevel)}", _game.CycleGibLevelSetting),
                new($"Corpses: {GetCorpseDurationLabel(_game._corpseDurationMode)}", _game.CycleCorpseDurationSetting),
                new($"Healer Radar: {(_game._healerRadarEnabled ? "Enabled" : "Disabled")}", _game.ToggleHealerRadarSetting),
                new($"Show Healer: {(_game._showHealerEnabled ? "Enabled" : "Disabled")}", _game.ToggleShowHealerSetting),
                new($"Show Healing: {(_game._showHealingEnabled ? "Enabled" : "Disabled")}", _game.ToggleShowHealingSetting),
                new($"Healthbar: {(_game._showHealthBarEnabled ? "Enabled" : "Disabled")}", _game.ToggleShowHealthBarSetting),
                new($"Persistent Name: {(_game._showPersistentSelfNameEnabled ? "Enabled" : "Disabled")}", _game.TogglePersistentSelfNameSetting),
                new($"Sprite Shadow: {(_game._spriteDropShadowEnabled ? "Enabled" : "Disabled")}", _game.ToggleSpriteDropShadowSetting),
                new($"Kill Cam: {(_game._killCamEnabled ? "Enabled" : "Disabled")}", _game.ToggleKillCamSetting),
                new($"V Sync: {(_game._graphics.SynchronizeWithVerticalRetrace ? "Enabled" : "Disabled")}", _game.ToggleVSyncSetting),
                new("Controls", OpenControlsMenuFromOptions),
            };

            if (_game.HasClientPluginOptions())
            {
                actions.Add(new MenuPageAction("Plugin Options", OpenPluginOptionsMenuFromOptions));
            }

            return actions;
        }

        private void AdvanceOptionsPage()
        {
            GetVisibleOptionsMenuActions(out var pageCount);
            _game._optionsPageIndex = (_game._optionsPageIndex + 1) % Math.Max(1, pageCount);
            _game._optionsHoverIndex = -1;
        }

        private void OpenControlsMenuFromOptions()
        {
            OpenControlsMenu(_game._optionsMenuOpenedFromGameplay);
        }

        private void OpenPluginOptionsMenuFromOptions()
        {
            OpenPluginOptionsMenu(_game._optionsMenuOpenedFromGameplay);
        }

        private static string GetParticleModeLabel(int particleMode)
        {
            return particleMode switch
            {
                0 => "Normal",
                2 => "Alternative (faster)",
                _ => "Disabled",
            };
        }

        private static string GetMusicModeLabel(MusicMode musicMode)
        {
            return musicMode switch
            {
                MusicMode.None => "None",
                MusicMode.MenuOnly => "Menu Only",
                MusicMode.InGameOnly => "In-Game Only",
                _ => "Menu and In-Game",
            };
        }

        private static string GetGibLevelLabel(int gibLevel)
        {
            return gibLevel switch
            {
                0 => "0, No blood or gibs",
                1 => "1, Blood only",
                2 => "2, Blood and medium gibs",
                _ => $"{gibLevel}, Full blood and gibs",
            };
        }

        private static string GetCorpseDurationLabel(int corpseDurationMode)
        {
            return corpseDurationMode == ClientSettings.CorpseDurationInfinite
                ? "Infinite"
                : "300 ticks";
        }
    }
}
