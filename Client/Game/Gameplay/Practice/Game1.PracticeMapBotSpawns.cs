#nullable enable

using OpenGarrison.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenGarrison.Client;

public partial class Game1
{
    private SimpleLevel? _practiceMapBotSpawnLevel;
    private bool[] _practiceMapBotSpawnPreviousTriggerStates = [];
    private bool[] _practiceMapBotSpawnPreviousLocalIntersections = [];
    private int[] _practiceMapBotSpawnLocalFireCounts = [];

    private void AdvancePracticeMapBotSpawns()
    {
        if (!IsPracticeSessionActive)
        {
            ResetPracticeMapBotSpawnState();
            return;
        }

        var botSpawns = _world.Level.BotSpawns;
        if (botSpawns.Count == 0)
        {
            ResetPracticeMapBotSpawnStateIfLevelChanged();
            return;
        }

        EnsurePracticeMapBotSpawnState(botSpawns.Count);
        var graph = _world.Level.LogicGraph;
        for (var index = 0; index < botSpawns.Count; index += 1)
        {
            var marker = botSpawns[index];
            var graphOutput = marker.UsesTrigger && graph.GetOutput(marker.TriggerNodeIndex);
            var localIntersectsTrigger = IsLocalPlayerMatchingPracticeMapBotTrigger(marker);
            var directLocalTrigger = IsPracticeMapBotLocalPlayerTriggerRaised(
                marker,
                localIntersectsTrigger,
                _practiceMapBotSpawnPreviousLocalIntersections[index]);
            if (directLocalTrigger && !IsPracticeMapBotLocalPlayerTriggerAllowed(marker, index))
            {
                directLocalTrigger = false;
            }

            var current = graphOutput || directLocalTrigger;
            if (_garrisonBuilderQuickTestActive
                && localIntersectsTrigger != _practiceMapBotSpawnPreviousLocalIntersections[index])
            {
                AddConsoleLine(
                    $"builder bot diag: local {(localIntersectsTrigger ? "entered" : "left")} botSpawn[{index}] trigger zone " +
                    $"ref={marker.TriggerRef} node={marker.TriggerNodeIndex} output={graphOutput} " +
                    $"alive={_world.LocalPlayer.IsAlive} team={_world.LocalPlayer.Team} class={_world.LocalPlayer.ClassId} " +
                    $"awaitingJoin={_world.LocalPlayerAwaitingJoin}");
            }

            if (_garrisonBuilderQuickTestActive && directLocalTrigger && !graphOutput)
            {
                AddConsoleLine(
                    $"builder bot diag: botSpawn[{index}] direct local trigger raised despite graph output false " +
                    $"ref={marker.TriggerRef} node={marker.TriggerNodeIndex}");
            }

            if (_garrisonBuilderQuickTestActive && current != _practiceMapBotSpawnPreviousTriggerStates[index])
            {
                AddConsoleLine(
                    $"builder bot diag: botSpawn[{index}] trigger output {(_practiceMapBotSpawnPreviousTriggerStates[index] ? "true" : "false")} -> {(current ? "true" : "false")} " +
                    $"ref={marker.TriggerRef} node={marker.TriggerNodeIndex}");
            }

            if (current && !_practiceMapBotSpawnPreviousTriggerStates[index])
            {
                if (directLocalTrigger && !graphOutput)
                {
                    RecordPracticeMapBotLocalPlayerTriggerFire(marker, index);
                }

                AddConsoleLine(
                    $"builder bot diag: botSpawn[{index}] spawn requested team={marker.Team} class={marker.ClassId?.ToString() ?? "Random"} type={marker.Kind} " +
                    $"nameMode={marker.NameMode} name=\"{marker.Name}\" forceNameplate={marker.ForceNameplate} forceHp={marker.ForceHealthBar} " +
                    $"deathNode={marker.DeathTriggerNodeIndex} " +
                    $"at=({marker.X:0.##},{marker.Y:0.##})");
                TrySpawnPracticeMapBot(marker);
            }

            _practiceMapBotSpawnPreviousTriggerStates[index] = current;
            _practiceMapBotSpawnPreviousLocalIntersections[index] = localIntersectsTrigger;
        }
    }

    private void ResetPracticeMapBotSpawnState()
    {
        _practiceMapBotSpawnLevel = _world.Level;
        var botSpawns = _world.Level.BotSpawns;
        _practiceMapBotSpawnPreviousTriggerStates = botSpawns.Count == 0
            ? []
            : new bool[botSpawns.Count];
        _practiceMapBotSpawnPreviousLocalIntersections = botSpawns.Count == 0
            ? []
            : new bool[botSpawns.Count];
        _practiceMapBotSpawnLocalFireCounts = botSpawns.Count == 0
            ? []
            : new int[botSpawns.Count];
    }

    private void ResetPracticeMapBotSpawnStateIfLevelChanged()
    {
        if (!ReferenceEquals(_practiceMapBotSpawnLevel, _world.Level))
        {
            ResetPracticeMapBotSpawnState();
        }
    }

    private void EnsurePracticeMapBotSpawnState(int count)
    {
        if (ReferenceEquals(_practiceMapBotSpawnLevel, _world.Level)
            && _practiceMapBotSpawnPreviousTriggerStates.Length == count
            && _practiceMapBotSpawnPreviousLocalIntersections.Length == count
            && _practiceMapBotSpawnLocalFireCounts.Length == count)
        {
            return;
        }

        ResetPracticeMapBotSpawnState();
    }

    private bool IsPracticeMapBotLocalPlayerTriggerAllowed(BotSpawnMarker marker, int index)
    {
        if (index < 0 || index >= _practiceMapBotSpawnLocalFireCounts.Length)
        {
            return false;
        }

        var maxFires = GetMapLogicPlayerTriggerMaxFires(marker.TriggerNodeIndex);
        return maxFires <= 0 || _practiceMapBotSpawnLocalFireCounts[index] < maxFires;
    }

    private void RecordPracticeMapBotLocalPlayerTriggerFire(BotSpawnMarker marker, int index)
    {
        if (index < 0 || index >= _practiceMapBotSpawnLocalFireCounts.Length)
        {
            return;
        }

        var maxFires = GetMapLogicPlayerTriggerMaxFires(marker.TriggerNodeIndex);
        if (maxFires <= 0)
        {
            return;
        }

        _practiceMapBotSpawnLocalFireCounts[index] = Math.Min(maxFires, _practiceMapBotSpawnLocalFireCounts[index] + 1);
    }

    private void LogPracticeMapBotSpawnDiagnostics(string label)
    {
        var botSpawns = _world.Level.BotSpawns;
        var graph = _world.Level.LogicGraph;
        var playerTriggerZones = _world.Level.RoomObjects.Count(static marker => marker.Type == RoomObjectType.PlayerTriggerZone);
        AddConsoleLine(
            $"builder bot diag: {label} level={_world.Level.Name} botSpawns={botSpawns.Count} " +
            $"logicNodes={graph.Nodes.Count} playerTriggerZones={playerTriggerZones}");
        AddConsoleLine(
            $"builder bot diag: local player alive={_world.LocalPlayer.IsAlive} team={_world.LocalPlayer.Team} " +
            $"class={_world.LocalPlayer.ClassId} awaitingJoin={_world.LocalPlayerAwaitingJoin} " +
            $"pos=({_world.LocalPlayer.X:0.##},{_world.LocalPlayer.Y:0.##})");

        for (var index = 0; index < botSpawns.Count; index += 1)
        {
            var marker = botSpawns[index];
            var output = marker.UsesTrigger && graph.GetOutput(marker.TriggerNodeIndex);
            var localIntersects = IsLocalPlayerMatchingPracticeMapBotTrigger(marker);
            AddConsoleLine(
                $"builder bot diag: botSpawn[{index}] ref={marker.TriggerRef} node={marker.TriggerNodeIndex} output={output} " +
                $"localIntersects={localIntersects} team={marker.Team} class={marker.ClassId?.ToString() ?? "Random"} type={marker.Kind} " +
                $"nameMode={marker.NameMode} name=\"{marker.Name}\" forceNameplate={marker.ForceNameplate} forceHp={marker.ForceHealthBar} " +
                $"deathNode={marker.DeathTriggerNodeIndex} " +
                $"pos=({marker.X:0.##},{marker.Y:0.##})");
        }
    }

    private bool IsPracticeMapBotLocalPlayerTriggerRaised(
        BotSpawnMarker marker,
        bool localIntersectsTrigger,
        bool previousLocalIntersectsTrigger)
    {
        if (!marker.UsesTrigger
            || marker.TriggerNodeIndex < 0
            || marker.TriggerNodeIndex >= _world.Level.LogicGraph.Nodes.Count)
        {
            return false;
        }

        var node = _world.Level.LogicGraph.Nodes[marker.TriggerNodeIndex];
        if (node.Kind != MapLogicNodeKind.PlayerTrigger)
        {
            return false;
        }

        return node.SignalMode == MapLogicSignalMode.Latch
            ? localIntersectsTrigger
            : node.PlayerDetectMode == MapLogicPlayerDetectMode.PlayerExit
                ? previousLocalIntersectsTrigger && !localIntersectsTrigger
                : localIntersectsTrigger && !previousLocalIntersectsTrigger;
    }

    private bool IsLocalPlayerMatchingPracticeMapBotTrigger(BotSpawnMarker marker)
    {
        if (!marker.UsesTrigger
            || marker.TriggerNodeIndex < 0
            || marker.TriggerNodeIndex >= _world.Level.LogicGraph.Nodes.Count)
        {
            return false;
        }

        var node = _world.Level.LogicGraph.Nodes[marker.TriggerNodeIndex];
        if (node.Kind != MapLogicNodeKind.PlayerTrigger)
        {
            return false;
        }

        if (!_world.LocalPlayer.IsAlive
            || !PlayerTriggerMetadata.AllowsTeam(node.PlayerTriggerTeamFilter, _world.LocalPlayer.Team)
            || (node.PlayerTriggerIntelCarriersOnly && !_world.LocalPlayer.IsCarryingIntel))
        {
            return false;
        }

        if (node.PlayerTriggerRoomObjectIndex >= 0
            && IsLocalPlayerIntersectingPlayerTriggerZone(node.PlayerTriggerRoomObjectIndex))
        {
            return true;
        }

        for (var index = 0; index < node.PlayerTriggerZoneRoomObjectIndices.Length; index += 1)
        {
            if (IsLocalPlayerIntersectingPlayerTriggerZone(node.PlayerTriggerZoneRoomObjectIndices[index]))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsLocalPlayerIntersectingPlayerTriggerZone(int roomObjectIndex)
    {
        if (roomObjectIndex < 0 || roomObjectIndex >= _world.Level.RoomObjects.Count)
        {
            return false;
        }

        var zone = _world.Level.RoomObjects[roomObjectIndex];
        return zone.Type == RoomObjectType.PlayerTriggerZone
            && _world.LocalPlayer.IntersectsMarker(zone.CenterX, zone.CenterY, zone.Width, zone.Height);
    }

    private bool TrySpawnPracticeMapBot(BotSpawnMarker marker)
    {
        if (!TryFindAvailablePracticeMapBotSlot(out var slot))
        {
            if (_garrisonBuilderQuickTestActive)
            {
                AddConsoleLine("builder bot diag: spawn failed - no available practice bot slot");
            }

            return false;
        }

        var teamBotCount = _practiceBotSlots.Values.Count(state => state.Team == marker.Team);
        var playerClass = ResolvePracticeMapBotClass(marker.Team, teamBotCount, marker.ClassId);
        var isDummy = marker.Kind == BotSpawnKind.Dummy;
        var displayName = ResolvePracticeMapBotDisplayName(marker, slot, teamBotCount, isDummy);

        _world.TrySetNetworkPlayerSpawnOverride(slot, marker.X, marker.Y);
        _world.SetNetworkPlayerMapSpawnClassBehaviorBypass(slot, true);
        try
        {
            if (!_world.TryPrepareNetworkPlayerJoin(slot))
            {
                return FailPracticeMapBotSpawn(slot, displayName, "prepare join failed");
            }

            if (!_world.TrySetNetworkPlayerName(slot, displayName))
            {
                return FailPracticeMapBotSpawn(slot, displayName, "set name failed");
            }

            if (!_world.TrySetNetworkPlayerTeam(slot, marker.Team))
            {
                return FailPracticeMapBotSpawn(slot, displayName, "set team failed");
            }

            if (!_world.TryApplyNetworkPlayerClassSelection(slot, playerClass))
            {
                return FailPracticeMapBotSpawn(slot, displayName, $"class selection failed class={playerClass}");
            }

            if (_world.TryGetNetworkPlayer(slot, out var spawnedBot))
            {
                ApplyPracticeMapBotReplicatedStates(spawnedBot, marker);
            }

            _practiceBotDisplayNamePool.Reserve(displayName);
            _practiceBotSlots[slot] = new PracticeBotSlotState(
                slot,
                marker.Team,
                playerClass,
                displayName,
                isDummy,
                isMapSpawned: true,
                marker.Respawn,
                marker.RespawnMode,
                marker.X,
                marker.Y);
            _practiceBotInputCache.Remove(slot);
            _practiceBotInputCacheAgeTicks.Remove(slot);
            if (marker.Respawn && marker.RespawnMode == BotSpawnRespawnMode.Node)
            {
                _world.TrySetNetworkPlayerSpawnOverride(slot, marker.X, marker.Y);
            }

            if (_garrisonBuilderQuickTestActive && _world.TryGetNetworkPlayer(slot, out var bot))
            {
                AddConsoleLine(
                    $"builder bot diag: spawn succeeded slot={slot} name=\"{bot.DisplayName}\" team={bot.Team} class={bot.ClassId} alive={bot.IsAlive} " +
                    $"forceNameplate={marker.ForceNameplate} forceHp={marker.ForceHealthBar} " +
                    $"pos=({bot.X:0.##},{bot.Y:0.##})");
            }

            return true;
        }
        finally
        {
            if (!marker.Respawn || marker.RespawnMode != BotSpawnRespawnMode.Node)
            {
                _world.TryClearNetworkPlayerSpawnOverride(slot);
            }
        }
    }

    private string ResolvePracticeMapBotDisplayName(
        BotSpawnMarker marker,
        byte slot,
        int teamBotCount,
        bool isDummy)
    {
        if (!string.IsNullOrWhiteSpace(marker.Name))
        {
            return marker.Name.Trim();
        }

        return isDummy
            ? $"{marker.Team} Dummy"
            : _practiceBotDisplayNamePool.GetOrAssign(slot, marker.Team, teamBotCount + 1);
    }

    private static void ApplyPracticeMapBotReplicatedStates(PlayerEntity bot, BotSpawnMarker marker)
    {
        SetPracticeMapBotVisualOverride(bot, BotSpawnMetadata.ForceNameplateReplicatedStateKey, marker.ForceNameplate);
        SetPracticeMapBotVisualOverride(bot, BotSpawnMetadata.ForceHealthBarReplicatedStateKey, marker.ForceHealthBar);
        if (marker.UsesDeathTrigger)
        {
            bot.SetReplicatedStateInt(
                BotSpawnMetadata.VisualReplicatedStateOwnerId,
                BotSpawnMetadata.DeathTriggerNodeReplicatedStateKey,
                marker.DeathTriggerNodeIndex);
        }
        else
        {
            bot.ClearReplicatedState(
                BotSpawnMetadata.VisualReplicatedStateOwnerId,
                BotSpawnMetadata.DeathTriggerNodeReplicatedStateKey);
        }
    }

    private static void SetPracticeMapBotVisualOverride(PlayerEntity bot, string key, bool enabled)
    {
        if (enabled)
        {
            bot.SetReplicatedStateBool(BotSpawnMetadata.VisualReplicatedStateOwnerId, key, true);
            return;
        }

        bot.ClearReplicatedState(BotSpawnMetadata.VisualReplicatedStateOwnerId, key);
    }

    private void AdvancePracticeMapBotRespawnPolicies()
    {
        if (_practiceBotSlots.Count == 0)
        {
            return;
        }

        var slotsToRelease = new List<byte>();
        foreach (var entry in _practiceBotSlots)
        {
            var state = entry.Value;
            if (!state.IsMapSpawned
                || state.Respawn
                || !_world.TryGetNetworkPlayer(entry.Key, out var bot)
                || bot.IsAlive)
            {
                continue;
            }

            slotsToRelease.Add(entry.Key);
        }

        for (var index = 0; index < slotsToRelease.Count; index += 1)
        {
            var slot = slotsToRelease[index];
            _world.TryClearNetworkPlayerSpawnOverride(slot);
            _world.TryReleaseNetworkPlayerSlot(slot);
            _practiceBotSlots.Remove(slot);
            _practiceBotDisplayNamePool.ReleaseSlot(slot);
            _practiceBotInputCache.Remove(slot);
            _practiceBotInputCacheAgeTicks.Remove(slot);
            if (_garrisonBuilderQuickTestActive)
            {
                AddConsoleLine($"builder bot diag: released non-respawning map bot slot={slot}");
            }
        }
    }

    private bool FailPracticeMapBotSpawn(byte slot, string displayName, string reason)
    {
        _world.TryReleaseNetworkPlayerSlot(slot);
        _practiceBotDisplayNamePool.ReleaseSlot(slot);
        if (_garrisonBuilderQuickTestActive)
        {
            AddConsoleLine($"builder bot diag: spawn failed slot={slot} reason={reason}");
        }

        return false;
    }

    private bool TryFindAvailablePracticeMapBotSlot(out byte slot)
    {
        for (var slotNumber = SimulationWorld.LocalPlayerSlot + 1; slotNumber <= SimulationWorld.MaxPlayableNetworkPlayers; slotNumber += 1)
        {
            slot = (byte)slotNumber;
            if (!_practiceBotSlots.ContainsKey(slot))
            {
                return true;
            }
        }

        slot = 0;
        return false;
    }

    private PlayerClass ResolvePracticeMapBotClass(PlayerTeam team, int teamClassIndex, PlayerClass? requestedClass)
    {
        if (requestedClass.HasValue)
        {
            return requestedClass.Value;
        }

        var cycle = GetEligiblePracticeBotClassCycle();
        var classOffset = team == PlayerTeam.Red ? 3 : 0;
        return cycle[(teamClassIndex + classOffset) % cycle.Length];
    }
}
