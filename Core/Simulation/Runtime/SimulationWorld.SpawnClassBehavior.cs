using System;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    public void SetNetworkPlayerMapSpawnClassBehaviorBypass(byte slot, bool bypass)
    {
        if (!IsPlayableNetworkPlayerSlot(slot))
        {
            return;
        }

        if (bypass)
        {
            _networkPlayerMapSpawnClassBehaviorBypassSlots.Add(slot);
        }
        else
        {
            _networkPlayerMapSpawnClassBehaviorBypassSlots.Remove(slot);
        }
    }

    public bool TryGetMapSpawnClassBehavior(PlayerTeam team, out SpawnClassBehaviorMarker marker)
    {
        marker = default;
        if (Level.SpawnClassBehaviors.Count == 0)
        {
            return false;
        }

        var anyMatch = default(SpawnClassBehaviorMarker?);
        for (var index = 0; index < Level.SpawnClassBehaviors.Count; index += 1)
        {
            var candidate = Level.SpawnClassBehaviors[index];
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

    public bool TryGetMapSpawnClassBehaviorForSlot(byte slot, out SpawnClassBehaviorMarker marker)
    {
        marker = default;
        return ShouldApplyMapSpawnClassBehaviorToSlot(slot)
            && TryGetMapSpawnClassBehavior(GetNetworkPlayerConfiguredTeam(slot), out marker);
    }

    public bool TryGetMapForcedClassDefinition(byte slot, out CharacterClassDefinition definition)
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

    public bool CanNetworkPlayerChangeTeamByMapBehavior(byte slot)
    {
        if (!TryGetMapSpawnClassBehaviorForSlot(slot, out var behavior))
        {
            return true;
        }

        return behavior.AllowTeamChange || IsNetworkPlayerAwaitingJoin(slot);
    }

    public bool CanNetworkPlayerSelectClassByMapBehavior(byte slot, CharacterClassDefinition definition)
    {
        if (!TryGetMapSpawnClassBehaviorForSlot(slot, out var behavior))
        {
            return true;
        }

        if (!behavior.AllowClassChange && !IsNetworkPlayerAwaitingJoin(slot))
        {
            return false;
        }

        return true;
    }

    private bool ShouldApplyMapSpawnClassBehaviorToSlot(byte slot) =>
        IsPlayableNetworkPlayerSlot(slot)
        && !_networkPlayerMapSpawnClassBehaviorBypassSlots.Contains(slot);

    private CharacterClassDefinition ResolveMapForcedClassDefinition(byte slot, CharacterClassDefinition requested)
    {
        return TryGetMapForcedClassDefinition(slot, out var forced)
            ? forced
            : requested;
    }

    private bool TryResolveMapManualSpawn(PlayerEntity player, PlayerTeam team, byte slot, out SpawnPoint spawn)
    {
        spawn = default;
        if (!ShouldApplyMapSpawnClassBehaviorToSlot(slot)
            || !TryGetMapSpawnClassBehavior(team, out var behavior)
            || !behavior.ManualSpawn)
        {
            return false;
        }

        if (player.CanOccupy(Level, team, behavior.X, behavior.Y))
        {
            spawn = new SpawnPoint(behavior.X, behavior.Y);
            return true;
        }

        if (TryFindSafeObjectiveSpawnPosition(player, team, behavior.X, behavior.Y, out var safeX, out var safeY))
        {
            spawn = new SpawnPoint(safeX, safeY);
            return true;
        }

        return false;
    }
}
