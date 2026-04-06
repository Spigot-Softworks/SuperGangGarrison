namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private void AdvancePlayableNetworkPlayer(byte slot)
    {
        if (!IsNetworkPlayerActive(slot) || !TryGetNetworkPlayer(slot, out var player))
        {
            return;
        }

        var input = ResolveNetworkPlayerInput(slot);
        var previousInput = GetPreviousNetworkInput(slot);
        if (player.IsAlive)
        {
            AdvanceAlivePlayerWithInput(player, input, previousInput, GetNetworkPlayerTeam(slot), slot == LocalPlayerSlot);
        }
        else
        {
            AdvanceNetworkRespawnTimer(slot);
        }

        SetPreviousNetworkInput(slot, input);
    }

    private PlayerInputSnapshot ResolveNetworkPlayerInput(byte slot)
    {
        if (slot == LocalPlayerSlot)
        {
            return _localInput;
        }

        return _additionalNetworkPlayerInputs.TryGetValue(slot, out var input) ? input : default;
    }

    private PlayerInputSnapshot GetPreviousNetworkInput(byte slot)
    {
        if (slot == LocalPlayerSlot)
        {
            return _previousLocalInput;
        }

        return _additionalNetworkPlayerPreviousInputs.TryGetValue(slot, out var input) ? input : default;
    }

    private void SetPreviousNetworkInput(byte slot, PlayerInputSnapshot input)
    {
        if (slot == LocalPlayerSlot)
        {
            _previousLocalInput = input;
            return;
        }

        _additionalNetworkPlayerPreviousInputs[slot] = input;
    }
}
