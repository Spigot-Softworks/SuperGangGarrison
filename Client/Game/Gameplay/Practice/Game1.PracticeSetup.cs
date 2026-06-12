#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace OpenGarrison.Client;

public partial class Game1
{
    private int _practiceSetupControllerIndex;
    private HostSetupMapPreviewState? _practiceMapBrowserPreviewState;
    private string? _practiceMapBrowserPreviewLevelName;

    private readonly record struct PracticeSetupLayout(
        Rectangle Panel,
        Rectangle MapLeftBounds,
        Rectangle MapValueBounds,
        Rectangle MapRightBounds,
        Rectangle TickLeftBounds,
        Rectangle TickValueBounds,
        Rectangle TickRightBounds,
        Rectangle TimeLeftBounds,
        Rectangle TimeValueBounds,
        Rectangle TimeRightBounds,
        Rectangle CapLeftBounds,
        Rectangle CapValueBounds,
        Rectangle CapRightBounds,
        Rectangle RespawnLeftBounds,
        Rectangle RespawnValueBounds,
        Rectangle RespawnRightBounds,
        Rectangle EnemyBotsLeftBounds,
        Rectangle EnemyBotsValueBounds,
        Rectangle EnemyBotsRightBounds,
        Rectangle FriendlyBotsLeftBounds,
        Rectangle FriendlyBotsValueBounds,
        Rectangle FriendlyBotsRightBounds,
        Rectangle SpecialAbilitiesBounds,
        Rectangle VipRulesBounds,
        Rectangle StartBounds,
        Rectangle ClientPowersBounds,
        Rectangle BackBounds,
        bool CompactLayout);

    private readonly record struct PracticeMapBrowserLayout(
        Rectangle Panel,
        Rectangle SearchBounds,
        Rectangle ModeFilterBounds,
        Rectangle BaseFilterBounds,
        Rectangle CustomFilterBounds,
        Rectangle ResetBounds,
        Rectangle ListBounds,
        Rectangle PreviewBounds,
        Rectangle SelectBounds,
        Rectangle CloseBounds,
        int RowHeight,
        int VisibleRowCapacity,
        int ScrollbarWidth);

    private void OpenPracticeSetupMenu()
    {
        CloseInGameMenu();
        _practiceSetupOpen = true;
        _manualConnectOpen = false;
        CloseHostSetupMenu();
        CloseLobbyBrowser(clearStatus: false);
        _optionsMenuOpen = false;
        _pluginOptionsMenuOpen = false;
        _controlsMenuOpen = false;
        _pendingControlsBinding = null;
        _pendingControllerControlsBinding = null;
        CloseCreditsMenu();
        _clientPowersOpen = false;
        _clientPowersOpenedFromGameplay = false;
        _editingPlayerName = false;
        _menuStatusMessage = string.Empty;
        _practiceSetupControllerIndex = 0;
        _practiceSetupState.CloseMapBrowser();
        ClosePracticeMapBrowserPreview();

        _practiceMapEntries = BuildPracticeMapEntries();
        if (IsPracticeSessionActive)
        {
            SelectPracticeMapEntry(_world.Level.Name);
        }

        NormalizePracticeSetupState();
        OpenPracticeMapBrowser();
    }

    private void UpdatePracticeSetupMenu(KeyboardState keyboard, MouseState mouse)
    {
        var layout = GetPracticeSetupLayout();
        if (_practiceSetupState.MapBrowserOpen)
        {
            UpdatePracticeMapBrowserMenu(keyboard, mouse, GetPracticeMapBrowserLayout());
            return;
        }

        if (IsKeyPressed(keyboard, Keys.Escape) || IsControllerMenuBackPressed())
        {
            ClosePracticeMapBrowser();
            _practiceSetupOpen = false;
            return;
        }

        if (IsKeyPressed(keyboard, Keys.Enter))
        {
            TryStartPracticeFromSetup();
            return;
        }

        if (TryUpdatePracticeSetupControllerInput())
        {
            return;
        }

        var clickPressed = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton != ButtonState.Pressed;
        if (!clickPressed)
        {
            return;
        }

        var point = new Point(mouse.X, mouse.Y);
        if (layout.MapLeftBounds.Contains(point))
        {
            _practiceSetupControllerIndex = 0;
            CyclePracticeMap(-1);
        }
        else if (layout.MapRightBounds.Contains(point))
        {
            _practiceSetupControllerIndex = 0;
            CyclePracticeMap(1);
        }
        else if (layout.MapValueBounds.Contains(point))
        {
            _practiceSetupControllerIndex = 0;
            OpenPracticeMapBrowser();
        }
        else if (layout.TickLeftBounds.Contains(point))
        {
            _practiceSetupControllerIndex = 1;
            CyclePracticeTickRate(-1);
        }
        else if (layout.TickRightBounds.Contains(point))
        {
            _practiceSetupControllerIndex = 1;
            CyclePracticeTickRate(1);
        }
        else if (layout.TimeLeftBounds.Contains(point))
        {
            _practiceSetupControllerIndex = 2;
            CyclePracticeTimeLimit(-1);
        }
        else if (layout.TimeRightBounds.Contains(point))
        {
            _practiceSetupControllerIndex = 2;
            CyclePracticeTimeLimit(1);
        }
        else if (layout.CapLeftBounds.Contains(point))
        {
            _practiceSetupControllerIndex = 3;
            CyclePracticeCapLimit(-1);
        }
        else if (layout.CapRightBounds.Contains(point))
        {
            _practiceSetupControllerIndex = 3;
            CyclePracticeCapLimit(1);
        }
        else if (layout.RespawnLeftBounds.Contains(point))
        {
            _practiceSetupControllerIndex = 4;
            CyclePracticeRespawn(-1);
        }
        else if (layout.RespawnRightBounds.Contains(point))
        {
            _practiceSetupControllerIndex = 4;
            CyclePracticeRespawn(1);
        }
        else if (layout.EnemyBotsLeftBounds.Contains(point))
        {
            _practiceSetupControllerIndex = 5;
            CyclePracticeEnemyBots(-1);
        }
        else if (layout.EnemyBotsRightBounds.Contains(point))
        {
            _practiceSetupControllerIndex = 5;
            CyclePracticeEnemyBots(1);
        }
        else if (layout.FriendlyBotsLeftBounds.Contains(point))
        {
            _practiceSetupControllerIndex = 6;
            CyclePracticeFriendlyBots(-1);
        }
        else if (layout.FriendlyBotsRightBounds.Contains(point))
        {
            _practiceSetupControllerIndex = 6;
            CyclePracticeFriendlyBots(1);
        }
        else if (layout.SpecialAbilitiesBounds.Contains(point))
        {
            _practiceSetupControllerIndex = 7;
            TogglePracticeSpecialAbilities();
        }
        else if (layout.VipRulesBounds.Contains(point))
        {
            _practiceSetupControllerIndex = 8;
            TogglePracticeVipRules();
        }
        else if (layout.StartBounds.Contains(point))
        {
            _practiceSetupControllerIndex = 9;
            TryStartPracticeFromSetup();
        }
        else if (layout.ClientPowersBounds.Contains(point))
        {
            _practiceSetupControllerIndex = 10;
            OpenClientPowersMenu(fromGameplay: false);
        }
        else if (layout.BackBounds.Contains(point))
        {
            _practiceSetupControllerIndex = 11;
            ClosePracticeMapBrowser();
            _practiceSetupOpen = false;
        }
    }

    private bool TryUpdatePracticeSetupControllerInput()
    {
        if (!IsControllerMenuInputActive())
        {
            return false;
        }

        if (TryConsumeControllerMenuNavigation(out var horizontalStep, out var verticalStep))
        {
            if (verticalStep != 0)
            {
                _practiceSetupControllerIndex = MoveControllerMenuSelectionClamped(_practiceSetupControllerIndex, 12, verticalStep);
                return true;
            }

            if (horizontalStep != 0)
            {
                AdjustPracticeSetupControllerRow(_practiceSetupControllerIndex, horizontalStep);
                return true;
            }
        }

        if (IsControllerMenuConfirmPressed())
        {
            ActivatePracticeSetupControllerRow(_practiceSetupControllerIndex);
            return true;
        }

        return false;
    }

    private void AdjustPracticeSetupControllerRow(int rowIndex, int direction)
    {
        switch (rowIndex)
        {
            case 0:
                CyclePracticeMap(direction);
                break;
            case 1:
                CyclePracticeTickRate(direction);
                break;
            case 2:
                CyclePracticeTimeLimit(direction);
                break;
            case 3:
                CyclePracticeCapLimit(direction);
                break;
            case 4:
                CyclePracticeRespawn(direction);
                break;
            case 5:
                CyclePracticeEnemyBots(direction);
                break;
            case 6:
                CyclePracticeFriendlyBots(direction);
                break;
        }
    }

    private void ActivatePracticeSetupControllerRow(int rowIndex)
    {
        switch (rowIndex)
        {
            case >= 0 and <= 6:
                if (rowIndex == 0)
                {
                    OpenPracticeMapBrowser();
                }
                else
                {
                    AdjustPracticeSetupControllerRow(rowIndex, 1);
                }
                break;
            case 7:
                TogglePracticeSpecialAbilities();
                break;
            case 8:
                TogglePracticeVipRules();
                break;
            case 9:
                TryStartPracticeFromSetup();
                break;
            case 10:
                OpenClientPowersMenu(fromGameplay: false);
                break;
            case 11:
                ClosePracticeMapBrowser();
                _practiceSetupOpen = false;
                break;
        }
    }

    private void DrawPracticeSetupMenu()
    {
        var viewportWidth = ViewportWidth;
        var viewportHeight = ViewportHeight;
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black * 0.86f);

        // Draw bottom bar and runners (in animated mode only) - behind everything else
        if (_menuBackgroundMode != MenuBackgroundMode.Static)
        {
            const int bottomBarHeight = 76;
            var barY = viewportHeight - bottomBarHeight;
            var bottomBarBounds = new Rectangle(0, barY, viewportWidth, bottomBarHeight);
            _spriteBatch.Draw(_pixel, bottomBarBounds, new Color(0x57, 0x4f, 0x47));
            _menuBottomBarRunners.Draw(bottomBarBounds);
        }

        var layout = GetPracticeSetupLayout();
        var panel = layout.Panel;
        var compactLayout = layout.CompactLayout;
        const float labelScale = 1f;
        const float valueScale = 1f;
        const float buttonScale = 1f;
        var rowLabelX = panel.X + (compactLayout ? 24f : 28f);
        var rowTextOffset = compactLayout ? 8f : 10f;
        var mapEntry = GetSelectedPracticeMapEntry();
        var mouse = GetScaledMouseState(GetConstrainedMouseState(Game1.GetCurrentMouseState()));
        var controllerMenuActive = IsControllerMenuInputActive();

        bool IsPracticeControlHighlighted(int rowIndex, params Rectangle[] bounds)
        {
            if (controllerMenuActive && _practiceSetupControllerIndex == rowIndex)
            {
                return true;
            }

            for (var index = 0; index < bounds.Length; index += 1)
            {
                if (bounds[index].Contains(mouse.Position))
                {
                    return true;
                }
            }

            return false;
        }

        DrawRoundedRectangleOutline(panel, new Color(59, 51, 46), new Color(213, 205, 188), outlineThickness: 2, radius: 8);

        DrawBitmapFontText("Map", new Vector2(rowLabelX, layout.MapValueBounds.Y + rowTextOffset), Color.White, labelScale);
        DrawPracticeSelectorRow(
            layout.MapLeftBounds,
            layout.MapValueBounds,
            layout.MapRightBounds,
            mapEntry is null ? "No local maps available" : GetPracticeMapDisplayLabel(mapEntry),
            compactLayout,
            buttonScale,
            valueScale,
            IsPracticeControlHighlighted(0, layout.MapLeftBounds, layout.MapValueBounds, layout.MapRightBounds));

        DrawBitmapFontText("Tick Rate", new Vector2(rowLabelX, layout.TickValueBounds.Y + rowTextOffset), Color.White, labelScale);
        DrawPracticeSelectorRow(
            layout.TickLeftBounds,
            layout.TickValueBounds,
            layout.TickRightBounds,
            _practiceTickRate.ToString(CultureInfo.InvariantCulture),
            compactLayout,
            buttonScale,
            valueScale,
            IsPracticeControlHighlighted(1, layout.TickLeftBounds, layout.TickValueBounds, layout.TickRightBounds));

        DrawBitmapFontText("Time Limit", new Vector2(rowLabelX, layout.TimeValueBounds.Y + rowTextOffset), Color.White, labelScale);
        DrawPracticeSelectorRow(
            layout.TimeLeftBounds,
            layout.TimeValueBounds,
            layout.TimeRightBounds,
            $"{_practiceTimeLimitMinutes} min",
            compactLayout,
            buttonScale,
            valueScale,
            IsPracticeControlHighlighted(2, layout.TimeLeftBounds, layout.TimeValueBounds, layout.TimeRightBounds));

        DrawBitmapFontText("Cap Limit", new Vector2(rowLabelX, layout.CapValueBounds.Y + rowTextOffset), Color.White, labelScale);
        DrawPracticeSelectorRow(
            layout.CapLeftBounds,
            layout.CapValueBounds,
            layout.CapRightBounds,
            _practiceCapLimit.ToString(CultureInfo.InvariantCulture),
            compactLayout,
            buttonScale,
            valueScale,
            IsPracticeControlHighlighted(3, layout.CapLeftBounds, layout.CapValueBounds, layout.CapRightBounds));

        DrawBitmapFontText("Respawn", new Vector2(rowLabelX, layout.RespawnValueBounds.Y + rowTextOffset), Color.White, labelScale);
        DrawPracticeSelectorRow(
            layout.RespawnLeftBounds,
            layout.RespawnValueBounds,
            layout.RespawnRightBounds,
            _practiceRespawnSeconds == 0 ? "Instant" : $"{_practiceRespawnSeconds}s",
            compactLayout,
            buttonScale,
            valueScale,
            IsPracticeControlHighlighted(4, layout.RespawnLeftBounds, layout.RespawnValueBounds, layout.RespawnRightBounds));

        DrawBitmapFontText("Enemy Bots", new Vector2(rowLabelX, layout.EnemyBotsValueBounds.Y + rowTextOffset), Color.White, labelScale);
        DrawPracticeSelectorRow(
            layout.EnemyBotsLeftBounds,
            layout.EnemyBotsValueBounds,
            layout.EnemyBotsRightBounds,
            GetPracticeBotCountLabel(_practiceEnemyBotCount),
            compactLayout,
            buttonScale,
            valueScale,
            IsPracticeControlHighlighted(5, layout.EnemyBotsLeftBounds, layout.EnemyBotsValueBounds, layout.EnemyBotsRightBounds));

        DrawBitmapFontText("Friendly Bots", new Vector2(rowLabelX, layout.FriendlyBotsValueBounds.Y + rowTextOffset), Color.White, labelScale);
        DrawPracticeSelectorRow(
            layout.FriendlyBotsLeftBounds,
            layout.FriendlyBotsValueBounds,
            layout.FriendlyBotsRightBounds,
            GetPracticeBotCountLabel(_practiceFriendlyBotCount),
            compactLayout,
            buttonScale,
            valueScale,
            IsPracticeControlHighlighted(6, layout.FriendlyBotsLeftBounds, layout.FriendlyBotsValueBounds, layout.FriendlyBotsRightBounds));

        DrawBitmapFontText("Special Abilities", new Vector2(rowLabelX, layout.SpecialAbilitiesBounds.Y + rowTextOffset), Color.White, labelScale);
        var specialAbilitiesLabel = _practiceSpecialAbilitiesEnabled ? "On" : "Off";
        DrawMenuInputBoxScaled(layout.SpecialAbilitiesBounds, specialAbilitiesLabel, IsPracticeControlHighlighted(7, layout.SpecialAbilitiesBounds), valueScale);

        DrawBitmapFontText("VIP Rules", new Vector2(rowLabelX, layout.VipRulesBounds.Y + rowTextOffset), Color.White, labelScale);
        var vipRulesLabel = _practiceVipRulesEnabled ? "On" : "Off";
        DrawMenuInputBoxScaled(layout.VipRulesBounds, vipRulesLabel, IsPracticeControlHighlighted(8, layout.VipRulesBounds), valueScale);

        DrawMenuButtonScaled(layout.StartBounds, "Start Practice", IsPracticeControlHighlighted(9, layout.StartBounds), buttonScale);
        DrawMenuButtonScaled(layout.ClientPowersBounds, "Experimental", IsPracticeControlHighlighted(10, layout.ClientPowersBounds), buttonScale);
        DrawMenuButtonScaled(layout.BackBounds, "Back", IsPracticeControlHighlighted(11, layout.BackBounds), buttonScale);

        if (!string.IsNullOrWhiteSpace(_menuStatusMessage))
        {
            DrawBitmapFontText(
                _menuStatusMessage,
                new Vector2(panel.X + 24f, panel.Bottom - (compactLayout ? 34f : 38f)),
                new Color(230, 220, 180),
                1f);
        }

        if (_practiceSetupState.MapBrowserOpen)
        {
            DrawPracticeMapBrowserOverlay(GetPracticeMapBrowserLayout());
        }
    }

    private void OpenPracticeMapBrowser()
    {
        _practiceSetupState.OpenMapBrowser();
        _practiceSetupState.EnsureMapBrowserSelectionVisible(GetPracticeMapBrowserLayout().VisibleRowCapacity);
        _menuStatusMessage = string.Empty;
    }

    private void ClosePracticeMapBrowser()
    {
        _practiceSetupState.CloseMapBrowser();
        ClosePracticeMapBrowserPreview();
    }

    private void UpdatePracticeMapBrowserMenu(KeyboardState keyboard, MouseState mouse, PracticeMapBrowserLayout layout)
    {
        var entries = _practiceSetupState.GetMapBrowserEntriesForDisplay();
        _practiceSetupState.ClampMapBrowserScroll(entries.Count, layout.VisibleRowCapacity);

        if (IsKeyPressed(keyboard, Keys.Escape) || IsControllerMenuBackPressed())
        {
            ClosePracticeMapBrowser();
            return;
        }

        if (IsKeyPressed(keyboard, Keys.Enter))
        {
            ConfirmPracticeMapBrowserSelection();
            return;
        }

        var wheelDelta = mouse.ScrollWheelValue - _previousMouse.ScrollWheelValue;
        if (wheelDelta != 0 && GetPracticeMapBrowserListInteractionBounds(layout).Contains(mouse.Position))
        {
            var stepCount = Math.Max(1, Math.Abs(wheelDelta) / 120);
            _practiceSetupState.MapBrowserScrollOffset = Math.Clamp(
                _practiceSetupState.MapBrowserScrollOffset + (wheelDelta > 0 ? -stepCount : stepCount),
                0,
                Math.Max(0, entries.Count - layout.VisibleRowCapacity));
        }

        var clickPressed = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton != ButtonState.Pressed;
        if (!clickPressed)
        {
            return;
        }

        if (layout.ModeFilterBounds.Contains(mouse.Position))
        {
            CyclePracticeMapBrowserModeFilter();
            return;
        }

        if (layout.BaseFilterBounds.Contains(mouse.Position))
        {
            _practiceSetupState.MapBrowserIncludeBaseMaps = !_practiceSetupState.MapBrowserIncludeBaseMaps;
            _practiceSetupState.NotifyMapBrowserFiltersChanged();
            return;
        }

        if (layout.CustomFilterBounds.Contains(mouse.Position))
        {
            _practiceSetupState.MapBrowserIncludeCustomMaps = !_practiceSetupState.MapBrowserIncludeCustomMaps;
            _practiceSetupState.NotifyMapBrowserFiltersChanged();
            return;
        }

        if (layout.ResetBounds.Contains(mouse.Position))
        {
            _practiceSetupState.ResetMapBrowserFilters();
            return;
        }

        if (layout.SelectBounds.Contains(mouse.Position))
        {
            ConfirmPracticeMapBrowserSelection();
            return;
        }

        if (layout.CloseBounds.Contains(mouse.Position))
        {
            ClosePracticeMapBrowser();
            return;
        }

        if (TryGetPracticeMapBrowserListHit(mouse.Position, layout, entries, out var hitIndex))
        {
            _practiceSetupState.SelectMapBrowserIndex(hitIndex);
        }
    }

    private bool TryHandlePracticeMapBrowserTextInput(char character)
    {
        if (!_practiceSetupOpen || !_practiceSetupState.MapBrowserOpen)
        {
            return false;
        }

        if (character == '\b')
        {
            var result = DeleteTextSelectionOrBackspace(
                _practiceSetupState.MapBrowserNameFilterBuffer,
                _practiceSetupState.MapBrowserNameFilterCursorIndex,
                _practiceSetupState.MapBrowserNameFilterSelectionStart);
            _practiceSetupState.MapBrowserNameFilterBuffer = result.Text;
            _practiceSetupState.MapBrowserNameFilterCursorIndex = result.CursorIndex;
            _practiceSetupState.MapBrowserNameFilterSelectionStart = result.SelectionStart;
            _practiceSetupState.NotifyMapBrowserFiltersChanged();
            return true;
        }

        if (char.IsControl(character))
        {
            return false;
        }

        var insertResult = InsertTextCharacterAtCursor(
            _practiceSetupState.MapBrowserNameFilterBuffer,
            character,
            _practiceSetupState.MapBrowserNameFilterCursorIndex,
            _practiceSetupState.MapBrowserNameFilterSelectionStart,
            48);
        _practiceSetupState.MapBrowserNameFilterBuffer = insertResult.Text;
        _practiceSetupState.MapBrowserNameFilterCursorIndex = insertResult.CursorIndex;
        _practiceSetupState.MapBrowserNameFilterSelectionStart = insertResult.SelectionStart;
        _practiceSetupState.NotifyMapBrowserFiltersChanged();
        return true;
    }

    private void ConfirmPracticeMapBrowserSelection()
    {
        if (!_practiceSetupState.ConfirmMapBrowserSelection())
        {
            _menuStatusMessage = "Select a map first.";
            return;
        }

        DisablePracticeVipRulesIfUnavailable();
        ClosePracticeMapBrowser();
        _menuStatusMessage = string.Empty;
    }

    private void CyclePracticeMapBrowserModeFilter()
    {
        var options = GetPracticeMapBrowserModeFilterOptions();
        var currentIndex = -1;
        for (var index = 0; index < options.Count; index += 1)
        {
            if (options[index] == _practiceSetupState.MapBrowserModeFilter)
            {
                currentIndex = index;
                break;
            }
        }

        var nextIndex = (Math.Max(0, currentIndex) + 1) % options.Count;
        _practiceSetupState.MapBrowserModeFilter = options[nextIndex];
        _practiceSetupState.NotifyMapBrowserFiltersChanged();
    }

    private List<GameModeKind?> GetPracticeMapBrowserModeFilterOptions()
    {
        var availableModes = _practiceMapEntries
            .Select(entry => entry.Mode)
            .ToHashSet();
        var options = GetHostSetupMapModeFilterOptions()
            .Where(mode => mode is null || availableModes.Contains(mode.Value))
            .ToList();
        return options.Count == 0 ? [null] : options;
    }

    private void DrawPracticeMapBrowserOverlay(PracticeMapBrowserLayout layout)
    {
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, ViewportWidth, ViewportHeight), Color.Black * 0.58f);
        _spriteBatch.Draw(_pixel, layout.Panel, new Color(38, 34, 31, 245));
        DrawRoundedRectangleOutline(layout.Panel, new Color(59, 51, 46), new Color(213, 205, 188), outlineThickness: 2, radius: 8);

        const float labelScale = 0.9f;
        var titleY = layout.Panel.Y + 16f;
        DrawBitmapFontText("PRACTICE MAPS", new Vector2(layout.Panel.X + 20f, titleY), Color.White, 1f);
        DrawBitmapFontText("Search", new Vector2(layout.SearchBounds.X, layout.SearchBounds.Y - 16f), new Color(210, 210, 210), labelScale);
        DrawMenuInputBoxScaled(
            layout.SearchBounds,
            _practiceSetupState.MapBrowserNameFilterBuffer,
            true,
            1f,
            _practiceSetupState.MapBrowserNameFilterCursorIndex,
            _practiceSetupState.MapBrowserNameFilterSelectionStart);

        var modeLabel = GetHostSetupMapModeFilterLabel(_practiceSetupState.MapBrowserModeFilter);
        DrawMenuButtonScaled(layout.ModeFilterBounds, modeLabel, layout.ModeFilterBounds.Contains(GetScaledMouseState(GetConstrainedMouseState(Game1.GetCurrentMouseState())).Position), 1f);
        DrawPracticeMapBrowserCheckbox(layout.BaseFilterBounds, "Base", _practiceSetupState.MapBrowserIncludeBaseMaps);
        DrawPracticeMapBrowserCheckbox(layout.CustomFilterBounds, "Custom", _practiceSetupState.MapBrowserIncludeCustomMaps);
        DrawMenuButtonScaled(layout.ResetBounds, "Reset", false, 0.95f);

        var entries = _practiceSetupState.GetMapBrowserEntriesForDisplay();
        _practiceSetupState.ClampMapBrowserScroll(entries.Count, layout.VisibleRowCapacity);
        DrawPracticeMapBrowserList(layout, entries);
        DrawPracticeMapBrowserPreview(layout.PreviewBounds);

        DrawMenuButtonScaled(layout.SelectBounds, "Select", false, 1f);
        DrawMenuButtonScaled(layout.CloseBounds, "Close", false, 1f);
    }

    private void DrawPracticeMapBrowserCheckbox(Rectangle rowBounds, string label, bool enabled)
    {
        var checkboxBounds = new Rectangle(rowBounds.X, rowBounds.Y + 6, 18, 18);
        DrawHostSetupFilterCheckboxRow(checkboxBounds, rowBounds, label, enabled, 0.9f);
    }

    private void DrawPracticeMapBrowserList(PracticeMapBrowserLayout layout, IReadOnlyList<PracticeMapEntry> entries)
    {
        _spriteBatch.Draw(_pixel, layout.ListBounds, new Color(34, 30, 28, 220));
        var mouse = GetScaledMouseState(GetConstrainedMouseState(Game1.GetCurrentMouseState()));
        var hoverIndex = TryGetPracticeMapBrowserListHit(mouse.Position, layout, entries, out var hitIndex)
            ? hitIndex
            : -1;
        var endIndex = Math.Min(entries.Count, _practiceSetupState.MapBrowserScrollOffset + layout.VisibleRowCapacity);
        for (var index = _practiceSetupState.MapBrowserScrollOffset; index < endIndex; index += 1)
        {
            var entry = entries[index];
            var visibleIndex = index - _practiceSetupState.MapBrowserScrollOffset;
            var rowBounds = new Rectangle(
                layout.ListBounds.X + 4,
                layout.ListBounds.Y + (visibleIndex * layout.RowHeight),
                layout.ListBounds.Width - 8,
                layout.RowHeight - 2);
            var rowColor = index == _practiceSetupState.MapBrowserIndex
                ? new Color(95, 72, 68)
                : index == hoverIndex
                    ? new Color(75, 67, 62)
                    : new Color(54, 47, 41);
            _spriteBatch.Draw(_pixel, rowBounds, rowColor);

            var displayName = entry.IsCustomMap ? $"{entry.DisplayName} (Custom)" : entry.DisplayName;
            var modeLabel = GetHostSetupMapModeShortLabel(entry.Mode);
            var modeWidth = MeasureBitmapFontWidth(modeLabel, 1f);
            var maxNameWidth = Math.Max(48f, rowBounds.Width - modeWidth - 24f);
            var trimmedName = TrimBitmapMenuText(displayName, maxNameWidth, 1f);
            var textY = rowBounds.Y + ((rowBounds.Height - MeasureBitmapFontHeight(1f)) * 0.5f);
            DrawBitmapFontText(trimmedName, new Vector2(rowBounds.X + 8f, textY), Color.White, 1f);
            DrawBitmapFontText(modeLabel, new Vector2(rowBounds.Right - modeWidth - 8f, textY), new Color(210, 210, 210), 1f);
        }

        if (entries.Count > layout.VisibleRowCapacity)
        {
            DrawHostSetupListScrollbar(
                layout.ListBounds,
                layout.ScrollbarWidth,
                _practiceSetupState.MapBrowserScrollOffset,
                entries.Count,
                layout.VisibleRowCapacity);
        }
    }

    private void DrawPracticeMapBrowserPreview(Rectangle bounds)
    {
        var selectedEntry = _practiceSetupState.GetSelectedMapBrowserEntry() ?? GetSelectedPracticeMapEntry();
        EnsurePracticeMapBrowserPreview(selectedEntry?.LevelName);
        DrawRoundedRectangleOutline(bounds, new Color(46, 40, 35), new Color(180, 172, 158), outlineThickness: 1, radius: 6);
        _spriteBatch.Draw(_pixel, bounds, new Color(22, 24, 28));

        if (_practiceMapBrowserPreviewState is null)
        {
            const string placeholder = "Select a map";
            var textWidth = MeasureBitmapFontWidth(placeholder, 0.9f);
            var textY = bounds.Y + ((bounds.Height - MeasureBitmapFontHeight(0.9f)) * 0.5f);
            DrawBitmapFontText(
                placeholder,
                new Vector2(bounds.X + ((bounds.Width - textWidth) * 0.5f), textY),
                new Color(150, 150, 150),
                0.9f);
            return;
        }

        _practiceMapBrowserPreviewState.Draw(_spriteBatch, bounds, interactiveView: false);
    }

    private void EnsurePracticeMapBrowserPreview(string? levelName)
    {
        if (string.IsNullOrWhiteSpace(levelName))
        {
            ClosePracticeMapBrowserPreview();
            return;
        }

        if (string.Equals(_practiceMapBrowserPreviewLevelName, levelName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        ClosePracticeMapBrowserPreview();
        var level = SimpleLevelFactory.CreateImportedLevel(levelName);
        if (level is null)
        {
            return;
        }

        _practiceMapBrowserPreviewState = CreateHostSetupMapPreviewState(level);
        _practiceMapBrowserPreviewLevelName = levelName;
    }

    private void ClosePracticeMapBrowserPreview()
    {
        _practiceMapBrowserPreviewState?.Dispose();
        _practiceMapBrowserPreviewState = null;
        _practiceMapBrowserPreviewLevelName = null;
    }

    private PracticeMapBrowserLayout GetPracticeMapBrowserLayout()
    {
        var panelWidth = Math.Min(ViewportWidth - 32, 900);
        var panelHeight = Math.Min(ViewportHeight - 32, 620);
        var panel = new Rectangle(
            (ViewportWidth - panelWidth) / 2,
            (ViewportHeight - panelHeight) / 2,
            panelWidth,
            panelHeight);

        var compact = panel.Width < 760 || panel.Height < 560;
        var padding = compact ? 16 : 20;
        var controlHeight = compact ? 30 : 34;
        var topY = panel.Y + (compact ? 48 : 54);
        var buttonHeight = compact ? 32 : 36;
        var buttonY = panel.Bottom - padding - buttonHeight;
        var listTop = topY + controlHeight + (compact ? 16 : 20);
        var contentBottom = buttonY - (compact ? 12 : 16);
        var contentHeight = Math.Max(120, contentBottom - listTop);
        var scrollbarWidth = 10;
        var listWidth = Math.Clamp((int)(panel.Width * 0.46f), 260, 400);
        var listBounds = new Rectangle(panel.X + padding, listTop, listWidth, contentHeight);
        var previewBounds = new Rectangle(
            listBounds.Right + scrollbarWidth + padding,
            listTop,
            panel.Right - padding - (listBounds.Right + scrollbarWidth + padding),
            contentHeight);
        var searchWidth = Math.Max(180, listWidth);
        var searchBounds = new Rectangle(panel.X + padding, topY, searchWidth, controlHeight);
        var modeBounds = new Rectangle(searchBounds.Right + 12, topY, compact ? 72 : 84, controlHeight);
        var baseBounds = new Rectangle(modeBounds.Right + 10, topY, compact ? 78 : 88, controlHeight);
        var customBounds = new Rectangle(baseBounds.Right + 8, topY, compact ? 98 : 110, controlHeight);
        var resetBounds = new Rectangle(panel.Right - padding - (compact ? 72 : 84), topY, compact ? 72 : 84, controlHeight);
        var closeWidth = compact ? 92 : 110;
        var selectWidth = compact ? 100 : 120;
        var closeBounds = new Rectangle(panel.Right - padding - closeWidth, buttonY, closeWidth, buttonHeight);
        var selectBounds = new Rectangle(closeBounds.X - 12 - selectWidth, buttonY, selectWidth, buttonHeight);
        var rowHeight = compact ? 28 : 30;
        var visibleRows = Math.Max(1, listBounds.Height / rowHeight);

        return new PracticeMapBrowserLayout(
            panel,
            searchBounds,
            modeBounds,
            baseBounds,
            customBounds,
            resetBounds,
            listBounds,
            previewBounds,
            selectBounds,
            closeBounds,
            rowHeight,
            visibleRows,
            scrollbarWidth);
    }

    private static Rectangle GetPracticeMapBrowserListInteractionBounds(PracticeMapBrowserLayout layout)
    {
        return new Rectangle(
            layout.ListBounds.X,
            layout.ListBounds.Y,
            layout.ListBounds.Width + layout.ScrollbarWidth + 8,
            layout.ListBounds.Height);
    }

    private bool TryGetPracticeMapBrowserListHit(
        Point mousePosition,
        PracticeMapBrowserLayout layout,
        IReadOnlyList<PracticeMapEntry> entries,
        out int hitIndex)
    {
        hitIndex = -1;
        if (!GetPracticeMapBrowserListInteractionBounds(layout).Contains(mousePosition))
        {
            return false;
        }

        var visibleIndex = (mousePosition.Y - layout.ListBounds.Y) / layout.RowHeight;
        var entryIndex = _practiceSetupState.MapBrowserScrollOffset + visibleIndex;
        if (visibleIndex < 0 || entryIndex < 0 || entryIndex >= entries.Count)
        {
            return false;
        }

        hitIndex = entryIndex;
        return true;
    }

    private PracticeSetupLayout GetPracticeSetupLayout()
    {
        var panelWidth = Math.Min(ViewportWidth - 32, 700);
        var panelHeight = Math.Min(ViewportHeight - 24, ViewportHeight < 720 ? 560 : 620);
        var panel = new Rectangle(
            (ViewportWidth - panelWidth) / 2,
            (ViewportHeight - panelHeight) / 2,
            panelWidth,
            panelHeight);

        var compactLayout = panel.Height < 580 || panel.Width < 640;
        var shortLayout = panel.Height < 500;
        var padding = shortLayout ? 16 : compactLayout ? 20 : 28;
        var rowHeight = shortLayout ? 30 : compactLayout ? 36 : 42;
        var rowGap = shortLayout ? 4 : compactLayout ? 8 : 10;
        var selectorButtonWidth = shortLayout ? 30 : compactLayout ? 34 : 40;
        var contentTop = panel.Y + (shortLayout ? 46 : compactLayout ? 58 : 72);
        var labelWidth = shortLayout ? 112 : compactLayout ? 126 : 150;
        var selectorLeft = panel.X + padding + labelWidth;
        var selectorWidth = panel.Width - (padding * 2) - labelWidth;
        var selectorValueWidth = selectorWidth - (selectorButtonWidth * 2) - 16;
        var buttonHeight = shortLayout ? 32 : compactLayout ? 36 : 42;
        var actionGap = shortLayout ? 6 : compactLayout ? 8 : 12;
        var actionWidth = (panel.Width - (padding * 2) - (actionGap * 2)) / 3;
        var actionsY = panel.Bottom - padding - buttonHeight - (shortLayout ? 0 : 4);

        var mapLeftBounds = new Rectangle(selectorLeft, contentTop, selectorButtonWidth, rowHeight);
        var mapValueBounds = new Rectangle(mapLeftBounds.Right + 8, contentTop, selectorValueWidth, rowHeight);
        var mapRightBounds = new Rectangle(mapValueBounds.Right + 8, contentTop, selectorButtonWidth, rowHeight);

        var tickLeftBounds = OffsetPracticeRow(mapLeftBounds, rowHeight + rowGap);
        var tickValueBounds = OffsetPracticeRow(mapValueBounds, rowHeight + rowGap);
        var tickRightBounds = OffsetPracticeRow(mapRightBounds, rowHeight + rowGap);

        var timeLeftBounds = OffsetPracticeRow(tickLeftBounds, rowHeight + rowGap);
        var timeValueBounds = OffsetPracticeRow(tickValueBounds, rowHeight + rowGap);
        var timeRightBounds = OffsetPracticeRow(tickRightBounds, rowHeight + rowGap);

        var capLeftBounds = OffsetPracticeRow(timeLeftBounds, rowHeight + rowGap);
        var capValueBounds = OffsetPracticeRow(timeValueBounds, rowHeight + rowGap);
        var capRightBounds = OffsetPracticeRow(timeRightBounds, rowHeight + rowGap);

        var respawnLeftBounds = OffsetPracticeRow(capLeftBounds, rowHeight + rowGap);
        var respawnValueBounds = OffsetPracticeRow(capValueBounds, rowHeight + rowGap);
        var respawnRightBounds = OffsetPracticeRow(capRightBounds, rowHeight + rowGap);

        var enemyBotsLeftBounds = OffsetPracticeRow(respawnLeftBounds, rowHeight + rowGap);
        var enemyBotsValueBounds = OffsetPracticeRow(respawnValueBounds, rowHeight + rowGap);
        var enemyBotsRightBounds = OffsetPracticeRow(respawnRightBounds, rowHeight + rowGap);

        var friendlyBotsLeftBounds = OffsetPracticeRow(enemyBotsLeftBounds, rowHeight + rowGap);
        var friendlyBotsValueBounds = OffsetPracticeRow(enemyBotsValueBounds, rowHeight + rowGap);
        var friendlyBotsRightBounds = OffsetPracticeRow(enemyBotsRightBounds, rowHeight + rowGap);

        var specialAbilitiesY = friendlyBotsLeftBounds.Y + rowHeight + rowGap;
        var specialAbilitiesX = selectorLeft;
        var specialAbilitiesWidth = selectorWidth;
        var specialAbilitiesBounds = new Rectangle(specialAbilitiesX, specialAbilitiesY, specialAbilitiesWidth, rowHeight);
        var vipRulesBounds = OffsetPracticeRow(specialAbilitiesBounds, rowHeight + rowGap);

        var startBounds = new Rectangle(panel.X + padding, actionsY, actionWidth, buttonHeight);
        var clientPowersBounds = new Rectangle(startBounds.Right + actionGap, actionsY, actionWidth, buttonHeight);
        var backBounds = new Rectangle(clientPowersBounds.Right + actionGap, actionsY, actionWidth, buttonHeight);

        return new PracticeSetupLayout(
            panel,
            mapLeftBounds,
            mapValueBounds,
            mapRightBounds,
            tickLeftBounds,
            tickValueBounds,
            tickRightBounds,
            timeLeftBounds,
            timeValueBounds,
            timeRightBounds,
            capLeftBounds,
            capValueBounds,
            capRightBounds,
            respawnLeftBounds,
            respawnValueBounds,
            respawnRightBounds,
            enemyBotsLeftBounds,
            enemyBotsValueBounds,
            enemyBotsRightBounds,
            friendlyBotsLeftBounds,
            friendlyBotsValueBounds,
            friendlyBotsRightBounds,
            specialAbilitiesBounds,
            vipRulesBounds,
            startBounds,
            clientPowersBounds,
            backBounds,
            compactLayout);
    }

    private void DrawPracticeSelectorRow(
        Rectangle leftBounds,
        Rectangle valueBounds,
        Rectangle rightBounds,
        string valueText,
        bool compactLayout,
        float buttonScale,
        float valueScale,
        bool highlighted)
    {
        DrawMenuButtonCentered(leftBounds, "<", highlighted, buttonScale);
        DrawMenuInputBoxScaled(valueBounds, valueText, highlighted, valueScale);
        DrawMenuButtonCentered(rightBounds, ">", highlighted, buttonScale);
    }

    private static Rectangle OffsetPracticeRow(Rectangle bounds, int offsetY)
    {
        return new Rectangle(bounds.X, bounds.Y + offsetY, bounds.Width, bounds.Height);
    }

    private static string GetPracticeMapDisplayLabel(PracticeMapEntry entry)
    {
        var modeLabel = entry.Mode switch
        {
            GameModeKind.Arena => "Arena",
            GameModeKind.ControlPoint => "CP",
            GameModeKind.Generator => "Gen",
            GameModeKind.KingOfTheHill => "KOTH",
            GameModeKind.DoubleKingOfTheHill => "DKOTH",
            GameModeKind.TeamDeathmatch => "TDM",
            _ => "CTF",
        };
        return entry.IsCustomMap
            ? $"{entry.DisplayName} [{modeLabel}] (Custom)"
            : $"{entry.DisplayName} [{modeLabel}]";
    }
}
