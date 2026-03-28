#nullable enable

using Microsoft.Xna.Framework;
using System;

namespace OpenGarrison.Client;

internal readonly record struct HostSetupMenuLayout(
    Rectangle Panel,
    Rectangle ListBounds,
    Rectangle ToggleBounds,
    Rectangle MoveUpBounds,
    Rectangle MoveDownBounds,
    Rectangle ServerNameBounds,
    Rectangle PortBounds,
    Rectangle SlotsBounds,
    Rectangle PasswordBounds,
    Rectangle RotationFileBounds,
    Rectangle TimeLimitBounds,
    Rectangle CapLimitBounds,
    Rectangle RespawnBounds,
    Rectangle LobbyBounds,
    Rectangle AutoBalanceBounds,
    Rectangle HostBounds,
    Rectangle BackBounds,
    bool CompactLayout)
{
    public int ListHeaderHeight => CompactLayout ? 18 : 20;

    public int RowHeight => CompactLayout ? 20 : 28;

    public Rectangle ListRowsBounds => new(
        ListBounds.X,
        ListBounds.Y + ListHeaderHeight,
        ListBounds.Width,
        ListBounds.Height - ListHeaderHeight);

    public Vector2 StatusPosition => CompactLayout
        ? new Vector2(Panel.X + 28f, Panel.Y + 62f)
        : new Vector2(Panel.X + 28f, Panel.Bottom - 38f);

    public Rectangle TerminalButtonBounds
    {
        get
        {
            var padding = CompactLayout ? 18 : 36;
            var actionGap = CompactLayout ? 12 : 20;
            var actionButtonHeight = CompactLayout ? 36 : 42;
            var actionPaddingBottom = CompactLayout ? 18 : 20;
            var actionButtonWidth = CompactLayout ? 120 : 140;
            var terminalWidth = CompactLayout ? 136 : 150;
            var y = Panel.Bottom - actionPaddingBottom - actionButtonHeight;
            var backX = Panel.Right - padding - actionButtonWidth;
            var hostX = backX - actionGap - actionButtonWidth;
            return new Rectangle(hostX - actionGap - terminalWidth, y, terminalWidth, actionButtonHeight);
        }
    }
}

internal readonly record struct ServerLauncherTabLayout(
    Rectangle SettingsTabBounds,
    Rectangle ConsoleTabBounds);

internal readonly record struct HostedServerConsoleLayout(
    Rectangle LogBounds,
    Rectangle SummaryBounds,
    Rectangle CommandBounds,
    Rectangle SendBounds,
    Rectangle ClearBounds,
    Rectangle StatusCommandBounds,
    Rectangle PlayersCommandBounds,
    Rectangle RotationCommandBounds,
    Rectangle HelpCommandBounds,
    Rectangle HostBounds,
    Rectangle BackBounds,
    bool CompactLayout);

internal static class HostSetupMenuLayoutCalculator
{
    public static HostSetupMenuLayout CreateMenuLayout(
        int viewportWidth,
        int viewportHeight,
        int mapCount,
        bool isServerLauncherMode)
    {
        var compactViewport = viewportWidth <= 864 || viewportHeight <= 624;
        var panelWidth = compactViewport
            ? Math.Min(760, viewportWidth - 40)
            : Math.Min(960, viewportWidth - 48);
        var panelHeight = compactViewport
            ? Math.Min(520, viewportHeight - 40)
            : Math.Min(620, viewportHeight - 48);
        var panel = new Rectangle(
            (viewportWidth - panelWidth) / 2,
            (viewportHeight - panelHeight) / 2,
            panelWidth,
            panelHeight);

        var compactLayout = IsCompact(panel);
        if (compactLayout)
        {
            var padding = 18;
            var listHeaderHeight = 18;
            var rowHeight = 20;
            var listWidth = Math.Min(288, Math.Max(248, (panel.Width - (padding * 2) - 20) / 2));
            var listX = panel.X + padding;
            var contentTop = panel.Y + (isServerLauncherMode ? 92 : 84);
            var availableListHeight = Math.Max(170, panel.Bottom - 156 - contentTop);
            var maxListHeight = listHeaderHeight + (Math.Max(1, mapCount) * rowHeight);
            var listHeight = Math.Min(Math.Min(236, maxListHeight), availableListHeight);
            var listBounds = new Rectangle(listX, contentTop, listWidth, listHeight);

            var listButtonGap = 8;
            var listButtonHeight = 28;
            var listButtonWidth = Math.Max(78, (listBounds.Width - (listButtonGap * 2)) / 3);
            var toggleBounds = new Rectangle(listBounds.X, listBounds.Bottom + 10, listButtonWidth, listButtonHeight);
            var moveUpBounds = new Rectangle(toggleBounds.Right + listButtonGap, toggleBounds.Y, listButtonWidth, listButtonHeight);
            var moveDownBounds = new Rectangle(moveUpBounds.Right + listButtonGap, toggleBounds.Y, listButtonWidth, listButtonHeight);

            var fieldX = listBounds.Right + 20;
            var fieldWidth = panel.Right - fieldX - padding;
            var fieldHeight = 26;
            var smallGap = 8;
            var smallRowSpacing = 44;

            var serverNameBounds = new Rectangle(fieldX, contentTop, fieldWidth, fieldHeight);
            var passwordBounds = new Rectangle(fieldX, serverNameBounds.Bottom + 10, fieldWidth, fieldHeight);
            var rotationFileBounds = new Rectangle(fieldX, passwordBounds.Bottom + 10, fieldWidth, fieldHeight);

            var tripleFieldWidth = Math.Max(70, (fieldWidth - (smallGap * 2)) / 3);
            var portBounds = new Rectangle(fieldX, rotationFileBounds.Bottom + 18, tripleFieldWidth, fieldHeight);
            var slotsBounds = new Rectangle(portBounds.Right + smallGap, portBounds.Y, tripleFieldWidth, fieldHeight);
            var timeLimitBounds = new Rectangle(slotsBounds.Right + smallGap, portBounds.Y, fieldWidth - (tripleFieldWidth * 2) - (smallGap * 2), fieldHeight);

            var doubleFieldWidth = Math.Max(100, (fieldWidth - smallGap) / 2);
            var capLimitBounds = new Rectangle(fieldX, portBounds.Y + smallRowSpacing, doubleFieldWidth, fieldHeight);
            var respawnBounds = new Rectangle(capLimitBounds.Right + smallGap, capLimitBounds.Y, fieldWidth - doubleFieldWidth - smallGap, fieldHeight);

            var listButtonScaleHeight = 28;
            var lobbyBounds = new Rectangle(fieldX, capLimitBounds.Bottom + 18, fieldWidth, listButtonScaleHeight);
            var autoBalanceBounds = new Rectangle(fieldX, lobbyBounds.Bottom + 6, fieldWidth, listButtonScaleHeight);

            var actionButtonHeight = 36;
            var actionButtonWidth = 120;
            var actionGap = 12;
            var actionPaddingBottom = 18;
            var backBounds = new Rectangle(
                panel.Right - padding - actionButtonWidth,
                panel.Bottom - actionPaddingBottom - actionButtonHeight,
                actionButtonWidth,
                actionButtonHeight);
            var hostBounds = new Rectangle(
                backBounds.X - actionGap - actionButtonWidth,
                backBounds.Y,
                actionButtonWidth,
                actionButtonHeight);

            return new HostSetupMenuLayout(
                panel,
                listBounds,
                toggleBounds,
                moveUpBounds,
                moveDownBounds,
                serverNameBounds,
                portBounds,
                slotsBounds,
                passwordBounds,
                rotationFileBounds,
                timeLimitBounds,
                capLimitBounds,
                respawnBounds,
                lobbyBounds,
                autoBalanceBounds,
                hostBounds,
                backBounds,
                compactLayout);
        }

        var roomyPadding = 36;
        var roomyInterColumnGap = 46;
        var roomyListWidth = 392;
        var roomyMinFieldWidth = 410;
        var roomyMaxListWidth = panel.Width - (roomyPadding * 2) - roomyInterColumnGap - roomyMinFieldWidth;
        if (roomyMaxListWidth < roomyListWidth)
        {
            roomyListWidth = Math.Max(320, roomyMaxListWidth);
        }

        var roomyListBounds = new Rectangle(panel.X + roomyPadding, panel.Y + 96, roomyListWidth, 328);
        var roomyToggleBounds = new Rectangle(roomyListBounds.X, roomyListBounds.Bottom + 14, 116, 34);
        var roomyMoveUpBounds = new Rectangle(roomyToggleBounds.Right + 12, roomyToggleBounds.Y, 116, 34);
        var roomyMoveDownBounds = new Rectangle(roomyMoveUpBounds.Right + 12, roomyToggleBounds.Y, 116, 34);

        var roomyFieldX = roomyListBounds.Right + roomyInterColumnGap;
        var roomyFieldWidth = panel.Right - roomyFieldX - roomyPadding;
        var roomyServerNameBounds = new Rectangle(roomyFieldX, panel.Y + 100, roomyFieldWidth, 32);
        var roomyPortBounds = new Rectangle(roomyFieldX, panel.Y + 150, roomyFieldWidth, 32);
        var roomySlotsBounds = new Rectangle(roomyFieldX, panel.Y + 200, roomyFieldWidth, 32);
        var roomyPasswordBounds = new Rectangle(roomyFieldX, panel.Y + 250, roomyFieldWidth, 32);
        var roomyRotationFileBounds = new Rectangle(roomyFieldX, panel.Y + 300, roomyFieldWidth, 32);
        var roomyTimeLimitBounds = new Rectangle(roomyFieldX, panel.Y + 350, roomyFieldWidth, 32);
        var roomyCapLimitBounds = new Rectangle(roomyFieldX, panel.Y + 400, roomyFieldWidth, 32);
        var roomyRespawnBounds = new Rectangle(roomyFieldX, panel.Y + 450, roomyFieldWidth, 32);

        var roomyBackBounds = new Rectangle(panel.Right - roomyPadding - 140, panel.Bottom - 20 - 42, 140, 42);
        var roomyHostBounds = new Rectangle(roomyBackBounds.X - 20 - 140, roomyBackBounds.Y, 140, 42);
        var roomyLobbyBounds = new Rectangle(roomyFieldX, roomyHostBounds.Y - 88, roomyFieldWidth, 34);
        var roomyAutoBalanceBounds = new Rectangle(roomyFieldX, roomyLobbyBounds.Bottom + 6, roomyFieldWidth, 34);

        return new HostSetupMenuLayout(
            panel,
            roomyListBounds,
            roomyToggleBounds,
            roomyMoveUpBounds,
            roomyMoveDownBounds,
            roomyServerNameBounds,
            roomyPortBounds,
            roomySlotsBounds,
            roomyPasswordBounds,
            roomyRotationFileBounds,
            roomyTimeLimitBounds,
            roomyCapLimitBounds,
            roomyRespawnBounds,
            roomyLobbyBounds,
            roomyAutoBalanceBounds,
            roomyHostBounds,
            roomyBackBounds,
            compactLayout);
    }

    public static ServerLauncherTabLayout CreateServerLauncherTabLayout(Rectangle panel)
    {
        return new ServerLauncherTabLayout(
            new Rectangle(panel.Right - 332, panel.Y + 18, 146, 32),
            new Rectangle(panel.Right - 176, panel.Y + 18, 146, 32));
    }

    public static HostedServerConsoleLayout CreateHostedServerConsoleLayout(Rectangle panel)
    {
        var compactLayout = IsCompact(panel);
        var padding = compactLayout ? 20 : 28;
        var sectionGap = compactLayout ? 12 : 18;
        var actionGap = compactLayout ? 12 : 20;
        var actionButtonHeight = compactLayout ? 38 : 42;
        var actionPaddingBottom = compactLayout ? 12 : 20;
        var actionButtonWidth = compactLayout ? 124 : 140;
        var commandButtonHeight = compactLayout ? 30 : 34;
        var commandButtonGap = compactLayout ? 8 : 10;
        var contentTop = panel.Y + (compactLayout ? 84 : 96);

        var backBounds = new Rectangle(
            panel.Right - padding - actionButtonWidth,
            panel.Bottom - actionPaddingBottom - actionButtonHeight,
            actionButtonWidth,
            actionButtonHeight);
        var hostBounds = new Rectangle(
            backBounds.X - actionGap - actionButtonWidth,
            backBounds.Y,
            actionButtonWidth,
            actionButtonHeight);

        var commandY = backBounds.Y - (compactLayout ? 46 : 52);
        var availableWidth = panel.Width - (padding * 2) - sectionGap;
        var logWidth = compactLayout
            ? Math.Clamp((int)MathF.Floor(availableWidth * 0.62f), 360, Math.Max(360, availableWidth - 190))
            : 574;
        var summaryWidth = Math.Max(compactLayout ? 190 : 220, availableWidth - logWidth);
        logWidth = availableWidth - summaryWidth;
        var contentHeight = Math.Max(compactLayout ? 220 : 320, commandY - contentTop - 12);

        var logBounds = new Rectangle(panel.X + padding, contentTop, logWidth, contentHeight);
        var summaryBounds = new Rectangle(logBounds.Right + sectionGap, contentTop, summaryWidth, contentHeight);
        var commandBounds = new Rectangle(logBounds.X, commandY, Math.Max(180, logBounds.Width - (compactLayout ? 168 : 178)), commandButtonHeight);
        var sendBounds = new Rectangle(commandBounds.Right + commandButtonGap, commandBounds.Y, compactLayout ? 68 : 78, commandButtonHeight);
        var clearBounds = new Rectangle(sendBounds.Right + commandButtonGap, commandBounds.Y, compactLayout ? 68 : 78, commandButtonHeight);
        var statusCommandBounds = new Rectangle(summaryBounds.X, commandBounds.Y, compactLayout ? 58 : 64, commandButtonHeight);
        var playersCommandBounds = new Rectangle(statusCommandBounds.Right + 8, statusCommandBounds.Y, compactLayout ? 66 : 72, commandButtonHeight);
        var rotationCommandBounds = new Rectangle(playersCommandBounds.Right + 8, statusCommandBounds.Y, compactLayout ? 72 : 78, commandButtonHeight);
        var helpCommandBounds = new Rectangle(rotationCommandBounds.Right + 8, statusCommandBounds.Y, compactLayout ? 56 : 60, commandButtonHeight);

        return new HostedServerConsoleLayout(
            logBounds,
            summaryBounds,
            commandBounds,
            sendBounds,
            clearBounds,
            statusCommandBounds,
            playersCommandBounds,
            rotationCommandBounds,
            helpCommandBounds,
            hostBounds,
            backBounds,
            compactLayout);
    }

    private static bool IsCompact(Rectangle panel)
    {
        return panel.Width <= 760 || panel.Height <= 520;
    }
}
