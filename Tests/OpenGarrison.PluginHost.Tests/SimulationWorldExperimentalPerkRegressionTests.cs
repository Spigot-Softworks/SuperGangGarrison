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
    private static readonly MethodInfo CombatTestSetLevelMethod = GetRequiredNonPublicMethod("CombatTestSetLevel");
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
            [player, 12f, player, PlayerEntity.SpyDamageRevealAlpha, DamageEventFlags.None, true, true, null, null, null])!;

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
    public void StockEngineerFreshSentryIgnoresStrayImmediateRightClickDestroy()
    {
        var world = CreateJoinedEngineerWorld(new ExperimentalGameplaySettings());

        Assert.True(world.TryMoveLocalPlayerToControlPointSpawn());
        PressFireSecondary(world);
        var sentry = Assert.Single(world.Sentries);
        Assert.False(sentry.IsBuilt);

        ReleaseAllInput(world);
        PressFireSecondary(world);

        Assert.Same(sentry, Assert.Single(world.Sentries));

        ReleaseAllInput(world);
        AdvanceTicks(world, world.Config.TicksPerSecond * 2);
        PressFireSecondary(world);

        Assert.Empty(world.Sentries);
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
    public void StockPyroAirblastPushesTeammates()
    {
        var world = CreateJoinedPyroWorld(new ExperimentalGameplaySettings());
        AdvanceTicks(world, 1);
        Assert.True(world.TryMoveLocalPlayerToControlPointSpawn());
        var teammate = CreateNetworkSoldier(world, 2);
        teammate.TeleportTo(world.LocalPlayer.X + 64f, world.LocalPlayer.Y);
        teammate.SetSpawnRoomState(false);
        teammate.ApplyVelocityImpulse(0f, 0f);

        PressFireSecondary(world);

        Assert.True(
            teammate.HorizontalSpeed > 0f,
            $"expected teammate to be pushed forward; speed={teammate.HorizontalSpeed:0.###}");
        Assert.True(
            teammate.VerticalSpeed < 0f,
            $"expected teammate to receive airblast lift; speed={teammate.VerticalSpeed:0.###}");
    }

    [Fact]
    public void PyroUtilityAirburstCarriesTeammatesWithPyroVelocity()
    {
        var world = CreateJoinedPyroWorld(new ExperimentalGameplaySettings());
        AdvanceTicks(world, 1);
        Assert.True(world.TryMoveLocalPlayerToControlPointSpawn());
        var teammate = CreateNetworkSoldier(world, 2);
        teammate.TeleportTo(world.LocalPlayer.X + 64f, world.LocalPlayer.Y);
        teammate.SetSpawnRoomState(false);
        teammate.ApplyVelocityImpulse(0f, 0f);
        var fuelBefore = world.LocalPlayer.PyroPrimaryFuelScaled;
        var primaryCooldownBefore = world.LocalPlayer.PrimaryCooldownTicks;

        PressUseAbilitySpace(world, world.LocalPlayer.X + 96f, world.LocalPlayer.Y + 32f);

        Assert.True(
            world.LocalPlayer.HorizontalSpeed < 0f,
            $"expected pyro to airburst backward; speed={world.LocalPlayer.HorizontalSpeed:0.###}");
        Assert.True(
            world.LocalPlayer.VerticalSpeed < 0f,
            $"expected pyro to airburst upward; speed={world.LocalPlayer.VerticalSpeed:0.###}");
        Assert.True(
            MathF.Abs(teammate.HorizontalSpeed - world.LocalPlayer.HorizontalSpeed) < 0.001f,
            $"expected teammate to inherit pyro hspeed; pyro={world.LocalPlayer.HorizontalSpeed:0.###} teammate={teammate.HorizontalSpeed:0.###}");
        Assert.True(
            MathF.Abs(teammate.VerticalSpeed - world.LocalPlayer.VerticalSpeed) < 2.5f,
            $"expected teammate to inherit pyro vspeed; pyro={world.LocalPlayer.VerticalSpeed:0.###} teammate={teammate.VerticalSpeed:0.###}");
        Assert.Equal(fuelBefore - (PlayerEntity.PyroAirburstCost * PlayerEntity.PyroPrimaryFuelScale), world.LocalPlayer.PyroPrimaryFuelScaled);
        Assert.Equal(Math.Max(0, primaryCooldownBefore - 1), world.LocalPlayer.PrimaryCooldownTicks);
    }

    [Fact]
    public void PyroUtilityAirburstDoesNotSuppressFlamethrowerPrimary()
    {
        var world = CreateJoinedPyroWorld(new ExperimentalGameplaySettings());
        AdvanceTicks(world, 1);
        Assert.True(world.TryMoveLocalPlayerToControlPointSpawn());

        PressUseAbilitySpaceAndFirePrimary(world, world.LocalPlayer.X + 96f, world.LocalPlayer.Y);

        Assert.NotEmpty(world.Flames);
        Assert.True(world.LocalPlayer.PrimaryCooldownTicks > 0);
        Assert.True(world.LocalPlayer.PyroAirblastCooldownTicks > 0);
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
    public void StockDemomanRightClickDetonatesWhileGrenadeLauncherIsEquipped()
    {
        var world = CreateJoinedDemomanWorld(new ExperimentalGameplaySettings());
        AdvanceTicks(world, 1);
        FirePrimaryOnce(world);
        Assert.NotEmpty(world.Mines);

        ReleaseAllInput(world);
        PressSwapWeaponSpace(world);
        Assert.True(world.LocalPlayer.HasEquippedBehavior(BuiltInGameplayBehaviorIds.GrenadeLauncher));

        ReleaseAllInput(world);
        PressFireSecondary(world);

        Assert.Empty(world.Mines);
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
        Assert.False(world.LocalPlayer.IsMedicHealing);
        AssertCoreSecondaryAbilityEvent(world, "weapon.medigun", BuiltInGameplayBehaviorIds.MedicNeedlegun);
    }

    [Fact]
    public void StockMedicRightClickFiresNeedlesAfterHealingTarget()
    {
        var world = CreateJoinedMedicWorld(new ExperimentalGameplaySettings());
        AdvanceTicks(world, 1);
        var teammate = CreateRedNetworkScout(world, 2);
        SetOpenCombatLevel(world);
        world.TeleportLocalPlayer(100f, 100f);
        world.LocalPlayer.SetSpawnRoomState(false);
        teammate.TeleportTo(world.LocalPlayer.X + 96f, world.LocalPlayer.Y);
        teammate.SetSpawnRoomState(false);

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
            AimWorldX: teammate.X,
            AimWorldY: teammate.Y,
            DebugKill: false,
            UseAbility: false,
            SwapWeapon: false));
        world.AdvanceOneTick();
        Assert.True(world.LocalPlayer.IsMedicHealing);

        PressFireSecondary(world);

        Assert.NotEmpty(world.Needles);
        Assert.False(world.LocalPlayer.IsMedicHealing);
        AssertCoreSecondaryAbilityEvent(world, "weapon.medigun", BuiltInGameplayBehaviorIds.MedicNeedlegun);
    }

    [Fact]
    public void StockMedicKritzHealNeedlesDealReducedDpsToEnemies()
    {
        var world = CreateJoinedMedicWorld(new ExperimentalGameplaySettings());
        AdvanceTicks(world, 1);
        Assert.True(world.TryMoveLocalPlayerToControlPointSpawn());
        var enemy = CreateBlueNetworkScout(world, 2);
        MoveKritzBeamTestPlayersToOpenCombatLevel(world, enemy);
        var healthBefore = enemy.Health;
        PressSwapWeaponSpace(world);

        var magazineCycleTicks = (18 * 5) + 45;
        for (var tick = 0; tick < magazineCycleTicks; tick += 1)
        {
            PressFireSecondary(world);
        }

        AdvanceTicks(world, 40);

        var damageDealt = healthBefore - enemy.Health;
        var expectedDamagePerHit = MedicHealNeedleProjectileEntity.DefaultEnemyDamagePerHit;
        Assert.InRange(damageDealt, expectedDamagePerHit * 2, expectedDamagePerHit * 6);
        Assert.Equal(0, damageDealt % expectedDamagePerHit);
    }

    [Fact]
    public void StockMedicKritzHealNeedlesHealTeammatesAndChargeUber()
    {
        var world = CreateJoinedMedicWorld(new ExperimentalGameplaySettings());
        AdvanceTicks(world, 1);
        Assert.True(world.TryMoveLocalPlayerToControlPointSpawn());
        var teammate = CreateRedNetworkScout(world, 2);
        SetOpenCombatLevel(world);
        world.TeleportLocalPlayer(100f, 100f);
        world.LocalPlayer.SetSpawnRoomState(false);
        teammate.TeleportTo(world.LocalPlayer.X + 96f, world.LocalPlayer.Y);
        teammate.SetSpawnRoomState(false);
        teammate.ApplyContinuousDamage(50f);
        var healthBefore = teammate.Health;
        PressSwapWeaponSpace(world);

        PressFireSecondary(world);
        AdvanceTicks(world, 20);

        Assert.True(teammate.Health > healthBefore);
        Assert.True(world.LocalPlayer.MedicUberCharge > 0f);
    }

    [Fact]
    public void StockMedicKritzUberActivatesWhenHoldingPrimaryAndSecondary()
    {
        var world = CreateJoinedMedicWorld(new ExperimentalGameplaySettings());
        AdvanceTicks(world, 1);
        Assert.True(world.TryMoveLocalPlayerToControlPointSpawn());
        var teammate = CreateRedNetworkScout(world, 2);
        SetOpenCombatLevel(world);
        world.TeleportLocalPlayer(100f, 100f);
        world.LocalPlayer.SetSpawnRoomState(false);
        teammate.TeleportTo(world.LocalPlayer.X + 96f, world.LocalPlayer.Y);
        teammate.SetSpawnRoomState(false);
        world.LocalPlayer.FillMedicUberCharge();
        PressSwapWeaponSpace(world);

        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: true,
            FireSecondary: true,
            AimWorldX: teammate.X,
            AimWorldY: teammate.Y,
            DebugKill: false,
            UseAbility: false,
            SwapWeapon: false));
        world.AdvanceOneTick();

        Assert.True(world.LocalPlayer.IsMedicUbering);
        Assert.True(world.LocalPlayer.IsMedicHealing);
        Assert.Equal(teammate.Id, world.LocalPlayer.MedicHealTargetId);
        var abilityEvent = Assert.Single(world.DrainPendingGameplayAbilityEvents());
        Assert.True(abilityEvent.Handled);
        Assert.Equal("weapon.medigun.crit", abilityEvent.ItemId);
        Assert.Equal(BuiltInGameplayBehaviorIds.MedicKritzHealNeedles, abilityEvent.ExecutorId);
    }

    [Fact]
    public void StockMedicKritzHoldingPrimaryAndSecondaryPrioritizesHealingOverDamageBeam()
    {
        var world = CreateJoinedMedicWorld(new ExperimentalGameplaySettings());
        AdvanceTicks(world, 1);
        Assert.True(world.TryMoveLocalPlayerToControlPointSpawn());
        var teammate = CreateRedNetworkScout(world, 2);
        var enemy = CreateBlueNetworkScout(world, 3);
        SetOpenCombatLevel(world);
        world.TeleportLocalPlayer(100f, 100f);
        world.LocalPlayer.SetSpawnRoomState(false);
        teammate.TeleportTo(world.LocalPlayer.X + 96f, world.LocalPlayer.Y);
        teammate.SetSpawnRoomState(false);
        enemy.TeleportTo(world.LocalPlayer.X + 96f, world.LocalPlayer.Y);
        enemy.SetSpawnRoomState(false);
        var enemyHealthBefore = enemy.Health;
        PressSwapWeaponSpace(world);

        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: true,
            FireSecondary: true,
            AimWorldX: teammate.X,
            AimWorldY: teammate.Y,
            DebugKill: false,
            UseAbility: false,
            SwapWeapon: false));
        world.AdvanceOneTick();

        Assert.True(world.LocalPlayer.IsMedicHealing);
        Assert.Equal(teammate.Id, world.LocalPlayer.MedicHealTargetId);
        Assert.Equal(enemyHealthBefore, enemy.Health);
        Assert.DoesNotContain(world.PendingVisualEvents, static visualEvent => visualEvent.EffectName == "Blood");
    }

    [Fact]
    public void StockMedicKritzPrimaryWhileSecondaryHeldPrioritizesHealing()
    {
        var world = CreateJoinedMedicWorld(new ExperimentalGameplaySettings());
        AdvanceTicks(world, 1);
        Assert.True(world.TryMoveLocalPlayerToControlPointSpawn());
        var teammate = CreateRedNetworkScout(world, 2);
        var enemy = CreateBlueNetworkScout(world, 3);
        MoveKritzBeamTestPlayersToOpenCombatLevel(world, enemy);
        teammate.TeleportTo(world.LocalPlayer.X + 96f, world.LocalPlayer.Y);
        teammate.SetSpawnRoomState(false);
        PressSwapWeaponSpace(world);

        PressFireSecondary(world);
        AdvanceTicks(world, 20);
        Assert.NotEmpty(world.Needles);

        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: true,
            FireSecondary: true,
            AimWorldX: teammate.X,
            AimWorldY: teammate.Y,
            DebugKill: false,
            UseAbility: false,
            SwapWeapon: false));
        world.AdvanceOneTick();

        Assert.True(world.LocalPlayer.IsMedicHealing);
        Assert.Equal(teammate.Id, world.LocalPlayer.MedicHealTargetId);
    }

    [Fact]
    public void StockMedicKritzUberIsReadyAtSeventyFivePercentCharge()
    {
        var world = CreateJoinedMedicWorld(new ExperimentalGameplaySettings());
        AdvanceTicks(world, 1);
        PressSwapWeaponSpace(world);

        AddMedicUberChargeUntil(world.LocalPlayer, PlayerEntity.MedicKritzUberReadyChargeThreshold);

        Assert.True(world.LocalPlayer.IsMedicUberReady);
        Assert.True(world.LocalPlayer.MedicUberCharge < PlayerEntity.MedicUberMaxCharge);
    }

    [Fact]
    public void StockMedicKritzUberReadyStateUpdatesWhenSwappingToKritzkrieg()
    {
        var world = CreateJoinedMedicWorld(new ExperimentalGameplaySettings());
        AdvanceTicks(world, 1);

        AddMedicUberChargeUntil(world.LocalPlayer, 1600f);

        Assert.False(world.LocalPlayer.IsMedicUberReady);
        PressSwapWeaponSpace(world);
        Assert.True(world.LocalPlayer.IsMedicUberReady);
    }

    [Fact]
    public void StockMedicKritzUberAtFullChargeLastsEightSecondsAndConsumesAllCharge()
    {
        var world = CreateJoinedMedicWorld(new ExperimentalGameplaySettings());
        AdvanceTicks(world, 1);
        var durationTicks = (int)(PlayerEntity.MedicUberDurationSeconds * world.Config.TicksPerSecond);
        PressSwapWeaponSpace(world);
        world.LocalPlayer.FillMedicUberCharge();

        Assert.True(world.LocalPlayer.TryStartMedicUber());
        Assert.True(world.LocalPlayer.MedicUberUsesFixedDuration);
        Assert.Equal(PlayerEntity.MedicUberMaxCharge, world.LocalPlayer.MedicUberCommittedCharge);

        AdvanceTicks(world, durationTicks - 1);

        Assert.True(world.LocalPlayer.IsMedicUbering);
        Assert.True(world.LocalPlayer.MedicUberCharge > 0f);

        AdvanceTicks(world, 2);

        Assert.False(world.LocalPlayer.IsMedicUbering);
        Assert.Equal(0f, world.LocalPlayer.MedicUberCharge);
    }

    [Fact]
    public void StockMedicKritzUberHudMeterScalesChargeToSeventyFivePercentThreshold()
    {
        var world = CreateJoinedMedicWorld(new ExperimentalGameplaySettings());
        AdvanceTicks(world, 1);
        PressSwapWeaponSpace(world);
        AddMedicUberChargeUntil(world.LocalPlayer, 1000f);

        world.LocalPlayer.GetMedicUberHudMeter(out var meterValue, out var meterMax);

        Assert.InRange(meterValue, 1000f, 1002f);
        Assert.Equal(PlayerEntity.MedicKritzUberReadyChargeThreshold, meterMax);
    }

    [Fact]
    public void StockMedicKritzHealNeedlesDoNotRetainBeamTargets()
    {
        var world = CreateJoinedMedicWorld(new ExperimentalGameplaySettings());
        AdvanceTicks(world, 1);
        Assert.True(world.TryMoveLocalPlayerToControlPointSpawn());
        var enemy = CreateBlueNetworkScout(world, 2);
        MoveKritzBeamTestPlayersToOpenCombatLevel(world, enemy);
        PressSwapWeaponSpace(world);

        PressFireSecondary(world);
        AdvanceTicks(world, 5);

        Assert.Null(world.LocalPlayer.MedicHealTargetId);
        Assert.False(world.LocalPlayer.IsMedicHealing);
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
    public void SpyJumpInputCancelsSuperjumpChargeWithoutCooldown()
    {
        var world = CreateJoinedSpyWorld(new ExperimentalGameplaySettings());
        AdvanceTicks(world, 1);
        Assert.True(world.TryMoveLocalPlayerToControlPointSpawn());
        var startY = world.LocalPlayer.Y;

        PressUseAbilitySpace(world);
        Assert.True(world.LocalPlayer.SpySuperjumpChargeTicks > 0);

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
            DebugKill: false,
            UseAbility: true,
            SwapWeapon: false));
        world.AdvanceOneTick();

        Assert.Equal(0, world.LocalPlayer.SpySuperjumpChargeTicks);
        Assert.False(world.LocalPlayer.IsSpySuperjumping);
        Assert.Equal(0, world.LocalPlayer.SpySuperjumpCooldownTicksRemaining);
        Assert.True(world.LocalPlayer.Y >= startY);

        world.AdvanceOneTick();
        Assert.Equal(0, world.LocalPlayer.SpySuperjumpChargeTicks);
        Assert.Equal(0, world.LocalPlayer.SpySuperjumpCooldownTicksRemaining);

        ReleaseAllInput(world);
        PressUseAbilitySpace(world);
        Assert.True(world.LocalPlayer.SpySuperjumpChargeTicks > 0);
    }

    [Fact]
    public void HealingCabinetRefreshesSpecialAbilityCooldowns()
    {
        var world = CreateJoinedHeavyWorld(new ExperimentalGameplaySettings());
        AdvanceTicks(world, 1);
        Assert.True(world.TryMoveLocalPlayerToControlPointSpawn());

        PressUseAbilitySpace(world);
        Assert.True(world.LocalPlayer.ExperimentalGhostDashCooldownTicksRemaining > 0);

        world.LocalPlayer.ForceSetHealth(world.LocalPlayer.MaxHealth);
        world.LocalPlayer.ForceSetAmmo(world.LocalPlayer.MaxShells);
        world.SetLocalInput(default);
        var cabinet = world.Level.GetRoomObjects(RoomObjectType.HealingCabinet).First();
        world.TeleportLocalPlayer(cabinet.CenterX, cabinet.CenterY);

        world.AdvanceOneTick();

        Assert.True(world.LocalPlayer.IsUsingHealingCabinet);
        Assert.Equal(0, world.LocalPlayer.ExperimentalGhostDashCooldownTicksRemaining);
    }

    [Fact]
    public void HealingCabinetPreservesSelectedSecondaryWeapon()
    {
        var world = CreateJoinedSoldierWorld(new ExperimentalGameplaySettings(EnableSoldierShotgunSecondaryWeapon: true));
        Assert.True(world.TrySetNetworkPlayerGameplayEquippedSlot(SimulationWorld.LocalPlayerSlot, GameplayEquipmentSlot.Secondary));
        Assert.Equal(GameplayEquipmentSlot.Secondary, world.LocalPlayer.SelectedGameplayEquippedSlot);
        Assert.Equal(GameplayEquipmentSlot.Secondary, world.LocalPlayer.GameplayLoadoutState.EquippedSlot);

        world.LocalPlayer.ForceSetAmmo(0);
        world.SetLocalInput(default);
        var cabinet = world.Level.GetRoomObjects(RoomObjectType.HealingCabinet).First();
        world.TeleportLocalPlayer(cabinet.CenterX, cabinet.CenterY);

        world.AdvanceOneTick();

        Assert.True(world.LocalPlayer.IsUsingHealingCabinet);
        Assert.Equal(world.LocalPlayer.MaxShells, world.LocalPlayer.CurrentShells);
        Assert.Equal(GameplayEquipmentSlot.Secondary, world.LocalPlayer.SelectedGameplayEquippedSlot);
        Assert.Equal(GameplayEquipmentSlot.Secondary, world.LocalPlayer.GameplayLoadoutState.EquippedSlot);
        Assert.Equal(world.LocalPlayer.GameplayLoadoutState.SecondaryItemId, world.LocalPlayer.GameplayLoadoutState.EquippedItemId);
    }

    [Fact]
    public void HeavyGhostDashRegistersEvadedDamageEvent()
    {
        var world = CreateJoinedHeavyWorld(new ExperimentalGameplaySettings());
        AdvanceTicks(world, 1);
        Assert.True(world.TryMoveLocalPlayerToControlPointSpawn());
        var attacker = CreateBlueNetworkScout(world, 2);
        var healthBefore = world.LocalPlayer.Health;

        PressUseAbilitySpace(world);
        world.DrainPendingDamageEvents();
        Assert.True(world.LocalPlayer.IsExperimentalGhostDashing);

        Assert.False(InvokeApplyPlayerDamage(world, world.LocalPlayer, 25, attacker));
        Assert.Equal(healthBefore, world.LocalPlayer.Health);
        var damageEvent = Assert.Single(world.DrainPendingDamageEvents());
        Assert.Equal(0, damageEvent.Amount);
        Assert.Equal(world.LocalPlayer.Id, damageEvent.TargetEntityId);
        Assert.True(damageEvent.Flags.HasFlag(DamageEventFlags.Evaded));
        Assert.True(damageEvent.Flags.HasFlag(DamageEventFlags.GhostDash));
    }

    [Fact]
    public void CivilianUmbrellaBlockRegistersDedicatedEvadedDamageEvent()
    {
        var world = CreateJoinedCivilianWorld(new ExperimentalGameplaySettings());
        AdvanceTicks(world, 1);
        Assert.True(world.TryMoveLocalPlayerToControlPointSpawn());
        var attacker = CreateBlueNetworkScout(world, 2);
        var healthBefore = world.LocalPlayer.Health;

        SetPlayerAimDirection(world.LocalPlayer, 0f);
        attacker.TeleportTo(world.LocalPlayer.X + 96f, world.LocalPlayer.Y);
        Assert.True(world.LocalPlayer.TryActivateCivvieUmbrella());
        world.DrainPendingDamageEvents();

        Assert.False(InvokeApplyPlayerDamage(world, world.LocalPlayer, 25, attacker));
        Assert.Equal(healthBefore, world.LocalPlayer.Health);
        var damageEvent = Assert.Single(world.DrainPendingDamageEvents());
        Assert.Equal(0, damageEvent.Amount);
        Assert.Equal(world.LocalPlayer.Id, damageEvent.TargetEntityId);
        Assert.True(damageEvent.Flags.HasFlag(DamageEventFlags.Evaded));
        Assert.True(damageEvent.Flags.HasFlag(DamageEventFlags.CivvieUmbrellaBlock));
        Assert.False(damageEvent.Flags.HasFlag(DamageEventFlags.GhostDash));
        Assert.InRange(damageEvent.X, world.LocalPlayer.X + 25f, world.LocalPlayer.X + 36f);
        Assert.InRange(damageEvent.Y, world.LocalPlayer.Y - 9f, world.LocalPlayer.Y - 5f);
    }

    [Fact]
    public void SpyBackstabBypassesCivilianUmbrellaShieldFromBehind()
    {
        var world = CreateJoinedCivilianWorld(new ExperimentalGameplaySettings());
        AdvanceTicks(world, 1);
        Assert.True(world.TryMoveLocalPlayerToControlPointSpawn());
        var spy = CreateBlueNetworkSpy(world, 2);
        var civilian = world.LocalPlayer;
        var chargeBefore = civilian.CivvieUmbrellaChargeTicks;

        civilian.TeleportTo(0f, 0f);
        spy.TeleportTo(-24f, 0f);
        SetPlayerAimDirection(civilian, 0f);
        Assert.True(civilian.TryActivateCivvieUmbrella());
        Assert.True(spy.TryToggleSpyCloak());
        world.DrainPendingDamageEvents();

        InvokeSpawnStabMask(world, spy, 0f);
        InvokeAdvanceStabMasks(world);

        Assert.False(civilian.IsAlive);
        Assert.Equal(chargeBefore, civilian.CivvieUmbrellaChargeTicks);
        Assert.Empty(world.DrainPendingDamageEvents().Where(static damageEvent =>
            damageEvent.Flags.HasFlag(DamageEventFlags.CivvieUmbrellaBlock)));
    }

    [Fact]
    public void SpyBackstabBypassesCivilianUmbrellaShieldFromFront()
    {
        var world = CreateJoinedCivilianWorld(new ExperimentalGameplaySettings());
        AdvanceTicks(world, 1);
        Assert.True(world.TryMoveLocalPlayerToControlPointSpawn());
        var spy = CreateBlueNetworkSpy(world, 2);
        var civilian = world.LocalPlayer;

        civilian.TeleportTo(0f, 0f);
        spy.TeleportTo(24f, 0f);
        SetPlayerAimDirection(civilian, 0f);
        Assert.True(civilian.TryActivateCivvieUmbrella());
        Assert.True(spy.TryToggleSpyCloak());
        world.DrainPendingDamageEvents();

        Assert.False(InvokeApplyPlayerDamage(world, civilian, 25, spy));
        Assert.Equal(civilian.MaxHealth, civilian.Health);
        _ = world.DrainPendingDamageEvents();

        InvokeSpawnStabMask(world, spy, 180f);
        InvokeAdvanceStabMasks(world);

        Assert.False(civilian.IsAlive);
        Assert.Empty(world.DrainPendingDamageEvents().Where(static damageEvent =>
            damageEvent.Flags.HasFlag(DamageEventFlags.CivvieUmbrellaBlock)));
    }

    [Fact]
    public void CivilianUmbrellaShieldDoesNotBlockDamageFromBehind()
    {
        var world = CreateJoinedCivilianWorld(new ExperimentalGameplaySettings());
        AdvanceTicks(world, 1);
        Assert.True(world.TryMoveLocalPlayerToControlPointSpawn());
        var attacker = CreateBlueNetworkScout(world, 2);
        var healthBefore = world.LocalPlayer.Health;
        var chargeBefore = world.LocalPlayer.CivvieUmbrellaChargeTicks;

        SetPlayerAimDirection(world.LocalPlayer, 0f);
        attacker.TeleportTo(world.LocalPlayer.X - 96f, world.LocalPlayer.Y);
        Assert.True(world.LocalPlayer.TryActivateCivvieUmbrella());
        world.DrainPendingDamageEvents();

        _ = InvokeApplyPlayerDamage(world, world.LocalPlayer, 25, attacker);
        Assert.Equal(healthBefore - 25, world.LocalPlayer.Health);
        Assert.Equal(chargeBefore, world.LocalPlayer.CivvieUmbrellaChargeTicks);
        Assert.Empty(world.DrainPendingDamageEvents().Where(static damageEvent =>
            damageEvent.Flags.HasFlag(DamageEventFlags.CivvieUmbrellaBlock)));
    }

    [Fact]
    public void CivilianUmbrellaShieldExplodesRocketsOnHitAndAbsorbsBlastDamage()
    {
        var world = CreateJoinedCivilianWorld(new ExperimentalGameplaySettings());
        AdvanceTicks(world, 1);
        SetOpenCombatLevel(world);
        var civilian = world.LocalPlayer;
        var soldier = CreateBlueNetworkSoldier(world, 2);

        civilian.TeleportTo(20f, 0f);
        civilian.SetSpawnRoomState(false);
        soldier.TeleportTo(0f, 0f);
        soldier.SetSpawnRoomState(false);
        civilian.GetCollisionBounds(out _, out var civilianTop, out _, out _);
        SetPlayerAimDirection(civilian, 180f);
        Assert.True(civilian.TryActivateCivvieUmbrella());

        var healthBefore = civilian.Health;
        var chargeBefore = civilian.CivvieUmbrellaChargeTicks;
        _ = world.DrainPendingDamageEvents();
        _ = world.DrainPendingSoundEvents();
        _ = world.DrainPendingVisualEvents();

        _ = SpawnCombatTestRocket(world, soldier, soldier.X, civilianTop + 1f, speed: 12f, directionRadians: 0f);
        AdvanceCombatRockets(world);

        Assert.Equal(0, GetCombatRocketCount(world));
        Assert.Equal(healthBefore, civilian.Health);
        Assert.Equal(
            chargeBefore - PlayerEntity.CivvieUmbrellaDirectExplosionDrainTicks - PlayerEntity.CivvieUmbrellaRocketDirectHitSplashDrainTicks,
            civilian.CivvieUmbrellaChargeTicks);
        Assert.Contains(world.PendingSoundEvents, static soundEvent => soundEvent.SoundName == "ExplosionSnd");
        Assert.Contains(world.PendingVisualEvents, static visualEvent => visualEvent.EffectName == "Explosion");
        Assert.Contains(
            world.DrainPendingDamageEvents(),
            static damageEvent => damageEvent.Flags.HasFlag(DamageEventFlags.CivvieUmbrellaBlock));
    }

    [Fact]
    public void CivilianUmbrellaShieldExplodesGrenadesOnHitAndAbsorbsBlastDamage()
    {
        var world = CreateJoinedCivilianWorld(new ExperimentalGameplaySettings());
        AdvanceTicks(world, 1);
        SetOpenCombatLevel(world);
        var civilian = world.LocalPlayer;
        var demoman = CreateBlueNetworkDemoman(world, 2);

        civilian.TeleportTo(20f, 0f);
        civilian.SetSpawnRoomState(false);
        demoman.TeleportTo(-500f, 0f);
        demoman.SetSpawnRoomState(false);
        SetPlayerAimDirection(civilian, 180f);
        Assert.True(civilian.TryActivateCivvieUmbrella());

        var healthBefore = civilian.Health;
        var chargeBefore = civilian.CivvieUmbrellaChargeTicks;
        _ = world.DrainPendingDamageEvents();
        _ = world.DrainPendingSoundEvents();
        _ = world.DrainPendingVisualEvents();

        _ = SpawnCombatTestGrenade(world, demoman, x: 0f, y: 0f, velocityX: 40f, velocityY: 0f);
        AdvanceCombatGrenades(world);

        Assert.Equal(0, GetCombatGrenadeCount(world));
        Assert.Equal(healthBefore, civilian.Health);
        Assert.Equal(chargeBefore - PlayerEntity.CivvieUmbrellaDirectExplosionDrainTicks, civilian.CivvieUmbrellaChargeTicks);
        Assert.Contains(world.PendingSoundEvents, static soundEvent => soundEvent.SoundName == "ExplosionSnd");
        Assert.Contains(world.PendingVisualEvents, static visualEvent => visualEvent.EffectName == "Explosion");
        Assert.Contains(
            world.DrainPendingDamageEvents(),
            static damageEvent => damageEvent.Flags.HasFlag(DamageEventFlags.CivvieUmbrellaBlock));
    }

    [Fact]
    public void CivilianUmbrellaShieldAbsorbsMineExplosionDamageFromFront()
    {
        var world = CreateJoinedCivilianWorld(new ExperimentalGameplaySettings());
        AdvanceTicks(world, 1);
        var civilian = world.LocalPlayer;
        var demoman = CreateBlueNetworkDemoman(world, 2);

        civilian.TeleportTo(0f, 0f);
        demoman.TeleportTo(96f, 0f);
        demoman.SetSpawnRoomState(false);
        SetPlayerAimDirection(civilian, 0f);
        Assert.True(civilian.TryActivateCivvieUmbrella());

        var healthBefore = civilian.Health;
        var chargeBefore = civilian.CivvieUmbrellaChargeTicks;
        _ = world.DrainPendingDamageEvents();
        _ = world.DrainPendingSoundEvents();
        _ = world.DrainPendingVisualEvents();

        var mine = SpawnCombatTestMine(world, demoman, civilian.X + 24f, civilian.Y);
        ExplodeCombatTestMine(world, mine);

        Assert.Equal(healthBefore, civilian.Health);
        Assert.Equal(chargeBefore - PlayerEntity.CivvieUmbrellaDirectExplosionDrainTicks, civilian.CivvieUmbrellaChargeTicks);
        Assert.Contains(world.PendingSoundEvents, static soundEvent => soundEvent.SoundName == "ExplosionSnd");
        Assert.Contains(world.PendingVisualEvents, static visualEvent => visualEvent.EffectName == "Explosion");
        Assert.Contains(
            world.DrainPendingDamageEvents(),
            static damageEvent => damageEvent.Flags.HasFlag(DamageEventFlags.CivvieUmbrellaBlock));
    }

    [Fact]
    public void CivilianUmbrellaShieldBlocksFlameProjectileAndAfterburn()
    {
        var world = CreateJoinedCivilianWorld(new ExperimentalGameplaySettings());
        AdvanceTicks(world, 1);
        SetOpenCombatLevel(world);
        var civilian = world.LocalPlayer;
        var pyro = CreateBlueNetworkPyro(world, 2);

        civilian.TeleportTo(20f, 0f);
        civilian.SetSpawnRoomState(false);
        pyro.TeleportTo(0f, 0f);
        pyro.SetSpawnRoomState(false);
        SetPlayerAimDirection(civilian, 180f);
        Assert.True(civilian.TryActivateCivvieUmbrella());

        var healthBefore = civilian.Health;
        var chargeBefore = civilian.CivvieUmbrellaChargeTicks;
        _ = world.DrainPendingDamageEvents();

        _ = SpawnCombatTestFlame(world, pyro, pyro.X, pyro.Y, velocityX: 10f, velocityY: 0f);
        AdvanceCombatFlames(world);

        Assert.False(civilian.IsBurning);
        Assert.Equal(healthBefore, civilian.Health);
        Assert.True(civilian.CivvieUmbrellaChargeTicks < chargeBefore);
        Assert.Contains(
            world.DrainPendingDamageEvents(),
            static damageEvent => damageEvent.Flags.HasFlag(DamageEventFlags.CivvieUmbrellaBlock));

        civilian.IgniteAfterburn(pyro.Id, 120f, PlayerEntity.BurnMaxIntensity, afterburnFalloff: false, burnFalloffAmount: 0f);
        var chargeBeforeAfterburn = civilian.CivvieUmbrellaChargeTicks;
        for (var tick = 0; tick < 5; tick += 1)
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
                AimWorldX: civilian.X - 96f,
                AimWorldY: civilian.Y,
                DebugKill: false,
                UseAbility: false,
                SwapWeapon: false));
            world.AdvanceOneTick();
        }

        Assert.True(civilian.IsBurning);
        Assert.Equal(healthBefore, civilian.Health);
        Assert.True(civilian.CivvieUmbrellaChargeTicks < chargeBeforeAfterburn);
    }

    [Fact]
    public void CivvieUmbrellaSplashExplosionDrainScalesImpactMultiplierBySplashIntensity()
    {
        Assert.Equal(1, PlayerEntity.GetCivvieUmbrellaSplashExplosionImpactMultiplier(0f));
        Assert.Equal(1, PlayerEntity.GetCivvieUmbrellaSplashExplosionImpactMultiplier(0.32f));
        Assert.Equal(2, PlayerEntity.GetCivvieUmbrellaSplashExplosionImpactMultiplier(0.34f));
        Assert.Equal(2, PlayerEntity.GetCivvieUmbrellaSplashExplosionImpactMultiplier(0.66f));
        Assert.Equal(3, PlayerEntity.GetCivvieUmbrellaSplashExplosionImpactMultiplier(0.68f));
        Assert.Equal(3, PlayerEntity.GetCivvieUmbrellaSplashExplosionImpactMultiplier(1f));
        Assert.Equal(
            PlayerEntity.CivvieUmbrellaImpactDrain * 2,
            PlayerEntity.GetCivvieUmbrellaSplashExplosionDrainTicksFromDamage(50f, 100f));
    }

    [Fact]
    public void CivilianUmbrellaAirblastPushesEnemiesNotTeammates()
    {
        var world = CreateJoinedCivilianWorld(new ExperimentalGameplaySettings());
        AdvanceTicks(world, 1);
        Assert.True(world.TryMoveLocalPlayerToControlPointSpawn());
        var teammate = CreateNetworkSoldier(world, 2);
        var enemy = CreateBlueNetworkScout(world, 3);
        teammate.TeleportTo(world.LocalPlayer.X + 64f, world.LocalPlayer.Y);
        enemy.TeleportTo(world.LocalPlayer.X + 64f, world.LocalPlayer.Y);
        teammate.SetSpawnRoomState(false);
        enemy.SetSpawnRoomState(false);
        teammate.ApplyVelocityImpulse(0f, 0f);
        enemy.ApplyVelocityImpulse(0f, 0f);

        HoldFireSecondaryUntilCivvieUmbrellaAirblast(world);

        Assert.True(
            MathF.Abs(teammate.HorizontalSpeed) < 0.001f,
            $"expected teammate to avoid horizontal airblast push; hspeed={teammate.HorizontalSpeed:0.###}");
        Assert.True(
            enemy.HorizontalSpeed > 0f,
            $"expected enemy to be pushed forward; speed={enemy.HorizontalSpeed:0.###}");
    }

    [Fact]
    public void CivilianTauntHealWaitsForFrameNineAndHealsFifteen()
    {
        var world = CreateJoinedCivilianWorld(new ExperimentalGameplaySettings());
        AdvanceTicks(world, 1);
        world.LocalPlayer.ForceSetHealth(100);
        var healthBefore = world.LocalPlayer.Health;

        PressTaunt(world);

        Assert.True(world.LocalPlayer.IsTaunting);

        AdvanceTicks(world, 29);
        Assert.Equal(healthBefore, world.LocalPlayer.Health);

        AdvanceTicks(world, 1);
        Assert.Equal(healthBefore + 15, world.LocalPlayer.Health);
    }

    [Fact]
    public void CivilianPogoTogglesOnUtilityPress()
    {
        var world = CreateJoinedCivilianWorld(new ExperimentalGameplaySettings());
        AdvanceTicks(world, 1);
        Assert.True(world.TryMoveLocalPlayerToControlPointSpawn());

        PressUseAbilitySpace(world);
        Assert.True(world.LocalPlayer.IsCivviePogoActive);

        ReleaseAllInput(world);
        PressUseAbilitySpace(world);
        Assert.False(world.LocalPlayer.IsCivviePogoActive);
    }

    [Fact]
    public void CivilianPogoTrickStartsWhenTauntPressedDuringSuperJumpAirPhase()
    {
        var world = CreateJoinedCivilianWorld(new ExperimentalGameplaySettings());
        AdvanceTicks(world, 1);
        Assert.True(world.TryMoveLocalPlayerToControlPointSpawn());

        PressUseAbilitySpace(world);
        Assert.True(world.LocalPlayer.IsCivviePogoActive);
        Assert.False(world.LocalPlayer.CanPerformCivviePogoTrick);

        PressUp(world);
        for (var tick = 0; tick < 120; tick += 1)
        {
            if (!world.LocalPlayer.IsGrounded && world.LocalPlayer.IsCivviePogoSuperJumpAirPhaseActive)
            {
                break;
            }

            world.AdvanceOneTick();
        }

        Assert.False(world.LocalPlayer.IsGrounded);
        Assert.True(world.LocalPlayer.IsCivviePogoSuperJumpAirPhaseActive);

        ReleaseAllInput(world);
        PressTaunt(world);

        Assert.True(world.LocalPlayer.IsCivviePogoTrickActive);
        Assert.False(world.LocalPlayer.IsTaunting);
        Assert.InRange(
            world.LocalPlayer.CivviePogoTrickDurationAtStart,
            1,
            PlayerEntity.ResolveCivviePogoTrickDurationTicks(30, world.Config.TicksPerSecond));
    }

    [Fact]
    public void CivilianUtilityDoesNotToggleStaleSoldierShotgunAfterClassChange()
    {
        var world = CreateJoinedSoldierWorld(new ExperimentalGameplaySettings(EnableSoldierShotgunSecondaryWeapon: true));
        AdvanceTicks(world, 1);

        Assert.True(world.LocalPlayer.HasExperimentalOffhandWeapon);
        PressUseAbilitySpace(world);
        Assert.True(world.LocalPlayer.IsExperimentalOffhandEquipped);

        ReleaseAllInput(world);
        world.LocalPlayer.SetSpawnRoomState(true);
        Assert.True(world.TrySetLocalClass(PlayerClass.Quote));
        AdvanceTicks(world, 1);

        Assert.Equal(PlayerClass.Quote, world.LocalPlayer.ClassId);
        Assert.True(world.LocalPlayer.IsAlive);
        Assert.False(world.LocalPlayer.HasExperimentalOffhandWeapon);
        Assert.False(world.LocalPlayer.IsExperimentalOffhandEquipped);
        Assert.False(world.LocalPlayer.IsExperimentalOffhandSelected);

        PressUseAbilitySpace(world);

        Assert.True(world.LocalPlayer.IsCivviePogoActive);
        Assert.False(world.LocalPlayer.HasExperimentalOffhandWeapon);
        Assert.False(world.LocalPlayer.IsExperimentalOffhandEquipped);
        Assert.False(world.LocalPlayer.IsExperimentalOffhandSelected);
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
        Assert.True(world.TryMoveLocalPlayerToControlPointSpawn());

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
    public void MedicSwapWeaponSpaceEquipsKritzWithoutActivatingUber()
    {
        var world = CreateJoinedMedicWorld(new ExperimentalGameplaySettings());
        AdvanceTicks(world, 1);

        Assert.True(world.LocalPlayer.HasExperimentalOffhandWeapon);
        Assert.False(world.LocalPlayer.IsExperimentalOffhandEquipped);
        world.LocalPlayer.FillMedicUberCharge();

        PressSwapWeaponSpace(world);

        Assert.False(world.LocalPlayer.IsMedicUbering);
        Assert.True(world.LocalPlayer.IsExperimentalOffhandEquipped);
        Assert.Equal(GameplayEquipmentSlot.Secondary, world.LocalPlayer.GameplayLoadoutState.EquippedSlot);
        Assert.True(world.LocalPlayer.HasEquippedBehavior(BuiltInGameplayBehaviorIds.MedigunCrit));
    }

    [Fact]
    public void StockMedicUberBlocksMedigunSwapWhileChargeIsActive()
    {
        var world = CreateJoinedMedicWorld(new ExperimentalGameplaySettings());
        AdvanceTicks(world, 1);
        world.LocalPlayer.FillMedicUberCharge();
        Assert.True(world.LocalPlayer.TryStartMedicUber());
        Assert.True(world.LocalPlayer.IsMedicUbering);
        Assert.Equal(GameplayEquipmentSlot.Primary, world.LocalPlayer.GameplayLoadoutState.EquippedSlot);

        PressSwapWeaponSpace(world);

        Assert.True(world.LocalPlayer.IsMedicUbering);
        Assert.False(world.LocalPlayer.IsExperimentalOffhandEquipped);
        Assert.Equal(GameplayEquipmentSlot.Primary, world.LocalPlayer.GameplayLoadoutState.EquippedSlot);
    }

    [Fact]
    public void StockMedicKritzUberBlocksMedigunSwapWhileChargeIsActive()
    {
        var world = CreateJoinedMedicWorld(new ExperimentalGameplaySettings());
        AdvanceTicks(world, 1);
        PressSwapWeaponSpace(world);
        world.LocalPlayer.FillMedicUberCharge();
        Assert.True(world.LocalPlayer.TryStartMedicUber());
        Assert.True(world.LocalPlayer.IsMedicUbering);
        Assert.True(world.LocalPlayer.IsExperimentalOffhandEquipped);
        Assert.Equal(GameplayEquipmentSlot.Secondary, world.LocalPlayer.GameplayLoadoutState.EquippedSlot);

        PressSwapWeaponSpace(world);

        Assert.True(world.LocalPlayer.IsMedicUbering);
        Assert.True(world.LocalPlayer.IsExperimentalOffhandEquipped);
        Assert.Equal(GameplayEquipmentSlot.Secondary, world.LocalPlayer.GameplayLoadoutState.EquippedSlot);
    }

    [Fact]
    public void MedicRightClickFiresNeedlesWithoutEquippingSecondaryWeapon()
    {
        var world = CreateJoinedMedicWorld(new ExperimentalGameplaySettings());
        AdvanceTicks(world, 1);

        PressFireSecondary(world);

        Assert.NotEmpty(world.Needles);
        Assert.False(world.LocalPlayer.IsMedicHealing);
        Assert.False(world.LocalPlayer.IsExperimentalOffhandEquipped);
        Assert.Equal(GameplayEquipmentSlot.Primary, world.LocalPlayer.GameplayLoadoutState.EquippedSlot);
        AssertCoreSecondaryAbilityEvent(world, "weapon.medigun", BuiltInGameplayBehaviorIds.MedicNeedlegun);
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
    public void EnemyEngineerSentryDoesNotInheritIncendiaryOrCryonicMunitions()
    {
        var world = CreateJoinedEngineerWorld(new ExperimentalGameplaySettings(
            EnableEngineerIncendiaryEnhancements: true,
            EnableEngineerCryonicMunitions: true));
        var enemyEngineer = CreateBlueNetworkEngineer(world, 2);
        var sentry = new SentryEntity(9000, enemyEngineer.Id, enemyEngineer.Team, enemyEngineer.X, enemyEngineer.Y, 1f);
        sentry.ForceBuilt();
        world.LocalPlayer.ForceSetHealth(world.LocalPlayer.MaxHealth);

        InvokeApplyExperimentalSentryPlayerHit(world, sentry, enemyEngineer, world.LocalPlayer, SentryEntity.HitDamage);

        Assert.Equal(0f, world.LocalPlayer.BurnDurationSourceTicks);
        Assert.False(world.LocalPlayer.IsExperimentalCryoSlowed);
        Assert.False(world.LocalPlayer.IsExperimentalCryoFrozen);
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
    public void EnemyEngineerSentryDoesNotInheritBuckshotConversion()
    {
        var world = CreateJoinedEngineerWorld(new ExperimentalGameplaySettings(EnableEngineerBuckshotConversion: true));
        var enemyEngineer = CreateBlueNetworkEngineer(world, 2);
        var sentry = BuildAndCompleteNetworkSentry(world, 2, enemyEngineer);
        PlaceLocalPlayerInVisibleSentryLane(world, sentry, preferredDistance: 180f, minimumDistance: 132f);

        AdvanceUntilSentryFired(world, sentry, 1);

        Assert.Empty(world.Shots);
    }

    [Fact]
    public void EnemyEngineerSentryDoesNotInheritPrecisionInstantiatorDamage()
    {
        var world = CreateJoinedEngineerWorld(new ExperimentalGameplaySettings(EnableEngineerPrecisionInstantiator: true));
        var enemyEngineer = CreateBlueNetworkEngineer(world, 2);
        var sentry = BuildAndCompleteNetworkSentry(world, 2, enemyEngineer);
        PlaceLocalPlayerInVisibleSentryLane(world, sentry, preferredDistance: 180f, minimumDistance: 132f);
        var healthBefore = world.LocalPlayer.Health;

        AdvanceUntilSentryFired(world, sentry, 1);

        Assert.Equal(SentryEntity.HitDamage, healthBefore - world.LocalPlayer.Health);
    }

    [Fact]
    public void EnemyEngineerSentryDoesNotInheritCaveatInjector()
    {
        var world = CreateJoinedEngineerWorld(new ExperimentalGameplaySettings(EnableEngineerCaveatInjector: true));
        var enemyEngineer = CreateBlueNetworkEngineer(world, 2);
        var sentry = BuildAndCompleteNetworkSentry(world, 2, enemyEngineer);
        PlaceLocalPlayerInVisibleSentryLane(world, sentry, preferredDistance: 180f, minimumDistance: 132f);

        AdvanceUntilSentryFired(
            world,
            sentry,
            ExperimentalGameplaySettings.DefaultEngineerCaveatInjectorShotInterval);

        Assert.Empty(world.Rockets);
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

    private static SimulationWorld CreateJoinedCivilianWorld(ExperimentalGameplaySettings settings)
    {
        var world = new SimulationWorld();
        Assert.True(world.TryLoadLevel("Harvest"));
        world.PrepareLocalPlayerJoin();
        world.SetLocalPlayerTeam(PlayerTeam.Red);
        world.CompleteLocalPlayerJoin(PlayerClass.Quote);
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

    private static SentryEntity BuildAndCompleteNetworkSentry(SimulationWorld world, byte ownerSlot, PlayerEntity owner)
    {
        Assert.True(world.TryMoveLocalPlayerToControlPointSpawn());
        owner.TeleportTo(world.LocalPlayer.X + 160f, world.LocalPlayer.Y);
        owner.SetSpawnRoomState(false);
        Assert.True(world.TrySetNetworkPlayerInput(
            ownerSlot,
            new PlayerInputSnapshot(
                Left: false,
                Right: false,
                Up: false,
                Down: false,
                BuildSentry: true,
                DestroySentry: false,
                Taunt: false,
                FirePrimary: false,
                FireSecondary: false,
                AimWorldX: owner.X + 96f,
                AimWorldY: owner.Y,
                DebugKill: false)));
        world.AdvanceOneTick();
        Assert.True(world.TrySetNetworkPlayerInput(ownerSlot, default));
        var sentry = Assert.Single(world.Sentries, candidate => candidate.OwnerPlayerId == owner.Id);
        sentry.ForceBuilt();
        return sentry;
    }

    private static void MoveKritzBeamTestPlayersToOpenCombatLevel(SimulationWorld world, PlayerEntity enemy)
    {
        SetOpenCombatLevel(world);
        world.TeleportLocalPlayer(100f, 100f);
        world.LocalPlayer.SetSpawnRoomState(false);
        enemy.TeleportTo(world.LocalPlayer.X + 96f, world.LocalPlayer.Y);
        enemy.SetSpawnRoomState(false);
    }

    private static void SetOpenCombatLevel(SimulationWorld world)
    {
        CombatTestSetLevelMethod.Invoke(
            world,
            [
                new SimpleLevel(
                    name: "experimental_perk_regression_test",
                    mode: GameModeKind.CaptureTheFlag,
                    bounds: new WorldBounds(1024f, 768f),
                    mapScale: 1f,
                    backgroundAssetName: null,
                    mapAreaIndex: 1,
                    mapAreaCount: 1,
                    localSpawn: new SpawnPoint(100f, 100f),
                    redSpawns: [new SpawnPoint(100f, 100f)],
                    blueSpawns: [new SpawnPoint(900f, 100f)],
                    intelBases:
                    [
                        new IntelBaseMarker(PlayerTeam.Red, 100f, 100f),
                        new IntelBaseMarker(PlayerTeam.Blue, 900f, 100f),
                    ],
                    roomObjects: [],
                    floorY: 768f,
                    solids: [],
                    importedFromSource: false),
            ]);
    }

    private static PlayerEntity CreateBlueNetworkScout(SimulationWorld world, byte slot)
    {
        Assert.True(world.TryPrepareNetworkPlayerJoin(slot));
        Assert.True(world.TrySetNetworkPlayerTeam(slot, PlayerTeam.Blue));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(slot, PlayerClass.Scout));
        Assert.True(world.TryGetNetworkPlayer(slot, out var player));
        return player;
    }

    private static PlayerEntity CreateBlueNetworkSpy(SimulationWorld world, byte slot)
    {
        Assert.True(world.TryPrepareNetworkPlayerJoin(slot));
        Assert.True(world.TrySetNetworkPlayerTeam(slot, PlayerTeam.Blue));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(slot, PlayerClass.Spy));
        Assert.True(world.TryGetNetworkPlayer(slot, out var player));
        return player;
    }

    private static PlayerEntity CreateBlueNetworkSoldier(SimulationWorld world, byte slot)
    {
        Assert.True(world.TryPrepareNetworkPlayerJoin(slot));
        Assert.True(world.TrySetNetworkPlayerTeam(slot, PlayerTeam.Blue));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(slot, PlayerClass.Soldier));
        Assert.True(world.TryGetNetworkPlayer(slot, out var player));
        return player;
    }

    private static PlayerEntity CreateBlueNetworkDemoman(SimulationWorld world, byte slot)
    {
        Assert.True(world.TryPrepareNetworkPlayerJoin(slot));
        Assert.True(world.TrySetNetworkPlayerTeam(slot, PlayerTeam.Blue));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(slot, PlayerClass.Demoman));
        Assert.True(world.TryGetNetworkPlayer(slot, out var player));
        return player;
    }

    private static PlayerEntity CreateBlueNetworkPyro(SimulationWorld world, byte slot)
    {
        Assert.True(world.TryPrepareNetworkPlayerJoin(slot));
        Assert.True(world.TrySetNetworkPlayerTeam(slot, PlayerTeam.Blue));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(slot, PlayerClass.Pyro));
        Assert.True(world.TryGetNetworkPlayer(slot, out var player));
        return player;
    }

    private static PlayerEntity CreateRedNetworkScout(SimulationWorld world, byte slot)
    {
        Assert.True(world.TryPrepareNetworkPlayerJoin(slot));
        Assert.True(world.TrySetNetworkPlayerTeam(slot, PlayerTeam.Red));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(slot, PlayerClass.Scout));
        Assert.True(world.TryGetNetworkPlayer(slot, out var player));
        return player;
    }

    private static PlayerEntity CreateBlueNetworkEngineer(SimulationWorld world, byte slot)
    {
        Assert.True(world.TryPrepareNetworkPlayerJoin(slot));
        Assert.True(world.TrySetNetworkPlayerTeam(slot, PlayerTeam.Blue));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(slot, PlayerClass.Engineer));
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

    private static void AddMedicUberChargeUntil(PlayerEntity medic, float targetCharge)
    {
        while (medic.MedicUberCharge + 0.001f < targetCharge)
        {
            medic.AddMedicUberCharge(1.75f);
        }
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
        => PressUseAbilitySpace(world, world.LocalPlayer.X + 96f, world.LocalPlayer.Y);

    private static void PressTaunt(SimulationWorld world)
    {
        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: true,
            FirePrimary: false,
            FireSecondary: false,
            AimWorldX: world.LocalPlayer.X + 96f,
            AimWorldY: world.LocalPlayer.Y,
            DebugKill: false,
            UseAbility: false,
            SwapWeapon: false));
        world.AdvanceOneTick();
    }

    private static void PressUp(SimulationWorld world)
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
            DebugKill: false,
            UseAbility: false,
            SwapWeapon: false));
    }

    private static void PressUseAbilitySpace(SimulationWorld world, float aimWorldX, float aimWorldY)
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
            AimWorldX: aimWorldX,
            AimWorldY: aimWorldY,
            DebugKill: false,
            UseAbility: true,
            SwapWeapon: false));
        world.AdvanceOneTick();
    }

    private static void PressUseAbilitySpaceAndFirePrimary(SimulationWorld world, float aimWorldX, float aimWorldY)
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
            AimWorldX: aimWorldX,
            AimWorldY: aimWorldY,
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

    private static void HoldFireSecondaryUntilCivvieUmbrellaAirblast(SimulationWorld world)
    {
        for (var tick = 0; tick < PlayerEntity.CivvieUmbrellaAirblastOpeningTick + 2; tick += 1)
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

        Assert.True(
            world.LocalPlayer.IsCivvieUmbrellaActive,
            "expected umbrella to stay active while holding secondary through opening airblast timing");
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

    private static void PlaceLocalPlayerInVisibleSentryLane(
        SimulationWorld world,
        SentryEntity sentry,
        float preferredDistance,
        float minimumDistance)
    {
        var targetPosition = FindVisibleSentryTargetPosition(
            world,
            sentry,
            world.LocalPlayer,
            preferredDistance,
            minimumDistance);
        world.LocalPlayer.TeleportTo(targetPosition.X, targetPosition.Y);
        world.LocalPlayer.SetSpawnRoomState(false);
        world.LocalPlayer.ForceSetHealth(world.LocalPlayer.MaxHealth);
    }

    private static void AdvanceUntilSentryFired(SimulationWorld world, SentryEntity sentry, int shotCount)
    {
        for (var tick = 0; tick < 240 && sentry.ConsecutiveShotsFired < shotCount; tick += 1)
        {
            world.AdvanceOneTick();
        }

        Assert.True(sentry.ConsecutiveShotsFired >= shotCount);
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
            [target, damage, attacker, PlayerEntity.SpyDamageRevealAlpha, DamageEventFlags.None, true, true, null, null, null])!;
    }

    private static void InvokeSpawnStabMask(SimulationWorld world, PlayerEntity owner, float directionDegrees)
    {
        var method = GetRequiredNonPublicMethod("SpawnStabMask");
        method.Invoke(world, [owner, directionDegrees]);
    }

    private static void InvokeAdvanceStabMasks(SimulationWorld world)
    {
        var method = GetRequiredNonPublicMethod("AdvanceStabMasks");
        method.Invoke(world, null);
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

    private static RocketProjectileEntity SpawnCombatTestRocket(
        SimulationWorld world,
        PlayerEntity owner,
        float x,
        float y,
        float speed = 0f,
        float directionRadians = 0f)
    {
        var method = typeof(SimulationWorld).GetMethod("CombatTestSpawnRocket", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var result = method!.Invoke(world, [owner, x, y, speed, directionRadians]);
        return Assert.IsType<RocketProjectileEntity>(result);
    }

    private static void AdvanceCombatRockets(SimulationWorld world)
    {
        var method = typeof(SimulationWorld).GetMethod("AdvanceRockets", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        _ = method!.Invoke(world, []);
    }

    private static int GetCombatRocketCount(SimulationWorld world)
    {
        return world.Rockets.Count;
    }

    private static GrenadeProjectileEntity SpawnCombatTestGrenade(
        SimulationWorld world,
        PlayerEntity owner,
        float x,
        float y,
        float velocityX = 0f,
        float velocityY = 0f)
    {
        var method = typeof(SimulationWorld).GetMethod("CombatTestSpawnGrenade", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var result = method!.Invoke(world, [owner, x, y, velocityX, velocityY]);
        return Assert.IsType<GrenadeProjectileEntity>(result);
    }

    private static void AdvanceCombatGrenades(SimulationWorld world)
    {
        var method = typeof(SimulationWorld).GetMethod("AdvanceGrenades", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        _ = method!.Invoke(world, []);
    }

    private static int GetCombatGrenadeCount(SimulationWorld world)
    {
        return world.Grenades.Count;
    }

    private static MineProjectileEntity SpawnCombatTestMine(
        SimulationWorld world,
        PlayerEntity owner,
        float x,
        float y,
        float velocityX = 0f,
        float velocityY = 0f,
        bool stickied = false)
    {
        var method = typeof(SimulationWorld).GetMethod(
            "CombatTestSpawnMine",
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(PlayerEntity), typeof(float), typeof(float), typeof(float), typeof(float), typeof(bool)],
            modifiers: null);
        Assert.NotNull(method);
        var result = method!.Invoke(world, [owner, x, y, velocityX, velocityY, stickied]);
        return Assert.IsType<MineProjectileEntity>(result);
    }

    private static void ExplodeCombatTestMine(SimulationWorld world, MineProjectileEntity mine)
    {
        var method = typeof(SimulationWorld).GetMethod("CombatTestExplodeMine", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        _ = method!.Invoke(world, [mine]);
    }

    private static FlameProjectileEntity SpawnCombatTestFlame(
        SimulationWorld world,
        PlayerEntity owner,
        float x,
        float y,
        float velocityX = 0f,
        float velocityY = 0f)
    {
        var method = typeof(SimulationWorld).GetMethod("CombatTestSpawnFlame", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var result = method!.Invoke(world, [owner, x, y, velocityX, velocityY]);
        return Assert.IsType<FlameProjectileEntity>(result);
    }

    private static void AdvanceCombatFlames(SimulationWorld world)
    {
        var method = typeof(SimulationWorld).GetMethod("AdvanceFlames", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        _ = method!.Invoke(world, []);
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
