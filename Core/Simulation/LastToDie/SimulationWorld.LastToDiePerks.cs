using OpenGarrison.Core.LastToDie;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private sealed class LastToDiePlayerPerkRuntime(LastToDieDerivedModifiers modifiers)
    {
        public LastToDieDerivedModifiers Modifiers { get; set; } = modifiers;

        /// <summary>
        /// The original offline Last to Die perks are represented by the
        /// experimental gameplay settings model. This copy is per network
        /// slot; <see cref="SimulationWorld.ExperimentalGameplaySettings"/>
        /// remains the world/practice fallback.
        /// </summary>
        public ExperimentalGameplaySettings LegacySettings { get; set; } = new();

        public int DamageHealingRemainder { get; set; }

        public float ScopedHealingAccumulator { get; set; }

        public float CloakedHealingAccumulator { get; set; }

        public int MedicHomeostasisHealingRemainder { get; set; }

        public int MedicSpikedVestReflectionRemainder { get; set; }

        public int? MedicSupportRelayActiveLinkTargetPlayerId { get; set; }

        public Dictionary<int, long> MedicSupportRelayCooldownUntilFrameByTargetPlayerId { get; } = [];

        public bool WasSpyCloaked { get; set; }

        public int ShroudGraceTicksRemaining { get; set; }

        public LastToDieRandom? RevolverCriticalRandom { get; set; }

        public LastToDieRandom? EvasionRandom { get; set; }

        public LastToDieRandom? OverkillerRandom { get; set; }
    }

    private readonly Dictionary<byte, LastToDiePlayerPerkRuntime> _lastToDiePerkRuntimesBySlot = [];
    // Client prediction receives the authoritative survivor/perk snapshot but
    // must not create the server-owned combat runtime. Keep that profile
    // separate so inactive online slots never become LTD owners.
    private readonly Dictionary<byte, ExperimentalGameplaySettings> _lastToDieLegacyGameplaySettingsBySlot = [];
    private ulong _lastToDieCombatSeed;
    private bool _lastToDieCombatSeedConfigured;

    public void ConfigureLastToDieCombatSeed(ulong seed)
    {
        _lastToDieCombatSeed = seed;
        _lastToDieCombatSeedConfigured = true;
        foreach (var entry in _lastToDiePerkRuntimesBySlot)
        {
            entry.Value.RevolverCriticalRandom = CreateLastToDieRevolverCriticalRandom(entry.Key);
            entry.Value.EvasionRandom = CreateLastToDieEvasionRandom(entry.Key);
            entry.Value.OverkillerRandom = CreateLastToDieOverkillerRandom(entry.Key);
            entry.Value.DamageHealingRemainder = 0;
            entry.Value.ScopedHealingAccumulator = 0f;
            entry.Value.CloakedHealingAccumulator = 0f;
            entry.Value.MedicHomeostasisHealingRemainder = 0;
            entry.Value.MedicSpikedVestReflectionRemainder = 0;
            entry.Value.MedicSupportRelayActiveLinkTargetPlayerId = null;
            entry.Value.MedicSupportRelayCooldownUntilFrameByTargetPlayerId.Clear();
            entry.Value.ShroudGraceTicksRemaining = 0;
            if (TryGetNetworkPlayer(entry.Key, out var player))
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

    public bool TryConfigureLastToDiePlayerBuild(
        byte slot,
        IEnumerable<LastToDiePerkId> perks,
        int? baseMaximumHealthOverride = null,
        bool refillHealth = false,
        bool resetDynamicState = false)
    {
        ArgumentNullException.ThrowIfNull(perks);
        if (!TryGetNetworkPlayer(slot, out var player))
        {
            return false;
        }

        var ownedPerks = perks.ToArray();
        var modifiers = LastToDieDerivedModifiers.FromPerks(ownedPerks);
        var legacySettings = LastToDieLegacyPerkSettings.FromPerks(
            player.ClassId,
            ownedPerks,
            ExperimentalGameplaySettings);
        var previousMaximumHealthBonus = 0;
        var healthBeforeConfiguration = player.Health;
        if (_lastToDiePerkRuntimesBySlot.TryGetValue(slot, out var runtime))
        {
            previousMaximumHealthBonus = runtime.Modifiers.MaximumHealthBonus;
            if (runtime.Modifiers.DamageHealingFraction != modifiers.DamageHealingFraction)
            {
                runtime.DamageHealingRemainder = 0;
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
                runtime.OverkillerRandom ??= _lastToDieCombatSeedConfigured
                    ? CreateLastToDieOverkillerRandom(slot)
                    : null;
            }
            runtime.Modifiers = modifiers;
            runtime.LegacySettings = legacySettings;
            _lastToDieLegacyGameplaySettingsBySlot[slot] = legacySettings;
        }
        else
        {
            runtime = new LastToDiePlayerPerkRuntime(modifiers)
            {
                LegacySettings = legacySettings,
                RevolverCriticalRandom = _lastToDieCombatSeedConfigured
                    ? CreateLastToDieRevolverCriticalRandom(slot)
                    : null,
                EvasionRandom = CreateLastToDieEvasionRandom(slot),
                OverkillerRandom = _lastToDieCombatSeedConfigured
                    ? CreateLastToDieOverkillerRandom(slot)
                    : null,
                WasSpyCloaked = player.IsAlive
                    && player.ClassId == PlayerClass.Spy
                    && player.IsSpyCloaked,
            };
            _lastToDiePerkRuntimesBySlot.Add(slot, runtime);
            _lastToDieLegacyGameplaySettingsBySlot[slot] = legacySettings;
        }

        // Class/respawn synchronization ran before the reward build was
        // installed. Reapply the per-slot legacy profile now so the first
        // hosted stage immediately gets the original Demo/Soldier/Engineer
        // loadout state instead of waiting for a later resync tick.
        SyncExperimentalGameplayLoadout(slot, player);

        player.SetLastToDieSpyRevolverProfile(modifiers.SpyRevolverProfile, refillHealth);
        player.SetLastToDieCloakedPerkMultipliers(
            modifiers.CloakedMovementSpeedMultiplier,
            modifiers.CloakedDamageTakenMultiplier);
        player.ConfigureLastToDieSpyCloakMeter(
            modifiers.RogueCommanderEnabled,
            modifiers.ProfessionalEnabled,
            Config.TicksPerSecond,
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
            Config.TicksPerSecond,
            resetDynamicState);
        if (!modifiers.AfterlifeEnabled || resetDynamicState)
        {
            ResetLastToDieSpyAfterlifeRuntime(slot, player);
        }
        player.ConfigureLastToDieSpyAfterlife(
            modifiers.AfterlifeEnabled,
            Config.TicksPerSecond,
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

        var baseMaximumHealth = Math.Max(
            1,
            baseMaximumHealthOverride ?? player.ClassDefinition.MaxHealth);
        var configured = TrySetNetworkPlayerMaxHealthOverride(
            slot,
            checked(baseMaximumHealth + modifiers.MaximumHealthBonus),
            refillHealth);
        if (configured
            && !refillHealth
            && player.IsAlive
            && modifiers.MaximumHealthBonus > previousMaximumHealthBonus)
        {
            player.ForceSetHealth(checked(
                healthBeforeConfiguration
                    + (modifiers.MaximumHealthBonus - previousMaximumHealthBonus)));
        }

        return configured;
    }

    /// <summary>
    /// Gets the authoritative legacy settings for a hosted Last to Die
    /// participant. Practice/offline callers that have no Last to Die build
    /// continue to use the world-level experimental settings.
    /// </summary>
    private bool TryGetLastToDieLegacyGameplaySettings(
        byte slot,
        out ExperimentalGameplaySettings settings)
    {
        if (_lastToDieLegacyGameplaySettingsBySlot.TryGetValue(slot, out settings))
        {
            return true;
        }

        if (_lastToDiePerkRuntimesBySlot.TryGetValue(slot, out var runtime))
        {
            settings = runtime.LegacySettings;
            return true;
        }

        settings = ExperimentalGameplaySettings;
        return false;
    }

    private ExperimentalGameplaySettings GetLastToDieGameplaySettings(PlayerEntity? player)
    {
        return player is not null
            && TryGetPlayerNetworkSlot(player, out var slot)
            && TryGetLastToDieLegacyGameplaySettings(slot, out var settings)
            ? settings
            : ExperimentalGameplaySettings;
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
        if (_lastToDiePerkRuntimesBySlot.Count == 0
            && _lastToDieLegacyGameplaySettingsBySlot.Count == 0)
        {
            return selector(ExperimentalGameplaySettings);
        }

        return _lastToDiePerkRuntimesBySlot.Values.Any(runtime => selector(runtime.LegacySettings))
            || _lastToDieLegacyGameplaySettingsBySlot.Values.Any(selector);
    }

    /// <summary>
    /// Applies only the deterministic client prediction/presentation portion
    /// of an authoritative Last to Die build. Healing, evasion, and damage
    /// rewards remain exclusively in the server-owned perk runtime.
    /// </summary>
    public bool TryApplyLastToDiePlayerPredictionProfile(
        byte slot,
        IEnumerable<string> ownedPerkIds)
    {
        ArgumentNullException.ThrowIfNull(ownedPerkIds);
        if (!TryGetNetworkPlayer(slot, out var player))
        {
            return false;
        }

        var ownedPerks = ownedPerkIds
            .Select(static perkId => new LastToDiePerkId(perkId))
            .Distinct()
            .ToArray();
        _lastToDieLegacyGameplaySettingsBySlot[slot] = LastToDieLegacyPerkSettings.FromPerks(
            player.ClassId,
            ownedPerks,
            ExperimentalGameplaySettings);
        var modifiers = LastToDieDerivedModifiers.FromPerks(ownedPerks);
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
            Config.TicksPerSecond,
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
            Config.TicksPerSecond,
            resetDynamicState: false);
        if (!modifiers.AfterlifeEnabled)
        {
            ResetLastToDieSpyAfterlifeRuntime(slot, player);
        }
        player.ConfigureLastToDieSpyAfterlife(
            modifiers.AfterlifeEnabled,
            Config.TicksPerSecond,
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
        SyncExperimentalGameplayLoadout(slot, player);
    }

    public bool ClearLastToDiePlayerPredictionProfile(byte slot)
    {
        if (!TryGetNetworkPlayer(slot, out var player))
        {
            return false;
        }

        _lastToDieLegacyGameplaySettingsBySlot.Remove(slot);
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
            || !TryGetPlayerNetworkSlot(attacker, out var slot)
            || !_lastToDiePerkRuntimesBySlot.TryGetValue(slot, out var runtime)
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
        var stream = LastToDieRandom.DeriveSeed(_lastToDieCombatSeed, slot);
        return new LastToDieRandom(
            LastToDieRandom.DeriveSeed(_lastToDieCombatSeed, stream),
            stream);
    }

    private LastToDieRandom CreateLastToDieEvasionRandom(byte slot)
    {
        const ulong evasionStreamDomain = 0x4556_4153_494F_4EUL;
        var domainSeed = _lastToDieCombatSeed ^ evasionStreamDomain;
        var stream = LastToDieRandom.DeriveSeed(domainSeed, slot);
        return new LastToDieRandom(
            LastToDieRandom.DeriveSeed(domainSeed, stream),
            stream);
    }

    private LastToDieRandom CreateLastToDieOverkillerRandom(byte slot)
    {
        const ulong overkillerStreamDomain = 0x4F56_4552_4B49_4C4CUL;
        var domainSeed = _lastToDieCombatSeed ^ overkillerStreamDomain;
        var stream = LastToDieRandom.DeriveSeed(domainSeed, slot);
        return new LastToDieRandom(
            LastToDieRandom.DeriveSeed(domainSeed, stream),
            stream);
    }

    public bool TryGetLastToDiePlayerModifiers(
        byte slot,
        out LastToDieDerivedModifiers modifiers)
    {
        if (_lastToDiePerkRuntimesBySlot.TryGetValue(slot, out var runtime))
        {
            modifiers = runtime.Modifiers;
            return true;
        }

        modifiers = new LastToDieDerivedModifiers();
        return false;
    }

    private LastToDieMedicKritzM2Payload CaptureLastToDieMedicKritzM2Payload(
        PlayerEntity owner)
    {
        var appliesHailMary = false;
        var appliesNeurotoxin = false;
        var appliesJavelin = false;
        if (owner.ClassId == PlayerClass.Medic
            && TryGetPlayerNetworkSlot(owner, out var slot)
            && _lastToDiePerkRuntimesBySlot.TryGetValue(slot, out var runtime))
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

    public bool TryGetLastToDieSniperConquistadorStacks(byte slot, out int stacks)
    {
        if (TryGetNetworkPlayer(slot, out var player)
            && player.ClassId == PlayerClass.Sniper
            && _lastToDiePerkRuntimesBySlot.TryGetValue(slot, out var runtime)
            && runtime.Modifiers.SniperProfile is { ConquistadorEnabled: true }
            && player.LastToDieSniperProfile.ConquistadorEnabled)
        {
            stacks = player.LastToDieSniperConquistadorStacks;
            return true;
        }

        stacks = 0;
        return false;
    }

    public bool TryRestoreLastToDieSniperConquistadorStacks(byte slot, int stacks)
    {
        if (!TryGetNetworkPlayer(slot, out var player)
            || player.ClassId != PlayerClass.Sniper
            || !_lastToDiePerkRuntimesBySlot.TryGetValue(slot, out var runtime)
            || runtime.Modifiers.SniperProfile is not { ConquistadorEnabled: true }
            || !player.LastToDieSniperProfile.ConquistadorEnabled)
        {
            return false;
        }

        player.RestoreLastToDieSniperConquistadorStacks(stacks);
        return true;
    }

    public bool CanPlayerCaptureControlPointsWhileCloaked(PlayerEntity player)
    {
        if (player.ClassId != PlayerClass.Spy)
        {
            return false;
        }

        return TryGetPlayerNetworkSlot(player, out var slot)
            && _lastToDiePerkRuntimesBySlot.TryGetValue(slot, out var runtime)
            && runtime.Modifiers.RogueCommanderEnabled;
    }

    public bool CanPlayerCaptureControlPointsWhileUbered(PlayerEntity player)
    {
        return player.IsAlive
            && player.ClassId == PlayerClass.Medic
            && player.IsMedicRegularUberDeliveryActive
            && TryGetPlayerNetworkSlot(player, out var slot)
            && _lastToDiePerkRuntimesBySlot.TryGetValue(slot, out var runtime)
            && runtime.Modifiers.MedicFieldCommanderEnabled;
    }

    public bool CanPlayerContributeToControlPoint(PlayerEntity player) =>
        !player.IsLastToDieSpyAfterlifeActive
        && (!player.IsSpyCloaked || CanPlayerCaptureControlPointsWhileCloaked(player))
        && (!player.IsMedicRegularUberDeliveryActive
            || CanPlayerCaptureControlPointsWhileUbered(player));

    private void ApplyLastToDieDamageRewards(
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
            || !TryGetPlayerNetworkSlot(attacker, out var slot)
            || !_lastToDiePerkRuntimesBySlot.TryGetValue(slot, out var runtime)
            || runtime.Modifiers.DamageHealingFraction <= 0f
            || attacker.Health >= attacker.MaxHealth)
        {
            return;
        }

        var scaledHealing = runtime.DamageHealingRemainder
            + (appliedDamage * (long)LastToDieDerivedModifiers.SpyVampireHealingNumerator);
        var wholeHealing = (int)(scaledHealing / LastToDieDerivedModifiers.SpyVampireHealingDenominator);
        runtime.DamageHealingRemainder = (int)(scaledHealing % LastToDieDerivedModifiers.SpyVampireHealingDenominator);
        if (wholeHealing <= 0)
        {
            return;
        }

        ApplyHealingWithFeedback(attacker, wholeHealing);
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
            || !TryGetPlayerNetworkSlot(attacker, out var slot)
            || !_lastToDiePerkRuntimesBySlot.TryGetValue(slot, out var runtime)
            || runtime.Modifiers.SniperProfile is not { OverkillerEnabled: true }
            || !attacker.LastToDieSniperProfile.OverkillerEnabled
            || runtime.OverkillerRandom is null
            || runtime.OverkillerRandom.NextUInt32()
                >= (uint)(LastToDieSniperProfile.OverkillerChance * (uint.MaxValue + 1d)))
        {
            return;
        }

        var executeResolution = ResolvePlayerDamage(
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
            KillPlayer(target, killer: attacker, weaponSpriteName: "RifleKL");
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
            || !TryGetPlayerNetworkSlot(attacker, out var attackerSlot)
            || !_lastToDiePerkRuntimesBySlot.TryGetValue(attackerSlot, out var runtime)
            || runtime.Modifiers.SniperProfile is not { SpottedEnabled: true }
            || !attacker.LastToDieSniperProfile.SpottedEnabled
            || !TryGetPlayerNetworkSlot(target, out var targetSlot))
        {
            return;
        }

        attacker.SetLastToDieSniperMarkedTargetSlot(targetSlot);
    }

    private void TryRegisterLastToDieSniperConquistadorKill(
        PlayerEntity killer,
        PlayerEntity victim)
    {
        if (!killer.IsAlive
            || killer.ClassId != PlayerClass.Sniper
            || killer.Team == victim.Team
            || !TryGetPlayerNetworkSlot(killer, out var killerSlot)
            || !_lastToDiePerkRuntimesBySlot.TryGetValue(killerSlot, out var runtime)
            || runtime.Modifiers.SniperProfile is not { ConquistadorEnabled: true }
            || !killer.LastToDieSniperProfile.ConquistadorEnabled)
        {
            return;
        }

        _ = killer.TryIncrementLastToDieSniperConquistadorStacks();
    }

    private void ClearLastToDieSniperMarksTargeting(byte targetSlot)
    {
        foreach (var player in EnumerateSimulatedPlayers())
        {
            if (player.LastToDieSniperMarkedTargetSlot == targetSlot)
            {
                player.ClearLastToDieSniperMarkedTarget();
            }
        }
    }

    private float GetLastToDieMedicHealingMultiplier(PlayerEntity medic, PlayerEntity target)
    {
        if (medic.ClassId != PlayerClass.Medic
            || target.MaxHealth <= 0
            || !TryGetPlayerNetworkSlot(medic, out var slot)
            || !_lastToDiePerkRuntimesBySlot.TryGetValue(slot, out var runtime)
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

    private float GetLastToDieMedicUberChargeGainMultiplier(PlayerEntity medic)
    {
        if (medic.ClassId != PlayerClass.Medic
            || !TryGetPlayerNetworkSlot(medic, out var slot)
            || !_lastToDiePerkRuntimesBySlot.TryGetValue(slot, out var runtime))
        {
            return 1f;
        }

        return MathF.Max(1f, runtime.Modifiers.MedicUberChargeGainMultiplier);
    }

    private float GetLastToDieMedicKritzCriticalDamageMultiplier(PlayerEntity medic)
    {
        if (!medic.LastToDieMedicKritPowerEnabled)
        {
            return ExperimentalGameplaySettings.KritzCriticalDamageMultiplier;
        }

        return LastToDieDerivedModifiers.MedicKritPowerCriticalDamageMultiplier;
    }

    private void ApplyLastToDieMedicHomeostasis(PlayerEntity medic, int appliedTargetHealing)
    {
        if (appliedTargetHealing <= 0
            || !medic.IsAlive
            || medic.ClassId != PlayerClass.Medic
            || !TryGetPlayerNetworkSlot(medic, out var slot)
            || !_lastToDiePerkRuntimesBySlot.TryGetValue(slot, out var runtime)
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

        ApplyHealingWithFeedback(medic, wholeHealing);
    }

    private void ApplyLastToDieDamageTakenEffects(
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
            || target.ClassId != PlayerClass.Medic
            || !damageTraits.HasFlag(PlayerDamageTraits.CanReflect)
            || damageTraits.HasFlag(PlayerDamageTraits.Periodic)
            || damageTraits.HasFlag(PlayerDamageTraits.Reflected)
            || !TryGetPlayerNetworkSlot(target, out var slot)
            || !_lastToDiePerkRuntimesBySlot.TryGetValue(slot, out var runtime)
            || !runtime.Modifiers.MedicSpikedVestEnabled)
        {
            return;
        }

        var scaledReflection = runtime.MedicSpikedVestReflectionRemainder
            + (appliedDamage * LastToDieDerivedModifiers.MedicSpikedVestReflectionNumerator);
        var reflectedDamage = scaledReflection
            / LastToDieDerivedModifiers.MedicSpikedVestReflectionDenominator;
        runtime.MedicSpikedVestReflectionRemainder = scaledReflection
            % LastToDieDerivedModifiers.MedicSpikedVestReflectionDenominator;
        if (reflectedDamage <= 0)
        {
            return;
        }

        var resolution = ResolvePlayerDamage(
            attacker,
            new PlayerDamageRequest(
                PlayerDamageApplicationKind.Instant,
                reflectedDamage,
                target,
                PlayerEntity.SpyDamageRevealAlpha,
                DamageEventFlags.None,
                PlayerDamageTraits.Reflected,
                AllowOsmosisHealOwnedSentries: false,
                new PlayerDamageUmbrellaOptions(AllowBlock: false)));
        if (resolution.WasFatal)
        {
            KillPlayer(attacker, killer: target);
        }
    }

    private int ApplyLastToDieOutgoingDamageMultiplier(
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

    private float ApplyLastToDieOutgoingDamageMultiplier(
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
        if (!TryGetPlayerNetworkSlot(attacker, out var slot)
            || !_lastToDiePerkRuntimesBySlot.TryGetValue(slot, out var runtime))
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
                && TryGetPlayerNetworkSlot(target, out var targetSlot)
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

    private int ApplyLastToDieIncomingDamageMultiplier(
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
        return multiplier >= 1f
            ? damage
            : Math.Max(1, (int)MathF.Round((damage * multiplier) + 0.0001f));
    }

    private float ApplyLastToDieIncomingDamageMultiplier(
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
        return multiplier >= 1f
            ? damage
            : MathF.Max(0.01f, damage * multiplier);
    }

    private float GetLastToDieEvasionChance(PlayerEntity target)
    {
        if (!target.IsAlive)
        {
            return 0f;
        }

        var cloakedEvasionChance = 0f;
        var stoicEvasionChance = 0f;
        if (TryGetPlayerNetworkSlot(target, out var slot)
            && _lastToDiePerkRuntimesBySlot.TryGetValue(slot, out var runtime))
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
        var medicLinkEvasionChance = Math.Clamp(
            target.LastToDieMedicLinkEvasionChance,
            0f,
            0.95f);
        return Math.Clamp(
            1f - ((1f - cloakedEvasionChance)
                * (1f - stoicEvasionChance)
                * (1f - guardianEvasionChance)
                * (1f - medicLinkEvasionChance)),
            0f,
            0.95f);
    }

    private bool RollLastToDieEvasion(PlayerEntity target, float totalEvasionChance)
    {
        if (totalEvasionChance <= 0f
            || !TryGetPlayerNetworkSlot(target, out var slot)
            || !_lastToDiePerkRuntimesBySlot.TryGetValue(slot, out var runtime)
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
                ? Math.Max(1, Config.TicksPerSecond)
                : 0;
            return;
        }

        if (advanceGraceTimer && runtime.ShroudGraceTicksRemaining > 0)
        {
            runtime.ShroudGraceTicksRemaining -= 1;
        }
    }

    private void ResetLastToDiePerkRuntimeOnDeath(byte slot)
    {
        if (!_lastToDiePerkRuntimesBySlot.TryGetValue(slot, out var runtime))
        {
            return;
        }

        runtime.DamageHealingRemainder = 0;
        runtime.ScopedHealingAccumulator = 0f;
        runtime.CloakedHealingAccumulator = 0f;
        runtime.MedicHomeostasisHealingRemainder = 0;
        runtime.MedicSpikedVestReflectionRemainder = 0;
        runtime.WasSpyCloaked = false;
        runtime.ShroudGraceTicksRemaining = 0;
        if (TryGetNetworkPlayer(slot, out var player))
        {
            player.ResetLastToDieSpyCloakDynamicState();
            player.ResetLastToDieSpyInfiltrateDynamicState();
            player.ResetLastToDieSpyAfterlifeDynamicState(preserveCooldown: true);
            player.ResetLastToDieSniperDynamicState();
        }
    }

    private void AdvanceLastToDiePassivePerks(byte slot, PlayerEntity player)
    {
        player.AdvanceLastToDieSpyCloakMeter(Config.TicksPerSecond);
        if (!_lastToDiePerkRuntimesBySlot.TryGetValue(slot, out var runtime))
        {
            return;
        }

        SyncLastToDieSpyCloakState(player, runtime, advanceGraceTimer: true);
        AdvanceLastToDieCloakedHealing(player, runtime);
        AdvanceLastToDieScopedHealing(player, runtime);
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
            runtime.Modifiers.CloakedHealingPerSecond / Math.Max(1, Config.TicksPerSecond);
        var wholeHealing = (int)runtime.CloakedHealingAccumulator;
        if (wholeHealing <= 0)
        {
            return;
        }

        runtime.CloakedHealingAccumulator -= wholeHealing;
        ApplyHealingWithFeedback(player, wholeHealing);
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
            runtime.Modifiers.ScopedHealingPerSecond / Math.Max(1, Config.TicksPerSecond);
        var wholeHealing = (int)runtime.ScopedHealingAccumulator;
        if (wholeHealing <= 0)
        {
            return;
        }

        runtime.ScopedHealingAccumulator -= wholeHealing;
        ApplyHealingWithFeedback(player, wholeHealing);
    }
}
