#nullable enable

using Microsoft.Xna.Framework;
using System;

namespace OpenGarrison.Client;

internal enum HostSetupScreen
{
    Main,
    Options,
    Maps,
}

internal readonly record struct HostSetupMenuLayout(
    Rectangle Panel,
    HostSetupScreen Screen,
    Rectangle ListBounds,
    Rectangle ToggleBounds,
    Rectangle MoveUpBounds,
    Rectangle MoveDownBounds,
    Rectangle ServerNameBounds,
    Rectangle PortBounds,
    Rectangle SlotsBounds,
    Rectangle PasswordBounds,
    Rectangle RconPasswordBounds,
    Rectangle RotationFileBounds,
    Rectangle TimeLimitBounds,
    Rectangle CapLimitBounds,
    Rectangle RespawnBounds,
    Rectangle LobbyBounds,
    Rectangle AutoBalanceBounds,
    Rectangle SecondaryAbilitiesBounds,
    Rectangle OptionsButtonBounds,
    Rectangle MapsButtonBounds,
    Rectangle OptionsListBounds,
    Rectangle ConfirmBounds,
    Rectangle ResetBounds,
    Rectangle HostBounds,
    Rectangle BackBounds,
    bool CompactLayout,
    int OptionsRowHeight)
{
    public int ContentTop => Math.Max(Panel.Y + (CompactLayout ? 58 : 64), GetContentTopAnchor() - 30);

    public int FooterTop => GetFooterTopAnchor() - (CompactLayout ? 16 : 20);

    public Rectangle ContentViewportBounds => new(
        Panel.X + (CompactLayout ? 12 : 20),
        ContentTop,
        Panel.Width - (CompactLayout ? 28 : 44),
        Math.Max(1, FooterTop - ContentTop));

    public Rectangle ContentScrollbarTrackBounds => new(
        Panel.Right - (CompactLayout ? 16 : 20),
        ContentViewportBounds.Y,
        8,
        ContentViewportBounds.Height);

    public int ListHeaderHeight => CompactLayout ? 18 : 20;

    public int RowHeight => CompactLayout ? 20 : 28;

    public Rectangle ListRowsBounds => new(
        ListBounds.X,
        ListBounds.Y + ListHeaderHeight,
        ListBounds.Width,
        ListBounds.Height - ListHeaderHeight);

    public int VisibleRowCapacity => Math.Max(1, ListRowsBounds.Height / RowHeight);

    public int OptionsVisibleRowCapacity => Math.Max(1, OptionsListBounds.Height / Math.Max(1, OptionsRowHeight));

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

    private int GetContentTopAnchor()
    {
        return Screen switch
        {
            HostSetupScreen.Options => OptionsListBounds.Y,
            HostSetupScreen.Maps => ListBounds.Y,
            _ => ServerNameBounds.Y,
        };
    }

    private int GetFooterTopAnchor()
    {
        return Screen switch
        {
            HostSetupScreen.Options => ConfirmBounds.Y,
            HostSetupScreen.Maps => ConfirmBounds.Y,
            _ => HostBounds.Y,
        };
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
        bool isServerLauncherMode,
        HostSetupScreen screen)
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
        return screen switch
        {
            HostSetupScreen.Options => compactLayout
                ? CreateCompactOptionsLayout(panel, isServerLauncherMode)
                : CreateRoomyOptionsLayout(panel, isServerLauncherMode),
            HostSetupScreen.Maps => compactLayout
                ? CreateCompactMapsLayout(panel, mapCount, isServerLauncherMode)
                : CreateRoomyMapsLayout(panel, mapCount, isServerLauncherMode),
            _ => compactLayout
                ? CreateCompactMainLayout(panel, isServerLauncherMode)
                : CreateRoomyMainLayout(panel, isServerLauncherMode),
        };
    }

    private static HostSetupMenuLayout CreateCompactMainLayout(Rectangle panel, bool isServerLauncherMode)
    {
        var padding = 18;
        var fieldHeight = 26;
        var fullFieldRowSpacing = 50;
        var smallGap = 8;
        var columnGap = 16;
        var navStackGap = 12;
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

        var contentTop = panel.Y + (isServerLauncherMode ? 102 : 68);
        var firstFieldY = contentTop + 18;
        ComputeMainScreenColumns(panel, padding, columnGap, out var leftX, out var leftColumnWidth, out var fieldX, out var fieldWidth);
        var navButtonHeight = ComputeMainNavButtonHeight(navAreaBottom: hostBounds.Y - 12, firstFieldY, navStackGap, compact: true);
        var optionsButtonBounds = new Rectangle(leftX, firstFieldY, leftColumnWidth, navButtonHeight);
        var mapsButtonBounds = new Rectangle(leftX, optionsButtonBounds.Bottom + navStackGap, leftColumnWidth, navButtonHeight);

        var serverNameBounds = new Rectangle(fieldX, firstFieldY, fieldWidth, fieldHeight);

        var doubleFieldWidth = Math.Max(88, (fieldWidth - smallGap) / 2);
        var passwordBounds = new Rectangle(fieldX, serverNameBounds.Y + fullFieldRowSpacing, doubleFieldWidth, fieldHeight);
        var rconPasswordBounds = new Rectangle(
            passwordBounds.Right + smallGap,
            passwordBounds.Y,
            fieldWidth - doubleFieldWidth - smallGap,
            fieldHeight);
        var rotationFileBounds = CreateRotationFileInputBounds(
            fieldX,
            passwordBounds.Y + fullFieldRowSpacing,
            fieldWidth,
            fieldHeight,
            compactLayout: true);

        var portBounds = new Rectangle(fieldX, rotationFileBounds.Y + fullFieldRowSpacing, doubleFieldWidth, fieldHeight);
        var slotsBounds = new Rectangle(portBounds.Right + smallGap, portBounds.Y, fieldWidth - doubleFieldWidth - smallGap, fieldHeight);

        var lobbyBounds = new Rectangle(fieldX, portBounds.Y + fullFieldRowSpacing, fieldWidth, 32);

        return CreateEmptyAuxiliaryLayout(
            panel,
            HostSetupScreen.Main,
            serverNameBounds,
            portBounds,
            slotsBounds,
            passwordBounds,
            rconPasswordBounds,
            rotationFileBounds,
            lobbyBounds,
            optionsButtonBounds,
            mapsButtonBounds,
            hostBounds,
            backBounds,
            compactLayout: true,
            optionsRowHeight: 28);
    }

    private static HostSetupMenuLayout CreateRoomyMainLayout(Rectangle panel, bool isServerLauncherMode)
    {
        var padding = 36;
        var fieldHeight = 32;
        var fullFieldRowSpacing = 58;
        var fieldGap = 12;
        var columnGap = 24;
        var navStackGap = 14;
        var actionButtonHeight = 42;
        var actionButtonWidth = 140;
        var actionGap = 20;
        var actionPaddingBottom = 20;
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

        var contentTop = panel.Y + (isServerLauncherMode ? 106 : 74);
        var firstFieldY = contentTop + 18;
        ComputeMainScreenColumns(panel, padding, columnGap, out var leftX, out var leftColumnWidth, out var fieldX, out var fieldWidth);
        var navButtonHeight = ComputeMainNavButtonHeight(navAreaBottom: hostBounds.Y - 16, firstFieldY, navStackGap, compact: false);
        var optionsButtonBounds = new Rectangle(leftX, firstFieldY, leftColumnWidth, navButtonHeight);
        var mapsButtonBounds = new Rectangle(leftX, optionsButtonBounds.Bottom + navStackGap, leftColumnWidth, navButtonHeight);

        var serverNameBounds = new Rectangle(fieldX, firstFieldY, fieldWidth, fieldHeight);

        var doubleFieldWidth = Math.Max(100, (fieldWidth - fieldGap) / 2);
        var passwordBounds = new Rectangle(fieldX, serverNameBounds.Y + fullFieldRowSpacing, doubleFieldWidth, fieldHeight);
        var rconPasswordBounds = new Rectangle(
            passwordBounds.Right + fieldGap,
            passwordBounds.Y,
            fieldWidth - doubleFieldWidth - fieldGap,
            fieldHeight);
        var rotationFileBounds = CreateRotationFileInputBounds(
            fieldX,
            passwordBounds.Y + fullFieldRowSpacing,
            fieldWidth,
            fieldHeight,
            compactLayout: true);

        var portBounds = new Rectangle(fieldX, rotationFileBounds.Y + fullFieldRowSpacing, doubleFieldWidth, fieldHeight);
        var slotsBounds = new Rectangle(portBounds.Right + fieldGap, portBounds.Y, fieldWidth - doubleFieldWidth - fieldGap, fieldHeight);

        var lobbyBounds = new Rectangle(fieldX, portBounds.Y + fullFieldRowSpacing, fieldWidth, 34);

        return CreateEmptyAuxiliaryLayout(
            panel,
            HostSetupScreen.Main,
            serverNameBounds,
            portBounds,
            slotsBounds,
            passwordBounds,
            rconPasswordBounds,
            rotationFileBounds,
            lobbyBounds,
            optionsButtonBounds,
            mapsButtonBounds,
            hostBounds,
            backBounds,
            compactLayout: false,
            optionsRowHeight: 30);
    }

    private static HostSetupMenuLayout CreateCompactOptionsLayout(Rectangle panel, bool isServerLauncherMode)
    {
        var padding = 18;
        var actionButtonHeight = 36;
        var actionButtonWidth = 120;
        var actionGap = 12;
        var actionPaddingBottom = 18;
        var confirmBounds = new Rectangle(
            panel.Right - padding - actionButtonWidth,
            panel.Bottom - actionPaddingBottom - actionButtonHeight,
            actionButtonWidth,
            actionButtonHeight);
        var resetBounds = new Rectangle(
            confirmBounds.X - actionGap - actionButtonWidth,
            confirmBounds.Y,
            actionButtonWidth,
            actionButtonHeight);

        var optionsRowHeight = 28;
        var contentTop = panel.Y + (isServerLauncherMode ? 102 : 68);
        var listTop = contentTop + 18;
        var listHeight = Math.Max(optionsRowHeight * 5, confirmBounds.Y - listTop - 16);
        var optionsListBounds = new Rectangle(panel.X + padding, listTop, panel.Width - (padding * 2) - 20, listHeight);

        return CreateOptionsAuxiliaryLayout(
            panel,
            optionsListBounds,
            confirmBounds,
            resetBounds,
            compactLayout: true,
            optionsRowHeight);
    }

    private static HostSetupMenuLayout CreateRoomyOptionsLayout(Rectangle panel, bool isServerLauncherMode)
    {
        var padding = 36;
        var actionButtonHeight = 42;
        var actionButtonWidth = 140;
        var actionGap = 20;
        var actionPaddingBottom = 20;
        var confirmBounds = new Rectangle(
            panel.Right - padding - actionButtonWidth,
            panel.Bottom - actionPaddingBottom - actionButtonHeight,
            actionButtonWidth,
            actionButtonHeight);
        var resetBounds = new Rectangle(
            confirmBounds.X - actionGap - actionButtonWidth,
            confirmBounds.Y,
            actionButtonWidth,
            actionButtonHeight);

        var optionsRowHeight = 30;
        var contentTop = panel.Y + (isServerLauncherMode ? 106 : 74);
        var listTop = contentTop + 18;
        var listHeight = Math.Max(optionsRowHeight * 5, confirmBounds.Y - listTop - 20);
        var optionsListBounds = new Rectangle(panel.X + padding, listTop, panel.Width - (padding * 2) - 20, listHeight);

        return CreateOptionsAuxiliaryLayout(
            panel,
            optionsListBounds,
            confirmBounds,
            resetBounds,
            compactLayout: false,
            optionsRowHeight);
    }

    private static HostSetupMenuLayout CreateCompactMapsLayout(Rectangle panel, int mapCount, bool isServerLauncherMode)
    {
        var padding = 18;
        var actionButtonHeight = 36;
        var actionButtonWidth = 120;
        var actionPaddingBottom = 18;
        var confirmBounds = new Rectangle(
            panel.Right - padding - actionButtonWidth,
            panel.Bottom - actionPaddingBottom - actionButtonHeight,
            actionButtonWidth,
            actionButtonHeight);

        var listHeaderHeight = 18;
        var rowHeight = 20;
        var contentTop = panel.Y + (isServerLauncherMode ? 102 : 68);
        var listX = panel.X + padding;
        var listWidth = panel.Width - (padding * 2) - 20;
        var listButtonHeight = 28;
        var listButtonGap = 8;
        var footerAreaHeight = actionButtonHeight + actionPaddingBottom + listButtonHeight + 24;
        var availableListHeight = Math.Max(156, panel.Bottom - footerAreaHeight - contentTop);
        var maxListHeight = listHeaderHeight + (Math.Max(1, mapCount) * rowHeight);
        var listHeight = Math.Min(Math.Max(220, maxListHeight), availableListHeight);
        var listBounds = new Rectangle(listX, contentTop + 18, listWidth, listHeight);

        var listButtonWidth = Math.Max(78, (listBounds.Width - (listButtonGap * 2)) / 3);
        var toggleBounds = new Rectangle(listBounds.X, listBounds.Bottom + 10, listButtonWidth, listButtonHeight);
        var moveUpBounds = new Rectangle(toggleBounds.Right + listButtonGap, toggleBounds.Y, listButtonWidth, listButtonHeight);
        var moveDownBounds = new Rectangle(moveUpBounds.Right + listButtonGap, toggleBounds.Y, listButtonWidth, listButtonHeight);

        return CreateMapsAuxiliaryLayout(
            panel,
            listBounds,
            toggleBounds,
            moveUpBounds,
            moveDownBounds,
            confirmBounds,
            compactLayout: true,
            optionsRowHeight: 28);
    }

    private static HostSetupMenuLayout CreateRoomyMapsLayout(Rectangle panel, int mapCount, bool isServerLauncherMode)
    {
        var padding = 36;
        var actionButtonHeight = 42;
        var actionButtonWidth = 140;
        var actionPaddingBottom = 20;
        var confirmBounds = new Rectangle(
            panel.Right - padding - actionButtonWidth,
            panel.Bottom - actionPaddingBottom - actionButtonHeight,
            actionButtonWidth,
            actionButtonHeight);

        var listHeaderHeight = 20;
        var rowHeight = 28;
        var contentTop = panel.Y + (isServerLauncherMode ? 106 : 74);
        var listX = panel.X + padding;
        var listWidth = panel.Width - (padding * 2) - 20;
        var listButtonHeight = 34;
        var listButtonGap = 12;
        var footerAreaHeight = actionButtonHeight + actionPaddingBottom + listButtonHeight + 28;
        var availableListHeight = Math.Max(240, panel.Bottom - footerAreaHeight - contentTop);
        var maxListHeight = listHeaderHeight + (Math.Max(1, mapCount) * rowHeight);
        var listHeight = Math.Min(Math.Max(300, maxListHeight), availableListHeight);
        var listBounds = new Rectangle(listX, contentTop + 18, listWidth, listHeight);

        var toggleBounds = new Rectangle(listBounds.X, listBounds.Bottom + 14, 116, listButtonHeight);
        var moveUpBounds = new Rectangle(toggleBounds.Right + listButtonGap, toggleBounds.Y, 116, listButtonHeight);
        var moveDownBounds = new Rectangle(moveUpBounds.Right + listButtonGap, toggleBounds.Y, 116, listButtonHeight);

        return CreateMapsAuxiliaryLayout(
            panel,
            listBounds,
            toggleBounds,
            moveUpBounds,
            moveDownBounds,
            confirmBounds,
            compactLayout: false,
            optionsRowHeight: 30);
    }

    private static HostSetupMenuLayout CreateEmptyAuxiliaryLayout(
        Rectangle panel,
        HostSetupScreen screen,
        Rectangle serverNameBounds,
        Rectangle portBounds,
        Rectangle slotsBounds,
        Rectangle passwordBounds,
        Rectangle rconPasswordBounds,
        Rectangle rotationFileBounds,
        Rectangle lobbyBounds,
        Rectangle optionsButtonBounds,
        Rectangle mapsButtonBounds,
        Rectangle hostBounds,
        Rectangle backBounds,
        bool compactLayout,
        int optionsRowHeight)
    {
        return new HostSetupMenuLayout(
            panel,
            screen,
            Rectangle.Empty,
            Rectangle.Empty,
            Rectangle.Empty,
            Rectangle.Empty,
            serverNameBounds,
            portBounds,
            slotsBounds,
            passwordBounds,
            rconPasswordBounds,
            rotationFileBounds,
            Rectangle.Empty,
            Rectangle.Empty,
            Rectangle.Empty,
            lobbyBounds,
            Rectangle.Empty,
            Rectangle.Empty,
            optionsButtonBounds,
            mapsButtonBounds,
            Rectangle.Empty,
            Rectangle.Empty,
            Rectangle.Empty,
            hostBounds,
            backBounds,
            compactLayout,
            optionsRowHeight);
    }

    private static HostSetupMenuLayout CreateOptionsAuxiliaryLayout(
        Rectangle panel,
        Rectangle optionsListBounds,
        Rectangle confirmBounds,
        Rectangle resetBounds,
        bool compactLayout,
        int optionsRowHeight)
    {
        var fieldX = optionsListBounds.X;
        var fieldWidth = optionsListBounds.Width;
        var rowHeight = compactLayout ? 26 : 32;
        var rowSpacing = compactLayout ? 48 : 58;
        var firstRowY = optionsListBounds.Y;
        var timeLimitBounds = new Rectangle(fieldX, firstRowY, fieldWidth, rowHeight);
        var capLimitBounds = new Rectangle(fieldX, timeLimitBounds.Y + rowSpacing, fieldWidth, rowHeight);
        var respawnBounds = new Rectangle(fieldX, capLimitBounds.Y + rowSpacing, fieldWidth, rowHeight);
        var autoBalanceBounds = new Rectangle(fieldX, respawnBounds.Y + rowSpacing, fieldWidth, compactLayout ? 34 : 38);
        var secondaryAbilitiesBounds = new Rectangle(fieldX, autoBalanceBounds.Y + (compactLayout ? 36 : 44), fieldWidth, compactLayout ? 34 : 38);

        return new HostSetupMenuLayout(
            panel,
            HostSetupScreen.Options,
            Rectangle.Empty,
            Rectangle.Empty,
            Rectangle.Empty,
            Rectangle.Empty,
            Rectangle.Empty,
            Rectangle.Empty,
            Rectangle.Empty,
            Rectangle.Empty,
            Rectangle.Empty,
            Rectangle.Empty,
            timeLimitBounds,
            capLimitBounds,
            respawnBounds,
            Rectangle.Empty,
            autoBalanceBounds,
            secondaryAbilitiesBounds,
            Rectangle.Empty,
            Rectangle.Empty,
            optionsListBounds,
            confirmBounds,
            resetBounds,
            Rectangle.Empty,
            Rectangle.Empty,
            compactLayout,
            optionsRowHeight);
    }

    private static HostSetupMenuLayout CreateMapsAuxiliaryLayout(
        Rectangle panel,
        Rectangle listBounds,
        Rectangle toggleBounds,
        Rectangle moveUpBounds,
        Rectangle moveDownBounds,
        Rectangle confirmBounds,
        bool compactLayout,
        int optionsRowHeight)
    {
        return new HostSetupMenuLayout(
            panel,
            HostSetupScreen.Maps,
            listBounds,
            toggleBounds,
            moveUpBounds,
            moveDownBounds,
            Rectangle.Empty,
            Rectangle.Empty,
            Rectangle.Empty,
            Rectangle.Empty,
            Rectangle.Empty,
            Rectangle.Empty,
            Rectangle.Empty,
            Rectangle.Empty,
            Rectangle.Empty,
            Rectangle.Empty,
            Rectangle.Empty,
            Rectangle.Empty,
            Rectangle.Empty,
            Rectangle.Empty,
            Rectangle.Empty,
            confirmBounds,
            Rectangle.Empty,
            Rectangle.Empty,
            Rectangle.Empty,
            compactLayout,
            optionsRowHeight);
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

    private static int ComputeMainNavButtonHeight(int navAreaBottom, int firstFieldY, int navStackGap, bool compact)
    {
        var availableNavStack = Math.Max(0, navAreaBottom - firstFieldY);
        var quarterStackHeight = Math.Max(1, (availableNavStack - navStackGap) / 4);
        return Math.Clamp(quarterStackHeight, compact ? 40 : 44, compact ? 52 : 58);
    }

    private static Rectangle CreateRotationFileInputBounds(
        int fieldX,
        int rowY,
        int fieldWidth,
        int fieldHeight,
        bool compactLayout)
    {
        var reserve = HostSetupMainScreenLayout.GetPlaylistFileControlReserve(compactLayout);
        return new Rectangle(fieldX + reserve, rowY, fieldWidth - reserve, fieldHeight);
    }

    private static void ComputeMainScreenColumns(
        Rectangle panel,
        int padding,
        int columnGap,
        out int leftX,
        out int leftColumnWidth,
        out int fieldX,
        out int fieldWidth)
    {
        const int scrollbarReserve = 20;
        var contentWidth = panel.Width - (padding * 2) - scrollbarReserve;
        leftColumnWidth = Math.Max(1, (contentWidth - columnGap) / 3);
        fieldWidth = contentWidth - columnGap - leftColumnWidth;
        leftX = panel.X + padding;
        fieldX = leftX + leftColumnWidth + columnGap;
    }

    private static bool IsCompact(Rectangle panel)
    {
        return panel.Width <= 760 || panel.Height <= 520;
    }
}
