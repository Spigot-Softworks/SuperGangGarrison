#nullable enable

using System;
using OpenGarrison.Protocol;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void HandleChatRelayMessage(ChatRelayMessage chatRelay)
    {
        AppendChatLine(chatRelay.PlayerName, chatRelay.Text, chatRelay.Team, chatRelay.TeamOnly);
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
        if (_networkClient.IsSpectator)
        {
            EnterOnlineSpectatorState("Connected as spectator.");
        }
        else if (wasSpectator)
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
            _ => "control command rejected",
        };
        if (ack.Kind == ControlCommandKind.SelectTeam && _networkClient.IsSpectator)
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
