#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Linq;
using OpenGarrison.ClientShared;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private enum FriendsMenuTab
    {
        Friends,
        Requests,
        Messages,
    }

    private enum FriendsContextMenuAction
    {
        Request,
        Message,
        Join,
        Remove,
        Accept,
        Deny,
    }

    private readonly record struct FriendsMenuLayout(
        Rectangle Panel,
        Rectangle[] TabBounds,
        Rectangle RefreshBounds,
        Rectangle PlayerCardButtonBounds,
        Rectangle NicknameBounds,
        Rectangle OwnCodeBounds,
        Rectangle CopyCodeBounds,
        Rectangle FriendCodeBounds,
        Rectangle ListBounds,
        Rectangle MessageAreaBounds,
        Rectangle MessageInputBounds,
        Rectangle[] RowBounds,
        bool CompactLayout);

    private readonly record struct FriendsContextMenuLayout(
        Rectangle Bounds,
        Rectangle[] ItemBounds,
        FriendsContextMenuAction[] Actions,
        string[] Labels);

    private void UpdateFriendsMenu(KeyboardState keyboard, MouseState mouse)
    {
        CompleteSocialPresenceTasks();
        var layout = GetFriendsMenuLayout();

        if (keyboard.IsKeyDown(Keys.Escape) && !_previousKeyboard.IsKeyDown(Keys.Escape))
        {
            _mainMenuOverlayStateController.CloseFriendsMenu(clearStatus: false);
            return;
        }

        if (keyboard.IsKeyDown(Keys.Enter)
            && !_previousKeyboard.IsKeyDown(Keys.Enter)
            && !_editingFriendNickname
            && !_editingFriendCode
            && !_editingFriendMessage
            && _friendsMenuTab == FriendsMenuTab.Friends)
        {
            TryJoinSelectedFriend();
            return;
        }

        _friendsMenuHoverIndex = GetFriendsMenuRowIndexAtPoint(mouse.Position, layout);

        var leftClickPressed = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton != ButtonState.Pressed;
        var rightClickPressed = mouse.RightButton == ButtonState.Pressed && _previousMouse.RightButton != ButtonState.Pressed;
        if (rightClickPressed)
        {
            UpdateFriendsMenuRightClick(mouse.Position, layout);
            return;
        }

        if (TryUpdatePlayerCardOverlay(mouse, layout, leftClickPressed))
        {
            return;
        }

        if (!leftClickPressed)
        {
            return;
        }

        var point = mouse.Position;
        if (!layout.Panel.Contains(point))
        {
            _mainMenuOverlayStateController.CloseFriendsMenu(clearStatus: false);
            return;
        }

        if (TryHandleFriendsContextMenuClick(point, layout))
        {
            return;
        }

        for (var index = 0; index < layout.TabBounds.Length; index += 1)
        {
            if (!layout.TabBounds[index].Contains(point))
            {
                continue;
            }

            _friendsMenuTab = (FriendsMenuTab)index;
            _friendsMenuSelectedIndex = -1;
            _friendsMenuHoverIndex = -1;
            CloseFriendsContextMenu();
            ClosePlayerCardOverlay();
            _editingFriendNickname = false;
            _editingFriendCode = false;
            _editingFriendMessage = false;
            ResetTextFieldClickTarget();
            if (_friendsMenuTab == FriendsMenuTab.Requests)
            {
                RefreshFriendRequests();
            }
            else if (_friendsMenuTab == FriendsMenuTab.Messages)
            {
                SelectDefaultFriendMessageTarget();
                PollDirectMessages();
            }

            return;
        }

        if (layout.RefreshBounds.Contains(point))
        {
            CloseFriendsContextMenu();
            ClosePlayerCardOverlay();
            RefreshFriendPresence();
            RefreshFriendRequests();
            PollDirectMessages();
            _menuStatusMessage = "Refreshing...";
            return;
        }

        if (layout.PlayerCardButtonBounds.Contains(point))
        {
            CloseFriendsContextMenu();
            _playerCardOwnOpen = !_playerCardOwnOpen;
            _playerCardEditorOpen = false;
            _playerCardDraggingPortrait = false;
            _playerCardDraggingColorWheel = false;
            _editingFriendNickname = false;
            _editingFriendCode = false;
            _editingFriendMessage = false;
            ResetTextFieldClickTarget();
            return;
        }

        if (layout.NicknameBounds.Contains(point))
        {
            CloseFriendsContextMenu();
            _editingFriendNickname = true;
            _editingFriendCode = false;
            _editingFriendMessage = false;
            if (IsTextFieldDoubleClick(TextFieldClickTarget.FriendsNickname))
            {
                SelectAllTextInActiveField(TextFieldClickTarget.FriendsNickname);
            }

            return;
        }

        switch (_friendsMenuTab)
        {
            case FriendsMenuTab.Friends:
                UpdateFriendsTabClick(point, layout);
                break;
            case FriendsMenuTab.Requests:
                UpdateFriendRequestsTabClick(point, layout);
                break;
            case FriendsMenuTab.Messages:
                UpdateFriendMessagesTabClick(point, layout);
                break;
        }
    }

    private int GetFriendsMenuRowIndexAtPoint(Point point, FriendsMenuLayout layout)
    {
        var visibleCount = Math.Min(layout.RowBounds.Length, GetVisibleFriendsMenuRowCount());
        for (var index = 0; index < visibleCount; index += 1)
        {
            if (layout.RowBounds[index].Contains(point))
            {
                return index;
            }
        }

        return -1;
    }

    private void UpdateFriendsMenuRightClick(Point point, FriendsMenuLayout layout)
    {
        if (!layout.Panel.Contains(point))
        {
            CloseFriendsContextMenu();
            return;
        }

        var rowIndex = GetFriendsMenuRowIndexAtPoint(point, layout);
        if (_friendsMenuTab == FriendsMenuTab.Friends || _friendsMenuTab == FriendsMenuTab.Messages)
        {
            if (rowIndex >= 0 && rowIndex < _friendList.Friends.Count)
            {
                _friendsMenuSelectedIndex = rowIndex;
            }

            var canOpenBlankMenu = _friendsMenuTab == FriendsMenuTab.Friends
                && (layout.ListBounds.Contains(point) || layout.FriendCodeBounds.Contains(point));
            if (rowIndex >= 0 || canOpenBlankMenu)
            {
                OpenFriendsContextMenu(point);
                return;
            }
        }
        else if (_friendsMenuTab == FriendsMenuTab.Requests
            && rowIndex >= 0
            && rowIndex < _friendRequestEntries.Count)
        {
            _friendsMenuSelectedIndex = rowIndex;
            OpenFriendsContextMenu(point);
            return;
        }

        CloseFriendsContextMenu();
    }

    private void OpenFriendsContextMenu(Point point)
    {
        _friendsContextMenuOpen = true;
        _friendsContextMenuTargetIndex = _friendsMenuSelectedIndex;
        _friendsContextMenuX = point.X;
        _friendsContextMenuY = point.Y;
        _editingFriendNickname = false;
        _editingFriendCode = false;
        _editingFriendMessage = false;
        ResetTextFieldClickTarget();
    }

    private void CloseFriendsContextMenu()
    {
        _friendsContextMenuOpen = false;
        _friendsContextMenuTargetIndex = -1;
    }

    private bool TryHandleFriendsContextMenuClick(Point point, FriendsMenuLayout layout)
    {
        if (!_friendsContextMenuOpen)
        {
            return false;
        }

        var contextLayout = GetFriendsContextMenuLayout(layout);
        for (var index = 0; index < contextLayout.ItemBounds.Length; index += 1)
        {
            if (!contextLayout.ItemBounds[index].Contains(point))
            {
                continue;
            }

            InvokeFriendsContextAction(contextLayout.Actions[index]);
            CloseFriendsContextMenu();
            return true;
        }

        CloseFriendsContextMenu();
        return true;
    }

    private void InvokeFriendsContextAction(FriendsContextMenuAction action)
    {
        if (_friendsContextMenuTargetIndex >= 0)
        {
            _friendsMenuSelectedIndex = _friendsContextMenuTargetIndex;
        }

        switch (action)
        {
            case FriendsContextMenuAction.Request:
                TrySendFriendRequestFromInput();
                break;
            case FriendsContextMenuAction.Message:
                TrySelectSelectedFriendForDirectMessage();
                break;
            case FriendsContextMenuAction.Join:
                TryJoinSelectedFriend();
                break;
            case FriendsContextMenuAction.Remove:
                TryRemoveSelectedFriend();
                break;
            case FriendsContextMenuAction.Accept:
                TryRespondToSelectedFriendRequest(accept: true);
                break;
            case FriendsContextMenuAction.Deny:
                TryRespondToSelectedFriendRequest(accept: false);
                break;
        }
    }

    private void UpdateFriendsTabClick(Point point, FriendsMenuLayout layout)
    {
        _editingFriendMessage = false;
        if (layout.OwnCodeBounds.Contains(point) || layout.CopyCodeBounds.Contains(point))
        {
            CopyOwnFriendCodeToClipboard();
            return;
        }

        if (layout.FriendCodeBounds.Contains(point))
        {
            _editingFriendNickname = false;
            _editingFriendCode = true;
            _editingFriendMessage = false;
            if (IsTextFieldDoubleClick(TextFieldClickTarget.FriendsCode))
            {
                SelectAllTextInActiveField(TextFieldClickTarget.FriendsCode);
            }

            return;
        }

        ResetTextFieldClickTarget();
        _editingFriendNickname = false;
        _editingFriendCode = false;
        _editingFriendMessage = false;
        if (_friendsMenuHoverIndex >= 0 && _friendsMenuHoverIndex < _friendList.Friends.Count)
        {
            _friendsMenuSelectedIndex = _friendsMenuHoverIndex;
        }
    }

    private void UpdateFriendRequestsTabClick(Point point, FriendsMenuLayout layout)
    {
        ResetTextFieldClickTarget();
        _editingFriendNickname = false;
        _editingFriendCode = false;
        _editingFriendMessage = false;
        if (_friendsMenuHoverIndex >= 0 && _friendsMenuHoverIndex < _friendRequestEntries.Count)
        {
            _friendsMenuSelectedIndex = _friendsMenuHoverIndex;
        }
    }

    private void UpdateFriendMessagesTabClick(Point point, FriendsMenuLayout layout)
    {
        _editingFriendNickname = false;
        _editingFriendCode = false;
        if (layout.MessageInputBounds.Contains(point))
        {
            SelectDefaultFriendMessageTarget();
            _editingFriendMessage = true;
            if (IsTextFieldDoubleClick(TextFieldClickTarget.FriendsMessage))
            {
                SelectAllTextInActiveField(TextFieldClickTarget.FriendsMessage);
            }

            return;
        }

        ResetTextFieldClickTarget();
        if (_friendsMenuHoverIndex >= 0 && _friendsMenuHoverIndex < _friendList.Friends.Count)
        {
            _friendsMenuSelectedIndex = _friendsMenuHoverIndex;
            _lastDirectMessageSenderFriendCode = _friendList.Friends[_friendsMenuSelectedIndex].FriendCode;
            _editingFriendMessage = true;
            InitializeFriendMessageCursor();
            return;
        }

        _editingFriendMessage = false;
    }

    private void DrawFriendsMenu()
    {
        var layout = GetFriendsMenuLayout();
        var panel = layout.Panel;

        DrawRoundedRectangle(new Rectangle(panel.X + 6, panel.Y + 6, panel.Width, panel.Height), Color.Black * 0.36f, 8);
        DrawRoundedRectangleOutline(panel, new Color(59, 51, 46), new Color(213, 205, 188), outlineThickness: 2, radius: 8);
        DrawBitmapFontText("Nickname", new Vector2(layout.NicknameBounds.X, layout.NicknameBounds.Y - 16f), Color.White, 1f);
        DrawMenuInputBoxScaled(
            layout.NicknameBounds,
            _friendNicknameInputBuffer,
            _editingFriendNickname,
            1f,
            _friendNicknameCursorIndex,
            _friendNicknameSelectionStart);
        DrawMenuButtonScaled(layout.PlayerCardButtonBounds, "Card", _playerCardOwnOpen, 1f);
        DrawMenuButtonScaled(layout.RefreshBounds, "Refresh", false, 1f);
        DrawFriendsTabs(layout);

        switch (_friendsMenuTab)
        {
            case FriendsMenuTab.Friends:
                DrawFriendsTab(layout);
                break;
            case FriendsMenuTab.Requests:
                DrawFriendRequestsTab(layout);
                break;
            case FriendsMenuTab.Messages:
                DrawFriendMessagesTab(layout);
                break;
        }

        if (!string.IsNullOrWhiteSpace(_menuStatusMessage))
        {
            var statusBounds = _friendsMenuTab == FriendsMenuTab.Messages && layout.MessageAreaBounds.Width > 0
                ? new Rectangle(layout.MessageAreaBounds.X + 4, layout.MessageAreaBounds.Bottom - 22, layout.MessageAreaBounds.Width - 8, 18)
                : new Rectangle(panel.X + 18, panel.Bottom - 34, panel.Width - 36, 20);
            _spriteBatch.Draw(_pixel, statusBounds, Color.Black * 0.58f);
            DrawBitmapFontText(
                TrimBitmapMenuText(_menuStatusMessage, statusBounds.Width - 8f, 1f),
                new Vector2(statusBounds.X + 4f, statusBounds.Y + 2f),
                new Color(230, 220, 180),
                1f);
        }

        if (_friendsContextMenuOpen)
        {
            DrawFriendsContextMenu(layout);
        }

        DrawPlayerCardOverlay(layout);
    }

    private void DrawFriendsTabs(FriendsMenuLayout layout)
    {
        string[] labels = ["Friends", "Requests", "Messages"];
        for (var index = 0; index < layout.TabBounds.Length; index += 1)
        {
            DrawMenuButtonScaled(layout.TabBounds[index], labels[index], index == (int)_friendsMenuTab, 1f);
        }
    }

    private void DrawFriendsTab(FriendsMenuLayout layout)
    {
        DrawBitmapFontText("Your Friend Code", new Vector2(layout.OwnCodeBounds.X, layout.OwnCodeBounds.Y - 16f), Color.White, 1f);
        DrawMenuInputBoxScaled(layout.OwnCodeBounds, _clientIdentity.FriendCode, active: false, 1f);
        DrawMenuButtonScaled(layout.CopyCodeBounds, "Copy", false, 1f);

        DrawBitmapFontText("Add Friend Code", new Vector2(layout.FriendCodeBounds.X, layout.FriendCodeBounds.Y - 16f), Color.White, 1f);
        DrawMenuInputBoxScaled(
            layout.FriendCodeBounds,
            _friendCodeInputBuffer,
            _editingFriendCode,
            1f,
            _friendCodeCursorIndex,
            _friendCodeSelectionStart);

        DrawRoundedRectangleOutline(layout.ListBounds, new Color(59, 51, 46), new Color(213, 205, 188), outlineThickness: 1, radius: 6);
        DrawBitmapFontText("NAME", new Vector2(layout.ListBounds.X + 10f, layout.ListBounds.Y - 20f), Color.White, 1f);
        DrawBitmapFontText("STATUS", new Vector2(layout.ListBounds.X + layout.ListBounds.Width * 0.52f, layout.ListBounds.Y - 20f), Color.White, 1f);

        if (_friendList.Friends.Count == 0)
        {
            DrawBitmapFontText("No friends added.", new Vector2(layout.ListBounds.X + 10f, layout.ListBounds.Y + 12f), new Color(210, 210, 210), 1f);
        }

        for (var index = 0; index < layout.RowBounds.Length && index < _friendList.Friends.Count; index += 1)
        {
            DrawFriendRow(index, layout.RowBounds[index], layout.ListBounds, layout.CompactLayout);
        }
    }

    private void DrawFriendRequestsTab(FriendsMenuLayout layout)
    {
        DrawRoundedRectangleOutline(layout.ListBounds, new Color(59, 51, 46), new Color(213, 205, 188), outlineThickness: 1, radius: 6);
        DrawBitmapFontText("REQUEST", new Vector2(layout.ListBounds.X + 10f, layout.ListBounds.Y - 20f), Color.White, 1f);
        DrawBitmapFontText("STATUS", new Vector2(layout.ListBounds.X + layout.ListBounds.Width * 0.62f, layout.ListBounds.Y - 20f), Color.White, 1f);

        if (_friendRequestEntries.Count == 0)
        {
            DrawBitmapFontText("No requests.", new Vector2(layout.ListBounds.X + 10f, layout.ListBounds.Y + 12f), new Color(210, 210, 210), 1f);
        }

        for (var index = 0; index < layout.RowBounds.Length && index < _friendRequestEntries.Count; index += 1)
        {
            DrawFriendRequestRow(index, layout.RowBounds[index], layout.ListBounds);
        }
    }

    private void DrawFriendMessagesTab(FriendsMenuLayout layout)
    {
        DrawRoundedRectangleOutline(layout.ListBounds, new Color(59, 51, 46), new Color(213, 205, 188), outlineThickness: 1, radius: 6);
        DrawBitmapFontText("NAME", new Vector2(layout.ListBounds.X + 10f, layout.ListBounds.Y - 20f), Color.White, 1f);
        DrawBitmapFontText("STATUS", new Vector2(layout.ListBounds.X + layout.ListBounds.Width * 0.52f, layout.ListBounds.Y - 20f), Color.White, 1f);

        if (_friendList.Friends.Count == 0)
        {
            DrawBitmapFontText("No friends added.", new Vector2(layout.ListBounds.X + 10f, layout.ListBounds.Y + 12f), new Color(210, 210, 210), 1f);
        }

        for (var index = 0; index < layout.RowBounds.Length && index < _friendList.Friends.Count; index += 1)
        {
            DrawFriendRow(index, layout.RowBounds[index], layout.ListBounds, layout.CompactLayout);
        }

        DrawBitmapFontText("MESSAGES", new Vector2(layout.MessageAreaBounds.X + 10f, layout.MessageAreaBounds.Y - 17f), Color.White, 1f);
        _spriteBatch.Draw(_pixel, layout.MessageAreaBounds, Color.Black * 0.65f);
        DrawRectangleBorder(layout.MessageAreaBounds, new Color(213, 205, 188) * 0.76f, 1);
        DrawSelectedFriendMessages(layout.MessageAreaBounds);

        DrawMenuInputBoxScaled(
            layout.MessageInputBounds,
            _friendMessageInputBuffer,
            _editingFriendMessage,
            1f,
            _friendMessageCursorIndex,
            _friendMessageSelectionStart);
    }

    private void DrawFriendRow(int index, Rectangle bounds, Rectangle listBounds, bool compactLayout)
    {
        var friend = _friendList.Friends[index];
        _friendPresenceByCode.TryGetValue(friend.FriendCode, out var presence);
        var highlighted = index == _friendsMenuSelectedIndex;
        var hovered = index == _friendsMenuHoverIndex;
        var background = highlighted
            ? new Color(95, 72, 68)
            : hovered
                ? new Color(75, 67, 62)
                : new Color(54, 47, 41);
        _spriteBatch.Draw(_pixel, bounds, background);

        var statusColumnX = listBounds.X + listBounds.Width * 0.52f;
        var nameWidth = statusColumnX - listBounds.X - 18f;
        var statusWidth = listBounds.Right - statusColumnX - 10f;
        var displayName = GetFriendDisplayName(friend, presence);
        var status = GetFriendStatusLabel(presence);
        var nameColor = presence?.Online == true ? new Color(140, 235, 130) : Color.White;
        var statusColor = presence?.Online == true ? Color.White : new Color(190, 190, 190);
        var textY = bounds.Y + (compactLayout ? 5f : 6f);

        DrawBitmapFontText(TrimBitmapMenuText(displayName, nameWidth, 1f), new Vector2(listBounds.X + 10f, textY), nameColor, 1f);
        DrawBitmapFontText(TrimBitmapMenuText(status, statusWidth, 1f), new Vector2(statusColumnX, textY), statusColor, 1f);
    }

    private void DrawFriendRequestRow(int index, Rectangle bounds, Rectangle listBounds)
    {
        var request = _friendRequestEntries[index];
        var highlighted = index == _friendsMenuSelectedIndex;
        var hovered = index == _friendsMenuHoverIndex;
        var background = highlighted
            ? new Color(95, 72, 68)
            : hovered
                ? new Color(75, 67, 62)
                : new Color(54, 47, 41);
        _spriteBatch.Draw(_pixel, bounds, background);

        var statusColumnX = listBounds.X + listBounds.Width * 0.62f;
        var name = string.IsNullOrWhiteSpace(request.DisplayName) ? request.FriendCode : request.DisplayName.Trim();
        var direction = string.Equals(request.Direction, "incoming", StringComparison.OrdinalIgnoreCase) ? "From" : "To";
        var label = $"{direction} {name}";
        var status = string.Equals(request.Direction, "incoming", StringComparison.OrdinalIgnoreCase)
            ? "Pending"
            : request.Status;
        DrawBitmapFontText(TrimBitmapMenuText(label, statusColumnX - listBounds.X - 18f, 1f), new Vector2(listBounds.X + 10f, bounds.Y + 5f), Color.White, 1f);
        DrawBitmapFontText(TrimBitmapMenuText(status, listBounds.Right - statusColumnX - 10f, 1f), new Vector2(statusColumnX, bounds.Y + 5f), new Color(210, 210, 210), 1f);
    }

    private void DrawRectangleBorder(Rectangle bounds, Color color, int thickness)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0 || thickness <= 0)
        {
            return;
        }

        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, thickness), color);
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Bottom - thickness, bounds.Width, thickness), color);
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, thickness, bounds.Height), color);
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.Right - thickness, bounds.Y, thickness, bounds.Height), color);
    }

    private void DrawFriendMessageRow(FriendDirectMessageEntry message, Rectangle bounds, Rectangle listBounds)
    {
        var outgoing = string.Equals(message.Direction, "outgoing", StringComparison.OrdinalIgnoreCase);
        var friendName = outgoing
            ? GetFriendDisplayName(message.FriendCode)
            : GetFriendDisplayName(message.FriendCode, message.DisplayName);
        var label = outgoing
            ? $"To {friendName}"
            : friendName;
        var prefix = $"[{label}]: ";
        var prefixWidth = MeasureBitmapFontWidth(prefix, 1f);
        var textY = bounds.Y + 5f;
        var textMaxWidth = listBounds.Width - 20f - prefixWidth;
        var color = new Color(138, 218, 255);
        DrawBitmapFontText(TrimBitmapMenuText(prefix, listBounds.Width - 20f, 1f), new Vector2(listBounds.X + 10f, textY), color, 1f);
        DrawBitmapFontText(TrimBitmapMenuText(message.Text, textMaxWidth, 1f), new Vector2(listBounds.X + 10f + prefixWidth, textY), color, 1f);
    }

    private void DrawSelectedFriendMessages(Rectangle bounds)
    {
        if (!TryGetSelectedFriend(out var friend))
        {
            DrawBitmapFontText("Select a friend.", new Vector2(bounds.X + 10f, bounds.Y + 10f), new Color(210, 210, 210), 1f);
            return;
        }

        var messages = _friendMessageEntries
            .Where(message => string.Equals(message.FriendCode, friend.FriendCode, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (messages.Count == 0)
        {
            DrawBitmapFontText("No messages.", new Vector2(bounds.X + 10f, bounds.Y + 10f), new Color(210, 210, 210), 1f);
            return;
        }

        var lineHeight = MeasureBitmapFontHeight(1f) + 4f;
        var maxLines = Math.Max(1, (int)MathF.Floor((bounds.Height - 16f) / lineHeight));
        var start = Math.Max(0, messages.Count - maxLines);
        var textY = bounds.Y + 8f;
        for (var index = start; index < messages.Count; index += 1)
        {
            var message = messages[index];
            var outgoing = string.Equals(message.Direction, "outgoing", StringComparison.OrdinalIgnoreCase);
            var speaker = outgoing ? "You" : GetFriendDisplayName(friend, _friendPresenceByCode.TryGetValue(friend.FriendCode, out var presence) ? presence : null);
            var prefix = $"[{speaker}]: ";
            var prefixWidth = MeasureBitmapFontWidth(prefix, 1f);
            var textMaxWidth = Math.Max(28f, bounds.Width - 20f - prefixWidth);
            var textX = bounds.X + 10f;
            var color = new Color(138, 218, 255);
            DrawBitmapFontText(TrimBitmapMenuText(prefix, bounds.Width - 20f, 1f), new Vector2(textX, textY), color, 1f);
            DrawBitmapFontText(TrimBitmapMenuText(message.Text, textMaxWidth, 1f), new Vector2(textX + prefixWidth, textY), color, 1f);
            textY += lineHeight;
        }
    }

    private void DrawFriendsContextMenu(FriendsMenuLayout layout)
    {
        var contextLayout = GetFriendsContextMenuLayout(layout);
        DrawRoundedRectangle(new Rectangle(contextLayout.Bounds.X + 4, contextLayout.Bounds.Y + 4, contextLayout.Bounds.Width, contextLayout.Bounds.Height), Color.Black * 0.36f, 8);
        DrawRoundedRectangleOutline(contextLayout.Bounds, new Color(59, 51, 46), new Color(213, 205, 188), outlineThickness: 2, radius: 8);

        for (var index = 0; index < contextLayout.ItemBounds.Length; index += 1)
        {
            DrawMenuButtonScaled(contextLayout.ItemBounds[index], contextLayout.Labels[index], false, 1f);
        }
    }

    private FriendsContextMenuLayout GetFriendsContextMenuLayout(FriendsMenuLayout layout)
    {
        FriendsContextMenuAction[] actions;
        string[] labels;
        if (_friendsMenuTab == FriendsMenuTab.Requests)
        {
            actions =
            [
                FriendsContextMenuAction.Accept,
                FriendsContextMenuAction.Deny,
            ];
            labels = ["Accept", "Deny"];
        }
        else if (_friendsMenuTab == FriendsMenuTab.Messages)
        {
            actions =
            [
                FriendsContextMenuAction.Message,
                FriendsContextMenuAction.Join,
                FriendsContextMenuAction.Remove,
            ];
            labels = ["Message", "Join", "Remove"];
        }
        else
        {
            actions =
            [
                FriendsContextMenuAction.Request,
                FriendsContextMenuAction.Message,
                FriendsContextMenuAction.Join,
                FriendsContextMenuAction.Remove,
            ];
            labels = ["Request", "Message", "Join", "Remove"];
        }

        const int popupPadding = 8;
        const int buttonGap = 8;
        var columns = actions.Length > 2 ? 2 : actions.Length;
        var rows = (int)MathF.Ceiling(actions.Length / (float)columns);
        var buttonWidth = Math.Min(108, Math.Max(88, (layout.Panel.Width - (popupPadding * 2) - buttonGap) / Math.Max(1, columns)));
        var buttonHeight = layout.CompactLayout ? 32 : 34;
        var popupWidth = (popupPadding * 2) + (columns * buttonWidth) + ((columns - 1) * buttonGap);
        var popupHeight = (popupPadding * 2) + (rows * buttonHeight) + ((rows - 1) * buttonGap);
        var minX = layout.Panel.X + 8;
        var minY = layout.Panel.Y + 8;
        var maxX = Math.Max(minX, layout.Panel.Right - popupWidth - 8);
        var maxY = Math.Max(minY, layout.Panel.Bottom - popupHeight - 8);
        var x = Math.Clamp(_friendsContextMenuX, minX, maxX);
        var y = Math.Clamp(_friendsContextMenuY, minY, maxY);
        var bounds = new Rectangle(x, y, popupWidth, popupHeight);
        var itemBounds = new Rectangle[actions.Length];
        for (var index = 0; index < itemBounds.Length; index += 1)
        {
            var column = index % columns;
            var row = index / columns;
            itemBounds[index] = new Rectangle(
                bounds.X + popupPadding + (column * (buttonWidth + buttonGap)),
                bounds.Y + popupPadding + (row * (buttonHeight + buttonGap)),
                buttonWidth,
                buttonHeight);
        }

        return new FriendsContextMenuLayout(bounds, itemBounds, actions, labels);
    }

    private FriendsMenuLayout GetFriendsMenuLayout()
    {
        var rightMargin = Math.Max(18, (int)MathF.Round(ViewportWidth * 0.035f));
        var panelWidth = Math.Min(ViewportWidth - 32, ViewportWidth >= 1024 ? 330 : 310);
        if (ViewportWidth - panelWidth - rightMargin < 16)
        {
            rightMargin = 16;
        }

        var bottomReserved = _menuBackgroundMode != MenuBackgroundMode.Static ? 92 : 16;
        var availableHeight = Math.Max(300, ViewportHeight - bottomReserved - 24);
        var panelHeight = Math.Min(availableHeight, ViewportHeight < 540 ? 430 : 500);
        var usableBottom = ViewportHeight - bottomReserved;
        var panel = new Rectangle(
            ViewportWidth - panelWidth - rightMargin,
            Math.Max(12, ((usableBottom - panelHeight) / 2) + 6),
            panelWidth,
            panelHeight);

        var compactLayout = panel.Height < 470;
        var padding = compactLayout ? 16 : 18;
        var fieldHeight = 32;
        var smallButtonWidth = compactLayout ? 78 : 84;
        var refreshWidth = compactLayout ? 86 : 92;
        var playerCardButtonWidth = compactLayout ? 58 : 62;
        var inlineGap = 10;
        var nicknameY = panel.Y + (compactLayout ? 30 : 32);
        var nicknameWidth = panel.Width - (padding * 2) - refreshWidth - playerCardButtonWidth - (inlineGap * 2);
        var nicknameBounds = new Rectangle(panel.X + padding, nicknameY, nicknameWidth, fieldHeight);
        var playerCardButtonBounds = new Rectangle(nicknameBounds.Right + inlineGap, nicknameBounds.Y, playerCardButtonWidth, fieldHeight);
        var refreshBounds = new Rectangle(playerCardButtonBounds.Right + inlineGap, nicknameBounds.Y, refreshWidth, fieldHeight);

        var tabGap = 6;
        var tabY = nicknameBounds.Bottom + (compactLayout ? 14 : 16);
        var tabWidth = (panel.Width - (padding * 2) - (tabGap * 2)) / 3;
        var tabBounds = new Rectangle[3];
        for (var index = 0; index < tabBounds.Length; index += 1)
        {
            tabBounds[index] = new Rectangle(panel.X + padding + (index * (tabWidth + tabGap)), tabY, tabWidth, 30);
        }

        var inlineFieldWidth = panel.Width - (padding * 2) - smallButtonWidth - 8;
        var contentTop = tabBounds[0].Bottom + (compactLayout ? 34 : 38);
        var ownCodeBounds = new Rectangle(panel.X + padding, contentTop, inlineFieldWidth, fieldHeight);
        var copyCodeBounds = new Rectangle(ownCodeBounds.Right + 8, ownCodeBounds.Y, smallButtonWidth, fieldHeight);
        var friendCodeBounds = new Rectangle(panel.X + padding, ownCodeBounds.Bottom + 32, panel.Width - (padding * 2), fieldHeight);

        var listTop = _friendsMenuTab == FriendsMenuTab.Friends
            ? friendCodeBounds.Bottom + 32
            : tabBounds[0].Bottom + 36;
        var listBottom = panel.Bottom - padding;
        var messageInputBounds = Rectangle.Empty;
        var messageAreaBounds = Rectangle.Empty;
        Rectangle listBounds;
        if (_friendsMenuTab == FriendsMenuTab.Messages)
        {
            var messageInputHeight = 32;
            messageInputBounds = new Rectangle(panel.X + padding, panel.Bottom - padding - messageInputHeight, panel.Width - (padding * 2), messageInputHeight);
            var messageAreaBottom = messageInputBounds.Y - 5;
            var availableContentHeight = Math.Max(150, messageAreaBottom - listTop);
            var friendListHeight = Math.Clamp((int)MathF.Round(availableContentHeight * 0.34f), 58, compactLayout ? 96 : 118);
            listBounds = new Rectangle(panel.X + padding, listTop, panel.Width - (padding * 2), friendListHeight);
            messageAreaBounds = new Rectangle(
                listBounds.X,
                listBounds.Bottom + 20,
                listBounds.Width,
                Math.Max(72, messageAreaBottom - listBounds.Bottom - 20));
        }
        else
        {
            listBounds = new Rectangle(panel.X + padding, listTop, panel.Width - (padding * 2), Math.Max(90, listBottom - listTop));
        }

        var rowHeight = compactLayout ? 28 : 30;
        var visibleRowCount = _friendsMenuTab == FriendsMenuTab.Messages
            ? Math.Clamp(listBounds.Height / rowHeight, 1, 18)
            : Math.Clamp(listBounds.Height / rowHeight, 3, 18);
        var rowBounds = new Rectangle[visibleRowCount];
        for (var index = 0; index < rowBounds.Length; index += 1)
        {
            rowBounds[index] = new Rectangle(listBounds.X, listBounds.Y + (index * rowHeight), listBounds.Width, rowHeight - 2);
        }

        return new FriendsMenuLayout(
            panel,
            tabBounds,
            refreshBounds,
            playerCardButtonBounds,
            nicknameBounds,
            ownCodeBounds,
            copyCodeBounds,
            friendCodeBounds,
            listBounds,
            messageAreaBounds,
            messageInputBounds,
            rowBounds,
            compactLayout);
    }

    private int GetVisibleFriendsMenuRowCount()
    {
        return _friendsMenuTab switch
        {
            FriendsMenuTab.Requests => _friendRequestEntries.Count,
            FriendsMenuTab.Messages => _friendList.Friends.Count,
            _ => _friendList.Friends.Count,
        };
    }

    private void TrySendFriendRequestFromInput()
    {
        TrySendFriendRequest(_friendCodeInputBuffer, clearFriendCodeInput: true);
    }

    private bool TrySendFriendRequestToCode(string friendCode)
    {
        return TrySendFriendRequest(friendCode, clearFriendCodeInput: false);
    }

    private bool TrySendFriendRequest(string friendCodeText, bool clearFriendCodeInput)
    {
        if (_friendRequestSendTask is not null)
        {
            _menuStatusMessage = "Request already in progress.";
            return false;
        }

        if (!CanSendFriendRequestToCode(friendCodeText, out var friendCode, out var failureMessage))
        {
            _menuStatusMessage = failureMessage;
            return false;
        }

        _friendRequestSendTask = _presenceClient.SendFriendRequestAsync(_clientIdentity, friendCode);
        if (clearFriendCodeInput)
        {
            _friendCodeInputBuffer = string.Empty;
            InitializeFriendCodeCursor();
        }

        _menuStatusMessage = "Sending request...";
        return true;
    }

    private bool CanSendFriendRequestToCode(string friendCodeText, out string friendCode, out string failureMessage)
    {
        friendCode = string.Empty;
        failureMessage = string.Empty;
        if (!ClientIdentityDocument.TryNormalizeFriendCode(friendCodeText, out friendCode))
        {
            failureMessage = "Enter a valid friend code.";
            return false;
        }

        if (string.Equals(friendCode, _clientIdentity.FriendCode, StringComparison.OrdinalIgnoreCase))
        {
            failureMessage = "That is your friend code.";
            return false;
        }

        var normalizedFriendCode = friendCode;
        if (_friendList.Friends.Any(friend => string.Equals(friend.FriendCode, normalizedFriendCode, StringComparison.OrdinalIgnoreCase)))
        {
            failureMessage = "Friend is already listed.";
            return false;
        }

        if (_friendRequestEntries.Any(request =>
                string.Equals(request.FriendCode, normalizedFriendCode, StringComparison.OrdinalIgnoreCase)
                && (string.Equals(request.Status, "pending", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(request.Status, "accepted", StringComparison.OrdinalIgnoreCase))))
        {
            failureMessage = "Friend request already exists.";
            return false;
        }

        return true;
    }

    private bool TryRespondToSelectedFriendRequest(bool accept)
    {
        if (!CanRespondToSelectedFriendRequest() || _friendRequestRespondTask is not null)
        {
            _menuStatusMessage = _friendRequestRespondTask is not null
                ? "Request response already in progress."
                : "Choose an incoming request.";
            return false;
        }

        var request = _friendRequestEntries[_friendsMenuSelectedIndex];
        _friendRequestRespondTask = _presenceClient.RespondToFriendRequestAsync(_clientIdentity, request.RequestId, accept);
        _menuStatusMessage = accept ? "Accepting request..." : "Denying request...";
        return true;
    }

    private bool CanRespondToSelectedFriendRequest()
    {
        if (_friendsMenuSelectedIndex < 0 || _friendsMenuSelectedIndex >= _friendRequestEntries.Count)
        {
            return false;
        }

        var request = _friendRequestEntries[_friendsMenuSelectedIndex];
        return string.Equals(request.Direction, "incoming", StringComparison.OrdinalIgnoreCase)
            && string.Equals(request.Status, "pending", StringComparison.OrdinalIgnoreCase);
    }

    private string GetFriendNicknameInputDefault()
    {
        if (!string.IsNullOrWhiteSpace(_clientIdentity.DisplayName))
        {
            return _clientIdentity.DisplayName.Trim();
        }

        return NormalizeFriendNickname(_world.LocalPlayer.DisplayName);
    }

    private void SaveFriendNicknameFromInput()
    {
        var nickname = NormalizeFriendNickname(_friendNicknameInputBuffer);
        if (string.IsNullOrWhiteSpace(nickname))
        {
            _menuStatusMessage = "Enter a nickname.";
            _editingFriendNickname = true;
            _editingFriendCode = false;
            return;
        }

        _clientIdentity.DisplayName = nickname;
        _clientIdentity.Save();
        _friendNicknameInputBuffer = nickname;
        InitializeFriendNicknameCursor();
        _editingFriendNickname = false;
        _editingFriendCode = true;
        _lastSocialPresenceSignature = string.Empty;
        _socialPresenceSecondsUntilHeartbeat = 0d;
        _menuStatusMessage = "Nickname saved.";
    }

    private void CopyOwnFriendCodeToClipboard()
    {
        _menuStatusMessage = TrySetClipboardText(_clientIdentity.FriendCode)
            ? "Friend code copied."
            : $"Your friend code is {_clientIdentity.FriendCode}.";
    }

    private static string NormalizeFriendNickname(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var sanitized = new string(value.Where(character => !char.IsControl(character) && character != '#').ToArray()).Trim();
        return sanitized.Length > 20 ? sanitized[..20] : sanitized;
    }

    private bool TryRemoveSelectedFriend()
    {
        if (!CanRemoveSelectedFriend())
        {
            _menuStatusMessage = "Choose a friend to remove.";
            return false;
        }

        _friendList.RemoveAt(_friendsMenuSelectedIndex);
        _friendList.Save();
        _friendsMenuSelectedIndex = _friendList.Friends.Count == 0
            ? -1
            : Math.Clamp(_friendsMenuSelectedIndex, 0, _friendList.Friends.Count - 1);
        _menuStatusMessage = "Friend removed.";
        return true;
    }

    private bool TryGetSelectedFriend(out FriendListEntry friend)
    {
        friend = null!;
        if (_friendsMenuSelectedIndex < 0 || _friendsMenuSelectedIndex >= _friendList.Friends.Count)
        {
            return false;
        }

        friend = _friendList.Friends[_friendsMenuSelectedIndex];
        return true;
    }

    private void SelectDefaultFriendMessageTarget()
    {
        if (_friendsMenuSelectedIndex >= 0 && _friendsMenuSelectedIndex < _friendList.Friends.Count)
        {
            _lastDirectMessageSenderFriendCode = _friendList.Friends[_friendsMenuSelectedIndex].FriendCode;
            return;
        }

        if (_friendList.Friends.Count > 0)
        {
            _friendsMenuSelectedIndex = 0;
            _lastDirectMessageSenderFriendCode = _friendList.Friends[0].FriendCode;
        }
    }

    private bool TrySendSelectedFriendDirectMessageFromInput()
    {
        SelectDefaultFriendMessageTarget();
        if (!TryGetSelectedFriend(out var friend))
        {
            _menuStatusMessage = "Choose a friend to message.";
            return false;
        }

        var text = _friendMessageInputBuffer.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (!TrySendDirectMessage(friend.FriendCode, text, echoToChat: false))
        {
            return false;
        }

        _friendMessageInputBuffer = string.Empty;
        InitializeFriendMessageCursor();
        _lastDirectMessageSenderFriendCode = friend.FriendCode;
        _editingFriendMessage = true;
        return true;
    }

    private bool TrySelectSelectedFriendForDirectMessage()
    {
        if (_friendsMenuSelectedIndex < 0 || _friendsMenuSelectedIndex >= _friendList.Friends.Count)
        {
            _menuStatusMessage = "Choose a friend to message.";
            return false;
        }

        var friend = _friendList.Friends[_friendsMenuSelectedIndex];
        _lastDirectMessageSenderFriendCode = friend.FriendCode;
        _friendsMenuTab = FriendsMenuTab.Messages;
        _friendsMenuSelectedIndex = Math.Clamp(_friendsMenuSelectedIndex, 0, _friendList.Friends.Count - 1);
        _friendsMenuHoverIndex = -1;
        _editingFriendMessage = true;
        InitializeFriendMessageCursor();
        PollDirectMessages();
        _menuStatusMessage = string.Empty;
        return true;
    }

    private bool TryJoinSelectedFriend()
    {
        if (!TryGetSelectedFriendPresence(out var presence) || !CanJoinFriendPresence(presence))
        {
            _menuStatusMessage = "Friend is not on a joinable server.";
            return false;
        }

        var endpoint = new NetworkEndpoint(presence.Host, presence.UdpPort, presence.WebSocketPort, presence.WebSocketUrl);
        var connected = TryConnectToServer(endpoint, addConsoleFeedback: false);
        if (connected)
        {
            _mainMenuOverlayStateController.CloseFriendsMenu(clearStatus: false);
        }

        return connected;
    }

    private bool CanJoinSelectedFriend()
    {
        return TryGetSelectedFriendPresence(out var presence) && CanJoinFriendPresence(presence);
    }

    private bool CanRemoveSelectedFriend()
    {
        return _friendsMenuSelectedIndex >= 0 && _friendsMenuSelectedIndex < _friendList.Friends.Count;
    }

    private bool TryGetSelectedFriendPresence(out FriendPresenceEntry presence)
    {
        presence = null!;
        if (!TryGetSelectedFriend(out var friend))
        {
            return false;
        }

        return _friendPresenceByCode.TryGetValue(friend.FriendCode, out presence!);
    }

    private static bool CanJoinFriendPresence(FriendPresenceEntry presence)
    {
        return presence.Online
            && presence.Joinable
            && !string.IsNullOrWhiteSpace(presence.Host)
            && (presence.UdpPort > 0 || presence.WebSocketPort > 0 || !string.IsNullOrWhiteSpace(presence.WebSocketUrl));
    }

    private static string GetFriendDisplayName(FriendListEntry friend, FriendPresenceEntry? presence)
    {
        if (!string.IsNullOrWhiteSpace(presence?.DisplayName))
        {
            return presence.DisplayName.Trim();
        }

        return friend.DisplayLabel;
    }

    private static string GetFriendStatusLabel(FriendPresenceEntry? presence)
    {
        if (presence?.Online != true)
        {
            return "Offline";
        }

        return presence.Status switch
        {
            "server" => "Online",
            "jump" => "Jump",
            "last_to_die" => "Last to Die",
            "practice" => "Practice",
            _ => "Main Menu",
        };
    }
}
