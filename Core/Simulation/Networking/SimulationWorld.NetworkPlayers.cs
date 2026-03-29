namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    public bool TryGetNetworkPlayer(byte slot, out PlayerEntity player)
    {
        if (slot == LocalPlayerSlot)
        {
            player = LocalPlayer;
            return true;
        }

        if (TryGetAdditionalNetworkPlayer(slot, out player))
        {
            return true;
        }

        player = null!;
        return false;
    }

    public IEnumerable<(byte Slot, PlayerEntity Player)> EnumerateActiveNetworkPlayers()
    {
        for (var index = 0; index < NetworkPlayerSlots.Count; index += 1)
        {
            var slot = NetworkPlayerSlots[index];
            if (!IsNetworkPlayerEnabled(slot)
                || IsNetworkPlayerAwaitingJoin(slot)
                || !TryGetNetworkPlayer(slot, out var player))
            {
                continue;
            }

            yield return (slot, player);
        }
    }

    public IEnumerable<(byte Slot, PlayerEntity Player)> EnumerateReplicatedNetworkPlayers()
    {
        for (var index = 0; index < NetworkPlayerSlots.Count; index += 1)
        {
            var slot = NetworkPlayerSlots[index];
            if (!IsNetworkPlayerEnabled(slot)
                || !TryGetNetworkPlayer(slot, out var player))
            {
                continue;
            }

            yield return (slot, player);
        }
    }

    public int GetNetworkPlayerRespawnTicks(byte slot)
    {
        return slot switch
        {
            LocalPlayerSlot => LocalPlayerRespawnTicks,
            _ when _additionalNetworkPlayerRespawnTicks.TryGetValue(slot, out var ticks) => ticks,
            _ => 0,
        };
    }

    public bool IsNetworkPlayerAwaitingJoin(byte slot)
    {
        return slot switch
        {
            LocalPlayerSlot => _localPlayerAwaitingJoin,
            _ when _additionalNetworkPlayerAwaitingJoin.TryGetValue(slot, out var awaitingJoin) => awaitingJoin,
            _ => false,
        };
    }

    public static bool IsPlayableNetworkPlayerSlot(byte slot)
    {
        return slot >= LocalPlayerSlot && slot <= MaxPlayableNetworkPlayers;
    }

    public static string GetNetworkPlayerDefaultName(byte slot)
    {
        return slot switch
        {
            LocalPlayerSlot => DefaultLocalPlayerName,
            _ => $"Player {slot}",
        };
    }

    private bool TryGetNetworkPlayerSlot(PlayerEntity player, out byte slot)
    {
        if (ReferenceEquals(player, LocalPlayer))
        {
            slot = LocalPlayerSlot;
            return true;
        }

        foreach (var entry in _additionalNetworkPlayersBySlot)
        {
            if (ReferenceEquals(entry.Value, player))
            {
                slot = entry.Key;
                return true;
            }
        }

        slot = 0;
        return false;
    }

    private bool TryGetAdditionalNetworkPlayer(byte slot, out PlayerEntity player)
    {
        return _additionalNetworkPlayersBySlot.TryGetValue(slot, out player!);
    }

    private PlayerEntity EnsureAdditionalNetworkPlayer(byte slot)
    {
        if (_additionalNetworkPlayersBySlot.TryGetValue(slot, out var player))
        {
            return player;
        }

        var definition = CharacterClassCatalog.Scout;
        var defaultTeam = GetDefaultNetworkPlayerTeam(slot);
        player = new PlayerEntity(AllocateEntityId(), definition, GetNetworkPlayerDefaultName(slot));
        SpawnPlayerResolved(player, defaultTeam, ReserveSpawn(player, defaultTeam), clearMedicHealingTarget: false);
        player.Kill();
        _additionalNetworkPlayersBySlot[slot] = player;
        _additionalNetworkPlayerClassDefinitions[slot] = definition;
        _additionalNetworkPlayerTeams[slot] = defaultTeam;
        _additionalNetworkPlayerAwaitingJoin[slot] = true;
        _additionalNetworkPlayerRespawnTicks[slot] = 0;
        _entities.Add(player.Id, player);
        return player;
    }

    private static PlayerTeam GetDefaultNetworkPlayerTeam(byte slot)
    {
        return slot % 2 == 0 ? PlayerTeam.Blue : PlayerTeam.Red;
    }

    private bool IsNetworkPlayerEnabled(byte slot)
    {
        return slot switch
        {
            LocalPlayerSlot => true,
            _ => _enabledAdditionalNetworkPlayerSlots.Contains(slot),
        };
    }

    private void SetNetworkPlayerEnabled(byte slot, bool enabled)
    {
        if (slot == LocalPlayerSlot)
        {
            return;
        }

        if (enabled)
        {
            EnsureAdditionalNetworkPlayer(slot);
            _enabledAdditionalNetworkPlayerSlots.Add(slot);
        }
        else
        {
            _enabledAdditionalNetworkPlayerSlots.Remove(slot);
        }
    }
}
