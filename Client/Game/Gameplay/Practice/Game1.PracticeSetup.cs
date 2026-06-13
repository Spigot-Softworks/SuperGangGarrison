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
        Rectangle SpecialAbilitiesLeftBounds,
        Rectangle SpecialAbilitiesValueBounds,
        Rectangle SpecialAbilitiesRightBounds,
        Rectangle StartBounds,
        Rectangle ClientPowersBounds,
        Rectangle BackBounds,
        bool CompactLayout);

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
    }

    private void UpdatePracticeSetupMenu(KeyboardState keyboard, MouseState mouse)
    {
        if (_practiceSetupState.MapBrowserOpen)
        {
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

            var mapLayout = PracticeMapsMenuLayoutCalculator.Create(ViewportWidth, ViewportHeight);
            UpdatePracticeMapSelectionMenu(keyboard, mouse, mapLayout);
            return;
        }

        var layout = GetPracticeSetupLayout();

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
        else if (layout.SpecialAbilitiesLeftBounds.Contains(point))
        {
            _practiceSetupControllerIndex = 7;
            CyclePracticeSpecialAbilities(-1);
        }
        else if (layout.SpecialAbilitiesRightBounds.Contains(point))
        {
            _practiceSetupControllerIndex = 7;
            CyclePracticeSpecialAbilities(1);
        }
        else if (layout.StartBounds.Contains(point))
        {
            _practiceSetupControllerIndex = 8;
            TryStartPracticeFromSetup();
        }
        else if (layout.ClientPowersBounds.Contains(point))
        {
            _practiceSetupControllerIndex = 9;
            OpenClientPowersMenu(fromGameplay: false);
        }
        else if (layout.BackBounds.Contains(point))
        {
            _practiceSetupControllerIndex = 10;
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
                _practiceSetupControllerIndex = MoveControllerMenuSelectionClamped(_practiceSetupControllerIndex, 11, verticalStep);
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
            case 7:
                CyclePracticeSpecialAbilities(direction);
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
                CyclePracticeSpecialAbilities(1);
                break;
            case 8:
                TryStartPracticeFromSetup();
                break;
            case 9:
                OpenClientPowersMenu(fromGameplay: false);
                break;
            case 10:
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
        var rowTextOffset = (layout.MapValueBounds.Height - MeasureBitmapFontHeight(labelScale)) * 0.5f;
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

        (bool Left, bool Value, bool Right) GetPracticeSelectorHighlights(int rowIndex, Rectangle left, Rectangle value, Rectangle right)
        {
            var controller = controllerMenuActive && _practiceSetupControllerIndex == rowIndex;
            return (
                controller || left.Contains(mouse.Position),
                controller || value.Contains(mouse.Position),
                controller || right.Contains(mouse.Position));
        }

        DrawRoundedRectangleOutline(panel, new Color(59, 51, 46), new Color(213, 205, 188), outlineThickness: 2, radius: 8);

        DrawBitmapFontText("Map", new Vector2(rowLabelX, layout.MapValueBounds.Y + rowTextOffset), Color.White, labelScale);
        var mapHighlights = GetPracticeSelectorHighlights(0, layout.MapLeftBounds, layout.MapValueBounds, layout.MapRightBounds);
        DrawPracticeSelectorRow(
            layout.MapLeftBounds,
            layout.MapValueBounds,
            layout.MapRightBounds,
            mapEntry is null ? "No local maps available" : GetPracticeMapDisplayLabel(mapEntry),
            buttonScale,
            valueScale,
            mapHighlights.Left,
            mapHighlights.Value,
            mapHighlights.Right);

        DrawBitmapFontText("Tick Rate", new Vector2(rowLabelX, layout.TickValueBounds.Y + rowTextOffset), Color.White, labelScale);
        var tickHighlights = GetPracticeSelectorHighlights(1, layout.TickLeftBounds, layout.TickValueBounds, layout.TickRightBounds);
        DrawPracticeSelectorRow(
            layout.TickLeftBounds,
            layout.TickValueBounds,
            layout.TickRightBounds,
            _practiceTickRate.ToString(CultureInfo.InvariantCulture),
            buttonScale,
            valueScale,
            tickHighlights.Left,
            tickHighlights.Value,
            tickHighlights.Right);

        DrawBitmapFontText("Time Limit", new Vector2(rowLabelX, layout.TimeValueBounds.Y + rowTextOffset), Color.White, labelScale);
        var timeHighlights = GetPracticeSelectorHighlights(2, layout.TimeLeftBounds, layout.TimeValueBounds, layout.TimeRightBounds);
        DrawPracticeSelectorRow(
            layout.TimeLeftBounds,
            layout.TimeValueBounds,
            layout.TimeRightBounds,
            $"{_practiceTimeLimitMinutes} min",
            buttonScale,
            valueScale,
            timeHighlights.Left,
            timeHighlights.Value,
            timeHighlights.Right);

        DrawBitmapFontText("Cap Limit", new Vector2(rowLabelX, layout.CapValueBounds.Y + rowTextOffset), Color.White, labelScale);
        var capHighlights = GetPracticeSelectorHighlights(3, layout.CapLeftBounds, layout.CapValueBounds, layout.CapRightBounds);
        DrawPracticeSelectorRow(
            layout.CapLeftBounds,
            layout.CapValueBounds,
            layout.CapRightBounds,
            _practiceCapLimit.ToString(CultureInfo.InvariantCulture),
            buttonScale,
            valueScale,
            capHighlights.Left,
            capHighlights.Value,
            capHighlights.Right);

        DrawBitmapFontText("Respawn", new Vector2(rowLabelX, layout.RespawnValueBounds.Y + rowTextOffset), Color.White, labelScale);
        var respawnHighlights = GetPracticeSelectorHighlights(4, layout.RespawnLeftBounds, layout.RespawnValueBounds, layout.RespawnRightBounds);
        DrawPracticeSelectorRow(
            layout.RespawnLeftBounds,
            layout.RespawnValueBounds,
            layout.RespawnRightBounds,
            _practiceRespawnSeconds == 0 ? "Instant" : $"{_practiceRespawnSeconds}s",
            buttonScale,
            valueScale,
            respawnHighlights.Left,
            respawnHighlights.Value,
            respawnHighlights.Right);

        DrawBitmapFontText("Enemy Bots", new Vector2(rowLabelX, layout.EnemyBotsValueBounds.Y + rowTextOffset), Color.White, labelScale);
        var enemyBotHighlights = GetPracticeSelectorHighlights(5, layout.EnemyBotsLeftBounds, layout.EnemyBotsValueBounds, layout.EnemyBotsRightBounds);
        DrawPracticeSelectorRow(
            layout.EnemyBotsLeftBounds,
            layout.EnemyBotsValueBounds,
            layout.EnemyBotsRightBounds,
            GetPracticeBotCountLabel(_practiceEnemyBotCount),
            buttonScale,
            valueScale,
            enemyBotHighlights.Left,
            enemyBotHighlights.Value,
            enemyBotHighlights.Right);

        DrawBitmapFontText("Friendly Bots", new Vector2(rowLabelX, layout.FriendlyBotsValueBounds.Y + rowTextOffset), Color.White, labelScale);
        var friendlyBotHighlights = GetPracticeSelectorHighlights(6, layout.FriendlyBotsLeftBounds, layout.FriendlyBotsValueBounds, layout.FriendlyBotsRightBounds);
        DrawPracticeSelectorRow(
            layout.FriendlyBotsLeftBounds,
            layout.FriendlyBotsValueBounds,
            layout.FriendlyBotsRightBounds,
            GetPracticeBotCountLabel(_practiceFriendlyBotCount),
            buttonScale,
            valueScale,
            friendlyBotHighlights.Left,
            friendlyBotHighlights.Value,
            friendlyBotHighlights.Right);

        DrawBitmapFontText("Special Abilities", new Vector2(rowLabelX, layout.SpecialAbilitiesValueBounds.Y + rowTextOffset), Color.White, labelScale);
        var specialAbilitiesHighlights = GetPracticeSelectorHighlights(7, layout.SpecialAbilitiesLeftBounds, layout.SpecialAbilitiesValueBounds, layout.SpecialAbilitiesRightBounds);
        DrawPracticeSelectorRow(
            layout.SpecialAbilitiesLeftBounds,
            layout.SpecialAbilitiesValueBounds,
            layout.SpecialAbilitiesRightBounds,
            _practiceSpecialAbilitiesEnabled ? "On" : "Off",
            buttonScale,
            valueScale,
            specialAbilitiesHighlights.Left,
            specialAbilitiesHighlights.Value,
            specialAbilitiesHighlights.Right);

        DrawMenuButtonScaled(layout.StartBounds, "Start Practice", IsPracticeControlHighlighted(8, layout.StartBounds), buttonScale);
        DrawMenuButtonScaled(layout.ClientPowersBounds, "Experimental", IsPracticeControlHighlighted(9, layout.ClientPowersBounds), buttonScale);
        DrawMenuButtonScaled(layout.BackBounds, "Back", IsPracticeControlHighlighted(10, layout.BackBounds), buttonScale);

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
            DrawPracticeMapSelectionOverlay(PracticeMapsMenuLayoutCalculator.Create(ViewportWidth, ViewportHeight), 1f);
        }
    }

    private void OpenPracticeMapBrowser()
    {
        _practiceSetupState.OpenMapBrowser();
        var layout = PracticeMapsMenuLayoutCalculator.Create(ViewportWidth, ViewportHeight);
        _practiceSetupState.EnsureAvailableMapSelectionVisible(layout.AvailableVisibleRowCapacity);
        _practiceMapContextMenu = null;
        _practiceEditField = PracticeEditField.None;
        _menuStatusMessage = string.Empty;
    }

    private void ClosePracticeMapBrowser()
    {
        _practiceSetupState.CloseMapBrowser();
        _practiceMapContextMenu = null;
        _practiceEditField = PracticeEditField.None;
        ClosePracticeMapBrowserPreview();
    }

    private void ConfirmPracticeMapBrowserSelection()
    {
        if (!_practiceSetupState.ConfirmMapBrowserSelection())
        {
            _menuStatusMessage = "Select a map first.";
            return;
        }

        ClosePracticeMapBrowser();
        _menuStatusMessage = string.Empty;
    }

    private bool TryHandlePracticeMapBrowserTextInput(char character)
    {
        if (!_practiceSetupOpen || !_practiceSetupState.MapBrowserOpen || _practiceEditField != PracticeEditField.MapNameFilter)
        {
            return false;
        }

        if (character == '\b')
        {
            var result = DeleteTextSelectionOrBackspace(
                _practiceSetupState.AvailableMapNameFilterBuffer,
                _practiceSetupState.AvailableMapNameFilterCursorIndex,
                _practiceSetupState.AvailableMapNameFilterSelectionStart);
            _practiceSetupState.AvailableMapNameFilterBuffer = result.Text;
            _practiceSetupState.AvailableMapNameFilterCursorIndex = result.CursorIndex;
            _practiceSetupState.AvailableMapNameFilterSelectionStart = result.SelectionStart;
            _practiceSetupState.NotifyAvailableMapFiltersChanged();
            return true;
        }

        if (char.IsControl(character))
        {
            return false;
        }

        var insertResult = InsertTextCharacterAtCursor(
            _practiceSetupState.AvailableMapNameFilterBuffer,
            character,
            _practiceSetupState.AvailableMapNameFilterCursorIndex,
            _practiceSetupState.AvailableMapNameFilterSelectionStart,
            48);
        _practiceSetupState.AvailableMapNameFilterBuffer = insertResult.Text;
        _practiceSetupState.AvailableMapNameFilterCursorIndex = insertResult.CursorIndex;
        _practiceSetupState.AvailableMapNameFilterSelectionStart = insertResult.SelectionStart;
        _practiceSetupState.NotifyAvailableMapFiltersChanged();
        return true;
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
        var rowHeight = shortLayout ? 26 : compactLayout ? 28 : 32;
        var rowGap = shortLayout ? 6 : compactLayout ? 8 : 10;
        var selectorButtonWidth = shortLayout ? 28 : compactLayout ? 32 : 36;
        var contentTop = panel.Y + (shortLayout ? 44 : compactLayout ? 54 : 64);
        var labelWidth = shortLayout ? 112 : compactLayout ? 126 : 150;
        var selectorLeft = panel.X + padding + labelWidth;
        var selectorWidth = panel.Width - (padding * 2) - labelWidth;
        var selectorValueWidth = selectorWidth - (selectorButtonWidth * 2) - 16;
        var buttonHeight = shortLayout ? 30 : compactLayout ? 34 : 38;
        var actionGap = shortLayout ? 6 : compactLayout ? 8 : 12;
        var actionWidth = (panel.Width - (padding * 2) - (actionGap * 2)) / 3;
        var actionsY = panel.Bottom - padding - buttonHeight;

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

        var specialAbilitiesLeftBounds = OffsetPracticeRow(friendlyBotsLeftBounds, rowHeight + rowGap);
        var specialAbilitiesValueBounds = OffsetPracticeRow(friendlyBotsValueBounds, rowHeight + rowGap);
        var specialAbilitiesRightBounds = OffsetPracticeRow(friendlyBotsRightBounds, rowHeight + rowGap);

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
            specialAbilitiesLeftBounds,
            specialAbilitiesValueBounds,
            specialAbilitiesRightBounds,
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
        float buttonScale,
        float valueScale,
        bool leftHighlighted,
        bool valueHighlighted,
        bool rightHighlighted,
        bool enabled = true)
    {
        DrawMenuButtonCentered(leftBounds, "<", leftHighlighted, buttonScale, enabled);
        DrawMenuSelectorValueScaled(valueBounds, valueText, valueHighlighted, valueScale, enabled);
        DrawMenuButtonCentered(rightBounds, ">", rightHighlighted, buttonScale, enabled);
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
            GameModeKind.Scr => "SCR",
            GameModeKind.Vip => "VIP",
            _ => "CTF",
        };
        return entry.IsCustomMap
            ? $"{entry.DisplayName} [{modeLabel}] (Custom)"
            : $"{entry.DisplayName} [{modeLabel}]";
    }
}
