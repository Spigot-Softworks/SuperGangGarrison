#nullable enable

using System;
using OpenGarrison.Protocol;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void HandleChatRelayMessage(ChatRelayMessage chatRelay)
    {
        if (IsChatRelayMuted(chatRelay))
        {
            return;
        }

        AppendChatLine(chatRelay.PlayerName, chatRelay.Text, chatRelay.Team, chatRelay.TeamOnly, chatRelay.PlayerSlot);
        TryShowOverheadChatMessage(chatRelay);
    }

    private bool IsChatRelayMuted(ChatRelayMessage chatRelay)
    {
        if (chatRelay.PlayerSlot != 0)
        {
            return IsScoreboardSlotMuted(chatRelay.PlayerSlot);
        }

        return TryResolveOverheadChatPlayerSlot(chatRelay, out var slot)
            && IsScoreboardSlotMuted(slot);
    }

    private void HandleAutoBalanceNoticeMessage(AutoBalanceNoticeMessage notice)
    {
        if (notice.Kind == AutoBalanceNoticeKind.Pending)
        {
            var delaySeconds = Math.Max(1, notice.DelaySeconds);
            var fromLabel = GetTeamLabel(notice.FromTeam);
            var toLabel = GetTeamLabel(notice.ToTeam);
            var label = fromLabel == "??" || toLabel == "??"
                ? $"Auto-balance in {delaySeconds}s."
                : $"Auto-balance in {delaySeconds}s (moving {fromLabel} to {toLabel}).";
            ShowAutoBalanceNotice(label, delaySeconds);
            AddNetworkConsoleLine(label);
            return;
        }

        var destinationLabel = GetTeamLabel(notice.ToTeam);
        var appliedLabel = string.IsNullOrWhiteSpace(notice.PlayerName)
            ? "Auto-balance applied."
            : destinationLabel == "??"
                ? $"Auto-balance: {notice.PlayerName} moved."
                : $"Auto-balance: {notice.PlayerName} moved to {destinationLabel}.";
        ShowAutoBalanceNotice(appliedLabel, 6);
        AddNetworkConsoleLine(appliedLabel);
    }

    private void HandleSessionSlotChangedMessage(SessionSlotChangedMessage slotChanged)
    {
        var wasSpectator = _networkClient.IsSpectator;
        _networkClient.SetLocalPlayerSlot(slotChanged.PlayerSlot);
        if (IsWatchOnlySession() && !_networkClient.IsSpectator)
        {
            ReturnToMainMenuWithNetworkStatus("Watch session was moved to a playable slot.");
            return;
        }

        if (_networkClient.IsSpectator)
        {
            EnterOnlineSpectatorState("Connected as spectator.");
        }
        else if (wasSpectator && !IsWatchOnlySession())
        {
            EnterOnlineClassSelectionState(string.Empty);
        }

        AddNetworkConsoleLine($"session slot changed to {_networkClient.LocalPlayerSlot}");
    }

    private void HandleControlAckMessage(ControlAckMessage ack)
    {
        _networkClient.AcknowledgeControlCommand(ack.Sequence, ack.Kind);
        if (ack.Accepted)
        {
            return;
        }

        var description = ack.Kind switch
        {
            ControlCommandKind.SelectTeam => "team selection rejected",
            ControlCommandKind.SelectClass => "class selection rejected",
            ControlCommandKind.Spectate => "spectate request rejected",
            ControlCommandKind.SelectGameplayLoadout => "loadout selection rejected",
            _ => "control command rejected",
        };
        if (ack.Kind == ControlCommandKind.SelectTeam && _networkClient.IsSpectator && !IsWatchOnlySession())
        {
            OpenOnlineTeamSelection(clearPendingSelections: false, statusMessage: description);
        }
        else
        {
            SetNetworkStatus(description);
        }

        AddNetworkConsoleLine(description);
    }
}
