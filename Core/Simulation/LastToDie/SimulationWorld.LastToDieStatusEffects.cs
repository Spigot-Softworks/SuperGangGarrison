using OpenGarrison.Core.LastToDie;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private readonly record struct LastToDieStatusRuntimeKey(
        int TargetPlayerId,
        LastToDieStatusEffectId EffectId,
        LastToDieStatusEffectKind Kind,
        int SourcePlayerId);

    private sealed class LastToDieStatusRuntime(
        LastToDieStatusEffectSpec spec,
        int? sourcePlayerId,
        int? assistingMedicPlayerId)
    {
        public LastToDieStatusEffectSpec Spec { get; set; } = spec;

        public int? SourcePlayerId { get; } = sourcePlayerId;

        public int? AssistingMedicPlayerId { get; set; } = assistingMedicPlayerId;

        public int RemainingTicks { get; set; } = spec.DurationTicks;

        public double DamageAccumulator { get; set; }
    }

    private readonly Dictionary<LastToDieStatusRuntimeKey, LastToDieStatusRuntime>
        _lastToDieStatusRuntimes = [];
    private readonly HashSet<LastToDieStatusRuntimeKey> _lastToDieStatusKeysAtTickStart = [];
    private readonly Dictionary<int, double> _lastToDieGuardianHealingRemaindersByTargetId = [];

    public bool TryApplyLastToDieStatusEffect(
        int targetPlayerId,
        int? sourcePlayerId,
        LastToDieStatusEffectSpec requestedSpec,
        int? assistingMedicPlayerId = null)
    {
        var target = FindPlayerById(targetPlayerId);
        if (target is null
            || !target.IsAlive
            || !TryGetPlayerNetworkSlot(target, out _)
            || !TryNormalizeLastToDieStatusEffect(requestedSpec, out var spec))
        {
            return false;
        }

        PlayerEntity? source = null;
        if (sourcePlayerId.HasValue)
        {
            source = FindPlayerById(sourcePlayerId.Value);
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
            else if (!CanTeamDamagePlayer(source.Team, source.Id, target))
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
            && FindPlayerById(assistingMedicPlayerId.Value) is { } assistingMedic
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
        if (_lastToDieStatusRuntimes.TryGetValue(key, out var runtime))
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
            _lastToDieStatusRuntimes.Add(key, runtime);
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

    public IReadOnlyList<LastToDieActiveStatusEffectSnapshot> GetLastToDieStatusEffects(
        int targetPlayerId)
    {
        return _lastToDieStatusRuntimes
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
                entry.Value.Spec.StackCount))
            .ToArray();
    }

    private void TryApplyLastToDieSniperStatusPayload(
        PlayerEntity source,
        PlayerEntity target,
        bool appliesTranqDarts,
        float poisonTipDamagePerSecond)
    {
        var ticksPerSecond = Math.Max(1, Config.TicksPerSecond);
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
            var nextStackCount = _lastToDieStatusRuntimes.TryGetValue(slowKey, out var slowRuntime)
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

    private bool TryApplyLastToDieSniperGuardian(
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
                    * Math.Max(1, Config.TicksPerSecond),
                LastToDieSniperProfile.GuardianHealingPerSecond,
                LastToDieSniperProfile.GuardianEvasionChance));
    }

    private void BeginLastToDieStatusEffectsTick()
    {
        _lastToDieStatusKeysAtTickStart.Clear();
        if (_lastToDieStatusRuntimes.Count == 0)
        {
            return;
        }

        var orderedKeys = _lastToDieStatusRuntimes.Keys
            .OrderBy(static key => key.TargetPlayerId)
            .ThenBy(static key => key.Kind)
            .ThenBy(static key => key.EffectId.Value, StringComparer.Ordinal)
            .ThenBy(static key => key.SourcePlayerId)
            .ToArray();
        foreach (var key in orderedKeys)
        {
            if (!_lastToDieStatusRuntimes.TryGetValue(key, out var runtime))
            {
                continue;
            }

            var target = FindPlayerById(key.TargetPlayerId);
            if (target is null || !target.IsAlive)
            {
                RemoveLastToDieStatusRuntime(key, runtime);
                continue;
            }

            _lastToDieStatusKeysAtTickStart.Add(key);
            AdvanceLastToDieStatusDamage(target, runtime);
        }

        AdvanceLastToDieGuardianHealing();
    }

    private void EndLastToDieStatusEffectsTick()
    {
        if (_lastToDieStatusKeysAtTickStart.Count == 0)
        {
            return;
        }

        var orderedKeys = _lastToDieStatusKeysAtTickStart
            .OrderBy(static key => key.TargetPlayerId)
            .ThenBy(static key => key.Kind)
            .ThenBy(static key => key.EffectId.Value, StringComparer.Ordinal)
            .ThenBy(static key => key.SourcePlayerId)
            .ToArray();
        foreach (var key in orderedKeys)
        {
            if (!_lastToDieStatusRuntimes.TryGetValue(key, out var runtime))
            {
                continue;
            }

            runtime.RemainingTicks -= 1;
            if (runtime.RemainingTicks <= 0)
            {
                RemoveLastToDieStatusRuntime(key, runtime);
            }
        }

        _lastToDieStatusKeysAtTickStart.Clear();
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
            / (double)Math.Max(1, Config.TicksPerSecond);
        var wholeDamage = (int)Math.Floor(runtime.DamageAccumulator + 0.000000001d);
        if (wholeDamage <= 0)
        {
            return;
        }

        runtime.DamageAccumulator -= wholeDamage;
        var source = runtime.SourcePlayerId.HasValue
            ? FindPlayerById(runtime.SourcePlayerId.Value)
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
        var resolution = ResolvePlayerDamage(
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
            KillPlayer(
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
        var targetPlayerIds = _lastToDieStatusKeysAtTickStart
            .Where(static key => key.Kind == LastToDieStatusEffectKind.BeneficialBuff)
            .Select(static key => key.TargetPlayerId)
            .Distinct()
            .OrderBy(static targetPlayerId => targetPlayerId)
            .ToArray();
        foreach (var targetPlayerId in targetPlayerIds)
        {
            var target = FindPlayerById(targetPlayerId);
            if (target is null || !target.IsAlive)
            {
                _lastToDieGuardianHealingRemaindersByTargetId.Remove(targetPlayerId);
                continue;
            }

            LastToDieStatusRuntime? selectedRuntime = null;
            foreach (var key in _lastToDieStatusKeysAtTickStart)
            {
                if (key.TargetPlayerId != targetPlayerId
                    || key.Kind != LastToDieStatusEffectKind.BeneficialBuff
                    || !_lastToDieStatusRuntimes.TryGetValue(key, out var runtime)
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
                _lastToDieGuardianHealingRemaindersByTargetId.Remove(targetPlayerId);
                continue;
            }

            var remainder = _lastToDieGuardianHealingRemaindersByTargetId.GetValueOrDefault(targetPlayerId);
            remainder += selectedRuntime.Spec.HealingPerSecond
                / Math.Max(1d, Config.TicksPerSecond);
            var wholeHealing = (int)Math.Floor(remainder + 0.000000001d);
            if (wholeHealing <= 0)
            {
                _lastToDieGuardianHealingRemaindersByTargetId[targetPlayerId] = remainder;
                continue;
            }

            remainder -= wholeHealing;
            var appliedHealing = ApplyHealingWithFeedback(target, wholeHealing);
            if (appliedHealing > 0
                && selectedRuntime.SourcePlayerId.HasValue
                && FindPlayerById(selectedRuntime.SourcePlayerId.Value) is { } source)
            {
                AwardHealingPoints(source, appliedHealing);
            }

            if (target.Health >= target.MaxHealth)
            {
                _lastToDieGuardianHealingRemaindersByTargetId.Remove(targetPlayerId);
            }
            else
            {
                _lastToDieGuardianHealingRemaindersByTargetId[targetPlayerId] = remainder;
            }
        }
    }

    private void RemoveLastToDieStatusRuntime(
        LastToDieStatusRuntimeKey key,
        LastToDieStatusRuntime runtime)
    {
        _lastToDieStatusRuntimes.Remove(key);
        _lastToDieStatusKeysAtTickStart.Remove(key);
        if (runtime.Spec.Kind == LastToDieStatusEffectKind.Slow
            && FindPlayerById(key.TargetPlayerId) is { } target)
        {
            RefreshLastToDieStatusDebuffState(target);
        }
        else if (runtime.Spec.Kind == LastToDieStatusEffectKind.BeneficialBuff
            && FindPlayerById(key.TargetPlayerId) is { } guardianTarget)
        {
            RefreshLastToDieGuardianState(guardianTarget);
        }
    }

    private void RefreshLastToDieStatusDebuffState(PlayerEntity target)
    {
        var movementSpeedMultiplier = 1f;
        var outgoingDamageMultiplier = 1f;
        foreach (var entry in _lastToDieStatusRuntimes)
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
            }
        }

        target.SetLastToDieStatusMovementSpeedMultiplier(movementSpeedMultiplier);
        target.SetLastToDieStatusOutgoingDamageMultiplier(outgoingDamageMultiplier);
    }

    private void RefreshLastToDieGuardianState(PlayerEntity target)
    {
        var evasionChance = 0f;
        var hasGuardianStatus = false;
        foreach (var entry in _lastToDieStatusRuntimes)
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
            _lastToDieGuardianHealingRemaindersByTargetId.Remove(target.Id);
        }
    }

    private void ClearLastToDieStatusEffectsForTarget(int targetPlayerId)
    {
        var keys = _lastToDieStatusRuntimes.Keys
            .Where(key => key.TargetPlayerId == targetPlayerId)
            .ToArray();
        if (keys.Length == 0)
        {
            return;
        }

        foreach (var key in keys)
        {
            if (_lastToDieStatusRuntimes.TryGetValue(key, out var runtime))
            {
                RemoveLastToDieStatusRuntime(key, runtime);
            }
        }

        FindPlayerById(targetPlayerId)?.ClearLastToDieStatusRuntimeState();
    }

    private void ClearLastToDieStatusEffectsForReleasedPlayer(int playerId)
    {
        var affectedTargets = new HashSet<int>();
        var keys = _lastToDieStatusRuntimes.Keys
            .Where(key => key.TargetPlayerId == playerId || key.SourcePlayerId == playerId)
            .ToArray();
        foreach (var key in keys)
        {
            if (_lastToDieStatusRuntimes.TryGetValue(key, out var runtime))
            {
                affectedTargets.Add(key.TargetPlayerId);
                RemoveLastToDieStatusRuntime(key, runtime);
            }
        }

        foreach (var targetPlayerId in affectedTargets)
        {
            if (FindPlayerById(targetPlayerId) is { } target)
            {
                RefreshLastToDieStatusDebuffState(target);
            }
        }

        FindPlayerById(playerId)?.ClearLastToDieStatusRuntimeState();
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
            StackCount = stackCount,
        };
        return true;
    }
}
