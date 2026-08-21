namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    public bool TryMoveLocalPlayerToControlPointSpawn()
    {
        return TryMoveNetworkPlayerToControlPointSpawn(LocalPlayerSlot);
    }

    public bool TryMoveNetworkPlayerToControlPointSpawn(byte slot)
    {
        if (IsNetworkPlayerAwaitingJoin(slot)
            || !TryGetNetworkPlayer(slot, out var player)
            || !player.IsAlive)
        {
            return false;
        }

        if (!TryResolveControlPointSpawn(player, player.Team, out var spawnX, out var spawnY))
        {
            return false;
        }

        SpawnPlayerResolved(player, player.Team, spawnX, spawnY, clearMedicHealingTarget: false);
        return true;
    }

    public bool TryMoveLocalPlayerToIntelSpawn()
    {
        return TryMoveNetworkPlayerToIntelSpawn(LocalPlayerSlot);
    }

    public bool TryMoveNetworkPlayerToIntelSpawn(byte slot)
    {
        if (IsNetworkPlayerAwaitingJoin(slot)
            || !TryGetNetworkPlayer(slot, out var player)
            || !player.IsAlive)
        {
            return false;
        }

        var ownIntelBase = Level.GetIntelBase(player.Team);
        if (!ownIntelBase.HasValue
            || !TryFindSafeObjectiveSpawnPosition(player, player.Team, ownIntelBase.Value.X, ownIntelBase.Value.Y, out var spawnX, out var spawnY))
        {
            return false;
        }

        SpawnPlayerResolved(player, player.Team, spawnX, spawnY, clearMedicHealingTarget: false);
        return true;
    }

    public bool TryMoveNetworkPlayerToLastToDieObjectiveSpawn(byte slot)
    {
        return MatchRules.Mode == GameModeKind.CaptureTheFlag
            ? TryMoveNetworkPlayerToIntelSpawn(slot)
            : TryMoveNetworkPlayerToControlPointSpawn(slot);
    }

    public bool TryMoveNetworkPlayerToLastToDieEnemySpawn(byte slot, PlayerTeam spawnSide)
        => TryConfigureNetworkPlayerLastToDieEnemySpawn(
            slot,
            spawnSide,
            repositionAlivePlayer: true);

    public bool TryConfigureNetworkPlayerLastToDieEnemySpawn(
        byte slot,
        PlayerTeam spawnSide,
        bool repositionAlivePlayer)
    {
        if (spawnSide is not (PlayerTeam.Red or PlayerTeam.Blue)
            || IsNetworkPlayerAwaitingJoin(slot)
            || !TryGetNetworkPlayer(slot, out var player)
            || repositionAlivePlayer && !player.IsAlive)
        {
            return false;
        }

        var sideSpawns = spawnSide == PlayerTeam.Red ? Level.RedSpawns : Level.BlueSpawns;
        if (sideSpawns.Count == 0)
        {
            return false;
        }

        if (spawnSide == player.Team)
        {
            if (!TryClearNetworkPlayerSpawnOverride(slot) || !repositionAlivePlayer)
            {
                return !repositionAlivePlayer;
            }

            var teamSpawn = ReserveSpawn(player, spawnSide);
            return SpawnPlayerResolved(
                player,
                player.Team,
                teamSpawn,
                clearMedicHealingTarget: false);
        }

        var opposingSpawn = ReserveSpawn(player, spawnSide);
        if (!TryFindLastToDieEnemyIngressSpawnPosition(
                player,
                opposingSpawn,
                out var spawnX,
                out var spawnY))
        {
            return false;
        }

        if (!TrySetNetworkPlayerSpawnOverride(slot, spawnX, spawnY))
        {
            return false;
        }

        if (!repositionAlivePlayer)
        {
            return true;
        }

        return SpawnPlayerResolved(
                player,
                player.Team,
                spawnX,
                spawnY,
                clearMedicHealingTarget: false);
    }

    private bool TryFindLastToDieEnemyIngressSpawnPosition(
        PlayerEntity player,
        SpawnPoint sourceSpawn,
        out float spawnX,
        out float spawnY)
    {
        const float step = 8f;
        var centerX = Bounds.Width * 0.5f;
        var direction = MathF.Sign(centerX - sourceSpawn.X);
        if (direction == 0f)
        {
            direction = player.Team == PlayerTeam.Blue ? -1 : 1;
        }

        var spawnRooms = Level.GetRoomObjects(RoomObjectType.SpawnRoom);
        (float X, float Y)? firstOpenPosition = null;
        var maximumSteps = Math.Max(1, (int)MathF.Ceiling(Bounds.Width / step));
        var previousX = sourceSpawn.X;
        for (var stepIndex = 0; stepIndex <= maximumSteps; stepIndex += 1)
        {
            var candidateX = Math.Clamp(
                sourceSpawn.X + (direction * stepIndex * step),
                -player.CollisionLeftOffset,
                Bounds.Width - player.CollisionRightOffset);
            if (stepIndex > 0 && candidateX == previousX)
            {
                break;
            }

            previousX = candidateX;
            if (IntersectsAnySpawnRoom(player, candidateX, sourceSpawn.Y, spawnRooms)
                || !player.CanOccupy(Level, player.Team, candidateX, sourceSpawn.Y))
            {
                continue;
            }

            firstOpenPosition ??= (candidateX, sourceSpawn.Y);
            if (!player.CanOccupy(Level, player.Team, candidateX, sourceSpawn.Y + 1f))
            {
                spawnX = candidateX;
                spawnY = sourceSpawn.Y;
                return true;
            }
        }

        if (firstOpenPosition.HasValue)
        {
            spawnX = firstOpenPosition.Value.X;
            spawnY = firstOpenPosition.Value.Y;
            return true;
        }

        spawnX = 0f;
        spawnY = 0f;
        return false;
    }

    private static bool IntersectsAnySpawnRoom(
        PlayerEntity player,
        float x,
        float y,
        IReadOnlyList<RoomObjectMarker> spawnRooms)
    {
        var left = x + player.CollisionLeftOffset;
        var right = x + player.CollisionRightOffset;
        var top = y + player.CollisionTopOffset;
        var bottom = y + player.CollisionBottomOffset;
        for (var index = 0; index < spawnRooms.Count; index += 1)
        {
            var room = spawnRooms[index];
            if (left < room.Right
                && right > room.Left
                && top < room.Bottom
                && bottom > room.Top)
            {
                return true;
            }
        }

        return false;
    }

    private bool SpawnPlayerResolved(
        PlayerEntity player,
        PlayerTeam team,
        float x,
        float y,
        bool clearMedicHealingTarget = true,
        bool playRespawnSound = false)
    {
        if (ShouldCancelSpawn(player, team, x, y))
        {
            return false;
        }

        if (clearMedicHealingTarget)
        {
            ClearLastToDieStatusEffectsForTarget(player.Id);
        }

        player.Spawn(team, x, y);
        player.ResolveBlockingOverlap(Level, team);
        UpdateSpawnRoomState(player);
        if (clearMedicHealingTarget)
        {
            player.ClearMedicHealingTarget();
        }

        if (playRespawnSound)
        {
            RegisterWorldSoundEvent("RespawnSnd", player.X, player.Y);
        }

        return true;
    }

    private bool SpawnPlayerResolved(
        PlayerEntity player,
        PlayerTeam team,
        SpawnPoint spawn,
        bool clearMedicHealingTarget = true,
        bool playRespawnSound = false)
    {
        return SpawnPlayerResolved(player, team, spawn.X, spawn.Y, clearMedicHealingTarget, playRespawnSound);
    }

    private bool RespawnConfiguredNetworkPlayer(byte slot, PlayerEntity player)
    {
        var team = GetNetworkPlayerConfiguredTeam(slot);
        player.SetClassDefinition(GetNetworkPlayerClassDefinition(slot));
        if (!SpawnPlayerResolved(player, team, ReserveSpawn(player, team, slot), playRespawnSound: true))
        {
            return false;
        }

        SyncExperimentalGameplayLoadout(slot, player);
        return true;
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
            if (_practiceCombatDummyMode != PracticeCombatDummyMode.None)
            {
                SpawnPracticeCombatDummyResolved(playRespawnSound: true);
            }
            else
            {
                EnemyPlayer.SetClassDefinition(_enemyDummyClassDefinition);
                SpawnPlayerResolved(EnemyPlayer, _enemyDummyTeam, ReserveSpawn(EnemyPlayer, _enemyDummyTeam), playRespawnSound: true);
            }
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
                SpawnPlayerResolved(FriendlyDummy, GetNetworkPlayerConfiguredTeam(LocalPlayerSlot), friendlySpawn.X, friendlySpawn.Y, playRespawnSound: true);
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

        var spawnPool = BuildTeamSpawnSelectionPool(spawns, team);
        var spawnRooms = Level.GetRoomObjects(RoomObjectType.SpawnRoom);
        var requireSpawnRoom = spawnRooms.Count > 0;
        var useForwardObjectivePriority = IsForwardSpawnSelectionPool(spawnPool);
        var startIndex = useForwardObjectivePriority
            ? 0
            : team == PlayerTeam.Blue ? _nextBlueSpawnIndex : _nextRedSpawnIndex;
        var selectedPoolIndex = -1;
        SpawnPoint selectedSpawn = default;

        for (var offset = 0; offset < spawnPool.Count; offset += 1)
        {
            var poolIndex = (startIndex + offset) % spawnPool.Count;
            var spawn = spawnPool[poolIndex];
            if (requireSpawnRoom && !IsSpawnPointInsideSpawnRoom(spawn, spawnRooms))
            {
                continue;
            }

            if (!player.CanOccupy(Level, team, spawn.X, spawn.Y))
            {
                continue;
            }

            selectedPoolIndex = poolIndex;
            selectedSpawn = spawn;
            break;
        }

        if (selectedPoolIndex < 0)
        {
            selectedPoolIndex = startIndex % spawnPool.Count;
            selectedSpawn = spawnPool[selectedPoolIndex];
        }

        if (!useForwardObjectivePriority)
        {
            if (team == PlayerTeam.Blue)
            {
                _nextBlueSpawnIndex = selectedPoolIndex + 1;
            }
            else
            {
                _nextRedSpawnIndex = selectedPoolIndex + 1;
            }
        }

        return selectedSpawn;
    }

    private IReadOnlyList<SpawnPoint> BuildTeamSpawnSelectionPool(IReadOnlyList<SpawnPoint> spawns, PlayerTeam team)
    {
        var standardSpawns = new List<SpawnPoint>();
        var activeForwardSpawns = new List<SpawnPoint>();
        for (var index = 0; index < spawns.Count; index += 1)
        {
            var spawn = spawns[index];
            if (spawn.IsStandardSpawn)
            {
                standardSpawns.Add(spawn);
                continue;
            }

            if (IsForwardSpawnActive(spawn, team))
            {
                activeForwardSpawns.Add(spawn);
            }
        }

        if (activeForwardSpawns.Count > 0)
        {
            activeForwardSpawns.Sort(static (left, right) =>
                right.Priority.CompareTo(left.Priority));
            return activeForwardSpawns;
        }

        if (standardSpawns.Count > 0)
        {
            return standardSpawns;
        }

        return spawns;
    }

    private bool IsForwardSpawnActive(SpawnPoint spawn, PlayerTeam team)
    {
        if (!spawn.IsForwardSpawn)
        {
            return false;
        }

        if (spawn.UsesLogicSignal)
        {
            var graph = Level.LogicGraph;
            return graph.HasNodes && graph.GetOutput(spawn.LogicSignalNodeIndex);
        }

        var controlPoint = TryGetLinkedControlPointState(spawn.LinkedControlPointIndex);
        if (controlPoint is null)
        {
            return false;
        }

        return ForwardSpawnMetadata.EvaluateUseCondition(spawn.UseCondition, team, controlPoint.Team);
    }

    private ControlPointState? TryGetLinkedControlPointState(int linkedControlPointIndex)
    {
        if (_controlPoints.Count == 0)
        {
            return null;
        }

        if (linkedControlPointIndex > 0)
        {
            for (var index = 0; index < _controlPoints.Count; index += 1)
            {
                var point = _controlPoints[index];
                if (point.Index == linkedControlPointIndex)
                {
                    return point;
                }
            }

            for (var index = 0; index < _controlPoints.Count; index += 1)
            {
                var point = _controlPoints[index];
                if (ControlPointMarkerIndex.TryGetIndex(point.Marker, out var markerIndex)
                    && markerIndex == linkedControlPointIndex)
                {
                    return point;
                }
            }
        }

        return _controlPoints.Count == 1 ? _controlPoints[0] : null;
    }

    private SpawnPoint ReserveSpawn(PlayerEntity player, PlayerTeam team, byte slot)
    {
        if (_networkPlayerSpawnOverrides.TryGetValue(slot, out var spawnOverride))
        {
            if (player.CanOccupy(Level, team, spawnOverride.X, spawnOverride.Y))
            {
                return spawnOverride;
            }

            if (TryFindSafeObjectiveSpawnPosition(
                    player,
                    team,
                    spawnOverride.X,
                    spawnOverride.Y,
                    out var safeX,
                    out var safeY))
            {
                return new SpawnPoint(safeX, safeY);
            }
        }

        if (TryResolveMapManualSpawn(player, team, slot, out var mapManualSpawn))
        {
            return mapManualSpawn;
        }

        return ReserveSpawn(player, team);
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
        return TryFindSafeObjectiveSpawnPosition(player, team, marker.CenterX, marker.CenterY, out spawnX, out spawnY);
    }

    private bool TryFindSafeObjectiveSpawnPosition(PlayerEntity player, PlayerTeam team, float objectiveX, float objectiveY, out float spawnX, out float spawnY)
    {
        var horizontalOffsets = new[] { 0f, -16f, 16f, -32f, 32f, -48f, 48f, -64f, 64f };
        const float verticalStartOffset = -96f;
        const float verticalEndOffset = 96f;
        const float verticalStep = 4f;

        for (var horizontalIndex = 0; horizontalIndex < horizontalOffsets.Length; horizontalIndex += 1)
        {
            var candidateX = objectiveX + horizontalOffsets[horizontalIndex];
            float? nearestOpenCandidateY = null;
            for (var candidateY = objectiveY + verticalStartOffset; candidateY <= objectiveY + verticalEndOffset; candidateY += verticalStep)
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

    internal IReadOnlyList<SpawnPoint> CombatTestGetTeamSpawnSelectionPool(PlayerTeam team)
    {
        var spawns = team == PlayerTeam.Blue ? Level.BlueSpawns : Level.RedSpawns;
        return BuildTeamSpawnSelectionPool(spawns, team);
    }

    internal SpawnPoint CombatTestReserveTeamSpawn(PlayerEntity player, PlayerTeam team)
    {
        return ReserveSpawn(player, team);
    }

    private static bool IsForwardSpawnSelectionPool(IReadOnlyList<SpawnPoint> spawnPool)
    {
        if (spawnPool.Count == 0)
        {
            return false;
        }

        for (var index = 0; index < spawnPool.Count; index += 1)
        {
            if (!spawnPool[index].IsForwardSpawn)
            {
                return false;
            }
        }

        return true;
    }

    internal void CombatTestSetControlPointOwner(int controlPointIndex, PlayerTeam? team)
    {
        for (var index = 0; index < _controlPoints.Count; index += 1)
        {
            if (_controlPoints[index].Index == controlPointIndex)
            {
                _controlPoints[index].Team = team;
                return;
            }
        }
    }
}
