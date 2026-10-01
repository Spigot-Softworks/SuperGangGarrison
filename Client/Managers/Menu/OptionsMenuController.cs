#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.IO;
using OpenGarrison.Core;

using static OpenGarrison.Client.Game1;

namespace OpenGarrison.Client;

public sealed class OptionsMenuController
    {
        private readonly record struct OptionsMenuAction(
            string Label,
            string Value,
            Action Activate,
            OptionsMenuTab Tab,
            Action<int>? AdjustValue = null,
            bool IsGroupHeader = false);
        private readonly record struct ReplayMenuEntry(string DisplayName, string Path, string Kind, bool IsOpenGarrisonDemo, DateTime LastWriteTimeUtc);

        private enum OptionsMenuTab
        {
            Graphics,
            Audio,
            Controls,
            Hud,
            Gameplay,
            Replays,
            Account,
            Plugins,
        }

        private static readonly string[] FullOptionsMenuTabLabels =
        {
            "Graphics",
            "Audio",
            "Controls",
            "HUD",
            "Gameplay",
            "Replays",
            "Account",
            "Plugins",
        };

        private static readonly string[] BrowserOptionsMenuTabLabels = ["Graphics", "Audio", "Controls", "HUD", "Gameplay", "About"];
        private static readonly string[] Gg2OnlyOptionsMenuTabLabels = ["Graphics", "Audio", "Controls", "HUD", "Gameplay"];
        private static string[] OptionsMenuTabLabels => OpenGarrison.ClientShared.ClientDistribution.IsGg2Only
            ? Gg2OnlyOptionsMenuTabLabels
            : IsRestrictedBrowserEdition ? BrowserOptionsMenuTabLabels : FullOptionsMenuTabLabels;

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

        private readonly IMenuContext _context;
        private int _optionsHoverIndex = -1;
        private int _optionsScrollOffset;
        private enum AudioOptionsGroup { Game, Chat }
        private AudioOptionsGroup? _expandedAudioGroup = AudioOptionsGroup.Game;
        private bool _crtOptionsExpanded;

        public OptionsMenuController(IMenuContext context)
        {
            _context = context;
        }

        public void OpenOptionsMenu(bool fromGameplay)
        {
            _context._optionsMenuOpen = true;
            _crtOptionsExpanded = false;
            _context._optionsMenuOpenedFromGameplay = fromGameplay;
            _context._optionsPageIndex = 0;
            _optionsScrollOffset = 0;
            _context._pluginOptionsMenuOpen = false;
            _context._pendingPluginOptionsKeyItem = null;
            _context._selectedPluginOptionsPluginId = null;
            _context._controlsMenuOpen = false;
            _context._pendingControlsBinding = null;
            _context._pendingControllerControlsBinding = null;
            _optionsHoverIndex = -1;
            _context._pluginOptionsHoverIndex = -1;
            _context._controlsHoverIndex = -1;
            _context._editingPlayerName = false;
            _context._playerNameEditBuffer = _context._world.LocalPlayer.DisplayName;
        }

        public void CloseOptionsMenu()
        {
            var reopenInGameMenu = _context._optionsMenuOpenedFromGameplay && !_context._mainMenuOpen;
            _context._optionsMenuOpen = false;
            _context._optionsMenuOpenedFromGameplay = false;
            _context._pluginOptionsMenuOpen = false;
            _context._pluginOptionsMenuOpenedFromGameplay = false;
            _context._pendingPluginOptionsKeyItem = null;
            _context._selectedPluginOptionsPluginId = null;
            _optionsHoverIndex = -1;
            _optionsScrollOffset = 0;
            _context._pluginOptionsHoverIndex = -1;
            _context._editingPlayerName = false;
            _context._playerNameEditBuffer = _context._world.LocalPlayer.DisplayName;
            _context.CloseAccountDialog();
            if (reopenInGameMenu)
            {
                _context.OpenInGameMenu();
            }
        }

        public void OpenPluginOptionsMenu(bool fromGameplay)
        {
            _context._pluginOptionsMenuOpen = true;
            _context._pluginOptionsMenuOpenedFromGameplay = fromGameplay;
            _context._pendingPluginOptionsKeyItem = null;
            _context._selectedPluginOptionsPluginId = null;
            _context._optionsMenuOpen = false;
            _context._pluginOptionsHoverIndex = -1;
            _context._pluginOptionsScrollOffset = 0;
            _optionsHoverIndex = -1;
            _context._editingPlayerName = false;
            _context._playerNameEditBuffer = _context._world.LocalPlayer.DisplayName;
        }

        public void ClosePluginOptionsMenu()
        {
            var reopenFromGameplay = _context._pluginOptionsMenuOpenedFromGameplay;
            _context._pluginOptionsMenuOpen = false;
            _context._pluginOptionsMenuOpenedFromGameplay = false;
            _context._pendingPluginOptionsKeyItem = null;
            _context._selectedPluginOptionsPluginId = null;
            _context._pluginOptionsHoverIndex = -1;
            _context._pluginOptionsScrollOffset = 0;
            OpenOptionsMenu(reopenFromGameplay);
        }

        public void OpenControlsMenu(bool fromGameplay)
        {
            _context._controlsMenuOpen = true;
            _context._controlsMenuOpenedFromGameplay = fromGameplay;
            _context._controlsHoverIndex = -1;
            _context._controlsScrollOffset = 0;
            _context._controlsPageIndex = 0;
            _context._pendingControlsBinding = null;
            _context._pendingControllerControlsBinding = null;
            _context._optionsMenuOpen = false;
            _context._pluginOptionsMenuOpen = false;
            _context._editingPlayerName = false;
        }

        public void CloseControlsMenu()
        {
            var reopenInGameMenu = _context._controlsMenuOpenedFromGameplay && !_context._mainMenuOpen;
            _context._controlsMenuOpen = false;
            _context._controlsMenuOpenedFromGameplay = false;
            _context._controlsHoverIndex = -1;
            _context._controlsScrollOffset = 0;
            _context._pendingControlsBinding = null;
            _context._pendingControllerControlsBinding = null;

            if (_context._mainMenuOpen || reopenInGameMenu)
            {
                OpenOptionsMenu(reopenInGameMenu);
            }
        }

        public void UpdateOptionsMenu(KeyboardState keyboard, MouseState mouse)
        {
            if (_context._accountDialogOpen)
            {
                _context.UpdateAccountDialog(keyboard, mouse);
                return;
            }

            if (_context.IsKeyPressed(keyboard, Keys.Escape))
            {
                if (_context._editingPlayerName)
                {
                    _context._editingPlayerName = false;
                    _context._playerNameEditBuffer = _context._world.LocalPlayer.DisplayName;
                    return;
                }

                CloseOptionsMenu();
                return;
            }

            if (_context._editingPlayerName)
            {
                var valueClickPressed = mouse.LeftButton == ButtonState.Pressed && _context._previousMouse.LeftButton != ButtonState.Pressed;
                if (valueClickPressed)
                {
                    GetOptionsMenuPanelLayout(out _, out var valueListBounds, out _, out _, out var valueRowHeight);
                    var rowBounds = new Rectangle(
                        valueListBounds.X,
                        valueListBounds.Y + ((0 - _optionsScrollOffset) * valueRowHeight),
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

                        if (valueBoxBounds.Contains(mouse.Position) && _context.IsTextFieldDoubleClick(TextFieldClickTarget.OptionsPlayerName))
                        {
                            _context.SelectAllTextInActiveField(TextFieldClickTarget.OptionsPlayerName);
                        }
                        else
                        {
                            _context.ResetTextFieldClickTarget();
                        }
                    }
                    else
                    {
                        _context.ResetTextFieldClickTarget();
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
            var optionsScrollOffset = _optionsScrollOffset;
            if (_context.TryHandleScrollbarDrag(
                    mouse,
                    _context._previousMouse,
                    ScrollbarOwners.OptionsMenu,
                    trackBounds,
                    ref optionsScrollOffset,
                    actions.Count,
                    visibleRowCount))
            {
                _optionsScrollOffset = optionsScrollOffset;
                return;
            }

            _optionsScrollOffset = optionsScrollOffset;

            const float optionsRowValueHorizontalPadding = 14f;
            const float optionsRowValueTextScale = 1f;
            var wheelDelta = mouse.ScrollWheelValue - _context._previousMouse.ScrollWheelValue;
            if (wheelDelta != 0 && listBounds.Contains(mouse.Position))
            {
                var stepCount = Math.Max(1, Math.Abs(wheelDelta) / 120);
                _optionsScrollOffset = Math.Clamp(
                    _optionsScrollOffset + (wheelDelta > 0 ? -stepCount : stepCount),
                    0,
                    Math.Max(0, actions.Count - visibleRowCount));
            }

            if (mouse.LeftButton == ButtonState.Pressed && _context._previousMouse.LeftButton != ButtonState.Pressed)
            {
                for (var tabIndex = 0; tabIndex < tabBounds.Length; tabIndex += 1)
                {
                    if (tabBounds[tabIndex].Contains(mouse.Position))
                    {
                        _context._optionsPageIndex = tabIndex;
                        _optionsScrollOffset = 0;
                        _optionsHoverIndex = -1;
                        if (GetOptionsMenuTab(tabIndex) == OptionsMenuTab.Account)
                        {
                            _context.BeginAccountProfileRefresh(silent: true);
                        }
                        return;
                    }
                }
            }

            if (_context.IsControllerMenuInputActive())
            {
                if (_optionsHoverIndex < 0 && actions.Count > 0)
                {
                    _optionsHoverIndex = 0;
                }
            }
            else
            {
                _optionsHoverIndex = -1;
            }

            if (_context.ShouldUseMouseMenuHover(mouse) && backBounds.Contains(mouse.Position))
            {
                _optionsHoverIndex = actions.Count;
            }
            else if (_context.ShouldUseMouseMenuHover(mouse) && listBounds.Contains(mouse.Position))
            {
                var visibleIndex = (mouse.Y - listBounds.Y) / rowHeight;
                var hoverIndex = _optionsScrollOffset + visibleIndex;
                if (visibleIndex >= 0 && hoverIndex >= 0 && hoverIndex < actions.Count)
                {
                    _optionsHoverIndex = hoverIndex;
                }
            }

            var clickPressed = mouse.LeftButton == ButtonState.Pressed && _context._previousMouse.LeftButton != ButtonState.Pressed;
            if (!clickPressed || _optionsHoverIndex < 0)
            {
                return;
            }

            if (_optionsHoverIndex == actions.Count)
            {
                CloseOptionsMenu();
                return;
            }

            var selectedAction = actions[_optionsHoverIndex];
            if (IsOptionsStepperRow(selectedAction))
            {
                var visibleIndex = _optionsHoverIndex - _optionsScrollOffset;
                var rowBounds = new Rectangle(listBounds.X, listBounds.Y + (visibleIndex * rowHeight), listBounds.Width, rowHeight);
                var valueRightX = rowBounds.Right - optionsRowValueHorizontalPadding;
                var displayValue = $"< {selectedAction.Value} >";
                var valueWidth = _context.MeasureBitmapFontWidth(displayValue, optionsRowValueTextScale);
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
            if (!_context.IsControllerMenuInputActive())
            {
                return false;
            }

            if (_context.IsControllerMenuBackPressed())
            {
                CloseOptionsMenu();
                return true;
            }

            var handled = false;
            if (_context.TryConsumeControllerMenuNavigation(out var horizontalStep, out var verticalStep))
            {
                if (verticalStep != 0)
                {
                    _optionsHoverIndex = MoveControllerMenuSelection(
                        _optionsHoverIndex,
                        actions.Count + 1,
                        verticalStep);
                    EnsureOptionsControllerSelectionVisible(actions.Count, visibleRowCount);
                    handled = true;
                }
                else if (horizontalStep != 0)
                {
                    if (_optionsHoverIndex >= 0 && _optionsHoverIndex < actions.Count)
                    {
                        var selectedAction = actions[_optionsHoverIndex];
                        if (TryAdjustOptionsStepperValue(selectedAction, horizontalStep))
                        {
                            return true;
                        }
                    }

                    _context._optionsPageIndex = Math.Clamp(_context._optionsPageIndex + horizontalStep, 0, OptionsMenuTabLabels.Length - 1);
                    _optionsScrollOffset = 0;
                    _optionsHoverIndex = actions.Count > 0 ? 0 : -1;
                    if (GetOptionsMenuTab(_context._optionsPageIndex) == OptionsMenuTab.Account)
                    {
                        _context.BeginAccountProfileRefresh(silent: true);
                    }
                    handled = true;
                }
            }

            if (_context.IsControllerMenuConfirmPressed())
            {
                if (_optionsHoverIndex < 0 && actions.Count > 0)
                {
                    _optionsHoverIndex = 0;
                }

                if (_optionsHoverIndex == actions.Count)
                {
                    CloseOptionsMenu();
                    return true;
                }

                if (_optionsHoverIndex >= 0 && _optionsHoverIndex < actions.Count)
                {
                    actions[_optionsHoverIndex].Activate();
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
            if (_optionsHoverIndex < 0 || _optionsHoverIndex >= actionCount)
            {
                return;
            }

            if (_optionsHoverIndex < _optionsScrollOffset)
            {
                _optionsScrollOffset = _optionsHoverIndex;
            }
            else if (_optionsHoverIndex >= _optionsScrollOffset + visibleRowCount)
            {
                _optionsScrollOffset = _optionsHoverIndex - visibleRowCount + 1;
            }
        }

        public void DrawOptionsMenu()
        {
            var viewportWidth = _context.ViewportWidth;
            var viewportHeight = _context.ViewportHeight;
            _context._spriteBatch.Draw(_context._pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black * 0.86f);

            // Draw bottom bar and runners (in animated mode only) - behind everything else
            if (_context.GameplayRuntimeSettings.MenuBackgroundMode != MenuBackgroundMode.Static)
            {
                const int bottomBarHeight = 76;
                var barY = viewportHeight - bottomBarHeight;
                var bottomBarBounds = new Rectangle(0, barY, viewportWidth, bottomBarHeight);
                _context._spriteBatch.Draw(_context._pixel, bottomBarBounds, new Color(0x57, 0x4f, 0x47));
                _context.Menus.MenuBottomBarRunners.Draw(bottomBarBounds);
            }

            var actions = BuildOptionsMenuActions();
            GetOptionsMenuPanelLayout(out var panel, out var listBounds, out var backBounds, out var compactLayout, out var rowHeight);
            var mouse = _context.GetFrameMouseState();
            var visibleRowCount = Math.Max(1, listBounds.Height / rowHeight);
            ClampOptionsScrollOffset(actions.Count, visibleRowCount);

            _context.DrawRoundedRectangleOutline(panel, new Color(59, 51, 46), new Color(213, 205, 188), outlineThickness: 2, radius: 8);

                const float optionsHeaderScale = 1f;
                const float optionsRowTextScale = 1f;
                const float optionsCompactRowTextScale = 1f;
                const float optionsRowHorizontalPadding = 14f;
                const float optionsRowColumnGap = 20f;

                _context.DrawBitmapFontText("Options", new Vector2(listBounds.X, panel.Y + 14f), Color.White, optionsHeaderScale);
                DrawOptionsMenuTabs(panel, compactLayout);
            if (actions.Count > visibleRowCount)
            {
                var visibleStart = _optionsScrollOffset + 1;
                var visibleEnd = Math.Min(actions.Count, _optionsScrollOffset + visibleRowCount);
                _context.DrawBitmapFontText(
                    $"{visibleStart}-{visibleEnd}/{actions.Count}",
                    new Vector2(listBounds.Right - (compactLayout ? 78f : 96f), panel.Y + 14f),
                    new Color(186, 186, 186),
                    1f);
            }

            var endIndex = Math.Min(actions.Count, _optionsScrollOffset + visibleRowCount);
            var rowSpacing = compactLayout ? 4 : 6;
            var rowHeightWithoutSpacing = rowHeight - rowSpacing;
            for (var index = _optionsScrollOffset; index < endIndex; index += 1)
            {
                var visibleRow = index - _optionsScrollOffset;
                var rowBounds = new Rectangle(listBounds.X, listBounds.Y + (visibleRow * rowHeight), listBounds.Width, rowHeightWithoutSpacing);
                var isHovered = index == _optionsHoverIndex || rowBounds.Contains(mouse.Position);
                var row = actions[index];
                _context._spriteBatch.Draw(_context._pixel, rowBounds, row.IsGroupHeader
                    ? isHovered ? new Color(101, 83, 62) : new Color(80, 66, 51)
                    : isHovered ? new Color(36, 32, 29) : new Color(54, 47, 41));

                var textScale = compactLayout ? optionsCompactRowTextScale : optionsRowTextScale;
                var textY = rowBounds.Y + ((rowBounds.Height - _context.MeasureBitmapFontHeight(textScale)) * 0.5f);

                var labelX = rowBounds.X + optionsRowHorizontalPadding;
                var valueRightX = rowBounds.Right - optionsRowHorizontalPadding;
                var displayValue = row.Label switch
                {
                    _ when IsOptionsStepperRow(row) => $"< {row.Value} >",
                    _ => row.Value,
                };
                var trimmedValue = _context.TrimBitmapMenuText(displayValue, rowBounds.Width * 0.42f, textScale);
                var valueWidth = _context.MeasureBitmapFontWidth(trimmedValue, textScale);
                var valueX = valueRightX - valueWidth;
                var labelMaxWidth = Math.Max(40f, valueX - labelX - optionsRowColumnGap);
                var trimmedLabel = _context.TrimBitmapMenuText(row.Label, labelMaxWidth, textScale);

                _context.DrawBitmapFontText(trimmedLabel, new Vector2(labelX, textY), row.IsGroupHeader ? new Color(239, 201, 104) : Color.White, textScale);

                if (row.Label == "Player Name" && _context._editingPlayerName)
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

                    _context.DrawMenuInputBoxScaled(
                        valueBoxBounds,
                        _context._playerNameEditBuffer,
                        true,
                        textScale,
                        _context._playerNameEditCursorIndex,
                        _context._playerNameEditSelectionStart);
                }
                else if (!string.IsNullOrWhiteSpace(trimmedValue))
                {
                    _context.DrawBitmapFontText(trimmedValue, new Vector2(valueX, textY), Color.White, textScale);
                }
            }

            if (actions.Count > visibleRowCount)
            {
                var trackBounds = new Rectangle(panel.Right - 20, listBounds.Y, 8, listBounds.Height);
                _context._spriteBatch.Draw(_context._pixel, trackBounds, new Color(22, 24, 28));

                var maxOffset = Math.Max(1, actions.Count - visibleRowCount);
                var thumbHeight = Math.Max(24, (int)MathF.Round(trackBounds.Height * (visibleRowCount / (float)actions.Count)));
                var thumbTravel = Math.Max(0, trackBounds.Height - thumbHeight);
                var thumbY = trackBounds.Y + (int)MathF.Round((_optionsScrollOffset / (float)maxOffset) * thumbTravel);
                var thumbBounds = new Rectangle(trackBounds.X, thumbY, trackBounds.Width, thumbHeight);
                _context._spriteBatch.Draw(_context._pixel, thumbBounds, new Color(105, 105, 105));
            }

            var backHovered = _optionsHoverIndex == actions.Count || backBounds.Contains(mouse.Position);
            _context.DrawMenuButtonScaled(backBounds, "Back", backHovered, 1f);
            _context.DrawAccountDialog();
        }

        private List<OptionsMenuAction> BuildOptionsMenuActions()
        {
            var currentTab = GetOptionsMenuTab(_context._optionsPageIndex);
            if (currentTab == OptionsMenuTab.Audio)
            {
                var audioActions = BuildAudioOptionsActions();
                if (OpenGarrison.ClientShared.ClientDistribution.IsGg2Only)
                    audioActions.RemoveAll(action => action.Label is not (MasterVolumeLabel or "Mute All Audio (F12)" or SoundEffectsVolumeLabel or "Music" or MenuMusicVolumeLabel or InGameMusicVolumeLabel));
                return audioActions;
            }
            var allActions = new List<OptionsMenuAction>
            {
                // Graphics: display, rendering, and client-side visual presentation.
                new("Display Mode", OperatingSystem.IsBrowser() ? "Browser" : Game1.GetDisplayModeLabel(_context.DisplayRuntimeSettings.DisplayMode), _context.CycleDisplayModeSetting, OptionsMenuTab.Graphics),
                new("Aspect Ratio", Game1.GetIngameResolutionLabel(_context.DisplayRuntimeSettings.IngameResolution), _context.CycleIngameResolutionSetting, OptionsMenuTab.Graphics),
                new("Camera panning", _context.GameplayRuntimeSettings.CameraPanningEnabled ? "Enabled" : "Disabled", _context.ToggleCameraPanningSetting, OptionsMenuTab.Graphics),
                new("Window Size", OperatingSystem.IsBrowser() ? "Browser" : Game1.GetWindowSizeLabel(_context.DisplayRuntimeSettings.WindowSize), _context.CycleWindowSizeSetting, OptionsMenuTab.Graphics),
                new("Cursor Size", Game1.GetCursorSizeLabel(_context.HudRuntimeSettings.CursorSizePercent), _context.CycleCursorSizeSetting, OptionsMenuTab.Graphics, _context.AdjustCursorSizeSetting),
                new("Menu Background", GetMenuBackgroundModeLabel(_context.GameplayRuntimeSettings.MenuBackgroundMode), _context.CycleMenuBackgroundModeSetting, OptionsMenuTab.Graphics),
                new("Particles", GetParticleModeLabel(_context.GameplayRuntimeSettings.ParticleMode), _context.CycleParticleModeSetting, OptionsMenuTab.Graphics),
                new("Flame Style", GetFlameRenderModeLabel(_context.GameplayRuntimeSettings.FlameRenderMode), _context.CycleFlameRenderModeSetting, OptionsMenuTab.Graphics),
                new("Blood Style", GetBloodRenderModeLabel(_context.GameplayRuntimeSettings.BloodRenderMode), _context.CycleBloodRenderModeSetting, OptionsMenuTab.Graphics),
                new("Dynamic Ragdoll", _context.GameplayRuntimeSettings.DynamicRagdollEnabled ? "Enabled" : "Disabled", _context.ToggleDynamicRagdollSetting, OptionsMenuTab.Graphics),
                new("Burn Charred Corpses", _context.GameplayRuntimeSettings.BurnCharredCorpsesEnabled ? "Enabled" : "Disabled", _context.ToggleBurnCharredCorpsesSetting, OptionsMenuTab.Graphics),
                new("Gibs", GetGibLevelLabel(_context.GameplayRuntimeSettings.GibLevel), _context.CycleGibLevelSetting, OptionsMenuTab.Graphics),
                new("Blood Amount", GetBloodAmountLabel(_context.GameplayRuntimeSettings.BloodAmountLevel), _context.CycleBloodAmountSetting, OptionsMenuTab.Graphics),
                new("Blood Persistence", $"{_context.GameplayRuntimeSettings.BloodPersistenceSeconds}s", () => _context.AdjustBloodPersistenceSeconds(1), OptionsMenuTab.Graphics, _context.AdjustBloodPersistenceSeconds),
                new("Corpse Fade", GetCorpseFadeModeLabel(_context.GameplayRuntimeSettings.CorpseFadeMode), _context.CycleCorpseFadeModeSetting, OptionsMenuTab.Graphics),
                new("Stuck Arrows", _context.GameplayRuntimeSettings.StuckArrowsEnabled ? "Enabled" : "Disabled", _context.ToggleStuckArrowsSetting, OptionsMenuTab.Graphics),
                new("Weapon Bob", GetWeaponBobModeLabel(_context.GameplayRuntimeSettings.WeaponBobMode), _context.CycleWeaponBobSetting, OptionsMenuTab.Graphics),
                new("Corpses", GetCorpseDurationLabel(_context.GameplayRuntimeSettings.CorpseDurationMode), _context.CycleCorpseDurationSetting, OptionsMenuTab.Graphics),
                new("Sprite Shadow", _context.GameplayRuntimeSettings.SpriteDropShadowEnabled ? "Enabled" : "Disabled", _context.ToggleSpriteDropShadowSetting, OptionsMenuTab.Graphics),
                new("Weapon Rotation", _context.GameplayRuntimeSettings.PixelPerfectWeaponRotation ? "Pixel-Perfect" : "High-Res", _context.ToggleWeaponRotationStyleSetting, OptionsMenuTab.Graphics),
                new("Weapon Rotation Source", _context.GameplayRuntimeSettings.UseLocalWeaponRotation ? "Local (snappier)" : "Remote (accurate)", _context.ToggleWeaponRotationSourceSetting, OptionsMenuTab.Graphics),
                new("Uber Outlines", _context.GameplayRuntimeSettings.ShowUberOutlinesEnabled ? "Enabled" : "Disabled", _context.ToggleUberOutlinesSetting, OptionsMenuTab.Graphics),
                new("Projectile Team Tint", _context.GameplayRuntimeSettings.ProjectileTeamTintEnabled ? "Enabled" : "Disabled", _context.ToggleProjectileTeamTintSetting, OptionsMenuTab.Graphics),
                new("Frame Limit", GetFrameRateLimitLabel(_context.DisplayRuntimeSettings.FrameRateLimit), _context.CycleFrameRateLimitSetting, OptionsMenuTab.Graphics),
                new("V Sync", _context._graphics.SynchronizeWithVerticalRetrace ? "Enabled" : "Disabled", _context.ToggleVSyncSetting, OptionsMenuTab.Graphics),
                new("Reset Window Size", string.Empty, _context.ResetWindowSize, OptionsMenuTab.Graphics),

                // Controls: bindings and controller aiming behavior.
                new("Keyboard & Mouse", string.Empty, OpenControlsMenuFromOptions, OptionsMenuTab.Controls),
                new("Swap Weapons", _context.GetSwapWeaponsBindingLabel(), _context.CycleSwapWeaponsBindingSetting, OptionsMenuTab.Controls),
                new("Scroll Wheel Weapon Swap", _context._inputBindings.ScrollWheelWeaponSwapEnabled ? "On" : "Off", _context.ToggleScrollWheelWeaponSwapSetting, OptionsMenuTab.Controls),
                new("Controller Input", Game1.GetControllerInputModeLabel(_context._clientSettings.ControllerInputMode), _context.CycleControllerInputModeSetting, OptionsMenuTab.Controls),
                new("Controller Reticle", Game1.GetControllerReticleModeLabel(_context._clientSettings.ControllerReticleMode), _context.CycleControllerReticleModeSetting, OptionsMenuTab.Controls),
                new("Controller Aim Assist", _context._clientSettings.ControllerAimAssistEnabled ? "Enabled" : "Disabled", _context.ToggleControllerAimAssistSetting, OptionsMenuTab.Controls),
                new("Flick to Change Directions", _context._clientSettings.ControllerFlickToChangeDirections ? "Enabled" : "Disabled", _context.ToggleControllerFlickToChangeDirectionsSetting, OptionsMenuTab.Controls),
                new(ControllerAimAssistStrengthLabel, Game1.GetControllerPercentLabel(OpenGarrisonPreferencesDocument.NormalizeControllerAimAssistStrength(_context._clientSettings.ControllerAimAssistStrength)), _context.CycleControllerAimAssistStrengthSetting, OptionsMenuTab.Controls, step => _context.AdjustControllerAimAssistStrengthSetting(step * 0.1f)),
                new(ControllerAimDeadzoneLabel, Game1.GetControllerPercentLabel(OpenGarrisonPreferencesDocument.NormalizeControllerAimDeadzone(_context._clientSettings.ControllerAimDeadzone)), _context.CycleControllerAimDeadzoneSetting, OptionsMenuTab.Controls, step => _context.AdjustControllerAimDeadzoneSetting(step * 0.05f)),
                new(ControllerScopedAimSpeedLabel, Game1.GetControllerSpeedLabel(OpenGarrisonPreferencesDocument.NormalizeControllerScopedPrecisionSpeed(_context._clientSettings.ControllerScopedPrecisionSpeed)), _context.CycleControllerScopedPrecisionSpeedSetting, OptionsMenuTab.Controls, step => _context.AdjustControllerScopedPrecisionSpeedSetting(step * 30f)),
                new(ControllerAimDistanceTier1Label, Game1.GetControllerPixelsLabel(OpenGarrisonPreferencesDocument.NormalizeControllerAimDistance(_context._clientSettings.ControllerAimDistanceTier1, OpenGarrisonPreferencesDocument.DefaultControllerAimDistanceTier1)), _context.CycleControllerAimDistanceTier1Setting, OptionsMenuTab.Controls, step => _context.AdjustControllerAimDistanceTier1Setting(step * 16f)),
                new(ControllerAimDistanceTier2Label, Game1.GetControllerPixelsLabel(OpenGarrisonPreferencesDocument.NormalizeControllerAimDistance(_context._clientSettings.ControllerAimDistanceTier2, OpenGarrisonPreferencesDocument.DefaultControllerAimDistanceTier2)), _context.CycleControllerAimDistanceTier2Setting, OptionsMenuTab.Controls, step => _context.AdjustControllerAimDistanceTier2Setting(step * 16f)),
                new(ControllerAimDistanceTier3Label, Game1.GetControllerPixelsLabel(OpenGarrisonPreferencesDocument.NormalizeControllerAimDistance(_context._clientSettings.ControllerAimDistanceTier3, OpenGarrisonPreferencesDocument.DefaultControllerAimDistanceTier3)), _context.CycleControllerAimDistanceTier3Setting, OptionsMenuTab.Controls, step => _context.AdjustControllerAimDistanceTier3Setting(step * 16f)),

                // HUD: in-match overlays, player presentation, and feedback effects.
                new("Healer Radar", _context.HudRuntimeSettings.HealerRadarEnabled ? "Enabled" : "Disabled", _context.ToggleHealerRadarSetting, OptionsMenuTab.Hud),
                new("Show Healer", _context.HudRuntimeSettings.ShowHealerEnabled ? "Enabled" : "Disabled", _context.ToggleShowHealerSetting, OptionsMenuTab.Hud),
                new("Show Healing", _context.HudRuntimeSettings.ShowHealingEnabled ? "Enabled" : "Disabled", _context.ToggleShowHealingSetting, OptionsMenuTab.Hud),
                new("Health Bar", _context.HudRuntimeSettings.ShowHealthBarEnabled ? "Enabled" : "Disabled", _context.ToggleShowHealthBarSetting, OptionsMenuTab.Hud),
                new("Shield Bar", _context.HudRuntimeSettings.ShowShieldBarEnabled ? "Enabled" : "Disabled", _context.ToggleShowShieldBarSetting, OptionsMenuTab.Hud),
                new("Weapon HUD", Game1.GetHudWeaponDisplayModeLabel(_context.HudRuntimeSettings.HudShowOnlyActiveWeapon), _context.ToggleHudWeaponDisplayModeSetting, OptionsMenuTab.Hud),
                new("Build Menu Style", Game1.GetBuildMenuStyleLabel(_context._clientSettings.BuildMenuStyle), _context.CycleBuildMenuStyleSetting, OptionsMenuTab.Hud),
                new("Overhead Chat", _context.HudRuntimeSettings.OverheadChatEnabled ? "Enabled" : "Disabled", _context.ToggleOverheadChatSetting, OptionsMenuTab.Hud),
                new("Low HP Color", Game1.GetLowHealthColorModeLabel(_context.HudRuntimeSettings.LowHealthColorMode), _context.CycleLowHealthColorModeSetting, OptionsMenuTab.Hud),
                new("Playercard Size", Game1.GetPlayerCardSizeLabel(_context.HudRuntimeSettings.PlayerCardSizeMode), _context.CyclePlayerCardSizeSetting, OptionsMenuTab.Hud),
                new("Portrait Rumble", _context.HudRuntimeSettings.PortraitRumbleEnabled ? "Enabled" : "Disabled", _context.TogglePortraitRumbleSetting, OptionsMenuTab.Hud),
                new("MVP Art", _context.HudRuntimeSettings.PostGameMvpArtEnabled ? "Enabled" : "Disabled", _context.TogglePostGameMvpArtSetting, OptionsMenuTab.Hud),
                new("Damage Vignette", _context.HudRuntimeSettings.DamageVignetteEnabled ? "Enabled" : "Disabled", _context.ToggleDamageVignetteSetting, OptionsMenuTab.Hud),
                new("Vignette Intensity", Game1.GetDamageVignetteIntensityLabel(_context.HudRuntimeSettings.DamageVignetteIntensityPercent), _context.CycleDamageVignetteIntensitySetting, OptionsMenuTab.Hud),
                new("Player Names", _context.HudRuntimeSettings.ShowPlayerNamesEnabled ? "Enabled" : "Disabled", _context.ToggleShowPlayerNamesSetting, OptionsMenuTab.Hud),
                new("Persistent Name", _context.HudRuntimeSettings.ShowPersistentSelfNameEnabled ? "Enabled" : "Disabled", _context.TogglePersistentSelfNameSetting, OptionsMenuTab.Hud),
                new("Edit HUD", _context._mainMenuOpen ? "In context only" : string.Empty, OpenHudEditorFromOptions, OptionsMenuTab.Hud),

                // Gameplay: rules and simulation behavior that affect the match itself.
                new("Player Name", _context._editingPlayerName ? GetTextWithCursor(_context._playerNameEditBuffer, _context._playerNameEditCursorIndex) : _context._world.LocalPlayer.DisplayName, _context.BeginEditingPlayerName, OptionsMenuTab.Gameplay),
                new("Kill Cam", _context.GameplayRuntimeSettings.KillCamEnabled ? "Enabled" : "Disabled", _context.ToggleKillCamSetting, OptionsMenuTab.Gameplay),
                new("Network Smoothing", _context.GameplayRuntimeSettings.PositionSmoothingEnabled ? "Enabled" : "Disabled", _context.TogglePositionSmoothingSetting, OptionsMenuTab.Gameplay),
                new("Enable Prediction", _context.GameplayRuntimeSettings.EnablePrediction ? "Enabled" : "Disabled", _context.TogglePredictionSetting, OptionsMenuTab.Gameplay),

                // Account: portable identity, recovery, points, and future cosmetic currency.
                new("Friend Code", _context._clientIdentity.FriendCode, NoOp, OptionsMenuTab.Account),
                new("Protection", _context._accountIsProtected ? "Recovery enabled" : "Not protected", NoOp, OptionsMenuTab.Account),
                new("Lifetime Points", $"{_context._accountLifetimePoints:N0}", NoOp, OptionsMenuTab.Account),
                new("Wallet", $"{_context._accountWalletBalance:N0}", NoOp, OptionsMenuTab.Account),
                new(
                    "Global Rank",
                    !_context._accountGlobalRankKnown
                        ? "Not checked"
                        : _context._accountGlobalRank > 0 ? $"#{_context._accountGlobalRank:N0}" : "Unranked",
                    NoOp,
                    OptionsMenuTab.Account),
                new("Recovery Key", _context.GetAccountRecoveryKeyDisplay(), NoOp, OptionsMenuTab.Account),
                new("Refresh Account", _context.IsAccountOperationPending ? "Working..." : string.Empty, () => _context.BeginAccountProfileRefresh(silent: false), OptionsMenuTab.Account),
                new(_context._accountIsProtected ? "Replace Recovery Key" : "Protect This Account", string.Empty, _context.BeginProtectAccount, OptionsMenuTab.Account),
                new("Shorten Friend Code", _context.CanShortenAccountFriendCode ? "Create 8-char code" : "Already short", _context.BeginShortenAccountFriendCode, OptionsMenuTab.Account),
                new("Sign In On This Device", string.Empty, _context.OpenAccountLoginDialog, OptionsMenuTab.Account),
                new("Account Status", _context.GetAccountStatusDisplay(), NoOp, OptionsMenuTab.Account),
            };

            if (_context.IsCrtSettingsUnlockedForSession)
            {
                var crtActions = new List<OptionsMenuAction>
                {
                    new("CRT", _crtOptionsExpanded ? "[-]" : "[+]", ToggleCrtOptionsGroup, OptionsMenuTab.Graphics, IsGroupHeader: true),
                };
                if (_crtOptionsExpanded)
                {
                    crtActions.AddRange(new OptionsMenuAction[]
                    {
                        new("CRT Filter", _context.GetCrtPresetLabel(), _context.CycleCrtPresetSetting, OptionsMenuTab.Graphics),
                        new("CRT Quality", _context.GetCrtQualityStatusLabel(), _context.CycleCrtQualitySetting, OptionsMenuTab.Graphics),
                        new("CRT Signal", _context.GetCrtSignalModeLabel(), _context.CycleCrtSignalModeSetting, OptionsMenuTab.Graphics),
                        new("CRT Curvature", _context._clientSettings.CrtCurvatureEnabled ? "Enabled" : "Disabled", _context.ToggleCrtCurvatureSetting, OptionsMenuTab.Graphics),
                        new("CRT Brightness", $"{_context._clientSettings.CrtBrightnessPercent}%", _context.CycleCrtBrightnessSetting, OptionsMenuTab.Graphics, step => _context.AdjustCrtBrightnessSetting(step * 5)),
                        new("Reset CRT Settings", string.Empty, _context.ResetCrtSettings, OptionsMenuTab.Graphics),
                    });
                }

                allActions.InsertRange(2, crtActions);
            }

            if (!IsRestrictedBrowserEdition && _context.HasClientPluginOptions())
            {
                allActions.Add(new OptionsMenuAction("Plugin Options", string.Empty, OpenPluginOptionsMenuFromOptions, OptionsMenuTab.Plugins));
            }

            allActions.Add(new OptionsMenuAction("Version", GetApplicationVersionLabel(), NoOp, OptionsMenuTab.Plugins));
            if (IsRestrictedBrowserEdition && !_context._optionsMenuOpenedFromGameplay)
                allActions.Add(new OptionsMenuAction("Credits", string.Empty, _context.OpenCreditsMenu, OptionsMenuTab.Plugins));

            if (currentTab == OptionsMenuTab.Replays)
            {
                allActions.AddRange(BuildReplayMenuActions());
            }

            var filteredActions = new List<OptionsMenuAction>(allActions.Count);

            foreach (var action in allActions)
            {
                if (OperatingSystem.IsBrowser() && action.Label is ("Display Mode" or "Window Size" or "Reset Window Size"
                    or "CRT" or "CRT Filter" or "CRT Quality" or "CRT Signal" or "CRT Curvature" or "CRT Brightness" or "Reset CRT Settings"))
                    continue;
                if (OpenGarrison.ClientShared.ClientDistribution.IsGg2Only && !IsGg2OnlyOption(action))
                    continue;
                if (action.Tab == currentTab)
                {
                    filteredActions.Add(action);
                }
            }

            return filteredActions;
        }

        private static bool IsGg2OnlyOption(OptionsMenuAction action) => action.Tab switch
        {
            OptionsMenuTab.Graphics => action.Label is "Display Mode" or "Aspect Ratio"
                or "CRT" or "CRT Filter" or "CRT Quality" or "CRT Signal" or "CRT Curvature" or "CRT Brightness" or "Reset CRT Settings"
                or "Window Size" or "Cursor Size" or "Particles" or "Flame Style" or "Blood Style" or "Blood Amount" or "Blood Persistence"
                or "Dynamic Ragdoll" or "Corpse Fade" or "Gibs"
                or "Stuck Arrows" or "Corpses" or "Sprite Shadow" or "Weapon Rotation"
                or "Frame Limit" or "V Sync" or "Reset Window Size",
            OptionsMenuTab.Controls => action.Label is "Keyboard & Mouse" or "Controller Input"
                or "Controller Reticle" or "Stick Deadzone" or "Scoped Aim Speed",
            OptionsMenuTab.Hud => action.Label is "Healer Radar" or "Show Healer" or "Show Healing"
                or "Health Bar" or "Overhead Chat" or "Player Names",
            OptionsMenuTab.Gameplay => action.Label is "Player Name" or "Network Smoothing",
            _ => false,
        };

        private List<OptionsMenuAction> BuildAudioOptionsActions()
        {
            var actions = new List<OptionsMenuAction>
            {
                new("Game", _expandedAudioGroup == AudioOptionsGroup.Game ? "[-]" : "[+]",
                    () => ToggleAudioOptionsGroup(AudioOptionsGroup.Game), OptionsMenuTab.Audio, IsGroupHeader: true),
            };
            if (_expandedAudioGroup == AudioOptionsGroup.Game)
            {
                actions.AddRange(new OptionsMenuAction[]
                {
                    new(MasterVolumeLabel, $"{_context.AudioRuntimeSettings.MasterVolumePercent}%", () => _context.AdjustMasterVolume(5), OptionsMenuTab.Audio, step => _context.AdjustMasterVolume(step * 5)),
                    new("Mute All Audio (F12)", _context.AudioRuntimeSettings.AudioMuted ? "Muted" : "Unmuted", _context.ToggleAudioMuteSetting, OptionsMenuTab.Audio),
                    new(SoundEffectsVolumeLabel, $"{_context.AudioRuntimeSettings.SoundEffectsVolumePercent}%", () => _context.AdjustSoundEffectsVolume(5), OptionsMenuTab.Audio, step => _context.AdjustSoundEffectsVolume(step * 5)),
                    new("Music", GetMusicModeLabel(_context.AudioRuntimeSettings.MusicMode), _context.CycleMusicModeSetting, OptionsMenuTab.Audio),
                    new(MenuMusicVolumeLabel, $"{_context.AudioRuntimeSettings.MenuMusicVolumePercent}%", () => _context.AdjustMenuMusicVolume(5), OptionsMenuTab.Audio, step => _context.AdjustMenuMusicVolume(step * 5)),
                    new(InGameMusicVolumeLabel, $"{_context.AudioRuntimeSettings.IngameMusicVolumePercent}%", () => _context.AdjustIngameMusicVolume(5), OptionsMenuTab.Audio, step => _context.AdjustIngameMusicVolume(step * 5)),
                    new("Dynamic Music", _context.AudioRuntimeSettings.DynamicMusicEnabled ? "Enabled" : "Disabled", _context.ToggleDynamicMusicSetting, OptionsMenuTab.Audio),
                    new(CombatMusicVolumeLabel, $"{_context.AudioRuntimeSettings.CombatMusicVolumePercent}%", () => _context.AdjustCombatMusicVolume(5), OptionsMenuTab.Audio, step => _context.AdjustCombatMusicVolume(step * 5)),
                    new("Jukebox Volume", $"{_context._voiceSettings.JukeboxVolumePercent}%", () => _context.AdjustVoiceSetting("jukebox", 5), OptionsMenuTab.Audio, step => _context.AdjustVoiceSetting("jukebox", step * 5)),
                    new("Jukebox", _context._voiceSettings.JukeboxMuted ? "Muted" : "Unmuted", _context.ToggleJukeboxMute, OptionsMenuTab.Audio),
                    new("Music Library", OperatingSystem.IsBrowser() ? "Import / Remove" : "Open Folder", _context.ManageJukeboxLibrary, OptionsMenuTab.Audio),
                });
            }
            actions.Add(new("Chat", _expandedAudioGroup == AudioOptionsGroup.Chat ? "[-]" : "[+]",
                () => ToggleAudioOptionsGroup(AudioOptionsGroup.Chat), OptionsMenuTab.Audio, IsGroupHeader: true));
            if (_expandedAudioGroup == AudioOptionsGroup.Chat)
            {
                actions.AddRange(new OptionsMenuAction[]
                {
                    new(_context.GetVoiceMuteActionLabel(), _context._voiceSettings.VoiceMuted ? "Muted" : "Unmuted", _context.ToggleVoiceMute, OptionsMenuTab.Audio),
                    new("Voice Volume", $"{_context._voiceSettings.VoiceVolumePercent}%", () => _context.AdjustVoiceSetting("voice", 5), OptionsMenuTab.Audio, step => _context.AdjustVoiceSetting("voice", step * 5)),
                    new("Spatial Voice", _context._voiceSettings.SpatialVoice ? "On" : "Off", _context.ToggleSpatialVoice, OptionsMenuTab.Audio),
                    new("Microphone Mode", _context.GetVoiceModeLabel(), _context.CycleVoiceMode, OptionsMenuTab.Audio),
                    new("Push to Talk Key", InputBindingsSettings.FormatBinding(_context._inputBindings.PushToTalk), OpenControlsMenuFromOptions, OptionsMenuTab.Audio),
                    new("Microphone", string.IsNullOrEmpty(_context._voiceSettings.MicrophoneName) ? "Default" : _context._voiceSettings.MicrophoneName, _context.CycleVoiceMicrophone, OptionsMenuTab.Audio),
                    new("Microphone Gain", $"{_context._voiceSettings.MicrophoneGainPercent}%", () => _context.AdjustVoiceSetting("gain", 5), OptionsMenuTab.Audio, step => _context.AdjustVoiceSetting("gain", step * 5)),
                    new("Voice Channel", _context.GetVoiceChannelLabel(), _context.ToggleVoiceTeamOnly, OptionsMenuTab.Audio),
                    new("Voice Status", _context.GetVoiceStatusLabel(), NoOp, OptionsMenuTab.Audio),
                });
            }
            return actions;
        }

        private void ToggleAudioOptionsGroup(AudioOptionsGroup group)
        {
            _expandedAudioGroup = _expandedAudioGroup == group ? null : group;
            _optionsScrollOffset = 0;
            _optionsHoverIndex = group == AudioOptionsGroup.Game ? 0 : 1;
        }

        private void ToggleCrtOptionsGroup()
        {
            _crtOptionsExpanded = !_crtOptionsExpanded;
            _optionsScrollOffset = 0;
            var actions = BuildOptionsMenuActions();
            _optionsHoverIndex = Math.Max(0, actions.FindIndex(action => action.Label == "CRT"));
        }

        private static OptionsMenuTab GetOptionsMenuTab(int pageIndex)
        {
            if (OpenGarrison.ClientShared.ClientDistribution.IsGg2Only)
                return pageIndex is >= 0 and < 5 ? (OptionsMenuTab)pageIndex : OptionsMenuTab.Graphics;
            if (IsRestrictedBrowserEdition)
                return pageIndex is >= 0 and < 5 ? (OptionsMenuTab)pageIndex : OptionsMenuTab.Plugins;
            return pageIndex switch
            {
                0 => OptionsMenuTab.Graphics,
                1 => OptionsMenuTab.Audio,
                2 => OptionsMenuTab.Controls,
                3 => OptionsMenuTab.Hud,
                4 => OptionsMenuTab.Gameplay,
                5 => OptionsMenuTab.Replays,
                6 => OptionsMenuTab.Account,
                _ => OptionsMenuTab.Plugins,
            };
        }

        private List<OptionsMenuAction> BuildReplayMenuActions()
        {
            var actions = new List<OptionsMenuAction>();
            if (OperatingSystem.IsBrowser())
            {
                actions.Add(new OptionsMenuAction("Always Record Games:", "Unavailable in browser", NoOp, OptionsMenuTab.Replays));
                actions.Add(new OptionsMenuAction("Replay Browser", "Unavailable in browser", NoOp, OptionsMenuTab.Replays));
                return actions;
            }

            actions.Add(new OptionsMenuAction(
                "Always Record Games:",
                _context._clientSettings.AlwaysRecordGames ? "Yes" : "No (Default)",
                _context.ToggleAlwaysRecordGames,
                OptionsMenuTab.Replays));

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
                    _context.AddConsoleLine($"replay folder skipped: {directory} ({ex.Message})");
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
                ? _context.TryPlayOpenGarrisonDemo(entry.Path, addConsoleFeedback: true)
                : _context.TryPlayLegacyReplay(entry.Path, addConsoleFeedback: true);
            if (!started)
            {
                return;
            }

            _context._menuStatusMessage = string.Empty;
            _context._optionsMenuOpen = false;
            _context._optionsMenuOpenedFromGameplay = false;
            _optionsHoverIndex = -1;
            _optionsScrollOffset = 0;
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
            var minimumButtonWidth = compactLayout ? 58 : 72;
            var buttonWidth = Math.Min(160, Math.Max(minimumButtonWidth, (panel.Width - (padding * 2) - ((tabCount - 1) * spacing)) / tabCount));
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
                var selected = i == _context._optionsPageIndex;
                _context.DrawMenuButtonScaled(tabBounds[i], OptionsMenuTabLabels[i], selected, 1f);
            }
        }

        private void GetOptionsMenuPanelLayout(out Rectangle panel, out Rectangle listBounds, out Rectangle backBounds, out bool compactLayout, out int rowHeight)
        {
            var panelWidth = Math.Min(_context.ViewportWidth - 32, 760);
            var panelHeight = Math.Min(_context.ViewportHeight - 32, _context.ViewportHeight < 540 ? _context.ViewportHeight - 36 : 620);
            panel = new Rectangle(
                (_context.ViewportWidth - panelWidth) / 2,
                (_context.ViewportHeight - panelHeight) / 2,
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
            _optionsScrollOffset = Math.Clamp(
                _optionsScrollOffset,
                0,
                Math.Max(0, rowCount - visibleRowCount));
        }

        private void OpenControlsMenuFromOptions()
        {
            OpenControlsMenu(_context._optionsMenuOpenedFromGameplay);
        }

        private void OpenPluginOptionsMenuFromOptions()
        {
            OpenPluginOptionsMenu(_context._optionsMenuOpenedFromGameplay);
        }

        private void OpenHudEditorFromOptions()
        {
            _context.OpenHudEditor(openedFromOptions: true);
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

        private static string GetWeaponBobModeLabel(WeaponBobMode mode)
        {
            return OpenGarrisonPreferencesDocument.NormalizeWeaponBobMode(mode) == WeaponBobMode.Disabled
                ? "Disabled"
                : "Enabled";
        }

        private static string GetFlameRenderModeLabel(int flameRenderMode)
        {
            return flameRenderMode == 0 ? "Particle" : "Sprite";
        }

        private static string GetBloodRenderModeLabel(int bloodRenderMode)
        {
            return bloodRenderMode == 0 ? "Squib" : "Classic";
        }

        private static string GetCorpseFadeModeLabel(int corpseFadeMode)
        {
            return corpseFadeMode == 0 ? "Regular" : "Dissolve";
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

        private static string GetBloodAmountLabel(int bloodAmountLevel)
        {
            return GetAmountLevelLabel(bloodAmountLevel);
        }

        private static string GetAmountLevelLabel(int amountLevel)
        {
            var level = Math.Clamp(amountLevel, 1, 5);
            var name = level switch
            {
                1 => "Very Low",
                2 => "Low",
                3 => "Medium",
                4 => "High",
                _ => "Maximum",
            };
            return $"{name} ({level * 20}%)";
        }

        private static string GetCorpseDurationLabel(int corpseDurationMode)
        {
            return corpseDurationMode == ClientSettings.CorpseDurationInfinite
                ? "Infinite"
                : "300 ticks";
        }
}
