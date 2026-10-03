namespace OpenGarrison.Core;

internal sealed partial class SpawnSystem
{
    internal bool TryMoveLocalPlayerToControlPointSpawn()
    {
        return TryMoveNetworkPlayerToControlPointSpawn(SimulationConstants.LocalPlayerSlot);
    }

    internal bool TryMoveNetworkPlayerToControlPointSpawn(byte slot)
    {
        if (_host.NetworkPlayerRules.IsNetworkPlayerAwaitingJoin(slot)
            || !_host.NetworkPlayerRules.TryGetNetworkPlayer(slot, out var player)
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

    internal bool TryMoveLocalPlayerToIntelSpawn()
    {
        return TryMoveNetworkPlayerToIntelSpawn(SimulationConstants.LocalPlayerSlot);
    }

    internal bool TryMoveNetworkPlayerToIntelSpawn(byte slot)
    {
        if (_host.NetworkPlayerRules.IsNetworkPlayerAwaitingJoin(slot)
            || !_host.NetworkPlayerRules.TryGetNetworkPlayer(slot, out var player)
            || !player.IsAlive)
        {
            return false;
        }

        var ownIntelBase = _host.Level.GetIntelBase(player.Team);
        if (!ownIntelBase.HasValue
            || !TryFindSafeObjectiveSpawnPosition(player, player.Team, ownIntelBase.Value.X, ownIntelBase.Value.Y, out var spawnX, out var spawnY))
        {
            return false;
        }

        SpawnPlayerResolved(player, player.Team, spawnX, spawnY, clearMedicHealingTarget: false);
        return true;
    }

    internal bool TryMoveNetworkPlayerToLastToDieObjectiveSpawn(byte slot)
    {
        return _host.MatchRules.Mode == GameModeKind.CaptureTheFlag
            ? TryMoveNetworkPlayerToIntelSpawn(slot)
            : TryMoveNetworkPlayerToControlPointSpawn(slot);
    }

    internal bool TryMoveNetworkPlayerToLastToDieEnemySpawn(byte slot, PlayerTeam spawnSide)
        => TryConfigureNetworkPlayerLastToDieEnemySpawn(
            slot,
            spawnSide,
            repositionAlivePlayer: true);

    internal bool TryConfigureNetworkPlayerLastToDieEnemySpawn(
        byte slot,
        PlayerTeam spawnSide,
        bool repositionAlivePlayer)
    {
        if (spawnSide is not (PlayerTeam.Red or PlayerTeam.Blue)
            || _host.NetworkPlayerRules.IsNetworkPlayerAwaitingJoin(slot)
            || !_host.NetworkPlayerRules.TryGetNetworkPlayer(slot, out var player)
            || repositionAlivePlayer && !player.IsAlive)
        {
            return false;
        }

        var sideSpawns = spawnSide == PlayerTeam.Red ? _host.Level.RedSpawns : _host.Level.BlueSpawns;
        if (sideSpawns.Count == 0)
        {
            return false;
        }

        if (spawnSide == player.Team)
        {
            if (!_host.NetworkPlayerRules.TryClearNetworkPlayerSpawnOverride(slot) || !repositionAlivePlayer)
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
            // A failed reassignment must not retain an earlier opposing-side
            // override when the enemy next respawns.
            _host.NetworkPlayerRules.TryClearNetworkPlayerSpawnOverride(slot);
            return false;
        }

        if (!_host.NetworkPlayerRules.TrySetNetworkPlayerSpawnOverride(slot, spawnX, spawnY))
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
        var centerX = _host.Level.Bounds.Width * 0.5f;
        var direction = MathF.Sign(centerX - sourceSpawn.X);
        if (direction == 0f)
        {
            direction = player.Team == PlayerTeam.Blue ? -1 : 1;
        }

        // Spawn-room markers can stop short of the actual exit doors (stock
        // Harvest does this). A clear, grounded point in that gap is still
        // trapped behind a gate that blocks this enemy's team. Start on the
        // public side of the whole opposing gate envelope, including exits
        // on another floor, rather than accepting the first unmarked tile.
        var searchStartX = sourceSpawn.X;
        foreach (var gate in _host.Level.GetBlockingTeamGates(player.Team, carryingIntel: false))
        {
            if (gate.Type != RoomObjectType.TeamGate || gate.Team == player.Team)
            {
                continue;
            }

            if (direction > 0 && gate.Right >= sourceSpawn.X && gate.Left < centerX)
            {
                searchStartX = Math.Max(searchStartX, gate.Right - player.CollisionLeftOffset + 1f);
            }
            else if (direction < 0 && gate.Left <= sourceSpawn.X && gate.Right > centerX)
            {
                searchStartX = Math.Min(searchStartX, gate.Left - player.CollisionRightOffset - 1f);
            }
        }

        var spawnRooms = _host.Level.GetRoomObjects(RoomObjectType.SpawnRoom);
        (float X, float Y)? firstOpenPosition = null;
        var maximumSteps = Math.Max(1, (int)MathF.Ceiling(_host.Level.Bounds.Width / step));
        var previousX = searchStartX;
        for (var stepIndex = 0; stepIndex <= maximumSteps; stepIndex += 1)
        {
            var candidateX = Math.Clamp(
                searchStartX + (direction * stepIndex * step),
                -player.CollisionLeftOffset,
                _host.Level.Bounds.Width - player.CollisionRightOffset);
            if (stepIndex > 0 && candidateX == previousX)
            {
                break;
            }

            previousX = candidateX;
            if (IntersectsAnySpawnRoom(player, candidateX, sourceSpawn.Y, spawnRooms)
                || !player.CanOccupy(_host.Level, player.Team, candidateX, sourceSpawn.Y))
            {
                continue;
            }

            firstOpenPosition ??= (candidateX, sourceSpawn.Y);
            if (!player.CanOccupy(_host.Level, player.Team, candidateX, sourceSpawn.Y + 1f))
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

    internal bool SpawnPlayerResolved(
        PlayerEntity player,
        PlayerTeam team,
        float x,
        float y,
        bool clearMedicHealingTarget = true,
        bool playRespawnSound = false)
    {
        if (_host.Decisions.ShouldCancelSpawn(player, team, x, y))
        {
            return false;
        }

        if (clearMedicHealingTarget)
        {
            _host.LastToDieRules.ClearLastToDieStatusEffectsForTarget(player.Id);
        }

        foreach (var otherPlayer in _host.EnumerateSimulatedPlayers())
        {
            // _host.EnemyPlayerEnabled defaults to true before the constructor has
            // assigned _host.EnemyPlayer, so the initial local spawn can encounter
            // that not-yet-materialized enumeration entry.
            if (otherPlayer is not null && otherPlayer.MedicHealTargetId == player.Id)
            {
                otherPlayer.ClearMedicHealingTarget();
            }
        }

        player.Spawn(team, x, y);
        player.ResolveBlockingOverlap(_host.Level, team);
        _host.RoomEffects.UpdateSpawnRoomState(player);
        if (clearMedicHealingTarget)
        {
            player.ClearMedicHealingTarget();
        }

        if (playRespawnSound)
        {
            _host.WorldEffects.RegisterWorldSoundEvent("RespawnSnd", player.X, player.Y);
        }

        return true;
    }

    internal bool SpawnPlayerResolved(
        PlayerEntity player,
        PlayerTeam team,
        SpawnPoint spawn,
        bool clearMedicHealingTarget = true,
        bool playRespawnSound = false)
    {
        return SpawnPlayerResolved(player, team, spawn.X, spawn.Y, clearMedicHealingTarget, playRespawnSound);
    }

    internal bool RespawnConfiguredNetworkPlayer(byte slot, PlayerEntity player)
    {
        var team = _host.NetworkPlayerRules.GetNetworkPlayerConfiguredTeam(slot);
        player.SetClassDefinition(_host.NetworkPlayerRules.GetNetworkPlayerClassDefinition(slot));
        if (!SpawnPlayerResolved(player, team, ReserveSpawn(player, team, slot), playRespawnSound: true))
        {
            return false;
        }

        _host.ExperimentalRules.SyncExperimentalGameplayLoadout(slot, player);
        return true;
    }

    internal void RespawnPlayersForNewRound()
    {
        for (var index = 0; index < SimulationConstants.NetworkPlayerSlots.Count; index += 1)
        {
            var slot = SimulationConstants.NetworkPlayerSlots[index];
            if (!_host.NetworkPlayerRules.TryGetNetworkPlayer(slot, out var player))
            {
                continue;
            }

            player.SetClassDefinition(_host.NetworkPlayerRules.GetNetworkPlayerClassDefinition(slot));
            if (_host.NetworkPlayerRules.IsNetworkPlayerAwaitingJoin(slot))
            {
                player.ClearMedicHealingTarget();
                player.Kill();
                continue;
            }

            RespawnConfiguredNetworkPlayer(slot, player);
        }

        if (_host.EnemyPlayerEnabled)
        {
            if (_host.DummyState.CombatMode != PracticeCombatDummyMode.None)
            {
                _host.PracticeDummies.SpawnPracticeCombatDummyResolved(playRespawnSound: true);
            }
            else
            {
                _host.EnemyPlayer.SetClassDefinition(_host.DummyState.EnemyClassDefinition);
                SpawnPlayerResolved(_host.EnemyPlayer, _host.DummyState.EnemyTeam, ReserveSpawn(_host.EnemyPlayer, _host.DummyState.EnemyTeam), playRespawnSound: true);
            }
            _host.DummyState.EnemyRespawnTicks = 0;
        }
        else
        {
            _host.EnemyPlayer.Kill();
            _host.DummyState.EnemyRespawnTicks = 0;
        }

        if (_host.FriendlyDummyEnabled)
        {
            _host.FriendlyDummy.SetClassDefinition(_host.LocalState.FriendlyDummyClassDefinition);
            if (_host.NetworkPlayerRules.IsNetworkPlayerAwaitingJoin(SimulationConstants.LocalPlayerSlot))
            {
                _host.FriendlyDummy.Kill();
            }
            else
            {
                var friendlySpawn = _host.PracticeDummies.FindFriendlyDummySpawnNearLocalPlayer();
                SpawnPlayerResolved(_host.FriendlyDummy, _host.NetworkPlayerRules.GetNetworkPlayerConfiguredTeam(SimulationConstants.LocalPlayerSlot), friendlySpawn.X, friendlySpawn.Y, playRespawnSound: true);
            }
        }
        else
        {
            _host.FriendlyDummy.Kill();
        }
    }

    internal SpawnPoint ReserveSpawn(PlayerEntity player, PlayerTeam team)
    {
        var spawns = team == PlayerTeam.Blue ? _host.Level.BlueSpawns : _host.Level.RedSpawns;
        if (spawns.Count == 0)
        {
            return _host.Level.LocalSpawn;
        }

        var spawnPool = BuildTeamSpawnSelectionPool(spawns, team);
        var spawnRooms = _host.Level.GetRoomObjects(RoomObjectType.SpawnRoom);
        var requireSpawnRoom = spawnRooms.Count > 0;
        var startIndex = team == PlayerTeam.Blue ? _host.Lifecycle.NextBlueSpawnIndex : _host.Lifecycle.NextRedSpawnIndex;
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

            if (!player.CanOccupy(_host.Level, team, spawn.X, spawn.Y))
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

        if (team == PlayerTeam.Blue)
            _host.Lifecycle.NextBlueSpawnIndex = selectedPoolIndex + 1;
        else
            _host.Lifecycle.NextRedSpawnIndex = selectedPoolIndex + 1;

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
            var highestPriority = activeForwardSpawns.Max(spawn => spawn.Priority);
            return OrderSpawnTier(activeForwardSpawns.Where(spawn => spawn.Priority == highestPriority));
        }

        if (standardSpawns.Count > 0)
            return OrderSpawnTier(standardSpawns);

        // Legacy maps sometimes supply numbered spawns only, including neutral
        // starts. In that case use the home tier, never an arbitrary forward tier.
        var homePriority = spawns.Min(spawn => spawn.LegacySpawnSlot > 0 ? spawn.LegacySpawnSlot : spawn.Priority);
        return OrderSpawnTier(spawns.Where(spawn =>
            (spawn.LegacySpawnSlot > 0 ? spawn.LegacySpawnSlot : spawn.Priority) == homePriority));
    }

    private static SpawnPoint[] OrderSpawnTier(IEnumerable<SpawnPoint> spawns)
        => spawns.OrderBy(spawn => spawn.X).ThenBy(spawn => spawn.Y).ToArray();

    private bool IsForwardSpawnActive(SpawnPoint spawn, PlayerTeam team)
    {
        if (!spawn.IsForwardSpawn)
        {
            return false;
        }

        if (spawn.UsesLogicSignal)
        {
            var graph = _host.Level.LogicGraph;
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
        if (_host.Objectives.ControlPoints.Points.Count == 0)
        {
            return null;
        }

        if (linkedControlPointIndex > 0)
        {
            for (var index = 0; index < _host.Objectives.ControlPoints.Points.Count; index += 1)
            {
                var point = _host.Objectives.ControlPoints.Points[index];
                if (point.Index == linkedControlPointIndex)
                {
                    return point;
                }
            }

            for (var index = 0; index < _host.Objectives.ControlPoints.Points.Count; index += 1)
            {
                var point = _host.Objectives.ControlPoints.Points[index];
                if (ControlPointMarkerIndex.TryGetIndex(point.Marker, out var markerIndex)
                    && markerIndex == linkedControlPointIndex)
                {
                    return point;
                }
            }
        }

        return _host.Objectives.ControlPoints.Points.Count == 1 ? _host.Objectives.ControlPoints.Points[0] : null;
    }

    internal SpawnPoint ReserveSpawn(PlayerEntity player, PlayerTeam team, byte slot)
    {
        if (_host.PlayerRegistry.SpawnOverrides.TryGetValue(slot, out var spawnOverride))
        {
            if (player.CanOccupy(_host.Level, team, spawnOverride.X, spawnOverride.Y))
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

        if (_host.ClassRules.TryResolveMapManualSpawn(player, team, slot, out var mapManualSpawn))
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
        if (_host.MatchRules.Mode == GameModeKind.KingOfTheHill)
        {
            return _host.ObjectiveRules.GetSingleKothPoint()?.Marker
                ?? _host.Level.GetFirstRoomObject(RoomObjectType.ControlPoint);
        }

        if (_host.MatchRules.Mode == GameModeKind.DoubleKingOfTheHill)
        {
            return _host.ObjectiveRules.GetDualKothPoint(team)?.Marker
                ?? _host.Level.GetFirstRoomObject(RoomObjectType.ControlPoint);
        }

        return _host.Level.GetFirstRoomObject(RoomObjectType.ControlPoint)
            ?? _host.Level.GetFirstRoomObject(RoomObjectType.ArenaControlPoint);
    }

    private bool TryFindSafeControlPointSpawnPosition(PlayerEntity player, PlayerTeam team, RoomObjectMarker marker, out float spawnX, out float spawnY)
    {
        return TryFindSafeObjectiveSpawnPosition(player, team, marker.CenterX, marker.CenterY, out spawnX, out spawnY);
    }

    internal bool TryFindSafeObjectiveSpawnPosition(PlayerEntity player, PlayerTeam team, float objectiveX, float objectiveY, out float spawnX, out float spawnY)
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
                if (!player.CanOccupy(_host.Level, team, candidateX, candidateY))
                {
                    continue;
                }

                if (!player.CanOccupy(_host.Level, team, candidateX, candidateY + 1f))
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
        var spawns = team == PlayerTeam.Blue ? _host.Level.BlueSpawns : _host.Level.RedSpawns;
        return BuildTeamSpawnSelectionPool(spawns, team);
    }

    internal SpawnPoint CombatTestReserveTeamSpawn(PlayerEntity player, PlayerTeam team)
    {
        return ReserveSpawn(player, team);
    }


    internal void CombatTestSetControlPointOwner(int controlPointIndex, PlayerTeam? team)
    {
        for (var index = 0; index < _host.Objectives.ControlPoints.Points.Count; index += 1)
        {
            if (_host.Objectives.ControlPoints.Points[index].Index == controlPointIndex)
            {
                _host.Objectives.ControlPoints.Points[index].Team = team;
                return;
            }
        }
    }
}
