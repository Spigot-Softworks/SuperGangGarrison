namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private void SpawnPlayerResolved(PlayerEntity player, PlayerTeam team, float x, float y, bool clearMedicHealingTarget = true)
    {
        player.Spawn(team, x, y);
        player.ResolveBlockingOverlap(Level, team);
        UpdateSpawnRoomState(player);
        if (clearMedicHealingTarget)
        {
            player.ClearMedicHealingTarget();
        }
    }

    private void SpawnPlayerResolved(PlayerEntity player, PlayerTeam team, SpawnPoint spawn, bool clearMedicHealingTarget = true)
    {
        SpawnPlayerResolved(player, team, spawn.X, spawn.Y, clearMedicHealingTarget);
    }

    private void RespawnConfiguredNetworkPlayer(byte slot, PlayerEntity player)
    {
        var team = GetNetworkPlayerConfiguredTeam(slot);
        player.SetClassDefinition(GetNetworkPlayerClassDefinition(slot));
        SpawnPlayerResolved(player, team, ReserveSpawn(player, team));
    }

    private void RespawnPlayersForNewRound()
    {
        for (var index = 0; index < NetworkPlayerSlots.Count; index += 1)
        {
            var slot = NetworkPlayerSlots[index];
            if (!TryGetNetworkPlayer(slot, out var player))
            {
                continue;
            }

            player.SetClassDefinition(GetNetworkPlayerClassDefinition(slot));
            if (IsNetworkPlayerAwaitingJoin(slot))
            {
                player.ClearMedicHealingTarget();
                player.Kill();
                continue;
            }

            RespawnConfiguredNetworkPlayer(slot, player);
        }

        if (EnemyPlayerEnabled)
        {
            EnemyPlayer.SetClassDefinition(_enemyDummyClassDefinition);
            SpawnPlayerResolved(EnemyPlayer, _enemyDummyTeam, ReserveSpawn(EnemyPlayer, _enemyDummyTeam));
            _enemyDummyRespawnTicks = 0;
        }
        else
        {
            EnemyPlayer.Kill();
            _enemyDummyRespawnTicks = 0;
        }

        if (FriendlyDummyEnabled)
        {
            FriendlyDummy.SetClassDefinition(_friendlyDummyClassDefinition);
            if (IsNetworkPlayerAwaitingJoin(LocalPlayerSlot))
            {
                FriendlyDummy.Kill();
            }
            else
            {
                var friendlySpawn = FindFriendlyDummySpawnNearLocalPlayer();
                SpawnPlayerResolved(FriendlyDummy, GetNetworkPlayerConfiguredTeam(LocalPlayerSlot), friendlySpawn.X, friendlySpawn.Y);
            }
        }
        else
        {
            FriendlyDummy.Kill();
        }
    }

    private SpawnPoint ReserveSpawn(PlayerEntity player, PlayerTeam team)
    {
        var spawns = team == PlayerTeam.Blue ? Level.BlueSpawns : Level.RedSpawns;
        if (spawns.Count == 0)
        {
            return Level.LocalSpawn;
        }

        var spawnRooms = Level.GetRoomObjects(RoomObjectType.SpawnRoom);
        var requireSpawnRoom = spawnRooms.Count > 0;
        var startIndex = team == PlayerTeam.Blue ? _nextBlueSpawnIndex : _nextRedSpawnIndex;
        var selectedIndex = -1;
        SpawnPoint selectedSpawn = default;

        for (var offset = 0; offset < spawns.Count; offset += 1)
        {
            var index = (startIndex + offset) % spawns.Count;
            var spawn = spawns[index];
            if (requireSpawnRoom && !IsSpawnPointInsideSpawnRoom(spawn, spawnRooms))
            {
                continue;
            }

            if (!player.CanOccupy(Level, team, spawn.X, spawn.Y))
            {
                continue;
            }

            selectedIndex = index;
            selectedSpawn = spawn;
            break;
        }

        if (selectedIndex < 0)
        {
            selectedIndex = startIndex % spawns.Count;
            selectedSpawn = spawns[selectedIndex];
        }

        if (team == PlayerTeam.Blue)
        {
            _nextBlueSpawnIndex = selectedIndex + 1;
        }
        else
        {
            _nextRedSpawnIndex = selectedIndex + 1;
        }

        return selectedSpawn;
    }

    private static bool IsSpawnPointInsideSpawnRoom(SpawnPoint spawn, IReadOnlyList<RoomObjectMarker> spawnRooms)
    {
        for (var index = 0; index < spawnRooms.Count; index += 1)
        {
            var room = spawnRooms[index];
            if (spawn.X >= room.Left && spawn.X <= room.Right && spawn.Y >= room.Top && spawn.Y <= room.Bottom)
            {
                return true;
            }
        }

        return false;
    }
}
