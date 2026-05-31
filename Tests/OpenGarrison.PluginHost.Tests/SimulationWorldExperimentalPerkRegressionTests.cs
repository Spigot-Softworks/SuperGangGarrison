using OpenGarrison.Core;
using OpenGarrison.GameplayModding;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class SimulationWorldExperimentalPerkRegressionTests
{
    private static readonly MethodInfo ApplyPlayerDamageMethod = GetRequiredNonPublicMethod("ApplyPlayerDamage");
    private static readonly MethodInfo ApplySentryDamageMethod = GetRequiredNonPublicMethod("ApplySentryDamage");
    private static readonly MethodInfo TryHandleNetworkSecondaryAbilityMethod = GetRequiredNonPublicMethod("TryHandleNetworkSecondaryAbility");
    private static readonly MethodInfo TryHandleExperimentalRageActivationMethod = GetRequiredNonPublicMethod("TryHandleExperimentalRageActivation");
    private static readonly MethodInfo UpdateExperimentalEngineerEssenceExtractorMethod = GetRequiredNonPublicMethod("UpdateExperimentalEngineerEssenceExtractor");
    private static readonly MethodInfo UpdateExperimentalEngineerFreezeRayMethod = GetRequiredNonPublicMethod("UpdateExperimentalEngineerFreezeRay");
    private static readonly MethodInfo ApplyExperimentalSentryPlayerHitMethod = GetRequiredNonPublicMethod("ApplyExperimentalSentryPlayerHit");
    private static readonly MethodInfo SpawnRocketMethod = GetRequiredNonPublicMethod("SpawnRocket");
    private static readonly MethodInfo SpawnShotMethod = GetRequiredNonPublicMethod("SpawnShot");
    private static readonly MethodInfo TryResolveExperimentalEngineerRocketTrackingDirectionMethod = GetRequiredNonPublicMethod("TryResolveExperimentalEngineerRocketTrackingDirection");
    private static readonly MethodInfo GetExperimentalSentryReloadTicksMethod = GetRequiredNonPublicMethod("GetExperimentalSentryReloadTicks");
    private static readonly MethodInfo GetExperimentalSentryIdleResetTicksMethod = GetRequiredNonPublicMethod("GetExperimentalSentryIdleResetTicks");
    private static readonly MethodInfo GetExperimentalSentryTargetRangeMethod = GetRequiredNonPublicMethod("GetExperimentalSentryTargetRange");
    private static readonly MethodInfo HasSentryLineOfSightMethod = GetRequiredNonPublicMethod("HasSentryLineOfSight");
    private static readonly MethodInfo FirePrimaryWeaponMethod = GetRequiredNonPublicMethod("FirePrimaryWeapon");
    private static readonly MethodInfo TryHandleExperimentalEngineerAlternateWeaponInteractionMethod = GetRequiredNonPublicMethod("TryHandleExperimentalEngineerAlternateWeaponInteraction");
    private static readonly MethodInfo PlayerCanOccupyMethod = GetRequiredNonPublicPlayerMethod("CanOccupy");
    private static readonly MethodInfo GetExperimentalMovementSpeedMultiplierMethod = GetRequiredNonPublicPlayerMethod("GetExperimentalMovementSpeedMultiplier");
    private static readonly FieldInfo AimDirectionDegreesBackingField = typeof(PlayerEntity).GetField("<AimDirectionDegrees>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Could not find PlayerEntity aim backing field.");

    [Fact]
    public void SpeedLoaderReloadMultiplierReducesRocketRefillTicks()
    {
        const float reloadMultiplier = 1f / 0.6f;
        var player = new PlayerEntity(1, CharacterClassCatalog.Soldier, "Test");
        player.Spawn(PlayerTeam.Red, 0f, 0f);
        player.SetExperimentalReloadSpeedMultiplier(reloadMultiplier);

        player.ForceSetAmmo(player.PrimaryWeapon.MaxAmmo);
        Assert.True(player.TryFirePrimaryWeapon());

        var expectedReloadTicks = Math.Max(1, (int)MathF.Round(player.PrimaryWeapon.AmmoReloadTicks / reloadMultiplier));
        Assert.Equal(expectedReloadTicks, player.ReloadTicksUntilNextShell);
    }

    [Fact]
    public void ShrapnelJunkieConvertsSelfDamageIntoHealing()
    {
        var world = CreateSoldierWorld(new ExperimentalGameplaySettings(EnableSelfDamageHealing: true));
        var player = world.LocalPlayer;
        player.ForceSetHealth(Math.Max(1, player.MaxHealth / 2));
        var healthBefore = player.Health;

        var applyContinuousDamageMethod = typeof(SimulationWorld).GetMethod(
            "ApplyPlayerContinuousDamage",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(applyContinuousDamageMethod);

        var died = (bool)applyContinuousDamageMethod!.Invoke(
            world,
            [player, 12f, player, PlayerEntity.SpyDamageRevealAlpha, DamageEventFlags.None, true])!;

        Assert.False(died);
        Assert.True(player.Health > healthBefore);
    }

    [Fact]
    public void UntouchableKillRewardAppliesGhostPhaseInsteadOfUber()
    {
        var world = CreateSoldierWorld(new ExperimentalGameplaySettings(
            EnableGhostPhaseOnKill: true,
            KillInvincibilityDurationSeconds: 1f));

        Assert.True(world.TryPrepareNetworkPlayerJoin(2));
        Assert.True(world.TrySetNetworkPlayerTeam(2, PlayerTeam.Blue));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(2, PlayerClass.Scout));
        Assert.True(world.TryGetNetworkPlayer(2, out var victim));
        victim.ForceSetHealth(1);

        var killMethod = typeof(SimulationWorld).GetMethod("KillPlayer", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(killMethod);
        _ = killMethod!.Invoke(
            world,
            [
                victim,
                true,
                world.LocalPlayer,
                "RocketKL",
                DeadBodyAnimationKind.Default,
                null,
                null,
                null,
                true,
                true,
                false,
                true,
            ]);

        Assert.True(world.LocalPlayer.IsExperimentalGhostDashing);
        Assert.False(world.LocalPlayer.IsUbered);
    }

    [Fact]
    public void StockEngineerRightClickBuildsSentry()
    {
        var world = CreateJoinedEngineerWorld(new ExperimentalGameplaySettings());

        Assert.True(world.TryMoveLocalPlayerToControlPointSpawn());
        world.SetLocalInput(new PlayerInputSnapshot(
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            true,
            world.LocalPlayer.X + 96f,
            world.LocalPlayer.Y,
            false));

        world.AdvanceOneTick();

        Assert.Single(world.Sentries);
    }

    [Fact]
    public void StockEngineerRightClickBuildsSentryWhenSpecialAbilitiesAreDisabled()
    {
        var world = CreateJoinedEngineerWorld(new ExperimentalGameplaySettings(EnableSecondaryAbilities: false));

        Assert.True(world.TryMoveLocalPlayerToControlPointSpawn());
        PressFireSecondary(world);

        Assert.Single(world.Sentries);
        AssertCoreSecondaryAbilityEvent(world, "ability.engineer-pda", BuiltInGameplayBehaviorIds.EngineerPda);
    }

    [Fact]
    public void StockPyroRightClickAirblastsWhenSpecialAbilitiesAreDisabled()
    {
        var world = CreateJoinedPyroWorld(new ExperimentalGameplaySettings(EnableSecondaryAbilities: false));
        AdvanceTicks(world, 1);

        PressFireSecondary(world);

        Assert.True(world.LocalPlayer.PyroAirblastCooldownTicks > 0);
        AssertCoreSecondaryAbilityEvent(world, "ability.pyro-airblast", BuiltInGameplayBehaviorIds.PyroAirblast);
    }

    [Fact]
    public void StockDemomanRightClickDetonatesInsteadOfSwappingWhenMouseSecondaryIsSwapBound()
    {
        var world = CreateJoinedDemomanWorld(new ExperimentalGameplaySettings());
        AdvanceTicks(world, 1);
        Assert.True(world.LocalPlayer.HasExperimentalOffhandWeapon);
        Assert.False(world.LocalPlayer.IsExperimentalOffhandEquipped);
        FirePrimaryOnce(world);
        Assert.NotEmpty(world.Mines);

        ReleaseAllInput(world);
        PressFireSecondaryAndSwapWeapon(world);

        Assert.Empty(world.Mines);
        Assert.False(world.LocalPlayer.IsExperimentalOffhandEquipped);
        Assert.Equal(GameplayEquipmentSlot.Primary, world.LocalPlayer.GameplayLoadoutState.EquippedSlot);
        AssertCoreSecondaryAbilityEvent(world, "ability.demoman-detonate", BuiltInGameplayBehaviorIds.DemomanDetonate);
    }

    [Fact]
    public void StockDemomanRightClickDetonatesWhenSpecialAbilitiesAreDisabled()
    {
        var world = CreateJoinedDemomanWorld(new ExperimentalGameplaySettings(EnableSecondaryAbilities: false));
        AdvanceTicks(world, 1);
        Assert.False(world.LocalPlayer.HasExperimentalOffhandWeapon);
        FirePrimaryOnce(world);
        Assert.NotEmpty(world.Mines);

        ReleaseAllInput(world);
        PressFireSecondary(world);

        Assert.Empty(world.Mines);
        AssertCoreSecondaryAbilityEvent(world, "ability.demoman-detonate", BuiltInGameplayBehaviorIds.DemomanDetonate);
    }

    [Fact]
    public void StockHeavyRightClickUsesSandvichWhenSpecialAbilitiesAreDisabled()
    {
        var world = CreateJoinedHeavyWorld(new ExperimentalGameplaySettings(EnableSecondaryAbilities: false));
        AdvanceTicks(world, 1);
        world.LocalPlayer.ForceSetHealth(world.LocalPlayer.MaxHealth - 80);

        PressFireSecondary(world);

        Assert.True(world.LocalPlayer.IsHeavyEating);
        AssertCoreSecondaryAbilityEvent(world, "ability.heavy-sandvich", BuiltInGameplayBehaviorIds.HeavySandvich);
    }

    [Fact]
    public void StockMedicRightClickFiresNeedlesWhenSpecialAbilitiesAreDisabled()
    {
        var world = CreateJoinedMedicWorld(new ExperimentalGameplaySettings(EnableSecondaryAbilities: false));
        AdvanceTicks(world, 1);

        PressFireSecondary(world);

        Assert.NotEmpty(world.Needles);
        AssertCoreSecondaryAbilityEvent(world, "weapon.medigun.crit", BuiltInGameplayBehaviorIds.MedicNeedlegun);
    }

    [Fact]
    public void StockSniperRightClickScopesInsteadOfSwappingWhenMouseSecondaryIsSwapBound()
    {
        var world = CreateJoinedSniperWorld(new ExperimentalGameplaySettings(EnableSecondaryAbilities: false));
        AdvanceTicks(world, 1);

        PressFireSecondaryAndSwapWeapon(world);

        Assert.True(world.LocalPlayer.IsSniperScoped);
        Assert.Equal(GameplayEquipmentSlot.Primary, world.LocalPlayer.GameplayLoadoutState.EquippedSlot);
        AssertCoreSecondaryAbilityEvent(world, "ability.sniper-scope", BuiltInGameplayBehaviorIds.SniperScope);
    }

    [Fact]
    public void StockSpyRightClickCloaksInsteadOfSwappingWhenMouseSecondaryIsSwapBound()
    {
        var world = CreateJoinedSpyWorld(new ExperimentalGameplaySettings(EnableSecondaryAbilities: false));
        AdvanceTicks(world, 1);

        PressFireSecondaryAndSwapWeapon(world);

        Assert.True(world.LocalPlayer.IsSpyCloaked);
        Assert.Equal(GameplayEquipmentSlot.Primary, world.LocalPlayer.GameplayLoadoutState.EquippedSlot);
        AssertCoreSecondaryAbilityEvent(world, "ability.spy-cloak", BuiltInGameplayBehaviorIds.SpyCloak);
    }

    [Fact]
    public void OutputInducerEngineerPdaCanPlaceSecondSentry()
    {
        var world = CreateJoinedEngineerWorld(new ExperimentalGameplaySettings(EnableEngineerOutputInducer: true));

        Assert.True(world.TryMoveLocalPlayerToControlPointSpawn());
        InvokeEngineerPda(world);
        Assert.Single(world.Sentries);

        world.LocalPlayer.AddMetal(world.LocalPlayer.MaxMetal);
        world.TeleportLocalPlayer(world.LocalPlayer.X + 96f, world.LocalPlayer.Y);

        InvokeEngineerPda(world);

        Assert.Equal(2, world.Sentries.Count);
    }

    [Fact]
    public void GuardianMatrixAppliesShieldAndAbsorbsIncomingDamage()
    {
        var world = CreateJoinedEngineerWorld(new ExperimentalGameplaySettings(EnableEngineerGuardianMatrix: true));
        _ = BuildAndCompleteLocalSentry(world);

        AdvanceTicks(world, 1);

        Assert.Equal(ExperimentalGameplaySettings.DefaultEngineerGuardianMatrixShieldHealth, world.LocalPlayer.ExperimentalShieldHealth);
        var healthBefore = world.LocalPlayer.Health;

        _ = InvokeApplyPlayerDamage(world, world.LocalPlayer, 50, world.EnemyPlayer);

        Assert.Equal(healthBefore, world.LocalPlayer.Health);
        Assert.Equal(ExperimentalGameplaySettings.DefaultEngineerGuardianMatrixShieldHealth - 50f, world.LocalPlayer.ExperimentalShieldHealth);
    }

    [Fact]
    public void GuardianMatrixAndHardwareHardenerIncreaseSentryMaxHealth()
    {
        var world = CreateJoinedEngineerWorld(new ExperimentalGameplaySettings(
            EnableEngineerGuardianMatrix: true,
            EnableEngineerHardwareHardener: true));

        var sentry = BuildLocalSentry(world);

        Assert.Equal(
            SentryEntity.DefaultMaxHealth
            + ExperimentalGameplaySettings.DefaultEngineerGuardianMatrixSentryBonusHealth
            + ExperimentalGameplaySettings.DefaultEngineerHardwareHardenerSentryBonusHealth,
            sentry.MaxHealth);
    }

    [Fact]
    public void HardwareHardenerResistsIncomingSentryDamageAboveHalfHealthOnly()
    {
        var world = CreateJoinedEngineerWorld(new ExperimentalGameplaySettings(EnableEngineerHardwareHardener: true));
        var sentry = BuildAndCompleteLocalSentry(world);

        var healthBeforeProtectedHit = sentry.Health;
        _ = InvokeApplySentryDamage(world, sentry, 10, world.EnemyPlayer);
        Assert.Equal(7, healthBeforeProtectedHit - sentry.Health);

        Assert.False(sentry.ApplyDamage(sentry.Health - ((sentry.MaxHealth / 2) - 1)));
        var healthBeforeUnprotectedHit = sentry.Health;
        _ = InvokeApplySentryDamage(world, sentry, 10, world.EnemyPlayer);
        Assert.Equal(10, healthBeforeUnprotectedHit - sentry.Health);
    }

    [Fact]
    public void RegenerativeDiodeHealsEngineerAndBuiltSentryOverTime()
    {
        var world = CreateJoinedEngineerWorld(new ExperimentalGameplaySettings(EnableEngineerRegenerativeDiode: true));
        var sentry = BuildAndCompleteLocalSentry(world);

        world.LocalPlayer.ForceSetHealth(world.LocalPlayer.MaxHealth - 20);
        Assert.False(sentry.ApplyDamage(20));
        var playerHealthBefore = world.LocalPlayer.Health;
        var sentryHealthBefore = sentry.Health;

        AdvanceTicks(world, world.Config.TicksPerSecond);

        Assert.Equal(playerHealthBefore + 5, world.LocalPlayer.Health);
        Assert.Equal(sentryHealthBefore + 5, sentry.Health);
    }

    [Fact]
    public void OsmosisConductorPlayerDamageHealsOwnedSentries()
    {
        var world = CreateJoinedEngineerWorld(new ExperimentalGameplaySettings(EnableEngineerOsmosisConductor: true));
        var sentry = BuildAndCompleteLocalSentry(world);
        var enemy = CreateBlueNetworkScout(world, 2);

        Assert.False(sentry.ApplyDamage(15));
        enemy.TeleportTo(world.LocalPlayer.X + 96f, world.LocalPlayer.Y);
        var sentryHealthBefore = sentry.Health;

        _ = InvokeApplyPlayerDamage(world, enemy, 10, world.LocalPlayer);

        Assert.Equal(Math.Min(sentry.MaxHealth, sentryHealthBefore + 10), sentry.Health);
    }

    [Fact]
    public void OsmosisConductorDoesNotLetSentryDamageHealOwnedSentries()
    {
        var world = CreateJoinedEngineerWorld(new ExperimentalGameplaySettings(EnableEngineerOsmosisConductor: true));
        var sentry = BuildAndCompleteLocalSentry(world);
        var enemy = CreateBlueNetworkScout(world, 2);
        enemy.ForceSetHealth(999);

        Assert.False(sentry.ApplyDamage(15));
        var sentryHealthBefore = sentry.Health;

        InvokeApplyExperimentalSentryPlayerHit(world, sentry, world.LocalPlayer, enemy, SentryEntity.HitDamage);

        Assert.Equal(sentryHealthBefore, sentry.Health);
    }

    [Fact]
    public void AmperageAcceleratorReducesSentryReloadAfterConsecutiveShots()
    {
        var world = CreateJoinedEngineerWorld(new ExperimentalGameplaySettings(EnableEngineerAmperageAccelerator: true));
        var sentry = BuildLocalSentry(world);
        var idleResetTicks = InvokeGetExperimentalSentryIdleResetTicks(world);

        var initialReloadTicks = InvokeGetExperimentalSentryReloadTicks(world, world.LocalPlayer, sentry);
        for (var shotIndex = 0; shotIndex < ExperimentalGameplaySettings.DefaultEngineerAmperageAcceleratorShotsToMax; shotIndex += 1)
        {
            sentry.FireAt(sentry.X + 16f, sentry.Y, idleResetTicks: idleResetTicks);
        }

        var acceleratedReloadTicks = InvokeGetExperimentalSentryReloadTicks(world, world.LocalPlayer, sentry);

        Assert.Equal(SentryEntity.ReloadTicks, initialReloadTicks);
        Assert.True(acceleratedReloadTicks < initialReloadTicks);
        Assert.Equal(2, acceleratedReloadTicks);
    }

    [Fact]
    public void CooperativeTargetingHarnessPrioritizesRecentlyDamagedEnemyOverCloserTarget()
    {
        var world = CreateJoinedEngineerWorld(new ExperimentalGameplaySettings(EnableEngineerCooperativeTargetingHarness: true));
        world.EnemyPlayer.ForceSetHealth(0);
        var sentry = BuildAndCompleteLocalSentry(world);
        var closerEnemy = CreateBlueNetworkScout(world, 2);
        var markedEnemy = CreateBlueNetworkScout(world, 3);

        var closerPosition = FindVisibleSentryTargetPosition(world, sentry, closerEnemy, preferredDistance: 96f, minimumDistance: 48f);
        var markedPosition = FindVisibleSentryTargetPosition(world, sentry, markedEnemy, preferredDistance: 220f, minimumDistance: closerPosition.Distance + 64f);
        closerEnemy.TeleportTo(closerPosition.X, closerPosition.Y);
        markedEnemy.TeleportTo(markedPosition.X, markedPosition.Y);
        _ = InvokeApplyPlayerDamage(world, markedEnemy, 1, world.LocalPlayer);

        AdvanceTicks(world, 1);

        Assert.Equal(markedEnemy.Id, sentry.CurrentTargetPlayerId);
    }

    [Fact]
    public void MisdirectionFieldCanEvadeIncomingDamageForEngineer()
    {
        var world = CreateJoinedEngineerWorld(new ExperimentalGameplaySettings(EnableEngineerMisdirectionField: true));
        _ = BuildAndCompleteLocalSentry(world);

        AdvanceTicks(world, 1);

        Assert.True(world.IsPlayerInsideExperimentalEngineerMisdirectionFieldForVisuals(world.LocalPlayer));
        _ = world.DrainPendingDamageEvents();
        var evadedHitCount = 0;
        for (var attempt = 0; attempt < 25; attempt += 1)
        {
            world.LocalPlayer.ForceSetHealth(world.LocalPlayer.MaxHealth);
            _ = InvokeApplyPlayerDamage(world, world.LocalPlayer, 1, world.EnemyPlayer);
            var events = world.DrainPendingDamageEvents();
            if (events.Any(static damageEvent => damageEvent.Flags.HasFlag(DamageEventFlags.Evaded)))
            {
                evadedHitCount += 1;
            }
        }

        Assert.True(evadedHitCount > 0);
    }

    [Fact]
    public void IncendiaryEnhancementsIgniteTargets()
    {
        var world = CreateJoinedEngineerWorld(new ExperimentalGameplaySettings(EnableEngineerIncendiaryEnhancements: true));
        world.EnemyPlayer.ForceSetHealth(0);
        var sentry = BuildAndCompleteLocalSentry(world);
        var enemy = CreateBlueNetworkScout(world, 2);
        var targetPosition = FindVisibleSentryTargetPosition(world, sentry, enemy, preferredDistance: 92f, minimumDistance: 56f);
        enemy.TeleportTo(targetPosition.X, targetPosition.Y);

        AdvanceTicks(world, 40);

        Assert.True(enemy.BurnDurationSourceTicks > 0f);
    }

    [Fact]
    public void CryonicMunitionsFreezesTargetsAfterSustainedFire()
    {
        var world = CreateJoinedEngineerWorld(new ExperimentalGameplaySettings(EnableEngineerCryonicMunitions: true));
        var sentry = BuildAndCompleteLocalSentry(world);
        var enemy = CreateBlueNetworkScout(world, 2);
        enemy.ForceSetHealth(999);

        InvokeApplyExperimentalSentryPlayerHit(world, sentry, world.LocalPlayer, enemy, SentryEntity.HitDamage);
        Assert.True(enemy.IsExperimentalCryoSlowed);
        Assert.False(enemy.IsExperimentalCryoFrozen);

        InvokeApplyExperimentalSentryPlayerHit(world, sentry, world.LocalPlayer, enemy, SentryEntity.HitDamage);
        Assert.True(enemy.IsExperimentalCryoSlowed);
        Assert.False(enemy.IsExperimentalCryoFrozen);

        InvokeApplyExperimentalSentryPlayerHit(world, sentry, world.LocalPlayer, enemy, SentryEntity.HitDamage);
        Assert.True(enemy.IsExperimentalCryoFrozen);
    }

    [Fact]
    public void AutonomousPhaseEngineMakesBuiltSentryFollowEngineer()
    {
        var world = CreateJoinedEngineerWorld(new ExperimentalGameplaySettings(EnableEngineerAutonomousPhaseEngine: true));
        var sentry = BuildAndCompleteLocalSentry(world);
        var startX = sentry.X;
        var startY = sentry.Y;

        world.TeleportLocalPlayer(world.LocalPlayer.X + 160f, world.LocalPlayer.Y - 48f);
        AdvanceTicks(world, 30);

        Assert.True(world.IsExperimentalEngineerFloatingSentry(sentry));
        Assert.True(MathF.Abs(sentry.X - startX) > 24f || MathF.Abs(sentry.Y - startY) > 12f);
    }

    [Fact]
    public void EngineerCanActivateSharedRage()
    {
        var world = CreateJoinedEngineerWorld(new ExperimentalGameplaySettings(EnableRage: true));
        world.LocalPlayer.AddRageCharge(ExperimentalGameplaySettings.RageMaxCharge, ExperimentalGameplaySettings.RageMaxCharge);

        Assert.True(InvokeTryHandleExperimentalRageActivation(world, world.LocalPlayer));
        Assert.True(world.LocalPlayer.IsRaging);
    }

    [Fact]
    public void JumpPadLaunchesEngineerOnlyWhenJumpIsPressed()
    {
        var world = CreateJoinedEngineerWorld(new ExperimentalGameplaySettings());
        Assert.True(world.TryMoveLocalPlayerToControlPointSpawn());
        Assert.True(world.TryBuildLocalJumpPad());
        var pad = Assert.Single(world.JumpPads);
        AdvanceUntilJumpPadLanded(world, pad);

        AdvanceTicks(world, 5);

        Assert.True(world.LocalPlayer.VerticalSpeed >= -0.01f);
        Assert.True(world.LocalPlayer.IsGrounded);

        PressJump(world);

        Assert.True(world.LocalPlayer.VerticalSpeed < -world.LocalPlayer.JumpSpeed);
        Assert.False(world.LocalPlayer.IsGrounded);
    }

    [Fact]
    public void GravitonAffixerPullsNearbyEnemiesAndSlowsThemOnContact()
    {
        var world = CreateJoinedEngineerWorld(new ExperimentalGameplaySettings(EnableEngineerGravitonAffixer: true));
        Assert.True(world.TryMoveLocalPlayerToControlPointSpawn());
        Assert.True(world.TryBuildLocalJumpPad());
        var pad = Assert.Single(world.JumpPads);
        AdvanceUntilJumpPadLanded(world, pad);
        var enemy = CreateBlueNetworkScout(world, 2);
        enemy.TeleportTo(pad.X + 96f, pad.Y - 8f);
        enemy.ResolveBlockingOverlap(world.Level, enemy.Team);
        AdvanceTicks(world, 1);

        Assert.True(enemy.HorizontalSpeed < 0f);
        Assert.True(enemy.VerticalSpeed >= -0.01f);

        enemy.TeleportTo(pad.X, pad.Y);
        enemy.ResolveBlockingOverlap(world.Level, enemy.Team);
        AdvanceTicks(world, 1);

        Assert.True(InvokeGetExperimentalMovementSpeedMultiplier(enemy) < 1f);
    }

    [Fact]
    public void AuraEnergizerAddsPadAuraBurstAndReducedLaunchHeight()
    {
        var baselineWorld = CreateJoinedEngineerWorld(new ExperimentalGameplaySettings());
        Assert.True(baselineWorld.TryMoveLocalPlayerToControlPointSpawn());
        Assert.True(baselineWorld.TryBuildLocalJumpPad());
        var baselinePad = Assert.Single(baselineWorld.JumpPads);
        AdvanceUntilJumpPadLanded(baselineWorld, baselinePad);
        PressJump(baselineWorld);

        var baselineLaunchSpeed = baselineWorld.LocalPlayer.VerticalSpeed;

        var auraWorld = CreateJoinedEngineerWorld(new ExperimentalGameplaySettings(EnableEngineerAuraEnergizer: true));
        Assert.True(auraWorld.TryMoveLocalPlayerToControlPointSpawn());
        Assert.True(auraWorld.TryBuildLocalJumpPad());
        var auraPad = Assert.Single(auraWorld.JumpPads);
        AdvanceUntilJumpPadLanded(auraWorld, auraPad);
        auraWorld.TeleportLocalPlayer(auraPad.X + 24f, auraPad.Y);
        AdvanceTicks(auraWorld, 1);

        Assert.True(InvokeGetExperimentalMovementSpeedMultiplier(auraWorld.LocalPlayer) >= ExperimentalGameplaySettings.DefaultEngineerAuraEnergizerAuraMovementSpeedMultiplier);

        auraWorld.TeleportLocalPlayer(auraPad.X, auraPad.Y);
        PressJump(auraWorld);

        Assert.True(InvokeGetExperimentalMovementSpeedMultiplier(auraWorld.LocalPlayer) > ExperimentalGameplaySettings.DefaultEngineerAuraEnergizerAuraMovementSpeedMultiplier);
        Assert.True(MathF.Abs(auraWorld.LocalPlayer.VerticalSpeed) < MathF.Abs(baselineLaunchSpeed));
    }

    [Fact]
    public void EntanglementTraverserTeleportsEngineerToOwnedSentry()
    {
        var world = CreatePrototypeEngineerWorld(new ExperimentalGameplaySettings(EnableEngineerEntanglementTraverser: true));
        Assert.True(world.TryBuildLocalSentry());
        var sentry = Assert.Single(world.Sentries);
        for (var tick = 0; tick < 2000 && !sentry.IsBuilt; tick += 1)
        {
            world.AdvanceOneTick();
        }

        Assert.True(sentry.IsBuilt);
        world.LocalPlayer.AddMetal(50f);
        world.TeleportLocalPlayer(sentry.X + 112f, sentry.Y);
        Assert.True(world.TryBuildLocalJumpPad());
        var pad = Assert.Single(world.JumpPads);
        AdvanceUntilJumpPadLanded(world, pad);
        PressJump(world);

        for (var tick = 0; tick < 180 && MathF.Abs(world.LocalPlayer.X - sentry.X) > 24f; tick += 1)
        {
            world.AdvanceOneTick();
        }

        Assert.True(MathF.Abs(world.LocalPlayer.X - sentry.X) <= 24f);
        Assert.True(MathF.Abs(world.LocalPlayer.Y - (sentry.Y - MathF.Max(world.LocalPlayer.Height, SentryEntity.Height))) <= 24f);
        Assert.True(world.LocalPlayer.VerticalSpeed >= -0.01f);
        Assert.Equal(pad.Id, world.JumpPads[0].Id);
    }

    [Fact]
    public void AlchemicalAnodeLetsSentryRepairFromItsOwnDamage()
    {
        var world = CreateJoinedEngineerWorld(new ExperimentalGameplaySettings(EnableEngineerAlchemicalAnode: true));
        var sentry = BuildAndCompleteLocalSentry(world);
        var enemy = CreateBlueNetworkScout(world, 2);
        enemy.ForceSetHealth(999);

        Assert.False(sentry.ApplyDamage(25));
        var healthBefore = sentry.Health;

        InvokeApplyExperimentalSentryPlayerHit(world, sentry, world.LocalPlayer, enemy, SentryEntity.HitDamage);

        Assert.True(sentry.Health > healthBefore);
    }

    [Fact]
    public void EfficiencyStabilizerScalesMovementSpeedWithCurrentMetal()
    {
        var world = CreateJoinedEngineerWorld(new ExperimentalGameplaySettings(EnableEngineerEfficiencyStabilizer: true));
        AdvanceTicks(world, 1);

        Assert.Equal(2f, InvokeGetExperimentalMovementSpeedMultiplier(world.LocalPlayer), 3);
        Assert.True(world.LocalPlayer.SpendMetal(50f));
        AdvanceTicks(world, 1);

        var movementMultiplier = InvokeGetExperimentalMovementSpeedMultiplier(world.LocalPlayer);
        Assert.InRange(movementMultiplier, 1.5f, 1.51f);
    }

    [Fact]
    public void MateriaRecyclerRaisesMetalCapAndRefillsMetalOnDamage()
    {
        var world = CreateJoinedEngineerWorld(new ExperimentalGameplaySettings(EnableEngineerMateriaRecycler: true));
        var enemy = CreateBlueNetworkScout(world, 2);
        AdvanceTicks(world, 1);

        Assert.Equal(200f, world.LocalPlayer.MaxMetal);
        Assert.True(world.LocalPlayer.SpendMetal(200f));
        Assert.Equal(0f, world.LocalPlayer.Metal);

        _ = InvokeApplyPlayerDamage(world, enemy, 12, world.LocalPlayer);

        Assert.True(world.LocalPlayer.Metal >= 12f);
    }

    [Fact]
    public void MateriaRecyclerStillAllowsBuildingSentryAtOneHundredMetal()
    {
        var world = CreateJoinedEngineerWorld(new ExperimentalGameplaySettings(EnableEngineerMateriaRecycler: true));
        Assert.True(world.TryMoveLocalPlayerToControlPointSpawn());
        AdvanceTicks(world, 1);

        Assert.Equal(200f, world.LocalPlayer.MaxMetal);
        Assert.True(world.LocalPlayer.SpendMetal(100f));
        Assert.Equal(100f, world.LocalPlayer.Metal);
        Assert.True(world.LocalPlayer.CanAffordSentry());
        Assert.True(world.TryBuildLocalSentry());
    }

    [Fact]
    public void DestinyPunctuatorDisablesSentryBuildAndUpgradesEngineerShotgun()
    {
        var world = CreateJoinedEngineerWorld(new ExperimentalGameplaySettings(EnableEngineerDestinyPunctuator: true));
        AdvanceTicks(world, 1);

        Assert.Equal(CharacterClassCatalog.Engineer.MaxHealth + ExperimentalGameplaySettings.DefaultEngineerDestinyPunctuatorBonusMaxHealth, world.LocalPlayer.MaxHealth);
        Assert.Equal(ExperimentalGameplaySettings.DefaultEngineerDestinyPunctuatorShotgunClipSize, world.LocalPlayer.MaxShells);
        Assert.False(world.LocalPlayer.CanAffordSentry() && world.TryBuildLocalSentry());
        Assert.Empty(world.Sentries);
    }

    [Fact]
    public void DestinyPunctuatorCyclesEngineerBeamModesWithWeaponInteraction()
    {
        var world = CreateJoinedEngineerWorld(new ExperimentalGameplaySettings(
            EnableEngineerDestinyPunctuator: true,
            EnableEngineerEssenceExtractor: true,
            EnableEngineerFreezeRay: true));
        AdvanceTicks(world, 1);

        Assert.True(InvokeTryHandleExperimentalEngineerAlternateWeaponInteraction(world, world.LocalPlayer));
        Assert.True(world.LocalPlayer.IsExperimentalOffhandEquipped);
        Assert.Equal(ExperimentalEngineerAlternateWeaponMode.EssenceExtractor, world.LocalPlayer.ExperimentalEngineerAlternateWeaponMode);

        Assert.True(InvokeTryHandleExperimentalEngineerAlternateWeaponInteraction(world, world.LocalPlayer));
        Assert.Equal(ExperimentalEngineerAlternateWeaponMode.FreezeRay, world.LocalPlayer.ExperimentalEngineerAlternateWeaponMode);

        Assert.True(InvokeTryHandleExperimentalEngineerAlternateWeaponInteraction(world, world.LocalPlayer));
        Assert.False(world.LocalPlayer.IsExperimentalOffhandEquipped);
        Assert.Equal(ExperimentalEngineerAlternateWeaponMode.None, world.LocalPlayer.ExperimentalEngineerAlternateWeaponMode);
    }

    [Fact]
    public void DestinyPunctuatorSecondaryBlastConsumesTwoShellsAndSpawnsPellets()
    {
        var world = CreateJoinedEngineerWorld(new ExperimentalGameplaySettings(
            EnableEngineerDestinyPunctuator: true,
            EnableEngineerEssenceExtractor: true));
        Assert.True(world.TryMoveLocalPlayerToControlPointSpawn());
        AdvanceTicks(world, 1);
        Assert.True(InvokeTryHandleExperimentalEngineerAlternateWeaponInteraction(world, world.LocalPlayer));
        Assert.True(world.LocalPlayer.IsExperimentalOffhandEquipped);
        world.LocalPlayer.ForceSetAmmo(ExperimentalGameplaySettings.DefaultEngineerDestinyPunctuatorShotgunClipSize);

        TryHandleNetworkSecondaryAbilityMethod.Invoke(
            world,
            [
                world.LocalPlayer,
                new PlayerInputSnapshot(
                    Left: false,
                    Right: false,
                    Up: false,
                    Down: false,
                    BuildSentry: false,
                    DestroySentry: false,
                    Taunt: false,
                    FirePrimary: false,
                    FireSecondary: true,
                    AimWorldX: world.LocalPlayer.X + 128f,
                    AimWorldY: world.LocalPlayer.Y,
                    DebugKill: false),
                default(PlayerInputSnapshot),
                GameplayAbilityInputPhase.Pressed,
                world.LocalPlayer.X,
                world.LocalPlayer.Y,
            ]);

        Assert.Equal(0, world.LocalPlayer.CurrentShells);
        Assert.True(world.Shots.Count >= CharacterClassCatalog.Shotgun.ProjectilesPerShot);
        Assert.False(world.LocalPlayer.IsExperimentalOffhandEquipped);
    }

    [Fact]
    public void DestinyPunctuatorSecondaryBlastAlsoWorksWithShotgunPresented()
    {
        var world = CreateJoinedEngineerWorld(new ExperimentalGameplaySettings(
            EnableEngineerDestinyPunctuator: true,
            EnableEngineerEssenceExtractor: true));
        Assert.True(world.TryMoveLocalPlayerToControlPointSpawn());
        AdvanceTicks(world, 1);

        Assert.True(InvokeTryHandleExperimentalEngineerAlternateWeaponInteraction(world, world.LocalPlayer));
        Assert.True(InvokeTryHandleExperimentalEngineerAlternateWeaponInteraction(world, world.LocalPlayer));
        Assert.False(world.LocalPlayer.IsExperimentalOffhandEquipped);
        Assert.Equal(ExperimentalEngineerAlternateWeaponMode.None, world.LocalPlayer.ExperimentalEngineerAlternateWeaponMode);

        world.LocalPlayer.ForceSetAmmo(ExperimentalGameplaySettings.DefaultEngineerDestinyPunctuatorShotgunClipSize);
        TryHandleNetworkSecondaryAbilityMethod.Invoke(
            world,
            [
                world.LocalPlayer,
                new PlayerInputSnapshot(
                    Left: false,
                    Right: false,
                    Up: false,
                    Down: false,
                    BuildSentry: false,
                    DestroySentry: false,
                    Taunt: false,
                    FirePrimary: false,
                    FireSecondary: true,
                    AimWorldX: world.LocalPlayer.X + 128f,
                    AimWorldY: world.LocalPlayer.Y,
                    DebugKill: false),
                default(PlayerInputSnapshot),
                GameplayAbilityInputPhase.Pressed,
                world.LocalPlayer.X,
                world.LocalPlayer.Y,
            ]);

        Assert.Equal(0, world.LocalPlayer.CurrentShells);
        Assert.True(world.Shots.Count >= CharacterClassCatalog.Shotgun.ProjectilesPerShot);
    }

    [Fact]
    public void EngineerBeamModesCycleWithoutDestinyAndClearExistingBeamTargets()
    {
        var world = CreateJoinedEngineerWorld(new ExperimentalGameplaySettings(
            EnableEngineerEssenceExtractor: true,
            EnableEngineerFreezeRay: true));
        Assert.True(world.TryMoveLocalPlayerToControlPointSpawn());
        var enemy = CreateBlueNetworkScout(world, 2);
        enemy.TeleportTo(world.LocalPlayer.X + 96f, world.LocalPlayer.Y);
        AdvanceTicks(world, 1);

        Assert.True(InvokeTryHandleExperimentalEngineerAlternateWeaponInteraction(world, world.LocalPlayer));
        Assert.True(world.LocalPlayer.IsExperimentalOffhandEquipped);
        Assert.Equal(ExperimentalEngineerAlternateWeaponMode.EssenceExtractor, world.LocalPlayer.ExperimentalEngineerAlternateWeaponMode);

        InvokeUpdateExperimentalEngineerEssenceExtractor(world, world.LocalPlayer, enemy.X, enemy.Y);
        Assert.True(world.LocalPlayer.IsMedicHealing);
        Assert.Equal(enemy.Id, world.LocalPlayer.MedicHealTargetId);

        Assert.True(InvokeTryHandleExperimentalEngineerAlternateWeaponInteraction(world, world.LocalPlayer));
        Assert.Equal(ExperimentalEngineerAlternateWeaponMode.FreezeRay, world.LocalPlayer.ExperimentalEngineerAlternateWeaponMode);
        Assert.False(world.LocalPlayer.IsMedicHealing);
        Assert.Null(world.LocalPlayer.MedicHealTargetId);
        Assert.Null(world.LocalPlayer.ExperimentalAdditionalMedicBeamTargetPlayerId1);
        Assert.Null(world.LocalPlayer.ExperimentalAdditionalMedicBeamTargetPlayerId2);

        Assert.True(InvokeTryHandleExperimentalEngineerAlternateWeaponInteraction(world, world.LocalPlayer));
        Assert.False(world.LocalPlayer.IsExperimentalOffhandEquipped);
        Assert.Equal(ExperimentalEngineerAlternateWeaponMode.None, world.LocalPlayer.ExperimentalEngineerAlternateWeaponMode);
    }

    [Fact]
    public void EngineerBeamOffhandDoesNotBlockSecondarySentryPlacement()
    {
        var world = CreateJoinedEngineerWorld(new ExperimentalGameplaySettings(EnableEngineerEssenceExtractor: true));
        Assert.True(world.TryMoveLocalPlayerToControlPointSpawn());
        AdvanceTicks(world, 1);

        Assert.True(InvokeTryHandleExperimentalEngineerAlternateWeaponInteraction(world, world.LocalPlayer));
        Assert.True(world.LocalPlayer.IsExperimentalOffhandEquipped);
        Assert.Equal(ExperimentalEngineerAlternateWeaponMode.EssenceExtractor, world.LocalPlayer.ExperimentalEngineerAlternateWeaponMode);

        TryHandleNetworkSecondaryAbilityMethod.Invoke(
            world,
            [
                world.LocalPlayer,
                default(PlayerInputSnapshot),
                default(PlayerInputSnapshot),
                GameplayAbilityInputPhase.Pressed,
                world.LocalPlayer.X,
                world.LocalPlayer.Y,
            ]);

        Assert.Single(world.Sentries);
        Assert.True(world.LocalPlayer.IsExperimentalOffhandEquipped);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void EngineerBeamAvailableDoesNotBlockShotgunOutSentryPlacement(bool enableEssenceExtractor, bool enableFreezeRay)
    {
        var world = CreateJoinedEngineerWorld(new ExperimentalGameplaySettings(
            EnableEngineerEssenceExtractor: enableEssenceExtractor,
            EnableEngineerFreezeRay: enableFreezeRay));
        Assert.True(world.TryMoveLocalPlayerToControlPointSpawn());
        AdvanceTicks(world, 1);

        Assert.True(world.LocalPlayer.HasExperimentalOffhandWeapon);
        Assert.False(world.LocalPlayer.IsExperimentalOffhandEquipped);

        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: true,
            AimWorldX: world.LocalPlayer.X + 96f,
            AimWorldY: world.LocalPlayer.Y,
            DebugKill: false));
        world.AdvanceOneTick();

        Assert.Single(world.Sentries);
    }

    [Fact]
    public void SoldierCivilDefenseTurretUsesRightClickWithoutTogglingOffhand()
    {
        var world = CreateJoinedSoldierWorld(new ExperimentalGameplaySettings(
            EnableSoldierShotgunSecondaryWeapon: true,
            EnableSoldierCivilDefenseTurret: true));
        Assert.True(world.TryMoveLocalPlayerToControlPointSpawn());
        AdvanceTicks(world, 1);

        Assert.True(world.LocalPlayer.HasExperimentalOffhandWeapon);
        Assert.False(world.LocalPlayer.IsExperimentalOffhandEquipped);

        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: true,
            AimWorldX: world.LocalPlayer.X + 96f,
            AimWorldY: world.LocalPlayer.Y,
            DebugKill: false));
        world.AdvanceOneTick();

        Assert.Single(world.CivilDefenseTurrets);
        Assert.False(world.LocalPlayer.IsExperimentalOffhandEquipped);
    }

    [Fact]
    public void SoldierSwapWeaponInputTogglesOffhandOnlyOnceWithSpaceUtilityInput()
    {
        var world = CreateJoinedSoldierWorld(new ExperimentalGameplaySettings(EnableSoldierShotgunSecondaryWeapon: true));
        AdvanceTicks(world, 1);

        Assert.True(world.LocalPlayer.HasExperimentalOffhandWeapon);
        Assert.False(world.LocalPlayer.IsExperimentalOffhandEquipped);

        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: false,
            AimWorldX: world.LocalPlayer.X + 96f,
            AimWorldY: world.LocalPlayer.Y,
            DebugKill: false,
            UseAbility: true,
            SwapWeapon: true));
        world.AdvanceOneTick();

        Assert.True(world.LocalPlayer.IsExperimentalOffhandEquipped);
    }

    [Fact]
    public void SoldierSwapWeaponInputCanToggleOffhandBackToPrimary()
    {
        var world = CreateJoinedSoldierWorld(new ExperimentalGameplaySettings(EnableSoldierShotgunSecondaryWeapon: true));
        AdvanceTicks(world, 1);

        Assert.True(world.LocalPlayer.HasExperimentalOffhandWeapon);
        Assert.False(world.LocalPlayer.IsExperimentalOffhandEquipped);

        PressSwapWeaponSpace(world);
        Assert.True(world.LocalPlayer.IsExperimentalOffhandEquipped);

        ReleaseAllInput(world);

        PressSwapWeaponSpace(world);
        Assert.False(world.LocalPlayer.IsExperimentalOffhandEquipped);
        Assert.Equal(GameplayEquipmentSlot.Primary, world.LocalPlayer.GameplayLoadoutState.EquippedSlot);
    }

    [Fact]
    public void SoldierUseAbilityInputCanToggleOffhandBackToPrimary()
    {
        var world = CreateJoinedSoldierWorld(new ExperimentalGameplaySettings(EnableSoldierShotgunSecondaryWeapon: true));
        AdvanceTicks(world, 1);

        Assert.True(world.LocalPlayer.HasExperimentalOffhandWeapon);
        Assert.False(world.LocalPlayer.IsExperimentalOffhandEquipped);

        PressUseAbilitySpace(world);
        Assert.True(world.LocalPlayer.IsExperimentalOffhandEquipped);

        ReleaseAllInput(world);

        PressUseAbilitySpace(world);
        Assert.False(world.LocalPlayer.IsExperimentalOffhandEquipped);
        Assert.Equal(GameplayEquipmentSlot.Primary, world.LocalPlayer.GameplayLoadoutState.EquippedSlot);
    }

    [Fact]
    public void HeavyUseAbilityInputStartsBurstGhostDashWithoutEatingSandvich()
    {
        var world = CreateJoinedHeavyWorld(new ExperimentalGameplaySettings());
        AdvanceTicks(world, 1);
        Assert.True(world.TryMoveLocalPlayerToControlPointSpawn());

        var durationTicks = GetHeavyGhostDashDurationTicks(world);
        var startX = world.LocalPlayer.X;
        PressUseAbilitySpace(world);
        var initialSpeed = MathF.Abs(world.LocalPlayer.HorizontalSpeed);
        var expectedBurstSpeed = LegacyMovementModel.GetMaxRunSpeed(world.LocalPlayer.RunPower)
            * ExperimentalGameplaySettings.HeavyGhostDashBurstSpeedMultiplier;

        Assert.True(world.LocalPlayer.IsExperimentalGhostDashing);
        Assert.True(world.LocalPlayer.IsExperimentalGhostDashVisible);
        Assert.InRange(world.LocalPlayer.ExperimentalGhostDashTrailAlpha, 0f, 0.5f);
        Assert.False(world.LocalPlayer.IsHeavyEating);
        Assert.Equal(GetHeavyGhostDashCooldownTicks(world), world.LocalPlayer.ExperimentalGhostDashCooldownTicksRemaining);
        Assert.True(
            initialSpeed > expectedBurstSpeed * 0.25f,
            $"expected burst speed; speed={initialSpeed:0.###} expected={expectedBurstSpeed:0.###}");

        world.SetLocalInput(default);
        AdvanceTicks(world, durationTicks + 2);

        var totalDistance = MathF.Abs(world.LocalPlayer.X - startX);
        Assert.True(totalDistance > 10f);
        Assert.False(world.LocalPlayer.IsExperimentalGhostDashing);
        Assert.True(world.LocalPlayer.ExperimentalGhostDashCooldownTicksRemaining > 0);
    }

    [Fact]
    public void StockHeavyGhostDashTreatsStaleMomentumContentAsBurst()
    {
        var world = CreateJoinedHeavyWorld(new ExperimentalGameplaySettings());
        AdvanceTicks(world, 1);
        Assert.True(world.TryMoveLocalPlayerToControlPointSpawn());

        using var staleParameters = JsonDocument.Parse(
            """
            {
              "durationSeconds": 0.25,
              "movementDurationSeconds": 0.75,
              "cooldownSeconds": 12,
              "impulse": 75,
              "nextAttackDamageMultiplier": 1.4,
              "slideVelocityPerTick": 3,
              "useMomentum": true
            }
            """);
        var staleAbility = new GameplayAbilityDefinition(
            Category: GameplayAbilityConstants.UtilityCategory,
            Activation: GameplayAbilityConstants.PressedActivation,
            ExecutorId: BuiltInGameplayBehaviorIds.HeavyGhostDash,
            Parameters: staleParameters.RootElement
                .EnumerateObject()
                .ToDictionary(property => property.Name, property => property.Value.Clone(), StringComparer.OrdinalIgnoreCase));
        var item = CharacterClassCatalog.RuntimeRegistry.GetRequiredItem(StockGameplayModCatalog.HeavyUtilityItemId) with
        {
            Ability = staleAbility,
        };
        var input = new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: false,
            AimWorldX: world.LocalPlayer.X + 96f,
            AimWorldY: world.LocalPlayer.Y,
            DebugKill: false,
            UseAbility: true,
            SwapWeapon: true);
        world.LocalPlayer.ApplyVelocityImpulse(0f, 0f);

        var executeMethod = typeof(SimulationWorld).GetMethod(
            "ExecuteHeavyGhostDashAbility",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(executeMethod);

        var result = (GameplayAbilityResult)executeMethod!.Invoke(
            world,
            [
                new GameplayAbilityContext
                {
                    World = world,
                    Player = world.LocalPlayer,
                    Item = item,
                    Ability = staleAbility,
                    Phase = GameplayAbilityInputPhase.Pressed,
                    Input = input,
                    PreviousInput = default,
                    SourceX = world.LocalPlayer.X,
                    SourceY = world.LocalPlayer.Y,
                },
            ])!;
        var expectedBurstSpeed = LegacyMovementModel.GetMaxRunSpeed(world.LocalPlayer.RunPower)
            * ExperimentalGameplaySettings.HeavyGhostDashBurstSpeedMultiplier;

        Assert.True(result.Handled);
        Assert.True(result.ConsumedInput);
        Assert.True(world.LocalPlayer.IsExperimentalGhostDashing);
        Assert.True(world.LocalPlayer.IsExperimentalGhostDashVisible);
        Assert.False(world.LocalPlayer.IsHeavyEating);
        Assert.True(MathF.Abs(world.LocalPlayer.HorizontalSpeed) >= expectedBurstSpeed * 0.95f);
    }

    [Fact]
    public void HeavyRightClickCancelsSandvichWithDoubleCooldown()
    {
        var world = CreateJoinedHeavyWorld(new ExperimentalGameplaySettings());
        AdvanceTicks(world, 1);
        world.LocalPlayer.ForceSetHealth(world.LocalPlayer.MaxHealth - 80);

        PressFireSecondary(world);

        Assert.True(world.LocalPlayer.IsHeavyEating);
        Assert.Equal(PlayerEntity.HeavySandvichCooldownTicks, world.LocalPlayer.HeavyEatCooldownTicksRemaining);
        Assert.Equal(PlayerEntity.HeavySandvichCooldownTicks, world.LocalPlayer.HeavyEatCooldownDurationTicks);

        ReleaseAllInput(world);
        PressFireSecondary(world);

        Assert.False(world.LocalPlayer.IsHeavyEating);
        Assert.Equal(0, world.LocalPlayer.HeavyEatTicksRemaining);
        Assert.Equal(PlayerEntity.HeavySandvichCooldownTicks * 2, world.LocalPlayer.HeavyEatCooldownTicksRemaining);
        Assert.Equal(PlayerEntity.HeavySandvichCooldownTicks * 2, world.LocalPlayer.HeavyEatCooldownDurationTicks);
    }

    [Fact]
    public void HeavyMinigunShotEffectsApplyIncreasedKnockbackAndMovementSlow()
    {
        var world = CreateJoinedHeavyWorld(new ExperimentalGameplaySettings());
        AdvanceTicks(world, 1);
        Assert.True(world.TryMoveLocalPlayerToControlPointSpawn());
        var target = CreateBlueNetworkScout(world, 2);
        target.ForceSetHealth(999);
        target.TeleportTo(world.LocalPlayer.X + 96f, world.LocalPlayer.Y);
        AdvanceTicks(world, 1);
        var stockMinigun = CharacterClassCatalog.RuntimeRegistry.CreatePrimaryWeaponDefinition(
            CharacterClassCatalog.RuntimeRegistry.GetRequiredItem("weapon.minigun"));
        var baselineSpeedMultiplier = InvokeGetExperimentalMovementSpeedMultiplier(target);
        var baselineHorizontalSpeed = MathF.Abs(target.HorizontalSpeed);
        var slowTicks = Math.Max(
            1,
            (int)MathF.Ceiling(stockMinigun.PlayerSlowRefreshSourceTicks * world.Config.TicksPerSecond / LegacyMovementModel.SourceTicksPerSecond));

        InvokeSpawnShot(
            world,
            world.LocalPlayer,
            target.X - 12f,
            target.Y,
            12f,
            0f,
            stockMinigun.DirectHitDamage ?? ShotProjectileEntity.DamagePerHit,
            stockMinigun.PlayerKnockbackScale,
            stockMinigun.PlayerSlowMovementMultiplier,
            slowTicks);
        for (var tick = 0; tick < 20 && !target.IsDirectFireSlowed; tick += 1)
        {
            world.AdvanceOneTick();
        }

        Assert.True(
            target.IsDirectFireSlowed,
            $"target was not slowed; health={target.Health}, shots={world.Shots.Count}, target=({target.X:0.###},{target.Y:0.###}), heavy=({world.LocalPlayer.X:0.###},{world.LocalPlayer.Y:0.###})");
        Assert.True(target.Health < 999);
        Assert.True(MathF.Abs(target.HorizontalSpeed) > baselineHorizontalSpeed);
        Assert.True(stockMinigun.PlayerKnockbackScale > 1f);
        Assert.Equal(baselineSpeedMultiplier * 0.97f, InvokeGetExperimentalMovementSpeedMultiplier(target), precision: 3);
    }

    [Fact]
    public void HeavyGhostDashRechargesAfterCooldown()
    {
        var world = CreateJoinedHeavyWorld(new ExperimentalGameplaySettings());
        AdvanceTicks(world, 1);

        PressUseAbilitySpace(world);
        ReleaseAllInput(world);
        AdvanceTicks(world, GetHeavyGhostDashCooldownTicks(world) - 1);

        Assert.Equal(0, world.LocalPlayer.ExperimentalGhostDashCooldownTicksRemaining);

        PressUseAbilitySpace(world);

        Assert.True(world.LocalPlayer.IsExperimentalGhostDashing);
        Assert.Equal(GetHeavyGhostDashCooldownTicks(world), world.LocalPlayer.ExperimentalGhostDashCooldownTicksRemaining);
    }

    [Fact]
    public void HeavyAbilityInputRoutesThroughDispatcherAndRecordsUsedEvent()
    {
        var world = CreateJoinedHeavyWorld(new ExperimentalGameplaySettings());
        AdvanceTicks(world, 1);

        PressUseAbilitySpace(world);

        var abilityEvent = Assert.Single(world.DrainPendingGameplayAbilityEvents());
        Assert.True(abilityEvent.Handled);
        Assert.True(abilityEvent.ConsumedInput);
        Assert.False(abilityEvent.Cancelled);
        Assert.Equal(world.LocalPlayer.Id, abilityEvent.PlayerId);
        Assert.Equal("ability.heavy-utility", abilityEvent.ItemId);
        Assert.Equal(GameplayAbilityConstants.UtilityCategory, abilityEvent.AbilityCategory);
        Assert.Equal(BuiltInGameplayBehaviorIds.HeavyGhostDash, abilityEvent.ExecutorId);
        Assert.Equal(GameplayAbilityInputPhase.Pressed, abilityEvent.Phase);
        Assert.True(world.LocalPlayer.IsExperimentalGhostDashing);
    }

    [Fact]
    public void HeavyAbilityInputCanBeCancelledBeforeExecution()
    {
        var world = CreateJoinedHeavyWorld(new ExperimentalGameplaySettings());
        AdvanceTicks(world, 1);
        var intercepted = false;
        world.GameplayAbilityInputInterceptor = abilityEvent =>
        {
            intercepted = true;
            Assert.Equal("ability.heavy-utility", abilityEvent.ItemId);
            Assert.Equal(BuiltInGameplayBehaviorIds.HeavyGhostDash, abilityEvent.ExecutorId);
            return true;
        };

        PressUseAbilitySpace(world);

        Assert.True(intercepted);
        Assert.False(world.LocalPlayer.IsExperimentalGhostDashing);
        Assert.Equal(0, world.LocalPlayer.ExperimentalGhostDashCooldownTicksRemaining);
        var abilityEvent = Assert.Single(world.DrainPendingGameplayAbilityEvents());
        Assert.True(abilityEvent.Cancelled);
        Assert.True(abilityEvent.ConsumedInput);
        Assert.False(abilityEvent.Handled);
    }

    [Fact]
    public void StockAbilityStateIsMirroredThroughCoreAbilityReplicatedState()
    {
        var world = CreateJoinedHeavyWorld(new ExperimentalGameplaySettings());
        AdvanceTicks(world, 1);

        PressUseAbilitySpace(world);

        Assert.True(GameplayAbilityReplicatedState.TryGetInt(
            world.LocalPlayer,
            GameplayAbilityReplicatedState.HeavyDashCooldownTicksKey,
            out var cooldownTicks));
        Assert.Equal(GetHeavyGhostDashCooldownTicks(world), cooldownTicks);
    }

    [Fact]
    public void SpecialAbilitiesDisabledLeavesCoreRightClickButBlocksUtilityAbilityDispatch()
    {
        var world = CreateJoinedHeavyWorld(new ExperimentalGameplaySettings(EnableSecondaryAbilities: false));
        AdvanceTicks(world, 1);

        world.LocalPlayer.ForceSetHealth(world.LocalPlayer.MaxHealth - 80);
        PressFireSecondary(world);

        Assert.True(world.LocalPlayer.IsHeavyEating);
        AssertCoreSecondaryAbilityEvent(world, "ability.heavy-sandvich", BuiltInGameplayBehaviorIds.HeavySandvich);

        ReleaseAllInput(world);
        PressUseAbilitySpace(world);

        Assert.False(world.LocalPlayer.IsExperimentalGhostDashing);
        Assert.Equal(0, world.LocalPlayer.ExperimentalGhostDashCooldownTicksRemaining);
        Assert.Empty(world.DrainPendingGameplayAbilityEvents());
    }

    [Fact]
    public void SpecialAbilitiesDisabledBlocksDataDrivenSecondaryWeapons()
    {
        var world = CreateJoinedSoldierWorld(new ExperimentalGameplaySettings(
            EnableSecondaryAbilities: false,
            EnableSoldierShotgunSecondaryWeapon: true));
        AdvanceTicks(world, 1);

        Assert.False(world.LocalPlayer.HasExperimentalOffhandWeapon);

        PressSwapWeaponSpace(world);

        Assert.False(world.LocalPlayer.IsExperimentalOffhandEquipped);
        Assert.Equal(GameplayEquipmentSlot.Primary, world.LocalPlayer.GameplayLoadoutState.EquippedSlot);
    }

    [Fact]
    public void DisablingSpecialAbilitiesAtRuntimeResyncsExistingSecondaryWeapons()
    {
        var world = CreateJoinedSoldierWorld(new ExperimentalGameplaySettings(EnableSoldierShotgunSecondaryWeapon: true));
        var networkSoldier = CreateNetworkSoldier(world, 2);
        AdvanceTicks(world, 1);

        PressSwapWeaponSpace(world);
        PressNetworkSwapWeaponSpace(world, 2, networkSoldier);

        Assert.True(world.LocalPlayer.IsExperimentalOffhandEquipped);
        Assert.True(networkSoldier.IsExperimentalOffhandEquipped);

        world.ConfigureExperimentalGameplaySettings(world.ExperimentalGameplaySettings with
        {
            EnableSecondaryAbilities = false,
            EnableSoldierShotgunSecondaryWeapon = false,
        });

        Assert.False(world.LocalPlayer.HasExperimentalOffhandWeapon);
        Assert.False(world.LocalPlayer.IsExperimentalOffhandEquipped);
        Assert.False(networkSoldier.HasExperimentalOffhandWeapon);
        Assert.False(networkSoldier.IsExperimentalOffhandEquipped);
    }

    [Fact]
    public void SpecialAbilitiesDisabledBlocksPassiveAbilityDispatch()
    {
        var world = CreateJoinedEngineerWorld(new ExperimentalGameplaySettings(
            EnableSecondaryAbilities: false,
            EnablePassiveHealthRegeneration: true));
        AdvanceTicks(world, 1);
        world.LocalPlayer.ForceSetHealth(world.LocalPlayer.MaxHealth - 30);

        AdvanceTicks(world, world.Config.TicksPerSecond);

        Assert.Equal(world.LocalPlayer.MaxHealth - 30, world.LocalPlayer.Health);
        Assert.Empty(world.DrainPendingGameplayAbilityEvents());
    }

    [Fact]
    public void NetworkSoldierSwapWeaponInputCanToggleOffhandBackToPrimary()
    {
        var world = CreateJoinedSoldierWorld(new ExperimentalGameplaySettings(EnableSoldierShotgunSecondaryWeapon: true));
        var player = CreateNetworkSoldier(world, 2);
        AdvanceTicks(world, 1);

        Assert.True(player.HasExperimentalOffhandWeapon);
        Assert.False(player.IsExperimentalOffhandEquipped);

        PressNetworkSwapWeaponSpace(world, 2, player);
        Assert.True(player.IsExperimentalOffhandEquipped);

        ReleaseNetworkInput(world, 2);

        PressNetworkSwapWeaponSpace(world, 2, player);
        Assert.False(player.IsExperimentalOffhandEquipped);
        Assert.Equal(GameplayEquipmentSlot.Primary, player.GameplayLoadoutState.EquippedSlot);
    }

    [Fact]
    public void SoldierPrimarySlotSnapshotAfterShotgunSwapAllowsRocketFire()
    {
        var world = CreateJoinedSoldierWorld(new ExperimentalGameplaySettings(EnableSoldierShotgunSecondaryWeapon: true));
        AdvanceTicks(world, 1);

        PressSwapWeaponSpace(world);
        Assert.True(world.LocalPlayer.IsExperimentalOffhandSelected);

        ApplyPrimarySoldierSnapshot(world.LocalPlayer);

        Assert.False(world.LocalPlayer.IsExperimentalOffhandEquipped);
        Assert.False(world.LocalPlayer.IsExperimentalOffhandSelected);
        Assert.Equal(GameplayEquipmentSlot.Primary, world.LocalPlayer.GameplayLoadoutState.EquippedSlot);

        ReleaseAllInput(world);
        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: true,
            FireSecondary: false,
            AimWorldX: world.LocalPlayer.X + 96f,
            AimWorldY: world.LocalPlayer.Y,
            DebugKill: false));
        world.AdvanceOneTick();

        Assert.NotEmpty(world.Rockets);
    }

    [Fact]
    public void SoldierSwapWeaponInputStowsSelectedSecondarySlotBackToPrimary()
    {
        var world = CreateJoinedSoldierWorld(new ExperimentalGameplaySettings(EnableSoldierShotgunSecondaryWeapon: true));
        AdvanceTicks(world, 1);

        Assert.True(world.LocalPlayer.HasExperimentalOffhandWeapon);
        Assert.True(world.LocalPlayer.TrySelectGameplayEquippedSlot(GameplayEquipmentSlot.Secondary));
        Assert.False(world.LocalPlayer.IsExperimentalOffhandEquipped);
        Assert.True(world.LocalPlayer.IsExperimentalOffhandSelected);
        Assert.Equal(GameplayEquipmentSlot.Secondary, world.LocalPlayer.GameplayLoadoutState.EquippedSlot);

        PressSwapWeaponSpace(world);

        Assert.False(world.LocalPlayer.IsExperimentalOffhandEquipped);
        Assert.False(world.LocalPlayer.IsExperimentalOffhandSelected);
        Assert.Equal(GameplayEquipmentSlot.Primary, world.LocalPlayer.GameplayLoadoutState.EquippedSlot);
    }

    [Fact]
    public void SwapWeaponInputTogglesAnyClassSecondaryWeaponWithoutFiringUtility()
    {
        var world = CreateJoinedScoutWorld(new ExperimentalGameplaySettings());
        Assert.True(world.TrySetNetworkPlayerGameplaySecondaryItem(SimulationWorld.LocalPlayerSlot, "weapon.rocketlauncher"));
        AdvanceTicks(world, 1);

        Assert.True(world.LocalPlayer.HasExperimentalOffhandWeapon);
        Assert.False(world.LocalPlayer.IsExperimentalOffhandEquipped);

        PressSwapWeaponSpace(world);

        Assert.True(world.LocalPlayer.IsExperimentalOffhandEquipped);
        Assert.False(world.LocalPlayer.IsTaunting);

        ReleaseAllInput(world);

        PressSwapWeaponSpace(world);

        Assert.False(world.LocalPlayer.IsExperimentalOffhandEquipped);
        Assert.Equal(GameplayEquipmentSlot.Primary, world.LocalPlayer.GameplayLoadoutState.EquippedSlot);
        Assert.False(world.LocalPlayer.IsTaunting);
    }

    [Fact]
    public void MedicSwapWeaponSpaceUsesUberWithoutEquippingSecondaryWeapon()
    {
        var world = CreateJoinedMedicWorld(new ExperimentalGameplaySettings());
        AdvanceTicks(world, 1);

        Assert.True(world.LocalPlayer.HasExperimentalOffhandWeapon);
        Assert.False(world.LocalPlayer.IsExperimentalOffhandEquipped);
        world.LocalPlayer.FillMedicUberCharge();

        PressSwapWeaponSpace(world);

        Assert.True(world.LocalPlayer.IsMedicUbering);
        Assert.False(world.LocalPlayer.IsExperimentalOffhandEquipped);
        Assert.Equal(GameplayEquipmentSlot.Primary, world.LocalPlayer.GameplayLoadoutState.EquippedSlot);
    }

    [Fact]
    public void MedicRightClickFiresNeedlesWithoutEquippingSecondaryWeapon()
    {
        var world = CreateJoinedMedicWorld(new ExperimentalGameplaySettings());
        AdvanceTicks(world, 1);

        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: true,
            AimWorldX: world.LocalPlayer.X + 96f,
            AimWorldY: world.LocalPlayer.Y,
            DebugKill: false));
        world.AdvanceOneTick();

        Assert.NotEmpty(world.Needles);
        Assert.False(world.LocalPlayer.IsExperimentalOffhandEquipped);
        Assert.Equal(GameplayEquipmentSlot.Primary, world.LocalPlayer.GameplayLoadoutState.EquippedSlot);
    }

    [Fact]
    public void EssenceExtractorWorksThroughEquippedOffhandPrimaryInput()
    {
        var world = CreateJoinedEngineerWorld(new ExperimentalGameplaySettings(EnableEngineerEssenceExtractor: true));
        Assert.True(world.TryMoveLocalPlayerToControlPointSpawn());
        var enemy = CreateBlueNetworkScout(world, 2);
        enemy.ForceSetHealth(999);
        enemy.TeleportTo(world.LocalPlayer.X + 96f, world.LocalPlayer.Y);
        AdvanceTicks(world, 1);

        Assert.True(InvokeTryHandleExperimentalEngineerAlternateWeaponInteraction(world, world.LocalPlayer));
        Assert.Equal(ExperimentalEngineerAlternateWeaponMode.EssenceExtractor, world.LocalPlayer.ExperimentalEngineerAlternateWeaponMode);
        var healthBefore = enemy.Health;

        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: true,
            FireSecondary: false,
            AimWorldX: enemy.X,
            AimWorldY: enemy.Y,
            DebugKill: false));
        AdvanceTicks(world, 5);

        Assert.True(enemy.Health < healthBefore);
        Assert.Equal(enemy.Id, world.LocalPlayer.MedicHealTargetId);
    }

    [Fact]
    public void FreezeRayWorksThroughEquippedOffhandPrimaryInput()
    {
        var world = CreateJoinedEngineerWorld(new ExperimentalGameplaySettings(EnableEngineerFreezeRay: true));
        Assert.True(world.TryMoveLocalPlayerToControlPointSpawn());
        var enemy = CreateBlueNetworkScout(world, 2);
        enemy.ForceSetHealth(999);
        enemy.TeleportTo(world.LocalPlayer.X + 96f, world.LocalPlayer.Y);
        AdvanceTicks(world, 1);

        Assert.True(InvokeTryHandleExperimentalEngineerAlternateWeaponInteraction(world, world.LocalPlayer));
        Assert.Equal(ExperimentalEngineerAlternateWeaponMode.FreezeRay, world.LocalPlayer.ExperimentalEngineerAlternateWeaponMode);

        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: true,
            FireSecondary: false,
            AimWorldX: enemy.X,
            AimWorldY: enemy.Y,
            DebugKill: false));
        AdvanceTicks(world, world.Config.TicksPerSecond * 3);

        Assert.True(enemy.IsExperimentalCryoFrozen);
        Assert.Equal(enemy.Id, world.LocalPlayer.MedicHealTargetId);
    }

    [Fact]
    public void EssenceExtractorDrainsEnemyAndAppliesDamageVulnerability()
    {
        var world = CreateJoinedEngineerWorld(new ExperimentalGameplaySettings(EnableEngineerEssenceExtractor: true));
        Assert.True(world.TryMoveLocalPlayerToControlPointSpawn());
        var enemy = CreateBlueNetworkScout(world, 2);
        enemy.TeleportTo(world.LocalPlayer.X + 96f, world.LocalPlayer.Y);
        var healthBefore = enemy.Health;

        for (var tick = 0; tick < 5; tick += 1)
        {
            InvokeUpdateExperimentalEngineerEssenceExtractor(world, world.LocalPlayer, enemy.X, enemy.Y);
        }

        Assert.True(enemy.Health < healthBefore);
        Assert.True(enemy.ExperimentalDamageTakenMultiplier > 1f);
    }

    [Fact]
    public void EssenceExtractorDealsTwentyFiveDamagePerSecondAndHealsInOneChunk()
    {
        var world = CreateJoinedEngineerWorld(new ExperimentalGameplaySettings(EnableEngineerEssenceExtractor: true));
        Assert.True(world.TryMoveLocalPlayerToControlPointSpawn());
        var enemy = CreateBlueNetworkScout(world, 2);
        world.LocalPlayer.ForceSetHealth(Math.Max(1, world.LocalPlayer.MaxHealth - 40));
        enemy.ForceSetHealth(999);
        enemy.TeleportTo(world.LocalPlayer.X + 96f, world.LocalPlayer.Y);
        var healthBefore = enemy.Health;
        var localHealthBefore = world.LocalPlayer.Health;

        for (var tick = 0; tick < world.Config.TicksPerSecond; tick += 1)
        {
            InvokeUpdateExperimentalEngineerEssenceExtractor(world, world.LocalPlayer, enemy.X, enemy.Y);
        }

        Assert.Equal(25, healthBefore - enemy.Health);
        Assert.Equal(25, world.LocalPlayer.Health - localHealthBefore);
        var healingEvents = world.PendingHealingEvents;
        Assert.Single(healingEvents);
        Assert.Equal(20, healingEvents[0].Amount);
    }

    [Fact]
    public void FreezeRayChainsAcrossThreeTargetsWithoutHealingEngineer()
    {
        var world = CreateJoinedEngineerWorld(new ExperimentalGameplaySettings(EnableEngineerFreezeRay: true));
        Assert.True(world.TryMoveLocalPlayerToControlPointSpawn());
        world.EnemyPlayer.ForceSetHealth(0);
        var primaryEnemy = CreateBlueNetworkScout(world, 2);
        var chainedEnemyA = CreateBlueNetworkScout(world, 3);
        var chainedEnemyB = CreateBlueNetworkScout(world, 4);
        var unaffectedEnemy = CreateBlueNetworkScout(world, 5);
        primaryEnemy.ForceSetHealth(999);
        chainedEnemyA.ForceSetHealth(999);
        chainedEnemyB.ForceSetHealth(999);
        unaffectedEnemy.ForceSetHealth(999);
        world.LocalPlayer.ForceSetHealth(Math.Max(1, world.LocalPlayer.MaxHealth - 40));
        primaryEnemy.TeleportTo(world.LocalPlayer.X + 96f, world.LocalPlayer.Y);
        chainedEnemyA.TeleportTo(primaryEnemy.X + 36f, primaryEnemy.Y + 8f);
        chainedEnemyB.TeleportTo(primaryEnemy.X + 60f, primaryEnemy.Y - 6f);
        unaffectedEnemy.TeleportTo(primaryEnemy.X + 132f, primaryEnemy.Y + 88f);
        var localHealthBefore = world.LocalPlayer.Health;

        for (var tick = 0; tick < world.Config.TicksPerSecond * 3; tick += 1)
        {
            InvokeUpdateExperimentalEngineerFreezeRay(world, world.LocalPlayer, primaryEnemy.X, primaryEnemy.Y);
        }

        Assert.True(primaryEnemy.IsExperimentalCryoFrozen);
        Assert.True(chainedEnemyA.IsExperimentalCryoFrozen);
        Assert.True(chainedEnemyB.IsExperimentalCryoFrozen);
        Assert.False(unaffectedEnemy.IsExperimentalCryoFrozen);
        Assert.True(primaryEnemy.Health < 999);
        Assert.True(chainedEnemyA.Health < 999);
        Assert.True(chainedEnemyB.Health < 999);
        Assert.Equal(localHealthBefore, world.LocalPlayer.Health);
        Assert.Equal(primaryEnemy.Id, world.LocalPlayer.MedicHealTargetId);
        Assert.NotNull(world.LocalPlayer.ExperimentalAdditionalMedicBeamTargetPlayerId1);
        Assert.NotNull(world.LocalPlayer.ExperimentalAdditionalMedicBeamTargetPlayerId2);
    }

    [Fact]
    public void FreezeRayExposureSurvivesShortBreakAndWeakensEnemyCombat()
    {
        var world = CreateJoinedEngineerWorld(new ExperimentalGameplaySettings(EnableEngineerFreezeRay: true));
        Assert.True(world.TryMoveLocalPlayerToControlPointSpawn());
        var enemy = CreateBlueNetworkScout(world, 2);
        enemy.ForceSetHealth(999);
        enemy.TeleportTo(world.LocalPlayer.X + 96f, world.LocalPlayer.Y);

        for (var tick = 0; tick < 10; tick += 1)
        {
            InvokeUpdateExperimentalEngineerFreezeRay(world, world.LocalPlayer, enemy.X, enemy.Y);
        }

        enemy.ForceSetAmmo(enemy.PrimaryWeapon.MaxAmmo);
        Assert.True(enemy.TryFirePrimaryWeapon());
        Assert.True(enemy.PrimaryCooldownTicks > enemy.PrimaryWeapon.ReloadDelayTicks);

        var healthBefore = world.LocalPlayer.Health;
        _ = InvokeApplyPlayerDamage(world, world.LocalPlayer, 10, enemy);
        Assert.Equal(healthBefore - 6, world.LocalPlayer.Health);

        for (var tick = 0; tick < (world.Config.TicksPerSecond * 3) / 2; tick += 1)
        {
            InvokeUpdateExperimentalEngineerFreezeRay(world, world.LocalPlayer, enemy.X, enemy.Y);
        }

        AdvanceTicks(world, world.Config.TicksPerSecond);

        for (var tick = 0; tick < world.Config.TicksPerSecond; tick += 1)
        {
            InvokeUpdateExperimentalEngineerFreezeRay(world, world.LocalPlayer, enemy.X, enemy.Y);
        }

        Assert.True(enemy.IsExperimentalCryoFrozen);
    }

    [Fact]
    public void FreezeRayFrozenKillsSpawnCryoTintedGibsAndBlood()
    {
        var world = CreateJoinedEngineerWorld(new ExperimentalGameplaySettings(EnableEngineerFreezeRay: true));
        Assert.True(world.TryMoveLocalPlayerToControlPointSpawn());
        world.EnemyPlayer.ForceSetHealth(0);
        var enemy = CreateBlueNetworkScout(world, 2);
        enemy.ForceSetHealth(999);
        enemy.TeleportTo(world.LocalPlayer.X + 96f, world.LocalPlayer.Y);

        for (var tick = 0; tick < world.Config.TicksPerSecond * 3; tick += 1)
        {
            InvokeUpdateExperimentalEngineerFreezeRay(world, world.LocalPlayer, enemy.X, enemy.Y);
        }

        enemy.ForceSetHealth(1);
        for (var tick = 0; tick < 6 && enemy.IsAlive; tick += 1)
        {
            InvokeUpdateExperimentalEngineerFreezeRay(world, world.LocalPlayer, enemy.X, enemy.Y);
        }

        Assert.False(enemy.IsAlive);
        Assert.Contains(world.PlayerGibs, static gib => gib.ExperimentalCryoTinted);
        Assert.Contains(world.BloodDrops, static blood => blood.ExperimentalCryoTinted);
    }

    [Fact]
    public void ExperimentalOverkillAugmentAddsTwoCaveatRocketsToShotgunShots()
    {
        var world = CreateJoinedEngineerWorld(new ExperimentalGameplaySettings(EnableEngineerExperimentalOverkillAugment: true));
        Assert.True(world.TryMoveLocalPlayerToControlPointSpawn());
        SetPlayerAimDirection(world.LocalPlayer, 0f);

        Assert.True(world.LocalPlayer.TryFirePrimaryWeapon());
        InvokeFirePrimaryWeapon(world, world.LocalPlayer, world.LocalPlayer.X + 128f, world.LocalPlayer.Y);

        Assert.Equal(ExperimentalGameplaySettings.DefaultEngineerExperimentalOverkillAugmentRocketCount, world.Rockets.Count);
        Assert.All(
            world.Rockets,
            rocket =>
            {
                Assert.True(rocket.EnableExperimentalCaveatTracking);
                Assert.Equal(ExperimentalGameplaySettings.DefaultEngineerCaveatInjectorRocketRenderScale, rocket.ExperimentalVisualScale);
            });
    }

    [Fact]
    public void ExperimentalOverkillAugmentRocketsSeekWithoutCaveatInjector()
    {
        var world = CreateJoinedEngineerWorld(new ExperimentalGameplaySettings(EnableEngineerExperimentalOverkillAugment: true));
        Assert.True(world.TryMoveLocalPlayerToControlPointSpawn());
        var enemy = CreateBlueNetworkScout(world, 2);
        enemy.TeleportTo(world.LocalPlayer.X + 220f, world.LocalPlayer.Y + 84f);
        SetPlayerAimDirection(world.LocalPlayer, 0f);
        var lockDelayTicks = Math.Max(
            1,
            (int)MathF.Round(world.Config.TicksPerSecond * ExperimentalGameplaySettings.DefaultEngineerCaveatInjectorRocketLockDelaySeconds));

        Assert.True(world.LocalPlayer.TryFirePrimaryWeapon());
        InvokeFirePrimaryWeapon(world, world.LocalPlayer, world.LocalPlayer.X + 128f, world.LocalPlayer.Y);

        var rocket = world.Rockets[0];
        Assert.True(rocket.EnableExperimentalCaveatTracking);
        for (var tick = 0; tick < lockDelayTicks; tick += 1)
        {
            rocket.AdvanceOneTick((float)world.Config.FixedDeltaSeconds);
        }

        Assert.True(InvokeTryResolveExperimentalEngineerRocketTrackingDirection(world, rocket, world.LocalPlayer, out var trackedDirection));
        Assert.True(MathF.Abs(trackedDirection) > 0.01f);
    }

    [Fact]
    public void CaveatInjectorLaunchesMiniRocketsEveryFifthShot()
    {
        var world = CreateJoinedEngineerWorld(new ExperimentalGameplaySettings(EnableEngineerCaveatInjector: true));
        world.EnemyPlayer.ForceSetHealth(0);
        var sentry = BuildAndCompleteLocalSentry(world);
        var enemy = CreateBlueNetworkScout(world, 2);
        enemy.ForceSetHealth(999);
        var targetPosition = FindVisibleSentryTargetPosition(world, sentry, enemy, preferredDistance: 220f, minimumDistance: 160f);
        enemy.TeleportTo(targetPosition.X, targetPosition.Y);

        for (var tick = 0; tick < 180 && world.Rockets.Count < ExperimentalGameplaySettings.DefaultEngineerCaveatInjectorRocketCount; tick += 1)
        {
            world.AdvanceOneTick();
        }

        Assert.True(sentry.ConsecutiveShotsFired >= 5);
        Assert.True(world.Rockets.Count >= ExperimentalGameplaySettings.DefaultEngineerCaveatInjectorRocketCount);
        Assert.All(
            world.Rockets,
            rocket =>
            {
                Assert.False(rocket.EnableExperimentalStingerTracking);
                Assert.True(rocket.EnableExperimentalCaveatTracking);
                Assert.Equal(ExperimentalGameplaySettings.DefaultEngineerCaveatInjectorDirectHitDamage, rocket.DirectHitDamageValue);
                Assert.Equal(ExperimentalGameplaySettings.DefaultEngineerCaveatInjectorExplosionDamage, rocket.ExplosionDamageValue);
                Assert.Equal(ExperimentalGameplaySettings.DefaultEngineerCaveatInjectorRocketRenderScale, rocket.ExperimentalVisualScale);
                Assert.True(rocket.DistanceToTravel > RocketProjectileEntity.MaxDistanceToTravel);
            });
    }

    [Fact]
    public void CaveatInjectorDelaysLockThenBurstsSpeedOnAcquire()
    {
        var world = CreateJoinedEngineerWorld(new ExperimentalGameplaySettings(EnableEngineerCaveatInjector: true));
        var enemy = CreateBlueNetworkScout(world, 2);
        enemy.TeleportTo(world.LocalPlayer.X + 196f, world.LocalPlayer.Y + 84f);
        var lockDelayTicks = Math.Max(
            1,
            (int)MathF.Round(world.Config.TicksPerSecond * ExperimentalGameplaySettings.DefaultEngineerCaveatInjectorRocketLockDelaySeconds));

        InvokeSpawnRocket(
            world,
            world.LocalPlayer,
            world.LocalPlayer.X,
            world.LocalPlayer.Y,
            0f,
            enableExperimentalCaveatTracking: true,
            visualScale: ExperimentalGameplaySettings.DefaultEngineerCaveatInjectorRocketRenderScale,
            trackingLockTicksRemaining: lockDelayTicks);

        var rocket = Assert.Single(world.Rockets);
        Assert.False(InvokeTryResolveExperimentalEngineerRocketTrackingDirection(world, rocket, world.LocalPlayer, out _));

        for (var tick = 0; tick < lockDelayTicks; tick += 1)
        {
            rocket.AdvanceOneTick((float)world.Config.FixedDeltaSeconds);
        }

        var speedBeforeLock = rocket.Speed;
        Assert.True(InvokeTryResolveExperimentalEngineerRocketTrackingDirection(world, rocket, world.LocalPlayer, out var trackedDirection));
        Assert.True(MathF.Abs(trackedDirection) > 0.01f);
        Assert.True(rocket.Speed > speedBeforeLock * 1.9f);
    }

    [Fact]
    public void BuildingSentryUsesEngineerAimDirectionForInitialFacing()
    {
        var world = CreateJoinedEngineerWorld(new ExperimentalGameplaySettings());
        _ = world.TryMoveLocalPlayerToControlPointSpawn();
        SetPlayerAimDirection(world.LocalPlayer, 180f);

        Assert.True(world.TryBuildLocalSentry());

        var sentry = Assert.Single(world.Sentries);
        Assert.True(sentry.StartDirectionX < 0f);
        Assert.True(sentry.FacingDirectionX < 0f);
    }

    [Fact]
    public void PrecisionInstantiatorExtendsSentryTargetRangeBeyondDefault()
    {
        var world = CreateJoinedEngineerWorld(new ExperimentalGameplaySettings(EnableEngineerPrecisionInstantiator: true));
        var sentry = BuildAndCompleteLocalSentry(world);
        _ = sentry;
        var range = InvokeGetExperimentalSentryTargetRange(world, world.LocalPlayer);

        Assert.True(range > SentryEntity.TargetRange);
    }

    [Fact]
    public void CaveatInjectorExtendsSentryTargetRangeBeyondDefault()
    {
        var world = CreateJoinedEngineerWorld(new ExperimentalGameplaySettings(EnableEngineerCaveatInjector: true));
        var range = InvokeGetExperimentalSentryTargetRange(world, world.LocalPlayer);

        Assert.True(range > SentryEntity.TargetRange);
    }

    [Fact]
    public void BuckshotConversionDealsMoreThanSingleBulletAtCloseRange()
    {
        var world = CreateJoinedEngineerWorld(new ExperimentalGameplaySettings(EnableEngineerBuckshotConversion: true));
        world.EnemyPlayer.ForceSetHealth(0);
        var sentry = BuildAndCompleteLocalSentry(world);
        var enemy = CreateBlueNetworkScout(world, 2);
        enemy.ForceSetHealth(999);
        var targetPosition = FindVisibleSentryTargetPosition(world, sentry, enemy, preferredDistance: 84f, minimumDistance: 56f);
        enemy.TeleportTo(targetPosition.X, targetPosition.Y);
        var healthBefore = enemy.Health;

        AdvanceTicks(world, 40);

        Assert.True(healthBefore - enemy.Health > SentryEntity.HitDamage);
    }

    [Fact]
    public void BuckshotConversionSpawnsScattergunPellets()
    {
        var world = CreateJoinedEngineerWorld(new ExperimentalGameplaySettings(EnableEngineerBuckshotConversion: true));
        world.EnemyPlayer.ForceSetHealth(0);
        var sentry = BuildAndCompleteLocalSentry(world);
        var enemy = CreateBlueNetworkScout(world, 2);
        enemy.ForceSetHealth(999);
        var targetPosition = FindVisibleSentryTargetPosition(world, sentry, enemy, preferredDistance: 180f, minimumDistance: 132f);
        enemy.TeleportTo(targetPosition.X, targetPosition.Y);

        for (var tick = 0; tick < 180 && world.Shots.Count == 0; tick += 1)
        {
            world.AdvanceOneTick();
        }

        Assert.True(world.Shots.Count >= CharacterClassCatalog.Scattergun.ProjectilesPerShot);
        Assert.All(
            world.Shots,
            shot =>
            {
                Assert.True(shot.ApplyExperimentalEngineerSentryPerkEffects);
                Assert.Equal(sentry.Id, shot.SourceSentryId);
            });
    }

    [Fact]
    public void IntegrityProjectorReflectsNearbyEnemyRockets()
    {
        var world = CreateJoinedEngineerWorld(new ExperimentalGameplaySettings(EnableEngineerIntegrityProjector: true));
        var sentry = BuildAndCompleteLocalSentry(world);
        var enemy = CreateBlueNetworkScout(world, 2);
        enemy.TeleportTo(sentry.X + 120f, sentry.Y);
        InvokeSpawnRocket(world, enemy, sentry.X + 16f, sentry.Y, 0f);

        AdvanceTicks(world, 2);

        var rocket = Assert.Single(world.Rockets);
        Assert.Equal(PlayerTeam.Red, rocket.Team);
    }

    [Fact]
    public void ConfusionFieldAssignsFriendlyFireTargetAndRetaliationMark()
    {
        var world = CreateJoinedEngineerWorld(new ExperimentalGameplaySettings(EnableEngineerConfusionField: true));
        var sentry = BuildAndCompleteLocalSentry(world);
        var confusedEnemy = CreateBlueNetworkScout(world, 2);
        var allyEnemy = CreateBlueNetworkScout(world, 3);
        var confusedPosition = FindVisibleSentryTargetPosition(world, sentry, confusedEnemy, preferredDistance: 88f, minimumDistance: 56f);
        confusedEnemy.TeleportTo(confusedPosition.X, confusedPosition.Y);
        allyEnemy.TeleportTo(confusedPosition.X + 48f, confusedPosition.Y);

        for (var tick = 0; tick < 180 && !confusedEnemy.ExperimentalConfusedAttackTargetPlayerId.HasValue; tick += 1)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(allyEnemy.Id, confusedEnemy.ExperimentalConfusedAttackTargetPlayerId);
        _ = InvokeApplyPlayerDamage(world, allyEnemy, 5, confusedEnemy);
        Assert.True(confusedEnemy.IsExperimentalConfusionRetaliationMarked);
        Assert.True(SimulationWorld.ShouldTreatPlayerAsExperimentalFriendlyFireTarget(allyEnemy, confusedEnemy));
    }

    [Fact]
    public void BulletResistanceOnlyReducesBulletDamage()
    {
        var world = CreateSoldierWorld(new ExperimentalGameplaySettings(PassiveBulletResistance: 0.5f));
        var player = world.LocalPlayer;
        player.ForceSetHealth(player.MaxHealth);
        var enemy = world.EnemyPlayer;
        enemy.ForceSetHealth(enemy.MaxHealth);

        _ = InvokeApplyPlayerDamage(world, player, 20, enemy);
        var bulletHealthLoss = player.MaxHealth - player.Health;

        player.ForceSetHealth(player.MaxHealth);
        InvokeSpawnRocket(world, enemy, player.X - 24f, player.Y, 0f);
        AdvanceTicks(world, 10);
        var explosiveHealthLoss = player.MaxHealth - player.Health;

        Assert.True(bulletHealthLoss < explosiveHealthLoss);
    }

    [Fact]
    public void AcquiredWeaponDamageMultiplierOnlyAppliesWhileWeaponIsEquipped()
    {
        var world = CreateSoldierWorld(new ExperimentalGameplaySettings(AcquiredWeaponDamageMultiplier: 2f));
        var player = world.LocalPlayer;
        player.SetAcquiredWeapon(PlayerClass.Medic);
        var target = world.EnemyPlayer;
        target.ForceSetHealth(target.MaxHealth);

        _ = InvokeApplyPlayerDamage(world, target, 10, player);
        var holsteredDamage = target.MaxHealth - target.Health;

        target.ForceSetHealth(target.MaxHealth);
        player.EquipAcquiredWeapon();
        _ = InvokeApplyPlayerDamage(world, target, 10, player);
        var equippedDamage = target.MaxHealth - target.Health;

        Assert.Equal(10, holsteredDamage);
        Assert.Equal(20, equippedDamage);
    }

    [Fact]
    public void JumpHeightAndBonusJumpsApplyThroughExperimentalSettings()
    {
        var world = CreateSoldierWorld(new ExperimentalGameplaySettings(
            PassiveJumpHeightMultiplier: 1.5f,
            PassiveBonusAirJumps: 2));

        Assert.Equal(
            CharacterClassCatalog.Soldier.JumpSpeed * 1.5f,
            world.LocalPlayer.JumpSpeed);
        Assert.Equal(CharacterClassCatalog.Soldier.MaxAirJumps + 2, world.LocalPlayer.MaxAirJumps);
    }

    [Fact]
    public void PassiveEvasionChanceCanEvadeIncomingDamage()
    {
        var world = CreateSoldierWorld(new ExperimentalGameplaySettings(PassiveEvasionChance: 0.75f));
        var evadedHits = 0;
        for (var attempt = 0; attempt < 25; attempt += 1)
        {
            world.LocalPlayer.ForceSetHealth(world.LocalPlayer.MaxHealth);
            _ = InvokeApplyPlayerDamage(world, world.LocalPlayer, 1, world.EnemyPlayer);
            var events = world.DrainPendingDamageEvents();
            if (events.Any(static damageEvent => damageEvent.Flags.HasFlag(DamageEventFlags.Evaded)))
            {
                evadedHits += 1;
            }
        }

        Assert.True(evadedHits > 0);
    }

    private static SimulationWorld CreateSoldierWorld(ExperimentalGameplaySettings settings)
    {
        var world = new SimulationWorld();
        Assert.True(world.TrySetLocalClass(PlayerClass.Soldier));
        world.ConfigureExperimentalGameplaySettings(settings);
        world.ForceRespawnLocalPlayer();
        return world;
    }

    private static SimulationWorld CreateJoinedSoldierWorld(ExperimentalGameplaySettings settings)
    {
        var world = new SimulationWorld();
        Assert.True(world.TryLoadLevel("Harvest"));
        world.PrepareLocalPlayerJoin();
        world.SetLocalPlayerTeam(PlayerTeam.Red);
        world.CompleteLocalPlayerJoin(PlayerClass.Soldier);
        world.ConfigureExperimentalGameplaySettings(settings);
        return world;
    }

    private static SimulationWorld CreateJoinedHeavyWorld(ExperimentalGameplaySettings settings)
    {
        var world = new SimulationWorld();
        Assert.True(world.TryLoadLevel("Harvest"));
        world.PrepareLocalPlayerJoin();
        world.SetLocalPlayerTeam(PlayerTeam.Red);
        world.CompleteLocalPlayerJoin(PlayerClass.Heavy);
        world.ConfigureExperimentalGameplaySettings(settings);
        return world;
    }

    private static SimulationWorld CreateJoinedPyroWorld(ExperimentalGameplaySettings settings)
    {
        var world = new SimulationWorld();
        Assert.True(world.TryLoadLevel("Harvest"));
        world.PrepareLocalPlayerJoin();
        world.SetLocalPlayerTeam(PlayerTeam.Red);
        world.CompleteLocalPlayerJoin(PlayerClass.Pyro);
        world.ConfigureExperimentalGameplaySettings(settings);
        return world;
    }

    private static SimulationWorld CreateJoinedDemomanWorld(ExperimentalGameplaySettings settings)
    {
        var world = new SimulationWorld();
        Assert.True(world.TryLoadLevel("Harvest"));
        world.PrepareLocalPlayerJoin();
        world.SetLocalPlayerTeam(PlayerTeam.Red);
        world.CompleteLocalPlayerJoin(PlayerClass.Demoman);
        world.ConfigureExperimentalGameplaySettings(settings);
        return world;
    }

    private static SimulationWorld CreateJoinedSniperWorld(ExperimentalGameplaySettings settings)
    {
        var world = new SimulationWorld();
        Assert.True(world.TryLoadLevel("Harvest"));
        world.PrepareLocalPlayerJoin();
        world.SetLocalPlayerTeam(PlayerTeam.Red);
        world.CompleteLocalPlayerJoin(PlayerClass.Sniper);
        world.ConfigureExperimentalGameplaySettings(settings);
        return world;
    }

    private static SimulationWorld CreateJoinedSpyWorld(ExperimentalGameplaySettings settings)
    {
        var world = new SimulationWorld();
        Assert.True(world.TryLoadLevel("Harvest"));
        world.PrepareLocalPlayerJoin();
        world.SetLocalPlayerTeam(PlayerTeam.Red);
        world.CompleteLocalPlayerJoin(PlayerClass.Spy);
        world.ConfigureExperimentalGameplaySettings(settings);
        return world;
    }

    private static SimulationWorld CreateJoinedScoutWorld(ExperimentalGameplaySettings settings)
    {
        var world = new SimulationWorld();
        Assert.True(world.TryLoadLevel("Harvest"));
        world.PrepareLocalPlayerJoin();
        world.SetLocalPlayerTeam(PlayerTeam.Red);
        world.CompleteLocalPlayerJoin(PlayerClass.Scout);
        world.ConfigureExperimentalGameplaySettings(settings);
        return world;
    }

    private static SimulationWorld CreateJoinedMedicWorld(ExperimentalGameplaySettings settings)
    {
        var world = new SimulationWorld();
        Assert.True(world.TryLoadLevel("Harvest"));
        world.PrepareLocalPlayerJoin();
        world.SetLocalPlayerTeam(PlayerTeam.Red);
        world.CompleteLocalPlayerJoin(PlayerClass.Medic);
        world.ConfigureExperimentalGameplaySettings(settings);
        return world;
    }

    private static SimulationWorld CreateJoinedEngineerWorld(ExperimentalGameplaySettings settings)
    {
        var world = new SimulationWorld();
        Assert.True(world.TryLoadLevel("Harvest"));
        world.PrepareLocalPlayerJoin();
        world.SetLocalPlayerTeam(PlayerTeam.Red);
        world.CompleteLocalPlayerJoin(PlayerClass.Engineer);
        world.ConfigureExperimentalGameplaySettings(settings);
        return world;
    }

    private static SimulationWorld CreatePrototypeEngineerWorld(ExperimentalGameplaySettings settings)
    {
        var world = new SimulationWorld();
        Assert.True(world.TrySetLocalClass(PlayerClass.Engineer));
        world.ConfigureExperimentalGameplaySettings(settings);
        world.ForceRespawnLocalPlayer();
        if (world.LocalPlayer.IsInSpawnRoom)
        {
            var startX = world.LocalPlayer.X;
            for (var offset = 64f; offset <= 512f && world.LocalPlayer.IsInSpawnRoom; offset += 64f)
            {
                world.TeleportLocalPlayer(startX + offset, world.LocalPlayer.Y);
                world.AdvanceOneTick();
            }
        }

        Assert.False(world.LocalPlayer.IsInSpawnRoom);
        return world;
    }

    private static SentryEntity BuildLocalSentry(SimulationWorld world)
    {
        _ = world.TryMoveLocalPlayerToControlPointSpawn();
        Assert.True(world.TryBuildLocalSentry());
        return Assert.Single(world.Sentries);
    }

    private static SentryEntity BuildAndCompleteLocalSentry(SimulationWorld world)
    {
        var sentry = BuildLocalSentry(world);
        for (var tick = 0; tick < 2000 && !sentry.IsBuilt; tick += 1)
        {
            world.AdvanceOneTick();
        }

        Assert.True(sentry.IsBuilt);
        Assert.Equal(sentry.MaxHealth, sentry.Health);
        return sentry;
    }

    private static PlayerEntity CreateBlueNetworkScout(SimulationWorld world, byte slot)
    {
        Assert.True(world.TryPrepareNetworkPlayerJoin(slot));
        Assert.True(world.TrySetNetworkPlayerTeam(slot, PlayerTeam.Blue));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(slot, PlayerClass.Scout));
        Assert.True(world.TryGetNetworkPlayer(slot, out var player));
        return player;
    }

    private static PlayerEntity CreateNetworkSoldier(SimulationWorld world, byte slot)
    {
        Assert.True(world.TryPrepareNetworkPlayerJoin(slot));
        Assert.True(world.TrySetNetworkPlayerTeam(slot, PlayerTeam.Red));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(slot, PlayerClass.Soldier));
        Assert.True(world.TryGetNetworkPlayer(slot, out var player));
        return player;
    }

    private static void PressJump(SimulationWorld world)
    {
        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: true,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: false,
            AimWorldX: world.LocalPlayer.X + 96f,
            AimWorldY: world.LocalPlayer.Y,
            DebugKill: false));
        world.AdvanceOneTick();
    }

    private static void PressSwapWeaponSpace(SimulationWorld world)
    {
        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: false,
            AimWorldX: world.LocalPlayer.X + 96f,
            AimWorldY: world.LocalPlayer.Y,
            DebugKill: false,
            UseAbility: true,
            SwapWeapon: true));
        world.AdvanceOneTick();
    }

    private static void PressUseAbilitySpace(SimulationWorld world)
    {
        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: false,
            AimWorldX: world.LocalPlayer.X + 96f,
            AimWorldY: world.LocalPlayer.Y,
            DebugKill: false,
            UseAbility: true,
            SwapWeapon: false));
        world.AdvanceOneTick();
    }

    private static void PressFireSecondaryAndSwapWeapon(SimulationWorld world)
    {
        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: true,
            AimWorldX: world.LocalPlayer.X + 96f,
            AimWorldY: world.LocalPlayer.Y,
            DebugKill: false,
            UseAbility: false,
            SwapWeapon: true));
        world.AdvanceOneTick();
    }

    private static void PressFireSecondary(SimulationWorld world)
    {
        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: true,
            AimWorldX: world.LocalPlayer.X + 96f,
            AimWorldY: world.LocalPlayer.Y,
            DebugKill: false,
            UseAbility: false,
            SwapWeapon: false));
        world.AdvanceOneTick();
    }

    private static void FirePrimaryOnce(SimulationWorld world)
    {
        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: true,
            FireSecondary: false,
            AimWorldX: world.LocalPlayer.X + 96f,
            AimWorldY: world.LocalPlayer.Y,
            DebugKill: false,
            UseAbility: false,
            SwapWeapon: false));
        world.AdvanceOneTick();
    }

    private static void AssertCoreSecondaryAbilityEvent(
        SimulationWorld world,
        string expectedItemId,
        string expectedExecutorId)
    {
        var abilityEvent = Assert.Single(world.DrainPendingGameplayAbilityEvents());
        Assert.True(abilityEvent.Handled);
        Assert.True(abilityEvent.ConsumedInput);
        Assert.False(abilityEvent.Cancelled);
        Assert.Equal(expectedItemId, abilityEvent.ItemId);
        Assert.Equal(GameplayAbilityConstants.SecondaryCategory, abilityEvent.AbilityCategory);
        Assert.Equal(expectedExecutorId, abilityEvent.ExecutorId);
        Assert.Contains(GameplayAbilityConstants.CoreSecondaryInputTag, abilityEvent.Tags);
    }

    private static void PressNetworkSwapWeaponSpace(SimulationWorld world, byte slot, PlayerEntity player)
    {
        Assert.True(world.TrySetNetworkPlayerInput(
            slot,
            new PlayerInputSnapshot(
                Left: false,
                Right: false,
                Up: false,
                Down: false,
                BuildSentry: false,
                DestroySentry: false,
                Taunt: false,
                FirePrimary: false,
                FireSecondary: false,
                AimWorldX: player.X + 96f,
                AimWorldY: player.Y,
                DebugKill: false,
                UseAbility: true,
                SwapWeapon: true)));
        world.AdvanceOneTick();
    }

    private static void ReleaseNetworkInput(SimulationWorld world, byte slot)
    {
        Assert.True(world.TrySetNetworkPlayerInput(slot, default));
        world.AdvanceOneTick();
    }

    private static void ReleaseAllInput(SimulationWorld world)
    {
        world.SetLocalInput(default);
        world.AdvanceOneTick();
    }

    private static void ApplyPrimarySoldierSnapshot(PlayerEntity player)
    {
        player.ApplyNetworkState(
            team: PlayerTeam.Red,
            classDefinition: CharacterClassCatalog.Soldier,
            isAlive: true,
            x: player.X,
            y: player.Y,
            horizontalSpeed: 0f,
            verticalSpeed: 0f,
            health: player.MaxHealth,
            currentShells: player.PrimaryWeapon.MaxAmmo,
            kills: 0,
            deaths: 0,
            caps: 0,
            points: 0f,
            healPoints: 0,
            activeDominationCount: 0,
            isDominatingLocalViewer: false,
            isDominatedByLocalViewer: false,
            metal: player.MaxMetal,
            isGrounded: true,
            remainingAirJumps: player.MaxAirJumps,
            isCarryingIntel: false,
            intelRechargeTicks: 0f,
            isSpyCloaked: false,
            spyCloakAlpha: 0f,
            isSpySuperjumping: false,
            spySuperjumpHorizontalVelocity: 0f,
            spySuperjumpCooldownTicksRemaining: 0,
            spyBackstabVisualTicksRemaining: 0,
            isUbered: false,
            isKritzCritBoosted: false,
            isHeavyEating: false,
            heavyEatTicksRemaining: 0,
            isSniperScoped: false,
            sniperChargeTicks: 0,
            isUsingBinoculars: false,
            binocularsFocusX: 0f,
            binocularsFocusY: 0f,
            facingDirectionX: player.FacingDirectionX,
            aimDirectionDegrees: player.AimDirectionDegrees,
            aimWorldX: player.X + 96f,
            aimWorldY: player.Y,
            isTaunting: false,
            tauntFrameIndex: 0f,
            isChatBubbleVisible: false,
            chatBubbleFrameIndex: 0,
            chatBubbleAlpha: 0f,
            gameplayEquippedSlot: (byte)GameplayEquipmentSlot.Primary);
    }

    private static void AdvanceTicks(SimulationWorld world, int ticks)
    {
        for (var tick = 0; tick < ticks; tick += 1)
        {
            world.AdvanceOneTick();
        }
    }

    private static int GetHeavyGhostDashDurationTicks(SimulationWorld world)
    {
        return Math.Max(1, (int)MathF.Round(world.Config.TicksPerSecond * ExperimentalGameplaySettings.HeavyGhostDashDurationSeconds));
    }

    private static int GetHeavyGhostDashMovementDurationTicks(SimulationWorld world)
    {
        var ability = CharacterClassCatalog.RuntimeRegistry.GetRequiredItem(StockGameplayModCatalog.HeavyUtilityItemId).Ability!;
        return GameplayAbilityParameterReader.GetTicks(
            ability,
            "movementDurationTicks",
            "movementDurationSeconds",
            GetHeavyGhostDashDurationTicks(world),
            world.Config.TicksPerSecond);
    }

    private static int GetHeavyGhostDashCooldownTicks(SimulationWorld world)
    {
        return Math.Max(1, (int)MathF.Round(world.Config.TicksPerSecond * ExperimentalGameplaySettings.HeavyGhostDashCooldownSeconds));
    }

    private static void AdvanceUntilJumpPadLanded(SimulationWorld world, JumpPadEntity pad)
    {
        for (var tick = 0; tick < 180 && !pad.HasLanded; tick += 1)
        {
            world.AdvanceOneTick();
        }

        Assert.True(pad.HasLanded);
    }

    private static (float X, float Y, float Distance) FindVisibleSentryTargetPosition(
        SimulationWorld world,
        SentryEntity sentry,
        PlayerEntity enemy,
        float preferredDistance,
        float minimumDistance)
    {
        (float X, float Y, float Distance)? bestCandidate = null;
        var bestDistanceDelta = float.MaxValue;
        for (var horizontalOffset = -320f; horizontalOffset <= 320f; horizontalOffset += 16f)
        {
            for (var verticalOffset = -192f; verticalOffset <= 160f; verticalOffset += 16f)
            {
                enemy.TeleportTo(sentry.X + horizontalOffset, sentry.Y + verticalOffset);
                if (!InvokePlayerCanOccupy(enemy, world.Level, enemy.Team, enemy.X, enemy.Y))
                {
                    continue;
                }

                if (!InvokeHasSentryLineOfSight(world, sentry, enemy))
                {
                    continue;
                }

                var distance = MathF.Sqrt(((enemy.X - sentry.X) * (enemy.X - sentry.X)) + ((enemy.Y - sentry.Y) * (enemy.Y - sentry.Y)));
                if (distance < minimumDistance)
                {
                    continue;
                }

                var distanceDelta = MathF.Abs(distance - preferredDistance);
                if (distanceDelta >= bestDistanceDelta)
                {
                    continue;
                }

                bestCandidate = (enemy.X, enemy.Y, distance);
                bestDistanceDelta = distanceDelta;
            }
        }

        return bestCandidate
            ?? throw new Xunit.Sdk.XunitException("Could not place enemy in visible sentry lane.");
    }

    private static void InvokeEngineerPda(SimulationWorld world)
    {
        TryHandleNetworkSecondaryAbilityMethod.Invoke(
            world,
            [
                world.LocalPlayer,
                default(PlayerInputSnapshot),
                default(PlayerInputSnapshot),
                GameplayAbilityInputPhase.Pressed,
                world.LocalPlayer.X,
                world.LocalPlayer.Y,
            ]);
    }

    private static bool InvokeTryHandleExperimentalRageActivation(SimulationWorld world, PlayerEntity player)
    {
        return (bool)TryHandleExperimentalRageActivationMethod.Invoke(world, [player])!;
    }

    private static bool InvokeApplyPlayerDamage(SimulationWorld world, PlayerEntity target, int damage, PlayerEntity attacker)
    {
        return (bool)ApplyPlayerDamageMethod.Invoke(
            world,
            [target, damage, attacker, PlayerEntity.SpyDamageRevealAlpha, DamageEventFlags.None, true])!;
    }

    private static bool InvokeApplySentryDamage(SimulationWorld world, SentryEntity target, int damage, PlayerEntity attacker)
    {
        return (bool)ApplySentryDamageMethod.Invoke(world, [target, damage, attacker])!;
    }

    private static void InvokeUpdateExperimentalEngineerEssenceExtractor(SimulationWorld world, PlayerEntity engineer, float aimWorldX, float aimWorldY)
    {
        UpdateExperimentalEngineerEssenceExtractorMethod.Invoke(world, [engineer, aimWorldX, aimWorldY]);
    }

    private static void InvokeUpdateExperimentalEngineerFreezeRay(SimulationWorld world, PlayerEntity engineer, float aimWorldX, float aimWorldY)
    {
        UpdateExperimentalEngineerFreezeRayMethod.Invoke(world, [engineer, aimWorldX, aimWorldY]);
    }

    private static void InvokeApplyExperimentalSentryPlayerHit(SimulationWorld world, SentryEntity sentry, PlayerEntity owner, PlayerEntity target, int baseDamage)
    {
        ApplyExperimentalSentryPlayerHitMethod.Invoke(world, [sentry, owner, target, baseDamage]);
    }

    private static void InvokeSpawnRocket(
        SimulationWorld world,
        PlayerEntity owner,
        float x,
        float y,
        float directionRadians,
        bool enableExperimentalCaveatTracking = false,
        float visualScale = 1f,
        int trackingLockTicksRemaining = 0)
    {
        SpawnRocketMethod.Invoke(
            world,
            [
                owner,
                x,
                y,
                4.5f,
                directionRadians,
                null,
                0f,
                false,
                false,
                1f,
                false,
                false,
                enableExperimentalCaveatTracking,
                visualScale,
                trackingLockTicksRemaining,
                null,
            ]);
    }

    private static void InvokeSpawnShot(
        SimulationWorld world,
        PlayerEntity owner,
        float x,
        float y,
        float velocityX,
        float velocityY,
        float damagePerHit,
        float playerKnockbackScale,
        float? playerSlowMovementMultiplier,
        int playerSlowRefreshTicks)
    {
        SpawnShotMethod.Invoke(
            world,
            [
                owner,
                x,
                y,
                velocityX,
                velocityY,
                damagePerHit,
                false,
                null,
                null,
                false,
                playerKnockbackScale,
                playerSlowMovementMultiplier,
                playerSlowRefreshTicks,
            ]);
    }

    private static void InvokeFirePrimaryWeapon(SimulationWorld world, PlayerEntity attacker, float aimWorldX, float aimWorldY)
    {
        FirePrimaryWeaponMethod.Invoke(world, [attacker, aimWorldX, aimWorldY]);
    }

    private static bool InvokeTryHandleExperimentalEngineerAlternateWeaponInteraction(SimulationWorld world, PlayerEntity player)
    {
        return (bool)(TryHandleExperimentalEngineerAlternateWeaponInteractionMethod.Invoke(world, [player]) ?? false);
    }

    private static bool InvokeTryResolveExperimentalEngineerRocketTrackingDirection(
        SimulationWorld world,
        RocketProjectileEntity rocket,
        PlayerEntity owner,
        out float targetDirectionRadians)
    {
        var arguments = new object[] { rocket, owner, 0f };
        var resolved = (bool)TryResolveExperimentalEngineerRocketTrackingDirectionMethod.Invoke(world, arguments)!;
        targetDirectionRadians = (float)arguments[2];
        return resolved;
    }

    private static int InvokeGetExperimentalSentryReloadTicks(SimulationWorld world, PlayerEntity owner, SentryEntity sentry)
    {
        return (int)GetExperimentalSentryReloadTicksMethod.Invoke(world, [owner, sentry])!;
    }

    private static int InvokeGetExperimentalSentryIdleResetTicks(SimulationWorld world)
    {
        return (int)GetExperimentalSentryIdleResetTicksMethod.Invoke(world, null)!;
    }

    private static float InvokeGetExperimentalSentryTargetRange(SimulationWorld world, PlayerEntity owner)
    {
        return (float)GetExperimentalSentryTargetRangeMethod.Invoke(world, [owner])!;
    }

    private static bool InvokeHasSentryLineOfSight(SimulationWorld world, SentryEntity sentry, PlayerEntity target)
    {
        return (bool)HasSentryLineOfSightMethod.Invoke(world, [sentry, target])!;
    }

    private static bool InvokePlayerCanOccupy(PlayerEntity player, SimpleLevel level, PlayerTeam team, float x, float y)
    {
        return (bool)PlayerCanOccupyMethod.Invoke(player, [level, team, x, y])!;
    }

    private static float InvokeGetExperimentalMovementSpeedMultiplier(PlayerEntity player)
    {
        return (float)GetExperimentalMovementSpeedMultiplierMethod.Invoke(player, null)!;
    }

    private static void SetPlayerAimDirection(PlayerEntity player, float aimDirectionDegrees)
    {
        AimDirectionDegreesBackingField.SetValue(player, aimDirectionDegrees);
    }

    private static MethodInfo GetRequiredNonPublicMethod(string methodName)
    {
        var method = typeof(SimulationWorld).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (method is not null)
        {
            return method;
        }

        throw new InvalidOperationException($"Could not find SimulationWorld.{methodName}.");
    }

    private static MethodInfo GetRequiredNonPublicPlayerMethod(string methodName)
    {
        var method = typeof(PlayerEntity).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (method is not null)
        {
            return method;
        }

        throw new InvalidOperationException($"Could not find PlayerEntity.{methodName}.");
    }
}
