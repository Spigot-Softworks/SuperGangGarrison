using OpenGarrison.Core;
using OpenGarrison.GameplayModding;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class SimulationWorldNetworkPlayerConfigurationTests
{
    [Fact]
    public void NetworkPlayerSlotRangeSupportsFortyPlayableSlots()
    {
        Assert.Equal(40, SimulationWorld.MaxPlayableNetworkPlayers);
        Assert.Equal(SimulationWorld.MaxPlayableNetworkPlayers, SimulationWorld.NetworkPlayerSlots.Count);
        Assert.Equal(SimulationWorld.LocalPlayerSlot, SimulationWorld.NetworkPlayerSlots[0]);
        Assert.Equal((byte)SimulationWorld.MaxPlayableNetworkPlayers, SimulationWorld.NetworkPlayerSlots[^1]);
        Assert.True(SimulationWorld.IsPlayableNetworkPlayerSlot((byte)SimulationWorld.MaxPlayableNetworkPlayers));
        Assert.False(SimulationWorld.IsPlayableNetworkPlayerSlot((byte)(SimulationWorld.MaxPlayableNetworkPlayers + 1)));
        Assert.False(SimulationWorld.IsPlayableNetworkPlayerSlot(SimulationWorld.FirstSpectatorSlot));

        var world = new SimulationWorld();

        Assert.True(world.TryPrepareNetworkPlayerJoin((byte)SimulationWorld.MaxPlayableNetworkPlayers));
        Assert.False(world.TryPrepareNetworkPlayerJoin((byte)(SimulationWorld.MaxPlayableNetworkPlayers + 1)));
        Assert.False(world.TryPrepareNetworkPlayerJoin(SimulationWorld.FirstSpectatorSlot));
    }

    [Fact]
    public void TrySetNetworkPlayerGameplayLoadoutUpdatesLocalPlayerAuthoritativeState()
    {
        var world = CreateWorldWithLocalClass(PlayerClass.Soldier);

        var changed = world.TrySetNetworkPlayerGameplayLoadout(SimulationWorld.LocalPlayerSlot, "soldier.black-box");

        Assert.True(changed);
        Assert.Equal("soldier.black-box", world.LocalPlayer.SelectedGameplayLoadoutId);
        Assert.Equal("soldier.black-box", world.LocalPlayer.GameplayLoadoutState.LoadoutId);
        Assert.Equal("weapon.blackbox", world.LocalPlayer.GameplayLoadoutState.PrimaryItemId);
        Assert.Equal(GameplayEquipmentSlot.Primary, world.LocalPlayer.GameplayLoadoutState.EquippedSlot);
        Assert.Equal("weapon.blackbox", world.LocalPlayer.GameplayLoadoutState.EquippedItemId);
    }

    [Fact]
    public void TrySetNetworkPlayerGameplayLoadoutCreatesAndUpdatesRemotePlayerState()
    {
        var world = new SimulationWorld();

        Assert.True(world.TryPrepareNetworkPlayerJoin(2));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(2, PlayerClass.Soldier));

        var changed = world.TrySetNetworkPlayerGameplayLoadout(2, "soldier.direct-hit");

        Assert.True(changed);
        Assert.True(world.TryGetNetworkPlayer(2, out var remotePlayer));
        Assert.Equal(PlayerClass.Soldier, remotePlayer.ClassId);
        Assert.Equal("soldier.direct-hit", remotePlayer.SelectedGameplayLoadoutId);
        Assert.Equal("soldier.direct-hit", remotePlayer.GameplayLoadoutState.LoadoutId);
        Assert.Equal("weapon.directhit", remotePlayer.GameplayLoadoutState.PrimaryItemId);
        Assert.Equal("weapon.directhit", remotePlayer.GameplayLoadoutState.EquippedItemId);
    }

    [Fact]
    public void RemoteNetworkJoinAppliesStockSecondaryAndUtilityLoadout()
    {
        var world = new SimulationWorld();

        Assert.True(world.TryPrepareNetworkPlayerJoin(2));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(2, PlayerClass.Soldier));

        Assert.True(world.TryGetNetworkPlayer(2, out var remotePlayer));
        Assert.Equal("soldier.stock", remotePlayer.SelectedGameplayLoadoutId);
        Assert.Equal("weapon.soldier-shotgun", remotePlayer.GameplayLoadoutState.SecondaryItemId);
        Assert.Equal("ability.soldier-utility", remotePlayer.GameplayLoadoutState.UtilityItemId);
        Assert.True(remotePlayer.HasExperimentalOffhandWeapon);
        Assert.Equal(remotePlayer.ExperimentalOffhandMaxShells, remotePlayer.ExperimentalOffhandCurrentShells);
    }

    [Fact]
    public void NetworkPlayerClassSelectionAcceptsGameplayClassId()
    {
        var world = new SimulationWorld();
        var gameplayClassId = CharacterClassCatalog.Soldier.GameplayClassId;

        Assert.True(world.TryPrepareNetworkPlayerJoin(2));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(2, gameplayClassId));

        Assert.True(world.TryGetNetworkPlayer(2, out var remotePlayer));
        Assert.Equal(PlayerClass.Soldier, remotePlayer.ClassId);
        Assert.Equal(gameplayClassId, remotePlayer.GameplayClassId);
        Assert.Equal("soldier.stock", remotePlayer.SelectedGameplayLoadoutId);
        Assert.Equal("weapon.rocketlauncher", remotePlayer.GameplayLoadoutState.PrimaryItemId);
    }

    [Fact]
    public void TrySetNetworkPlayerGameplayEquippedSlotSelectsHeavySecondaryWhenAvailable()
    {
        var world = CreateWorldWithLocalClass(PlayerClass.Heavy);

        var changed = world.TrySetNetworkPlayerGameplayEquippedSlot(SimulationWorld.LocalPlayerSlot, GameplayEquipmentSlot.Secondary);

        Assert.True(changed);
        Assert.Equal(GameplayEquipmentSlot.Secondary, world.LocalPlayer.SelectedGameplayEquippedSlot);
        Assert.Equal(GameplayEquipmentSlot.Secondary, world.LocalPlayer.GameplayLoadoutState.EquippedSlot);
        Assert.Equal("ability.heavy-sandvich", world.LocalPlayer.GameplayLoadoutState.SecondaryItemId);
        Assert.Equal("ability.heavy-sandvich", world.LocalPlayer.GameplayLoadoutState.EquippedItemId);
    }

    [Fact]
    public void TrySetNetworkPlayerGameplayEquippedSlotSelectsSoldierShotgunWhenAvailable()
    {
        var world = CreateWorldWithLocalClass(PlayerClass.Soldier);

        var changed = world.TrySetNetworkPlayerGameplayEquippedSlot(SimulationWorld.LocalPlayerSlot, GameplayEquipmentSlot.Secondary);

        Assert.True(changed);
        Assert.Equal(GameplayEquipmentSlot.Secondary, world.LocalPlayer.SelectedGameplayEquippedSlot);
        Assert.Equal(GameplayEquipmentSlot.Secondary, world.LocalPlayer.GameplayLoadoutState.EquippedSlot);
        Assert.Equal("weapon.soldier-shotgun", world.LocalPlayer.GameplayLoadoutState.SecondaryItemId);
        Assert.Equal("weapon.soldier-shotgun", world.LocalPlayer.GameplayLoadoutState.EquippedItemId);
    }

    [Fact]
    public void TrySetNetworkPlayerGameplayLoadoutRejectsUnknownLoadoutAndPreservesAuthoritativeState()
    {
        var world = CreateWorldWithLocalClass(PlayerClass.Soldier);
        var initialLoadoutId = world.LocalPlayer.SelectedGameplayLoadoutId;
        var initialState = world.LocalPlayer.GameplayLoadoutState;

        var changed = world.TrySetNetworkPlayerGameplayLoadout(SimulationWorld.LocalPlayerSlot, "soldier.not-a-real-loadout");

        Assert.False(changed);
        Assert.Equal(initialLoadoutId, world.LocalPlayer.SelectedGameplayLoadoutId);
        Assert.Equal(initialState, world.LocalPlayer.GameplayLoadoutState);
    }

    [Fact]
    public void RequestedLocalTeamSelectionWithSameClassKillsAndRespawnsAfterDelay()
    {
        var world = CreateWorldWithLocalClass(PlayerClass.Soldier);
        var originalTeam = world.LocalPlayer.Team;
        var oppositeTeam = originalTeam == PlayerTeam.Red ? PlayerTeam.Blue : PlayerTeam.Red;

        Assert.True(world.TryRequestNetworkPlayerTeamSelection(SimulationWorld.LocalPlayerSlot, oppositeTeam));
        Assert.True(world.LocalPlayer.IsAlive);
        Assert.Equal(originalTeam, world.LocalPlayer.Team);

        var changed = world.TrySetLocalClass(PlayerClass.Soldier);

        Assert.True(changed);
        Assert.False(world.LocalPlayer.IsAlive);
        Assert.True(world.GetNetworkPlayerRespawnTicks(SimulationWorld.LocalPlayerSlot) > 1);

        for (var tick = 0; tick < world.Config.TicksPerSecond * 6; tick += 1)
        {
            world.AdvanceOneTick();
        }

        Assert.True(world.LocalPlayer.IsAlive);
        Assert.Equal(oppositeTeam, world.LocalPlayer.Team);
    }

    [Fact]
    public void RequestedRemoteTeamSelectionWaitsForClassConfirmationBeforeDelayedRespawn()
    {
        var world = new SimulationWorld();

        Assert.True(world.TryPrepareNetworkPlayerJoin(2));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(2, PlayerClass.Soldier));
        Assert.True(world.TryGetNetworkPlayer(2, out var player));
        var originalTeam = player.Team;
        var oppositeTeam = originalTeam == PlayerTeam.Red ? PlayerTeam.Blue : PlayerTeam.Red;

        Assert.True(world.TryRequestNetworkPlayerTeamSelection(2, oppositeTeam));

        Assert.True(player.IsAlive);
        Assert.Equal(originalTeam, player.Team);
        Assert.Equal(oppositeTeam, world.GetNetworkPlayerConfiguredTeam(2));

        Assert.True(world.TryApplyNetworkPlayerClassSelection(2, PlayerClass.Soldier));

        Assert.False(player.IsAlive);
        Assert.True(world.GetNetworkPlayerRespawnTicks(2) > 1);

        AdvanceUntilRespawn(world, 2);

        Assert.True(player.IsAlive);
        Assert.Equal(oppositeTeam, player.Team);
    }

    [Fact]
    public void RequestedSameTeamSelectionRespawnsAfterClassConfirmation()
    {
        var world = new SimulationWorld();

        Assert.True(world.TryPrepareNetworkPlayerJoin(2));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(2, PlayerClass.Soldier));
        Assert.True(world.TryGetNetworkPlayer(2, out var player));
        var originalTeam = player.Team;

        Assert.True(world.TryRequestNetworkPlayerTeamSelection(2, originalTeam));
        Assert.True(player.IsAlive);

        Assert.True(world.TryApplyNetworkPlayerClassSelection(2, PlayerClass.Soldier));

        Assert.False(player.IsAlive);
        Assert.True(world.GetNetworkPlayerRespawnTicks(2) > 1);

        AdvanceUntilRespawn(world, 2);

        Assert.True(player.IsAlive);
        Assert.Equal(originalTeam, player.Team);
        Assert.Equal(PlayerClass.Soldier, player.ClassId);
    }

    [Fact]
    public void TrySetLocalClassOutsideSpawnSpawnsCorpseForClassChange()
    {
        var world = CreateWorldWithLocalClass(PlayerClass.Soldier);
        world.ForceRespawnLocalPlayer();
        _ = world.DrainPendingSoundEvents();
        world.LocalPlayer.TeleportTo(512f, 256f);
        world.LocalPlayer.SetSpawnRoomState(false);

        var changed = world.TrySetLocalClass(PlayerClass.Scout);

        Assert.True(changed);
        Assert.False(world.LocalPlayer.IsAlive);
        Assert.Single(world.DeadBodies);
        Assert.Empty(world.PlayerGibs);
        Assert.DoesNotContain(world.PendingSoundEvents, soundEvent => soundEvent.SoundName == "Gibbing");
        Assert.Contains(world.PendingSoundEvents, soundEvent => soundEvent.SoundName is "DeathSnd1" or "DeathSnd2");
    }

    [Fact]
    public void TrySetLocalClassInSpawnDoesNotSpawnCorpseOrDeathSound()
    {
        var world = CreateWorldWithLocalClass(PlayerClass.Soldier);
        world.LocalPlayer.SetSpawnRoomState(true);

        var changed = world.TrySetLocalClass(PlayerClass.Scout);

        Assert.True(changed);
        Assert.False(world.LocalPlayer.IsAlive);
        Assert.Empty(world.DeadBodies);
        Assert.Empty(world.PlayerGibs);
        Assert.DoesNotContain(world.PendingSoundEvents, soundEvent => soundEvent.SoundName == "Gibbing");
        Assert.DoesNotContain(world.PendingSoundEvents, soundEvent => soundEvent.SoundName is "DeathSnd1" or "DeathSnd2");
    }

    [Fact]
    public void TrySetLocalClassInSpawnPlaysRespawnSoundAfterRespawn()
    {
        var world = CreateWorldWithLocalClass(PlayerClass.Soldier);
        world.LocalPlayer.SetSpawnRoomState(true);

        Assert.True(world.TrySetLocalClass(PlayerClass.Scout));

        world.AdvanceOneTick();

        Assert.True(world.LocalPlayer.IsAlive);
        Assert.Equal(PlayerClass.Scout, world.LocalPlayer.ClassId);
        Assert.Contains(world.PendingSoundEvents, soundEvent => soundEvent.SoundName == "RespawnSnd");
    }

    [Fact]
    public void TimedRespawnAfterDeathPlaysRespawnSound()
    {
        var world = CreateWorldWithLocalClass(PlayerClass.Soldier);
        world.LocalPlayer.SetSpawnRoomState(false);

        world.ForceKillLocalPlayer();
        _ = world.DrainPendingSoundEvents();

        for (var tick = 0; tick < world.Config.TicksPerSecond * 6 && !world.LocalPlayer.IsAlive; tick += 1)
        {
            world.AdvanceOneTick();
        }

        Assert.True(world.LocalPlayer.IsAlive);
        Assert.Contains(world.PendingSoundEvents, soundEvent => soundEvent.SoundName == "RespawnSnd");
    }

    [Fact]
    public void ChangingLocalTeamDoesNotRespawnOtherJoinedPlayers()
    {
        var world = CreateWorldWithLocalClass(PlayerClass.Soldier);

        Assert.True(world.TryPrepareNetworkPlayerJoin(2));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(2, PlayerClass.Scout));
        Assert.True(world.TryGetNetworkPlayer(2, out var remotePlayer));

        var expectedHealth = Math.Max(1, remotePlayer.MaxHealth - 5);
        remotePlayer.ForceSetHealth(expectedHealth);
        var expectedPositionX = remotePlayer.X;
        var expectedPositionY = remotePlayer.Y;

        var oppositeTeam = world.LocalPlayer.Team == PlayerTeam.Red ? PlayerTeam.Blue : PlayerTeam.Red;
        Assert.True(world.TrySetNetworkPlayerTeam(SimulationWorld.LocalPlayerSlot, oppositeTeam));

        Assert.Equal(expectedHealth, remotePlayer.Health);
        Assert.Equal(expectedPositionX, remotePlayer.X);
        Assert.Equal(expectedPositionY, remotePlayer.Y);
        Assert.True(remotePlayer.IsAlive);
    }

    [Fact]
    public void ChangingLiveNetworkPlayerTeamSchedulesRespawnInsteadOfImmediateSpawn()
    {
        var world = CreateWorldWithLocalClass(PlayerClass.Soldier);
        var oppositeTeam = world.LocalPlayer.Team == PlayerTeam.Red ? PlayerTeam.Blue : PlayerTeam.Red;

        Assert.True(world.TrySetNetworkPlayerTeam(SimulationWorld.LocalPlayerSlot, oppositeTeam));

        Assert.False(world.LocalPlayer.IsAlive);
        Assert.Equal(oppositeTeam, world.LocalPlayer.Team);
        Assert.True(world.GetNetworkPlayerRespawnTicks(SimulationWorld.LocalPlayerSlot) > 1);
        Assert.Empty(world.DeadBodies);
        Assert.Empty(world.PlayerGibs);

        AdvanceUntilRespawn(world, SimulationWorld.LocalPlayerSlot);

        Assert.True(world.LocalPlayer.IsAlive);
        Assert.Equal(oppositeTeam, world.LocalPlayer.Team);
    }

    [Fact]
    public void NetworkPlayerMaxHealthOverrideClampsAndClearsPlayerHealth()
    {
        var world = new SimulationWorld();
        Assert.True(world.TryPrepareNetworkPlayerJoin(2));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(2, PlayerClass.Heavy));
        Assert.True(world.TryGetNetworkPlayer(2, out var remotePlayer));

        Assert.True(world.TrySetNetworkPlayerMaxHealthOverride(2, 25));
        Assert.Equal(25, remotePlayer.MaxHealth);
        Assert.Equal(25, remotePlayer.Health);

        remotePlayer.ForceSetHealth(10);
        Assert.True(world.TrySetNetworkPlayerMaxHealthOverride(2, null, refillHealth: false));

        Assert.Equal(CharacterClassCatalog.Heavy.MaxHealth, remotePlayer.MaxHealth);
        Assert.Equal(10, remotePlayer.Health);
    }

    [Fact]
    public void ReleasingNetworkPlayerSlotClearsMaxHealthOverride()
    {
        var world = new SimulationWorld();

        Assert.True(world.TryPrepareNetworkPlayerJoin(2));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(2, PlayerClass.Heavy));
        Assert.True(world.TrySetNetworkPlayerMaxHealthOverride(2, 25));
        Assert.True(world.TryGetNetworkPlayer(2, out var remotePlayer));
        Assert.Equal(25, remotePlayer.MaxHealth);

        Assert.True(world.TryReleaseNetworkPlayerSlot(2));
        Assert.True(world.TryPrepareNetworkPlayerJoin(2));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(2, PlayerClass.Heavy));

        Assert.True(world.TryGetNetworkPlayer(2, out remotePlayer));
        Assert.Equal(CharacterClassCatalog.Heavy.MaxHealth, remotePlayer.MaxHealth);
    }

    private static SimulationWorld CreateWorldWithLocalClass(PlayerClass playerClass)
    {
        var world = new SimulationWorld();
        world.PrepareLocalPlayerJoin();
        world.CompleteLocalPlayerJoin(playerClass);
        _ = world.DrainPendingSoundEvents();
        Assert.Equal(playerClass, world.LocalPlayer.ClassId);
        return world;
    }

    private static void AdvanceUntilRespawn(SimulationWorld world, byte slot)
    {
        for (var tick = 0; tick < world.Config.TicksPerSecond * 6 && world.GetNetworkPlayerRespawnTicks(slot) > 0; tick += 1)
        {
            world.AdvanceOneTick();
        }
    }
}
