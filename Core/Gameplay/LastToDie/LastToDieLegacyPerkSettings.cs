namespace OpenGarrison.Core.LastToDie;

using OpenGarrison.Core;

/// <summary>
/// Translates the original offline Last to Die stock perks into the existing
/// gameplay settings model. The hosted runtime keeps this value per player;
/// it must not be written into the world's ordinary settings record.
/// </summary>
public static class LastToDieLegacyPerkSettings
{
    public static ExperimentalGameplaySettings FromPerks(
        PlayerClass playerClass,
        IEnumerable<LastToDiePerkId> perks,
        ExperimentalGameplaySettings? baseSettings = null)
    {
        ArgumentNullException.ThrowIfNull(perks);

        // Hosted Last to Die can coexist with generic world-wide tuning and
        // accessory stats. Start from that record, then layer the original
        // class/run defaults and the player's own perks over it.
        var settings = (baseSettings ?? new ExperimentalGameplaySettings()) with
        {
            EnableSoldierFastCapture = playerClass == PlayerClass.Soldier,
            EnableDemoknightFastCapture = playerClass == PlayerClass.Demoman,
            EnableDemoknightKit = playerClass == PlayerClass.Demoman,
            EnableCapturedPointHealingAura = true,
            DemoknightSwordBaseDamage = playerClass == PlayerClass.Demoman
                ? 100
                : ExperimentalGameplaySettings.DefaultDemoknightSwordBaseDamage,
            EnableComboTracking = true,
            EnableKillStreakTracking = true,
            EnableRage = true,
            EnableEnemyHealthPackDrops = true,
            EnableEnemyDroppedWeapons = true,
            EnemyHealthPackDropChance = 1f,
        };

        foreach (var perk in perks.Distinct())
        {
            settings = perk switch
            {
                var id when id == LastToDiePerkIds.Soldier.Shotgun => settings with
                {
                    EnableSoldierShotgunSecondaryWeapon = true,
                    SoldierShotgunPelletMultiplier = 2,
                },
                var id when id == LastToDiePerkIds.Soldier.HealOnDamage => settings with
                {
                    EnableHealOnDamage = true,
                },
                var id when id == LastToDiePerkIds.Soldier.HealOnKill => settings with
                {
                    EnableHealOnKill = true,
                    HealOnKillAmount = 75,
                },
                var id when id == LastToDiePerkIds.Soldier.RateOfFireOnDamage => settings with
                {
                    EnableRateOfFireMultiplierOnDamage = true,
                },
                var id when id == LastToDiePerkIds.Soldier.InstantReload => settings with
                {
                    EnableSoldierInstantReload = true,
                },
                var id when id == LastToDiePerkIds.Soldier.PassiveHealthRegeneration => settings with
                {
                    EnablePassiveHealthRegeneration = true,
                    PassiveHealthRegenerationPerSecond = 8f,
                },
                var id when id == LastToDiePerkIds.Soldier.InvincibilityOnKill => settings with
                {
                    EnableGhostPhaseOnKill = true,
                    KillInvincibilityDurationSeconds = 1f,
                },
                var id when id == LastToDiePerkIds.Soldier.ProjectileSpeedMultiplier => settings with
                {
                    EnableProjectileSpeedMultiplier = true,
                    ProjectileSpeedMultiplierValue = 1.6f,
                },
                var id when id == LastToDiePerkIds.Soldier.AirshotDamageMultiplier => settings with
                {
                    EnableAirshotDamageMultiplier = true,
                    AirshotDamageMultiplierValue = 1.5f,
                },
                var id when id == LastToDiePerkIds.Soldier.StingerRockets => settings with
                {
                    EnableSoldierStingerRockets = true,
                },
                var id when id == LastToDiePerkIds.Soldier.RageExtensionOnKill => settings with
                {
                    EnableSoldierRageExtensionOnKill = true,
                },
                var id when id == LastToDiePerkIds.Soldier.DangerClose => settings with
                {
                    EnableSoldierDangerClose = true,
                },
                var id when id == LastToDiePerkIds.Soldier.SelfDamageHealing => settings with
                {
                    EnableSelfDamageHealing = true,
                },
                var id when id == LastToDiePerkIds.Soldier.ReloadSpeedMultiplier => settings with
                {
                    ReloadSpeedMultiplierValue = 1f / 0.6f,
                },
                var id when id == LastToDiePerkIds.Soldier.AmmoRegeneratesWhileSwappedOut => settings with
                {
                    EnableSoldierAmmoRegeneratesWhileSwappedOut = true,
                },
                var id when id == LastToDiePerkIds.Soldier.InfiniteAmmoDuringRage => settings with
                {
                    EnableSoldierInfiniteAmmoDuringRage = true,
                },
                var id when id == LastToDiePerkIds.Soldier.RageCaptureLockout => settings with
                {
                    EnableSoldierRageCaptureLockout = true,
                },
                var id when id == LastToDiePerkIds.Soldier.NapalmRockets => settings with
                {
                    EnableSoldierNapalmRockets = true,
                },
                var id when id == LastToDiePerkIds.Soldier.FinalClipRocketBurst => settings with
                {
                    EnableSoldierFinalClipRocketBurst = true,
                },
                var id when id == LastToDiePerkIds.Soldier.RageCaptureDuringRage => settings with
                {
                    EnableSoldierRageCaptureDuringRage = true,
                },
                var id when id == LastToDiePerkIds.Soldier.CivilDefenseTurret => settings with
                {
                    EnableSoldierCivilDefenseTurret = true,
                },
                var id when id == LastToDiePerkIds.Soldier.LuckyBastard => settings with
                {
                    EnableSoldierLuckyBastard = true,
                },
                var id when id == LastToDiePerkIds.Soldier.Thundergunner => settings with
                {
                    EnableSoldierThundergunner = true,
                },
                var id when id == LastToDiePerkIds.Soldier.Battleborn => settings with
                {
                    EnableSoldierBattleborn = true,
                },
                var id when id == LastToDiePerkIds.Soldier.FogOfWar => settings with
                {
                    EnableSoldierFogOfWar = true,
                },
                var id when id == LastToDiePerkIds.Engineer.GuardianMatrix => settings with
                {
                    EnableEngineerGuardianMatrix = true,
                },
                var id when id == LastToDiePerkIds.Engineer.IncendiaryEnhancements => settings with
                {
                    EnableEngineerIncendiaryEnhancements = true,
                },
                var id when id == LastToDiePerkIds.Engineer.CryonicMunitions => settings with
                {
                    EnableEngineerCryonicMunitions = true,
                },
                var id when id == LastToDiePerkIds.Engineer.AutonomousPhaseEngine => settings with
                {
                    EnableEngineerAutonomousPhaseEngine = true,
                },
                var id when id == LastToDiePerkIds.Engineer.OutputInducer => settings with
                {
                    EnableEngineerOutputInducer = true,
                },
                var id when id == LastToDiePerkIds.Engineer.EssenceExtractor => settings with
                {
                    EnableEngineerEssenceExtractor = true,
                },
                var id when id == LastToDiePerkIds.Engineer.CooperativeTargetingHarness => settings with
                {
                    EnableEngineerCooperativeTargetingHarness = true,
                },
                var id when id == LastToDiePerkIds.Engineer.RegenerativeDiode => settings with
                {
                    EnableEngineerRegenerativeDiode = true,
                },
                var id when id == LastToDiePerkIds.Engineer.OsmosisConductor => settings with
                {
                    EnableEngineerOsmosisConductor = true,
                },
                var id when id == LastToDiePerkIds.Engineer.AmperageAccelerator => settings with
                {
                    EnableEngineerAmperageAccelerator = true,
                },
                var id when id == LastToDiePerkIds.Engineer.HardwareHardener => settings with
                {
                    EnableEngineerHardwareHardener = true,
                },
                var id when id == LastToDiePerkIds.Engineer.CaveatInjector => settings with
                {
                    EnableEngineerCaveatInjector = true,
                },
                var id when id == LastToDiePerkIds.Engineer.PrecisionInstantiator => settings with
                {
                    EnableEngineerPrecisionInstantiator = true,
                },
                var id when id == LastToDiePerkIds.Engineer.BuckshotConversion => settings with
                {
                    EnableEngineerBuckshotConversion = true,
                },
                var id when id == LastToDiePerkIds.Engineer.IntegrityProjector => settings with
                {
                    EnableEngineerIntegrityProjector = true,
                },
                var id when id == LastToDiePerkIds.Engineer.MisdirectionField => settings with
                {
                    EnableEngineerMisdirectionField = true,
                },
                var id when id == LastToDiePerkIds.Engineer.ConfusionField => settings with
                {
                    EnableEngineerConfusionField = true,
                },
                var id when id == LastToDiePerkIds.Engineer.GravitonAffixer => settings with
                {
                    EnableEngineerGravitonAffixer = true,
                },
                var id when id == LastToDiePerkIds.Engineer.AuraEnergizer => settings with
                {
                    EnableEngineerAuraEnergizer = true,
                },
                var id when id == LastToDiePerkIds.Engineer.EntanglementTraverser => settings with
                {
                    EnableEngineerEntanglementTraverser = true,
                },
                var id when id == LastToDiePerkIds.Engineer.AlchemicalAnode => settings with
                {
                    EnableEngineerAlchemicalAnode = true,
                },
                var id when id == LastToDiePerkIds.Engineer.ExperimentalOverkillAugment => settings with
                {
                    EnableEngineerExperimentalOverkillAugment = true,
                },
                var id when id == LastToDiePerkIds.Engineer.EfficiencyStabilizer => settings with
                {
                    EnableEngineerEfficiencyStabilizer = true,
                },
                var id when id == LastToDiePerkIds.Engineer.MateriaRecycler => settings with
                {
                    EnableEngineerMateriaRecycler = true,
                },
                var id when id == LastToDiePerkIds.Engineer.DestinyPunctuator => settings with
                {
                    EnableEngineerDestinyPunctuator = true,
                    PassiveMovementSpeedMultiplier = settings.PassiveMovementSpeedMultiplier * 1.3f,
                    PassiveJumpHeightMultiplier = settings.PassiveJumpHeightMultiplier * 1.3f,
                },
                var id when id == LastToDiePerkIds.Engineer.FreezeRay => settings with
                {
                    EnableEngineerFreezeRay = true,
                },
                var id when id == LastToDiePerkIds.Demoknight.MeleeRange => settings with
                {
                    DemoknightSwordRangeMultiplier = 1.5f,
                },
                var id when id == LastToDiePerkIds.Demoknight.Lifesteal => settings with
                {
                    EnableHealOnDamage = true,
                    HealOnDamageFraction = 0.6f,
                },
                var id when id == LastToDiePerkIds.Demoknight.MoveSpeed => settings with
                {
                    PassiveMovementSpeedMultiplier = 1.3f,
                },
                var id when id == LastToDiePerkIds.Demoknight.KillHeal => settings with
                {
                    EnableHealOnKill = true,
                    HealOnKillAmount = 75,
                },
                var id when id == LastToDiePerkIds.Demoknight.KillInvincibility => settings with
                {
                    EnableInvincibilityOnKill = true,
                    KillInvincibilityDurationSeconds = 2f,
                },
                var id when id == LastToDiePerkIds.Demoknight.ChargeRate => settings with
                {
                    DemoknightChargeRechargeMultiplier = 1.8f,
                },
                var id when id == LastToDiePerkIds.Demoknight.ChargeResistance => settings with
                {
                    DemoknightChargeDamageTakenMultiplier = 0.2f,
                },
                var id when id == LastToDiePerkIds.Demoknight.DamageMultiplier => settings with
                {
                    DemoknightSwordDamageMultiplier = 1.4f,
                },
                var id when id == LastToDiePerkIds.Demoknight.FullHealOnKill => settings with
                {
                    EnableFullHealOnKill = true,
                },
                var id when id == LastToDiePerkIds.Demoknight.AttackSpeed => settings with
                {
                    DemoknightSwordCooldownMultiplier = 1f / 1.5f,
                },
                var id when id == LastToDiePerkIds.Demoknight.PostRageRegeneration => settings with
                {
                    EnableDemoknightPostRageRegeneration = true,
                },
                var id when id == LastToDiePerkIds.Demoknight.FullControlDuringCharge => settings with
                {
                    EnableDemoknightFullControlDuringCharge = true,
                },
                var id when id == LastToDiePerkIds.Demoknight.GhostDash => settings with
                {
                    EnableDemoknightGhostDash = true,
                },
                _ => settings,
            };
        }

        return settings;
    }
}
