using OpenGarrison.Core;
using OpenGarrison.Protocol;
using System.Text.Json;

partial class GameServer
{
    private const string VipPresentationSourcePluginId = "vip.mode";
    private const string VipPresentationTargetPluginId = "open.garrison.client";
    private const string VipPresentationMessageType = "vip-event";
    private const int VipPresentationDurationTicks = 180;

    private sealed record VipPresentationPayload(string Text, int DurationTicks);

    private void PublishVipAnnouncements()
    {
        if (!_world.IsVipModeActive)
        {
            ResetVipAnnouncementTracking();
            return;
        }

        if (_world.VipAssignmentVersion != _lastAnnouncedVipAssignmentVersion)
        {
            AnnounceVipAssignments();
            _lastAnnouncedVipAssignmentVersion = _world.VipAssignmentVersion;
        }

        if (!_world.VipWarmupActive
            && !_world.ControlPointSetupActive
            && _world.VipSlotsByTeam.Count > 0
            && _lastAnnouncedVipDirectiveAssignmentVersion != _world.VipAssignmentVersion)
        {
            AnnounceVipRoundDirective();
            _lastAnnouncedVipDirectiveAssignmentVersion = _world.VipAssignmentVersion;
        }
    }

    private void ResetVipAnnouncementTracking()
    {
        _lastAnnouncedVipAssignmentVersion = -1;
        _lastAnnouncedVipDirectiveAssignmentVersion = -1;
        _lastAnnouncedVipSlotsByTeam.Clear();
    }

    private void AnnounceVipAssignments()
    {
        foreach (var entry in _world.VipSlotsByTeam)
        {
            if (_lastAnnouncedVipSlotsByTeam.TryGetValue(entry.Key, out var previousSlot)
                && previousSlot == entry.Value)
            {
                continue;
            }

            _lastAnnouncedVipSlotsByTeam[entry.Key] = entry.Value;
            var label = TryGetPlayerDisplayName(entry.Value, out var playerName)
                ? playerName
                : $"slot {entry.Value}";
            var message = $"{label} is the VIP!";
            _adminOperations.BroadcastSystemMessage(message);
            BroadcastVipPresentation(message);
        }

        foreach (var staleTeam in _lastAnnouncedVipSlotsByTeam.Keys.Where(team => !_world.VipSlotsByTeam.ContainsKey(team)).ToArray())
        {
            _lastAnnouncedVipSlotsByTeam.Remove(staleTeam);
        }
    }

    private void AnnounceVipRoundDirective()
    {
        if (_world.VipSlotsByTeam.Count > 1)
        {
            SendVipDirectiveToTeam(PlayerTeam.Red, "Escort your VIP!");
            SendVipDirectiveToTeam(PlayerTeam.Blue, "Escort your VIP!");
            return;
        }

        SendVipDirectiveToTeam(PlayerTeam.Red, "Protect the VIP!");
        SendVipDirectiveToTeam(PlayerTeam.Blue, "Kill the VIP!");
    }

    private void SendVipDirectiveToTeam(PlayerTeam team, string message)
    {
        foreach (var client in _clientsBySlot.Values)
        {
            if (ServerHelpers.IsSpectatorSlot(client.Slot))
            {
                continue;
            }

            var clientTeam = _world.GetNetworkPlayerConfiguredTeam(client.Slot);
            if (clientTeam == team)
            {
                _adminOperations.SendSystemMessage(client.Slot, message);
                SendVipPresentation(client.Slot, message);
            }
        }
    }

    private void BroadcastVipPresentation(string message)
    {
        _outboundMessaging.BroadcastPluginMessage(
            VipPresentationSourcePluginId,
            VipPresentationTargetPluginId,
            VipPresentationMessageType,
            BuildVipPresentationPayload(message),
            PluginMessagePayloadFormat.Json,
            schemaVersion: 1);
    }

    private void SendVipPresentation(byte slot, string message)
    {
        _outboundMessaging.SendPluginMessage(
            slot,
            VipPresentationSourcePluginId,
            VipPresentationTargetPluginId,
            VipPresentationMessageType,
            BuildVipPresentationPayload(message),
            PluginMessagePayloadFormat.Json,
            schemaVersion: 1);
    }

    private static string BuildVipPresentationPayload(string message)
    {
        return JsonSerializer.Serialize(new VipPresentationPayload(message, VipPresentationDurationTicks));
    }

    private bool TryGetPlayerDisplayName(byte slot, out string playerName)
    {
        playerName = string.Empty;
        if (_clientsBySlot.TryGetValue(slot, out var client) && !string.IsNullOrWhiteSpace(client.Name))
        {
            playerName = client.Name;
            return true;
        }

        if (_world.TryGetNetworkPlayer(slot, out var player) && !string.IsNullOrWhiteSpace(player.DisplayName))
        {
            playerName = player.DisplayName;
            return true;
        }

        return false;
    }
}
