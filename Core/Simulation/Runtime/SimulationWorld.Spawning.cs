namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    public bool TryMoveLocalPlayerToControlPointSpawn()
    {
        if (LocalPlayerAwaitingJoin || !LocalPlayer.IsAlive)
        {
            return false;
        }

        if (!TryResolveControlPointSpawn(LocalPlayer, LocalPlayer.Team, out var spawnX, out var spawnY))
        {
            return false;
        }

        SpawnPlayerResolved(LocalPlayer, LocalPlayer.Team, spawnX, spawnY, clearMedicHealingTarget: false);
        return true;
    }

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

    private bool TryResolveControlPointSpawn(PlayerEntity player, PlayerTeam team, out float spawnX, out float spawnY)
    {
        var marker = GetPreferredControlPointSpawnMarker(team);
        if (marker is null)
        {
            spawnX = 0f;
            spawnY = 0f;
            return false;
        }

        return TryFindSafeControlPointSpawnPosition(player, team, marker.Value, out spawnX, out spawnY);
    }

    private RoomObjectMarker? GetPreferredControlPointSpawnMarker(PlayerTeam team)
    {
        if (MatchRules.Mode == GameModeKind.KingOfTheHill)
        {
            return GetSingleKothPoint()?.Marker
                ?? Level.GetFirstRoomObject(RoomObjectType.ControlPoint);
        }

        if (MatchRules.Mode == GameModeKind.DoubleKingOfTheHill)
        {
            return GetDualKothPoint(team)?.Marker
                ?? Level.GetFirstRoomObject(RoomObjectType.ControlPoint);
        }

        return Level.GetFirstRoomObject(RoomObjectType.ControlPoint)
            ?? Level.GetFirstRoomObject(RoomObjectType.ArenaControlPoint);
    }

    private bool TryFindSafeControlPointSpawnPosition(PlayerEntity player, PlayerTeam team, RoomObjectMarker marker, out float spawnX, out float spawnY)
    {
        var markerCenterX = marker.CenterX;
        var markerCenterY = marker.CenterY;
        var horizontalOffsets = new[] { 0f, -16f, 16f, -32f, 32f, -48f, 48f, -64f, 64f };
        const float verticalStartOffset = -96f;
        const float verticalEndOffset = 96f;
        const float verticalStep = 4f;

        for (var horizontalIndex = 0; horizontalIndex < horizontalOffsets.Length; horizontalIndex += 1)
        {
            var candidateX = markerCenterX + horizontalOffsets[horizontalIndex];
            float? nearestOpenCandidateY = null;
            for (var candidateY = markerCenterY + verticalStartOffset; candidateY <= markerCenterY + verticalEndOffset; candidateY += verticalStep)
            {
                if (!player.CanOccupy(Level, team, candidateX, candidateY))
                {
                    continue;
                }

                if (!player.CanOccupy(Level, team, candidateX, candidateY + 1f))
                {
                    spawnX = candidateX;
                    spawnY = candidateY;
                    return true;
                }

                nearestOpenCandidateY ??= candidateY;
            }

            if (nearestOpenCandidateY.HasValue)
            {
                spawnX = candidateX;
                spawnY = nearestOpenCandidateY.Value;
                return true;
            }
        }

        spawnX = 0f;
        spawnY = 0f;
        return false;
    }
}
