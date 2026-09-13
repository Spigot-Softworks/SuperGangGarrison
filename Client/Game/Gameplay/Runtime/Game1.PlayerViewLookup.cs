#nullable enable

using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private static string GetIntelStateLabel(TeamIntelligenceState intelState)
    {
        if (intelState.IsAtBase)
        {
            return "home";
        }

        if (intelState.IsDropped)
        {
            return $"dropped:{intelState.ReturnTicksRemaining}";
        }

        return "carried";
    }

    private PlayerEntity? FindPlayerById(int playerId)
    {
        if (GetResolvedLocalPlayerId() == playerId)
        {
            return _world.LocalPlayer;
        }

        foreach (var player in EnumerateRemotePlayersForView())
        {
            if (player.Id == playerId)
            {
                return player;
            }
        }

        return null;
    }

    private int GetResolvedLocalPlayerId()
    {
        if (_networkClient is { Protocol64ModeEnabled: true, IsSpectator: false }
            && _networkClient.TryGetProtocol64PlayerState(_networkClient.LocalPlayerSlot, out var state)
            && state.PlayerId <= int.MaxValue)
            return (int)state.PlayerId;
        return _localPlayerSnapshotEntityId ?? _world.LocalPlayer.Id;
    }
}
