namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    public void SetCaptureSpeedMultiplierPerPlayer(float multiplier)
    {
        _configuredCaptureSpeedMultiplierPerPlayer = float.Clamp(multiplier, 0f, 10f);
    }

    public void SetVipAllowDuplicateClasses(bool enabled)
    {
        _vipAllowDuplicateClasses = enabled;
    }

    public void SetClassLimit(PlayerClass playerClass, int limit)
    {
        if (!Enum.IsDefined(playerClass))
        {
            return;
        }

        limit = Math.Clamp(limit, 0, MaxPlayableNetworkPlayers);
        if (limit == 0)
        {
            _configuredClassLimits.Remove(playerClass);
            return;
        }

        _configuredClassLimits[playerClass] = limit;
    }

    public void SetAllClassLimits(int limit)
    {
        SetClassLimit(PlayerClass.Scout, limit);
        SetClassLimit(PlayerClass.Engineer, limit);
        SetClassLimit(PlayerClass.Pyro, limit);
        SetClassLimit(PlayerClass.Soldier, limit);
        SetClassLimit(PlayerClass.Demoman, limit);
        SetClassLimit(PlayerClass.Heavy, limit);
        SetClassLimit(PlayerClass.Sniper, limit);
        SetClassLimit(PlayerClass.Medic, limit);
        SetClassLimit(PlayerClass.Spy, limit);
        SetClassLimit(PlayerClass.Quote, limit);
    }

    public int GetUniformClassLimit()
    {
        int? uniformLimit = null;
        foreach (var playerClass in EnumerateLimitableClasses())
        {
            var limit = GetClassLimit(playerClass);
            if (!uniformLimit.HasValue)
            {
                uniformLimit = limit;
                continue;
            }

            if (uniformLimit.Value != limit)
            {
                return 0;
            }
        }

        return uniformLimit ?? 0;
    }

    public int GetClassLimit(PlayerClass playerClass)
    {
        return _configuredClassLimits.TryGetValue(playerClass, out var limit) ? limit : 0;
    }

    private bool CanApplyNetworkPlayerClassLimit(byte slot, CharacterClassDefinition definition)
    {
        if (!CharacterClassCatalog.RuntimeRegistry.TryGetClassBinding(definition.GameplayClassId, out var binding))
        {
            return false;
        }

        var limit = GetEffectiveClassLimit(binding.PlayerClass);
        if (limit <= 0)
        {
            return true;
        }

        var team = GetNetworkPlayerConfiguredTeam(slot);
        var currentCount = 0;
        foreach (var candidateSlot in NetworkPlayerSlots)
        {
            if (candidateSlot == slot
                || !IsNetworkPlayerEnabled(candidateSlot)
                || IsNetworkPlayerAwaitingJoin(candidateSlot)
                || GetNetworkPlayerConfiguredTeam(candidateSlot) != team)
            {
                continue;
            }

            var candidateDefinition = GetNetworkPlayerClassDefinition(candidateSlot);
            if (!CharacterClassCatalog.RuntimeRegistry.TryGetClassBinding(candidateDefinition.GameplayClassId, out var candidateBinding)
                || candidateBinding.PlayerClass != binding.PlayerClass)
            {
                continue;
            }

            currentCount += 1;
            if (currentCount >= limit)
            {
                return false;
            }
        }

        return true;
    }

    private int GetEffectiveClassLimit(PlayerClass playerClass)
    {
        if (IsVipModeActive && !_vipAllowDuplicateClasses)
        {
            return 1;
        }

        return GetClassLimit(playerClass);
    }

    private static IEnumerable<PlayerClass> EnumerateLimitableClasses()
    {
        yield return PlayerClass.Scout;
        yield return PlayerClass.Engineer;
        yield return PlayerClass.Pyro;
        yield return PlayerClass.Soldier;
        yield return PlayerClass.Demoman;
        yield return PlayerClass.Heavy;
        yield return PlayerClass.Sniper;
        yield return PlayerClass.Medic;
        yield return PlayerClass.Spy;
        yield return PlayerClass.Quote;
    }
}
