namespace OpenGarrison.Core;

internal sealed partial class ExperimentalRulesSystem
{
    internal bool IsExperimentalEngineerPerkOwner(PlayerEntity? player)
    {
        return player is not null
            && IsExperimentalPracticePowerOwner(player)
            && player.ClassId == PlayerClass.Engineer;
    }

    internal bool HasExperimentalEngineerEssenceExtractor(PlayerEntity? player)
    {
        return HasExperimentalEngineerEssenceExtractorAvailable(player)
            && IsExperimentalEngineerAlternateWeaponModeActive(player, ExperimentalEngineerAlternateWeaponMode.EssenceExtractor);
    }

    internal bool HasExperimentalEngineerFreezeRay(PlayerEntity? player)
    {
        return HasExperimentalEngineerFreezeRayAvailable(player)
            && IsExperimentalEngineerAlternateWeaponModeActive(player, ExperimentalEngineerAlternateWeaponMode.FreezeRay);
    }

    internal bool HasExperimentalEngineerDestinyPunctuator(PlayerEntity? player)
    {
        return IsExperimentalEngineerPerkOwner(player)
            && _host.LastToDieRules.GetLastToDieGameplaySettings(player!).EnableEngineerDestinyPunctuator;
    }

    private bool HasExperimentalEngineerEssenceExtractorAvailable(PlayerEntity? player)
    {
        return IsExperimentalEngineerPerkOwner(player)
            && _host.LastToDieRules.GetLastToDieGameplaySettings(player!).EnableEngineerEssenceExtractor;
    }

    internal bool HasExperimentalEngineerFreezeRayAvailable(PlayerEntity? player)
    {
        return IsExperimentalEngineerPerkOwner(player)
            && _host.LastToDieRules.GetLastToDieGameplaySettings(player!).EnableEngineerFreezeRay;
    }

    internal bool HasExperimentalEngineerAlternateWeaponAvailable(PlayerEntity? player)
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

    internal ExperimentalEngineerAlternateWeaponMode GetExperimentalEngineerDefaultAlternateWeaponMode(PlayerEntity? player)
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

    internal void ClearExperimentalEngineerAlternateWeaponState(PlayerEntity player, bool flushEssenceHealing = true)
    {
        if (flushEssenceHealing
            && player.ExperimentalEngineerAlternateWeaponMode == ExperimentalEngineerAlternateWeaponMode.EssenceExtractor)
        {
            _host.SupportRules.FlushExperimentalEngineerEssenceExtractorHealing(player);
        }

        player.ClearMedicHealingTarget();
    }

    internal bool IsExperimentalEngineerFloatingSentry(SentryEntity sentry)
    {
        if (_host.Structures.IsLastToDieDroneSentry(sentry))
        {
            return true;
        }

        var owner = _host.FindPlayerById(sentry.OwnerPlayerId);
        return sentry.IsBuilt
            && IsExperimentalEngineerPerkOwner(owner)
            && _host.LastToDieRules.GetLastToDieGameplaySettings(owner).EnableEngineerAutonomousPhaseEngine;
    }

    internal static bool ShouldTreatPlayerAsExperimentalFriendlyFireTarget(PlayerEntity observer, PlayerEntity candidate)
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

    internal int GetExperimentalOwnedSentryCount(int ownerPlayerId)
    {
        var count = 0;
        for (var sentryIndex = 0; sentryIndex < _host.WorldObjects.Sentries.Count; sentryIndex += 1)
        {
            if (_host.WorldObjects.Sentries[sentryIndex].OwnerPlayerId == ownerPlayerId
                && !_host.WorldObjects.Sentries[sentryIndex].IsDispenser)
            {
                count += 1;
            }
        }

        return count;
    }

    internal int GetExperimentalMaxOwnedSentries(PlayerEntity player)
    {
        if (!IsExperimentalEngineerPerkOwner(player))
        {
            return 1;
        }

        if (HasExperimentalEngineerDestinyPunctuator(player))
        {
            return 0;
        }

        var settings = _host.LastToDieRules.GetLastToDieGameplaySettings(player);
        return 1
            + (settings.EnableEngineerOutputInducer
                ? global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerOutputInducerAdditionalSentries
                : 0);
    }

    internal int GetExperimentalSentryMaxHealth(PlayerEntity owner)
    {
        var maxHealth = SentryEntity.DefaultMaxHealth;
        if (!IsExperimentalEngineerPerkOwner(owner))
        {
            return maxHealth;
        }

        var settings = _host.LastToDieRules.GetLastToDieGameplaySettings(owner);
        if (settings.EnableEngineerGuardianMatrix)
        {
            maxHealth += global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerGuardianMatrixSentryBonusHealth;
        }

        if (settings.EnableEngineerHardwareHardener)
        {
            maxHealth += global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerHardwareHardenerSentryBonusHealth;
        }

        return maxHealth;
    }

    internal int GetExperimentalSentryReloadTicks(PlayerEntity owner, SentryEntity sentry)
    {
        var reloadTicks = SentryEntity.ReloadTicks;
        if (!IsExperimentalEngineerPerkOwner(owner))
        {
            return reloadTicks;
        }

        var settings = _host.LastToDieRules.GetLastToDieGameplaySettings(owner);
        if (settings.EnableEngineerPrecisionInstantiator)
        {
            reloadTicks = Math.Max(
                1,
                (int)MathF.Round(
                    reloadTicks * global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerPrecisionInstantiatorReloadMultiplier));
        }
        else if (settings.EnableEngineerBuckshotConversion)
        {
            reloadTicks = Math.Max(
                1,
                (int)MathF.Round(
                    reloadTicks * global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerBuckshotConversionReloadMultiplier));
        }

        if (!settings.EnableEngineerAmperageAccelerator)
        {
            return reloadTicks;
        }

        var shotsToMax = Math.Max(1, global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerAmperageAcceleratorShotsToMax);
        var rampFraction = Math.Clamp(sentry.ConsecutiveShotsFired / (float)shotsToMax, 0f, 1f);
        var rateOfFireMultiplier = 1f + ((global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerAmperageAcceleratorMaxRateOfFireMultiplier - 1f) * rampFraction);
        return Math.Max(1, (int)MathF.Round(reloadTicks / rateOfFireMultiplier));
    }

    internal int GetExperimentalSentryIdleResetTicks()
    {
        return Math.Max(
            1,
            (int)MathF.Round(
                _host.Config.TicksPerSecond * global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerAmperageAcceleratorResetSeconds));
    }

    internal float GetExperimentalSentryTargetRange(PlayerEntity owner)
    {
        if (IsExperimentalEngineerPerkOwner(owner)
            && (_host.LastToDieRules.GetLastToDieGameplaySettings(owner).EnableEngineerPrecisionInstantiator
                || _host.LastToDieRules.GetLastToDieGameplaySettings(owner).EnableEngineerCaveatInjector))
        {
            return GetExperimentalEngineerInfiniteTargetRange();
        }

        return SentryEntity.TargetRange;
    }

    private float GetExperimentalEngineerInfiniteTargetRange()
    {
        return MathF.Sqrt((_host.Bounds.Width * _host.Bounds.Width) + (_host.Bounds.Height * _host.Bounds.Height));
    }

    private static float GetExperimentalNearbySentryAuraRadius()
    {
        return global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerNearbySentryAuraRadius;
    }

    private float GetExperimentalEngineerMaxMetal(PlayerEntity player)
    {
        var maxMetal = 100f;
        if (IsExperimentalEngineerPerkOwner(player)
            && _host.LastToDieRules.GetLastToDieGameplaySettings(player).EnableEngineerMateriaRecycler)
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
            || !_host.LastToDieRules.GetLastToDieGameplaySettings(player).EnableEngineerEfficiencyStabilizer)
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
        for (var sentryIndex = 0; sentryIndex < _host.WorldObjects.Sentries.Count; sentryIndex += 1)
        {
            var sentry = _host.WorldObjects.Sentries[sentryIndex];
            if (sentry.OwnerPlayerId != player!.Id
                || !sentry.IsBuilt
                || SimulationMath.DistanceBetween(sentry.X, sentry.Y, player.X, player.Y) > auraRadius)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    internal bool IsPlayerInsideExperimentalEngineerMisdirectionFieldForVisuals(PlayerEntity? player)
    {
        return player is not null
            && _host.LastToDieRules.GetLastToDieGameplaySettings(player).EnableEngineerMisdirectionField
            && IsPlayerNearExperimentalOwnedSentry(player);
    }

    internal void ApplyExperimentalEngineerPassivePlayerEffects(PlayerEntity player)
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
            var stockSecondary = GameplaySecondaryWeaponResolver.Resolve(
                player,
                allowSoldierShotgun: false,
                allowSoldierShotgunLtd: false);
            var isLegacyAlternateSelected = player.IsExperimentalOffhandSelected
                && player.ExperimentalEngineerAlternateWeaponMode != ExperimentalEngineerAlternateWeaponMode.None;
            if (isLegacyAlternateSelected)
            {
                ClearExperimentalEngineerAlternateWeaponState(player);
                player.StowExperimentalOffhandWeapon();
            }

            player.SetExperimentalOffhandWeapon(stockSecondary);
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

        var settings = _host.LastToDieRules.GetLastToDieGameplaySettings(player);
        if (settings.EnableEngineerRegenerativeDiode)
        {
            _host.DamageRules.ApplyHealingWithFeedback(
                player,
                global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerRegenerativeDiodeHealthPerSecond / Math.Max(1, _host.Config.TicksPerSecond));
        }

        if (settings.EnableEngineerGuardianMatrix && IsPlayerNearExperimentalOwnedSentry(player))
        {
            player.SetExperimentalShieldHealth(global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerGuardianMatrixShieldHealth);
            return;
        }

        player.ClearExperimentalShield();
    }

    internal void ApplyExperimentalEngineerSentryPassiveEffects(SentryEntity sentry, PlayerEntity owner)
    {
        if (!IsExperimentalEngineerPerkOwner(owner))
        {
            return;
        }

        if (HasExperimentalEngineerDestinyPunctuator(owner))
        {
            return;
        }

        var settings = _host.LastToDieRules.GetLastToDieGameplaySettings(owner);
        if (settings.EnableEngineerRegenerativeDiode
            && sentry.Health < sentry.MaxHealth)
        {
            sentry.ApplyContinuousHealing(
                global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerRegenerativeDiodeHealthPerSecond / Math.Max(1, _host.Config.TicksPerSecond));
        }

        if (sentry.IsBuilt)
        {
            if (settings.EnableEngineerAutonomousPhaseEngine)
            {
                AdvanceExperimentalAutonomousSentry(sentry, owner);
            }

            if (settings.EnableEngineerIntegrityProjector)
            {
                TryApplyExperimentalIntegrityProjector(sentry, owner);
            }

            if (settings.EnableEngineerConfusionField)
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
        for (var sentryIndex = 0; sentryIndex < _host.WorldObjects.Sentries.Count; sentryIndex += 1)
        {
            var candidate = _host.WorldObjects.Sentries[sentryIndex];
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
        var reflectedCount = _host.ReflectEnemyExplosiveProjectiles(
            owner,
            aimRadians,
            sentry.X,
            sentry.Y,
            radial: true,
            radialRadius: global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerIntegrityProjectorRadius);
        if (reflectedCount > 0)
        {
            _host.WorldEffects.RegisterWorldSoundEvent("AirblastSnd", sentry.X, sentry.Y);
            _host.WorldEffects.RegisterVisualEffect("Poof", sentry.X, sentry.Y, sentry.AimDirectionDegrees);
        }
    }

    private void ApplyExperimentalConfusionFieldAura(SentryEntity sentry)
    {
        var confusionTicks = GetExperimentalEngineerConfusionTargetTicks();
        var auraRadius = global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerConfusionFieldAuraRadius;
        foreach (var candidate in _host.EnumerateSimulatedPlayers())
        {
            if (!candidate.IsAlive
                || candidate.Team == sentry.Team
                || SimulationMath.DistanceBetween(sentry.X, sentry.Y, candidate.X, candidate.Y) > auraRadius)
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
        var cadence = Math.Max(1, _host.Config.TicksPerSecond / 5);
        var frame = (ulong)_host.Frame;
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
        foreach (var candidate in _host.EnumerateSimulatedPlayers())
        {
            if (!candidate.IsAlive
                || candidate.Id == confusedPlayer.Id
                || candidate.Team != confusedPlayer.Team)
            {
                continue;
            }

            var distance = SimulationMath.DistanceBetween(confusedPlayer.X, confusedPlayer.Y, candidate.X, candidate.Y);
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            bestTarget = candidate;
        }

        return bestTarget;
    }

    internal int ApplyExperimentalIncomingSentryDamageMultiplier(SentryEntity sentry, int damage)
    {
        if (damage <= 0)
        {
            return damage;
        }

        var owner = _host.FindPlayerById(sentry.OwnerPlayerId);
        if (!IsExperimentalEngineerPerkOwner(owner)
            || !_host.LastToDieRules.GetLastToDieGameplaySettings(owner!).EnableEngineerHardwareHardener
            || sentry.Health <= (sentry.MaxHealth / 2))
        {
            return damage;
        }

        var scaledDamage = damage * (1f - global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerHardwareHardenerDamageResistance);
        return Math.Max(1, (int)MathF.Round(scaledDamage));
    }

    internal bool IsExperimentalEngineerPriorityTarget(PlayerEntity owner, PlayerEntity candidate)
    {
        return _host.LastToDieRules.GetLastToDieGameplaySettings(owner).EnableEngineerCooperativeTargetingHarness
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

    internal void ApplyExperimentalSentryDamageRewards(SentryEntity sentry, PlayerEntity owner, int appliedDamage)
    {
        if (appliedDamage <= 0
            || !IsExperimentalEngineerPerkOwner(owner))
        {
            return;
        }

        var settings = _host.LastToDieRules.GetLastToDieGameplaySettings(owner);
        if (settings.EnableEngineerOsmosisConductor)
        {
            _host.DamageRules.ApplyHealingWithFeedback(owner, appliedDamage);
        }

        if (settings.EnableEngineerAlchemicalAnode)
        {
            sentry.Heal(Math.Max(1, (int)MathF.Round(appliedDamage * global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerAlchemicalAnodeHealingFraction)));
        }
    }

    private void ApplyExperimentalPlayerDamageToOwnedSentries(PlayerEntity owner, int appliedDamage)
    {
        if (appliedDamage <= 0
            || !IsExperimentalEngineerPerkOwner(owner)
            || !_host.LastToDieRules.GetLastToDieGameplaySettings(owner).EnableEngineerOsmosisConductor
            || HasExperimentalEngineerDestinyPunctuator(owner))
        {
            return;
        }

        for (var sentryIndex = 0; sentryIndex < _host.WorldObjects.Sentries.Count; sentryIndex += 1)
        {
            var sentry = _host.WorldObjects.Sentries[sentryIndex];
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
            || !_host.LastToDieRules.GetLastToDieGameplaySettings(owner).EnableEngineerMateriaRecycler)
        {
            return;
        }

        owner.AddMetal(appliedDamage * global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerMateriaRecyclerMetalPerDamage);
    }

    private void DestroyExperimentalEngineerOwnedSentries(int ownerPlayerId)
    {
        for (var sentryIndex = _host.WorldObjects.Sentries.Count - 1; sentryIndex >= 0; sentryIndex -= 1)
        {
            if (_host.WorldObjects.Sentries[sentryIndex].OwnerPlayerId == ownerPlayerId)
            {
                _host.Structures.DestroySentry(_host.WorldObjects.Sentries[sentryIndex], attacker: null);
            }
        }
    }

    internal void ApplyExperimentalEngineerFriendlyFireRetaliation(PlayerEntity attacker, PlayerEntity target, int appliedDamage)
    {
        if (appliedDamage <= 0
            || !_host.LastToDieRules.GetLastToDieGameplaySettings(attacker).EnableEngineerConfusionField
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

    internal int GetExperimentalEngineerCryoFreezeDurationTicks()
    {
        return Math.Max(
            1,
            (int)MathF.Round(
                _host.Config.TicksPerSecond * global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerCryonicMunitionsFreezeDurationSeconds));
    }

    private int GetExperimentalEngineerCryoSlowTicks()
    {
        return Math.Max(
            1,
            (int)MathF.Round(
                _host.Config.TicksPerSecond * global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerCryonicMunitionsSlowRefreshSeconds));
    }

    internal int GetExperimentalEngineerEssenceExtractorDebuffTicks()
    {
        return Math.Max(
            1,
            (int)MathF.Round(
                _host.Config.TicksPerSecond * global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerEssenceExtractorDebuffSeconds));
    }

    internal int GetExperimentalEngineerFreezeRaySlowTicks()
    {
        return Math.Max(
            1,
            (int)MathF.Round(
                _host.Config.TicksPerSecond * global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerFreezeRaySlowRefreshSeconds));
    }

    internal int GetExperimentalEngineerFreezeRayFreezeThresholdTicks()
    {
        return Math.Max(
            1,
            (int)MathF.Round(
                _host.Config.TicksPerSecond * global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerFreezeRayFreezeDelaySeconds));
    }

    internal int GetExperimentalEngineerFreezeRayExposureWindowTicks()
    {
        return Math.Max(
            1,
            (int)MathF.Round(
                _host.Config.TicksPerSecond * global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerFreezeRayExposureWindowSeconds));
    }

    private int GetExperimentalEngineerConfusionTargetTicks()
    {
        return Math.Max(
            1,
            (int)MathF.Round(
                _host.Config.TicksPerSecond * global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerConfusionFieldTargetLockSeconds));
    }

    internal static float GetExperimentalEngineerCaveatTurnRateRadians()
    {
        return global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerCaveatInjectorRocketTurnRateDegrees * (MathF.PI / 180f);
    }

    private int GetExperimentalEngineerCaveatLockDelayTicks()
    {
        return Math.Max(
            0,
            (int)MathF.Round(
                _host.Config.TicksPerSecond * global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerCaveatInjectorRocketLockDelaySeconds));
    }

    internal bool FireExperimentalSentry(SentryEntity sentry, PlayerEntity owner, SentryTarget target, int reloadTicks, int idleResetTicks)
    {
        sentry.FireAt(target.X, target.Y, reloadTicks, idleResetTicks);
        var ownerHasExperimentalEngineerPerks = IsExperimentalEngineerPerkOwner(owner);

        var settings = _host.LastToDieRules.GetLastToDieGameplaySettings(owner);
        if (ownerHasExperimentalEngineerPerks
            && settings.EnableEngineerPrecisionInstantiator)
        {
            FireExperimentalPrecisionSentryShot(sentry, owner, target);
            return true;
        }

        if (ownerHasExperimentalEngineerPerks
            && settings.EnableEngineerBuckshotConversion)
        {
            FireExperimentalBuckshotSentryShot(sentry, owner, target);
            return true;
        }

        FireExperimentalDefaultSentryShot(sentry, owner, target);
        return true;
    }

    private void FireExperimentalDefaultSentryShot(SentryEntity sentry, PlayerEntity owner, SentryTarget target)
    {
        var distance = SimulationMath.DistanceBetween(sentry.X, sentry.Y, target.X, target.Y);
        if (distance > 0f)
        {
            _host.WorldEffects.RegisterCombatTrace(
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
        else
        {
            ApplyExperimentalSentryStructuralTargetDamage(sentry, target, owner, SentryEntity.HitDamage);
        }

        if (IsExperimentalEngineerPerkOwner(owner)
            && _host.LastToDieRules.GetLastToDieGameplaySettings(owner).EnableEngineerCaveatInjector
            && sentry.ConsecutiveShotsFired % global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerCaveatInjectorShotInterval == 0)
        {
            FireExperimentalCaveatMiniRockets(sentry, owner, target);
        }
    }

    private void FireExperimentalBuckshotSentryShot(SentryEntity sentry, PlayerEntity owner, SentryTarget target)
    {
        var distance = SimulationMath.DistanceBetween(sentry.X, sentry.Y, target.X, target.Y);
        if (distance > 0f)
        {
            _host.WorldEffects.RegisterCombatTrace(
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
        else
        {
            ApplyExperimentalSentryStructuralTargetDamage(sentry, target, owner, SentryEntity.HitDamage);
        }

        int pelletDamage = Math.Max(
            1,
            (int)MathF.Round(CharacterClassCatalog.Scattergun.DirectHitDamage ?? ShotProjectileEntity.DamagePerHit));
        var pelletCount = Math.Max(
            1,
            (int)MathF.Round(
                CharacterClassCatalog.Scattergun.ProjectilesPerShot
                * global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerBuckshotConversionPelletMultiplier));
        var pelletKnockback = BulletKnockbackRules.ResolvePayload(
            CharacterClassCatalog.Scattergun,
            pelletCount);
        var baseAngle = DeterministicMath.Atan2(target.Y - sentry.Y, target.X - sentry.X);
        for (var pelletIndex = 0; pelletIndex < pelletCount; pelletIndex += 1)
        {
            var spreadRadians = SimulationMath.DegreesToRadians(
                ((_host.Randoms.Gameplay.NextSingle() * 2f) - 1f)
                * CharacterClassCatalog.Scattergun.SpreadDegrees
                * 1.25f);
            var pelletAngle = baseAngle + spreadRadians;
            var directionX = DeterministicMath.Cos(pelletAngle);
            var directionY = DeterministicMath.Sin(pelletAngle);
            var pelletSpeed = CharacterClassCatalog.Scattergun.MinShotSpeed
                + (_host.Randoms.Gameplay.NextSingle() * CharacterClassCatalog.Scattergun.AdditionalRandomShotSpeed);
            var spawnX = sentry.X + directionX * 14f;
            var spawnY = sentry.Y + directionY * 14f;
            if (_host.GeometryResolver.IsProjectileSpawnBlocked(sentry.X, sentry.Y, spawnX, spawnY, sentry.Team))
            {
                _host.WorldEffects.RegisterImpactEffect(spawnX, spawnY, DeterministicMath.Atan2(directionY, directionX) * (180f / MathF.PI));
                continue;
            }

            _host.SpawnShot(
                owner,
                spawnX,
                spawnY,
                directionX * pelletSpeed,
                directionY * pelletSpeed,
                pelletDamage,
                killFeedWeaponSpriteNameOverride: "TurretKL",
                sourceSentryId: sentry.Id,
                applyExperimentalEngineerSentryPerkEffects: true,
                playerKnockbackImpulse: pelletKnockback.Impulse,
                playerKnockbackAirborneVerticalScale: pelletKnockback.AirborneVerticalScale,
                playerKnockbackGroundedVerticalScale: pelletKnockback.GroundedVerticalScale);
        }

        if (IsExperimentalEngineerPerkOwner(owner)
            && _host.LastToDieRules.GetLastToDieGameplaySettings(owner).EnableEngineerCaveatInjector
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
        if (target.DamageableZoneRoomObjectIndex is int damageableZoneIndex)
        {
            _host.WorldEffects.RegisterCombatTrace(sentry.X, sentry.Y, directionX, directionY, distance, hitCharacter: false, sentry.Team, isSniperTracer: true);
            _host.MapLogic.TryApplyDamageableZoneDamage(
                damageableZoneIndex,
                global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerPrecisionInstantiatorDamage,
                sentry.Team);
            return;
        }

        var hit = _host.GeometryResolver.ResolveRifleHit(owner, sentry.X, sentry.Y, directionX, directionY, GetExperimentalSentryTargetRange(owner));
        _host.WorldEffects.RegisterCombatTrace(sentry.X, sentry.Y, directionX, directionY, hit.Distance, hit.HitPlayer is not null, sentry.Team, isSniperTracer: true);
        if (hit.HitPlayer is not null)
        {
            ApplyExperimentalSentryPlayerHit(
                sentry,
                owner,
                hit.HitPlayer,
                global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerPrecisionInstantiatorDamage);
        }
        else if (hit.HitSentry is not null
            && _host.Combat.ApplySentryDamage(
                hit.HitSentry,
                ResolveExperimentalSentryOverdriveDamage(
                    sentry,
                    global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerPrecisionInstantiatorDamage),
                owner))
        {
            _host.Structures.DestroySentry(hit.HitSentry, owner);
        }
        else if (hit.HitGenerator is not null)
        {
            _host.ObjectiveRules.TryDamageGenerator(
                hit.HitGenerator.Team,
                ResolveExperimentalSentryOverdriveDamage(
                    sentry,
                    global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerPrecisionInstantiatorDamage),
                owner);
        }
        else if (hit.HitJumpPad is not null)
        {
            hit.HitJumpPad.TakeDamage(
                ResolveExperimentalSentryOverdriveDamage(
                    sentry,
                    global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerPrecisionInstantiatorDamage));
        }
    }

    private static int ResolveExperimentalSentryOverdriveDamage(SentryEntity sentry, int baseDamage)
    {
        var multiplier = sentry.IsOverdriveActive
            ? WhippingCordCatalog.AutogunOverdriveDamageMultiplier
            : 1f;
        return Math.Max(1, (int)MathF.Round(baseDamage * multiplier));
    }

    internal void ApplyExperimentalSentryStructuralTargetDamage(SentryEntity sentry, SentryTarget target, PlayerEntity owner, float damage)
    {
        var appliedDamage = Math.Max(
            1,
            (int)MathF.Round(
                damage * (sentry.IsOverdriveActive ? WhippingCordCatalog.AutogunOverdriveDamageMultiplier : 1f)));
        if (target.Generator is not null)
        {
            _host.ObjectiveRules.TryDamageGenerator(target.Generator.Team, appliedDamage, owner);
            return;
        }

        if (target.Sentry is not null)
        {
            if (_host.Combat.ApplySentryDamage(target.Sentry, appliedDamage, owner))
            {
                _host.Structures.DestroySentry(target.Sentry, owner);
            }

            return;
        }

        if (target.JumpPad is not null)
        {
            target.JumpPad.TakeDamage(appliedDamage);
            return;
        }

        if (target.DamageableZoneRoomObjectIndex is int damageableZoneIndex)
        {
            _host.MapLogic.TryApplyDamageableZoneDamage(damageableZoneIndex, appliedDamage, sentry.Team);
        }
    }

    internal void ApplyExperimentalSentryPlayerHit(
        SentryEntity sentry,
        PlayerEntity owner,
        PlayerEntity target,
        int baseDamage,
        PlayerDamageTraits additionalTraits = PlayerDamageTraits.None,
        bool criticalBoost = false,
        bool useLiveAttackerCriticalBoost = true,
        float? threatSourceX = null,
        float? threatSourceY = null,
        BulletKnockbackPayload? knockbackPayload = null,
        float? impactDirectionX = null,
        float? impactDirectionY = null)
    {
        var appliedBaseDamage = Math.Max(
            1,
            (int)MathF.Round(
                baseDamage
                    * GetExperimentalOutgoingSentryDamageMultiplier(owner, target)
                    * (sentry.IsOverdriveActive ? WhippingCordCatalog.AutogunOverdriveDamageMultiplier : 1f)));
        _host.WorldEffects.RegisterBloodEffect(target.X, target.Y, sentry.AimDirectionDegrees - 180f, 2);
        var healthBefore = target.Health;
        var resolution = _host.ResolvePlayerDamageWithContext(
            target,
            appliedBaseDamage,
            owner,
            PlayerEntity.SpyDamageRevealAlpha,
            allowOsmosisHealOwnedSentries: false,
            civvieUmbrellaThreatSourceX: threatSourceX,
            civvieUmbrellaThreatSourceY: threatSourceY,
            civvieUmbrellaCriticalBoost: criticalBoost,
            civvieUmbrellaUseLiveAttackerCriticalBoost: useLiveAttackerCriticalBoost,
            additionalTraits: PlayerDamageTraits.Bullet | additionalTraits);
        if (resolution.ShouldApplyOnHitEffects && target.IsAlive)
        {
            var directionX = impactDirectionX ?? (target.X - sentry.X);
            var directionY = impactDirectionY ?? (target.Y - sentry.Y);
            BulletKnockbackRules.Apply(
                target,
                directionX,
                directionY,
                knockbackPayload ?? BulletKnockbackRules.CreateSentryPayload());
        }
        if (resolution.WasFatal)
        {
            _host.PlayerDeaths.KillPlayer(target, killer: owner, weaponSpriteName: "TurretKL", deathCamSentry: sentry);
        }

        ApplyExperimentalSentryDamageRewards(sentry, owner, Math.Max(0, healthBefore - target.Health));

        if (!resolution.ShouldApplyOnHitEffects
            || !target.IsAlive
            || !IsExperimentalEngineerPerkOwner(owner))
        {
            return;
        }

        var settings = _host.LastToDieRules.GetLastToDieGameplaySettings(owner);
        if (settings.EnableEngineerIncendiaryEnhancements)
        {
            target.IgniteAfterburn(
                owner.Id,
                global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerIncendiaryEnhancementsBurnDurationSourceTicks,
                global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerIncendiaryEnhancementsBurnIntensity,
                afterburnFalloff: false,
                burnFalloffAmount: 0f);
            TrySpawnExperimentalIncendiaryFlames(sentry, owner, target);
        }

        if (settings.EnableEngineerCryonicMunitions)
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
        var distance = SimulationMath.DistanceBetween(sentry.X, sentry.Y, target.X, target.Y);
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
            var spreadRadians = ((_host.Randoms.Gameplay.NextSingle() * 2f) - 1f) * (8f * (MathF.PI / 180f));
            var directionRadians = DeterministicMath.Atan2(directionY, directionX) + spreadRadians;
            _host.SpawnFlame(
                owner,
                sentry.X,
                sentry.Y - 8f,
                DeterministicMath.Cos(directionRadians) * global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerIncendiaryEnhancementsFlameSpeed,
                DeterministicMath.Sin(directionRadians) * global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerIncendiaryEnhancementsFlameSpeed);
        }
    }

    private void FireExperimentalCaveatMiniRockets(SentryEntity sentry, PlayerEntity owner, SentryTarget target)
    {
        var rocketCombat = new RocketCombatDefinition(
            global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerCaveatInjectorDirectHitDamage,
            global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerCaveatInjectorExplosionDamage,
            global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerCaveatInjectorBlastRadius,
            RocketProjectileEntity.SplashThresholdFactor);
        var baseAngle = DeterministicMath.Atan2(target.Y - sentry.Y, target.X - sentry.X);
        var spreadRadians = global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerCaveatInjectorSpreadDegrees * (MathF.PI / 180f);
        var lockDelayTicks = GetExperimentalEngineerCaveatLockDelayTicks();
        var rocketTravelDistance = GetExperimentalEngineerInfiniteTargetRange();
        for (var rocketIndex = 0; rocketIndex < global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerCaveatInjectorRocketCount; rocketIndex += 1)
        {
            var spreadOffset = (rocketIndex - 1) * spreadRadians;
            _host.SpawnRocket(
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

            if (_host.Rockets.Count > 0)
            {
                _host.Rockets[^1].SetDistanceToTravel(rocketTravelDistance);
            }
        }
    }

    internal bool TryResolveExperimentalEngineerRocketTrackingDirection(RocketProjectileEntity rocket, PlayerEntity owner, out float targetDirectionRadians)
    {
        targetDirectionRadians = 0f;
        var settings = _host.LastToDieRules.GetLastToDieGameplaySettings(owner);
        if (!rocket.EnableExperimentalCaveatTracking
            || rocket.ExperimentalTrackingLockTicksRemaining > 0
            || !IsExperimentalEngineerPerkOwner(owner)
            || (!settings.EnableEngineerCaveatInjector
                && !settings.EnableEngineerExperimentalOverkillAugment))
        {
            return false;
        }

        PlayerEntity? bestTarget = null;
        var bestDistanceSquared = float.MaxValue;
        foreach (var candidate in _host.EnumerateSimulatedPlayers())
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

        targetDirectionRadians = DeterministicMath.Atan2(bestTarget.Y - rocket.Y, bestTarget.X - rocket.X);
        _ = rocket.TryApplyExperimentalCaveatLockSpeedBurst(
            global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerCaveatInjectorPostLockSpeedMultiplier);
        return true;
    }
}
