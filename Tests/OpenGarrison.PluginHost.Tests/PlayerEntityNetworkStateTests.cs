using OpenGarrison.Core;
using OpenGarrison.GameplayModding;
using OpenGarrison.Protocol;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class PlayerEntityNetworkStateTests
{
    [Fact]
    public void ApplyNetworkStateFallsBackToDefaultGameplayLoadoutWhenReplicatedLoadoutIsInvalid()
    {
        var player = new PlayerEntity(1, CharacterClassCatalog.Scout, "Test");

        player.ApplyNetworkState(
            team: PlayerTeam.Red,
            classDefinition: CharacterClassCatalog.Soldier,
            isAlive: true,
            x: 10f,
            y: 20f,
            horizontalSpeed: 1f,
            verticalSpeed: 2f,
            health: 150,
            currentShells: 3,
            kills: 0,
            deaths: 0,
            caps: 0,
            points: 0f,
            healPoints: 0,
            activeDominationCount: 0,
            isDominatingLocalViewer: false,
            isDominatedByLocalViewer: false,
            metal: 50f,
            isGrounded: true,
            remainingAirJumps: 0,
            isCarryingIntel: false,
            intelRechargeTicks: 0f,
            isSpyCloaked: false,
            spyCloakAlpha: 0f,
            isSpySuperjumping: false,
            spySuperjumpHorizontalVelocity: 0f,
            spySuperjumpCooldownTicksRemaining: 0,
            spyBackstabVisualTicksRemaining: 0,
            isUbered: false,            isKritzCritBoosted: false,            isHeavyEating: false,
            heavyEatTicksRemaining: 0,
            isSniperScoped: false,
            sniperChargeTicks: 0,
            isUsingBinoculars: false,
            binocularsFocusX: 0f,
            binocularsFocusY: 0f,
            facingDirectionX: 1f,
            aimDirectionDegrees: 90f,
            aimWorldX: 0f,
            aimWorldY: 0f,
            isTaunting: false,
            tauntFrameIndex: 0f,
            isChatBubbleVisible: false,
            chatBubbleFrameIndex: 0,
            chatBubbleAlpha: 0f,
            gameplayModPackId: "stock.gg2",
            gameplayLoadoutId: "soldier.invalid",
            gameplayPrimaryItemId: "weapon.not-real",
            gameplaySecondaryItemId: "",
            gameplayUtilityItemId: "",
            gameplayEquippedSlot: (byte)GameplayEquipmentSlot.Secondary,
            gameplayEquippedItemId: "weapon.not-real",
            gameplayAcquiredItemId: "");

        Assert.Equal(PlayerClass.Soldier, player.ClassId);
        Assert.Equal("soldier.stock", player.SelectedGameplayLoadoutId);
        Assert.Equal("soldier.stock", player.GameplayLoadoutState.LoadoutId);
        Assert.Equal(GameplayEquipmentSlot.Secondary, player.GameplayLoadoutState.EquippedSlot);
        Assert.Equal("weapon.soldier-shotgun", player.GameplayLoadoutState.EquippedItemId);
    }

    [Fact]
    public void ApplyNetworkStateAcceptsValidatedReplicatedGameplayLoadoutState()
    {
        var player = new PlayerEntity(1, CharacterClassCatalog.Scout, "Test");

        player.ApplyNetworkState(
            team: PlayerTeam.Blue,
            classDefinition: CharacterClassCatalog.Heavy,
            isAlive: true,
            x: 10f,
            y: 20f,
            horizontalSpeed: 1f,
            verticalSpeed: 2f,
            health: 180,
            currentShells: 5,
            kills: 1,
            deaths: 2,
            caps: 3,
            points: 4f,
            healPoints: 0,
            activeDominationCount: 0,
            isDominatingLocalViewer: false,
            isDominatedByLocalViewer: false,
            metal: 25f,
            isGrounded: true,
            remainingAirJumps: 0,
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
            facingDirectionX: -1f,
            aimDirectionDegrees: 180f,
            aimWorldX: 0f,
            aimWorldY: 0f,
            isTaunting: false,
            tauntFrameIndex: 0f,
            isChatBubbleVisible: false,
            chatBubbleFrameIndex: 0,
            chatBubbleAlpha: 0f,
            gameplayModPackId: "stock.gg2",
            gameplayLoadoutId: "heavy.stock",
            gameplayPrimaryItemId: "weapon.minigun",
            gameplaySecondaryItemId: "weapon.heavy-shotgun",
            gameplayUtilityItemId: "",
            gameplayEquippedSlot: (byte)GameplayEquipmentSlot.Primary,
            gameplayEquippedItemId: "weapon.minigun",
            gameplayAcquiredItemId: "");

        Assert.Equal(PlayerClass.Heavy, player.ClassId);
        Assert.Equal("heavy.stock", player.SelectedGameplayLoadoutId);
        Assert.Equal("heavy.stock", player.GameplayLoadoutState.LoadoutId);
        Assert.Equal("weapon.heavy-shotgun", player.GameplayLoadoutState.SecondaryItemId);
        Assert.DoesNotContain("ability.heavy-sandvich", player.GameplayLoadoutState.AbilityItemIds ?? []);
        Assert.True(player.HasGameplayAbilityBehavior(
            GameplayAbilityConstants.SpecialChannel,
            BuiltInGameplayBehaviorIds.HeavySandvich));
        Assert.Equal(GameplayEquipmentSlot.Primary, player.SelectedGameplayEquippedSlot);
        Assert.Equal(GameplayEquipmentSlot.Primary, player.GameplayLoadoutState.EquippedSlot);
        Assert.Equal("weapon.minigun", player.GameplayLoadoutState.EquippedItemId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ApplyNetworkStateClearsStaleOffhandSelectionWhenReplicatedSlotReturnsToPrimary(bool includeFullLoadoutState)
    {
        var player = new PlayerEntity(1, CharacterClassCatalog.Soldier, "Test");
        player.Spawn(PlayerTeam.Red, 0f, 0f);
        player.SetExperimentalOffhandWeapon(CharacterClassCatalog.SoldierShotgun);
        player.EquipExperimentalOffhandWeapon();

        Assert.True(player.IsExperimentalOffhandSelected);
        Assert.True(player.IsExperimentalOffhandEquipped);

        ApplySoldierNetworkSnapshot(player, GameplayEquipmentSlot.Primary, includeFullLoadoutState);

        Assert.False(player.IsExperimentalOffhandEquipped);
        Assert.False(player.IsExperimentalOffhandSelected);
        Assert.Equal(GameplayEquipmentSlot.Primary, player.SelectedGameplayEquippedSlot);
        Assert.Equal(GameplayEquipmentSlot.Primary, player.GameplayLoadoutState.EquippedSlot);
        Assert.Equal(player.GameplayLoadoutState.PrimaryItemId, player.GameplayLoadoutState.EquippedItemId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ApplyNetworkStateCanSelectOffhandFromReplicatedSecondarySlot(bool includeFullLoadoutState)
    {
        var player = new PlayerEntity(1, CharacterClassCatalog.Soldier, "Test");
        player.Spawn(PlayerTeam.Red, 0f, 0f);
        player.SetExperimentalOffhandWeapon(CharacterClassCatalog.SoldierShotgun);

        Assert.False(player.IsExperimentalOffhandSelected);
        Assert.False(player.IsExperimentalOffhandEquipped);

        ApplySoldierNetworkSnapshot(player, GameplayEquipmentSlot.Secondary, includeFullLoadoutState);

        Assert.True(player.IsExperimentalOffhandEquipped);
        Assert.True(player.IsExperimentalOffhandSelected);
        Assert.Equal(GameplayEquipmentSlot.Secondary, player.SelectedGameplayEquippedSlot);
        Assert.Equal(GameplayEquipmentSlot.Secondary, player.GameplayLoadoutState.EquippedSlot);
        Assert.Equal(player.GameplayLoadoutState.SecondaryItemId, player.GameplayLoadoutState.EquippedItemId);
    }

    [Fact]
    public void ApplyNetworkStateHydratesEquippedAcquiredSoldierWeaponIdentity()
    {
        var player = new PlayerEntity(1, CharacterClassCatalog.Scout, "Test");

        ApplySoldierNetworkSnapshot(
            player,
            GameplayEquipmentSlot.Secondary,
            includeFullLoadoutState: true,
            acquiredItemId: "weapon.rocketlauncher",
            equippedItemIdOverride: "weapon.rocketlauncher");

        Assert.Equal(PlayerClass.Soldier, player.AcquiredWeaponClassId);
        Assert.True(player.IsAcquiredWeaponPresented);
        Assert.Equal("weapon.rocketlauncher", player.GameplayLoadoutState.AcquiredItemId);
        Assert.Equal("weapon.rocketlauncher", player.GameplayLoadoutState.EquippedItemId);
        Assert.Equal(PrimaryWeaponKind.RocketLauncher, player.AcquiredWeapon?.Kind);
    }

    [Fact]
    public void ApplyNetworkStateHydratesSniperBowAsSelectedPrimary()
    {
        var player = new PlayerEntity(1, CharacterClassCatalog.Sniper, "Test");
        player.Spawn(PlayerTeam.Red, 0f, 0f);

        Assert.False(player.HasExperimentalOffhandWeapon);
        Assert.False(player.IsSniperBowEquipped);

        ApplySniperNetworkSnapshot(
            player,
            "weapon.bow");

        Assert.False(player.HasExperimentalOffhandWeapon);
        Assert.True(player.IsSniperBowEquipped);
        Assert.Equal("weapon.sniper-smg", player.GameplayLoadoutState.SecondaryItemId);
        Assert.Equal("weapon.bow", player.GameplayLoadoutState.PrimaryItemId);
        Assert.Equal("weapon.bow", player.GameplayLoadoutState.EquippedItemId);
    }

    [Fact]
    public void ApplyNetworkStateHydratesMedicKritzAsSelectedPrimary()
    {
        var player = new PlayerEntity(1, CharacterClassCatalog.Medic, "Test");
        player.Spawn(PlayerTeam.Red, 0f, 0f);

        Assert.False(player.HasExperimentalOffhandWeapon);
        Assert.Equal(0, player.ExperimentalOffhandCurrentShells);

        ApplyMedicNetworkSnapshot(
            player,
            "weapon.medigun.crit");

        Assert.False(player.HasExperimentalOffhandWeapon);
        Assert.Equal(1, player.CurrentShells);
        Assert.Equal(1, player.MaxShells);
        Assert.Equal("weapon.medic-needlegun", player.GameplayLoadoutState.SecondaryItemId);
        Assert.Equal("weapon.medigun.crit", player.GameplayLoadoutState.PrimaryItemId);
        Assert.Equal("weapon.medigun.crit", player.GameplayLoadoutState.EquippedItemId);
    }

    [Fact]
    public void ApplyProtocol64StateKeepsMedicSecondaryAmmoSeparateFromMedigun()
    {
        var player = new PlayerEntity(1, CharacterClassCatalog.Scout, "Test");
        var state = new Protocol64PlayerState(
            Slot: 1,
            PlayerId: 1,
            Generation: 1,
            GameplayClassId: "medic",
            Health: 100,
            MaxHealth: 150,
            Team: (byte)PlayerTeam.Red,
            IsAlive: true,
            X: 0f,
            Y: 0f,
            VelocityX: 0f,
            VelocityY: 0f,
            ActiveWeapon: 0,
            AbilityState: 0,
            StateTick: 1,
            CurrentAmmo: 1,
            MaxAmmo: 1,
            OffhandAmmo: 17,
            OffhandMaxAmmo: 40,
            OffhandCooldownTicks: 2,
            OffhandReloadTicks: 3,
            MedicNeedleCooldownTicks: 5,
            MedicNeedleRefillTicks: 17);

        player.ApplyProtocol64State(state, CharacterClassCatalog.Medic);

        Assert.Equal(PlayerClass.Medic, player.ClassId);
        Assert.Equal(150, player.MaxHealth);
        Assert.Equal(1, player.CurrentShells);
        Assert.Equal(1, player.MaxShells);
        Assert.True(player.HasExperimentalOffhandWeapon);
        Assert.Equal(17, player.ExperimentalOffhandCurrentShells);
        Assert.Equal(40, player.ExperimentalOffhandMaxShells);
        Assert.Equal(5, player.MedicNeedleCooldownTicks);
        Assert.Equal(17, player.MedicNeedleRefillTicks);
    }

    [Fact]
    public void ApplyNetworkStateHydratesMedicHealDartCooldownFromReplicatedState()
    {
        var player = new PlayerEntity(1, CharacterClassCatalog.Medic, "Test");

        ApplyMedicNetworkSnapshot(
            player,
            "weapon.medigun",
            [
                WholeGameplayState(GameplayAbilityReplicatedState.MedicHealDartCooldownTicksKey, 7),
            ]);

        Assert.Equal(7, player.MedicHealDartCooldownTicks);
        Assert.Equal(1, player.CurrentShells);
    }

    [Theory]
    [InlineData("sniper", 125, 25, "weapon.sniper-smg")]
    [InlineData("scout", 125, 12, "weapon.scout-pistol")]
    [InlineData("medic", 150, 40, "weapon.medic-needlegun")]
    public void Protocol64HydratesConfiguredSecondaryIndependentlyFromAlternatePrimaries(
        string gameplayClassId,
        int maxHealth,
        int offhandMaxAmmo,
        string expectedSecondaryItemId)
    {
        var player = new PlayerEntity(1, CharacterClassCatalog.Scout, "Test");
        var state = new Protocol64PlayerState(
            Slot: 1,
            PlayerId: 1,
            Generation: 1,
            GameplayClassId: gameplayClassId,
            Health: maxHealth,
            MaxHealth: maxHealth,
            Team: (byte)PlayerTeam.Red,
            IsAlive: true,
            X: 0f,
            Y: 0f,
            VelocityX: 0f,
            VelocityY: 0f,
            ActiveWeapon: (byte)GameplayEquipmentSlot.Secondary,
            AbilityState: 0,
            StateTick: 1,
            OffhandAmmo: offhandMaxAmmo,
            OffhandMaxAmmo: offhandMaxAmmo);

        player.ApplyProtocol64State(state, CharacterClassCatalog.GetDefinition(gameplayClassId));

        Assert.Equal(GameplayEquipmentSlot.Secondary, player.SelectedGameplayEquippedSlot);
        Assert.Equal(GameplayEquipmentSlot.Secondary, player.GameplayLoadoutState.EquippedSlot);
        Assert.Equal(expectedSecondaryItemId, player.GameplayLoadoutState.SecondaryItemId);
        Assert.True(player.HasExperimentalOffhandWeapon);
        Assert.True(player.IsExperimentalOffhandSelected);
    }

    [Fact]
    public void ApplyNetworkStateUsesAuthoritativeMaxHealthForReplicatedPlayer()
    {
        var player = new PlayerEntity(1, CharacterClassCatalog.Scout, "Test");

        ApplySoldierNetworkSnapshot(
            player,
            GameplayEquipmentSlot.Primary,
            includeFullLoadoutState: true,
            networkMaxHealth: 200);

        Assert.Equal(200, player.MaxHealth);
        Assert.Equal(200, player.Health);
    }

    [Fact]
    public void PredictionRestorePreservesAuthoritativeNetworkMaxHealth()
    {
        var player = new PlayerEntity(1, CharacterClassCatalog.Scout, "Test");
        player.ApplyProtocol64State(
            new Protocol64PlayerState(
                Slot: 1,
                PlayerId: 1,
                Generation: 1,
                GameplayClassId: "medic",
                Health: 100,
                MaxHealth: 150,
                Team: (byte)PlayerTeam.Red,
                IsAlive: true,
                X: 0f,
                Y: 0f,
                VelocityX: 0f,
                VelocityY: 0f,
                ActiveWeapon: 0,
                AbilityState: 0,
                StateTick: 1,
                CurrentAmmo: 40,
                MaxAmmo: 40),
            CharacterClassCatalog.Medic);

        var predictionState = player.CapturePredictionState();
        player.SetClassDefinition(CharacterClassCatalog.Scout);
        player.RestorePredictionState(predictionState);

        Assert.Equal(150, player.MaxHealth);
        Assert.Equal(100, player.Health);
    }

    [Fact]
    public void PredictionRestorePreservesTransientAbilityMovementState()
    {
        var spy = new PlayerEntity(1, CharacterClassCatalog.Spy, "Spy");
        spy.Spawn(PlayerTeam.Red, 100f, 100f);
        Assert.True(spy.TryStartSpySuperjumpCharge(32f, leftHeld: true, rightHeld: false, upHeld: false, downHeld: false));
        var spyState = spy.CapturePredictionState();

        spy.CancelSpySuperjumpCharge();
        spy.RestorePredictionState(spyState);

        Assert.Equal(spyState.SpySuperjumpChargeTicks, spy.SpySuperjumpChargeTicks);
        Assert.Equal(spyState.SpySuperjumpChargeDirectionDegrees, spy.SpySuperjumpChargeDirectionDegrees);
        Assert.Equal(spyState.SpySuperjumpChargeStartMovementButtons, spy.SpySuperjumpChargeStartMovementButtons);

        var heavy = new PlayerEntity(2, CharacterClassCatalog.Heavy, "Heavy");
        heavy.Spawn(PlayerTeam.Red, 100f, 100f);
        Assert.True(heavy.TryStartExperimentalGhostDash(
            dashTicks: 12,
            cooldownTicks: 40,
            nextAttackDamageMultiplier: 2f,
            dashImpulse: 48f,
            requireExperimentalDemoknight: false,
            useMomentum: true,
            movementTicks: 8,
            slideVelocityPerTick: 3.5f,
            burstSpeedMultiplier: 4f,
            disableGravity: true,
            enableGhostTrail: true));
        var heavyState = heavy.CapturePredictionState();

        heavy.AdvanceTickState(default, 1d / 30d);
        heavy.RestorePredictionState(heavyState);
        var restoredHeavyState = heavy.CapturePredictionState();

        Assert.Equal(heavyState.ExperimentalGhostDashMovementTicksRemaining, restoredHeavyState.ExperimentalGhostDashMovementTicksRemaining);
        Assert.Equal(heavyState.ExperimentalGhostDashDistanceRemaining, restoredHeavyState.ExperimentalGhostDashDistanceRemaining);
        Assert.Equal(heavyState.ExperimentalGhostDashInitialDistance, restoredHeavyState.ExperimentalGhostDashInitialDistance);
        Assert.Equal(heavyState.ExperimentalGhostDashMomentumDirectionX, restoredHeavyState.ExperimentalGhostDashMomentumDirectionX);
        Assert.Equal(heavyState.ExperimentalGhostDashSlideVelocityPerTick, restoredHeavyState.ExperimentalGhostDashSlideVelocityPerTick);
        Assert.Equal(heavyState.ExperimentalGhostDashNextAttackDamageMultiplierValue, restoredHeavyState.ExperimentalGhostDashNextAttackDamageMultiplierValue);
    }

    [Theory]
    [InlineData(PlayerClass.Soldier, "soldier_shotgun_ammo")]
    [InlineData(PlayerClass.Demoman, "demoman_gl_ammo")]
    [InlineData(PlayerClass.Scout, "scout_nailgun_ammo")]
    [InlineData(PlayerClass.Sniper, "sniper_bow_ammo")]
    [InlineData(PlayerClass.Medic, "medic_kritz_ammo")]
    public void ApplyNetworkStateReplacesResolvedReplicatedOffhandAmmoState(
        PlayerClass playerClass,
        string ammoKey)
    {
        var player = new PlayerEntity(1, CharacterClassCatalog.Scout, "Test");
        var classDefinition = CharacterClassCatalog.GetDefinition(playerClass);

        ApplyReplicatedAmmoSnapshot(player, classDefinition, ammoKey, 4);
        Assert.True(player.TryGetReplicatedStateInt("core.player", ammoKey, out var initialAmmo));
        Assert.Equal(4, initialAmmo);

        // A resolved delta can legitimately contain an empty replicated-state list after the
        // server status merge removes the last runtime weapon state.
        ApplyReplicatedAmmoSnapshot(player, classDefinition, ammoKey, null);
        Assert.False(player.TryGetReplicatedStateInt("core.player", ammoKey, out _));
    }

    [Fact]
    public void CloakedSpyHitRevealsCloakToMinimumThirtyPercent()
    {
        var player = new PlayerEntity(1, CharacterClassCatalog.Spy, "SpyTest");
        player.Spawn(PlayerTeam.Red, 0f, 0f);
        Assert.True(player.TryToggleSpyCloak());

        player.ApplyNetworkState(
            team: PlayerTeam.Red,
            classDefinition: CharacterClassCatalog.Spy,
            isAlive: true,
            x: 0f,
            y: 0f,
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
            isSpyCloaked: true,
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
            facingDirectionX: 1f,
            aimDirectionDegrees: 0f,
            aimWorldX: 0f,
            aimWorldY: 0f,
            isTaunting: false,
            tauntFrameIndex: 0f,
            isChatBubbleVisible: false,
            chatBubbleFrameIndex: 0,
            chatBubbleAlpha: 0f,
            gameplayModPackId: "stock.gg2",
            gameplayLoadoutId: "spy.stock",
            gameplayPrimaryItemId: "weapon.revolver",
            gameplaySecondaryItemId: "ability.spy-cloak",
            gameplayUtilityItemId: "",
            gameplayEquippedSlot: (byte)GameplayEquipmentSlot.Primary,
            gameplayEquippedItemId: "weapon.revolver",
            gameplayAcquiredItemId: "");

        player.RevealSpy(PlayerEntity.SpyDamageRevealAlpha);

        Assert.Equal(0.3f, player.SpyCloakAlpha);
        Assert.True(player.IsSpyVisibleToEnemies);
    }

    [Fact]
    public void ApplyNetworkStateMarksFullyCloakedSpyVisibleToEnemiesDuringBackstab()
    {
        var player = new PlayerEntity(1, CharacterClassCatalog.Spy, "SpyTest");
        player.Spawn(PlayerTeam.Red, 0f, 0f);
        Assert.True(player.TryToggleSpyCloak());

        player.ApplyNetworkState(
            team: PlayerTeam.Red,
            classDefinition: CharacterClassCatalog.Spy,
            isAlive: true,
            x: 0f,
            y: 0f,
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
            isSpyCloaked: true,
            spyCloakAlpha: 0f,
            isSpySuperjumping: false,
            spySuperjumpHorizontalVelocity: 0f,
            spySuperjumpCooldownTicksRemaining: 0,
            spyBackstabVisualTicksRemaining: PlayerEntity.SpyBackstabVisualTicksDefault,
            isUbered: false,
            isKritzCritBoosted: false,
            isHeavyEating: false,
            heavyEatTicksRemaining: 0,
            isSniperScoped: false,
            sniperChargeTicks: 0,
            isUsingBinoculars: false,
            binocularsFocusX: 0f,
            binocularsFocusY: 0f,
            facingDirectionX: 1f,
            aimDirectionDegrees: 0f,
            aimWorldX: 0f,
            aimWorldY: 0f,
            isTaunting: false,
            tauntFrameIndex: 0f,
            isChatBubbleVisible: false,
            chatBubbleFrameIndex: 0,
            chatBubbleAlpha: 0f,
            gameplayModPackId: "stock.gg2",
            gameplayLoadoutId: "spy.stock",
            gameplayPrimaryItemId: "weapon.revolver",
            gameplaySecondaryItemId: "ability.spy-cloak",
            gameplayUtilityItemId: "",
            gameplayEquippedSlot: (byte)GameplayEquipmentSlot.Primary,
            gameplayEquippedItemId: "weapon.revolver",
            gameplayAcquiredItemId: "");

        Assert.Equal(0f, player.SpyCloakAlpha);
        Assert.Equal(PlayerEntity.SpyBackstabVisualTicksDefault, player.SpyBackstabVisualTicksRemaining);
        Assert.True(player.IsSpyVisibleToEnemies);
    }

    [Fact]
    public void ComputeSpyVisibleToEnemiesRequiresCloakAlphaUnlessBackstabIsAnimating()
    {
        Assert.False(PlayerEntity.ComputeSpyVisibleToEnemies(isSpyCloaked: true, spyCloakAlpha: 0f, spyBackstabVisualTicksRemaining: 0));
        Assert.True(PlayerEntity.ComputeSpyVisibleToEnemies(isSpyCloaked: true, spyCloakAlpha: 0f, spyBackstabVisualTicksRemaining: 12));
        Assert.True(PlayerEntity.ComputeSpyVisibleToEnemies(isSpyCloaked: true, spyCloakAlpha: 0.2f, spyBackstabVisualTicksRemaining: 0));
        Assert.False(PlayerEntity.ComputeSpyVisibleToEnemies(isSpyCloaked: false, spyCloakAlpha: 1f, spyBackstabVisualTicksRemaining: 12));
    }

    [Fact]
    public void ApplyNetworkStateClearsDeadPlayerTransientStateAndClampsReplicatedValues()
    {
        var player = new PlayerEntity(1, CharacterClassCatalog.Scout, "Test");

        player.ApplyNetworkState(
            team: PlayerTeam.Red,
            classDefinition: CharacterClassCatalog.Sniper,
            isAlive: false,
            x: 10f,
            y: 20f,
            horizontalSpeed: 1f,
            verticalSpeed: 2f,
            health: 999,
            currentShells: 999,
            kills: -3,
            deaths: -2,
            caps: -1,
            points: -5f,
            healPoints: -4,
            activeDominationCount: -1,
            isDominatingLocalViewer: true,
            isDominatedByLocalViewer: true,
            metal: 999f,
            isGrounded: false,
            remainingAirJumps: 99,
            isCarryingIntel: true,
            intelRechargeTicks: 999f,
            isSpyCloaked: true,
            spyCloakAlpha: 5f,
            isSpySuperjumping: false,
            spySuperjumpCooldownTicksRemaining: 0,
            spySuperjumpHorizontalVelocity: 0f,
            spyBackstabVisualTicksRemaining: 0,
            isUbered: true,
            isKritzCritBoosted: false,
            isHeavyEating: true,
            heavyEatTicksRemaining: 50,
            isSniperScoped: true,
            sniperChargeTicks: 80,
            isUsingBinoculars: false,
            binocularsFocusX: 0f,
            binocularsFocusY: 0f,
            facingDirectionX: -1f,
            aimDirectionDegrees: 270f,
            aimWorldX: 0f,
            aimWorldY: 0f,
            isTaunting: true,
            tauntFrameIndex: 2f,
            isChatBubbleVisible: true,
            chatBubbleFrameIndex: 3,
            chatBubbleAlpha: 2f,
            burnIntensity: 99f,
            burnDurationSourceTicks: 200f,
            burnDecayDelaySourceTicksRemaining: 50f,
            burnIntensityDecayPerSourceTick: 4f,
            burnedByPlayerId: 7,
            primaryCooldownTicks: 20,
            reloadTicksUntilNextShell: 30,
            gameplayModPackId: "stock.gg2",
            gameplayLoadoutId: "sniper.stock",
            gameplayPrimaryItemId: "weapon.rifle",
            gameplaySecondaryItemId: "ability.sniper-scope",
            gameplayUtilityItemId: "",
            gameplayEquippedSlot: (byte)GameplayEquipmentSlot.Primary,
            gameplayEquippedItemId: "weapon.rifle",
            gameplayAcquiredItemId: "");

        Assert.False(player.IsAlive);
        Assert.Equal(0, player.Health);
        Assert.Equal(0, player.PrimaryCooldownTicks);
        Assert.Equal(0, player.ReloadTicksUntilNextShell);
        Assert.False(player.IsCarryingIntel);
        Assert.Equal(0f, player.IntelRechargeTicks);
        Assert.False(player.IsSniperScoped);
        Assert.Equal(0, player.SniperChargeTicks);
        Assert.False(player.IsBurning);
        Assert.Equal(0f, player.BurnIntensity);
        Assert.Null(player.BurnedByPlayerId);
        Assert.Equal(player.MaxMetal, player.Metal);
        Assert.Equal(0, player.Kills);
        Assert.Equal(0, player.Deaths);
        Assert.Equal(0, player.Caps);
        Assert.Equal(0f, player.Points);
        Assert.Equal(0, player.HealPoints);
        Assert.Equal(0, player.ActiveDominationCount);
    }

    [Fact]
    public void ApplyNetworkStateRejectsInvalidReplicatedStateIdentifiersFromSnapshot()
    {
        var player = new PlayerEntity(1, CharacterClassCatalog.Scout, "Test");

        player.ApplyNetworkState(
            team: PlayerTeam.Red,
            classDefinition: CharacterClassCatalog.Scout,
            isAlive: true,
            x: 10f,
            y: 20f,
            horizontalSpeed: 0f,
            verticalSpeed: 0f,
            health: 125,
            currentShells: 6,
            kills: 0,
            deaths: 0,
            caps: 0,
            points: 0f,
            healPoints: 0,
            activeDominationCount: 0,
            isDominatingLocalViewer: false,
            isDominatedByLocalViewer: false,
            metal: 0f,
            isGrounded: true,
            remainingAirJumps: 0,
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
            facingDirectionX: 1f,
            aimDirectionDegrees: 0f,
            aimWorldX: 0f,
            aimWorldY: 0f,
            isTaunting: false,
            tauntFrameIndex: 0f,
            isChatBubbleVisible: false,
            chatBubbleFrameIndex: 0,
            chatBubbleAlpha: 0f,
            replicatedStateEntries:
            [
                new GameplayReplicatedStateEntry(" plugin.score ", " round_wins ", GameplayReplicatedStateValueKind.Whole, IntValue: 4),
                new GameplayReplicatedStateEntry("plugin:bad", "value", GameplayReplicatedStateValueKind.Toggle, BoolValue: true),
                new GameplayReplicatedStateEntry("plugin.score", "bad key", GameplayReplicatedStateValueKind.Toggle, BoolValue: true),
            ]);

        var replicatedEntries = player.GetReplicatedStateEntries();
        var entry = Assert.Single(replicatedEntries);
        Assert.Equal("plugin.score", entry.OwnerId);
        Assert.Equal("round_wins", entry.Key);
        Assert.Equal(4, entry.IntValue);
    }

    [Fact]
    public void ApplyNetworkStateUsesReplicatedHeavyDashStateForOnlineVisibility()
    {
        var player = new PlayerEntity(1, CharacterClassCatalog.Heavy, "Test");

        ApplyHeavyNetworkSnapshot(
            player,
            [
                new GameplayReplicatedStateEntry(
                    GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId,
                    GameplayAbilityReplicatedState.HeavyDashActiveKey,
                    GameplayReplicatedStateValueKind.Toggle,
                    BoolValue: true),
                new GameplayReplicatedStateEntry(
                    GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId,
                    GameplayAbilityReplicatedState.HeavyDashVisibleKey,
                    GameplayReplicatedStateValueKind.Toggle,
                    BoolValue: true),
                new GameplayReplicatedStateEntry(
                    GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId,
                    GameplayAbilityReplicatedState.HeavyDashTrailAlphaKey,
                    GameplayReplicatedStateValueKind.Scalar,
                    FloatValue: 0.4f),
            ]);

        Assert.True(player.IsExperimentalGhostDashing);
        Assert.True(player.IsExperimentalGhostDashVisible);
        Assert.Equal(0.4f, player.ExperimentalGhostDashTrailAlpha);

        ApplyHeavyNetworkSnapshot(
            player,
            [
                new GameplayReplicatedStateEntry(
                    GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId,
                    GameplayAbilityReplicatedState.HeavyDashActiveKey,
                    GameplayReplicatedStateValueKind.Toggle,
                    BoolValue: false),
                new GameplayReplicatedStateEntry(
                    GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId,
                    GameplayAbilityReplicatedState.HeavyDashVisibleKey,
                    GameplayReplicatedStateValueKind.Toggle,
                    BoolValue: true),
                new GameplayReplicatedStateEntry(
                    GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId,
                    GameplayAbilityReplicatedState.HeavyDashTrailAlphaKey,
                    GameplayReplicatedStateValueKind.Scalar,
                    FloatValue: 0.25f),
            ]);

        Assert.False(player.IsExperimentalGhostDashing);
        Assert.True(player.IsExperimentalGhostDashVisible);
        Assert.Equal(0.25f, player.ExperimentalGhostDashTrailAlpha);
    }

    [Fact]
    public void ApplyNetworkStateHydratesReplicatedHeavyDashCooldownForHud()
    {
        var player = new PlayerEntity(1, CharacterClassCatalog.Heavy, "Test");

        ApplyHeavyNetworkSnapshot(
            player,
            [
                new GameplayReplicatedStateEntry(
                    GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId,
                    GameplayAbilityReplicatedState.HeavyDashCooldownTicksKey,
                    GameplayReplicatedStateValueKind.Whole,
                    IntValue: 42),
            ]);

        Assert.Equal(42, player.ExperimentalGhostDashCooldownTicksRemaining);
        Assert.True(GameplayAbilityReplicatedState.TryGetInt(
            player,
            GameplayAbilityReplicatedState.HeavyDashCooldownTicksKey,
            out var cooldownTicks));
        Assert.Equal(42, cooldownTicks);
    }

    [Fact]
    public void ApplyNetworkStateHydratesReplicatedCivvieRuntimeStateForOnlineVisuals()
    {
        var player = new PlayerEntity(1, CharacterClassCatalog.Civilian, "Test");

        ApplyCivilianNetworkSnapshot(
            player,
            [
                new GameplayReplicatedStateEntry(
                    GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId,
                    GameplayAbilityReplicatedState.CivvieUmbrellaCooldownTicksKey,
                    GameplayReplicatedStateValueKind.Whole,
                    IntValue: 45),
                new GameplayReplicatedStateEntry(
                    GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId,
                    GameplayAbilityReplicatedState.CivvieUmbrellaActiveKey,
                    GameplayReplicatedStateValueKind.Toggle,
                    BoolValue: true),
                new GameplayReplicatedStateEntry(
                    GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId,
                    GameplayAbilityReplicatedState.CivvieUmbrellaDisabledKey,
                    GameplayReplicatedStateValueKind.Toggle,
                    BoolValue: false),
                new GameplayReplicatedStateEntry(
                    GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId,
                    GameplayAbilityReplicatedState.CivviePogoActiveKey,
                    GameplayReplicatedStateValueKind.Toggle,
                    BoolValue: false),
                new GameplayReplicatedStateEntry(
                    GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId,
                    GameplayAbilityReplicatedState.CivviePogoCrunchTicksKey,
                    GameplayReplicatedStateValueKind.Whole,
                    IntValue: 0),
            ]);

        Assert.True(player.IsCivvieUmbrellaActive);
        Assert.Equal(PlayerEntity.CivvieUmbrellaMaxChargeTicks - 45, player.CivvieUmbrellaChargeTicks);
        Assert.False(player.IsCivvieUmbrellaDisabled);
        Assert.False(player.IsCivviePogoActive);

        ApplyCivilianNetworkSnapshot(
            player,
            [
                new GameplayReplicatedStateEntry(
                    GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId,
                    GameplayAbilityReplicatedState.CivvieUmbrellaCooldownTicksKey,
                    GameplayReplicatedStateValueKind.Whole,
                    IntValue: PlayerEntity.CivvieUmbrellaMaxChargeTicks),
                new GameplayReplicatedStateEntry(
                    GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId,
                    GameplayAbilityReplicatedState.CivvieUmbrellaActiveKey,
                    GameplayReplicatedStateValueKind.Toggle,
                    BoolValue: false),
                new GameplayReplicatedStateEntry(
                    GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId,
                    GameplayAbilityReplicatedState.CivvieUmbrellaDisabledKey,
                    GameplayReplicatedStateValueKind.Toggle,
                    BoolValue: true),
                new GameplayReplicatedStateEntry(
                    GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId,
                    GameplayAbilityReplicatedState.CivviePogoActiveKey,
                    GameplayReplicatedStateValueKind.Toggle,
                    BoolValue: true),
                new GameplayReplicatedStateEntry(
                    GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId,
                    GameplayAbilityReplicatedState.CivviePogoCrunchTicksKey,
                    GameplayReplicatedStateValueKind.Whole,
                    IntValue: 2),
                new GameplayReplicatedStateEntry(
                    GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId,
                    GameplayAbilityReplicatedState.CivviePogoTrickTicksKey,
                    GameplayReplicatedStateValueKind.Whole,
                    IntValue: 11),
                new GameplayReplicatedStateEntry(
                    GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId,
                    GameplayAbilityReplicatedState.CivviePogoTrickDurationTicksKey,
                    GameplayReplicatedStateValueKind.Whole,
                    IntValue: 18),
            ]);

        Assert.False(player.IsCivvieUmbrellaActive);
        Assert.True(player.IsCivvieUmbrellaDisabled);
        Assert.True(player.IsCivviePogoActive);
        Assert.Equal(2, player.CivviePogoCrunchTicksRemaining);
        Assert.Equal(11, player.CivviePogoTrickTicksRemaining);
        Assert.Equal(18, player.CivviePogoTrickDurationAtStart);

        // A delta snapshot omits expired ability entries.  The client must clear
        // the old trick state instead of replaying the previous taunt frame.
        ApplyCivilianNetworkSnapshot(player, []);

        Assert.Equal(0, player.CivviePogoTrickTicksRemaining);
        Assert.Equal(0, player.CivviePogoTrickDurationAtStart);
    }

    [Fact]
    public void ApplyNetworkStateIgnoresStaleReplicatedHeavyDashStateAfterClassChange()
    {
        var player = new PlayerEntity(1, CharacterClassCatalog.Heavy, "Test");
        var staleHeavyDashState = new[]
        {
            new GameplayReplicatedStateEntry(
                GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId,
                GameplayAbilityReplicatedState.HeavyDashActiveKey,
                GameplayReplicatedStateValueKind.Toggle,
                BoolValue: true),
            new GameplayReplicatedStateEntry(
                GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId,
                GameplayAbilityReplicatedState.HeavyDashVisibleKey,
                GameplayReplicatedStateValueKind.Toggle,
                BoolValue: true),
            new GameplayReplicatedStateEntry(
                GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId,
                GameplayAbilityReplicatedState.HeavyDashTrailAlphaKey,
                GameplayReplicatedStateValueKind.Scalar,
                FloatValue: 0.5f),
        };

        ApplyHeavyNetworkSnapshot(player, staleHeavyDashState);
        ApplySoldierNetworkSnapshot(
            player,
            GameplayEquipmentSlot.Primary,
            includeFullLoadoutState: true,
            replicatedStateEntries: staleHeavyDashState);

        Assert.Equal(PlayerClass.Soldier, player.ClassId);
        Assert.False(player.IsExperimentalGhostDashing);
        Assert.False(player.IsExperimentalGhostDashVisible);
        Assert.Equal(0f, player.ExperimentalGhostDashTrailAlpha);
    }

    [Fact]
    public void ExperimentalMetalConfigurationUpdatesCapacityAndPassiveRegeneration()
    {
        var player = new PlayerEntity(1, CharacterClassCatalog.Engineer, "Test");
        player.Spawn(PlayerTeam.Red, 0f, 0f);
        Assert.True(player.SpendMetal(70f));

        player.ConfigureExperimentalMetal(150f, 0.25f);
        Assert.Equal(150f, player.MaxMetal);
        Assert.Equal(0.25f, player.PassiveMetalRegenerationPerTick);

        player.AdvanceEngineerResources();
        Assert.Equal(30.25f, player.Metal, 3);

        player.AddMetal(500f);
        Assert.Equal(player.MaxMetal, player.Metal);

        player.ConfigureExperimentalMetal(80f, 0.1f);
        Assert.Equal(80f, player.MaxMetal);
        Assert.Equal(80f, player.Metal);
    }

    [Fact]
    public void AdvanceAfterburnVisualDecaysBurnWithoutApplyingDamage()
    {
        var player = new PlayerEntity(1, CharacterClassCatalog.Scout, "Test");
        player.Spawn(PlayerTeam.Red, 0f, 0f);
        player.IgniteAfterburn(2, 60f, 6f, afterburnFalloff: false, burnFalloffAmount: 0f);
        var startingHealth = player.Health;
        var startingIntensity = player.BurnIntensity;
        var startingDuration = player.BurnDurationSourceTicks;

        player.AdvanceAfterburnVisual(1f / 60f);

        Assert.Equal(startingHealth, player.Health);
        Assert.True(player.IsBurning);
        Assert.Equal(startingIntensity, player.BurnIntensity);
        Assert.True(player.BurnDurationSourceTicks < startingDuration);
    }

    [Fact]
    public void BurnVisualCountUsesIntensityWhenDurationIsMissingFromSnapshot()
    {
        var player = new PlayerEntity(1, CharacterClassCatalog.Soldier, "Test");

        player.ApplyNetworkState(
            team: PlayerTeam.Red,
            classDefinition: CharacterClassCatalog.Soldier,
            isAlive: true,
            x: 10f,
            y: 20f,
            horizontalSpeed: 0f,
            verticalSpeed: 0f,
            health: 200,
            currentShells: 4,
            kills: 0,
            deaths: 0,
            caps: 0,
            points: 0f,
            healPoints: 0,
            activeDominationCount: 0,
            isDominatingLocalViewer: false,
            isDominatedByLocalViewer: false,
            metal: 50f,
            isGrounded: true,
            remainingAirJumps: 0,
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
            facingDirectionX: 1f,
            aimDirectionDegrees: 0f,
            aimWorldX: 0f,
            aimWorldY: 0f,
            isTaunting: false,
            tauntFrameIndex: 0f,
            isChatBubbleVisible: false,
            chatBubbleFrameIndex: 0,
            chatBubbleAlpha: 0f,
            burnIntensity: 4f,
            burnDurationSourceTicks: 0f,
            burnDecayDelaySourceTicksRemaining: 45f,
            burnIntensityDecayPerSourceTick: 0f,
            burnedByPlayerId: 2);

        Assert.True(player.IsBurning);
        Assert.True(player.BurnVisualCount > 0);

        player.AdvanceAfterburnVisual(1f / 60f);

        Assert.True(player.IsBurning);
        Assert.True(player.BurnVisualCount > 0);
    }

    private static void ApplyReplicatedAmmoSnapshot(
        PlayerEntity player,
        CharacterClassDefinition classDefinition,
        string ammoKey,
        int? ammo)
    {
        player.ApplyNetworkState(
            team: PlayerTeam.Red,
            classDefinition: classDefinition,
            isAlive: true,
            x: 10f,
            y: 20f,
            horizontalSpeed: 0f,
            verticalSpeed: 0f,
            health: 100,
            currentShells: 1,
            kills: 0,
            deaths: 0,
            caps: 0,
            points: 0f,
            healPoints: 0,
            activeDominationCount: 0,
            isDominatingLocalViewer: false,
            isDominatedByLocalViewer: false,
            metal: 0f,
            isGrounded: true,
            remainingAirJumps: 0,
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
            facingDirectionX: 1f,
            aimDirectionDegrees: 0f,
            aimWorldX: 100f,
            aimWorldY: 20f,
            isTaunting: false,
            tauntFrameIndex: 0f,
            isChatBubbleVisible: false,
            chatBubbleFrameIndex: 0,
            chatBubbleAlpha: 0f,
            replicatedStateEntries: ammo.HasValue
                ? [new GameplayReplicatedStateEntry(
                    "core.player",
                    ammoKey,
                    GameplayReplicatedStateValueKind.Whole,
                    ammo.Value,
                    0f,
                    false)]
                : []);
    }

    private static void ApplySoldierNetworkSnapshot(
        PlayerEntity player,
        GameplayEquipmentSlot equippedSlot,
        bool includeFullLoadoutState,
        GameplayReplicatedStateEntry[]? replicatedStateEntries = null,
        string acquiredItemId = "",
        string? equippedItemIdOverride = null,
        int networkMaxHealth = 0)
    {
        var equippedItemId = equippedSlot == GameplayEquipmentSlot.Secondary
            ? "weapon.soldier-shotgun"
            : "weapon.rocketlauncher";
        equippedItemId = equippedItemIdOverride ?? equippedItemId;

        player.ApplyNetworkState(
            team: PlayerTeam.Red,
            classDefinition: CharacterClassCatalog.Soldier,
            isAlive: true,
            x: 10f,
            y: 20f,
            horizontalSpeed: 0f,
            verticalSpeed: 0f,
            health: 200,
            currentShells: 4,
            kills: 0,
            deaths: 0,
            caps: 0,
            points: 0f,
            healPoints: 0,
            activeDominationCount: 0,
            isDominatingLocalViewer: false,
            isDominatedByLocalViewer: false,
            metal: 50f,
            isGrounded: true,
            remainingAirJumps: 0,
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
            facingDirectionX: 1f,
            aimDirectionDegrees: 0f,
            aimWorldX: 106f,
            aimWorldY: 20f,
            isTaunting: false,
            tauntFrameIndex: 0f,
            isChatBubbleVisible: false,
            chatBubbleFrameIndex: 0,
            chatBubbleAlpha: 0f,
            gameplayModPackId: includeFullLoadoutState ? "stock.gg2" : "",
            gameplayLoadoutId: includeFullLoadoutState ? "soldier.stock" : "",
            gameplayPrimaryItemId: includeFullLoadoutState ? "weapon.rocketlauncher" : "",
            gameplaySecondaryItemId: includeFullLoadoutState ? "weapon.soldier-shotgun" : "",
            gameplayUtilityItemId: "",
            gameplayEquippedSlot: (byte)equippedSlot,
            gameplayEquippedItemId: includeFullLoadoutState ? equippedItemId : "",
            gameplayAcquiredItemId: acquiredItemId,
            replicatedStateEntries: replicatedStateEntries,
            networkMaxHealth: networkMaxHealth);
    }

    private static void ApplySniperNetworkSnapshot(
        PlayerEntity player,
        string primaryItemId,
        GameplayReplicatedStateEntry[]? replicatedStateEntries = null)
    {
        player.ApplyNetworkState(
            team: PlayerTeam.Red,
            classDefinition: CharacterClassCatalog.Sniper,
            isAlive: true,
            x: 10f,
            y: 20f,
            horizontalSpeed: 0f,
            verticalSpeed: 0f,
            health: 120,
            currentShells: 1,
            kills: 0,
            deaths: 0,
            caps: 0,
            points: 0f,
            healPoints: 0,
            activeDominationCount: 0,
            isDominatingLocalViewer: false,
            isDominatedByLocalViewer: false,
            metal: 0f,
            isGrounded: true,
            remainingAirJumps: 0,
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
            facingDirectionX: 1f,
            aimDirectionDegrees: 0f,
            aimWorldX: 106f,
            aimWorldY: 20f,
            isTaunting: false,
            tauntFrameIndex: 0f,
            isChatBubbleVisible: false,
            chatBubbleFrameIndex: 0,
            chatBubbleAlpha: 0f,
            gameplayModPackId: "stock.gg2",
            gameplayLoadoutId: "sniper.stock",
            gameplayPrimaryItemId: primaryItemId,
            gameplaySecondaryItemId: "",
            gameplayUtilityItemId: "",
            gameplayEquippedSlot: (byte)GameplayEquipmentSlot.Primary,
            gameplayEquippedItemId: primaryItemId,
            gameplayAcquiredItemId: "",
            replicatedStateEntries: replicatedStateEntries);
    }

    private static void ApplyMedicNetworkSnapshot(
        PlayerEntity player,
        string primaryItemId,
        GameplayReplicatedStateEntry[]? replicatedStateEntries = null)
    {
        player.ApplyNetworkState(
            team: PlayerTeam.Red,
            classDefinition: CharacterClassCatalog.Medic,
            isAlive: true,
            x: 10f,
            y: 20f,
            horizontalSpeed: 0f,
            verticalSpeed: 0f,
            health: 120,
            currentShells: 1,
            kills: 0,
            deaths: 0,
            caps: 0,
            points: 0f,
            healPoints: 0,
            activeDominationCount: 0,
            isDominatingLocalViewer: false,
            isDominatedByLocalViewer: false,
            metal: 0f,
            isGrounded: true,
            remainingAirJumps: 0,
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
            facingDirectionX: 1f,
            aimDirectionDegrees: 0f,
            aimWorldX: 106f,
            aimWorldY: 20f,
            isTaunting: false,
            tauntFrameIndex: 0f,
            isChatBubbleVisible: false,
            chatBubbleFrameIndex: 0,
            chatBubbleAlpha: 0f,
            gameplayModPackId: "stock.gg2",
            gameplayLoadoutId: "medic.stock",
            gameplayPrimaryItemId: primaryItemId,
            gameplaySecondaryItemId: "",
            gameplayUtilityItemId: "",
            gameplayEquippedSlot: (byte)GameplayEquipmentSlot.Primary,
            gameplayEquippedItemId: primaryItemId,
            gameplayAcquiredItemId: "",
            replicatedStateEntries: replicatedStateEntries);
    }

    private static void ApplyHeavyNetworkSnapshot(
        PlayerEntity player,
        GameplayReplicatedStateEntry[] replicatedStateEntries)
    {
        player.ApplyNetworkState(
            team: PlayerTeam.Red,
            classDefinition: CharacterClassCatalog.Heavy,
            isAlive: true,
            x: 10f,
            y: 20f,
            horizontalSpeed: 0f,
            verticalSpeed: 0f,
            health: 200,
            currentShells: 200,
            kills: 0,
            deaths: 0,
            caps: 0,
            points: 0f,
            healPoints: 0,
            activeDominationCount: 0,
            isDominatingLocalViewer: false,
            isDominatedByLocalViewer: false,
            metal: 0f,
            isGrounded: true,
            remainingAirJumps: 0,
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
            facingDirectionX: 1f,
            aimDirectionDegrees: 0f,
            aimWorldX: 106f,
            aimWorldY: 20f,
            isTaunting: false,
            tauntFrameIndex: 0f,
            isChatBubbleVisible: false,
            chatBubbleFrameIndex: 0,
            chatBubbleAlpha: 0f,
            gameplayModPackId: "stock.gg2",
            gameplayLoadoutId: "heavy.stock",
            gameplayPrimaryItemId: "weapon.minigun",
            gameplaySecondaryItemId: "ability.heavy-sandvich",
            gameplayUtilityItemId: "ability.heavy-utility",
            gameplayEquippedSlot: (byte)GameplayEquipmentSlot.Primary,
            gameplayEquippedItemId: "weapon.minigun",
            gameplayAcquiredItemId: "",
            replicatedStateEntries: replicatedStateEntries);
    }

    private static void ApplyCivilianNetworkSnapshot(
        PlayerEntity player,
        GameplayReplicatedStateEntry[] replicatedStateEntries)
    {
        player.ApplyNetworkState(
            team: PlayerTeam.Red,
            classDefinition: CharacterClassCatalog.Civilian,
            isAlive: true,
            x: 10f,
            y: 20f,
            horizontalSpeed: 0f,
            verticalSpeed: 0f,
            health: 140,
            currentShells: 6,
            kills: 0,
            deaths: 0,
            caps: 0,
            points: 0f,
            healPoints: 0,
            activeDominationCount: 0,
            isDominatingLocalViewer: false,
            isDominatedByLocalViewer: false,
            metal: 0f,
            isGrounded: true,
            remainingAirJumps: 0,
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
            facingDirectionX: 1f,
            aimDirectionDegrees: 0f,
            aimWorldX: 106f,
            aimWorldY: 20f,
            isTaunting: false,
            tauntFrameIndex: 0f,
            isChatBubbleVisible: false,
            chatBubbleFrameIndex: 0,
            chatBubbleAlpha: 0f,
            gameplayModPackId: "stock.gg2",
            gameplayLoadoutId: "civilian.stock",
            gameplayPrimaryItemId: "weapon.umbrella",
            gameplaySecondaryItemId: "ability.umbrella",
            gameplayUtilityItemId: "ability.civilian-pogo",
            gameplayEquippedSlot: (byte)GameplayEquipmentSlot.Primary,
            gameplayEquippedItemId: "weapon.umbrella",
            gameplayAcquiredItemId: "",
            replicatedStateEntries: replicatedStateEntries);
    }

    private static GameplayReplicatedStateEntry WholeGameplayState(string key, int value)
        => new(
            GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId,
            key,
            GameplayReplicatedStateValueKind.Whole,
            IntValue: value);
}
