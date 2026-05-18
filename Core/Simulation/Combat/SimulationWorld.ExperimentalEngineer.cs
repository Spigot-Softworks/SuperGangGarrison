namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private bool IsExperimentalEngineerPerkOwner(PlayerEntity? player)
    {
        return player is not null
            && IsExperimentalPracticePowerOwner(player)
            && player.ClassId == PlayerClass.Engineer;
    }

    private bool HasExperimentalEngineerEssenceExtractor(PlayerEntity? player)
    {
        return HasExperimentalEngineerEssenceExtractorAvailable(player)
            && IsExperimentalEngineerAlternateWeaponModeActive(player, ExperimentalEngineerAlternateWeaponMode.EssenceExtractor);
    }

    private bool HasExperimentalEngineerFreezeRay(PlayerEntity? player)
    {
        return HasExperimentalEngineerFreezeRayAvailable(player)
            && IsExperimentalEngineerAlternateWeaponModeActive(player, ExperimentalEngineerAlternateWeaponMode.FreezeRay);
    }

    private bool HasExperimentalEngineerDestinyPunctuator(PlayerEntity? player)
    {
        return IsExperimentalEngineerPerkOwner(player)
            && ExperimentalGameplaySettings.EnableEngineerDestinyPunctuator;
    }

    private bool HasExperimentalEngineerEssenceExtractorAvailable(PlayerEntity? player)
    {
        return IsExperimentalEngineerPerkOwner(player)
            && ExperimentalGameplaySettings.EnableEngineerEssenceExtractor;
    }

    private bool HasExperimentalEngineerFreezeRayAvailable(PlayerEntity? player)
    {
        return IsExperimentalEngineerPerkOwner(player)
            && ExperimentalGameplaySettings.EnableEngineerFreezeRay;
    }

    private bool HasExperimentalEngineerAlternateWeaponAvailable(PlayerEntity? player)
    {
        return HasExperimentalEngineerEssenceExtractorAvailable(player)
            || HasExperimentalEngineerFreezeRayAvailable(player);
    }

    private static bool IsExperimentalEngineerAlternateWeaponModeActive(PlayerEntity? player, ExperimentalEngineerAlternateWeaponMode mode)
    {
        return player is not null
            && player.IsExperimentalOffhandSelected
            && player.ExperimentalEngineerAlternateWeaponMode == mode;
    }

    private ExperimentalEngineerAlternateWeaponMode GetExperimentalEngineerDefaultAlternateWeaponMode(PlayerEntity? player)
    {
        if (HasExperimentalEngineerEssenceExtractorAvailable(player))
        {
            return ExperimentalEngineerAlternateWeaponMode.EssenceExtractor;
        }

        if (HasExperimentalEngineerFreezeRayAvailable(player))
        {
            return ExperimentalEngineerAlternateWeaponMode.FreezeRay;
        }

        return ExperimentalEngineerAlternateWeaponMode.None;
    }

    private void ClearExperimentalEngineerAlternateWeaponState(PlayerEntity player, bool flushEssenceHealing = true)
    {
        if (flushEssenceHealing
            && player.ExperimentalEngineerAlternateWeaponMode == ExperimentalEngineerAlternateWeaponMode.EssenceExtractor)
        {
            FlushExperimentalEngineerEssenceExtractorHealing(player);
        }

        player.ClearMedicHealingTarget();
    }

    public bool IsExperimentalEngineerFloatingSentry(SentryEntity sentry)
    {
        var owner = FindPlayerById(sentry.OwnerPlayerId);
        return sentry.IsBuilt
            && IsExperimentalEngineerPerkOwner(owner)
            && ExperimentalGameplaySettings.EnableEngineerAutonomousPhaseEngine;
    }

    public static bool ShouldTreatPlayerAsExperimentalFriendlyFireTarget(PlayerEntity observer, PlayerEntity candidate)
    {
        if (!observer.IsAlive
            || !candidate.IsAlive
            || observer.Id == candidate.Id
            || observer.Team != candidate.Team)
        {
            return false;
        }

        return observer.ExperimentalConfusedAttackTargetPlayerId == candidate.Id
            || candidate.IsExperimentalConfusionRetaliationMarked;
    }

    private int GetExperimentalOwnedSentryCount(int ownerPlayerId)
    {
        var count = 0;
        for (var sentryIndex = 0; sentryIndex < _sentries.Count; sentryIndex += 1)
        {
            if (_sentries[sentryIndex].OwnerPlayerId == ownerPlayerId)
            {
                count += 1;
            }
        }

        return count;
    }

    private int GetExperimentalMaxOwnedSentries(PlayerEntity player)
    {
        if (!IsExperimentalEngineerPerkOwner(player))
        {
            return 1;
        }

        if (HasExperimentalEngineerDestinyPunctuator(player))
        {
            return 0;
        }

        return 1
            + (ExperimentalGameplaySettings.EnableEngineerOutputInducer
                ? global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerOutputInducerAdditionalSentries
                : 0);
    }

    private int GetExperimentalSentryMaxHealth(PlayerEntity owner)
    {
        var maxHealth = SentryEntity.DefaultMaxHealth;
        if (!IsExperimentalEngineerPerkOwner(owner))
        {
            return maxHealth;
        }

        if (ExperimentalGameplaySettings.EnableEngineerGuardianMatrix)
        {
            maxHealth += global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerGuardianMatrixSentryBonusHealth;
        }

        if (ExperimentalGameplaySettings.EnableEngineerHardwareHardener)
        {
            maxHealth += global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerHardwareHardenerSentryBonusHealth;
        }

        return maxHealth;
    }

    private int GetExperimentalSentryReloadTicks(PlayerEntity owner, SentryEntity sentry)
    {
        var reloadTicks = SentryEntity.ReloadTicks;
        if (!IsExperimentalEngineerPerkOwner(owner))
        {
            return reloadTicks;
        }

        if (ExperimentalGameplaySettings.EnableEngineerPrecisionInstantiator)
        {
            reloadTicks = Math.Max(
                1,
                (int)MathF.Round(
                    reloadTicks * global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerPrecisionInstantiatorReloadMultiplier));
        }
        else if (ExperimentalGameplaySettings.EnableEngineerBuckshotConversion)
        {
            reloadTicks = Math.Max(
                1,
                (int)MathF.Round(
                    reloadTicks * global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerBuckshotConversionReloadMultiplier));
        }

        if (!ExperimentalGameplaySettings.EnableEngineerAmperageAccelerator)
        {
            return reloadTicks;
        }

        var shotsToMax = Math.Max(1, global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerAmperageAcceleratorShotsToMax);
        var rampFraction = Math.Clamp(sentry.ConsecutiveShotsFired / (float)shotsToMax, 0f, 1f);
        var rateOfFireMultiplier = 1f + ((global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerAmperageAcceleratorMaxRateOfFireMultiplier - 1f) * rampFraction);
        return Math.Max(1, (int)MathF.Round(reloadTicks / rateOfFireMultiplier));
    }

    private int GetExperimentalSentryIdleResetTicks()
    {
        return Math.Max(
            1,
            (int)MathF.Round(
                Config.TicksPerSecond * global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerAmperageAcceleratorResetSeconds));
    }

    private float GetExperimentalSentryTargetRange(PlayerEntity owner)
    {
        if (IsExperimentalEngineerPerkOwner(owner)
            && (ExperimentalGameplaySettings.EnableEngineerPrecisionInstantiator
                || ExperimentalGameplaySettings.EnableEngineerCaveatInjector))
        {
            return GetExperimentalEngineerInfiniteTargetRange();
        }

        return SentryEntity.TargetRange;
    }

    private float GetExperimentalEngineerInfiniteTargetRange()
    {
        return MathF.Sqrt((Bounds.Width * Bounds.Width) + (Bounds.Height * Bounds.Height));
    }

    private static float GetExperimentalNearbySentryAuraRadius()
    {
        return global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerNearbySentryAuraRadius;
    }

    private float GetExperimentalEngineerMaxMetal(PlayerEntity player)
    {
        var maxMetal = 100f;
        if (IsExperimentalEngineerPerkOwner(player)
            && ExperimentalGameplaySettings.EnableEngineerMateriaRecycler)
        {
            maxMetal += global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerMateriaRecyclerBonusMaxMetal;
        }

        return maxMetal;
    }

    private static float GetExperimentalEngineerPassiveMetalRegenerationPerTick(PlayerEntity player)
    {
        _ = player;
        return 0.1f;
    }

    private float GetExperimentalEngineerMovementSpeedMultiplier(PlayerEntity player)
    {
        if (!IsExperimentalEngineerPerkOwner(player)
            || !ExperimentalGameplaySettings.EnableEngineerEfficiencyStabilizer)
        {
            return 1f;
        }

        return 1f + (player.Metal * global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerEfficiencyStabilizerMovementSpeedPerMetal);
    }

    private bool IsPlayerNearExperimentalOwnedSentry(PlayerEntity? player)
    {
        if (!IsExperimentalEngineerPerkOwner(player))
        {
            return false;
        }

        var auraRadius = GetExperimentalNearbySentryAuraRadius();
        for (var sentryIndex = 0; sentryIndex < _sentries.Count; sentryIndex += 1)
        {
            var sentry = _sentries[sentryIndex];
            if (sentry.OwnerPlayerId != player!.Id
                || !sentry.IsBuilt
                || DistanceBetween(sentry.X, sentry.Y, player.X, player.Y) > auraRadius)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    public bool IsPlayerInsideExperimentalEngineerMisdirectionFieldForVisuals(PlayerEntity? player)
    {
        return player is not null
            && ExperimentalGameplaySettings.EnableEngineerMisdirectionField
            && IsPlayerNearExperimentalOwnedSentry(player);
    }

    private void ApplyExperimentalEngineerPassivePlayerEffects(PlayerEntity player)
    {
        if (!IsExperimentalEngineerPerkOwner(player))
        {
            player.SetExperimentalEngineerEssenceExtractorPresented(false);
            player.SetExperimentalEngineerFreezeRayPresented(false);
            player.SetExperimentalEngineerAlternateWeaponMode(ExperimentalEngineerAlternateWeaponMode.None);
            player.SetExperimentalPrimaryWeaponOverride(null);
            player.SetExperimentalMaxHealthBonus(0);
            player.SetExperimentalHealthPackHealingMultiplier(1f);
            player.SetExperimentalEngineerMetalMovementSpeedMultiplier(1f);
            player.ConfigureExperimentalMetal(100f, 0.1f);
            player.ClearExperimentalShield();
            return;
        }

        var destinyActive = HasExperimentalEngineerDestinyPunctuator(player);
        if (destinyActive)
        {
            DestroyExperimentalEngineerOwnedSentries(player.Id);
        }

        player.ConfigureExperimentalMetal(
            GetExperimentalEngineerMaxMetal(player),
            GetExperimentalEngineerPassiveMetalRegenerationPerTick(player));
        player.SetExperimentalEngineerMetalMovementSpeedMultiplier(GetExperimentalEngineerMovementSpeedMultiplier(player));
        player.SetExperimentalMaxHealthBonus(
            destinyActive
                ? global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerDestinyPunctuatorBonusMaxHealth
                : 0);
        player.SetExperimentalHealthPackHealingMultiplier(
            destinyActive
                ? global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerDestinyPunctuatorHealthPackHealingMultiplier
                : 1f);
        player.SetExperimentalPrimaryWeaponOverride(
            destinyActive
                ? CharacterClassCatalog.Shotgun with
                {
                    MaxAmmo = global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerDestinyPunctuatorShotgunClipSize,
                }
                : null);

        var essenceAvailable = HasExperimentalEngineerEssenceExtractorAvailable(player);
        var freezeAvailable = HasExperimentalEngineerFreezeRayAvailable(player);
        if (!essenceAvailable && !freezeAvailable)
        {
            if (player.IsExperimentalOffhandSelected)
            {
                ClearExperimentalEngineerAlternateWeaponState(player);
                player.StowExperimentalOffhandWeapon();
            }

            player.SetExperimentalOffhandWeapon(null);
            player.SetExperimentalEngineerAlternateWeaponMode(ExperimentalEngineerAlternateWeaponMode.None);
        }
        else
        {
            player.SetExperimentalOffhandWeapon(CharacterClassCatalog.Medigun);
            var alternateMode = player.ExperimentalEngineerAlternateWeaponMode;
            var modeIsValid = alternateMode switch
            {
                ExperimentalEngineerAlternateWeaponMode.EssenceExtractor => essenceAvailable,
                ExperimentalEngineerAlternateWeaponMode.FreezeRay => freezeAvailable,
                _ => false,
            };
            if (!modeIsValid)
            {
                if (player.IsExperimentalOffhandSelected)
                {
                    ClearExperimentalEngineerAlternateWeaponState(player);
                    player.StowExperimentalOffhandWeapon();
                }

                player.SetExperimentalEngineerAlternateWeaponMode(ExperimentalEngineerAlternateWeaponMode.None);
            }
        }

        player.SetExperimentalEngineerFreezeRayPresented(
            player.IsExperimentalOffhandPresented
            && player.ExperimentalEngineerAlternateWeaponMode == ExperimentalEngineerAlternateWeaponMode.FreezeRay);
        player.SetExperimentalEngineerEssenceExtractorPresented(
            player.IsExperimentalOffhandPresented
            && player.ExperimentalEngineerAlternateWeaponMode == ExperimentalEngineerAlternateWeaponMode.EssenceExtractor);

        if (ExperimentalGameplaySettings.EnableEngineerRegenerativeDiode)
        {
            ApplyHealingWithFeedback(
                player,
                global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerRegenerativeDiodeHealthPerSecond / Math.Max(1, Config.TicksPerSecond));
        }

        if (ExperimentalGameplaySettings.EnableEngineerGuardianMatrix && IsPlayerNearExperimentalOwnedSentry(player))
        {
            player.SetExperimentalShieldHealth(global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerGuardianMatrixShieldHealth);
            return;
        }

        player.ClearExperimentalShield();
    }

    private void ApplyExperimentalEngineerSentryPassiveEffects(SentryEntity sentry, PlayerEntity owner)
    {
        if (!IsExperimentalEngineerPerkOwner(owner))
        {
            return;
        }

        if (HasExperimentalEngineerDestinyPunctuator(owner))
        {
            return;
        }

        if (ExperimentalGameplaySettings.EnableEngineerRegenerativeDiode
            && sentry.Health < sentry.MaxHealth)
        {
            sentry.ApplyContinuousHealing(
                global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerRegenerativeDiodeHealthPerSecond / Math.Max(1, Config.TicksPerSecond));
        }

        if (sentry.IsBuilt)
        {
            if (ExperimentalGameplaySettings.EnableEngineerAutonomousPhaseEngine)
            {
                AdvanceExperimentalAutonomousSentry(sentry, owner);
            }

            if (ExperimentalGameplaySettings.EnableEngineerIntegrityProjector)
            {
                TryApplyExperimentalIntegrityProjector(sentry, owner);
            }

            if (ExperimentalGameplaySettings.EnableEngineerConfusionField)
            {
                ApplyExperimentalConfusionFieldAura(sentry);
            }
        }
    }

    private void AdvanceExperimentalAutonomousSentry(SentryEntity sentry, PlayerEntity owner)
    {
        var ownedIndex = GetExperimentalOwnedSentryFormationIndex(owner.Id, sentry);
        var lateralDirection = ownedIndex % 2 == 0 ? -1f : 1f;
        var desiredX = owner.X + (lateralDirection * global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerAutonomousPhaseEngineFollowDistance);
        var desiredY = owner.Y - global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerAutonomousPhaseEngineHoverHeight;
        var deltaX = desiredX - sentry.X;
        var deltaY = desiredY - sentry.Y;
        var distance = MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
        if (distance <= 0.001f)
        {
            sentry.MoveTo(desiredX, desiredY);
            return;
        }

        var moveDistance = MathF.Min(
            distance,
            global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerAutonomousPhaseEngineFollowSpeedPerTick);
        sentry.MoveTo(
            sentry.X + (deltaX / distance) * moveDistance,
            sentry.Y + (deltaY / distance) * moveDistance);
    }

    private int GetExperimentalOwnedSentryFormationIndex(int ownerPlayerId, SentryEntity sentry)
    {
        var ownedIndex = 0;
        for (var sentryIndex = 0; sentryIndex < _sentries.Count; sentryIndex += 1)
        {
            var candidate = _sentries[sentryIndex];
            if (candidate.OwnerPlayerId != ownerPlayerId)
            {
                continue;
            }

            if (ReferenceEquals(candidate, sentry))
            {
                return ownedIndex;
            }

            ownedIndex += 1;
        }

        return 0;
    }

    private void TryApplyExperimentalIntegrityProjector(SentryEntity sentry, PlayerEntity owner)
    {
        var aimRadians = sentry.AimDirectionDegrees * (MathF.PI / 180f);
        var reflectedCount = ReflectEnemyExplosiveProjectiles(
            owner,
            aimRadians,
            sentry.X,
            sentry.Y,
            radial: true,
            radialRadius: global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerIntegrityProjectorRadius);
        if (reflectedCount > 0)
        {
            RegisterWorldSoundEvent("AirblastSnd", sentry.X, sentry.Y);
            RegisterVisualEffect("Poof", sentry.X, sentry.Y, sentry.AimDirectionDegrees);
        }
    }

    private void ApplyExperimentalConfusionFieldAura(SentryEntity sentry)
    {
        var confusionTicks = GetExperimentalEngineerConfusionTargetTicks();
        var auraRadius = global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerConfusionFieldAuraRadius;
        foreach (var candidate in EnumerateSimulatedPlayers())
        {
            if (!candidate.IsAlive
                || candidate.Team == sentry.Team
                || DistanceBetween(sentry.X, sentry.Y, candidate.X, candidate.Y) > auraRadius)
            {
                continue;
            }

            candidate.RefreshExperimentalConfusion(confusionTicks);
            if (candidate.ExperimentalConfusedAttackTargetPlayerId.HasValue)
            {
                continue;
            }

            if (!ShouldTriggerExperimentalConfusionRetarget(candidate.Id, sentry.Id))
            {
                continue;
            }

            var allyTarget = FindNearestExperimentalConfusionFriendlyTarget(candidate);
            if (allyTarget is null)
            {
                continue;
            }

            candidate.SetExperimentalConfusedAttackTarget(allyTarget.Id, confusionTicks);
        }
    }

    private bool ShouldTriggerExperimentalConfusionRetarget(int playerId, int sentryId)
    {
        var cadence = Math.Max(1, Config.TicksPerSecond / 5);
        var frame = (ulong)Frame;
        if ((frame + (ulong)(playerId * 7) + (ulong)(sentryId * 13)) % (ulong)cadence != 0)
        {
            return false;
        }

        var roll = (int)((frame + (ulong)(playerId * 17) + (ulong)(sentryId * 19)) % 100UL);
        return roll < 35;
    }

    private PlayerEntity? FindNearestExperimentalConfusionFriendlyTarget(PlayerEntity confusedPlayer)
    {
        PlayerEntity? bestTarget = null;
        var bestDistance = float.MaxValue;
        foreach (var candidate in EnumerateSimulatedPlayers())
        {
            if (!candidate.IsAlive
                || candidate.Id == confusedPlayer.Id
                || candidate.Team != confusedPlayer.Team)
            {
                continue;
            }

            var distance = DistanceBetween(confusedPlayer.X, confusedPlayer.Y, candidate.X, candidate.Y);
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            bestTarget = candidate;
        }

        return bestTarget;
    }

    private int ApplyExperimentalIncomingSentryDamageMultiplier(SentryEntity sentry, int damage)
    {
        if (damage <= 0)
        {
            return damage;
        }

        var owner = FindPlayerById(sentry.OwnerPlayerId);
        if (!IsExperimentalEngineerPerkOwner(owner)
            || !ExperimentalGameplaySettings.EnableEngineerHardwareHardener
            || sentry.Health <= (sentry.MaxHealth / 2))
        {
            return damage;
        }

        var scaledDamage = damage * (1f - global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerHardwareHardenerDamageResistance);
        return Math.Max(1, (int)MathF.Round(scaledDamage));
    }

    private bool IsExperimentalEngineerPriorityTarget(PlayerEntity owner, PlayerEntity candidate)
    {
        return ExperimentalGameplaySettings.EnableEngineerCooperativeTargetingHarness
            && IsExperimentalEngineerPerkOwner(owner)
            && !HasExperimentalEngineerDestinyPunctuator(owner)
            && candidate.LastDamageDealerPlayerId == owner.Id
            && candidate.LastDamageDealerAssistTicksRemaining > 0;
    }

    private float GetExperimentalOutgoingSentryDamageMultiplier(PlayerEntity owner, PlayerEntity target)
    {
        if (!IsExperimentalEngineerPriorityTarget(owner, target))
        {
            return 1f;
        }

        return global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerCooperativeTargetingHarnessDamageMultiplier;
    }

    private void ApplyExperimentalSentryDamageRewards(SentryEntity sentry, PlayerEntity owner, int appliedDamage)
    {
        if (appliedDamage <= 0
            || !IsExperimentalEngineerPerkOwner(owner))
        {
            return;
        }

        if (ExperimentalGameplaySettings.EnableEngineerOsmosisConductor)
        {
            ApplyHealingWithFeedback(owner, appliedDamage);
        }

        if (ExperimentalGameplaySettings.EnableEngineerAlchemicalAnode)
        {
            sentry.Heal(Math.Max(1, (int)MathF.Round(appliedDamage * global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerAlchemicalAnodeHealingFraction)));
        }
    }

    private void ApplyExperimentalPlayerDamageToOwnedSentries(PlayerEntity owner, int appliedDamage)
    {
        if (appliedDamage <= 0
            || !IsExperimentalEngineerPerkOwner(owner)
            || !ExperimentalGameplaySettings.EnableEngineerOsmosisConductor
            || HasExperimentalEngineerDestinyPunctuator(owner))
        {
            return;
        }

        for (var sentryIndex = 0; sentryIndex < _sentries.Count; sentryIndex += 1)
        {
            var sentry = _sentries[sentryIndex];
            if (sentry.OwnerPlayerId == owner.Id)
            {
                sentry.Heal(appliedDamage);
            }
        }
    }

    private void ApplyExperimentalEngineerPlayerDamageRewards(PlayerEntity owner, int appliedDamage)
    {
        if (appliedDamage <= 0
            || !IsExperimentalEngineerPerkOwner(owner)
            || !ExperimentalGameplaySettings.EnableEngineerMateriaRecycler)
        {
            return;
        }

        owner.AddMetal(appliedDamage * global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerMateriaRecyclerMetalPerDamage);
    }

    private void DestroyExperimentalEngineerOwnedSentries(int ownerPlayerId)
    {
        for (var sentryIndex = _sentries.Count - 1; sentryIndex >= 0; sentryIndex -= 1)
        {
            if (_sentries[sentryIndex].OwnerPlayerId == ownerPlayerId)
            {
                DestroySentry(_sentries[sentryIndex], attacker: null);
            }
        }
    }

    private void ApplyExperimentalEngineerFriendlyFireRetaliation(PlayerEntity attacker, PlayerEntity target, int appliedDamage)
    {
        if (appliedDamage <= 0
            || !ExperimentalGameplaySettings.EnableEngineerConfusionField
            || attacker.Team != target.Team
            || !attacker.IsExperimentalConfused
            || attacker.Id == target.Id)
        {
            return;
        }

        attacker.MarkExperimentalConfusionRetaliation(int.MaxValue / 4);
    }

    private static int GetExperimentalEngineerCryoFreezeThresholdHits()
    {
        return Math.Max(1, global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerCryonicMunitionsFreezeHitCount);
    }

    private int GetExperimentalEngineerCryoFreezeDurationTicks()
    {
        return Math.Max(
            1,
            (int)MathF.Round(
                Config.TicksPerSecond * global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerCryonicMunitionsFreezeDurationSeconds));
    }

    private int GetExperimentalEngineerCryoSlowTicks()
    {
        return Math.Max(
            1,
            (int)MathF.Round(
                Config.TicksPerSecond * global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerCryonicMunitionsSlowRefreshSeconds));
    }

    private int GetExperimentalEngineerEssenceExtractorDebuffTicks()
    {
        return Math.Max(
            1,
            (int)MathF.Round(
                Config.TicksPerSecond * global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerEssenceExtractorDebuffSeconds));
    }

    private int GetExperimentalEngineerFreezeRaySlowTicks()
    {
        return Math.Max(
            1,
            (int)MathF.Round(
                Config.TicksPerSecond * global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerFreezeRaySlowRefreshSeconds));
    }

    private int GetExperimentalEngineerFreezeRayFreezeThresholdTicks()
    {
        return Math.Max(
            1,
            (int)MathF.Round(
                Config.TicksPerSecond * global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerFreezeRayFreezeDelaySeconds));
    }

    private int GetExperimentalEngineerFreezeRayExposureWindowTicks()
    {
        return Math.Max(
            1,
            (int)MathF.Round(
                Config.TicksPerSecond * global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerFreezeRayExposureWindowSeconds));
    }

    private int GetExperimentalEngineerConfusionTargetTicks()
    {
        return Math.Max(
            1,
            (int)MathF.Round(
                Config.TicksPerSecond * global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerConfusionFieldTargetLockSeconds));
    }

    private static float GetExperimentalEngineerCaveatTurnRateRadians()
    {
        return global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerCaveatInjectorRocketTurnRateDegrees * (MathF.PI / 180f);
    }

    private int GetExperimentalEngineerCaveatLockDelayTicks()
    {
        return Math.Max(
            0,
            (int)MathF.Round(
                Config.TicksPerSecond * global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerCaveatInjectorRocketLockDelaySeconds));
    }

    private bool FireExperimentalSentry(SentryEntity sentry, PlayerEntity owner, SentryTarget target, int reloadTicks, int idleResetTicks)
    {
        sentry.FireAt(target.X, target.Y, reloadTicks, idleResetTicks);

        if (ExperimentalGameplaySettings.EnableEngineerPrecisionInstantiator)
        {
            FireExperimentalPrecisionSentryShot(sentry, owner, target);
            return true;
        }

        if (ExperimentalGameplaySettings.EnableEngineerBuckshotConversion)
        {
            FireExperimentalBuckshotSentryShot(sentry, owner, target);
            return true;
        }

        FireExperimentalDefaultSentryShot(sentry, owner, target, reloadTicks);
        return true;
    }

    private void FireExperimentalDefaultSentryShot(SentryEntity sentry, PlayerEntity owner, SentryTarget target, int reloadTicks)
    {
        var distance = DistanceBetween(sentry.X, sentry.Y, target.X, target.Y);
        if (distance > 0f)
        {
            RegisterCombatTrace(
                sentry.X,
                sentry.Y,
                (target.X - sentry.X) / distance,
                (target.Y - sentry.Y) / distance,
                distance,
                target.Player is not null);
        }

        if (target.Player is not null)
        {
            ApplyExperimentalSentryPlayerHit(sentry, owner, target.Player, SentryEntity.HitDamage);
        }
        else if (target.Generator is not null)
        {
            TryDamageGenerator(target.Generator.Team, SentryEntity.HitDamage, owner);
        }

        if (ExperimentalGameplaySettings.EnableEngineerCaveatInjector
            && sentry.ConsecutiveShotsFired % global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerCaveatInjectorShotInterval == 0)
        {
            FireExperimentalCaveatMiniRockets(sentry, owner, target);
        }
    }

    private void FireExperimentalBuckshotSentryShot(SentryEntity sentry, PlayerEntity owner, SentryTarget target)
    {
        var distance = DistanceBetween(sentry.X, sentry.Y, target.X, target.Y);
        if (distance > 0f)
        {
            RegisterCombatTrace(
                sentry.X,
                sentry.Y,
                (target.X - sentry.X) / distance,
                (target.Y - sentry.Y) / distance,
                distance,
                target.Player is not null);
        }

        if (target.Player is not null)
        {
            ApplyExperimentalSentryPlayerHit(sentry, owner, target.Player, SentryEntity.HitDamage);
        }
        else if (target.Generator is not null)
        {
            TryDamageGenerator(target.Generator.Team, SentryEntity.HitDamage, owner);
        }

        int pelletDamage = Math.Max(
            1,
            (int)MathF.Round(CharacterClassCatalog.Scattergun.DirectHitDamage ?? ShotProjectileEntity.DamagePerHit));
        var pelletCount = Math.Max(
            1,
            (int)MathF.Round(
                CharacterClassCatalog.Scattergun.ProjectilesPerShot
                * global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerBuckshotConversionPelletMultiplier));
        var baseAngle = MathF.Atan2(target.Y - sentry.Y, target.X - sentry.X);
        for (var pelletIndex = 0; pelletIndex < pelletCount; pelletIndex += 1)
        {
            var spreadRadians = DegreesToRadians(
                ((_random.NextSingle() * 2f) - 1f)
                * CharacterClassCatalog.Scattergun.SpreadDegrees
                * 1.25f);
            var pelletAngle = baseAngle + spreadRadians;
            var directionX = MathF.Cos(pelletAngle);
            var directionY = MathF.Sin(pelletAngle);
            var pelletSpeed = CharacterClassCatalog.Scattergun.MinShotSpeed
                + (_random.NextSingle() * CharacterClassCatalog.Scattergun.AdditionalRandomShotSpeed);
            var spawnX = sentry.X + directionX * 14f;
            var spawnY = sentry.Y + directionY * 14f;
            if (IsProjectileSpawnBlocked(sentry.X, sentry.Y, spawnX, spawnY))
            {
                RegisterImpactEffect(spawnX, spawnY, MathF.Atan2(directionY, directionX) * (180f / MathF.PI));
                continue;
            }

            SpawnShot(
                owner,
                spawnX,
                spawnY,
                directionX * pelletSpeed,
                directionY * pelletSpeed,
                pelletDamage,
                killFeedWeaponSpriteNameOverride: "TurretKL",
                sourceSentryId: sentry.Id,
                applyExperimentalEngineerSentryPerkEffects: true);
        }

        if (ExperimentalGameplaySettings.EnableEngineerCaveatInjector
            && sentry.ConsecutiveShotsFired % global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerCaveatInjectorShotInterval == 0)
        {
            FireExperimentalCaveatMiniRockets(sentry, owner, target);
        }
    }

    private void FireExperimentalPrecisionSentryShot(SentryEntity sentry, PlayerEntity owner, SentryTarget target)
    {
        var deltaX = target.X - sentry.X;
        var deltaY = target.Y - sentry.Y;
        var distance = MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
        if (distance <= 0.001f)
        {
            return;
        }

        var directionX = deltaX / distance;
        var directionY = deltaY / distance;
        var hit = ResolveRifleHit(owner, sentry.X, sentry.Y, directionX, directionY, GetExperimentalSentryTargetRange(owner));
        RegisterCombatTrace(sentry.X, sentry.Y, directionX, directionY, hit.Distance, hit.HitPlayer is not null, sentry.Team, isSniperTracer: true);
        if (hit.HitPlayer is not null)
        {
            ApplyExperimentalSentryPlayerHit(
                sentry,
                owner,
                hit.HitPlayer,
                global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerPrecisionInstantiatorDamage);
        }
        else if (hit.HitSentry is not null && ApplySentryDamage(hit.HitSentry, global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerPrecisionInstantiatorDamage, owner))
        {
            DestroySentry(hit.HitSentry, owner);
        }
        else if (hit.HitGenerator is not null)
        {
            TryDamageGenerator(hit.HitGenerator.Team, global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerPrecisionInstantiatorDamage, owner);
        }
    }

    private void ApplyExperimentalSentryPlayerHit(SentryEntity sentry, PlayerEntity owner, PlayerEntity target, int baseDamage)
    {
        var appliedBaseDamage = Math.Max(
            1,
            (int)MathF.Round(
                baseDamage * GetExperimentalOutgoingSentryDamageMultiplier(owner, target)));
        RegisterBloodEffect(target.X, target.Y, sentry.AimDirectionDegrees - 180f, 2);
        if (!target.IsUbered)
        {
            var distance = DistanceBetween(sentry.X, sentry.Y, target.X, target.Y);
            if (distance > 0.001f)
            {
                var sentryKnockbackPerSecond = 0.5f * LegacyMovementModel.SourceTicksPerSecond;
                var directionX = (target.X - sentry.X) / distance;
                var directionY = (target.Y - sentry.Y) / distance;
                target.AddImpulse(directionX * sentryKnockbackPerSecond, directionY * sentryKnockbackPerSecond);
            }
        }

        var healthBefore = target.Health;
        if (ApplyPlayerDamage(target, appliedBaseDamage, owner, PlayerEntity.SpyDamageRevealAlpha, allowOsmosisHealOwnedSentries: false))
        {
            KillPlayer(target, killer: owner, weaponSpriteName: "TurretKL", deathCamMessage: "You were killed by the autogun of", deathCamSentry: sentry);
        }

        ApplyExperimentalSentryDamageRewards(sentry, owner, Math.Max(0, healthBefore - target.Health));

        if (!target.IsAlive)
        {
            return;
        }

        if (ExperimentalGameplaySettings.EnableEngineerIncendiaryEnhancements)
        {
            target.IgniteAfterburn(
                owner.Id,
                global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerIncendiaryEnhancementsBurnDurationSourceTicks,
                global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerIncendiaryEnhancementsBurnIntensity,
                afterburnFalloff: false,
                burnFalloffAmount: 0f);
            TrySpawnExperimentalIncendiaryFlames(sentry, owner, target);
        }

        if (ExperimentalGameplaySettings.EnableEngineerCryonicMunitions)
        {
            target.AccumulateExperimentalCryoHit(
                owner.Id,
                freezeThresholdHits: GetExperimentalEngineerCryoFreezeThresholdHits(),
                freezeDurationTicks: GetExperimentalEngineerCryoFreezeDurationTicks(),
                slowMovementMultiplier: global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerCryonicMunitionsSlowMovementMultiplier,
                slowTicks: GetExperimentalEngineerCryoSlowTicks());
        }
    }

    private void TrySpawnExperimentalIncendiaryFlames(SentryEntity sentry, PlayerEntity owner, PlayerEntity target)
    {
        var distance = DistanceBetween(sentry.X, sentry.Y, target.X, target.Y);
        if (distance > global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerIncendiaryEnhancementsCloseRange)
        {
            return;
        }

        var directionX = target.X - sentry.X;
        var directionY = target.Y - sentry.Y;
        var directionLength = MathF.Sqrt((directionX * directionX) + (directionY * directionY));
        if (directionLength <= 0.001f)
        {
            return;
        }

        directionX /= directionLength;
        directionY /= directionLength;
        for (var flameIndex = 0; flameIndex < global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerIncendiaryEnhancementsFlameCount; flameIndex += 1)
        {
            var spreadRadians = ((_random.NextSingle() * 2f) - 1f) * (8f * (MathF.PI / 180f));
            var directionRadians = MathF.Atan2(directionY, directionX) + spreadRadians;
            SpawnFlame(
                owner,
                sentry.X,
                sentry.Y - 8f,
                MathF.Cos(directionRadians) * global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerIncendiaryEnhancementsFlameSpeed,
                MathF.Sin(directionRadians) * global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerIncendiaryEnhancementsFlameSpeed);
        }
    }

    private void FireExperimentalCaveatMiniRockets(SentryEntity sentry, PlayerEntity owner, SentryTarget target)
    {
        var rocketCombat = new RocketCombatDefinition(
            global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerCaveatInjectorDirectHitDamage,
            global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerCaveatInjectorExplosionDamage,
            global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerCaveatInjectorBlastRadius,
            RocketProjectileEntity.SplashThresholdFactor);
        var baseAngle = MathF.Atan2(target.Y - sentry.Y, target.X - sentry.X);
        var spreadRadians = global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerCaveatInjectorSpreadDegrees * (MathF.PI / 180f);
        var lockDelayTicks = GetExperimentalEngineerCaveatLockDelayTicks();
        var rocketTravelDistance = GetExperimentalEngineerInfiniteTargetRange();
        for (var rocketIndex = 0; rocketIndex < global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerCaveatInjectorRocketCount; rocketIndex += 1)
        {
            var spreadOffset = (rocketIndex - 1) * spreadRadians;
            SpawnRocket(
                owner,
                sentry.X,
                sentry.Y - 6f,
                global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerCaveatInjectorRocketSpeed,
                baseAngle + spreadOffset,
                rocketCombat,
                explodeImmediately: false,
                canGrantExperimentalInstantReloadOnHit: false,
                enableExperimentalStingerTracking: false,
                enableExperimentalCaveatTracking: true,
                experimentalVisualScale: global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerCaveatInjectorRocketRenderScale,
                experimentalTrackingLockTicksRemaining: lockDelayTicks,
                killFeedWeaponSpriteNameOverride: "TurretKL");

            if (_rockets.Count > 0)
            {
                _rockets[^1].SetDistanceToTravel(rocketTravelDistance);
            }
        }
    }

    private bool TryResolveExperimentalEngineerRocketTrackingDirection(RocketProjectileEntity rocket, PlayerEntity owner, out float targetDirectionRadians)
    {
        targetDirectionRadians = 0f;
        if (!rocket.EnableExperimentalCaveatTracking
            || rocket.ExperimentalTrackingLockTicksRemaining > 0
            || !IsExperimentalEngineerPerkOwner(owner)
            || (!ExperimentalGameplaySettings.EnableEngineerCaveatInjector
                && !ExperimentalGameplaySettings.EnableEngineerExperimentalOverkillAugment))
        {
            return false;
        }

        PlayerEntity? bestTarget = null;
        var bestDistanceSquared = float.MaxValue;
        foreach (var candidate in EnumerateSimulatedPlayers())
        {
            if (!candidate.IsAlive || candidate.Team == owner.Team)
            {
                continue;
            }

            var deltaX = candidate.X - rocket.X;
            var deltaY = candidate.Y - rocket.Y;
            var distanceSquared = (deltaX * deltaX) + (deltaY * deltaY);
            if (distanceSquared >= bestDistanceSquared)
            {
                continue;
            }

            bestDistanceSquared = distanceSquared;
            bestTarget = candidate;
        }

        if (bestTarget is null)
        {
            return false;
        }

        targetDirectionRadians = MathF.Atan2(bestTarget.Y - rocket.Y, bestTarget.X - rocket.X);
        _ = rocket.TryApplyExperimentalCaveatLockSpeedBurst(
            global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerCaveatInjectorPostLockSpeedMultiplier);
        return true;
    }
}
