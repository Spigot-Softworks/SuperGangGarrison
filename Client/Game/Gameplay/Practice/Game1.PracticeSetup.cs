#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;
using System;
using System.Globalization;

namespace OpenGarrison.Client;

public partial class Game1
{
    private int _practiceSetupControllerIndex;

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

        _practiceMapEntries = BuildPracticeMapEntries();
        if (IsPracticeSessionActive)
        {
            SelectPracticeMapEntry(_world.Level.Name);
        }

        NormalizePracticeSetupState();
    }

    private void UpdatePracticeSetupMenu(KeyboardState keyboard, MouseState mouse)
    {
        if (IsKeyPressed(keyboard, Keys.Escape) || IsControllerMenuBackPressed())
        {
            _practiceSetupOpen = false;
            return;
        }

        if (IsKeyPressed(keyboard, Keys.Enter))
        {
            TryStartPracticeFromSetup();
            return;
        }

        var layout = GetPracticeSetupLayout();
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
        }
    }

    private void ActivatePracticeSetupControllerRow(int rowIndex)
    {
        switch (rowIndex)
        {
            case >= 0 and <= 6:
                AdjustPracticeSetupControllerRow(rowIndex, 1);
                break;
            case 7:
                TogglePracticeSpecialAbilities();
                break;
            case 8:
                TryStartPracticeFromSetup();
                break;
            case 9:
                OpenClientPowersMenu(fromGameplay: false);
                break;
            case 10:
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
            GameModeKind.Scr => "SCR",
            _ => "CTF",
        };
        return entry.IsCustomMap
            ? $"{entry.DisplayName} [{modeLabel}] (Custom)"
            : $"{entry.DisplayName} [{modeLabel}]";
    }
}
