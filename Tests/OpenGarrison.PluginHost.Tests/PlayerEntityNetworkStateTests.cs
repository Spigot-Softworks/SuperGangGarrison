using OpenGarrison.Core;
using OpenGarrison.GameplayModding;
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
            isUbered: false,            isKritzCritBoosted: false,            isHeavyEating: false,
            heavyEatTicksRemaining: 0,
            isSniperScoped: false,
            sniperChargeTicks: 0,
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
            isUbered: false,
            isKritzCritBoosted: false,
            isHeavyEating: false,
            heavyEatTicksRemaining: 0,
            isSniperScoped: false,
            sniperChargeTicks: 0,
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
            gameplaySecondaryItemId: "ability.heavy-sandvich",
            gameplayUtilityItemId: "",
            gameplayEquippedSlot: (byte)GameplayEquipmentSlot.Secondary,
            gameplayEquippedItemId: "ability.heavy-sandvich",
            gameplayAcquiredItemId: "");

        Assert.Equal(PlayerClass.Heavy, player.ClassId);
        Assert.Equal("heavy.stock", player.SelectedGameplayLoadoutId);
        Assert.Equal("heavy.stock", player.GameplayLoadoutState.LoadoutId);
        Assert.Equal("ability.heavy-sandvich", player.GameplayLoadoutState.SecondaryItemId);
        Assert.Equal(GameplayEquipmentSlot.Secondary, player.SelectedGameplayEquippedSlot);
        Assert.Equal(GameplayEquipmentSlot.Secondary, player.GameplayLoadoutState.EquippedSlot);
        Assert.Equal("ability.heavy-sandvich", player.GameplayLoadoutState.EquippedItemId);
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
            isUbered: false,
            isKritzCritBoosted: false,
            isHeavyEating: false,
            heavyEatTicksRemaining: 0,
            isSniperScoped: false,
            sniperChargeTicks: 0,
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
            isUbered: true,
            isKritzCritBoosted: false,
            isHeavyEating: true,
            heavyEatTicksRemaining: 50,
            isSniperScoped: true,
            sniperChargeTicks: 80,
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
            isUbered: false,
            isKritzCritBoosted: false,
            isHeavyEating: false,
            heavyEatTicksRemaining: 0,
            isSniperScoped: false,
            sniperChargeTicks: 0,
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
            isUbered: false,
            isKritzCritBoosted: false,
            isHeavyEating: false,
            heavyEatTicksRemaining: 0,
            isSniperScoped: false,
            sniperChargeTicks: 0,
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
}
