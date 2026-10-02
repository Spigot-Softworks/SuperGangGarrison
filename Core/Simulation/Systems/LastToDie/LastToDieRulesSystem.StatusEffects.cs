using OpenGarrison.Core.LastToDie;

namespace OpenGarrison.Core;

internal sealed partial class LastToDieRulesSystem
{
    internal bool TryApplyLastToDieStatusEffect(
        int targetPlayerId,
        int? sourcePlayerId,
        LastToDieStatusEffectSpec requestedSpec,
        int? assistingMedicPlayerId = null)
    {
        var target = _host.FindPlayerById(targetPlayerId);
        if (target is null
            || !target.IsAlive
            || !_host.TryGetPlayerNetworkSlot(target, out _)
            || !TryNormalizeLastToDieStatusEffect(requestedSpec, out var spec))
        {
            return false;
        }

        PlayerEntity? source = null;
        if (sourcePlayerId.HasValue)
        {
            source = _host.FindPlayerById(sourcePlayerId.Value);
            if (source is null
                || ReferenceEquals(source, target))
            {
                return false;
            }

            if (spec.Kind == LastToDieStatusEffectKind.BeneficialBuff)
            {
                if (!source.IsAlive || source.Team != target.Team)
                {
                    return false;
                }
            }
            else if (!_host.CanTeamDamagePlayer(source.Team, source.Id, target))
            {
                return false;
            }
        }
        else if (spec.Kind == LastToDieStatusEffectKind.BeneficialBuff)
        {
            return false;
        }


        int? resolvedAssistingMedicPlayerId = null;
        if (assistingMedicPlayerId.HasValue
            && source is not null
            && _host.FindPlayerById(assistingMedicPlayerId.Value) is { } assistingMedic
            && assistingMedic.IsAlive
            && assistingMedic.Id != source.Id
            && assistingMedic.Id != target.Id
            && assistingMedic.Team == source.Team)
        {
            resolvedAssistingMedicPlayerId = assistingMedic.Id;
        }

        var key = new LastToDieStatusRuntimeKey(
            target.Id,
            spec.Id,
            spec.Kind,
            source?.Id ?? -1);
        if (_host.LastToDieState.StatusRuntimes.TryGetValue(key, out var runtime))
        {
            runtime.RemainingTicks = Math.Max(runtime.RemainingTicks, spec.DurationTicks);
            runtime.Spec = SelectStrongerLastToDieStatusSpec(runtime.Spec, spec);
            runtime.AssistingMedicPlayerId = resolvedAssistingMedicPlayerId;
        }
        else
        {
            runtime = new LastToDieStatusRuntime(
                spec,
                source?.Id,
                resolvedAssistingMedicPlayerId);
            _host.LastToDieState.StatusRuntimes.Add(key, runtime);
        }

        if (spec.Kind == LastToDieStatusEffectKind.Slow)
        {
            RefreshLastToDieStatusDebuffState(target);
        }
        else if (spec.Kind == LastToDieStatusEffectKind.Stun)
        {
            target.RefreshServerStunTicks(runtime.RemainingTicks);
        }
        else if (spec.Kind == LastToDieStatusEffectKind.BeneficialBuff)
        {
            RefreshLastToDieGuardianState(target);
        }

        return true;
    }

    internal IReadOnlyList<LastToDieActiveStatusEffectSnapshot> GetLastToDieStatusEffects(
        int targetPlayerId)
    {
        return _host.LastToDieState.StatusRuntimes
            .Where(entry => entry.Key.TargetPlayerId == targetPlayerId)
            .OrderBy(entry => entry.Key.Kind)
            .ThenBy(entry => entry.Key.EffectId.Value, StringComparer.Ordinal)
            .ThenBy(entry => entry.Key.SourcePlayerId)
            .Select(entry => new LastToDieActiveStatusEffectSnapshot(
                entry.Value.Spec.Id,
                entry.Value.Spec.Kind,
                entry.Key.TargetPlayerId,
                entry.Value.SourcePlayerId,
                entry.Value.RemainingTicks,
                entry.Value.Spec.DamagePerSecond,
                entry.Value.Spec.MovementSpeedMultiplier,
                entry.Value.Spec.HealingPerSecond,
                entry.Value.Spec.EvasionChance,
                entry.Value.Spec.OutgoingDamageMultiplier,
                entry.Value.Spec.StackCount,
                entry.Value.Spec.FireSpeedMultiplier,
                entry.Value.Spec.ReloadSpeedMultiplier))
            .ToArray();
    }

    internal void TryApplyLastToDieSniperStatusPayload(
        PlayerEntity source,
        PlayerEntity target,
        bool appliesTranqDarts,
        float poisonTipDamagePerSecond)
    {
        var ticksPerSecond = Math.Max(1, _host.Config.TicksPerSecond);
        if (appliesTranqDarts)
        {
            var durationTicks = LastToDieSniperProfile.TranqDartsDurationSeconds
                * ticksPerSecond;
            _ = TryApplyLastToDieStatusEffect(
                target.Id,
                source.Id,
                LastToDieStatusEffectSpec.Poison(
                    LastToDieStatusEffectIds.SniperTranqPoison,
                    durationTicks,
                    LastToDieSniperProfile.TranqDartsPoisonDamagePerSecond));

            var slowKey = new LastToDieStatusRuntimeKey(
                target.Id,
                LastToDieStatusEffectIds.SniperTranqSlow,
                LastToDieStatusEffectKind.Slow,
                source.Id);
            var nextStackCount = _host.LastToDieState.StatusRuntimes.TryGetValue(slowKey, out var slowRuntime)
                ? slowRuntime.Spec.StackCount + 1
                : 1;
            nextStackCount = Math.Clamp(
                nextStackCount,
                1,
                LastToDieSniperProfile.TranqDartsMaximumSlowStacks);
            _ = TryApplyLastToDieStatusEffect(
                target.Id,
                source.Id,
                LastToDieStatusEffectSpec.Slow(
                    LastToDieStatusEffectIds.SniperTranqSlow,
                    durationTicks,
                    1f - (nextStackCount * LastToDieSniperProfile.TranqDartsSlowPerStack),
                    LastToDieSniperProfile.TranqDartsOutgoingDamageMultiplier,
                    nextStackCount));
        }

        if (poisonTipDamagePerSecond > 0f)
        {
            _ = TryApplyLastToDieStatusEffect(
                target.Id,
                source.Id,
                LastToDieStatusEffectSpec.Poison(
                    LastToDieStatusEffectIds.SniperPoisonTip,
                    LastToDieSniperProfile.PoisonTipDurationSeconds * ticksPerSecond,
                    poisonTipDamagePerSecond));
        }
    }

    internal bool TryApplyLastToDieSniperGuardian(
        PlayerEntity sniper,
        PlayerEntity target)
    {
        if (sniper.ClassId != PlayerClass.Sniper
            || !sniper.LastToDieSniperProfile.GuardianEnabled
            || !sniper.IsAlive
            || !target.IsAlive
            || ReferenceEquals(sniper, target)
            || sniper.Team != target.Team)
        {
            return false;
        }

        return TryApplyLastToDieStatusEffect(
            target.Id,
            sniper.Id,
            LastToDieStatusEffectSpec.BeneficialBuff(
                LastToDieStatusEffectIds.SniperGuardian,
                LastToDieSniperProfile.GuardianDurationSeconds
                    * Math.Max(1, _host.Config.TicksPerSecond),
                LastToDieSniperProfile.GuardianHealingPerSecond,
                LastToDieSniperProfile.GuardianEvasionChance));
    }

    internal void BeginLastToDieStatusEffectsTick()
    {
        _host.LastToDieState.StatusKeysAtTickStart.Clear();
        if (_host.LastToDieState.StatusRuntimes.Count == 0)
        {
            return;
        }

        var orderedKeys = _host.LastToDieState.StatusRuntimes.Keys
            .OrderBy(static key => key.TargetPlayerId)
            .ThenBy(static key => key.Kind)
            .ThenBy(static key => key.EffectId.Value, StringComparer.Ordinal)
            .ThenBy(static key => key.SourcePlayerId)
            .ToArray();
        foreach (var key in orderedKeys)
        {
            if (!_host.LastToDieState.StatusRuntimes.TryGetValue(key, out var runtime))
            {
                continue;
            }

            var target = _host.FindPlayerById(key.TargetPlayerId);
            if (target is null || !target.IsAlive)
            {
                RemoveLastToDieStatusRuntime(key, runtime);
                continue;
            }

            _host.LastToDieState.StatusKeysAtTickStart.Add(key);
            AdvanceLastToDieStatusDamage(target, runtime);
        }

        AdvanceLastToDieGuardianHealing();
    }

    internal void EndLastToDieStatusEffectsTick()
    {
        if (_host.LastToDieState.StatusKeysAtTickStart.Count == 0)
        {
            return;
        }

        var orderedKeys = _host.LastToDieState.StatusKeysAtTickStart
            .OrderBy(static key => key.TargetPlayerId)
            .ThenBy(static key => key.Kind)
            .ThenBy(static key => key.EffectId.Value, StringComparer.Ordinal)
            .ThenBy(static key => key.SourcePlayerId)
            .ToArray();
        foreach (var key in orderedKeys)
        {
            if (!_host.LastToDieState.StatusRuntimes.TryGetValue(key, out var runtime))
            {
                continue;
            }

            runtime.RemainingTicks -= 1;
            if (runtime.RemainingTicks <= 0)
            {
                RemoveLastToDieStatusRuntime(key, runtime);
            }
        }

        _host.LastToDieState.StatusKeysAtTickStart.Clear();
    }

    private void AdvanceLastToDieStatusDamage(
        PlayerEntity target,
        LastToDieStatusRuntime runtime)
    {
        if (runtime.Spec.Kind is not (LastToDieStatusEffectKind.Bleed or LastToDieStatusEffectKind.Poison)
            || runtime.Spec.DamagePerSecond <= 0f)
        {
            return;
        }

        runtime.DamageAccumulator +=
            (runtime.Spec.DamagePerSecond * target.LastToDieIncomingDamageMultiplier)
            / (double)Math.Max(1, _host.Config.TicksPerSecond);
        var wholeDamage = (int)Math.Floor(runtime.DamageAccumulator + 0.000000001d);
        if (wholeDamage <= 0)
        {
            return;
        }

        runtime.DamageAccumulator -= wholeDamage;
        var source = runtime.SourcePlayerId.HasValue
            ? _host.FindPlayerById(runtime.SourcePlayerId.Value)
            : null;
        var traits = PlayerDamageTraits.Periodic
            | PlayerDamageTraits.LastToDieIncomingModifierPreApplied
            | (runtime.Spec.Kind == LastToDieStatusEffectKind.Bleed
                ? PlayerDamageTraits.Bleed
                : PlayerDamageTraits.Poison);
        if (runtime.Spec.Id == LastToDieStatusEffectIds.SniperTranqPoison
            || runtime.Spec.Id == LastToDieStatusEffectIds.SniperPoisonTip)
        {
            traits |= PlayerDamageTraits.BenefitFromLastToDieSpotted;
        }
        var resolution = _host.ResolvePlayerDamage(
            target,
            new PlayerDamageRequest(
                PlayerDamageApplicationKind.Instant,
                wholeDamage,
                source,
                PlayerEntity.SpyDamageRevealAlpha,
                DamageEventFlags.StatusTick,
                traits,
                AllowOsmosisHealOwnedSentries: false,
                new PlayerDamageUmbrellaOptions(AllowBlock: false),
                AssistPlayerIdOverride: runtime.AssistingMedicPlayerId ?? -1));
        if (resolution.WasFatal)
        {
            _host.KillPlayer(
                target,
                killer: source,
                weaponSpriteName: runtime.Spec.Kind == LastToDieStatusEffectKind.Bleed
                    ? "BleedKL"
                    : "PoisonKL",
                assistingPlayerIdOverride: runtime.AssistingMedicPlayerId ?? -1);
        }
    }

    private void AdvanceLastToDieGuardianHealing()
    {
        var targetPlayerIds = _host.LastToDieState.StatusKeysAtTickStart
            .Where(static key => key.Kind == LastToDieStatusEffectKind.BeneficialBuff)
            .Select(static key => key.TargetPlayerId)
            .Distinct()
            .OrderBy(static targetPlayerId => targetPlayerId)
            .ToArray();
        foreach (var targetPlayerId in targetPlayerIds)
        {
            var target = _host.FindPlayerById(targetPlayerId);
            if (target is null || !target.IsAlive)
            {
                _host.LastToDieState.GuardianHealingRemaindersByTargetId.Remove(targetPlayerId);
                continue;
            }

            LastToDieStatusRuntime? selectedRuntime = null;
            foreach (var key in _host.LastToDieState.StatusKeysAtTickStart)
            {
                if (key.TargetPlayerId != targetPlayerId
                    || key.Kind != LastToDieStatusEffectKind.BeneficialBuff
                    || !_host.LastToDieState.StatusRuntimes.TryGetValue(key, out var runtime)
                    || runtime.Spec.HealingPerSecond <= 0f)
                {
                    continue;
                }

                if (selectedRuntime is null
                    || runtime.Spec.HealingPerSecond > selectedRuntime.Spec.HealingPerSecond
                    || (runtime.Spec.HealingPerSecond == selectedRuntime.Spec.HealingPerSecond
                        && (runtime.SourcePlayerId ?? int.MaxValue)
                            < (selectedRuntime.SourcePlayerId ?? int.MaxValue)))
                {
                    selectedRuntime = runtime;
                }
            }

            if (selectedRuntime is null || target.Health >= target.MaxHealth)
            {
                _host.LastToDieState.GuardianHealingRemaindersByTargetId.Remove(targetPlayerId);
                continue;
            }

            var remainder = _host.LastToDieState.GuardianHealingRemaindersByTargetId.GetValueOrDefault(targetPlayerId);
            remainder += selectedRuntime.Spec.HealingPerSecond
                / Math.Max(1d, _host.Config.TicksPerSecond);
            var wholeHealing = (int)Math.Floor(remainder + 0.000000001d);
            if (wholeHealing <= 0)
            {
                _host.LastToDieState.GuardianHealingRemaindersByTargetId[targetPlayerId] = remainder;
                continue;
            }

            remainder -= wholeHealing;
            var appliedHealing = _host.ApplyHealingWithFeedback(target, wholeHealing);
            if (appliedHealing > 0
                && selectedRuntime.SourcePlayerId.HasValue
                && _host.FindPlayerById(selectedRuntime.SourcePlayerId.Value) is { } source)
            {
                _host.AwardHealingPoints(source, appliedHealing);
            }

            if (target.Health >= target.MaxHealth)
            {
                _host.LastToDieState.GuardianHealingRemaindersByTargetId.Remove(targetPlayerId);
            }
            else
            {
                _host.LastToDieState.GuardianHealingRemaindersByTargetId[targetPlayerId] = remainder;
            }
        }
    }

    private void RemoveLastToDieStatusRuntime(
        LastToDieStatusRuntimeKey key,
        LastToDieStatusRuntime runtime)
    {
        _host.LastToDieState.StatusRuntimes.Remove(key);
        _host.LastToDieState.StatusKeysAtTickStart.Remove(key);
        if (runtime.Spec.Kind == LastToDieStatusEffectKind.Slow
            && _host.FindPlayerById(key.TargetPlayerId) is { } target)
        {
            RefreshLastToDieStatusDebuffState(target);
        }
        else if (runtime.Spec.Kind == LastToDieStatusEffectKind.BeneficialBuff
            && _host.FindPlayerById(key.TargetPlayerId) is { } guardianTarget)
        {
            RefreshLastToDieGuardianState(guardianTarget);
        }
    }

    private void RefreshLastToDieStatusDebuffState(PlayerEntity target)
    {
        var movementSpeedMultiplier = 1f;
        var outgoingDamageMultiplier = 1f;
        var fireSpeedMultiplier = 1f;
        var reloadSpeedMultiplier = 1f;
        foreach (var entry in _host.LastToDieState.StatusRuntimes)
        {
            if (entry.Key.TargetPlayerId == target.Id
                && entry.Key.Kind == LastToDieStatusEffectKind.Slow)
            {
                movementSpeedMultiplier = Math.Min(
                    movementSpeedMultiplier,
                    entry.Value.Spec.MovementSpeedMultiplier);
                outgoingDamageMultiplier = Math.Min(
                    outgoingDamageMultiplier,
                    entry.Value.Spec.OutgoingDamageMultiplier);
                fireSpeedMultiplier = Math.Min(
                    fireSpeedMultiplier,
                    entry.Value.Spec.FireSpeedMultiplier);
                reloadSpeedMultiplier = Math.Min(
                    reloadSpeedMultiplier,
                    entry.Value.Spec.ReloadSpeedMultiplier);
            }
        }

        target.SetLastToDieStatusMovementSpeedMultiplier(movementSpeedMultiplier);
        target.SetLastToDieStatusOutgoingDamageMultiplier(outgoingDamageMultiplier);
        target.SetLastToDieStatusFireSpeedMultiplier(fireSpeedMultiplier);
        target.SetLastToDieStatusReloadSpeedMultiplier(reloadSpeedMultiplier);
    }

    private void RefreshLastToDieGuardianState(PlayerEntity target)
    {
        var evasionChance = 0f;
        var hasGuardianStatus = false;
        foreach (var entry in _host.LastToDieState.StatusRuntimes)
        {
            if (entry.Key.TargetPlayerId != target.Id
                || entry.Key.Kind != LastToDieStatusEffectKind.BeneficialBuff)
            {
                continue;
            }

            hasGuardianStatus = true;
            evasionChance = Math.Max(
                evasionChance,
                entry.Value.Spec.EvasionChance);
        }

        target.SetLastToDieGuardianEvasionChance(evasionChance);
        if (!hasGuardianStatus)
        {
            _host.LastToDieState.GuardianHealingRemaindersByTargetId.Remove(target.Id);
        }
    }

    internal void ClearLastToDieStatusEffectsForTarget(int targetPlayerId)
    {
        var keys = _host.LastToDieState.StatusRuntimes.Keys
            .Where(key => key.TargetPlayerId == targetPlayerId)
            .ToArray();
        if (keys.Length == 0)
        {
            return;
        }

        foreach (var key in keys)
        {
            if (_host.LastToDieState.StatusRuntimes.TryGetValue(key, out var runtime))
            {
                RemoveLastToDieStatusRuntime(key, runtime);
            }
        }

        _host.FindPlayerById(targetPlayerId)?.ClearLastToDieStatusRuntimeState();
    }

    internal void ClearLastToDieStatusEffectsForReleasedPlayer(int playerId)
    {
        var affectedTargets = new HashSet<int>();
        var keys = _host.LastToDieState.StatusRuntimes.Keys
            .Where(key => key.TargetPlayerId == playerId || key.SourcePlayerId == playerId)
            .ToArray();
        foreach (var key in keys)
        {
            if (_host.LastToDieState.StatusRuntimes.TryGetValue(key, out var runtime))
            {
                affectedTargets.Add(key.TargetPlayerId);
                RemoveLastToDieStatusRuntime(key, runtime);
            }
        }

        foreach (var targetPlayerId in affectedTargets)
        {
            if (_host.FindPlayerById(targetPlayerId) is { } target)
            {
                RefreshLastToDieStatusDebuffState(target);
            }
        }

        _host.FindPlayerById(playerId)?.ClearLastToDieStatusRuntimeState();
    }

    private static LastToDieStatusEffectSpec SelectStrongerLastToDieStatusSpec(
        LastToDieStatusEffectSpec current,
        LastToDieStatusEffectSpec candidate)
    {
        return current.Kind switch
        {
            LastToDieStatusEffectKind.Bleed or LastToDieStatusEffectKind.Poison
                when candidate.DamagePerSecond > current.DamagePerSecond => candidate,
            LastToDieStatusEffectKind.Slow
                => current with
                {
                    MovementSpeedMultiplier = Math.Min(
                        current.MovementSpeedMultiplier,
                        candidate.MovementSpeedMultiplier),
                    OutgoingDamageMultiplier = Math.Min(
                        current.OutgoingDamageMultiplier,
                        candidate.OutgoingDamageMultiplier),
                    FireSpeedMultiplier = Math.Min(
                        current.FireSpeedMultiplier,
                        candidate.FireSpeedMultiplier),
                    ReloadSpeedMultiplier = Math.Min(
                        current.ReloadSpeedMultiplier,
                        candidate.ReloadSpeedMultiplier),
                    StackCount = Math.Max(current.StackCount, candidate.StackCount),
                },
            LastToDieStatusEffectKind.BeneficialBuff => current with
            {
                HealingPerSecond = Math.Max(
                    current.HealingPerSecond,
                    candidate.HealingPerSecond),
                EvasionChance = Math.Max(
                    current.EvasionChance,
                    candidate.EvasionChance),
            },
            _ => current,
        } with
        {
            DurationTicks = Math.Max(current.DurationTicks, candidate.DurationTicks),
        };
    }

    private static bool TryNormalizeLastToDieStatusEffect(
        LastToDieStatusEffectSpec requested,
        out LastToDieStatusEffectSpec normalized)
    {
        normalized = default;
        if (!GameplayReplicatedStateContract.TryNormalizeIdentifier(requested.Id.Value, out var normalizedId)
            || requested.DurationTicks <= 0
            || !Enum.IsDefined(requested.Kind))
        {
            return false;
        }

        var damagePerSecond = MathF.Max(0f, requested.DamagePerSecond);
        var movementSpeedMultiplier = Math.Clamp(requested.MovementSpeedMultiplier, 0.05f, 1f);
        var healingPerSecond = MathF.Max(0f, requested.HealingPerSecond);
        var evasionChance = Math.Clamp(requested.EvasionChance, 0f, 0.95f);
        var outgoingDamageMultiplier = Math.Clamp(requested.OutgoingDamageMultiplier, 0.05f, 1f);
        var fireSpeedMultiplier = Math.Clamp(requested.FireSpeedMultiplier, 0.05f, 4f);
        var reloadSpeedMultiplier = Math.Clamp(requested.ReloadSpeedMultiplier, 0.05f, 4f);
        var stackCount = Math.Clamp(requested.StackCount, 1, byte.MaxValue);
        switch (requested.Kind)
        {
            case LastToDieStatusEffectKind.Bleed:
            case LastToDieStatusEffectKind.Poison:
                if (damagePerSecond <= 0f)
                {
                    return false;
                }

                movementSpeedMultiplier = 1f;
                healingPerSecond = 0f;
                evasionChance = 0f;
                fireSpeedMultiplier = 1f;
                reloadSpeedMultiplier = 1f;
                outgoingDamageMultiplier = 1f;
                stackCount = 1;
                break;
            case LastToDieStatusEffectKind.Slow:
                if (movementSpeedMultiplier >= 1f)
                {
                    return false;
                }

                damagePerSecond = 0f;
                healingPerSecond = 0f;
                evasionChance = 0f;
                break;
            case LastToDieStatusEffectKind.Stun:
                damagePerSecond = 0f;
                movementSpeedMultiplier = 1f;
                healingPerSecond = 0f;
                evasionChance = 0f;
                outgoingDamageMultiplier = 1f;
                fireSpeedMultiplier = 1f;
                reloadSpeedMultiplier = 1f;
                stackCount = 1;
                break;
            case LastToDieStatusEffectKind.BeneficialBuff:
                if (healingPerSecond <= 0f && evasionChance <= 0f)
                {
                    return false;
                }

                damagePerSecond = 0f;
                movementSpeedMultiplier = 1f;
                outgoingDamageMultiplier = 1f;
                fireSpeedMultiplier = 1f;
                reloadSpeedMultiplier = 1f;
                stackCount = 1;
                break;
            default:
                return false;
        }

        normalized = requested with
        {
            Id = new LastToDieStatusEffectId(normalizedId),
            DamagePerSecond = damagePerSecond,
            MovementSpeedMultiplier = movementSpeedMultiplier,
            HealingPerSecond = healingPerSecond,
            EvasionChance = evasionChance,
            OutgoingDamageMultiplier = outgoingDamageMultiplier,
            FireSpeedMultiplier = fireSpeedMultiplier,
            ReloadSpeedMultiplier = reloadSpeedMultiplier,
            StackCount = stackCount,
        };
        return true;
    }
}
