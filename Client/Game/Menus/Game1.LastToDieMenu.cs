#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace OpenGarrison.Client;

public partial class Game1
{
    private enum LastToDieMenuPage
    {
        Root,
        Difficulty,
        CoOp,
        Rankings,
        RoomJoin,
        RoomLoading,
        RoomError,
        PeerLobby,
    }

    private enum LastToDieRankingsTab
    {
        Personal,
        Global,
    }

    private enum LastToDieRankingsSort
    {
        Score,
        Rounds,
    }

    private readonly record struct LastToDieMenuLayout(
        Rectangle PlaqueBounds,
        Rectangle ContentBounds,
        Rectangle[] ButtonBounds,
        float Scale);

    private bool _lastToDieMenuOpen;
    private LastToDieMenuPage _lastToDieMenuPage;
    private int _lastToDieMenuHoverIndex = -1;
    private bool _lastToDieFriendsHover;
    private LoadedSpriteFrame? _lastToDieLogoTexture;
    private string? _lastToDieLogoTexturePath;
    private Task<OpenGarrison.ClientShared.LastToDieRankingsResponse>? _lastToDieRankingsTask;
    private OpenGarrison.ClientShared.LastToDieRankingsResponse? _lastToDieRankings;
    private Task<OpenGarrison.ClientShared.LastToDieLeaderboardResponse>? _lastToDieLeaderboardTask;
    private OpenGarrison.ClientShared.LastToDieLeaderboardResponse? _lastToDieLeaderboard;
    private string _lastToDieRankingsError = string.Empty;
    private string _lastToDieLeaderboardError = string.Empty;
    private LastToDieRankingsTab _lastToDieRankingsTab;
    private LastToDieRankingsSort _lastToDieRankingsSort;
    private int _lastToDieRankingsScrollOffset;

    private bool IsLastToDieMenuActive()
    {
        return _mainMenuOpen
            && (_lastToDieMenuOpen
                || _lastToDieRoomCodeJoinOpen
                || _lastToDieConnectionPresentationPending);
    }

    private void OpenLastToDieMenu(string? statusMessage = null)
    {
        _practiceCoOpMenu = false;
        _mainMenuOverlayStateController.OpenLastToDieMenu(statusMessage);
    }

    private void CloseLastToDieMenu(bool clearStatus = false)
    {
        _mainMenuOverlayStateController.CloseLastToDieMenu(clearStatus);
    }

    private void ReturnToLastToDieMenu(string? statusMessage = null)
    {
        StopLastToDieGameOverSound();
        ReturnToMainMenu(statusMessage);
        OpenLastToDieMenu(statusMessage);
    }

    private void UpdateLastToDieMenu(KeyboardState keyboard, MouseState mouse)
    {
        UpdateLastToDieRankingsRequest();
        if (_lastToDieMenuPage == LastToDieMenuPage.PeerLobby) { UpdatePeerLobby(keyboard, mouse); return; }
        var buttonLabels = GetLastToDieMenuButtonLabels();
        if (buttonLabels.Length == 0)
        {
            return;
        }

        var layout = GetLastToDieMenuLayout(buttonLabels.Length, _lastToDieMenuPage == LastToDieMenuPage.Rankings);
        if (IsKeyPressed(keyboard, Keys.Escape) || IsControllerMenuBackPressed())
        {
            if (_lastToDieMenuPage == LastToDieMenuPage.RoomError) { RestoreManagedRoomOrigin(); return; }
            if (_lastToDieMenuPage is LastToDieMenuPage.RoomJoin or LastToDieMenuPage.RoomLoading)
            {
                if (_managedReconnecting) { ReturnToLastToDieMenu(); return; }
                CancelManagedRoomRequest();
                CancelPeerRoomRequest();
                if (_lastToDieMenuPage == LastToDieMenuPage.RoomLoading) RestoreManagedRoomOrigin();
                else { _lastToDieMenuPage = LastToDieMenuPage.CoOp; _menuStatusMessage = string.Empty; }
                return;
            }
            if (_lastToDieMenuPage == LastToDieMenuPage.Rankings)
            {
                OpenLastToDieRankingsPage(false);
            }
            else if (_lastToDieMenuPage == LastToDieMenuPage.Difficulty)
            {
                OpenLastToDieDifficultyPage(false);
            }
            else if (_lastToDieMenuPage == LastToDieMenuPage.CoOp)
            {
                if (_practiceCoOpMenu) { CloseLastToDieMenu(); OpenPracticeSetupMenu(); }
                else OpenLastToDieCoOpPage(false);
            }
            else
            {
                CloseLastToDieMenu();
            }

            return;
        }

        if (_lastToDieMenuPage == LastToDieMenuPage.Rankings
            && UpdateLastToDieRankingsInput(keyboard, mouse, layout))
        {
            return;
        }

        var navigationCount = buttonLabels.Length + (IsRestrictedBrowserEdition ? 0 : 1);
        if (IsKeyPressed(keyboard, Keys.Up))
        {
            SetLastToDieMenuHoverIndex((_lastToDieMenuHoverIndex <= 0 ? navigationCount : _lastToDieMenuHoverIndex) - 1, navigationCount);
        }
        else if (IsKeyPressed(keyboard, Keys.Down))
        {
            SetLastToDieMenuHoverIndex((_lastToDieMenuHoverIndex + 1 + navigationCount) % navigationCount, navigationCount);
        }

        if (TryConsumeControllerMenuNavigation(out _, out var verticalStep) && verticalStep != 0)
        {
            SetLastToDieMenuHoverIndex(
                MoveControllerMenuSelection(_lastToDieMenuHoverIndex, navigationCount, verticalStep),
                navigationCount);
        }

        var useMouseHover = ShouldUseMouseMenuHover(mouse);
        var friendsBounds = GetLastToDieFriendsButtonBounds(layout.Scale);
        _lastToDieFriendsHover = !IsRestrictedBrowserEdition && useMouseHover && friendsBounds.Contains(mouse.Position);
        var hoveredButtonIndex = useMouseHover
            ? GetHoveredLastToDieMenuButtonIndex(mouse.Position, layout)
            : -1;
        if (hoveredButtonIndex >= 0)
        {
            _lastToDieMenuHoverIndex = hoveredButtonIndex;
        }
        else if (_lastToDieFriendsHover)
        {
            _lastToDieMenuHoverIndex = buttonLabels.Length;
        }
        else if (IsControllerMenuInputActive() && _lastToDieMenuHoverIndex < 0)
        {
            SetLastToDieMenuHoverIndex(0, navigationCount);
        }
        else if (_lastToDieMenuHoverIndex < 0 && buttonLabels.Length > 0)
        {
            _lastToDieMenuHoverIndex = Math.Clamp(_lastToDieMenuHoverIndex, -1, buttonLabels.Length - 1);
        }

        if (IsKeyPressed(keyboard, Keys.Enter) || IsControllerMenuConfirmPressed())
        {
            if (_lastToDieMenuHoverIndex == buttonLabels.Length)
            {
                OpenFriendsMenu();
            }
            else
            {
                ActivateLastToDieMenuButton(_lastToDieMenuHoverIndex >= 0 ? _lastToDieMenuHoverIndex : 0);
            }
            return;
        }

        var clickPressed = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton != ButtonState.Pressed;
        if (clickPressed && _lastToDieFriendsHover)
        {
            OpenFriendsMenu();
        }
        else if (clickPressed && hoveredButtonIndex >= 0)
        {
            ActivateLastToDieMenuButton(hoveredButtonIndex);
        }
    }

    private string[] GetLastToDieMenuButtonLabels()
    {
        return _lastToDieMenuPage switch
        {
            LastToDieMenuPage.RoomJoin => [$"Code: {_managedRoomCodeBuffer}", "Join", "Back"],
            LastToDieMenuPage.RoomLoading => ["Cancel"],
            LastToDieMenuPage.RoomError => ["Retry", "Back"],
            LastToDieMenuPage.Rankings => ["Back"],
            LastToDieMenuPage.Difficulty => ["Standard", "Hardcore", "Back"],
            LastToDieMenuPage.CoOp => ["Create", "Join", "Back"],
            _ => ["Play Solo", "Play Co-Op", "Rankings", "Back"],
        };
    }

    private void ActivateLastToDieMenuButton(int index)
    {
        switch (_lastToDieMenuPage)
        {
            case LastToDieMenuPage.RoomError:
                if (index == 0) BeginPeerRoom(_managedRoomReturnPage != LastToDieMenuPage.RoomJoin, _peerRoomRequest?.Settings);
                else if (index == 1) RestoreManagedRoomOrigin();
                break;
            case LastToDieMenuPage.RoomJoin:
                if (index == 1) BeginPeerRoom(false);
                else if (index == 2) { _lastToDieMenuPage = LastToDieMenuPage.CoOp; _menuStatusMessage = string.Empty; }
                break;
            case LastToDieMenuPage.RoomLoading:
                if (_managedReconnecting) { ReturnToLastToDieMenu(); break; }
                CancelManagedRoomRequest();
                CancelPeerRoomRequest();
                RestoreManagedRoomOrigin();
                break;
            case LastToDieMenuPage.Rankings:
                if (index == 0)
                {
                    OpenLastToDieRankingsPage(false);
                }
                break;

            case LastToDieMenuPage.Difficulty:
                switch (index)
                {
                    case 0:
                        TryStartSoloLastToDieRun(OpenGarrison.Core.LastToDie.LastToDieDifficulty.Standard);
                        break;
                    case 1:
                        TryStartSoloLastToDieRun(OpenGarrison.Core.LastToDie.LastToDieDifficulty.Hardcore);
                        break;
                    case 2:
                        OpenLastToDieDifficultyPage(false);
                        break;
                }
                break;

            case LastToDieMenuPage.CoOp:
                switch (index)
                {
                    case 0:
                        BeginPeerRoom(true);
                        break;
                    case 1:
                        OpenLastToDieRoomCodeJoin();
                        break;
                    case 2:
                        if (_practiceCoOpMenu) { CloseLastToDieMenu(); OpenPracticeSetupMenu(); }
                        else OpenLastToDieCoOpPage(false);
                        break;
                }
                break;

            default:
                switch (index)
                {
                    case 0:
                        OpenLastToDieDifficultyPage(true);
                        break;
                    case 1:
                        OpenLastToDieCoOpPage(true);
                        break;
                    case 2:
                        OpenLastToDieRankingsPage(true);
                        break;
                    case 3:
                        CloseLastToDieMenu();
                        break;
                }
                break;
        }
    }

    private void OpenLastToDieRankingsPage(bool open)
    {
        _lastToDieMenuPage = open ? LastToDieMenuPage.Rankings : LastToDieMenuPage.Root;
        _lastToDieMenuHoverIndex = 0;
        if (open)
        {
            _lastToDieRankings = null;
            _lastToDieLeaderboard = null;
            _lastToDieRankingsError = string.Empty;
            _lastToDieLeaderboardError = string.Empty;
            _lastToDieRankingsTab = LastToDieRankingsTab.Personal;
            _lastToDieRankingsSort = LastToDieRankingsSort.Score;
            _lastToDieRankingsScrollOffset = 0;
            _lastToDieRankingsTask = _presenceClient.GetLastToDieRankingsAsync(_clientIdentity);
        }
        else
        {
            _lastToDieRankingsTask = null;
            _lastToDieLeaderboardTask = null;
        }
    }

    private bool UpdateLastToDieRankingsInput(
        KeyboardState keyboard,
        MouseState mouse,
        LastToDieMenuLayout layout)
    {
        var (personalTab, globalTab) = GetLastToDieRankingsTabBounds(layout);
        var (scoreSort, roundsSort) = GetLastToDieRankingsSortBounds(layout);
        var listBounds = GetLastToDieRankingsListBounds(layout);
        var visibleRows = GetLastToDieRankingsVisibleRowCount(layout);
        var clickPressed = mouse.LeftButton == ButtonState.Pressed
            && _previousMouse.LeftButton != ButtonState.Pressed;

        if (clickPressed && personalTab.Contains(mouse.Position))
        {
            SetLastToDieRankingsTab(LastToDieRankingsTab.Personal);
            return true;
        }

        if (clickPressed && globalTab.Contains(mouse.Position))
        {
            SetLastToDieRankingsTab(LastToDieRankingsTab.Global);
            return true;
        }

        if (IsKeyPressed(keyboard, Keys.Tab))
        {
            SetLastToDieRankingsTab(
                _lastToDieRankingsTab == LastToDieRankingsTab.Personal
                    ? LastToDieRankingsTab.Global
                    : LastToDieRankingsTab.Personal);
            return true;
        }

        if (_lastToDieRankingsTab != LastToDieRankingsTab.Global)
        {
            return false;
        }

        if (clickPressed && scoreSort.Contains(mouse.Position))
        {
            SetLastToDieRankingsSort(LastToDieRankingsSort.Score);
            return true;
        }

        if (clickPressed && roundsSort.Contains(mouse.Position))
        {
            SetLastToDieRankingsSort(LastToDieRankingsSort.Rounds);
            return true;
        }

        if (IsKeyPressed(keyboard, Keys.S))
        {
            SetLastToDieRankingsSort(
                _lastToDieRankingsSort == LastToDieRankingsSort.Score
                    ? LastToDieRankingsSort.Rounds
                    : LastToDieRankingsSort.Score);
            return true;
        }

        var scrollDelta = 0;
        var wheelDelta = mouse.ScrollWheelValue - _previousMouse.ScrollWheelValue;
        if (wheelDelta != 0 && listBounds.Contains(mouse.Position))
        {
            scrollDelta = (wheelDelta > 0 ? -1 : 1) * Math.Max(1, Math.Abs(wheelDelta) / 120);
        }
        else if (IsKeyPressed(keyboard, Keys.Up))
        {
            scrollDelta = -1;
        }
        else if (IsKeyPressed(keyboard, Keys.Down))
        {
            scrollDelta = 1;
        }
        else if (IsKeyPressed(keyboard, Keys.PageUp))
        {
            scrollDelta = -visibleRows;
        }
        else if (IsKeyPressed(keyboard, Keys.PageDown))
        {
            scrollDelta = visibleRows;
        }
        else if (IsKeyPressed(keyboard, Keys.Home))
        {
            SetLastToDieRankingsScrollOffset(0, visibleRows);
            return true;
        }
        else if (IsKeyPressed(keyboard, Keys.End))
        {
            SetLastToDieRankingsScrollOffset(int.MaxValue, visibleRows);
            return true;
        }
        else if (TryConsumeControllerMenuNavigation(out var horizontalStep, out var verticalStep))
        {
            if (horizontalStep != 0)
            {
                SetLastToDieRankingsSort(
                    _lastToDieRankingsSort == LastToDieRankingsSort.Score
                        ? LastToDieRankingsSort.Rounds
                        : LastToDieRankingsSort.Score);
                return true;
            }

            scrollDelta = verticalStep;
        }

        if (scrollDelta != 0)
        {
            SetLastToDieRankingsScrollOffset(
                _lastToDieRankingsScrollOffset + scrollDelta,
                visibleRows);
            return true;
        }

        return false;
    }

    private void SetLastToDieRankingsTab(LastToDieRankingsTab tab)
    {
        if (_lastToDieRankingsTab == tab)
        {
            return;
        }

        _lastToDieRankingsTab = tab;
        _lastToDieRankingsScrollOffset = 0;
        if (tab == LastToDieRankingsTab.Global && _lastToDieLeaderboardTask is null)
        {
            RequestLastToDieLeaderboard(0);
        }
    }

    private void SetLastToDieRankingsSort(LastToDieRankingsSort sort)
    {
        if (_lastToDieRankingsSort == sort && _lastToDieLeaderboard is not null)
        {
            return;
        }

        _lastToDieRankingsSort = sort;
        _lastToDieRankingsScrollOffset = 0;
        RequestLastToDieLeaderboard(0);
    }

    private void SetLastToDieRankingsScrollOffset(int requestedOffset, int visibleRows)
    {
        var total = Math.Max(0, _lastToDieLeaderboard?.Total ?? 0);
        var maximumOffset = Math.Max(0, total - Math.Max(1, visibleRows));
        _lastToDieRankingsScrollOffset = Math.Clamp(requestedOffset, 0, maximumOffset);

        var page = _lastToDieLeaderboard;
        if (_lastToDieLeaderboardTask is null
            && (page is null
                || _lastToDieRankingsScrollOffset < page.Offset
                || _lastToDieRankingsScrollOffset + visibleRows > page.Offset + page.Entries.Count))
        {
            RequestLastToDieLeaderboard(_lastToDieRankingsScrollOffset);
        }
    }

    private void UpdateLastToDieRankingsRequest()
    {
        var rankingsTask = _lastToDieRankingsTask;
        if (rankingsTask is { IsCompleted: true })
        {
            _lastToDieRankingsTask = null;
            if (rankingsTask.IsCompletedSuccessfully)
            {
                _lastToDieRankings = rankingsTask.Result;
                _lastToDieRankingsError = string.Empty;
            }
            else
            {
                _lastToDieRankingsError = rankingsTask.IsCanceled
                    ? "Rankings request was canceled."
                    : rankingsTask.Exception?.GetBaseException().Message ?? "Rankings are temporarily unavailable.";
            }
        }

        var leaderboardTask = _lastToDieLeaderboardTask;
        if (leaderboardTask is not { IsCompleted: true })
        {
            return;
        }

        _lastToDieLeaderboardTask = null;
        if (leaderboardTask.IsCompletedSuccessfully)
        {
            _lastToDieLeaderboard = leaderboardTask.Result;
            _lastToDieLeaderboardError = string.Empty;
        }
        else
        {
            _lastToDieLeaderboardError = leaderboardTask.IsCanceled
                ? "Leaderboard request was canceled."
                : leaderboardTask.Exception?.GetBaseException().Message ?? "Leaderboard is temporarily unavailable.";
        }
    }

    private void RequestLastToDieLeaderboard(int offset)
    {
        _lastToDieLeaderboard = null;
        _lastToDieLeaderboardError = string.Empty;
        _lastToDieLeaderboardTask = _presenceClient.GetLastToDieLeaderboardAsync(
            _lastToDieRankingsSort == LastToDieRankingsSort.Score ? "score" : "round",
            limit: 25,
            offset: Math.Max(0, offset));
    }

    private void OpenLastToDieDifficultyPage(bool open)
    {
        _lastToDieMenuPage = open ? LastToDieMenuPage.Difficulty : LastToDieMenuPage.Root;
        _lastToDieMenuHoverIndex = 0;
    }

    private void OpenLastToDieCoOpPage(bool open)
    {
        _lastToDieMenuPage = open ? LastToDieMenuPage.CoOp : LastToDieMenuPage.Root;
        _lastToDieMenuHoverIndex = 0;
    }

    private void SetLastToDieMenuHoverIndex(int index, int itemCount)
    {
        if (itemCount <= 0)
        {
            _lastToDieMenuHoverIndex = -1;
            return;
        }

        _lastToDieMenuHoverIndex = Math.Clamp(index, 0, itemCount - 1);
    }

    private LastToDieMenuLayout GetLastToDieMenuLayout(int buttonCount, bool statsPage)
    {
        var plaqueTexture = _lastToDieMenuPlaqueTexture ?? _menuPlaqueTexture;
        var buttonTexture = _lastToDieMenuTextBoxSoloTexture ?? _menuTextBoxSoloTexture;
        if (plaqueTexture is null || buttonTexture is null)
        {
            return new LastToDieMenuLayout(Rectangle.Empty, Rectangle.Empty, [], 1f);
        }

        var fittedScale = MathF.Min(
            1f,
            MathF.Min(
                (ViewportWidth - 48f) / Math.Max(1f, plaqueTexture.Width),
                (ViewportHeight - 72f) / Math.Max(1f, plaqueTexture.Height)));
        var scale = fittedScale * (statsPage ? 0.92f : 0.78f);
        if (scale <= 0f)
        {
            scale = statsPage ? 0.92f : 0.78f;
        }

        var plaqueX = (int)MathF.Round(MathF.Max(24f, ViewportWidth * 0.05f));
        var plaqueWidth = Math.Max(1, (int)MathF.Round(plaqueTexture.Width * scale));
        if (statsPage)
        {
            plaqueWidth = Math.Min(
                Math.Max(1, ViewportWidth - plaqueX - 24),
                Math.Max(plaqueWidth, (int)MathF.Round(plaqueWidth * 1.45f)));
        }

        var plaqueBounds = new Rectangle(
            plaqueX,
            (int)MathF.Round((ViewportHeight - (plaqueTexture.Height * scale)) * 0.5f),
            plaqueWidth,
            Math.Max(1, (int)MathF.Round(plaqueTexture.Height * scale)));

        var contentBounds = new Rectangle(
            plaqueBounds.X + (int)MathF.Round(28f * scale),
            plaqueBounds.Y + (int)MathF.Round(34f * scale),
            plaqueBounds.Width - (int)MathF.Round(56f * scale),
            plaqueBounds.Height - (int)MathF.Round(68f * scale));

        var buttonWidth = Math.Max(1, (int)MathF.Round(buttonTexture.Width * scale));
        var buttonHeight = Math.Max(1, (int)MathF.Round(buttonTexture.Height * scale));
        var buttonX = plaqueBounds.X + ((plaqueBounds.Width - buttonWidth) / 2);
        var buttonGap = (int)MathF.Round(16f * scale);
        var buttonBounds = new Rectangle[Math.Max(0, buttonCount)];

        if (buttonCount > 0)
        {
            if (statsPage)
            {
                buttonBounds[0] = new Rectangle(
                    buttonX,
                    plaqueBounds.Bottom - buttonHeight - (int)MathF.Round(18f * scale),
                    buttonWidth,
                    buttonHeight);
            }
            else
            {
                var totalHeight = (buttonCount * buttonHeight) + ((buttonCount - 1) * buttonGap);
                var startY = plaqueBounds.Y + ((plaqueBounds.Height - totalHeight) / 2);
                for (var index = 0; index < buttonCount; index += 1)
                {
                    buttonBounds[index] = new Rectangle(buttonX, startY + index * (buttonHeight + buttonGap), buttonWidth, buttonHeight);
                }
            }
        }

        return new LastToDieMenuLayout(plaqueBounds, contentBounds, buttonBounds, scale);
    }

    private static int GetHoveredLastToDieMenuButtonIndex(Point mousePosition, LastToDieMenuLayout layout)
    {
        for (var index = 0; index < layout.ButtonBounds.Length; index += 1)
        {
            if (layout.ButtonBounds[index].Contains(mousePosition))
            {
                return index;
            }
        }

        return -1;
    }

    private Rectangle GetLastToDieFriendsButtonBounds(float scale)
    {
        var texture = _lastToDieMenuTextBoxSoloTexture ?? _menuTextBoxSoloTexture;
        if (texture is null)
        {
            return Rectangle.Empty;
        }

        const int bottomBarHeight = 76;
        var width = Math.Max(1, (int)MathF.Round(texture.Width * scale));
        var height = Math.Max(1, (int)MathF.Round(texture.Height * scale));
        var rightMargin = MathF.Max(20f, ViewportWidth * 0.04f);
        return new Rectangle(
            ViewportWidth - (int)MathF.Round(rightMargin) - width,
            ViewportHeight - bottomBarHeight + ((bottomBarHeight - height) / 2),
            width,
            height);
    }

    private static (Rectangle Personal, Rectangle Global) GetLastToDieRankingsTabBounds(
        LastToDieMenuLayout layout)
    {
        var gap = Math.Max(8, (int)MathF.Round(10f * layout.Scale));
        var width = Math.Max(110, (int)MathF.Round(150f * layout.Scale));
        var height = Math.Max(28, (int)MathF.Round(34f * layout.Scale));
        var y = layout.ContentBounds.Y + Math.Max(28, (int)MathF.Round(38f * layout.Scale));
        return (
            new Rectangle(layout.ContentBounds.X, y, width, height),
            new Rectangle(layout.ContentBounds.X + width + gap, y, width, height));
    }

    private static (Rectangle Score, Rectangle Rounds) GetLastToDieRankingsSortBounds(
        LastToDieMenuLayout layout)
    {
        var (_, globalTab) = GetLastToDieRankingsTabBounds(layout);
        var gap = Math.Max(8, (int)MathF.Round(10f * layout.Scale));
        var width = Math.Max(132, (int)MathF.Round(170f * layout.Scale));
        var height = Math.Max(25, (int)MathF.Round(29f * layout.Scale));
        var y = globalTab.Bottom + Math.Max(8, (int)MathF.Round(10f * layout.Scale));
        return (
            new Rectangle(layout.ContentBounds.X, y, width, height),
            new Rectangle(layout.ContentBounds.X + width + gap, y, width, height));
    }

    private static Rectangle GetLastToDieRankingsListBounds(LastToDieMenuLayout layout)
    {
        var (_, roundsSort) = GetLastToDieRankingsSortBounds(layout);
        var top = roundsSort.Bottom + Math.Max(8, (int)MathF.Round(12f * layout.Scale));
        var bottom = layout.ButtonBounds.Length > 0
            ? layout.ButtonBounds[0].Y - Math.Max(8, (int)MathF.Round(12f * layout.Scale))
            : layout.ContentBounds.Bottom;
        return new Rectangle(
            layout.ContentBounds.X,
            top,
            Math.Max(1, layout.ContentBounds.Width),
            Math.Max(1, bottom - top));
    }

    private static int GetLastToDieRankingsVisibleRowCount(LastToDieMenuLayout layout)
    {
        var list = GetLastToDieRankingsListBounds(layout);
        var rowHeight = Math.Max(22, (int)MathF.Round(27f * layout.Scale));
        var headerHeight = Math.Max(22, (int)MathF.Round(25f * layout.Scale));
        return Math.Max(1, (list.Height - headerHeight) / rowHeight);
    }

    private void DrawLastToDieMenu()
    {
        if (_lastToDieMenuPage == LastToDieMenuPage.PeerLobby) { DrawPeerLobby(); return; }
        var viewportWidth = ViewportWidth;
        var viewportHeight = ViewportHeight;
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), new Color(4, 6, 10, 220));

        // The LTD screen has its own gray footer treatment. Animated backgrounds
        // additionally get the usual runner silhouettes.
        const int bottomBarHeight = 76;
        var barY = viewportHeight - bottomBarHeight;
        var bottomBarBounds = new Rectangle(0, barY, viewportWidth, bottomBarHeight);
        _spriteBatch.Draw(_pixel, bottomBarBounds, new Color(0x4b, 0x4d, 0x50));
        if (_menuBackgroundMode != MenuBackgroundMode.Static)
        {
            _menuBottomBarRunners.Draw(bottomBarBounds);
        }

        DrawLastToDieMenuLogo(viewportWidth);

        var buttonLabels = GetLastToDieMenuButtonLabels();
        var layout = GetLastToDieMenuLayout(buttonLabels.Length, _lastToDieMenuPage == LastToDieMenuPage.Rankings);
        var plaqueTexture = _lastToDieMenuPlaqueTexture ?? _menuPlaqueTexture;
        var buttonTexture = _lastToDieMenuTextBoxSoloTexture ?? _menuTextBoxSoloTexture;
        if (plaqueTexture is not null && layout.PlaqueBounds != Rectangle.Empty)
        {
            DrawLoadedSpriteFrame(plaqueTexture, layout.PlaqueBounds, Color.White);
        }

        DrawLastToDieMenuHeader(layout);
        if (_lastToDieMenuPage == LastToDieMenuPage.Rankings)
        {
            DrawLastToDieRankingsPage(layout);
        }
        else if (_lastToDieMenuPage == LastToDieMenuPage.Difficulty)
        {
            DrawLastToDieDifficultyPage(layout);
        }

        for (var index = 0; index < layout.ButtonBounds.Length && index < buttonLabels.Length; index += 1)
        {
            DrawLastToDieMenuButton(buttonTexture, layout.ButtonBounds[index], buttonLabels[index], hovered: index == _lastToDieMenuHoverIndex, layout.Scale);
        }

        var friendsBounds = GetLastToDieFriendsButtonBounds(layout.Scale);
        if (!IsRestrictedBrowserEdition) DrawLastToDieMenuButton(
            buttonTexture,
            friendsBounds,
            "Friends",
            hovered: _lastToDieFriendsHover || _lastToDieMenuHoverIndex == buttonLabels.Length,
            layout.Scale);

        var statusMessage = GetLastToDieMenuStatusMessage();
        if (!string.IsNullOrWhiteSpace(statusMessage))
        {
            const float statusScale = 0.60f;
            var width = viewportWidth - 48f;
            var lines = WrapMenuBitmapText(statusMessage.Replace('\n', ' ').Replace('\r', ' '), width, statusScale);
            for (var line = 0; line < Math.Min(3, lines.Count); line++)
            {
                var text = lines[line] + (line == 2 && lines.Count > 3 ? "..." : "");
                DrawShadowedMenuBitmapFontText(TrimBitmapMenuText(text, width, statusScale),
                    new Vector2(24f, viewportHeight - 64f + line * 18f), Color.White, statusScale);
            }
        }
    }

    private void DrawLastToDieMenuHeader(LastToDieMenuLayout layout)
    {
        if (layout.ContentBounds == Rectangle.Empty)
        {
            return;
        }

        if (_lastToDieMenuPage == LastToDieMenuPage.Root)
        {
            return;
        }

        var title = _lastToDieMenuPage switch
        {
            LastToDieMenuPage.Rankings => "Rankings",
            LastToDieMenuPage.Difficulty => "Difficulty",
            LastToDieMenuPage.CoOp => _practiceCoOpMenu ? "Practice Co-op" : "Co-op",
            LastToDieMenuPage.RoomError => _managedRoomReturnPage == LastToDieMenuPage.Difficulty ? "Solo" : "Co-op",
            _ => "Last to Die",
        };
        if (_lastToDieMenuPage == LastToDieMenuPage.Rankings)
        {
            var scale = Math.Clamp(1.25f * layout.Scale, 0.95f, 1.25f);
            var position = new Vector2(layout.ContentBounds.X, layout.ContentBounds.Y);
            DrawBitmapFontText(title, position + Vector2.One, Color.Black * 0.65f, scale);
            DrawBitmapFontText(title, position, Color.White, scale);
        }
        else
        {
            DrawShadowedMenuBitmapFontText(
                title,
                new Vector2(layout.ContentBounds.X, layout.ContentBounds.Y),
                Color.White,
                1.06f * layout.Scale);
        }
    }

    private void DrawLastToDieRankingsPage(LastToDieMenuLayout layout)
    {
        if (layout.ContentBounds == Rectangle.Empty)
        {
            return;
        }

        var (personalTab, globalTab) = GetLastToDieRankingsTabBounds(layout);
        DrawLastToDieRankingsChoice(personalTab, "Personal", _lastToDieRankingsTab == LastToDieRankingsTab.Personal);
        DrawLastToDieRankingsChoice(globalTab, "Global", _lastToDieRankingsTab == LastToDieRankingsTab.Global);

        if (_lastToDieRankingsTab == LastToDieRankingsTab.Personal)
        {
            DrawLastToDiePersonalRankings(layout, personalTab.Bottom);
        }
        else
        {
            DrawLastToDieGlobalRankings(layout);
        }
    }

    private void DrawLastToDiePersonalRankings(LastToDieMenuLayout layout, int tabsBottom)
    {
        var scale = Math.Clamp(layout.Scale * 1.05f, 0.82f, 1.05f);
        var lineHeight = Math.Max(22f, MeasureBitmapFontHeight(scale) + 8f);
        var x = layout.ContentBounds.X + 3f;
        var y = tabsBottom + Math.Max(16f, 20f * layout.Scale);

        void DrawLine(string text, Color color, float lineScale = 1f)
        {
            var actualScale = scale * lineScale;
            var visible = TrimBitmapMenuText(text, layout.ContentBounds.Width - 12f, actualScale);
            var position = new Vector2(x, y);
            DrawBitmapFontText(visible, position + Vector2.One, Color.Black * 0.58f, actualScale);
            DrawBitmapFontText(visible, position, color, actualScale);
            y += lineHeight;
        }

        DrawLine("Online record", new Color(241, 210, 120), 1.08f);
        if (_lastToDieRankingsTask is not null && _lastToDieRankings is null)
        {
            DrawLine("Loading personal stats...", Color.White);
        }
        else if (_lastToDieRankings is { } rankings)
        {
            var player = rankings.Player;
            var scoreRank = player.ScoreRank > 0 ? $"  Global rank #{player.ScoreRank}" : string.Empty;
            var roundRank = player.RoundRank > 0 ? $"  Global rank #{player.RoundRank}" : string.Empty;
            DrawLine($"Best score       {FormatLastToDieScore(player.BestScoreUnits)}{scoreRank}", Color.White);
            DrawLine($"Rounds completed {player.HighestRound}{roundRank}", Color.White);
            DrawLine($"Runs played      {player.RunsPlayed}", Color.White);
        }
        else
        {
            DrawLine(
                string.IsNullOrWhiteSpace(_lastToDieRankingsError)
                    ? "Online stats are temporarily unavailable."
                    : _lastToDieRankingsError,
                new Color(240, 130, 130));
        }

        y += Math.Max(6f, 8f * layout.Scale);
        DrawLine("This device", new Color(241, 210, 120), 1.08f);
        DrawLine($"Best score       {FormatLastToDieScore(_lastToDieStats.BestScoreUnits)}", Color.White);
        DrawLine($"Rounds completed {_lastToDieStats.HighestRoundCompleted}", Color.White);
        DrawLine($"Runs played      {_lastToDieStats.RunsPlayed}", Color.White);
    }

    private void DrawLastToDieGlobalRankings(LastToDieMenuLayout layout)
    {
        var (scoreSort, roundsSort) = GetLastToDieRankingsSortBounds(layout);
        DrawLastToDieRankingsChoice(scoreSort, "Highest score", _lastToDieRankingsSort == LastToDieRankingsSort.Score);
        DrawLastToDieRankingsChoice(roundsSort, "Rounds completed", _lastToDieRankingsSort == LastToDieRankingsSort.Rounds);

        var list = GetLastToDieRankingsListBounds(layout);
        var scale = Math.Clamp(layout.Scale, 0.78f, 1f);
        var headerHeight = Math.Max(22, (int)MathF.Round(25f * layout.Scale));
        var rowHeight = Math.Max(22, (int)MathF.Round(27f * layout.Scale));
        var rightPadding = 15f;
        var roundsRight = list.Right - rightPadding;
        var scoreRight = roundsRight - Math.Max(72f, 86f * layout.Scale);
        var nameX = list.X + Math.Max(42f, 49f * layout.Scale);
        var nameWidth = Math.Max(50f, scoreRight - nameX - 14f);

        void DrawText(string text, Vector2 position, Color color, float textScale = -1f)
        {
            var actualScale = textScale > 0f ? textScale : scale;
            DrawBitmapFontText(text, position + Vector2.One, Color.Black * 0.58f, actualScale);
            DrawBitmapFontText(text, position, color, actualScale);
        }

        DrawText("#", new Vector2(list.X + 3f, list.Y), new Color(220, 205, 170));
        DrawText("Player", new Vector2(nameX, list.Y), new Color(220, 205, 170));
        DrawBitmapFontTextRightAligned("Score", new Vector2(scoreRight, list.Y), new Color(220, 205, 170), scale);
        DrawBitmapFontTextRightAligned("Rounds", new Vector2(roundsRight, list.Y), new Color(220, 205, 170), scale);
        _spriteBatch.Draw(_pixel, new Rectangle(list.X, list.Y + headerHeight - 4, list.Width, 1), new Color(178, 172, 157));

        if (_lastToDieLeaderboardTask is not null && _lastToDieLeaderboard is null)
        {
            DrawText("Loading global rankings...", new Vector2(list.X + 3f, list.Y + headerHeight + 5f), Color.White);
            return;
        }

        if (_lastToDieLeaderboard is null)
        {
            var error = string.IsNullOrWhiteSpace(_lastToDieLeaderboardError)
                ? "Global rankings are temporarily unavailable."
                : _lastToDieLeaderboardError;
            DrawText(
                TrimBitmapMenuText(error, list.Width - 12f, scale),
                new Vector2(list.X + 3f, list.Y + headerHeight + 5f),
                new Color(240, 130, 130));
            return;
        }

        var page = _lastToDieLeaderboard;
        if (page.Total <= 0)
        {
            DrawText("No recorded runs yet.", new Vector2(list.X + 3f, list.Y + headerHeight + 5f), Color.White);
            return;
        }

        var firstPageIndex = Math.Max(0, _lastToDieRankingsScrollOffset - page.Offset);
        var visibleRows = GetLastToDieRankingsVisibleRowCount(layout);
        for (var row = 0; row < visibleRows && firstPageIndex + row < page.Entries.Count; row += 1)
        {
            var entry = page.Entries[firstPageIndex + row];
            var y = list.Y + headerHeight + (row * rowHeight);
            if ((entry.Rank & 1) == 0)
            {
                _spriteBatch.Draw(
                    _pixel,
                    new Rectangle(list.X, y - 3, list.Width, rowHeight),
                    Color.Black * 0.12f);
            }

            DrawText($"{entry.Rank}", new Vector2(list.X + 3f, y), Color.White);
            var name = TrimBitmapMenuText(entry.DisplayName, nameWidth, scale);
            DrawText(name, new Vector2(nameX, y), Color.White);
            DrawBitmapFontTextRightAligned(
                FormatLastToDieScore(entry.BestScoreUnits),
                new Vector2(scoreRight, y),
                Color.White,
                scale);
            DrawBitmapFontTextRightAligned(
                entry.HighestRound.ToString(),
                new Vector2(roundsRight, y),
                Color.White,
                scale);
        }

        DrawLastToDieRankingsScrollbar(layout, list, visibleRows, page.Total);
    }

    private void DrawLastToDieRankingsChoice(Rectangle bounds, string label, bool active)
    {
        var fill = active ? new Color(126, 33, 31) : new Color(38, 38, 39);
        var outline = active ? new Color(245, 213, 135) : new Color(157, 154, 147);
        DrawRoundedRectangleOutline(bounds, fill, outline, outlineThickness: 1, radius: 3);
        var scale = Math.Clamp(bounds.Height / 34f, 0.72f, 0.94f);
        var visible = TrimBitmapMenuText(label, bounds.Width - 14f, scale);
        var position = new Vector2(
            bounds.X + ((bounds.Width - MeasureBitmapFontWidth(visible, scale)) * 0.5f),
            bounds.Y + MathF.Max(3f, ((bounds.Height - MeasureBitmapFontHeight(scale)) * 0.5f) - 1f));
        DrawBitmapFontText(visible, position + Vector2.One, Color.Black * 0.55f, scale);
        DrawBitmapFontText(visible, position, Color.White, scale);
    }

    private void DrawLastToDieRankingsScrollbar(
        LastToDieMenuLayout layout,
        Rectangle listBounds,
        int visibleRows,
        int totalRows)
    {
        if (totalRows <= visibleRows)
        {
            return;
        }

        var track = new Rectangle(listBounds.Right - 6, listBounds.Y + 24, 4, Math.Max(1, listBounds.Height - 26));
        _spriteBatch.Draw(_pixel, track, Color.Black * 0.42f);
        var thumbHeight = Math.Max(12, (int)MathF.Round(track.Height * (visibleRows / (float)totalRows)));
        var maximumOffset = Math.Max(1, totalRows - visibleRows);
        var thumbTravel = Math.Max(0, track.Height - thumbHeight);
        var thumbY = track.Y + (int)MathF.Round(thumbTravel * (_lastToDieRankingsScrollOffset / (float)maximumOffset));
        _spriteBatch.Draw(_pixel, new Rectangle(track.X, thumbY, track.Width, thumbHeight), new Color(235, 211, 150));
    }

    private static string FormatLastToDieScore(int scoreUnits)
    {
        var normalized = Math.Max(0, scoreUnits);
        var whole = normalized / 100;
        var fraction = normalized % 100;
        return fraction switch
        {
            0 => whole.ToString(),
            _ when fraction % 10 == 0 => $"{whole}.{fraction / 10}",
            _ => $"{whole}.{fraction:00}",
        };
    }

    private void DrawLastToDieDifficultyPage(LastToDieMenuLayout layout)
    {
        if (layout.ContentBounds == Rectangle.Empty || _lastToDieMenuHoverIndex != 1)
        {
            return;
        }

        var scale = 0.58f * layout.Scale;
        var prefix = "You and your opponents' max HP is ";
        var emphasis = "reduced to 25";
        var suffix = ".";
        var totalWidth =
            MeasureMenuBitmapFontWidth(prefix, scale)
            + MeasureMenuBitmapFontWidth(emphasis, scale)
            + MeasureMenuBitmapFontWidth(suffix, scale);
        var startX = layout.ContentBounds.X + ((layout.ContentBounds.Width - totalWidth) * 0.5f);
        var y = layout.ButtonBounds.Length > 0
            ? layout.ButtonBounds[^1].Bottom + (22f * layout.Scale)
            : layout.ContentBounds.Y + (76f * layout.Scale);
        var position = new Vector2(startX, y);
        DrawShadowedMenuBitmapFontText(prefix, position, Color.White, scale);
        position.X += MeasureMenuBitmapFontWidth(prefix, scale);
        DrawShadowedMenuBitmapFontText(emphasis, position, new Color(235, 58, 58), scale);
        position.X += MeasureMenuBitmapFontWidth(emphasis, scale);
        DrawShadowedMenuBitmapFontText(suffix, position, Color.White, scale);
    }

    private static string FormatLastToDieMenuDuration(int ticks)
    {
        if (ticks <= 0)
        {
            return "0:00";
        }

        var totalSeconds = Math.Max(0, (int)MathF.Round(ticks / (float)SimulationConfig.DefaultTicksPerSecond));
        var minutes = totalSeconds / 60;
        var seconds = totalSeconds % 60;
        return $"{minutes}:{seconds:00}";
    }

    private void DrawLastToDieMenuButton(LoadedSpriteFrame? texture, Rectangle bounds, string label, bool hovered, float plaqueScale)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        if (texture is not null)
        {
            DrawLoadedSpriteFrame(texture, bounds, hovered ? new Color(224, 224, 224) : Color.White);
        }
        else
        {
            _spriteBatch.Draw(_pixel, bounds, hovered ? new Color(178, 178, 178) : new Color(118, 118, 118));
        }

        DrawCenteredShadowedMenuBitmapFontText(label, bounds, Color.White, plaqueScale, 1f);
    }

    private void DrawCenteredShadowedMenuBitmapFontText(string text, Rectangle bounds, Color color, float plaqueScale, float textScaleMultiplier)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var textScale = GetMenuFontScaleToFit(text, bounds.Width - MathF.Max(14f, 26f * plaqueScale), bounds.Height - MathF.Max(8f, 12f * plaqueScale)) * textScaleMultiplier;
        var maxTextScale = GetMenuFontScaleToFit(text, bounds.Width - 2f, bounds.Height - 2f);
        textScale = MathF.Min(textScale, maxTextScale);
        var measuredWidth = MeasureMenuBitmapFontWidth(text, textScale);
        var lineHeight = MeasureMenuBitmapFontHeight(textScale);
        var position = new Vector2(
            bounds.X + ((bounds.Width - measuredWidth) * 0.5f),
            bounds.Y + ((bounds.Height - lineHeight) * 0.5f));
        DrawShadowedMenuBitmapFontText(text, position, color, textScale);
    }

    private void DrawShadowedMenuBitmapFontText(string text, Vector2 position, Color color, float scale)
    {
        DrawMenuBitmapFontText(text, position + new Vector2(2f, 2f), Color.Black * 0.82f, scale);
        DrawMenuBitmapFontText(text, position, color, scale);
    }

    private void DrawLastToDieMenuLogo(int viewportWidth)
    {
        EnsureLastToDieLogoTexture();
        if (_lastToDieLogoTexture is null)
        {
            return;
        }

        const float targetWidth = 420f;
        var scale = targetWidth / Math.Max(1f, _lastToDieLogoTexture.Width);
        var targetHeight = _lastToDieLogoTexture.Height * scale;
        var destination = new Rectangle(
            (int)MathF.Round(viewportWidth - targetWidth - 36f),
            32,
            (int)MathF.Round(targetWidth),
            (int)MathF.Round(targetHeight));
        DrawLoadedSpriteFrame(_lastToDieLogoTexture, destination, Color.White);
    }

    private void EnsureLastToDieLogoTexture()
    {
        var path = ContentRoot.GetPath("Sprites", "Menu", "LastToDie", "logo.png");
        if (string.IsNullOrWhiteSpace(path) || !CanLoadSpriteFrameFromPath(path))
        {
            DisposeLastToDieLogoTexture();
            return;
        }

        if (_lastToDieLogoTexture is not null
            && string.Equals(_lastToDieLogoTexturePath, path, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        DisposeLastToDieLogoTexture();
        _lastToDieLogoTexture = LoadSpriteFrameFromPath(path);
        _lastToDieLogoTexturePath = path;
    }

    private void DisposeLastToDieLogoTexture()
    {
        _lastToDieLogoTexture?.Dispose();
        _lastToDieLogoTexture = null;
        _lastToDieLogoTexturePath = null;
    }
}
