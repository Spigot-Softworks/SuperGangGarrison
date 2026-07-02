#nullable enable

using OpenGarrison.Core;
using System.Collections.Generic;

namespace OpenGarrison.Client;

public partial class Game1
{
    private SimpleLevel? _mapBotDeathLogicPulseLevel;
    private readonly Dictionary<byte, bool> _mapBotDeathLogicPulseAliveBySlot = new();
    private readonly HashSet<byte> _mapBotDeathLogicPulseSeenSlots = [];
    private readonly List<byte> _mapBotDeathLogicPulseStaleSlots = [];

    private void AdvanceMapBotDeathLogicPulses()
    {
        var level = _world.Level;
        if (!ReferenceEquals(_mapBotDeathLogicPulseLevel, level))
        {
            _mapBotDeathLogicPulseLevel = level;
            _mapBotDeathLogicPulseAliveBySlot.Clear();
        }

        var graph = level.LogicGraph;
        if (!graph.HasNodes)
        {
            _mapBotDeathLogicPulseAliveBySlot.Clear();
            return;
        }

        _mapBotDeathLogicPulseSeenSlots.Clear();
        foreach (var (slot, player) in _world.EnumerateReplicatedNetworkPlayers())
        {
            if (!player.TryGetReplicatedStateInt(
                    BotSpawnMetadata.VisualReplicatedStateOwnerId,
                    BotSpawnMetadata.DeathTriggerNodeReplicatedStateKey,
                    out var nodeIndex)
                || nodeIndex < 0
                || nodeIndex >= graph.Nodes.Count)
            {
                _mapBotDeathLogicPulseAliveBySlot.Remove(slot);
                continue;
            }

            _mapBotDeathLogicPulseSeenSlots.Add(slot);
            var alive = player.IsAlive;
            if (_mapBotDeathLogicPulseAliveBySlot.TryGetValue(slot, out var wasAlive)
                && wasAlive
                && !alive
                && _world.PulseMapLogicNode(nodeIndex)
                && _garrisonBuilderQuickTestActive)
            {
                AddConsoleLine($"builder bot diag: death pulse slot={slot} node={nodeIndex}");
            }

            _mapBotDeathLogicPulseAliveBySlot[slot] = alive;
        }

        _mapBotDeathLogicPulseStaleSlots.Clear();
        foreach (var slot in _mapBotDeathLogicPulseAliveBySlot.Keys)
        {
            if (!_mapBotDeathLogicPulseSeenSlots.Contains(slot))
            {
                _mapBotDeathLogicPulseStaleSlots.Add(slot);
            }
        }

        for (var index = 0; index < _mapBotDeathLogicPulseStaleSlots.Count; index += 1)
        {
            _mapBotDeathLogicPulseAliveBySlot.Remove(_mapBotDeathLogicPulseStaleSlots[index]);
        }
    }
}
