using OpenGarrison.Protocol;

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
        if (_networkPlayerForcedPressedButtons.TryGetValue(slot, out var forcedPressedButtons))
        {
            previousInput = ClearForcedPressedButtons(previousInput, forcedPressedButtons);
            _networkPlayerForcedPressedButtons.Remove(slot);
        }
        if (player.IsAlive)
        {
            AdvanceAlivePlayerWithInput(player, input, previousInput, GetNetworkPlayerTeam(slot), slot == LocalPlayerSlot);
            AdvanceLastToDiePassivePerks(slot, player);
        }
        else
        {
            AdvanceNetworkRespawnTimer(slot);
            ClearJumpInputBuffer(player);
            input = ClearRespawnActionInputState(input);
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

    private static PlayerInputSnapshot ClearRespawnActionInputState(PlayerInputSnapshot input)
    {
        return input with
        {
            BuildSentry = false,
            BuildDispenser = false,
            DestroySentry = false,
            DestroyDispenser = false,
            Taunt = false,
            FirePrimary = false,
            FireSecondary = false,
            DebugKill = false,
            DropIntel = false,
            UseAbility = false,
            InteractWeapon = false,
            SwapWeapon = false,
            ToggleSecondaryWeapon = false,
            ReadyUp = false,
        };
    }

    private static PlayerInputSnapshot ClearForcedPressedButtons(
        PlayerInputSnapshot input,
        InputButtons forcedPressedButtons)
    {
        return input with
        {
            Up = forcedPressedButtons.HasFlag(InputButtons.Up) ? false : input.Up,
            BuildSentry = forcedPressedButtons.HasFlag(InputButtons.BuildSentry) ? false : input.BuildSentry,
            BuildDispenser = forcedPressedButtons.HasFlag(InputButtons.BuildDispenser) ? false : input.BuildDispenser,
            DestroySentry = forcedPressedButtons.HasFlag(InputButtons.DestroySentry) ? false : input.DestroySentry,
            DestroyDispenser = forcedPressedButtons.HasFlag(InputButtons.DestroyDispenser) ? false : input.DestroyDispenser,
            Taunt = forcedPressedButtons.HasFlag(InputButtons.Taunt) ? false : input.Taunt,
            FirePrimary = forcedPressedButtons.HasFlag(InputButtons.FirePrimary) ? false : input.FirePrimary,
            FireSecondary = forcedPressedButtons.HasFlag(InputButtons.FireSecondary) ? false : input.FireSecondary,
            DebugKill = forcedPressedButtons.HasFlag(InputButtons.DebugKill) ? false : input.DebugKill,
            DropIntel = forcedPressedButtons.HasFlag(InputButtons.DropIntel) ? false : input.DropIntel,
            UseAbility = forcedPressedButtons.HasFlag(InputButtons.UseAbility) ? false : input.UseAbility,
            InteractWeapon = forcedPressedButtons.HasFlag(InputButtons.InteractWeapon) ? false : input.InteractWeapon,
            SwapWeapon = forcedPressedButtons.HasFlag(InputButtons.SwapWeapon) ? false : input.SwapWeapon,
            ToggleSecondaryWeapon = forcedPressedButtons.HasFlag(InputButtons.ToggleSecondaryWeapon) ? false : input.ToggleSecondaryWeapon,
            ReadyUp = forcedPressedButtons.HasFlag(InputButtons.ReadyUp) ? false : input.ReadyUp,
        };
    }
}
