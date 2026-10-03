using OpenGarrison.Protocol;

namespace OpenGarrison.Core;

internal sealed partial class NetworkPlayerSystem
{
    internal void AdvancePlayableNetworkPlayer(byte slot)
    {
        if (!_host.IsNetworkPlayerActive(slot) || !TryGetNetworkPlayer(slot, out var player))
        {
            return;
        }

        var input = ResolveNetworkPlayerInput(slot);
        var previousInput = GetPreviousNetworkInput(slot);
        if (_host.PlayerRegistry.ForcedPressedButtons.TryGetValue(slot, out var forcedPressedButtons))
        {
            if (forcedPressedButtons.ExplicitOnly)
            {
                // Held levels and reliable presses are independent on protocol 64.
                // Suppress inferred rises but retain falls for release-to-fire and
                // charge release. The command below supplies the only press edge.
                previousInput = previousInput with
                {
                    Up = previousInput.Up || input.Up,
                    FirePrimary = previousInput.FirePrimary || input.FirePrimary,
                    FireSecondary = previousInput.FireSecondary || input.FireSecondary,
                    UseAbility = previousInput.UseAbility || input.UseAbility,
                };
            }
            previousInput = ClearForcedPressedButtons(previousInput, forcedPressedButtons.Buttons);
            _host.PlayerRegistry.ForcedPressedButtons.Remove(slot);
        }
        if (player.IsAlive)
        {
            _host.PlayerInput.AdvanceAlivePlayerWithInput(player, input, previousInput, _host.GetNetworkPlayerTeam(slot), slot == SimulationConstants.LocalPlayerSlot);
            _host.LastToDieRules.AdvanceLastToDiePassivePerks(slot, player);
        }
        else
        {
            _host.PlayerDeaths.AdvanceNetworkRespawnTimer(slot);
            _host.Movement.ClearJumpInputBuffer(player);
            input = ClearRespawnActionInputState(input);
        }

        SetPreviousNetworkInput(slot, input);
    }

    internal PlayerInputSnapshot ResolveNetworkPlayerInput(byte slot)
    {
        if (slot == SimulationConstants.LocalPlayerSlot)
        {
            return _host.LocalState.Input;
        }

        return _host.PlayerRegistry.Inputs.TryGetValue(slot, out var input) ? input : default;
    }

    internal PlayerInputSnapshot GetPreviousNetworkInput(byte slot)
    {
        if (slot == SimulationConstants.LocalPlayerSlot)
        {
            return _host.LocalState.PreviousInput;
        }

        return _host.PlayerRegistry.PreviousInputs.TryGetValue(slot, out var input) ? input : default;
    }

    internal void SetPreviousNetworkInput(byte slot, PlayerInputSnapshot input)
    {
        if (slot == SimulationConstants.LocalPlayerSlot)
        {
            _host.LocalState.PreviousInput = input;
            return;
        }

        _host.PlayerRegistry.PreviousInputs[slot] = input;
    }

    internal static PlayerInputSnapshot ClearRespawnActionInputState(PlayerInputSnapshot input)
    {
        return input with
        {
            BuildSentry = false,
            BuildDispenser = false,
            DestroySentry = false,
            DestroyDispenser = false,
            BuildJumpPad = false,
            DestroyJumpPad = false,
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

    internal static PlayerInputSnapshot ClearForcedPressedButtons(
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
            BuildJumpPad = forcedPressedButtons.HasFlag(InputButtons.BuildJumpPad) ? false : input.BuildJumpPad,
            DestroyJumpPad = forcedPressedButtons.HasFlag(InputButtons.DestroyJumpPad) ? false : input.DestroyJumpPad,
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
