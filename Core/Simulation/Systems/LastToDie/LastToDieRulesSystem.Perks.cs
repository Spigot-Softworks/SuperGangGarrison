using OpenGarrison.Core.LastToDie;

namespace OpenGarrison.Core;

internal sealed partial class LastToDieRulesSystem
{
    internal void ConfigureLastToDieCombatSeed(ulong seed)
    {
        _host.LastToDieState.CombatSeed = seed;
        _host.LastToDieState.CombatSeedConfigured = true;
        foreach (var entry in _host.LastToDieState.PerkRuntimesBySlot)
        {
            entry.Value.RevolverCriticalRandom = CreateLastToDieRevolverCriticalRandom(entry.Key);
            entry.Value.EvasionRandom = CreateLastToDieEvasionRandom(entry.Key);
            entry.Value.OverkillerRandom = CreateLastToDieOverkillerRandom(entry.Key);
            entry.Value.DamageHealingRemainder = 0;
            entry.Value.ScopedDamageHealingRemainder = 0;
            entry.Value.ScopedHealingAccumulator = 0f;
            entry.Value.CloakedHealingAccumulator = 0f;
            entry.Value.MedicHomeostasisHealingRemainder = 0;
            entry.Value.MedicSpikedVestReflectionRemainder = 0;
            entry.Value.UniversalReflectionRemainder = 0;
            entry.Value.UniversalHealingAccumulator = 0f;
            entry.Value.InfiniteSlayWorksDamageAccumulator = 0f;
            entry.Value.MedicSupportRelayActiveLinkTargetPlayerId = null;
            entry.Value.MedicSupportRelayCooldownUntilFrameByTargetPlayerId.Clear();
            entry.Value.ShroudGraceTicksRemaining = 0;
            if (_host.TryGetNetworkPlayer(entry.Key, out var player))
            {
                entry.Value.WasSpyCloaked = player.IsAlive
                    && player.ClassId == PlayerClass.Spy
                    && player.IsSpyCloaked;
                player.ResetLastToDieLuckyStrikeTriggerProgress();
                player.ResetLastToDieMedicDynamicState();
                player.ResetLastToDieSniperDynamicState();
                player.ResetLastToDieSpyInfiltrateDynamicState();
                ResetLastToDieSpyAfterlifeRuntime(entry.Key, player);
            }
            else
            {
                entry.Value.WasSpyCloaked = false;
            }
        }
    }

    internal bool TryConfigureLastToDiePlayerBuild(
        byte slot,
        IEnumerable<LastToDiePerkId> perks,
        int? baseMaximumHealthOverride = null,
        bool refillHealth = false,
        bool resetDynamicState = false,
        int runKills = 0,
        bool secondChanceConsumed = false,
        bool runKillProgressionOwner = true)
    {
        ArgumentNullException.ThrowIfNull(perks);
        if (!_host.TryGetNetworkPlayer(slot, out var player))
        {
            return false;
        }

        var ownedPerks = perks.ToArray();
        var modifiers = LastToDieDerivedModifiers.FromPerks(ownedPerks);
        var legacySettings = LastToDieLegacyPerkSettings.FromPerks(
            player.ClassId,
            ownedPerks,
            _host.ExperimentalGameplaySettings);
        var previousStaticMaximumHealthBonus = 0;
        var previousBaseMaximumHealth = player.ClassDefinition.MaxHealth;
        var healthBeforeConfiguration = player.Health;
        if (_host.LastToDieState.PerkRuntimesBySlot.TryGetValue(slot, out var runtime))
        {
            previousBaseMaximumHealth = runtime.BaseMaximumHealth;
            previousStaticMaximumHealthBonus = runtime.Modifiers.MaximumHealthBonus
                + runtime.Modifiers.UniversalModifiers.MaximumHealthBonus;
            if (runtime.Modifiers.DamageHealingFraction != modifiers.DamageHealingFraction)
            {
                runtime.DamageHealingRemainder = 0;
            }
            if (runtime.Modifiers.SniperProfile?.AvariceEnabled
                != modifiers.SniperProfile?.AvariceEnabled)
            {
                runtime.ScopedDamageHealingRemainder = 0;
            }
            if (runtime.Modifiers.ScopedHealingPerSecond != modifiers.ScopedHealingPerSecond)
            {
                runtime.ScopedHealingAccumulator = 0f;
            }
            if (runtime.Modifiers.CloakedHealingPerSecond != modifiers.CloakedHealingPerSecond)
            {
                runtime.CloakedHealingAccumulator = 0f;
            }
            if (runtime.Modifiers.MedicHomeostasisHealingFraction != modifiers.MedicHomeostasisHealingFraction)
            {
                runtime.MedicHomeostasisHealingRemainder = 0;
            }
            if (runtime.Modifiers.MedicSpikedVestEnabled != modifiers.MedicSpikedVestEnabled)
            {
                runtime.MedicSpikedVestReflectionRemainder = 0;
                runtime.UniversalReflectionRemainder = 0;
                runtime.UniversalHealingAccumulator = 0f;
                runtime.InfiniteSlayWorksDamageAccumulator = 0f;
            }
            if (runtime.Modifiers.MedicSupportRelayEnabled != modifiers.MedicSupportRelayEnabled)
            {
                runtime.MedicSupportRelayActiveLinkTargetPlayerId = null;
            }
            if (resetDynamicState)
            {
                runtime.MedicSupportRelayActiveLinkTargetPlayerId = null;
                runtime.MedicSupportRelayCooldownUntilFrameByTargetPlayerId.Clear();
            }
            if (runtime.Modifiers.CloakedEvasionChance != modifiers.CloakedEvasionChance)
            {
                runtime.ShroudGraceTicksRemaining = 0;
                runtime.WasSpyCloaked = player.IsAlive
                    && player.ClassId == PlayerClass.Spy
                    && player.IsSpyCloaked;
                runtime.EvasionRandom ??= CreateLastToDieEvasionRandom(slot);
            }
            if (modifiers.SniperProfile is { OverkillerEnabled: true })
            {
                runtime.OverkillerRandom ??= _host.LastToDieState.CombatSeedConfigured
                    ? CreateLastToDieOverkillerRandom(slot)
                    : null;
            }
            runtime.Modifiers = modifiers;
            runtime.LegacySettings = legacySettings;
            runtime.BaseMaximumHealth = Math.Max(
                1,
                baseMaximumHealthOverride ?? GetLastToDieBaseMaximumHealth(player));
            runtime.SecondChanceConsumed = secondChanceConsumed;
            _host.LastToDieState.LegacyGameplaySettingsBySlot[slot] = legacySettings;
        }
        else
        {
            runtime = new LastToDiePlayerPerkRuntime(modifiers)
            {
                LegacySettings = legacySettings,
                BaseMaximumHealth = Math.Max(
                    1,
                    baseMaximumHealthOverride ?? GetLastToDieBaseMaximumHealth(player)),
                SecondChanceConsumed = secondChanceConsumed,
                RevolverCriticalRandom = _host.LastToDieState.CombatSeedConfigured
                    ? CreateLastToDieRevolverCriticalRandom(slot)
                    : null,
                EvasionRandom = CreateLastToDieEvasionRandom(slot),
                OverkillerRandom = _host.LastToDieState.CombatSeedConfigured
                    ? CreateLastToDieOverkillerRandom(slot)
                    : null,
                WasSpyCloaked = player.IsAlive
                    && player.ClassId == PlayerClass.Spy
                    && player.IsSpyCloaked,
            };
            _host.LastToDieState.PerkRuntimesBySlot.Add(slot, runtime);
            _host.LastToDieState.LegacyGameplaySettingsBySlot[slot] = legacySettings;
        }

        player.ConfigureLastToDieUniversalModifiers(
            modifiers.UniversalModifiers,
            runKills,
            secondChanceConsumed,
            runKillProgressionOwner);

        // Class/respawn synchronization ran before the reward build was
        // installed. Reapply the per-slot legacy profile now so the first
        // hosted stage immediately gets the original Demo/Soldier/Engineer
        // loadout state instead of waiting for a later resync tick.
        _host.SyncExperimentalGameplayLoadout(slot, player);

        player.SetLastToDieSpyRevolverProfile(modifiers.SpyRevolverProfile, refillHealth);
        player.SetLastToDieCloakedPerkMultipliers(
            modifiers.CloakedMovementSpeedMultiplier,
            modifiers.CloakedDamageTakenMultiplier);
        player.ConfigureLastToDieSpyCloakMeter(
            modifiers.RogueCommanderEnabled,
            modifiers.ProfessionalEnabled,
            _host.Config.TicksPerSecond,
            resetDynamicState);
        player.ConfigureLastToDieSpyStabAndJumpBootPerks(
            modifiers.MultistabEnabled,
            modifiers.SpringLoadedEnabled,
            modifiers.InstastabEnabled,
            modifiers.HealstabEnabled,
            modifiers.HealingHarnessEnabled,
            modifiers.DoubleJumpEnabled,
            resetDynamicState);
        player.ConfigureLastToDieSpyInfiltrate(
            modifiers.InfiltrateEnabled,
            _host.Config.TicksPerSecond,
            resetDynamicState);
        if (!modifiers.AfterlifeEnabled || resetDynamicState)
        {
            ResetLastToDieSpyAfterlifeRuntime(slot, player);
        }
        player.ConfigureLastToDieSpyAfterlife(
            modifiers.AfterlifeEnabled,
            _host.Config.TicksPerSecond,
            resetDynamicState);
        player.ConfigureLastToDieMedicSelfPerks(
            modifiers.MedicCombatMedicEnabled,
            modifiers.MedicSpikedVestEnabled,
            modifiers.MedicIronWillEnabled,
            modifiers.MedicModifiedSpringEnabled,
            resetDynamicState);
        player.ConfigureLastToDieMedicRejuvenationRay(modifiers.MedicRejuvenationRayEnabled);
        player.ConfigureLastToDieMedicKritPower(modifiers.MedicKritPowerEnabled);
        player.SetLastToDieSniperProfile(modifiers.SniperProfile);

        var baseMaximumHealth = modifiers.UniversalModifiers.BaseMaximumHealthOverride
            ?? runtime.BaseMaximumHealth;
        var staticMaximumHealthBonus = modifiers.MaximumHealthBonus
            + modifiers.UniversalModifiers.MaximumHealthBonus;
        var runKillMaximumHealthBonus = checked(
            modifiers.UniversalModifiers.MaximumHealthPerRunKill * Math.Max(0, runKills));
        var configured = _host.TrySetNetworkPlayerMaxHealthOverride(
            slot,
            checked(baseMaximumHealth + staticMaximumHealthBonus + runKillMaximumHealthBonus),
            refillHealth);
        if (configured
            && !refillHealth
            && player.IsAlive
            && (staticMaximumHealthBonus > previousStaticMaximumHealthBonus
                || (!baseMaximumHealthOverride.HasValue
                    && runtime.BaseMaximumHealth > previousBaseMaximumHealth)))
        {
            player.ForceSetHealth(checked(
                healthBeforeConfiguration
                    + (staticMaximumHealthBonus - previousStaticMaximumHealthBonus)
                    + (!baseMaximumHealthOverride.HasValue
                        ? Math.Max(0, runtime.BaseMaximumHealth - previousBaseMaximumHealth)
                        : 0)));
        }

        _ = _host.TrySetNetworkPlayerScale(slot, _host.MatchSettings.PlayerScale * modifiers.UniversalModifiers.PlayerScale);

        return configured;
    }

    internal static int GetLastToDieBaseMaximumHealth(PlayerEntity player)
    {
        var classBonus = player.ClassId is PlayerClass.Sniper
            or PlayerClass.Medic
            or PlayerClass.Spy
            or PlayerClass.Demoman
            ? 40
            : 0;
        return checked(player.ClassDefinition.MaxHealth + classBonus);
    }

    internal bool TrySetLastToDiePlayerRunKills(byte slot, int runKills)
    {
        if (!_host.TryGetNetworkPlayer(slot, out var player)
            || !_host.LastToDieState.PerkRuntimesBySlot.TryGetValue(slot, out var runtime))
        {
            return false;
        }

        var normalizedKills = Math.Max(0, runKills);
        if (player.LastToDieRunKills == normalizedKills)
        {
            return true;
        }

        player.SetLastToDieRunKills(normalizedKills);
        var universal = runtime.Modifiers.UniversalModifiers;
        if (universal.MaximumHealthPerRunKill > 0)
        {
            var baseMaximumHealth = universal.BaseMaximumHealthOverride
                ?? runtime.BaseMaximumHealth;
            var staticMaximumHealthBonus = runtime.Modifiers.MaximumHealthBonus
                + universal.MaximumHealthBonus;
            var runKillMaximumHealthBonus = checked(
                universal.MaximumHealthPerRunKill * normalizedKills);
            _ = _host.TrySetNetworkPlayerMaxHealthOverride(
                slot,
                checked(baseMaximumHealth + staticMaximumHealthBonus + runKillMaximumHealthBonus),
                refillHealth: false);
        }

        return true;
    }

    internal bool TryGetLastToDieSecondChanceConsumed(byte slot, out bool consumed)
    {
        consumed = _host.LastToDieState.PerkRuntimesBySlot.TryGetValue(slot, out var runtime)
            && runtime.SecondChanceConsumed;
        return _host.LastToDieState.PerkRuntimesBySlot.ContainsKey(slot);
    }

    internal bool TryActivateLastToDieSecondChance(PlayerEntity player)
    {
        if (!_host.TryGetPlayerNetworkSlot(player, out var slot)
            || !_host.LastToDieState.PerkRuntimesBySlot.TryGetValue(slot, out var runtime)
            || !runtime.Modifiers.UniversalModifiers.SecondChance
            || runtime.SecondChanceConsumed
            || player.LastToDieSecondChanceConsumed)
        {
            return false;
        }

        ClearLastToDieStatusEffectsForTarget(player.Id);
        player.ExtinguishAfterburn();
        if (!player.ActivateLastToDieSecondChance(Math.Max(1, _host.Config.TicksPerSecond * 2)))
        {
            return false;
        }

        runtime.SecondChanceConsumed = true;
        player.ForceSetHealth(Math.Max(1, (player.MaxHealth + 1) / 2));
        _host.MarkPendingFatalPlayerDamageEventPrevented(player.Id);
        return true;
    }

    internal void ApplyLastToDieKillRewards(PlayerEntity killer)
    {
        if (!_host.TryGetPlayerNetworkSlot(killer, out var slot)
            || !_host.LastToDieState.PerkRuntimesBySlot.TryGetValue(slot, out var runtime))
        {
            return;
        }

        if (runtime.Modifiers.UniversalModifiers.HealPerKill > 0 && killer.IsAlive)
        {
            _host.ApplyHealingWithFeedback(killer, runtime.Modifiers.UniversalModifiers.HealPerKill);
        }

        if (killer.LastToDieRunKillProgressionOwner)
        {
            _ = TrySetLastToDiePlayerRunKills(slot, checked(killer.LastToDieRunKills + 1));
        }
    }

    /// <summary>
    /// Gets the authoritative legacy settings for a hosted Last to Die
    /// participant. Practice/offline callers that have no Last to Die build
    /// continue to use the world-level experimental settings.
    /// </summary>
    internal bool TryGetLastToDieLegacyGameplaySettings(
        byte slot,
        out ExperimentalGameplaySettings settings)
    {
        if (_host.LastToDieState.LegacyGameplaySettingsBySlot.TryGetValue(slot, out var slotSettings))
        {
            settings = slotSettings;
            return true;
        }

        if (_host.LastToDieState.PerkRuntimesBySlot.TryGetValue(slot, out var runtime))
        {
            settings = runtime.LegacySettings;
            return true;
        }

        settings = _host.ExperimentalGameplaySettings;
        return false;
    }

    internal ExperimentalGameplaySettings GetLastToDieGameplaySettings(PlayerEntity? player)
    {
        return player is not null
            && _host.TryGetPlayerNetworkSlot(player, out var slot)
            && TryGetLastToDieLegacyGameplaySettings(slot, out var settings)
            ? settings
            : _host.ExperimentalGameplaySettings;
    }

    /// <summary>
    /// Resolves settings that are intentionally run-global in the original
    /// Last to Die rules (rage, drops, and the captured-point aura). Hosted
    /// participants keep those defaults in their per-slot profiles, so these
    /// gates must not depend on whichever world-level practice record happens
    /// to be installed.
    /// </summary>
    internal bool IsLastToDieGameplaySettingEnabled(
        Func<ExperimentalGameplaySettings, bool> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        if (_host.LastToDieState.PerkRuntimesBySlot.Count == 0
            && _host.LastToDieState.LegacyGameplaySettingsBySlot.Count == 0)
        {
            return selector(_host.ExperimentalGameplaySettings);
        }

        return _host.LastToDieState.PerkRuntimesBySlot.Values.Any(runtime => selector(runtime.LegacySettings))
            || _host.LastToDieState.LegacyGameplaySettingsBySlot.Values.Any(selector);
    }

    /// <summary>
    /// Applies only the deterministic client prediction/presentation portion
    /// of an authoritative Last to Die build. Healing, evasion, and damage
    /// rewards remain exclusively in the server-owned perk runtime.
    /// </summary>
    internal bool TryApplyLastToDiePlayerPredictionProfile(
        byte slot,
        IEnumerable<string> ownedPerkIds,
        int runKills = 0,
        bool secondChanceConsumed = false)
    {
        ArgumentNullException.ThrowIfNull(ownedPerkIds);
        if (!_host.TryGetNetworkPlayer(slot, out var player))
        {
            return false;
        }

        var ownedPerks = ownedPerkIds
            .Select(static perkId => new LastToDiePerkId(perkId))
            .Distinct()
            .ToArray();
        _host.LastToDieState.LegacyGameplaySettingsBySlot[slot] = LastToDieLegacyPerkSettings.FromPerks(
            player.ClassId,
            ownedPerks,
            _host.ExperimentalGameplaySettings);
        var modifiers = LastToDieDerivedModifiers.FromPerks(ownedPerks);
        player.ConfigureLastToDieUniversalModifiers(
            modifiers.UniversalModifiers,
            runKills,
            secondChanceConsumed,
            runKillProgressionOwner: false);
        _ = _host.TrySetNetworkPlayerScale(slot, _host.MatchSettings.PlayerScale * modifiers.UniversalModifiers.PlayerScale);
        ApplyLastToDiePlayerPredictionModifiers(slot, player, modifiers);
        return true;
    }

    private void ApplyLastToDiePlayerPredictionModifiers(
        byte slot,
        PlayerEntity player,
        LastToDieDerivedModifiers modifiers)
    {
        player.SetLastToDieSpyRevolverProfile(modifiers.SpyRevolverProfile, refillAmmo: false);
        player.SetLastToDieCloakedPerkMultipliers(
            modifiers.CloakedMovementSpeedMultiplier,
            modifiers.CloakedDamageTakenMultiplier);
        player.ConfigureLastToDieSpyCloakMeter(
            modifiers.RogueCommanderEnabled,
            modifiers.ProfessionalEnabled,
            _host.Config.TicksPerSecond,
            resetDynamicState: false);
        player.ConfigureLastToDieSpyStabAndJumpBootPerks(
            modifiers.MultistabEnabled,
            modifiers.SpringLoadedEnabled,
            modifiers.InstastabEnabled,
            modifiers.HealstabEnabled,
            modifiers.HealingHarnessEnabled,
            modifiers.DoubleJumpEnabled,
            resetDynamicState: false);
        player.ConfigureLastToDieSpyInfiltrate(
            modifiers.InfiltrateEnabled,
            _host.Config.TicksPerSecond,
            resetDynamicState: false);
        if (!modifiers.AfterlifeEnabled)
        {
            ResetLastToDieSpyAfterlifeRuntime(slot, player);
        }
        player.ConfigureLastToDieSpyAfterlife(
            modifiers.AfterlifeEnabled,
            _host.Config.TicksPerSecond,
            resetDynamicState: false);
        player.ConfigureLastToDieMedicSelfPerks(
            modifiers.MedicCombatMedicEnabled,
            modifiers.MedicSpikedVestEnabled,
            modifiers.MedicIronWillEnabled,
            modifiers.MedicModifiedSpringEnabled,
            resetDynamicState: false);
        player.ConfigureLastToDieMedicRejuvenationRay(modifiers.MedicRejuvenationRayEnabled);
        player.ConfigureLastToDieMedicKritPower(modifiers.MedicKritPowerEnabled);
        player.SetLastToDieSniperProfile(modifiers.SniperProfile);
        player.ConfigureLastToDieUniversalModifiers(
            modifiers.UniversalModifiers,
            player.LastToDieRunKills,
            player.LastToDieSecondChanceConsumed,
            player.LastToDieRunKillProgressionOwner);
        _host.SyncExperimentalGameplayLoadout(slot, player);
    }

    internal void ResetLastToDieClientSession()
    {
        ConfigureLastToDieStage(0);
        ClearLastToDiePlayerPredictionProfile(SimulationConstants.LocalPlayerSlot);
        _host.TrySetNetworkPlayerAutomaticRespawnSuppressed(SimulationConstants.LocalPlayerSlot, false);
    }

    internal bool ClearLastToDiePlayerPredictionProfile(byte slot)
    {
        if (!_host.TryGetNetworkPlayer(slot, out var player))
        {
            return false;
        }

        _host.LastToDieState.LegacyGameplaySettingsBySlot.Remove(slot);
        player.ConfigureLastToDieUniversalModifiers(
            new LastToDieUniversalModifiers(),
            runKills: 0,
            secondChanceConsumed: false,
            runKillProgressionOwner: false);
        _ = _host.TrySetNetworkPlayerScale(slot, _host.MatchSettings.PlayerScale);
        ApplyLastToDiePlayerPredictionModifiers(
            slot,
            player,
            new LastToDieDerivedModifiers());
        return true;
    }

    internal bool TryRollLastToDieSpyDeadlyCritical(PlayerEntity attacker)
    {
        if (attacker.ClassId != PlayerClass.Spy
            || !attacker.LastToDieSpyRevolverProfile.DeadlyEnabled
            || !_host.TryGetPlayerNetworkSlot(attacker, out var slot)
            || !_host.LastToDieState.PerkRuntimesBySlot.TryGetValue(slot, out var runtime)
            || runtime.Modifiers.SpyRevolverProfile is not { DeadlyEnabled: true }
            || runtime.RevolverCriticalRandom is null)
        {
            return false;
        }

        return runtime.RevolverCriticalRandom.NextUInt32()
            < (uint)(LastToDieSpyRevolverProfile.DeadlyCriticalChance * (uint.MaxValue + 1d));
    }

    private LastToDieRandom CreateLastToDieRevolverCriticalRandom(byte slot)
    {
        var stream = LastToDieRandom.DeriveSeed(_host.LastToDieState.CombatSeed, slot);
        return new LastToDieRandom(
            LastToDieRandom.DeriveSeed(_host.LastToDieState.CombatSeed, stream),
            stream);
    }

    private LastToDieRandom CreateLastToDieEvasionRandom(byte slot)
    {
        const ulong evasionStreamDomain = 0x4556_4153_494F_4EUL;
        var domainSeed = _host.LastToDieState.CombatSeed ^ evasionStreamDomain;
        var stream = LastToDieRandom.DeriveSeed(domainSeed, slot);
        return new LastToDieRandom(
            LastToDieRandom.DeriveSeed(domainSeed, stream),
            stream);
    }

    private LastToDieRandom CreateLastToDieOverkillerRandom(byte slot)
    {
        const ulong overkillerStreamDomain = 0x4F56_4552_4B49_4C4CUL;
        var domainSeed = _host.LastToDieState.CombatSeed ^ overkillerStreamDomain;
        var stream = LastToDieRandom.DeriveSeed(domainSeed, slot);
        return new LastToDieRandom(
            LastToDieRandom.DeriveSeed(domainSeed, stream),
            stream);
    }

    internal bool TryGetLastToDiePlayerModifiers(
        byte slot,
        out LastToDieDerivedModifiers modifiers)
    {
        if (_host.LastToDieState.PerkRuntimesBySlot.TryGetValue(slot, out var runtime))
        {
            modifiers = runtime.Modifiers;
            return true;
        }

        modifiers = new LastToDieDerivedModifiers();
        return false;
    }

    internal LastToDieMedicKritzM2Payload CaptureLastToDieMedicKritzM2Payload(
        PlayerEntity owner)
    {
        var appliesHailMary = false;
        var appliesNeurotoxin = false;
        var appliesJavelin = false;
        if (owner.ClassId == PlayerClass.Medic
            && _host.TryGetPlayerNetworkSlot(owner, out var slot)
            && _host.LastToDieState.PerkRuntimesBySlot.TryGetValue(slot, out var runtime))
        {
            appliesHailMary = runtime.Modifiers.MedicHailMaryEnabled;
            appliesNeurotoxin = runtime.Modifiers.MedicNeurotoxinEnabled;
            appliesJavelin = runtime.Modifiers.MedicJavelinEnabled;
        }

        return LastToDieMedicKritzM2Payload.Create(
            appliesHailMary,
            appliesNeurotoxin,
            appliesJavelin);
    }

    internal bool TryGetLastToDieSniperConquistadorStacks(byte slot, out int stacks)
    {
        if (_host.TryGetNetworkPlayer(slot, out var player)
            && player.ClassId == PlayerClass.Sniper
            && _host.LastToDieState.PerkRuntimesBySlot.TryGetValue(slot, out var runtime)
            && runtime.Modifiers.SniperProfile is { ConquistadorEnabled: true }
            && player.LastToDieSniperProfile.ConquistadorEnabled)
        {
            stacks = player.LastToDieSniperConquistadorStacks;
            return true;
        }

        stacks = 0;
        return false;
    }

    internal bool TryRestoreLastToDieSniperConquistadorStacks(byte slot, int stacks)
    {
        if (!_host.TryGetNetworkPlayer(slot, out var player)
            || player.ClassId != PlayerClass.Sniper
            || !_host.LastToDieState.PerkRuntimesBySlot.TryGetValue(slot, out var runtime)
            || runtime.Modifiers.SniperProfile is not { ConquistadorEnabled: true }
            || !player.LastToDieSniperProfile.ConquistadorEnabled)
        {
            return false;
        }

        player.RestoreLastToDieSniperConquistadorStacks(stacks);
        return true;
    }

    internal bool CanPlayerCaptureControlPointsWhileCloaked(PlayerEntity player)
    {
        if (player.ClassId != PlayerClass.Spy)
        {
            return false;
        }

        return _host.TryGetPlayerNetworkSlot(player, out var slot)
            && _host.LastToDieState.PerkRuntimesBySlot.TryGetValue(slot, out var runtime)
            && runtime.Modifiers.RogueCommanderEnabled;
    }

    internal bool CanPlayerCaptureControlPointsWhileUbered(PlayerEntity player)
    {
        return player.IsAlive
            && player.ClassId == PlayerClass.Medic
            && player.IsMedicRegularUberDeliveryActive
            && _host.TryGetPlayerNetworkSlot(player, out var slot)
            && _host.LastToDieState.PerkRuntimesBySlot.TryGetValue(slot, out var runtime)
            && runtime.Modifiers.MedicFieldCommanderEnabled;
    }

    internal bool CanPlayerContributeToControlPoint(PlayerEntity player) =>
        !player.IsLastToDieSpyAfterlifeActive
        && (!player.IsSpyCloaked || CanPlayerCaptureControlPointsWhileCloaked(player))
        && (!player.IsMedicRegularUberDeliveryActive
            || CanPlayerCaptureControlPointsWhileUbered(player));

    internal void ApplyLastToDieDamageRewards(
        PlayerEntity? attacker,
        PlayerEntity target,
        int appliedDamage,
        PlayerDamageTraits damageTraits)
    {
        TryEstablishLastToDieSniperSpottedMark(
            attacker,
            target,
            appliedDamage,
            damageTraits);
        TryApplyLastToDieSniperOverkiller(
            attacker,
            target,
            appliedDamage,
            damageTraits);
        if (attacker is null
            || appliedDamage <= 0
            || damageTraits.HasFlag(PlayerDamageTraits.Reflected)
            || !attacker.IsAlive
            || ReferenceEquals(attacker, target)
            || attacker.Team == target.Team
            || !_host.TryGetPlayerNetworkSlot(attacker, out var slot)
            || !_host.LastToDieState.PerkRuntimesBySlot.TryGetValue(slot, out var runtime)
            || attacker.Health >= attacker.MaxHealth)
        {
            return;
        }

        var hasVampire = runtime.Modifiers.DamageHealingFraction > 0f;
        var hasAvarice = attacker.ClassId == PlayerClass.Sniper
            && attacker.IsSniperScoped
            && runtime.Modifiers.SniperProfile is { AvariceEnabled: true }
            && attacker.LastToDieSniperProfile.AvariceEnabled;
        if (!hasVampire && !hasAvarice)
        {
            return;
        }

        if (hasAvarice)
        {
            const int avariceDenominator = 10;
            var avariceNumerator = (long)MathF.Round(
                LastToDieSniperProfile.AvariceLifestealFraction * avariceDenominator);
            var scaledAvariceHealing = runtime.ScopedDamageHealingRemainder
                + (appliedDamage * avariceNumerator);
            var wholeAvariceHealing = (int)(scaledAvariceHealing / avariceDenominator);
            runtime.ScopedDamageHealingRemainder = (int)(scaledAvariceHealing % avariceDenominator);
            if (wholeAvariceHealing > 0)
            {
                _host.ApplyHealingWithFeedback(attacker, wholeAvariceHealing);
            }
        }

        if (hasVampire)
        {
            var scaledHealing = runtime.DamageHealingRemainder
                + (appliedDamage * (long)LastToDieDerivedModifiers.SpyVampireHealingNumerator);
            var wholeHealing = (int)(scaledHealing / LastToDieDerivedModifiers.SpyVampireHealingDenominator);
            runtime.DamageHealingRemainder = (int)(scaledHealing % LastToDieDerivedModifiers.SpyVampireHealingDenominator);
            if (wholeHealing > 0)
            {
                _host.ApplyHealingWithFeedback(attacker, wholeHealing);
            }
        }
    }

    private void TryApplyLastToDieSniperOverkiller(
        PlayerEntity? attacker,
        PlayerEntity target,
        int appliedDamage,
        PlayerDamageTraits damageTraits)
    {
        if (attacker is null
            || appliedDamage <= 0
            || target.Health <= 0
            || !damageTraits.HasFlag(PlayerDamageTraits.LastToDieOverkillerEligible)
            || damageTraits.HasFlag(PlayerDamageTraits.LastToDieOverkillerFollowUp)
            || damageTraits.HasFlag(PlayerDamageTraits.Periodic)
            || damageTraits.HasFlag(PlayerDamageTraits.Reflected)
            || !attacker.IsAlive
            || attacker.ClassId != PlayerClass.Sniper
            || ReferenceEquals(attacker, target)
            || attacker.Team == target.Team
            || !_host.TryGetPlayerNetworkSlot(attacker, out var slot)
            || !_host.LastToDieState.PerkRuntimesBySlot.TryGetValue(slot, out var runtime)
            || runtime.Modifiers.SniperProfile is not { OverkillerEnabled: true }
            || !attacker.LastToDieSniperProfile.OverkillerEnabled
            || runtime.OverkillerRandom is null
            || runtime.OverkillerRandom.NextUInt32()
                >= (uint)(LastToDieSniperProfile.OverkillerChance * (uint.MaxValue + 1d)))
        {
            return;
        }

        var executeResolution = _host.ResolvePlayerDamage(
            target,
            new PlayerDamageRequest(
                PlayerDamageApplicationKind.Instant,
                Amount: 1f,
                Attacker: attacker,
                SpyRevealAlpha: PlayerEntity.SpySniperRevealAlpha,
                EventFlags: DamageEventFlags.None,
                Traits: PlayerDamageTraits.ExecuteAfterDefenses
                    | PlayerDamageTraits.LastToDieOverkillerFollowUp,
                AllowOsmosisHealOwnedSentries: false,
                Umbrella: new PlayerDamageUmbrellaOptions(AllowBlock: false),
                AttackerWasGrounded: attacker.IsGrounded,
                TargetWasGrounded: target.IsGrounded,
                FatalWeaponSpriteName: "RifleKL"));
        if (executeResolution.WasFatal)
        {
            _host.KillPlayer(target, killer: attacker, weaponSpriteName: "RifleKL");
        }
    }

    private void TryEstablishLastToDieSniperSpottedMark(
        PlayerEntity? attacker,
        PlayerEntity target,
        int appliedDamage,
        PlayerDamageTraits damageTraits)
    {
        if (attacker is null
            || appliedDamage <= 0
            || !damageTraits.HasFlag(PlayerDamageTraits.EstablishLastToDieSpotted)
            || damageTraits.HasFlag(PlayerDamageTraits.Periodic)
            || damageTraits.HasFlag(PlayerDamageTraits.Reflected)
            || !attacker.IsAlive
            || attacker.ClassId != PlayerClass.Sniper
            || ReferenceEquals(attacker, target)
            || attacker.Team == target.Team
            || !_host.TryGetPlayerNetworkSlot(attacker, out var attackerSlot)
            || !_host.LastToDieState.PerkRuntimesBySlot.TryGetValue(attackerSlot, out var runtime)
            || runtime.Modifiers.SniperProfile is not { SpottedEnabled: true }
            || !attacker.LastToDieSniperProfile.SpottedEnabled
            || !_host.TryGetPlayerNetworkSlot(target, out var targetSlot))
        {
            return;
        }

        attacker.SetLastToDieSniperMarkedTargetSlot(targetSlot);
    }

    internal void TryRegisterLastToDieSniperConquistadorKill(
        PlayerEntity killer,
        PlayerEntity victim)
    {
        if (!killer.IsAlive
            || killer.ClassId != PlayerClass.Sniper
            || killer.Team == victim.Team
            || !_host.TryGetPlayerNetworkSlot(killer, out var killerSlot)
            || !_host.LastToDieState.PerkRuntimesBySlot.TryGetValue(killerSlot, out var runtime)
            || runtime.Modifiers.SniperProfile is not { ConquistadorEnabled: true }
            || !killer.LastToDieSniperProfile.ConquistadorEnabled)
        {
            return;
        }

        _ = killer.TryIncrementLastToDieSniperConquistadorStacks();
    }

    internal void ClearLastToDieSniperMarksTargeting(byte targetSlot)
    {
        foreach (var player in _host.EnumerateSimulatedPlayers())
        {
            if (player.LastToDieSniperMarkedTargetSlot == targetSlot)
            {
                player.ClearLastToDieSniperMarkedTarget();
            }
        }
    }

    internal float GetLastToDieMedicHealingMultiplier(PlayerEntity medic, PlayerEntity target)
    {
        if (medic.ClassId != PlayerClass.Medic
            || target.MaxHealth <= 0
            || !_host.TryGetPlayerNetworkSlot(medic, out var slot)
            || !_host.LastToDieState.PerkRuntimesBySlot.TryGetValue(slot, out var runtime)
            || !runtime.Modifiers.MedicTraumaSurgeonEnabled)
        {
            return 1f;
        }

        var healthFraction = Math.Clamp(
            target.Health / (float)target.MaxHealth,
            LastToDieDerivedModifiers.MedicTraumaSurgeonMaximumHealingHealthFraction,
            1f);
        var missingFractionAcrossRamp = (1f - healthFraction)
            / (1f - LastToDieDerivedModifiers.MedicTraumaSurgeonMaximumHealingHealthFraction);
        return 1f + (missingFractionAcrossRamp
            * (LastToDieDerivedModifiers.MedicTraumaSurgeonMaximumHealingMultiplier - 1f));
    }

    internal float GetLastToDieMedicUberChargeGainMultiplier(PlayerEntity medic)
    {
        if (medic.ClassId != PlayerClass.Medic
            || !_host.TryGetPlayerNetworkSlot(medic, out var slot)
            || !_host.LastToDieState.PerkRuntimesBySlot.TryGetValue(slot, out var runtime))
        {
            return 1f;
        }

        return MathF.Max(1f, runtime.Modifiers.MedicUberChargeGainMultiplier);
    }

    internal static float GetLastToDieMedicKritzCriticalDamageMultiplier(PlayerEntity medic)
    {
        if (!medic.LastToDieMedicKritPowerEnabled)
        {
            return ExperimentalGameplaySettings.KritzCriticalDamageMultiplier;
        }

        return LastToDieDerivedModifiers.MedicKritPowerCriticalDamageMultiplier;
    }

    internal void ApplyLastToDieMedicHomeostasis(PlayerEntity medic, int appliedTargetHealing)
    {
        if (appliedTargetHealing <= 0
            || !medic.IsAlive
            || medic.ClassId != PlayerClass.Medic
            || !_host.TryGetPlayerNetworkSlot(medic, out var slot)
            || !_host.LastToDieState.PerkRuntimesBySlot.TryGetValue(slot, out var runtime)
            || runtime.Modifiers.MedicHomeostasisHealingFraction <= 0f)
        {
            return;
        }

        if (medic.Health >= medic.MaxHealth)
        {
            runtime.MedicHomeostasisHealingRemainder = 0;
            return;
        }

        var scaledHealing = runtime.MedicHomeostasisHealingRemainder
            + (appliedTargetHealing * LastToDieDerivedModifiers.MedicHomeostasisHealingNumerator);
        var wholeHealing = scaledHealing / LastToDieDerivedModifiers.MedicHomeostasisHealingDenominator;
        runtime.MedicHomeostasisHealingRemainder =
            scaledHealing % LastToDieDerivedModifiers.MedicHomeostasisHealingDenominator;
        if (wholeHealing <= 0)
        {
            return;
        }

        _host.ApplyHealingWithFeedback(medic, wholeHealing);
    }

    internal void ApplyLastToDieDamageTakenEffects(
        PlayerEntity target,
        PlayerEntity? attacker,
        int appliedDamage,
        PlayerDamageTraits damageTraits)
    {
        if (appliedDamage <= 0
            || attacker is null
            || !attacker.IsAlive
            || ReferenceEquals(attacker, target)
            || attacker.Team == target.Team
            || damageTraits.HasFlag(PlayerDamageTraits.Periodic)
            || damageTraits.HasFlag(PlayerDamageTraits.Reflected))
        {
            return;
        }

        if (!_host.TryGetPlayerNetworkSlot(target, out var slot)
            || !_host.LastToDieState.PerkRuntimesBySlot.TryGetValue(slot, out var runtime))
        {
            return;
        }

        var universal = runtime.Modifiers.UniversalModifiers;
        if (damageTraits.HasFlag(PlayerDamageTraits.CanApplyOnHitEffects)
            && universal.FreezingArmor)
        {
            _ = TryApplyLastToDieStatusEffect(
                attacker.Id,
                target.Id,
                LastToDieStatusEffectSpec.Slow(
                    LastToDieStatusEffectIds.RareFreezingArmor,
                    Math.Max(1, _host.Config.TicksPerSecond * 3),
                    0.9f,
                    fireSpeedMultiplier: 0.8f,
                    reloadSpeedMultiplier: 0.8f));
        }

        if (damageTraits.HasFlag(PlayerDamageTraits.CanApplyOnHitEffects)
            && universal.BlazingArmor)
        {
            attacker.IgniteAfterburn(
                target.Id,
                durationIncreaseSourceTicks: 60f,
                intensityIncrease: 2.5f,
                afterburnFalloff: false,
                burnFalloffAmount: 0f,
                killFeedWeaponSpriteName: "FlameKL");
        }

        if (!damageTraits.HasFlag(PlayerDamageTraits.CanReflect))
        {
            return;
        }

        var reflectTenths = (target.ClassId == PlayerClass.Medic
                && runtime.Modifiers.MedicSpikedVestEnabled
                    ? LastToDieDerivedModifiers.MedicSpikedVestReflectionNumerator
                    : 0)
            + (universal.ReflectionFraction > 0f
                ? (int)MathF.Round(universal.ReflectionFraction * 10f)
                : 0)
            + (universal.FatalBravado
                && target.MaxHealth > 0
                && target.Health * 2L < target.MaxHealth
                    ? 5
                    : 0);
        reflectTenths = Math.Clamp(reflectTenths, 0, 10);
        if (reflectTenths == 0)
        {
            return;
        }

        var scaledReflection = runtime.UniversalReflectionRemainder + (appliedDamage * reflectTenths);
        var reflectedDamage = scaledReflection / 10;
        runtime.UniversalReflectionRemainder = scaledReflection % 10;
        ApplyLastToDieReflectedDamage(target, attacker, reflectedDamage);
    }

    private void ApplyLastToDieReflectedDamage(
        PlayerEntity reflector,
        PlayerEntity attacker,
        int reflectedDamage)
    {
        if (reflectedDamage <= 0 || !attacker.IsAlive)
        {
            return;
        }

        var resolution = _host.ResolvePlayerDamage(
            attacker,
            new PlayerDamageRequest(
                PlayerDamageApplicationKind.Instant,
                reflectedDamage,
                reflector,
                PlayerEntity.SpyDamageRevealAlpha,
                DamageEventFlags.None,
                PlayerDamageTraits.Reflected,
                AllowOsmosisHealOwnedSentries: false,
                new PlayerDamageUmbrellaOptions(AllowBlock: false)));
        if (resolution.WasFatal)
        {
            _host.KillPlayer(attacker, killer: reflector);
        }
    }

    internal int ApplyLastToDieOutgoingDamageMultiplier(
        PlayerEntity? attacker,
        PlayerEntity target,
        int damage,
        PlayerDamageTraits damageTraits,
        bool? attackerWasGrounded,
        bool? targetWasGrounded)
    {
        var multiplier = GetLastToDieOutgoingDamageMultiplier(
            attacker,
            target,
            damageTraits,
            attackerWasGrounded,
            targetWasGrounded);
        return MathF.Abs(multiplier - 1f) <= 0.0001f
            ? damage
            : Math.Max(1, (int)MathF.Round(damage * multiplier));
    }

    internal float ApplyLastToDieOutgoingDamageMultiplier(
        PlayerEntity? attacker,
        PlayerEntity target,
        float damage,
        PlayerDamageTraits damageTraits,
        bool? attackerWasGrounded,
        bool? targetWasGrounded)
    {
        var multiplier = GetLastToDieOutgoingDamageMultiplier(
            attacker,
            target,
            damageTraits,
            attackerWasGrounded,
            targetWasGrounded);
        return MathF.Abs(multiplier - 1f) <= 0.0001f
            ? damage
            : MathF.Max(0.01f, damage * multiplier);
    }

    private float GetLastToDieOutgoingDamageMultiplier(
        PlayerEntity? attacker,
        PlayerEntity target,
        PlayerDamageTraits damageTraits,
        bool? attackerWasGrounded,
        bool? targetWasGrounded)
    {
        if (attacker is null
            || damageTraits.HasFlag(PlayerDamageTraits.Reflected)
            || !attacker.IsAlive
            || ReferenceEquals(attacker, target)
            || attacker.Team == target.Team)
        {
            return 1f;
        }

        var multiplier = attacker.LastToDieStatusOutgoingDamageMultiplier
            * attacker.LastToDieMedicLinkOutgoingDamageMultiplier;
        if (attacker.ClassId == PlayerClass.Sniper
            && attacker.IsSniperScoped
            && attacker.LastToDieSniperProfile.AsceticEnabled)
        {
            // The profile is also present on prediction-only clients, where
            // the authoritative perk runtime is intentionally absent.
            multiplier *= LastToDieSniperProfile.AsceticDamageMultiplier;
        }

        if (!_host.TryGetPlayerNetworkSlot(attacker, out var slot)
            || !_host.LastToDieState.PerkRuntimesBySlot.TryGetValue(slot, out var runtime))
        {
            return MathF.Max(0.05f, multiplier);
        }

        if (attacker.ClassId == PlayerClass.Sniper)
        {
            var sniperDamageBonus = 0f;
            if (runtime.Modifiers.SniperProfile is { ConquistadorEnabled: true }
                && attacker.LastToDieSniperProfile.ConquistadorEnabled)
            {
                sniperDamageBonus += Math.Clamp(
                    attacker.LastToDieSniperConquistadorStacks,
                    0,
                    LastToDieSniperProfile.ConquistadorMaximumStacks)
                    * LastToDieSniperProfile.ConquistadorDamageBonusPerStack;
            }

            if (damageTraits.HasFlag(PlayerDamageTraits.BenefitFromLastToDieSpotted)
                && runtime.Modifiers.SniperProfile is { SpottedEnabled: true }
                && attacker.LastToDieSniperProfile.SpottedEnabled
                && _host.TryGetPlayerNetworkSlot(target, out var targetSlot)
                && attacker.LastToDieSniperMarkedTargetSlot == targetSlot)
            {
                sniperDamageBonus += LastToDieSniperProfile.SpottedDamageMultiplier - 1f;
            }

            multiplier *= 1f + sniperDamageBonus;
        }

        if (damageTraits.HasFlag(PlayerDamageTraits.Periodic))
        {
            return MathF.Max(0.05f, multiplier);
        }

        multiplier *= attacker.GetLastToDieUniversalOutgoingDamageMultiplier();

        multiplier *= 1f + attacker.LastToDieSpyRogueOutgoingDamageBonus;
        if (attacker.ClassId == PlayerClass.Medic
            && runtime.Modifiers.MedicCombatMedicEnabled
            && attacker.MaxHealth > 0
            && attacker.Health * 2L < attacker.MaxHealth)
        {
            multiplier *= LastToDieDerivedModifiers.MedicCombatMedicDamageMultiplier;
        }
        var resolvedAttackerWasGrounded = attackerWasGrounded ?? attacker.IsGrounded;
        var resolvedTargetWasGrounded = targetWasGrounded ?? target.IsGrounded;
        if (resolvedAttackerWasGrounded && !resolvedTargetWasGrounded)
        {
            multiplier += MathF.Max(1f, runtime.Modifiers.GroundedVsAirborneDamageMultiplier) - 1f;
        }
        else if (!resolvedAttackerWasGrounded && resolvedTargetWasGrounded)
        {
            multiplier += MathF.Max(1f, runtime.Modifiers.AirborneVsGroundedDamageMultiplier) - 1f;
        }

        return MathF.Max(0.05f, multiplier);
    }

    internal int ApplyLastToDieIncomingDamageMultiplier(
        PlayerEntity target,
        int damage,
        PlayerDamageTraits damageTraits)
    {
        if (damage <= 0
            || damageTraits.HasFlag(PlayerDamageTraits.LastToDieIncomingModifierPreApplied))
        {
            return damage;
        }

        var multiplier = target.LastToDieIncomingDamageMultiplier;
        multiplier *= GetLastToDieUniversalIncomingDamageMultiplier(target, damageTraits);
        return multiplier >= 1f
            ? damage
            : Math.Max(1, (int)MathF.Round((damage * multiplier) + 0.0001f));
    }

    internal float ApplyLastToDieIncomingDamageMultiplier(
        PlayerEntity target,
        float damage,
        PlayerDamageTraits damageTraits)
    {
        if (damage <= 0f
            || damageTraits.HasFlag(PlayerDamageTraits.LastToDieIncomingModifierPreApplied))
        {
            return damage;
        }

        var multiplier = target.LastToDieIncomingDamageMultiplier;
        multiplier *= GetLastToDieUniversalIncomingDamageMultiplier(target, damageTraits);
        return multiplier >= 1f
            ? damage
            : MathF.Max(0.01f, damage * multiplier);
    }

    private float GetLastToDieUniversalIncomingDamageMultiplier(
        PlayerEntity target,
        PlayerDamageTraits damageTraits)
    {
        if (!_host.TryGetPlayerNetworkSlot(target, out var slot)
            || !_host.LastToDieState.PerkRuntimesBySlot.TryGetValue(slot, out var runtime))
        {
            return 1f;
        }

        var universal = runtime.Modifiers.UniversalModifiers;
        var multiplier = 1f;
        if (damageTraits.HasFlag(PlayerDamageTraits.Bullet))
        {
            multiplier *= universal.BulletDamageTakenMultiplier;
        }
        if (damageTraits.HasFlag(PlayerDamageTraits.Explosive))
        {
            multiplier *= universal.ExplosionDamageTakenMultiplier;
        }
        if (universal.Fortify
            && (target.IsCarryingIntel
                || Enumerable.Range(1, _host.Objectives.ControlPoints.Points.Count)
                    .Any(index => !_host.Objectives.ControlPoints.Points[index - 1].IsLocked
                        && _host.IsPlayerInControlPointCaptureZone(target, index))))
        {
            multiplier *= 0.7f;
        }

        return Math.Clamp(multiplier, 0.05f, 1f);
    }

    internal float GetLastToDieEvasionChance(PlayerEntity target)
    {
        if (!target.IsAlive)
        {
            return 0f;
        }

        var cloakedEvasionChance = 0f;
        var stoicEvasionChance = 0f;
        if (_host.TryGetPlayerNetworkSlot(target, out var slot)
            && _host.LastToDieState.PerkRuntimesBySlot.TryGetValue(slot, out var runtime))
        {
            if (target.ClassId == PlayerClass.Spy && runtime.Modifiers.CloakedEvasionChance > 0f)
            {
                SyncLastToDieSpyCloakState(target, runtime, advanceGraceTimer: false);
                if (target.IsSpyCloaked || runtime.ShroudGraceTicksRemaining > 0)
                {
                    cloakedEvasionChance = Math.Clamp(
                        runtime.Modifiers.CloakedEvasionChance,
                        0f,
                        0.95f);
                }
            }

            stoicEvasionChance = target.ClassId == PlayerClass.Medic
                    && runtime.Modifiers.MedicStoicEnabled
                    && PlayerEntity.MedicUberMaxCharge > 0f
                ? Math.Clamp(
                    (target.MedicUberCharge / PlayerEntity.MedicUberMaxCharge)
                        * LastToDieDerivedModifiers.MedicStoicMaximumEvasionChance,
                    0f,
                    LastToDieDerivedModifiers.MedicStoicMaximumEvasionChance)
                : 0f;
        }

        var guardianEvasionChance = Math.Clamp(
            target.LastToDieGuardianEvasionChance,
            0f,
            0.95f);
        var universalEvasionChance = 0f;
        if (_host.TryGetPlayerNetworkSlot(target, out var universalSlot)
            && _host.LastToDieState.PerkRuntimesBySlot.TryGetValue(universalSlot, out var universalRuntime)
            && universalRuntime.Modifiers.UniversalModifiers.FightOrFlight
            && target.MaxHealth > 0
            && target.Health * 2L < target.MaxHealth)
        {
            universalEvasionChance = 0.3f;
        }
        var medicLinkEvasionChance = Math.Clamp(
            target.LastToDieMedicLinkEvasionChance,
            0f,
            0.95f);
        return Math.Clamp(
            1f - ((1f - cloakedEvasionChance)
                * (1f - stoicEvasionChance)
                * (1f - guardianEvasionChance)
                * (1f - universalEvasionChance)
                * (1f - medicLinkEvasionChance)),
            0f,
            0.95f);
    }

    internal bool RollLastToDieEvasion(PlayerEntity target, float totalEvasionChance)
    {
        if (totalEvasionChance <= 0f
            || !_host.TryGetPlayerNetworkSlot(target, out var slot)
            || !_host.LastToDieState.PerkRuntimesBySlot.TryGetValue(slot, out var runtime)
            || runtime.EvasionRandom is null)
        {
            return false;
        }

        return runtime.EvasionRandom.NextUInt32()
            < (uint)(Math.Clamp(totalEvasionChance, 0f, 0.95f) * (uint.MaxValue + 1d));
    }

    private void SyncLastToDieSpyCloakState(
        PlayerEntity player,
        LastToDiePlayerPerkRuntime runtime,
        bool advanceGraceTimer)
    {
        if (!player.IsAlive || player.ClassId != PlayerClass.Spy)
        {
            runtime.WasSpyCloaked = false;
            runtime.ShroudGraceTicksRemaining = 0;
            return;
        }

        if (player.IsSpyCloaked)
        {
            runtime.WasSpyCloaked = true;
            runtime.ShroudGraceTicksRemaining = 0;
            return;
        }

        if (runtime.WasSpyCloaked)
        {
            runtime.WasSpyCloaked = false;
            runtime.ShroudGraceTicksRemaining = runtime.Modifiers.CloakedEvasionChance > 0f
                ? Math.Max(1, _host.Config.TicksPerSecond)
                : 0;
            return;
        }

        if (advanceGraceTimer && runtime.ShroudGraceTicksRemaining > 0)
        {
            runtime.ShroudGraceTicksRemaining -= 1;
        }
    }

    internal void ResetLastToDiePerkRuntimeOnDeath(byte slot)
    {
        if (!_host.LastToDieState.PerkRuntimesBySlot.TryGetValue(slot, out var runtime))
        {
            return;
        }

        runtime.DamageHealingRemainder = 0;
        runtime.ScopedDamageHealingRemainder = 0;
        runtime.ScopedHealingAccumulator = 0f;
        runtime.CloakedHealingAccumulator = 0f;
        runtime.MedicHomeostasisHealingRemainder = 0;
        runtime.MedicSpikedVestReflectionRemainder = 0;
        runtime.UniversalReflectionRemainder = 0;
        runtime.UniversalHealingAccumulator = 0f;
        runtime.InfiniteSlayWorksDamageAccumulator = 0f;
        runtime.WasSpyCloaked = false;
        runtime.ShroudGraceTicksRemaining = 0;
        if (_host.TryGetNetworkPlayer(slot, out var player))
        {
            player.ResetLastToDieSpyCloakDynamicState();
            player.ResetLastToDieSpyInfiltrateDynamicState();
            player.ResetLastToDieSpyAfterlifeDynamicState(preserveCooldown: true);
            player.ResetLastToDieSniperDynamicState();
        }
    }

    internal void AdvanceLastToDiePassivePerks(byte slot, PlayerEntity player)
    {
        player.AdvanceLastToDieSpyCloakMeter(_host.Config.TicksPerSecond);
        if (!_host.LastToDieState.PerkRuntimesBySlot.TryGetValue(slot, out var runtime))
        {
            return;
        }

        SyncLastToDieSpyCloakState(player, runtime, advanceGraceTimer: true);
        AdvanceLastToDieCloakedHealing(player, runtime);
        AdvanceLastToDieScopedHealing(player, runtime);
        AdvanceLastToDieUniversalPassives(player, runtime);
    }

    private void AdvanceLastToDieUniversalPassives(
        PlayerEntity player,
        LastToDiePlayerPerkRuntime runtime)
    {
        var ticksPerSecond = Math.Max(1, _host.Config.TicksPerSecond);
        var universal = runtime.Modifiers.UniversalModifiers;
        if (universal.RegenerationPerSecond > 0f && player.Health < player.MaxHealth)
        {
            runtime.UniversalHealingAccumulator += universal.RegenerationPerSecond / ticksPerSecond;
            var wholeHealing = (int)MathF.Floor(runtime.UniversalHealingAccumulator + 0.000001f);
            if (wholeHealing > 0)
            {
                runtime.UniversalHealingAccumulator -= wholeHealing;
                _host.ApplyHealingWithFeedback(player, wholeHealing);
            }
        }
        else
        {
            runtime.UniversalHealingAccumulator = 0f;
        }

        if (!universal.InfiniteSlayWorks)
        {
            runtime.InfiniteSlayWorksDamageAccumulator = 0f;
            return;
        }

        runtime.InfiniteSlayWorksDamageAccumulator += 1f / ticksPerSecond;
        var wholeDamage = (int)MathF.Floor(runtime.InfiniteSlayWorksDamageAccumulator + 0.000001f);
        if (wholeDamage <= 0)
        {
            return;
        }

        runtime.InfiniteSlayWorksDamageAccumulator -= wholeDamage;
        var resolution = _host.ResolvePlayerDamage(
            player,
            new PlayerDamageRequest(
                PlayerDamageApplicationKind.Instant,
                wholeDamage,
                Attacker: null,
                PlayerEntity.SpyDamageRevealAlpha,
                DamageEventFlags.None,
                PlayerDamageTraits.Periodic | PlayerDamageTraits.LastToDieIncomingModifierPreApplied,
                AllowOsmosisHealOwnedSentries: false,
                new PlayerDamageUmbrellaOptions(AllowBlock: false)));
        if (resolution.WasFatal)
        {
            _host.KillPlayer(player, killer: null, weaponSpriteName: "DeadKL");
        }
    }

    private void AdvanceLastToDieCloakedHealing(
        PlayerEntity player,
        LastToDiePlayerPerkRuntime runtime)
    {
        if (runtime.Modifiers.CloakedHealingPerSecond <= 0f
            || player.ClassId != PlayerClass.Spy
            || !player.IsSpyCloaked
            || player.Health >= player.MaxHealth)
        {
            return;
        }

        runtime.CloakedHealingAccumulator +=
            runtime.Modifiers.CloakedHealingPerSecond / Math.Max(1, _host.Config.TicksPerSecond);
        var wholeHealing = (int)runtime.CloakedHealingAccumulator;
        if (wholeHealing <= 0)
        {
            return;
        }

        runtime.CloakedHealingAccumulator -= wholeHealing;
        _host.ApplyHealingWithFeedback(player, wholeHealing);
    }

    private void AdvanceLastToDieScopedHealing(
        PlayerEntity player,
        LastToDiePlayerPerkRuntime runtime)
    {
        if (runtime.Modifiers.ScopedHealingPerSecond <= 0f
            || player.ClassId != PlayerClass.Sniper
            || !player.IsSniperScoped
            || player.Health >= player.MaxHealth)
        {
            return;
        }

        runtime.ScopedHealingAccumulator +=
            runtime.Modifiers.ScopedHealingPerSecond / Math.Max(1, _host.Config.TicksPerSecond);
        var wholeHealing = (int)runtime.ScopedHealingAccumulator;
        if (wholeHealing <= 0)
        {
            return;
        }

        runtime.ScopedHealingAccumulator -= wholeHealing;
        _host.ApplyHealingWithFeedback(player, wholeHealing);
    }
}
