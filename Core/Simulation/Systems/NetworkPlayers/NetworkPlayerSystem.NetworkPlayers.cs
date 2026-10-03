namespace OpenGarrison.Core;

internal sealed partial class NetworkPlayerSystem
{
    public bool TryGetNetworkPlayer(byte slot, out PlayerEntity player)
    {
        if (slot == SimulationConstants.LocalPlayerSlot)
        {
            player = _host.LocalPlayer;
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
        foreach (var slot in EnumerateEnabledNetworkPlayerSlots())
        {
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
        foreach (var slot in EnumerateEnabledNetworkPlayerSlots())
        {
            if (!IsNetworkPlayerEnabled(slot)
                || !TryGetNetworkPlayer(slot, out var player))
            {
                continue;
            }

            yield return (slot, player);
        }
    }

    internal IEnumerable<byte> EnumerateEnabledNetworkPlayerSlots()
    {
        // The local player is always slot 1 in the simulation. Additional
        // slots are materialized only when a player is actually enabled.
        yield return SimulationConstants.LocalPlayerSlot;
        foreach (var slot in _host.PlayerRegistry.EnabledAdditionalSlots)
        {
            yield return slot;
        }
    }

    public bool TryGetPlayerNetworkSlot(PlayerEntity player, out byte slot)
    {
        if (ReferenceEquals(player, _host.LocalPlayer))
        {
            slot = SimulationConstants.LocalPlayerSlot;
            return true;
        }

        foreach (var entry in _host.RemoteSnapshots.PlayersBySlot)
        {
            if (ReferenceEquals(entry.Value, player))
            {
                slot = entry.Key;
                return true;
            }
        }

        foreach (var entry in _host.RemoteSnapshots.ScoreboardPlayersBySlot)
        {
            if (ReferenceEquals(entry.Value, player))
            {
                slot = entry.Key;
                return true;
            }
        }

        foreach (var entry in _host.PlayerRegistry.PlayersBySlot)
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

    public int GetNetworkPlayerRespawnTicks(byte slot)
    {
        return slot switch
        {
            SimulationConstants.LocalPlayerSlot => _host.LocalPlayerRespawnTicks,
            _ when _host.PlayerRegistry.RespawnTicks.TryGetValue(slot, out var ticks) => ticks,
            _ => 0,
        };
    }

    public int GetNetworkPlayerPingMilliseconds(byte slot)
    {
        return _host.PlayerRegistry.PingMillisecondsBySlot.TryGetValue(slot, out var pingMilliseconds)
            ? pingMilliseconds
            : -1;
    }

    public bool IsNetworkPlayerBot(byte slot) => _host.PlayerRegistry.BotSlots.Contains(slot);

    internal void ApplySnapshotNetworkPlayerBot(byte slot, bool isBot)
    {
        if (isBot)
        {
            _host.PlayerRegistry.BotSlots.Add(slot);
            return;
        }

        _host.PlayerRegistry.BotSlots.Remove(slot);
    }

    internal void ApplySnapshotNetworkPlayerPingMilliseconds(byte slot, int pingMilliseconds)
    {
        if (pingMilliseconds >= 0)
        {
            _host.PlayerRegistry.PingMillisecondsBySlot[slot] = Math.Clamp(pingMilliseconds, 0, 9999);
            return;
        }

        _host.PlayerRegistry.PingMillisecondsBySlot.Remove(slot);
    }

    public bool IsNetworkPlayerAwaitingJoin(byte slot)
    {
        return slot switch
        {
            SimulationConstants.LocalPlayerSlot => _host.LocalState.PlayerAwaitingJoin,
            _ when _host.PlayerRegistry.AwaitingJoin.TryGetValue(slot, out var awaitingJoin) => awaitingJoin,
            _ => false,
        };
    }

    public static bool IsPlayableNetworkPlayerSlot(byte slot)
    {
        return slot >= SimulationConstants.LocalPlayerSlot && slot <= SimulationConstants.MaxPlayableNetworkPlayers;
    }

    public static string GetNetworkPlayerDefaultName(byte slot)
    {
        return slot switch
        {
            SimulationConstants.LocalPlayerSlot => SimulationConstants.DefaultLocalPlayerName,
            _ => $"Player {slot}",
        };
    }

    internal bool TryGetNetworkPlayerSlot(PlayerEntity player, out byte slot)
    {
        return _host.PlayerRegistry.SlotsByPlayerId.TryGetValue(player.Id, out slot);
    }

    internal bool TryGetAdditionalNetworkPlayer(byte slot, out PlayerEntity player)
    {
        return _host.PlayerRegistry.PlayersBySlot.TryGetValue(slot, out player!);
    }

    internal PlayerEntity EnsureAdditionalNetworkPlayer(byte slot)
    {
        if (_host.PlayerRegistry.PlayersBySlot.TryGetValue(slot, out var player))
        {
            return player;
        }

        var definition = CharacterClassCatalog.Scout;
        var defaultTeam = GetDefaultNetworkPlayerTeam(slot);
        player = new PlayerEntity(_host.AllocateEntityId(), definition, GetNetworkPlayerDefaultName(slot));
        player.SetPlayerScale(_host.MatchSettings.PlayerScale);
        _host.ServerTuning.ApplyServerGameplayTuning(slot, player);
        _host.Spawns.SpawnPlayerResolved(player, defaultTeam, _host.Spawns.ReserveSpawn(player, defaultTeam), clearMedicHealingTarget: false);
        player.Kill();
        _host.PlayerRegistry.PlayersBySlot[slot] = player;
        _host.PlayerRegistry.SlotsByPlayerId[player.Id] = slot;
        _host.PlayerRegistry.ClassDefinitions[slot] = definition;
        _host.PlayerRegistry.Teams[slot] = defaultTeam;
        _host.PlayerRegistry.AwaitingJoin[slot] = true;
        _host.PlayerRegistry.RespawnTicks[slot] = 0;
        _host.EntityStore.Add(player);
        return player;
    }

    internal static PlayerTeam GetDefaultNetworkPlayerTeam(byte slot)
    {
        return slot % 2 == 0 ? PlayerTeam.Blue : PlayerTeam.Red;
    }

    internal bool IsNetworkPlayerEnabled(byte slot)
    {
        return slot switch
        {
            SimulationConstants.LocalPlayerSlot => true,
            _ => _host.PlayerRegistry.EnabledAdditionalSlots.Contains(slot),
        };
    }

    internal void SetNetworkPlayerEnabled(byte slot, bool enabled)
    {
        if (slot == SimulationConstants.LocalPlayerSlot)
        {
            return;
        }

        if (enabled)
        {
            var player = EnsureAdditionalNetworkPlayer(slot);
            _host.PlayerRegistry.EnabledAdditionalSlots.Add(slot);
            _host.PlayerRegistry.ActivePlayersById[player.Id] = player;
        }
        else
        {
            if (_host.PlayerRegistry.EnabledAdditionalSlots.Remove(slot)
                && _host.PlayerRegistry.PlayersBySlot.TryGetValue(slot, out var player))
            {
                _host.PlayerRegistry.ActivePlayersById.Remove(player.Id);
            }
        }
    }
}
