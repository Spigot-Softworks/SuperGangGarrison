#nullable enable

using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{

    public PlayerEntity? FindPlayerById(int playerId)
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

    public int GetResolvedLocalPlayerId()
    {
        if (_networkClient is { Protocol64ModeEnabled: true, IsSpectator: false }
            && _networkClient.TryGetProtocol64PlayerState(_networkClient.LocalPlayerSlot, out var state)
            && state.PlayerId <= int.MaxValue)
            return (int)state.PlayerId;
        return _localPlayerSnapshotEntityId ?? _world.LocalPlayer.Id;
    }
}
