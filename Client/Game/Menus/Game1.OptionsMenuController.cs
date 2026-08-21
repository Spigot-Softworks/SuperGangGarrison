#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.IO;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class OptionsMenuController
    {
        private readonly record struct OptionsMenuAction(
            string Label,
            string Value,
            Action Activate,
            OptionsMenuTab Tab,
            Action<int>? AdjustValue = null);
        private readonly record struct ReplayMenuEntry(string DisplayName, string Path, string Kind, bool IsOpenGarrisonDemo, DateTime LastWriteTimeUtc);

        private enum OptionsMenuTab
        {
            Graphics,
            Audio,
            Controls,
            Hud,
            Gameplay,
            Replays,
            Plugins,
        }

        private static readonly string[] OptionsMenuTabLabels =
        {
            "Graphics",
            "Audio",
            "Controls",
            "HUD",
            "Gameplay",
            "Replays",
            "Plugins",
        };

        private const string MasterVolumeLabel = "Global Volume";
        private const string MenuMusicVolumeLabel = "Menu Music Volume";
        private const string InGameMusicVolumeLabel = "In-Game Music Volume";
        private const string CombatMusicVolumeLabel = "Combat Music Volume";
        private const string SoundEffectsVolumeLabel = "SFX Volume";
        private const string ControllerAimAssistStrengthLabel = "Aim Assist Strength";
        private const string ControllerAimDeadzoneLabel = "Stick Deadzone";
        private const string ControllerScopedAimSpeedLabel = "Scoped Aim Speed";
        private const string ControllerAimDistanceTier1Label = "Aim Distance 1";
        private const string ControllerAimDistanceTier2Label = "Aim Distance 2";
        private const string ControllerAimDistanceTier3Label = "Aim Distance 3";

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
            _game._pendingControllerControlsBinding = null;
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
            _game._controlsScrollOffset = 0;
            _game._controlsPageIndex = 0;
            _game._pendingControlsBinding = null;
            _game._pendingControllerControlsBinding = null;
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
            _game._controlsScrollOffset = 0;
            _game._pendingControlsBinding = null;
            _game._pendingControllerControlsBinding = null;

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
                var valueClickPressed = mouse.LeftButton == ButtonState.Pressed && _game._previousMouse.LeftButton != ButtonState.Pressed;
                if (valueClickPressed)
                {
                    GetOptionsMenuPanelLayout(out _, out var valueListBounds, out _, out _, out var valueRowHeight);
                    var rowBounds = new Rectangle(
                        valueListBounds.X,
                        valueListBounds.Y + ((0 - _game._optionsScrollOffset) * valueRowHeight),
                        valueListBounds.Width,
                        valueRowHeight - 2);

                    if (valueListBounds.Contains(mouse.Position))
                    {
                        const float optionsRowHorizontalPadding = 14f;
                        var valueBoxWidth = (int)(rowBounds.Width * 0.42f);
                        var valueBoxHeight = rowBounds.Height - 12;
                        if (valueBoxHeight < 30)
                        {
                            valueBoxHeight = 30;
                        }

                        var valueRightX = rowBounds.Right - optionsRowHorizontalPadding;
                        var valueBoxBounds = new Rectangle(
                            (int)(valueRightX - valueBoxWidth),
                            rowBounds.Y + ((rowBounds.Height - valueBoxHeight) / 2),
                            valueBoxWidth,
                            valueBoxHeight);

                        if (valueBoxBounds.Contains(mouse.Position) && _game.IsTextFieldDoubleClick(TextFieldClickTarget.OptionsPlayerName))
                        {
                            _game.SelectAllTextInActiveField(TextFieldClickTarget.OptionsPlayerName);
                        }
                        else
                        {
                            _game.ResetTextFieldClickTarget();
                        }
                    }
                    else
                    {
                        _game.ResetTextFieldClickTarget();
                    }
                }

                return;
            }

            var actions = BuildOptionsMenuActions();
            GetOptionsMenuPanelLayout(out var panel, out var listBounds, out var backBounds, out var compactLayout, out var rowHeight);
            var tabBounds = GetOptionsMenuTabButtonBounds(panel, compactLayout);
            var visibleRowCount = Math.Max(1, listBounds.Height / rowHeight);
            ClampOptionsScrollOffset(actions.Count, visibleRowCount);

            if (TryUpdateOptionsControllerInput(actions, visibleRowCount))
            {
                return;
            }

            var trackBounds = new Rectangle(panel.Right - 20, listBounds.Y, 8, listBounds.Height);
            var optionsScrollOffset = _game._optionsScrollOffset;
            if (_game.TryHandleScrollbarDrag(
                    mouse,
                    _game._previousMouse,
                    ScrollbarOwners.OptionsMenu,
                    trackBounds,
                    ref optionsScrollOffset,
                    actions.Count,
                    visibleRowCount))
            {
                _game._optionsScrollOffset = optionsScrollOffset;
                return;
            }

            _game._optionsScrollOffset = optionsScrollOffset;

            const float optionsRowValueHorizontalPadding = 14f;
            const float optionsRowValueTextScale = 1f;
            var wheelDelta = mouse.ScrollWheelValue - _game._previousMouse.ScrollWheelValue;
            if (wheelDelta != 0 && listBounds.Contains(mouse.Position))
            {
                var stepCount = Math.Max(1, Math.Abs(wheelDelta) / 120);
                _game._optionsScrollOffset = Math.Clamp(
                    _game._optionsScrollOffset + (wheelDelta > 0 ? -stepCount : stepCount),
                    0,
                    Math.Max(0, actions.Count - visibleRowCount));
            }

            if (mouse.LeftButton == ButtonState.Pressed && _game._previousMouse.LeftButton != ButtonState.Pressed)
            {
                for (var tabIndex = 0; tabIndex < tabBounds.Length; tabIndex += 1)
                {
                    if (tabBounds[tabIndex].Contains(mouse.Position))
                    {
                        _game._optionsPageIndex = tabIndex;
                        _game._optionsScrollOffset = 0;
                        _game._optionsHoverIndex = -1;
                        return;
                    }
                }
            }

            if (_game.IsControllerMenuInputActive())
            {
                if (_game._optionsHoverIndex < 0 && actions.Count > 0)
                {
                    _game._optionsHoverIndex = 0;
                }
            }
            else
            {
                _game._optionsHoverIndex = -1;
            }

            if (_game.ShouldUseMouseMenuHover(mouse) && backBounds.Contains(mouse.Position))
            {
                _game._optionsHoverIndex = actions.Count;
            }
            else if (_game.ShouldUseMouseMenuHover(mouse) && listBounds.Contains(mouse.Position))
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

            var selectedAction = actions[_game._optionsHoverIndex];
            if (IsOptionsStepperRow(selectedAction))
            {
                var visibleIndex = _game._optionsHoverIndex - _game._optionsScrollOffset;
                var rowBounds = new Rectangle(listBounds.X, listBounds.Y + (visibleIndex * rowHeight), listBounds.Width, rowHeight);
                var valueRightX = rowBounds.Right - optionsRowValueHorizontalPadding;
                var displayValue = $"< {selectedAction.Value} >";
                var valueWidth = _game.MeasureBitmapFontWidth(displayValue, optionsRowValueTextScale);
                var valueX = valueRightX - valueWidth;
                if (mouse.X < valueX + (valueWidth / 2))
                {
                    TryAdjustOptionsStepperValue(selectedAction, -1);
                    return;
                }
            }

            selectedAction.Activate();
        }

        private bool TryUpdateOptionsControllerInput(IReadOnlyList<OptionsMenuAction> actions, int visibleRowCount)
        {
            if (!_game.IsControllerMenuInputActive())
            {
                return false;
            }

            if (_game.IsControllerMenuBackPressed())
            {
                CloseOptionsMenu();
                return true;
            }

            var handled = false;
            if (_game.TryConsumeControllerMenuNavigation(out var horizontalStep, out var verticalStep))
            {
                if (verticalStep != 0)
                {
                    _game._optionsHoverIndex = MoveControllerMenuSelection(
                        _game._optionsHoverIndex,
                        actions.Count + 1,
                        verticalStep);
                    EnsureOptionsControllerSelectionVisible(actions.Count, visibleRowCount);
                    handled = true;
                }
                else if (horizontalStep != 0)
                {
                    if (_game._optionsHoverIndex >= 0 && _game._optionsHoverIndex < actions.Count)
                    {
                        var selectedAction = actions[_game._optionsHoverIndex];
                        if (TryAdjustOptionsStepperValue(selectedAction, horizontalStep))
                        {
                            return true;
                        }
                    }

                    _game._optionsPageIndex = Math.Clamp(_game._optionsPageIndex + horizontalStep, 0, OptionsMenuTabLabels.Length - 1);
                    _game._optionsScrollOffset = 0;
                    _game._optionsHoverIndex = actions.Count > 0 ? 0 : -1;
                    handled = true;
                }
            }

            if (_game.IsControllerMenuConfirmPressed())
            {
                if (_game._optionsHoverIndex < 0 && actions.Count > 0)
                {
                    _game._optionsHoverIndex = 0;
                }

                if (_game._optionsHoverIndex == actions.Count)
                {
                    CloseOptionsMenu();
                    return true;
                }

                if (_game._optionsHoverIndex >= 0 && _game._optionsHoverIndex < actions.Count)
                {
                    actions[_game._optionsHoverIndex].Activate();
                    return true;
                }
            }

            return handled;
        }

        private static bool TryAdjustOptionsStepperValue(OptionsMenuAction action, int step)
        {
            if (step == 0 || action.AdjustValue is null)
            {
                return false;
            }

            action.AdjustValue(step);
            return true;
        }

        private static bool IsOptionsStepperRow(OptionsMenuAction action)
        {
            return action.AdjustValue is not null;
        }

        private void EnsureOptionsControllerSelectionVisible(int actionCount, int visibleRowCount)
        {
            if (_game._optionsHoverIndex < 0 || _game._optionsHoverIndex >= actionCount)
            {
                return;
            }

            if (_game._optionsHoverIndex < _game._optionsScrollOffset)
            {
                _game._optionsScrollOffset = _game._optionsHoverIndex;
            }
            else if (_game._optionsHoverIndex >= _game._optionsScrollOffset + visibleRowCount)
            {
                _game._optionsScrollOffset = _game._optionsHoverIndex - visibleRowCount + 1;
            }
        }

        public void DrawOptionsMenu()
        {
            var viewportWidth = _game.ViewportWidth;
            var viewportHeight = _game.ViewportHeight;
            _game._spriteBatch.Draw(_game._pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black * 0.86f);

            // Draw bottom bar and runners (in animated mode only) - behind everything else
            if (_game._menuBackgroundMode != MenuBackgroundMode.Static)
            {
                const int bottomBarHeight = 76;
                var barY = viewportHeight - bottomBarHeight;
                var bottomBarBounds = new Rectangle(0, barY, viewportWidth, bottomBarHeight);
                _game._spriteBatch.Draw(_game._pixel, bottomBarBounds, new Color(0x57, 0x4f, 0x47));
                _game._menuBottomBarRunners.Draw(bottomBarBounds);
            }

            var actions = BuildOptionsMenuActions();
            GetOptionsMenuPanelLayout(out var panel, out var listBounds, out var backBounds, out var compactLayout, out var rowHeight);
            var mouse = _game.GetFrameMouseState();
            var visibleRowCount = Math.Max(1, listBounds.Height / rowHeight);
            ClampOptionsScrollOffset(actions.Count, visibleRowCount);

            _game.DrawRoundedRectangleOutline(panel, new Color(59, 51, 46), new Color(213, 205, 188), outlineThickness: 2, radius: 8);

                const float optionsHeaderScale = 1f;
                const float optionsRowTextScale = 1f;
                const float optionsCompactRowTextScale = 1f;
                const float optionsRowHorizontalPadding = 14f;
                const float optionsRowColumnGap = 20f;

                _game.DrawBitmapFontText("Options", new Vector2(listBounds.X, panel.Y + 14f), Color.White, optionsHeaderScale);
                DrawOptionsMenuTabs(panel, compactLayout);
            if (actions.Count > visibleRowCount)
            {
                var visibleStart = _game._optionsScrollOffset + 1;
                var visibleEnd = Math.Min(actions.Count, _game._optionsScrollOffset + visibleRowCount);
                _game.DrawBitmapFontText(
                    $"{visibleStart}-{visibleEnd}/{actions.Count}",
                    new Vector2(listBounds.Right - (compactLayout ? 78f : 96f), panel.Y + 14f),
                    new Color(186, 186, 186),
                    1f);
            }

            var endIndex = Math.Min(actions.Count, _game._optionsScrollOffset + visibleRowCount);
            var rowSpacing = compactLayout ? 4 : 6;
            var rowHeightWithoutSpacing = rowHeight - rowSpacing;
            for (var index = _game._optionsScrollOffset; index < endIndex; index += 1)
            {
                var visibleRow = index - _game._optionsScrollOffset;
                var rowBounds = new Rectangle(listBounds.X, listBounds.Y + (visibleRow * rowHeight), listBounds.Width, rowHeightWithoutSpacing);
                var isHovered = index == _game._optionsHoverIndex || rowBounds.Contains(mouse.Position);
                _game._spriteBatch.Draw(_game._pixel, rowBounds, isHovered ? new Color(36, 32, 29) : new Color(54, 47, 41));

                var textScale = compactLayout ? optionsCompactRowTextScale : optionsRowTextScale;
                var textY = rowBounds.Y + ((rowBounds.Height - _game.MeasureBitmapFontHeight(textScale)) * 0.5f);

                var row = actions[index];
                var labelX = rowBounds.X + optionsRowHorizontalPadding;
                var valueRightX = rowBounds.Right - optionsRowHorizontalPadding;
                var displayValue = row.Label switch
                {
                    _ when IsOptionsStepperRow(row) => $"< {row.Value} >",
                    _ => row.Value,
                };
                var trimmedValue = _game.TrimBitmapMenuText(displayValue, rowBounds.Width * 0.42f, textScale);
                var valueWidth = _game.MeasureBitmapFontWidth(trimmedValue, textScale);
                var valueX = valueRightX - valueWidth;
                var labelMaxWidth = Math.Max(40f, valueX - labelX - optionsRowColumnGap);
                var trimmedLabel = _game.TrimBitmapMenuText(row.Label, labelMaxWidth, textScale);

                _game.DrawBitmapFontText(trimmedLabel, new Vector2(labelX, textY), Color.White, textScale);

                if (row.Label == "Player Name" && _game._editingPlayerName)
                {
                    var valueBoxWidth = (int)(rowBounds.Width * 0.42f);
                    var valueBoxHeight = rowBounds.Height - 12;
                    if (valueBoxHeight < 18)
                    {
                        valueBoxHeight = 18;
                    }
                    var valueBoxBounds = new Rectangle(
                        (int)(valueRightX - valueBoxWidth),
                        rowBounds.Y + ((rowBounds.Height - valueBoxHeight) / 2),
                        valueBoxWidth,
                        valueBoxHeight);

                    _game.DrawMenuInputBoxScaled(
                        valueBoxBounds,
                        _game._playerNameEditBuffer,
                        true,
                        textScale,
                        _game._playerNameEditCursorIndex,
                        _game._playerNameEditSelectionStart);
                }
                else if (!string.IsNullOrWhiteSpace(trimmedValue))
                {
                    _game.DrawBitmapFontText(trimmedValue, new Vector2(valueX, textY), Color.White, textScale);
                }
            }

            if (actions.Count > visibleRowCount)
            {
                var trackBounds = new Rectangle(panel.Right - 20, listBounds.Y, 8, listBounds.Height);
                _game._spriteBatch.Draw(_game._pixel, trackBounds, new Color(22, 24, 28));

                var maxOffset = Math.Max(1, actions.Count - visibleRowCount);
                var thumbHeight = Math.Max(24, (int)MathF.Round(trackBounds.Height * (visibleRowCount / (float)actions.Count)));
                var thumbTravel = Math.Max(0, trackBounds.Height - thumbHeight);
                var thumbY = trackBounds.Y + (int)MathF.Round((_game._optionsScrollOffset / (float)maxOffset) * thumbTravel);
                var thumbBounds = new Rectangle(trackBounds.X, thumbY, trackBounds.Width, thumbHeight);
                _game._spriteBatch.Draw(_game._pixel, thumbBounds, new Color(105, 105, 105));
            }

            var backHovered = _game._optionsHoverIndex == actions.Count || backBounds.Contains(mouse.Position);
            _game.DrawMenuButtonScaled(backBounds, "Back", backHovered, 1f);
        }

        private List<OptionsMenuAction> BuildOptionsMenuActions()
        {
            var currentTab = GetOptionsMenuTab(_game._optionsPageIndex);
            var allActions = new List<OptionsMenuAction>
            {
                // Graphics: display, rendering, and client-side visual presentation.
                new("Display Mode", OperatingSystem.IsBrowser() ? "Browser" : Game1.GetDisplayModeLabel(_game._displayMode), _game.CycleDisplayModeSetting, OptionsMenuTab.Graphics),
                new("Aspect Ratio", Game1.GetIngameResolutionLabel(_game._ingameResolution), _game.CycleIngameResolutionSetting, OptionsMenuTab.Graphics),
                new("Window Size", OperatingSystem.IsBrowser() ? "Browser" : Game1.GetWindowSizeLabel(_game._windowSize), _game.CycleWindowSizeSetting, OptionsMenuTab.Graphics),
                new("Cursor Size", Game1.GetCursorSizeLabel(_game._cursorSizePercent), _game.CycleCursorSizeSetting, OptionsMenuTab.Graphics, _game.AdjustCursorSizeSetting),
                new("Menu Background", GetMenuBackgroundModeLabel(_game._menuBackgroundMode), _game.CycleMenuBackgroundModeSetting, OptionsMenuTab.Graphics),
                new("Particles", GetParticleModeLabel(_game._particleMode), _game.CycleParticleModeSetting, OptionsMenuTab.Graphics),
                new("Flame Style", GetFlameRenderModeLabel(_game._flameRenderMode), _game.CycleFlameRenderModeSetting, OptionsMenuTab.Graphics),
                new("Gibs", GetGibLevelLabel(_game._gibLevel), _game.CycleGibLevelSetting, OptionsMenuTab.Graphics),
                new("Stuck Arrows", _game._stuckArrowsEnabled ? "Enabled" : "Disabled", _game.ToggleStuckArrowsSetting, OptionsMenuTab.Graphics),
                new("Corpses", GetCorpseDurationLabel(_game._corpseDurationMode), _game.CycleCorpseDurationSetting, OptionsMenuTab.Graphics),
                new("Sprite Shadow", _game._spriteDropShadowEnabled ? "Enabled" : "Disabled", _game.ToggleSpriteDropShadowSetting, OptionsMenuTab.Graphics),
                new("Weapon Rotation", _game._pixelPerfectWeaponRotation ? "Pixel-Perfect" : "High-Res", _game.ToggleWeaponRotationStyleSetting, OptionsMenuTab.Graphics),
                new("Weapon Rotation Source", _game._useLocalWeaponRotation ? "Local (snappier)" : "Remote (accurate)", _game.ToggleWeaponRotationSourceSetting, OptionsMenuTab.Graphics),
                new("Uber Outlines", _game._uberOutlineEnabled ? "Enabled" : "Disabled", _game.ToggleUberOutlinesSetting, OptionsMenuTab.Graphics),
                new("Projectile Team Tint", _game._projectileTeamTintEnabled ? "Enabled" : "Disabled", _game.ToggleProjectileTeamTintSetting, OptionsMenuTab.Graphics),
                new("Frame Limit", GetFrameRateLimitLabel(_game._frameRateLimit), _game.CycleFrameRateLimitSetting, OptionsMenuTab.Graphics),
                new("V Sync", _game._graphics.SynchronizeWithVerticalRetrace ? "Enabled" : "Disabled", _game.ToggleVSyncSetting, OptionsMenuTab.Graphics),
                new("Reset Window Size", string.Empty, _game.ResetWindowSize, OptionsMenuTab.Graphics),

                // Audio: volume, music, and global audio behavior.
                new(MasterVolumeLabel, $"{_game._masterVolumePercent}%", () => _game.AdjustMasterVolume(5), OptionsMenuTab.Audio, step => _game.AdjustMasterVolume(step * 5)),
                new("Music", GetMusicModeLabel(_game._musicMode), _game.CycleMusicModeSetting, OptionsMenuTab.Audio),
                new(MenuMusicVolumeLabel, $"{_game._menuMusicVolumePercent}%", () => _game.AdjustMenuMusicVolume(5), OptionsMenuTab.Audio, step => _game.AdjustMenuMusicVolume(step * 5)),
                new(InGameMusicVolumeLabel, $"{_game._ingameMusicVolumePercent}%", () => _game.AdjustIngameMusicVolume(5), OptionsMenuTab.Audio, step => _game.AdjustIngameMusicVolume(step * 5)),
                new("Dynamic Music", _game._dynamicMusicEnabled ? "Enabled" : "Disabled", _game.ToggleDynamicMusicSetting, OptionsMenuTab.Audio),
                new(CombatMusicVolumeLabel, $"{_game._combatMusicVolumePercent}%", () => _game.AdjustCombatMusicVolume(5), OptionsMenuTab.Audio, step => _game.AdjustCombatMusicVolume(step * 5)),
                new(SoundEffectsVolumeLabel, $"{_game._soundEffectsVolumePercent}%", () => _game.AdjustSoundEffectsVolume(5), OptionsMenuTab.Audio, step => _game.AdjustSoundEffectsVolume(step * 5)),
                new("Mute All Audio (F12)", _game._audioMuted ? "Muted" : "Unmuted", _game.ToggleAudioMuteSetting, OptionsMenuTab.Audio),

                // Controls: bindings and controller aiming behavior.
                new("Keyboard & Mouse", string.Empty, OpenControlsMenuFromOptions, OptionsMenuTab.Controls),
                new("Swap Weapons", _game.GetSwapWeaponsBindingLabel(), _game.CycleSwapWeaponsBindingSetting, OptionsMenuTab.Controls),
                new("Controller Input", Game1.GetControllerInputModeLabel(_game._clientSettings.ControllerInputMode), _game.CycleControllerInputModeSetting, OptionsMenuTab.Controls),
                new("Controller Reticle", Game1.GetControllerReticleModeLabel(_game._clientSettings.ControllerReticleMode), _game.CycleControllerReticleModeSetting, OptionsMenuTab.Controls),
                new("Controller Aim Assist", _game._clientSettings.ControllerAimAssistEnabled ? "Enabled" : "Disabled", _game.ToggleControllerAimAssistSetting, OptionsMenuTab.Controls),
                new("Flick to Change Directions", _game._clientSettings.ControllerFlickToChangeDirections ? "Enabled" : "Disabled", _game.ToggleControllerFlickToChangeDirectionsSetting, OptionsMenuTab.Controls),
                new(ControllerAimAssistStrengthLabel, Game1.GetControllerPercentLabel(OpenGarrisonPreferencesDocument.NormalizeControllerAimAssistStrength(_game._clientSettings.ControllerAimAssistStrength)), _game.CycleControllerAimAssistStrengthSetting, OptionsMenuTab.Controls, step => _game.AdjustControllerAimAssistStrengthSetting(step * 0.1f)),
                new(ControllerAimDeadzoneLabel, Game1.GetControllerPercentLabel(OpenGarrisonPreferencesDocument.NormalizeControllerAimDeadzone(_game._clientSettings.ControllerAimDeadzone)), _game.CycleControllerAimDeadzoneSetting, OptionsMenuTab.Controls, step => _game.AdjustControllerAimDeadzoneSetting(step * 0.05f)),
                new(ControllerScopedAimSpeedLabel, Game1.GetControllerSpeedLabel(OpenGarrisonPreferencesDocument.NormalizeControllerScopedPrecisionSpeed(_game._clientSettings.ControllerScopedPrecisionSpeed)), _game.CycleControllerScopedPrecisionSpeedSetting, OptionsMenuTab.Controls, step => _game.AdjustControllerScopedPrecisionSpeedSetting(step * 30f)),
                new(ControllerAimDistanceTier1Label, Game1.GetControllerPixelsLabel(OpenGarrisonPreferencesDocument.NormalizeControllerAimDistance(_game._clientSettings.ControllerAimDistanceTier1, OpenGarrisonPreferencesDocument.DefaultControllerAimDistanceTier1)), _game.CycleControllerAimDistanceTier1Setting, OptionsMenuTab.Controls, step => _game.AdjustControllerAimDistanceTier1Setting(step * 16f)),
                new(ControllerAimDistanceTier2Label, Game1.GetControllerPixelsLabel(OpenGarrisonPreferencesDocument.NormalizeControllerAimDistance(_game._clientSettings.ControllerAimDistanceTier2, OpenGarrisonPreferencesDocument.DefaultControllerAimDistanceTier2)), _game.CycleControllerAimDistanceTier2Setting, OptionsMenuTab.Controls, step => _game.AdjustControllerAimDistanceTier2Setting(step * 16f)),
                new(ControllerAimDistanceTier3Label, Game1.GetControllerPixelsLabel(OpenGarrisonPreferencesDocument.NormalizeControllerAimDistance(_game._clientSettings.ControllerAimDistanceTier3, OpenGarrisonPreferencesDocument.DefaultControllerAimDistanceTier3)), _game.CycleControllerAimDistanceTier3Setting, OptionsMenuTab.Controls, step => _game.AdjustControllerAimDistanceTier3Setting(step * 16f)),

                // HUD: in-match overlays, player presentation, and feedback effects.
                new("Healer Radar", _game._healerRadarEnabled ? "Enabled" : "Disabled", _game.ToggleHealerRadarSetting, OptionsMenuTab.Hud),
                new("Show Healer", _game._showHealerEnabled ? "Enabled" : "Disabled", _game.ToggleShowHealerSetting, OptionsMenuTab.Hud),
                new("Show Healing", _game._showHealingEnabled ? "Enabled" : "Disabled", _game.ToggleShowHealingSetting, OptionsMenuTab.Hud),
                new("Health Bar", _game._showHealthBarEnabled ? "Enabled" : "Disabled", _game.ToggleShowHealthBarSetting, OptionsMenuTab.Hud),
                new("Shield Bar", _game._showShieldBarEnabled ? "Enabled" : "Disabled", _game.ToggleShowShieldBarSetting, OptionsMenuTab.Hud),
                new("Weapon HUD", Game1.GetHudWeaponDisplayModeLabel(_game._hudShowOnlyActiveWeapon), _game.ToggleHudWeaponDisplayModeSetting, OptionsMenuTab.Hud),
                new("Overhead Chat", _game._overheadChatEnabled ? "Enabled" : "Disabled", _game.ToggleOverheadChatSetting, OptionsMenuTab.Hud),
                new("Low HP Color", Game1.GetLowHealthColorModeLabel(_game._lowHealthColorMode), _game.CycleLowHealthColorModeSetting, OptionsMenuTab.Hud),
                new("Playercard Size", Game1.GetPlayerCardSizeLabel(_game._playerCardSizeMode), _game.CyclePlayerCardSizeSetting, OptionsMenuTab.Hud),
                new("Portrait Rumble", _game._portraitRumbleEnabled ? "Enabled" : "Disabled", _game.TogglePortraitRumbleSetting, OptionsMenuTab.Hud),
                new("MVP Art", _game._postGameMvpArtEnabled ? "Enabled" : "Disabled", _game.TogglePostGameMvpArtSetting, OptionsMenuTab.Hud),
                new("Damage Vignette", _game._damageVignetteEnabled ? "Enabled" : "Disabled", _game.ToggleDamageVignetteSetting, OptionsMenuTab.Hud),
                new("Vignette Intensity", Game1.GetDamageVignetteIntensityLabel(_game._damageVignetteIntensityPercent), _game.CycleDamageVignetteIntensitySetting, OptionsMenuTab.Hud),
                new("Player Names", _game._showPlayerNamesEnabled ? "Enabled" : "Disabled", _game.ToggleShowPlayerNamesSetting, OptionsMenuTab.Hud),
                new("Persistent Name", _game._showPersistentSelfNameEnabled ? "Enabled" : "Disabled", _game.TogglePersistentSelfNameSetting, OptionsMenuTab.Hud),
                new("Edit HUD", _game._mainMenuOpen ? "In game only" : string.Empty, OpenHudEditorFromOptions, OptionsMenuTab.Hud),

                // Gameplay: rules and simulation behavior that affect the match itself.
                new("Player Name", _game._editingPlayerName ? GetTextWithCursor(_game._playerNameEditBuffer, _game._playerNameEditCursorIndex) : _game._world.LocalPlayer.DisplayName, _game.BeginEditingPlayerName, OptionsMenuTab.Gameplay),
                new("Kill Cam", _game._killCamEnabled ? "Enabled" : "Disabled", _game.ToggleKillCamSetting, OptionsMenuTab.Gameplay),
                new("Network Smoothing", _game._positionSmoothingEnabled ? "Enabled" : "Disabled", _game.TogglePositionSmoothingSetting, OptionsMenuTab.Gameplay),
                new("Enable Prediction", _game._enablePrediction ? "Enabled" : "Disabled", _game.TogglePredictionSetting, OptionsMenuTab.Gameplay),
            };

            if (_game.HasClientPluginOptions())
            {
                allActions.Add(new OptionsMenuAction("Plugin Options", string.Empty, OpenPluginOptionsMenuFromOptions, OptionsMenuTab.Plugins));
            }

            allActions.Add(new OptionsMenuAction("Version", GetApplicationVersionLabel(), NoOp, OptionsMenuTab.Plugins));

            if (currentTab == OptionsMenuTab.Replays)
            {
                allActions.AddRange(BuildReplayMenuActions());
            }

            var filteredActions = new List<OptionsMenuAction>(allActions.Count);

            foreach (var action in allActions)
            {
                if (action.Tab == currentTab)
                {
                    filteredActions.Add(action);
                }
            }

            return filteredActions;
        }

        private static OptionsMenuTab GetOptionsMenuTab(int pageIndex)
        {
            return pageIndex switch
            {
                0 => OptionsMenuTab.Graphics,
                1 => OptionsMenuTab.Audio,
                2 => OptionsMenuTab.Controls,
                3 => OptionsMenuTab.Hud,
                4 => OptionsMenuTab.Gameplay,
                5 => OptionsMenuTab.Replays,
                _ => OptionsMenuTab.Plugins,
            };
        }

        private List<OptionsMenuAction> BuildReplayMenuActions()
        {
            var actions = new List<OptionsMenuAction>();
            if (OperatingSystem.IsBrowser())
            {
                actions.Add(new OptionsMenuAction("Replay Browser", "Unavailable in browser", NoOp, OptionsMenuTab.Replays));
                return actions;
            }

            var entries = GetReplayMenuEntries(out var status);
            actions.Add(new OptionsMenuAction("Replay Folders", status, NoOp, OptionsMenuTab.Replays));
            if (entries.Count == 0)
            {
                actions.Add(new OptionsMenuAction("No replay files found", "config/replays", NoOp, OptionsMenuTab.Replays));
                return actions;
            }

            foreach (var entry in entries)
            {
                var capturedEntry = entry;
                actions.Add(new OptionsMenuAction(
                    capturedEntry.DisplayName,
                    capturedEntry.Kind,
                    () => PlayReplayMenuEntry(capturedEntry),
                    OptionsMenuTab.Replays));
            }

            return actions;
        }

        private List<ReplayMenuEntry> GetReplayMenuEntries(out string status)
        {
            var entries = new List<ReplayMenuEntry>();
            var directories = GetReplaySearchDirectories();
            var searched = 0;
            foreach (var directory in directories)
            {
                try
                {
                    Directory.CreateDirectory(directory);
                    searched += 1;
                    foreach (var filePath in Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly))
                    {
                        if (!TryCreateReplayMenuEntry(filePath, out var entry))
                        {
                            continue;
                        }

                        entries.Add(entry);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
                {
                    _game.AddConsoleLine($"replay folder skipped: {directory} ({ex.Message})");
                }
            }

            entries.Sort((left, right) => right.LastWriteTimeUtc.CompareTo(left.LastWriteTimeUtc));
            if (entries.Count > 40)
            {
                entries.RemoveRange(40, entries.Count - 40);
            }

            status = entries.Count == 1
                ? $"1 file in {searched} folders"
                : $"{entries.Count} files in {searched} folders";
            return entries;
        }

        private static List<string> GetReplaySearchDirectories()
        {
            var directories = new List<string>();
            AddDistinctReplayDirectory(directories, RuntimePaths.ReplaysDirectory);
            return directories;
        }

        private static void AddDistinctReplayDirectory(List<string> directories, string directory)
        {
            var fullPath = Path.GetFullPath(directory);
            foreach (var existing in directories)
            {
                if (string.Equals(existing, fullPath, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                {
                    return;
                }
            }

            directories.Add(fullPath);
        }

        private static bool TryCreateReplayMenuEntry(string filePath, out ReplayMenuEntry entry)
        {
            entry = default;
            var extension = Path.GetExtension(filePath);
            var isOpenGarrisonDemo = string.Equals(extension, ".ogdemo", StringComparison.OrdinalIgnoreCase);
            var isLegacyReplay = string.Equals(extension, ".rply", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".replay", StringComparison.OrdinalIgnoreCase);
            if (!isOpenGarrisonDemo && !isLegacyReplay)
            {
                return false;
            }

            var fileInfo = new FileInfo(filePath);
            var kind = isOpenGarrisonDemo ? "Demo" : "Legacy Replay";
            entry = new ReplayMenuEntry(fileInfo.Name, fileInfo.FullName, kind, isOpenGarrisonDemo, fileInfo.LastWriteTimeUtc);
            return true;
        }

        private void PlayReplayMenuEntry(ReplayMenuEntry entry)
        {
            var started = entry.IsOpenGarrisonDemo
                ? _game.TryPlayOpenGarrisonDemo(entry.Path, addConsoleFeedback: true)
                : _game.TryPlayLegacyReplay(entry.Path, addConsoleFeedback: true);
            if (!started)
            {
                return;
            }

            _game._menuStatusMessage = string.Empty;
            _game._optionsMenuOpen = false;
            _game._optionsMenuOpenedFromGameplay = false;
            _game._optionsHoverIndex = -1;
            _game._optionsScrollOffset = 0;
        }

        private static void NoOp()
        {
        }

        private static Rectangle[] GetOptionsMenuTabButtonBounds(Rectangle panel, bool compactLayout)
        {
            var padding = compactLayout ? 20 : 28;
            var buttonHeight = compactLayout ? 34 : 42;
            var tabCount = OptionsMenuTabLabels.Length;
            var spacing = compactLayout ? 8 : 12;
            var buttonWidth = Math.Min(160, Math.Max(72, (panel.Width - (padding * 2) - ((tabCount - 1) * spacing)) / tabCount));
            var startX = panel.X + padding;
            var y = panel.Y + (compactLayout ? 52 : 60);
            var bounds = new Rectangle[tabCount];

            for (var i = 0; i < tabCount; i += 1)
            {
                bounds[i] = new Rectangle(startX + i * (buttonWidth + spacing), y, buttonWidth, buttonHeight);
            }

            return bounds;
        }

        private void DrawOptionsMenuTabs(Rectangle panel, bool compactLayout)
        {
            var tabBounds = GetOptionsMenuTabButtonBounds(panel, compactLayout);
            for (var i = 0; i < tabBounds.Length; i += 1)
            {
                var selected = i == _game._optionsPageIndex;
                _game.DrawMenuButtonScaled(tabBounds[i], OptionsMenuTabLabels[i], selected, 1f);
            }
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
            var baseRowHeight = compactLayout ? 24 : 26;
            var rowSpacing = compactLayout ? 4 : 6;
            rowHeight = baseRowHeight + rowSpacing;
            var titleHeight = compactLayout ? 48 : 56;
            var tabRowHeight = compactLayout ? 44 : 48;
            var headerHeight = titleHeight + tabRowHeight;
            var footerHeight = compactLayout ? 56 : 64;
            var scrollbarPadding = 18;
            var listTopPadding = compactLayout ? 8 : 10;

            listBounds = new Rectangle(
                panel.X + padding,
                panel.Y + headerHeight + listTopPadding,
                panel.Width - (padding * 2) - scrollbarPadding,
                Math.Max(rowHeight, panel.Height - headerHeight - footerHeight - 10 - listTopPadding));

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

        private void OpenHudEditorFromOptions()
        {
            _game.OpenHudEditor(openedFromOptions: true);
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

        private static string GetMenuBackgroundModeLabel(MenuBackgroundMode menuBackgroundMode)
        {
            return menuBackgroundMode switch
            {
                MenuBackgroundMode.DefaultMaps => "Default Maps",
                MenuBackgroundMode.AllMaps => "All Maps",
                _ => "Static",
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
