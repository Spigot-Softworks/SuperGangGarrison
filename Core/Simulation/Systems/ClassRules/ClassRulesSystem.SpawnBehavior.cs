using System;

namespace OpenGarrison.Core;

internal sealed partial class ClassRulesSystem
{
    internal void SetNetworkPlayerMapSpawnClassBehaviorBypass(byte slot, bool bypass)
    {
        if (!NetworkPlayerSystem.IsPlayableNetworkPlayerSlot(slot))
        {
            return;
        }

        if (bypass)
        {
            _host.PlayerRegistry.MapSpawnClassBehaviorBypassSlots.Add(slot);
        }
        else
        {
            _host.PlayerRegistry.MapSpawnClassBehaviorBypassSlots.Remove(slot);
        }
    }

    internal bool TryGetMapSpawnClassBehavior(PlayerTeam team, out SpawnClassBehaviorMarker marker)
    {
        marker = default;
        if (_host.Level.SpawnClassBehaviors.Count == 0)
        {
            return false;
        }

        var anyMatch = default(SpawnClassBehaviorMarker?);
        for (var index = 0; index < _host.Level.SpawnClassBehaviors.Count; index += 1)
        {
            var candidate = _host.Level.SpawnClassBehaviors[index];
            if (!candidate.AppliesToTeam(team))
            {
                continue;
            }

            if (candidate.Team == SpawnClassBehaviorTeam.Any)
            {
                anyMatch ??= candidate;
                continue;
            }

            marker = candidate;
            return true;
        }

        if (anyMatch.HasValue)
        {
            marker = anyMatch.Value;
            return true;
        }

        return false;
    }

    internal bool TryGetMapSpawnClassBehaviorForSlot(byte slot, out SpawnClassBehaviorMarker marker)
    {
        marker = default;
        return ShouldApplyMapSpawnClassBehaviorToSlot(slot)
            && TryGetMapSpawnClassBehavior(_host.GetNetworkPlayerConfiguredTeam(slot), out marker);
    }

    internal bool TryGetMapForcedClassDefinition(byte slot, out CharacterClassDefinition definition)
    {
        definition = CharacterClassCatalog.Scout;
        if (!TryGetMapSpawnClassBehaviorForSlot(slot, out var behavior)
            || !SpawnClassBehaviorMetadata.TryGetForcedGameplayClassId(behavior.ForcedClass, out var classId))
        {
            return false;
        }

        definition = CharacterClassCatalog.GetDefinition(classId);
        return true;
    }

    internal bool CanNetworkPlayerChangeTeamByMapBehavior(byte slot)
    {
        if (!TryGetMapSpawnClassBehaviorForSlot(slot, out var behavior))
        {
            return true;
        }

        return behavior.AllowTeamChange || _host.IsNetworkPlayerAwaitingJoin(slot);
    }

    internal bool CanNetworkPlayerSelectClassByMapBehavior(byte slot, CharacterClassDefinition definition)
    {
        if (!TryGetMapSpawnClassBehaviorForSlot(slot, out var behavior))
        {
            return true;
        }

        if (!behavior.AllowClassChange && !_host.IsNetworkPlayerAwaitingJoin(slot))
        {
            return false;
        }

        return true;
    }

    private bool ShouldApplyMapSpawnClassBehaviorToSlot(byte slot) =>
        NetworkPlayerSystem.IsPlayableNetworkPlayerSlot(slot)
        && !_host.PlayerRegistry.MapSpawnClassBehaviorBypassSlots.Contains(slot);

    internal CharacterClassDefinition ResolveMapForcedClassDefinition(byte slot, CharacterClassDefinition requested)
    {
        return TryGetMapForcedClassDefinition(slot, out var forced)
            ? forced
            : requested;
    }

    internal bool TryResolveMapManualSpawn(PlayerEntity player, PlayerTeam team, byte slot, out SpawnPoint spawn)
    {
        spawn = default;
        if (!ShouldApplyMapSpawnClassBehaviorToSlot(slot)
            || !TryGetMapSpawnClassBehavior(team, out var behavior)
            || !behavior.ManualSpawn)
        {
            return false;
        }

        if (player.CanOccupy(_host.Level, team, behavior.X, behavior.Y))
        {
            spawn = new SpawnPoint(behavior.X, behavior.Y);
            return true;
        }

        if (_host.TryFindSafeObjectiveSpawnPosition(player, team, behavior.X, behavior.Y, out var safeX, out var safeY))
        {
            spawn = new SpawnPoint(safeX, safeY);
            return true;
        }

        return false;
    }
}
