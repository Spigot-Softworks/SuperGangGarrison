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
        private readonly record struct OptionsMenuAction(string Label, string Value, Action Activate);

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
            _game._optionsScrollOffset = 0;
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
            _game._optionsScrollOffset = 0;
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

            var actions = BuildOptionsMenuActions();
            GetOptionsMenuPanelLayout(out _, out var listBounds, out var backBounds, out _, out var rowHeight);
            var visibleRowCount = Math.Max(1, listBounds.Height / rowHeight);
            ClampOptionsScrollOffset(actions.Count, visibleRowCount);

            var wheelDelta = mouse.ScrollWheelValue - _game._previousMouse.ScrollWheelValue;
            if (wheelDelta != 0 && listBounds.Contains(mouse.Position))
            {
                var stepCount = Math.Max(1, Math.Abs(wheelDelta) / 120);
                _game._optionsScrollOffset = Math.Clamp(
                    _game._optionsScrollOffset + (wheelDelta > 0 ? -stepCount : stepCount),
                    0,
                    Math.Max(0, actions.Count - visibleRowCount));
            }

            _game._optionsHoverIndex = -1;
            if (backBounds.Contains(mouse.Position))
            {
                _game._optionsHoverIndex = actions.Count;
            }
            else if (listBounds.Contains(mouse.Position))
            {
                var visibleIndex = (mouse.Y - listBounds.Y) / rowHeight;
                var hoverIndex = _game._optionsScrollOffset + visibleIndex;
                if (visibleIndex >= 0 && hoverIndex >= 0 && hoverIndex < actions.Count)
                {
                    _game._optionsHoverIndex = hoverIndex;
                }
            }

            var clickPressed = mouse.LeftButton == ButtonState.Pressed && _game._previousMouse.LeftButton != ButtonState.Pressed;
            if (!clickPressed || _game._optionsHoverIndex < 0)
            {
                return;
            }

            if (_game._optionsHoverIndex == actions.Count)
            {
                CloseOptionsMenu();
                return;
            }

            actions[_game._optionsHoverIndex].Activate();
        }

        public void DrawOptionsMenu()
        {
            var viewportWidth = _game.ViewportWidth;
            var viewportHeight = _game.ViewportHeight;
            _game._spriteBatch.Draw(_game._pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black * 0.78f);

            var actions = BuildOptionsMenuActions();
            GetOptionsMenuPanelLayout(out var panel, out var listBounds, out var backBounds, out var compactLayout, out var rowHeight);
            var visibleRowCount = Math.Max(1, listBounds.Height / rowHeight);
            ClampOptionsScrollOffset(actions.Count, visibleRowCount);

            _game._spriteBatch.Draw(_game._pixel, panel, new Color(34, 35, 39, 235));
            _game._spriteBatch.Draw(_game._pixel, new Rectangle(panel.X, panel.Y, panel.Width, 3), new Color(210, 210, 210));
            _game._spriteBatch.Draw(_game._pixel, new Rectangle(panel.X, panel.Bottom - 3, panel.Width, 3), new Color(76, 76, 76));

                const float optionsHeaderScale = 1.15f;
                const float optionsRowTextScale = 1.39f;
                const float optionsCompactRowTextScale = 1.24f;
                const float optionsRowHorizontalPadding = 14f;
                const float optionsRowColumnGap = 20f;

                _game.DrawBitmapFontText("Options", new Vector2(listBounds.X, panel.Y + 14f), Color.White, optionsHeaderScale);
            if (actions.Count > visibleRowCount)
            {
                var visibleStart = _game._optionsScrollOffset + 1;
                var visibleEnd = Math.Min(actions.Count, _game._optionsScrollOffset + visibleRowCount);
                _game.DrawBitmapFontText(
                    $"{visibleStart}-{visibleEnd}/{actions.Count}",
                    new Vector2(listBounds.Right - (compactLayout ? 78f : 96f), panel.Y + 14f),
                    new Color(186, 186, 186),
                    compactLayout ? 0.92f : 1f);
            }

            var endIndex = Math.Min(actions.Count, _game._optionsScrollOffset + visibleRowCount);
            for (var index = _game._optionsScrollOffset; index < endIndex; index += 1)
            {
                var visibleRow = index - _game._optionsScrollOffset;
                var rowBounds = new Rectangle(listBounds.X, listBounds.Y + (visibleRow * rowHeight), listBounds.Width, rowHeight - 2);
                var isHovered = index == _game._optionsHoverIndex;
                _game._spriteBatch.Draw(_game._pixel, rowBounds, isHovered ? new Color(60, 60, 70) : new Color(44, 46, 52, 170));

                var textScale = compactLayout ? optionsCompactRowTextScale : optionsRowTextScale;
                var textY = rowBounds.Y + ((rowBounds.Height - _game.MeasureBitmapFontHeight(textScale)) * 0.5f);

                var row = actions[index];
                var labelX = rowBounds.X + optionsRowHorizontalPadding;
                var valueRightX = rowBounds.Right - optionsRowHorizontalPadding;
                var trimmedValue = _game.TrimBitmapMenuText(row.Value, rowBounds.Width * 0.42f, textScale);
                var valueWidth = _game.MeasureBitmapFontWidth(trimmedValue, textScale);
                var valueX = valueRightX - valueWidth;
                var labelMaxWidth = Math.Max(40f, valueX - labelX - optionsRowColumnGap);
                var trimmedLabel = _game.TrimBitmapMenuText(row.Label, labelMaxWidth, textScale);

                _game.DrawBitmapFontText(trimmedLabel, new Vector2(labelX, textY), Color.White, textScale);
                if (!string.IsNullOrWhiteSpace(trimmedValue))
                {
                    _game.DrawBitmapFontText(trimmedValue, new Vector2(valueX, textY), Color.White, textScale);
                }
            }

            if (actions.Count > visibleRowCount)
            {
                var trackBounds = new Rectangle(panel.Right - 20, listBounds.Y, 8, listBounds.Height);
                _game._spriteBatch.Draw(_game._pixel, trackBounds, new Color(30, 32, 38));

                var maxOffset = Math.Max(1, actions.Count - visibleRowCount);
                var thumbHeight = Math.Max(24, (int)MathF.Round(trackBounds.Height * (visibleRowCount / (float)actions.Count)));
                var thumbTravel = Math.Max(0, trackBounds.Height - thumbHeight);
                var thumbY = trackBounds.Y + (int)MathF.Round((_game._optionsScrollOffset / (float)maxOffset) * thumbTravel);
                var thumbBounds = new Rectangle(trackBounds.X, thumbY, trackBounds.Width, thumbHeight);
                _game._spriteBatch.Draw(_game._pixel, thumbBounds, new Color(125, 125, 125));
            }

            var backHovered = _game._optionsHoverIndex == actions.Count;
            _game.DrawMenuButtonScaled(backBounds, "Back", backHovered, 1f);
        }

        private List<OptionsMenuAction> BuildOptionsMenuActions()
        {
            var actions = new List<OptionsMenuAction>
            {
                new("Player Name", _game._editingPlayerName ? _game._playerNameEditBuffer + "_" : _game._world.LocalPlayer.DisplayName, _game.BeginEditingPlayerName),
                new("Fullscreen", _game._graphics.IsFullScreen ? "On" : "Off", _game.ToggleFullscreenSetting),
                new("Music", GetMusicModeLabel(_game._musicMode), _game.CycleMusicModeSetting),
                new("Aspect Ratio", Game1.GetIngameResolutionLabel(_game._ingameResolution), _game.CycleIngameResolutionSetting),
                new("Particles", GetParticleModeLabel(_game._particleMode), _game.CycleParticleModeSetting),
                new("Flame Style", GetFlameRenderModeLabel(_game._flameRenderMode), _game.CycleFlameRenderModeSetting),
                new("Gibs", GetGibLevelLabel(_game._gibLevel), _game.CycleGibLevelSetting),
                new("Corpses", GetCorpseDurationLabel(_game._corpseDurationMode), _game.CycleCorpseDurationSetting),
                new("Healer Radar", _game._healerRadarEnabled ? "Enabled" : "Disabled", _game.ToggleHealerRadarSetting),
                new("Show Healer", _game._showHealerEnabled ? "Enabled" : "Disabled", _game.ToggleShowHealerSetting),
                new("Show Healing", _game._showHealingEnabled ? "Enabled" : "Disabled", _game.ToggleShowHealingSetting),
                new("Healthbar", _game._showHealthBarEnabled ? "Enabled" : "Disabled", _game.ToggleShowHealthBarSetting),
                new("Persistent Name", _game._showPersistentSelfNameEnabled ? "Enabled" : "Disabled", _game.TogglePersistentSelfNameSetting),
                new("Sprite Shadow", _game._spriteDropShadowEnabled ? "Enabled" : "Disabled", _game.ToggleSpriteDropShadowSetting),
                new("Kill Cam", _game._killCamEnabled ? "Enabled" : "Disabled", _game.ToggleKillCamSetting),
                new("V Sync", _game._graphics.SynchronizeWithVerticalRetrace ? "Enabled" : "Disabled", _game.ToggleVSyncSetting),
                new("Controls", string.Empty, OpenControlsMenuFromOptions),
            };

            if (_game.HasClientPluginOptions())
            {
                actions.Add(new OptionsMenuAction("Plugin Options", string.Empty, OpenPluginOptionsMenuFromOptions));
            }

            return actions;
        }

        private void GetOptionsMenuPanelLayout(out Rectangle panel, out Rectangle listBounds, out Rectangle backBounds, out bool compactLayout, out int rowHeight)
        {
            var panelWidth = Math.Min(_game.ViewportWidth - 32, 760);
            var panelHeight = Math.Min(_game.ViewportHeight - 32, _game.ViewportHeight < 540 ? _game.ViewportHeight - 36 : 620);
            panel = new Rectangle(
                (_game.ViewportWidth - panelWidth) / 2,
                (_game.ViewportHeight - panelHeight) / 2,
                panelWidth,
                panelHeight);

            compactLayout = panel.Height < 540 || panel.Width < 700;
            var padding = compactLayout ? 20 : 28;
            rowHeight = compactLayout ? 46 : 52;
            var headerHeight = compactLayout ? 48 : 56;
            var footerHeight = compactLayout ? 56 : 64;
            var scrollbarPadding = 18;

            listBounds = new Rectangle(
                panel.X + padding,
                panel.Y + headerHeight,
                panel.Width - (padding * 2) - scrollbarPadding,
                Math.Max(rowHeight, panel.Height - headerHeight - footerHeight - 10));

            var backWidth = compactLayout ? 150 : 180;
            var backHeight = compactLayout ? 36 : 42;
            backBounds = new Rectangle(panel.Right - padding - backWidth, panel.Bottom - padding - backHeight, backWidth, backHeight);
        }

        private void ClampOptionsScrollOffset(int rowCount, int visibleRowCount)
        {
            _game._optionsScrollOffset = Math.Clamp(
                _game._optionsScrollOffset,
                0,
                Math.Max(0, rowCount - visibleRowCount));
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

        private static string GetFlameRenderModeLabel(int flameRenderMode)
        {
            return flameRenderMode == 0 ? "Particle" : "Sprite";
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
