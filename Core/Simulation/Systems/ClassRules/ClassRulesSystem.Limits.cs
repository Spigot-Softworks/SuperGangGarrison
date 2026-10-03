namespace OpenGarrison.Core;

internal sealed partial class ClassRulesSystem
{
    internal void SetCaptureSpeedMultiplierPerPlayer(float multiplier)
    {
        _host.MatchSettings.CaptureSpeedMultiplierPerPlayer = float.Clamp(multiplier, 0f, 10f);
    }

    internal void SetVipAllowDuplicateClasses(bool enabled)
    {
        _host.VipState.AllowDuplicateClasses = enabled;
    }

    internal void SetClassLimit(PlayerClass playerClass, int limit)
    {
        if (!Enum.IsDefined(playerClass))
        {
            return;
        }

        limit = Math.Clamp(limit, 0, SimulationConstants.MaxPlayableNetworkPlayers);
        if (limit == 0)
        {
            _host.MatchSettings.ClassLimits.Remove(playerClass);
            return;
        }

        _host.MatchSettings.ClassLimits[playerClass] = limit;
    }

    internal void SetAllClassLimits(int limit)
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

    internal int GetUniformClassLimit()
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

    internal int GetClassLimit(PlayerClass playerClass)
    {
        return _host.MatchSettings.ClassLimits.TryGetValue(playerClass, out var limit) ? limit : 0;
    }

    internal bool CanApplyNetworkPlayerClassLimit(byte slot, CharacterClassDefinition definition)
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

        var team = _host.NetworkPlayers.GetNetworkPlayerConfiguredTeam(slot);
        var currentCount = 0;
        foreach (var candidateSlot in SimulationConstants.NetworkPlayerSlots)
        {
            if (candidateSlot == slot
                || !_host.NetworkPlayers.IsNetworkPlayerEnabled(candidateSlot)
                || _host.NetworkPlayers.IsNetworkPlayerAwaitingJoin(candidateSlot)
                || _host.NetworkPlayers.GetNetworkPlayerConfiguredTeam(candidateSlot) != team)
            {
                continue;
            }

            var candidateDefinition = _host.NetworkPlayers.GetNetworkPlayerClassDefinition(candidateSlot);
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
        if (_host.IsVipModeActive && !_host.VipState.AllowDuplicateClasses)
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
