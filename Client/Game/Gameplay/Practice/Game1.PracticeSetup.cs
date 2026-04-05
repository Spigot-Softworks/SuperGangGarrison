#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;
using System;
using System.Globalization;

namespace OpenGarrison.Client;

public partial class Game1
{
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
        Rectangle StartBounds,
        Rectangle ClientPowersBounds,
        Rectangle BackBounds,
        bool CompactLayout);

    private void OpenPracticeSetupMenu()
    {
        CloseInGameMenu();
        _practiceSetupOpen = true;
        _manualConnectOpen = false;
        _hostSetupOpen = false;
        _hostSetupEditField = HostSetupEditField.None;
        CloseLobbyBrowser(clearStatus: false);
        _optionsMenuOpen = false;
        _pluginOptionsMenuOpen = false;
        _controlsMenuOpen = false;
        _creditsOpen = false;
        _clientPowersOpen = false;
        _clientPowersOpenedFromGameplay = false;
        _editingPlayerName = false;
        _menuStatusMessage = string.Empty;

        _practiceMapEntries = BuildPracticeMapEntries();
        if (IsPracticeSessionActive)
        {
            SelectPracticeMapEntry(_world.Level.Name);
        }

        NormalizePracticeSetupState();
    }

    private void UpdatePracticeSetupMenu(KeyboardState keyboard, MouseState mouse)
    {
        if (IsKeyPressed(keyboard, Keys.Escape))
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
        var clickPressed = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton != ButtonState.Pressed;
        if (!clickPressed)
        {
            return;
        }

        var point = new Point(mouse.X, mouse.Y);
        if (layout.MapLeftBounds.Contains(point))
        {
            CyclePracticeMap(-1);
        }
        else if (layout.MapRightBounds.Contains(point))
        {
            CyclePracticeMap(1);
        }
        else if (layout.TickLeftBounds.Contains(point))
        {
            CyclePracticeTickRate(-1);
        }
        else if (layout.TickRightBounds.Contains(point))
        {
            CyclePracticeTickRate(1);
        }
        else if (layout.TimeLeftBounds.Contains(point))
        {
            CyclePracticeTimeLimit(-1);
        }
        else if (layout.TimeRightBounds.Contains(point))
        {
            CyclePracticeTimeLimit(1);
        }
        else if (layout.CapLeftBounds.Contains(point))
        {
            CyclePracticeCapLimit(-1);
        }
        else if (layout.CapRightBounds.Contains(point))
        {
            CyclePracticeCapLimit(1);
        }
        else if (layout.RespawnLeftBounds.Contains(point))
        {
            CyclePracticeRespawn(-1);
        }
        else if (layout.RespawnRightBounds.Contains(point))
        {
            CyclePracticeRespawn(1);
        }
        else if (layout.EnemyBotsLeftBounds.Contains(point))
        {
            CyclePracticeEnemyBots(-1);
        }
        else if (layout.EnemyBotsRightBounds.Contains(point))
        {
            CyclePracticeEnemyBots(1);
        }
        else if (layout.FriendlyBotsLeftBounds.Contains(point))
        {
            CyclePracticeFriendlyBots(-1);
        }
        else if (layout.FriendlyBotsRightBounds.Contains(point))
        {
            CyclePracticeFriendlyBots(1);
        }
        else if (layout.StartBounds.Contains(point))
        {
            TryStartPracticeFromSetup();
        }
        else if (layout.ClientPowersBounds.Contains(point))
        {
            OpenClientPowersMenu(fromGameplay: false);
        }
        else if (layout.BackBounds.Contains(point))
        {
            _practiceSetupOpen = false;
        }
    }

    private void DrawPracticeSetupMenu()
    {
        var viewportWidth = ViewportWidth;
        var viewportHeight = ViewportHeight;
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black * 0.78f);

        var layout = GetPracticeSetupLayout();
        var panel = layout.Panel;
        var compactLayout = layout.CompactLayout;
        const float labelScale = 1f;
        const float valueScale = 1f;
        const float buttonScale = 1f;
        var rowLabelX = panel.X + (compactLayout ? 24f : 28f);
        var rowTextOffset = compactLayout ? 8f : 10f;
        var mapEntry = GetSelectedPracticeMapEntry();

        _spriteBatch.Draw(_pixel, panel, new Color(34, 35, 39, 235));
        _spriteBatch.Draw(_pixel, new Rectangle(panel.X, panel.Y, panel.Width, 3), new Color(210, 210, 210));
        _spriteBatch.Draw(_pixel, new Rectangle(panel.X, panel.Bottom - 3, panel.Width, 3), new Color(76, 76, 76));

        DrawBitmapFontText("Map", new Vector2(rowLabelX, layout.MapValueBounds.Y + rowTextOffset), Color.White, labelScale);
        DrawPracticeSelectorRow(
            layout.MapLeftBounds,
            layout.MapValueBounds,
            layout.MapRightBounds,
            mapEntry is null ? "No local maps available" : GetPracticeMapDisplayLabel(mapEntry),
            compactLayout,
            buttonScale,
            valueScale);

        DrawBitmapFontText("Tick Rate", new Vector2(rowLabelX, layout.TickValueBounds.Y + rowTextOffset), Color.White, labelScale);
        DrawPracticeSelectorRow(
            layout.TickLeftBounds,
            layout.TickValueBounds,
            layout.TickRightBounds,
            _practiceTickRate.ToString(CultureInfo.InvariantCulture),
            compactLayout,
            buttonScale,
            valueScale);

        DrawBitmapFontText("Time Limit", new Vector2(rowLabelX, layout.TimeValueBounds.Y + rowTextOffset), Color.White, labelScale);
        DrawPracticeSelectorRow(
            layout.TimeLeftBounds,
            layout.TimeValueBounds,
            layout.TimeRightBounds,
            $"{_practiceTimeLimitMinutes} min",
            compactLayout,
            buttonScale,
            valueScale);

        DrawBitmapFontText("Cap Limit", new Vector2(rowLabelX, layout.CapValueBounds.Y + rowTextOffset), Color.White, labelScale);
        DrawPracticeSelectorRow(
            layout.CapLeftBounds,
            layout.CapValueBounds,
            layout.CapRightBounds,
            _practiceCapLimit.ToString(CultureInfo.InvariantCulture),
            compactLayout,
            buttonScale,
            valueScale);

        DrawBitmapFontText("Respawn", new Vector2(rowLabelX, layout.RespawnValueBounds.Y + rowTextOffset), Color.White, labelScale);
        DrawPracticeSelectorRow(
            layout.RespawnLeftBounds,
            layout.RespawnValueBounds,
            layout.RespawnRightBounds,
            _practiceRespawnSeconds == 0 ? "Instant" : $"{_practiceRespawnSeconds}s",
            compactLayout,
            buttonScale,
            valueScale);

        DrawBitmapFontText("Enemy Bots", new Vector2(rowLabelX, layout.EnemyBotsValueBounds.Y + rowTextOffset), Color.White, labelScale);
        DrawPracticeSelectorRow(
            layout.EnemyBotsLeftBounds,
            layout.EnemyBotsValueBounds,
            layout.EnemyBotsRightBounds,
            GetPracticeBotCountLabel(_practiceEnemyBotCount),
            compactLayout,
            buttonScale,
            valueScale);

        DrawBitmapFontText("Friendly Bots", new Vector2(rowLabelX, layout.FriendlyBotsValueBounds.Y + rowTextOffset), Color.White, labelScale);
        DrawPracticeSelectorRow(
            layout.FriendlyBotsLeftBounds,
            layout.FriendlyBotsValueBounds,
            layout.FriendlyBotsRightBounds,
            GetPracticeBotCountLabel(_practiceFriendlyBotCount),
            compactLayout,
            buttonScale,
            valueScale);

        DrawMenuButtonScaled(layout.StartBounds, "Start Practice", false, buttonScale);
        DrawMenuButtonScaled(layout.ClientPowersBounds, "Experimental", false, buttonScale);
        DrawMenuButtonScaled(layout.BackBounds, "Back", false, buttonScale);

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
        var padding = compactLayout ? 20 : 28;
        var rowHeight = compactLayout ? 36 : 42;
        var rowGap = compactLayout ? 8 : 10;
        var selectorButtonWidth = compactLayout ? 34 : 40;
        var contentTop = panel.Y + (compactLayout ? 58 : 72);
        var labelWidth = compactLayout ? 126 : 150;
        var selectorLeft = panel.X + padding + labelWidth;
        var selectorWidth = panel.Width - (padding * 2) - labelWidth;
        var selectorValueWidth = selectorWidth - (selectorButtonWidth * 2) - 16;
        var buttonHeight = compactLayout ? 36 : 42;
        var actionGap = compactLayout ? 8 : 12;
        var actionWidth = (panel.Width - (padding * 2) - (actionGap * 2)) / 3;
        var actionsY = panel.Bottom - padding - buttonHeight - 4;

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
        float valueScale)
    {
        DrawMenuButtonScaled(leftBounds, "<", false, buttonScale);
        DrawMenuInputBoxScaled(valueBounds, valueText, active: false, valueScale);
        DrawMenuButtonScaled(rightBounds, ">", false, buttonScale);
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
