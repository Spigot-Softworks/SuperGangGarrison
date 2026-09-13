#nullable enable

using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Protocol;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const int VoteMenuVisibleRows = 8;
    private static readonly Keys[] VoteMenuDigitKeys =
        [Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.D5, Keys.D6, Keys.D7, Keys.D8];
    private static readonly Keys[] VoteMenuNumpadKeys =
        [Keys.NumPad1, Keys.NumPad2, Keys.NumPad3, Keys.NumPad4, Keys.NumPad5, Keys.NumPad6, Keys.NumPad7, Keys.NumPad8];

    private enum VoteMenuPage
    {
        Main,
        MapNow,
        MapNextRound,
        Vip,
        Kick,
        Mute,
        Custom,
        CustomPlayer,
        CustomMap,
    }

    private readonly record struct VoteMenuAction(string Label, string Value, Action Activate);

    private VoteMenuMessage? _voteMenuCatalog;
    private bool _voteMenuOpen;
    private VoteMenuPage _voteMenuPage;
    private int _voteMenuSelectedIndex;
    private int _voteMenuScrollOffset;
    private string _voteMenuSelectedCustomId = string.Empty;

    private void HandleVoteMenuMessage(VoteMenuMessage menu)
    {
        if ((!_networkClient.IsConnected && !IsOfflinePracticeVote) || _networkClient.IsReplayConnection || _mainMenuOpen)
        {
            return;
        }

        _voteMenuCatalog = menu;
        _voteMenuPage = VoteMenuPage.Main;
        _voteMenuSelectedIndex = 0;
        _voteMenuScrollOffset = 0;
        _voteMenuSelectedCustomId = string.Empty;
        _voteMenuOpen = true;
        ResetChatInputState();
        CloseGameplaySelectionMenus();
    }

    private void CloseVoteMenu()
    {
        _voteMenuOpen = false;
        _voteMenuCatalog = null;
        _voteMenuPage = VoteMenuPage.Main;
        _voteMenuSelectedIndex = 0;
        _voteMenuScrollOffset = 0;
        _voteMenuSelectedCustomId = string.Empty;
    }

    private void UpdateVoteMenu(KeyboardState keyboard, MouseState mouse)
    {
        if (!_voteMenuOpen || (!_networkClient.IsConnected && !IsOfflinePracticeVote) || _networkClient.IsReplayConnection)
        {
            CloseVoteMenu();
            return;
        }

        if (IsKeyPressed(keyboard, Keys.Escape) || IsControllerMenuBackPressed())
        {
            if (_voteMenuPage == VoteMenuPage.Main)
            {
                CloseVoteMenu();
            }
            else
            {
                SetVoteMenuPage(VoteMenuPage.Main);
            }

            return;
        }

        var actions = BuildVoteMenuActions();
        if (actions.Count == 0)
        {
            CloseVoteMenu();
            return;
        }

        for (var visibleIndex = 0; visibleIndex < VoteMenuVisibleRows; visibleIndex += 1)
        {
            if (!IsKeyPressed(keyboard, VoteMenuDigitKeys[visibleIndex])
                && !IsKeyPressed(keyboard, VoteMenuNumpadKeys[visibleIndex]))
            {
                continue;
            }

            var actionIndex = _voteMenuScrollOffset + visibleIndex;
            if (actionIndex < actions.Count)
            {
                actions[actionIndex].Activate();
            }
            return;
        }

        if (TryConsumeControllerMenuNavigation(out _, out var verticalStep) && verticalStep != 0)
        {
            MoveVoteMenuSelection(verticalStep, actions.Count);
        }
        else if (IsKeyPressed(keyboard, Keys.Up))
        {
            MoveVoteMenuSelection(-1, actions.Count);
        }
        else if (IsKeyPressed(keyboard, Keys.Down))
        {
            MoveVoteMenuSelection(1, actions.Count);
        }

        GetVoteMenuLayout(actions.Count, out _, out var rowBounds);
        if (ShouldUseMouseMenuHover(mouse))
        {
            for (var visibleIndex = 0; visibleIndex < rowBounds.Length; visibleIndex += 1)
            {
                if (rowBounds[visibleIndex].Contains(mouse.Position))
                {
                    _voteMenuSelectedIndex = _voteMenuScrollOffset + visibleIndex;
                    break;
                }
            }
        }

        var wheelDelta = mouse.ScrollWheelValue - _previousMouse.ScrollWheelValue;
        if (wheelDelta != 0)
        {
            MoveVoteMenuSelection(wheelDelta > 0 ? -1 : 1, actions.Count);
        }

        var clickPressed = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton != ButtonState.Pressed;
        if ((IsKeyPressed(keyboard, Keys.Enter) || IsControllerMenuConfirmPressed() || clickPressed)
            && _voteMenuSelectedIndex >= 0
            && _voteMenuSelectedIndex < actions.Count)
        {
            actions[_voteMenuSelectedIndex].Activate();
        }
    }

    private void DrawVoteMenu()
    {
        if (!_voteMenuOpen)
        {
            return;
        }

        var actions = BuildVoteMenuActions();
        GetVoteMenuLayout(actions.Count, out var panel, out var rowBounds);
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, ViewportWidth, ViewportHeight), Color.Black * 0.68f);
        DrawRoundedRectangleOutline(panel, new Color(59, 51, 46), new Color(213, 205, 188), outlineThickness: 2, radius: 8);

        DrawBitmapFontText(GetVoteMenuTitle(), new Vector2(panel.X + 16f, panel.Y + 14f), Color.White, 1.15f);
        var hint = _voteMenuPage == VoteMenuPage.Main ? "Choose a vote" : "Esc: Back";
        var hintWidth = MeasureBitmapFontWidth(hint, 0.78f);
        DrawBitmapFontText(hint, new Vector2(panel.Right - hintWidth - 16f, panel.Y + 17f), new Color(205, 197, 180), 0.78f);

        for (var visibleIndex = 0; visibleIndex < rowBounds.Length; visibleIndex += 1)
        {
            var actionIndex = _voteMenuScrollOffset + visibleIndex;
            if (actionIndex >= actions.Count)
            {
                break;
            }

            var action = actions[actionIndex];
            var selected = actionIndex == _voteMenuSelectedIndex;
            DrawMenuButtonScaled(rowBounds[visibleIndex], $"{visibleIndex + 1}. {action.Label}", selected, 0.92f);
            if (!string.IsNullOrWhiteSpace(action.Value))
            {
                var value = TrimBitmapMenuText(action.Value, rowBounds[visibleIndex].Width * 0.33f, 0.76f);
                var valueWidth = MeasureBitmapFontWidth(value, 0.76f);
                DrawBitmapFontText(
                    value,
                    new Vector2(rowBounds[visibleIndex].Right - valueWidth - 12f, rowBounds[visibleIndex].Y + 13f),
                    new Color(230, 220, 180),
                    0.76f);
            }
        }

        if (actions.Count > VoteMenuVisibleRows)
        {
            var page = (_voteMenuScrollOffset / VoteMenuVisibleRows) + 1;
            var pages = (int)Math.Ceiling(actions.Count / (double)VoteMenuVisibleRows);
            DrawBitmapFontText($"{page}/{pages}", new Vector2(panel.Right - 54f, panel.Bottom - 24f), new Color(205, 197, 180), 0.72f);
        }
    }

    private List<VoteMenuAction> BuildVoteMenuActions()
    {
        var menu = _voteMenuCatalog;
        if (menu is null)
        {
            return [];
        }

        if (_voteMenuPage == VoteMenuPage.Main)
        {
            if (menu.VoteActive)
            {
                return
                [
                    new("Vote Yes", string.Empty, () => SubmitVoteMenuCommand(VoteCommandKind.CastYes)),
                    new("Vote No", string.Empty, () => SubmitVoteMenuCommand(VoteCommandKind.CastNo)),
                    new("Refresh Status", string.Empty, () => _networkClient.SendVoteCommand(VoteCommandKind.RequestStatus)),
                    new("Cancel My Vote", "Initiator only", () => SubmitVoteMenuCommand(VoteCommandKind.Cancel)),
                    new("Close", string.Empty, CloseVoteMenu),
                ];
            }

            var actions = new List<VoteMenuAction>
            {
                new("Change Map Now", string.Empty, () => SetVoteMenuPage(VoteMenuPage.MapNow)),
                new("Change Map Next Round", string.Empty, () => SetVoteMenuPage(VoteMenuPage.MapNextRound)),
            };
            if (menu.VipVoteAvailable)
            {
                actions.Add(new VoteMenuAction("Select VIP", string.Empty, () => SetVoteMenuPage(VoteMenuPage.Vip)));
            }

            if (menu.KickVoteAvailable)
            {
                actions.Add(new VoteMenuAction("Kick Player", string.Empty, () => SetVoteMenuPage(VoteMenuPage.Kick)));
            }

            if (menu.MuteVoteAvailable)
            {
                actions.Add(new VoteMenuAction("Mute Player", string.Empty, () => SetVoteMenuPage(VoteMenuPage.Mute)));
            }

            if (menu.ScrambleVoteAvailable)
            {
                actions.Add(new VoteMenuAction("Scramble Teams", string.Empty, () =>
                {
                    _networkClient.SendVoteCommand(VoteCommandKind.StartScramble);
                    CloseVoteMenu();
                }));
            }

            if (menu.CustomVotes.Count > 0)
            {
                actions.Add(new VoteMenuAction(
                    "Plugin Votes",
                    menu.CustomVotes.Count.ToString(CultureInfo.InvariantCulture),
                    () => SetVoteMenuPage(VoteMenuPage.Custom)));
            }

            if (menu.CooldownTicksRemaining > 0)
            {
                var seconds = (int)Math.Ceiling(menu.CooldownTicksRemaining / (double)Math.Max(1, _config.TicksPerSecond));
                actions.Insert(0, new VoteMenuAction("Vote Cooldown", $"{seconds}s", () => { }));
            }

            actions.Add(new VoteMenuAction("Close", string.Empty, CloseVoteMenu));
            return actions;
        }

        if (_voteMenuPage is VoteMenuPage.MapNow or VoteMenuPage.MapNextRound or VoteMenuPage.CustomMap)
        {
            var actions = new List<VoteMenuAction>();
            foreach (var map in menu.Maps)
            {
                var areaCount = Math.Clamp(map.AreaCount, 1, 32);
                for (var area = 1; area <= areaCount; area += 1)
                {
                    var capturedMap = map;
                    var capturedArea = area;
                    var label = areaCount > 1 ? $"{map.DisplayName} - Area {area}" : map.DisplayName;
                    actions.Add(new VoteMenuAction(label, string.Empty, () =>
                    {
                        if (_voteMenuPage == VoteMenuPage.CustomMap)
                        {
                            _networkClient.SendVoteCommand(
                                VoteCommandKind.StartCustom,
                                _voteMenuSelectedCustomId,
                                capturedArea,
                                argument: capturedMap.LevelName);
                        }
                        else
                        {
                            var command = _voteMenuPage == VoteMenuPage.MapNow
                                ? VoteCommandKind.StartMapNow
                                : VoteCommandKind.StartMapNextRound;
                            if (IsOfflinePracticeVote) SubmitOfflinePracticeMapVote(capturedMap.LevelName, capturedArea, command == VoteCommandKind.StartMapNextRound);
                            else _networkClient.SendVoteCommand(command, capturedMap.LevelName, capturedArea);
                        }
                        CloseVoteMenu();
                    }));
                }
            }

            actions.Add(new VoteMenuAction("Back", string.Empty, () =>
                SetVoteMenuPage(_voteMenuPage == VoteMenuPage.CustomMap ? VoteMenuPage.Custom : VoteMenuPage.Main)));
            return actions;
        }

        if (_voteMenuPage is VoteMenuPage.Kick or VoteMenuPage.Mute or VoteMenuPage.CustomPlayer)
        {
            var playerActions = new List<VoteMenuAction>();
            foreach (var player in menu.Players)
            {
                if ((_voteMenuPage is VoteMenuPage.Kick or VoteMenuPage.Mute
                        && player.Slot == _networkClient.LocalPlayerSlot)
                    || (_voteMenuPage == VoteMenuPage.Mute && player.IsMuted))
                {
                    continue;
                }

                var capturedPlayer = player;
                playerActions.Add(new VoteMenuAction(player.DisplayName, GetVoteTeamLabel(player.Team), () =>
                {
                    if (_voteMenuPage == VoteMenuPage.CustomPlayer)
                    {
                        _networkClient.SendVoteCommand(
                            VoteCommandKind.StartCustom,
                            _voteMenuSelectedCustomId,
                            targetSlot: capturedPlayer.Slot);
                    }
                    else
                    {
                        _networkClient.SendVoteCommand(
                            _voteMenuPage == VoteMenuPage.Kick
                                ? VoteCommandKind.StartKick
                                : VoteCommandKind.StartMute,
                            targetSlot: capturedPlayer.Slot);
                    }
                    CloseVoteMenu();
                }));
            }

            playerActions.Add(new VoteMenuAction("Back", string.Empty, () =>
                SetVoteMenuPage(_voteMenuPage == VoteMenuPage.CustomPlayer ? VoteMenuPage.Custom : VoteMenuPage.Main)));
            return playerActions;
        }

        if (_voteMenuPage == VoteMenuPage.Custom)
        {
            var customActions = new List<VoteMenuAction>();
            foreach (var customVote in menu.CustomVotes)
            {
                var capturedVote = customVote;
                customActions.Add(new VoteMenuAction(customVote.DisplayName, customVote.Description, () =>
                {
                    _voteMenuSelectedCustomId = capturedVote.Id;
                    switch ((VoteMenuTargetKind)capturedVote.TargetKind)
                    {
                        case VoteMenuTargetKind.Player:
                            SetVoteMenuPage(VoteMenuPage.CustomPlayer);
                            break;
                        case VoteMenuTargetKind.Map:
                            SetVoteMenuPage(VoteMenuPage.CustomMap);
                            break;
                        default:
                            _networkClient.SendVoteCommand(VoteCommandKind.StartCustom, capturedVote.Id);
                            CloseVoteMenu();
                            break;
                    }
                }));
            }

            customActions.Add(new VoteMenuAction("Back", string.Empty, () => SetVoteMenuPage(VoteMenuPage.Main)));
            return customActions;
        }

        var vipActions = new List<VoteMenuAction>();
        foreach (var player in menu.Players)
        {
            var capturedPlayer = player;
            vipActions.Add(new VoteMenuAction(player.DisplayName, GetVoteTeamLabel(player.Team), () =>
            {
                _networkClient.SendVoteCommand(
                    VoteCommandKind.StartVip,
                    targetSlot: capturedPlayer.Slot,
                    team: capturedPlayer.Team);
                CloseVoteMenu();
            }));
        }

        vipActions.Add(new VoteMenuAction("Back", string.Empty, () => SetVoteMenuPage(VoteMenuPage.Main)));
        return vipActions;
    }

    private void SubmitVoteMenuCommand(VoteCommandKind command)
    {
        _networkClient.SendVoteCommand(command, voteId: _voteMenuCatalog?.ActiveVoteId ?? 0);
        CloseVoteMenu();
    }

    private void SetVoteMenuPage(VoteMenuPage page)
    {
        _voteMenuPage = page;
        _voteMenuSelectedIndex = 0;
        _voteMenuScrollOffset = 0;
    }

    private void MoveVoteMenuSelection(int step, int count)
    {
        _voteMenuSelectedIndex = MoveControllerMenuSelection(_voteMenuSelectedIndex, count, step);
        if (_voteMenuSelectedIndex < _voteMenuScrollOffset)
        {
            _voteMenuScrollOffset = _voteMenuSelectedIndex;
        }
        else if (_voteMenuSelectedIndex >= _voteMenuScrollOffset + VoteMenuVisibleRows)
        {
            _voteMenuScrollOffset = _voteMenuSelectedIndex - VoteMenuVisibleRows + 1;
        }
    }

    private void GetVoteMenuLayout(int actionCount, out Rectangle panel, out Rectangle[] rowBounds)
    {
        var visibleRows = Math.Clamp(actionCount, 1, VoteMenuVisibleRows);
        const int rowHeight = 44;
        var panelWidth = Math.Min(480, Math.Max(280, ViewportWidth - 28));
        var panelHeight = 62 + (visibleRows * rowHeight) + 20;
        panel = new Rectangle(
            (ViewportWidth - panelWidth) / 2,
            (ViewportHeight - panelHeight) / 2,
            panelWidth,
            panelHeight);
        rowBounds = new Rectangle[visibleRows];
        for (var index = 0; index < visibleRows; index += 1)
        {
            rowBounds[index] = new Rectangle(panel.X + 12, panel.Y + 52 + (index * rowHeight), panel.Width - 24, rowHeight - 4);
        }
    }

    private string GetVoteMenuTitle() => _voteMenuPage switch
    {
        VoteMenuPage.MapNow => "Vote: Change Map Now",
        VoteMenuPage.MapNextRound => "Vote: Change Map Next Round",
        VoteMenuPage.Vip => "Vote: Select VIP",
        VoteMenuPage.Kick => "Vote: Kick Player",
        VoteMenuPage.Mute => "Vote: Mute Player",
        VoteMenuPage.Custom => "Plugin Votes",
        VoteMenuPage.CustomPlayer => "Vote: Select Player",
        VoteMenuPage.CustomMap => "Vote: Select Map",
        _ => "Vote Menu",
    };

    private static string GetVoteTeamLabel(byte team) => team switch
    {
        1 => "RED",
        2 => "BLU",
        _ => string.Empty,
    };
}
