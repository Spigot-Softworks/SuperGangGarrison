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
    public void RemoteNetworkJoinAppliesStockSecondaryWithoutInventingAnAbilitySlot()
    {
        var world = new SimulationWorld();

        Assert.True(world.TryPrepareNetworkPlayerJoin(2));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(2, PlayerClass.Soldier));

        Assert.True(world.TryGetNetworkPlayer(2, out var remotePlayer));
        Assert.Equal("soldier.stock", remotePlayer.SelectedGameplayLoadoutId);
        Assert.Equal("weapon.soldier-shotgun", remotePlayer.GameplayLoadoutState.SecondaryItemId);
        Assert.Null(remotePlayer.GameplayLoadoutState.UtilityItemId);
        Assert.DoesNotContain("ability.soldier-utility", remotePlayer.GameplayLoadoutState.AbilityItemIds!);
        Assert.True(remotePlayer.HasExperimentalOffhandWeapon);
        Assert.Equal(remotePlayer.ExperimentalOffhandMaxShells, remotePlayer.ExperimentalOffhandCurrentShells);
    }

    [Fact]
    public void LastToDieMedicJoinProvidesKritzAsAnAlternatePrimary()
    {
        var world = new SimulationWorld();
        world.ConfigureExperimentalGameplaySettings(new ExperimentalGameplaySettings(
            EnableSecondaryAbilities: true));

        Assert.True(world.TryPrepareNetworkPlayerJoin(2));
        Assert.True(world.TrySetNetworkPlayerAutomaticRespawnSuppressed(2, suppressed: true));
        Assert.True(world.TryForceNetworkPlayerClassSelectionAndRespawn(2, PlayerClass.Medic));

        Assert.True(world.TryGetNetworkPlayer(2, out var medic));
        Assert.True(medic.HasAlternatePrimaryWeapons);
        Assert.False(medic.HasExperimentalOffhandWeapon);
        Assert.Null(medic.GameplayLoadoutState.SecondaryItemId);
        Assert.True(world.TrySetNetworkPlayerGameplayPrimaryItem(2, "weapon.medigun.crit"));
        Assert.Equal("weapon.medigun.crit", medic.GameplayLoadoutState.PrimaryItemId);
        Assert.Equal(PrimaryWeaponKind.Medigun, medic.PrimaryWeapon.Kind);
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
    public void HeavySandvichIsAnAbilityAndCannotBeSelectedAsASecondaryWeapon()
    {
        var world = CreateWorldWithLocalClass(PlayerClass.Heavy);

        var changed = world.TrySetNetworkPlayerGameplayEquippedSlot(SimulationWorld.LocalPlayerSlot, GameplayEquipmentSlot.Secondary);

        Assert.False(changed);
        Assert.Equal(GameplayEquipmentSlot.Primary, world.LocalPlayer.SelectedGameplayEquippedSlot);
        Assert.Null(world.LocalPlayer.GameplayLoadoutState.SecondaryItemId);
        Assert.Contains("ability.heavy-sandvich", world.LocalPlayer.GameplayLoadoutState.AbilityItemIds!);
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
        world.LocalPlayer.SetSpawnRoomState(false);
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
        player.SetSpawnRoomState(false);
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
        player.SetSpawnRoomState(false);
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

    [Theory]
    [InlineData(PlayerClass.Sniper, "weapon.bow")]
    [InlineData(PlayerClass.Scout, "weapon.scout-nailgun")]
    [InlineData(PlayerClass.Medic, "weapon.medigun.crit")]
    public void LockedAlternatePrimarySelectedThroughGameplayInputSurvivesPracticeRespawn(
        PlayerClass playerClass,
        string expectedPrimaryItemId)
    {
        var world = CreateWorldWithLocalClass(playerClass);
        world.ConfigureExperimentalGameplaySettings(new ExperimentalGameplaySettings(
            EnableSecondaryAbilities: true));
        InstallPrimaryWeaponSwapCabinetAtLocalPlayer(world);
        world.SetLocalInput(default);
        world.SetLocalPreviousInput(default);

        Assert.True(world.LocalPlayer.IsAlive);
        Assert.True(world.LocalPlayer.HasAlternatePrimaryWeapons);
        Assert.False(world.LocalPlayer.HasExperimentalOffhandWeapon);
        Assert.True(world.IsNearPrimaryWeaponSwapStation(world.LocalPlayer));
        PressWeaponSwap(world);
        Assert.Equal(expectedPrimaryItemId, world.LocalPlayer.SelectedGameplayPrimaryItemId);
        Assert.Equal(GameplayEquipmentSlot.Primary, world.LocalPlayer.GameplayLoadoutState.EquippedSlot);

        world.LocalPlayer.SetSpawnRoomState(false);
        world.ForceKillLocalPlayer();

        // A settings/snapshot resync can run while the player is dead. This
        // must not forget the persistent selected-primary identity.
        world.ConfigureExperimentalGameplaySettings(new ExperimentalGameplaySettings(
            EnableSecondaryAbilities: true));

        // A same-class team confirmation is another real resync path used by
        // the network lifecycle while a player is awaiting respawn.
        Assert.True(world.TryRequestNetworkPlayerTeamSelection(
            SimulationWorld.LocalPlayerSlot,
            world.LocalPlayer.Team));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(
            SimulationWorld.LocalPlayerSlot,
            playerClass));
        Assert.False(world.LocalPlayer.IsAlive);

        AdvanceUntilRespawn(world, SimulationWorld.LocalPlayerSlot);

        Assert.True(world.LocalPlayer.IsAlive);
        Assert.False(world.LocalPlayer.HasExperimentalOffhandWeapon);
        Assert.Equal(GameplayEquipmentSlot.Primary, world.LocalPlayer.GameplayLoadoutState.EquippedSlot);
        Assert.Equal(expectedPrimaryItemId, world.LocalPlayer.GameplayLoadoutState.PrimaryItemId);
        Assert.Equal(expectedPrimaryItemId, world.LocalPlayer.GameplayLoadoutState.EquippedItemId);
    }

    [Theory]
    [InlineData(PlayerClass.Sniper, "weapon.bow")]
    [InlineData(PlayerClass.Scout, "weapon.scout-nailgun")]
    [InlineData(PlayerClass.Medic, "weapon.medigun.crit")]
    public void LockedAlternatePrimarySelectionSurvivesLocalNetworkRespawn(PlayerClass playerClass, string primaryItemId)
    {
        var world = CreateWorldWithLocalClass(playerClass);
        world.ConfigureExperimentalGameplaySettings(new ExperimentalGameplaySettings(
            EnableSecondaryAbilities: true));
        world.LocalPlayer.SetSpawnRoomState(false);
        Assert.True(world.LocalPlayer.TrySelectGameplayPrimaryItem(primaryItemId));

        Assert.Equal(primaryItemId, world.LocalPlayer.SelectedGameplayPrimaryItemId);
        Assert.Equal(GameplayEquipmentSlot.Primary, world.LocalPlayer.GameplayLoadoutState.EquippedSlot);

        world.ForceKillLocalPlayer();
        AdvanceUntilRespawn(world, SimulationWorld.LocalPlayerSlot);

        Assert.True(world.LocalPlayer.IsAlive);
        Assert.Equal(primaryItemId, world.LocalPlayer.SelectedGameplayPrimaryItemId);
        Assert.Equal(primaryItemId, world.LocalPlayer.GameplayLoadoutState.PrimaryItemId);
        Assert.Equal(GameplayEquipmentSlot.Primary, world.LocalPlayer.GameplayLoadoutState.EquippedSlot);
    }

    [Fact]
    public void LastToDieRespawnSuppressionKeepsDeadParticipantDeadUntilStageRespawn()
    {
        var world = CreateWorldWithLocalClass(PlayerClass.Soldier);
        Assert.True(world.TrySetNetworkPlayerAutomaticRespawnSuppressed(
            SimulationWorld.LocalPlayerSlot,
            suppressed: true));

        world.ForceKillLocalPlayer();
        for (var tick = 0; tick < world.Config.TicksPerSecond * 8; tick += 1)
        {
            world.AdvanceOneTick();
        }

        Assert.False(world.LocalPlayer.IsAlive);
        Assert.Null(world.LocalDeathCam);
        Assert.True(world.TryForceNetworkPlayerClassSelectionAndRespawn(
            SimulationWorld.LocalPlayerSlot,
            PlayerClass.Soldier));
        Assert.True(world.LocalPlayer.IsAlive);
    }

    [Theory]
    [InlineData(PlayerClass.Sniper)]
    [InlineData(PlayerClass.Medic)]
    public void LastToDieParticipantCanSwapLockedPrimaryAwayFromWeaponStation(PlayerClass playerClass)
    {
        var world = CreateWorldWithLocalClass(playerClass);
        world.ConfigureExperimentalGameplaySettings(new ExperimentalGameplaySettings(
            EnableSecondaryAbilities: true));
        world.LocalPlayer.SetSpawnRoomState(false);
        world.TeleportLocalPlayer(512f, 256f);
        Assert.False(world.IsNearPrimaryWeaponSwapStation(world.LocalPlayer));
        var defaultPrimaryItemId = world.LocalPlayer.GameplayLoadoutState.PrimaryItemId;

        PressWeaponSwap(world);
        Assert.Equal(defaultPrimaryItemId, world.LocalPlayer.GameplayLoadoutState.PrimaryItemId);

        world.SetLocalInput(default);
        world.AdvanceOneTick();
        Assert.True(world.TrySetNetworkPlayerAutomaticRespawnSuppressed(
            SimulationWorld.LocalPlayerSlot,
            suppressed: true));
        PressWeaponSwap(world);

        Assert.NotEqual(defaultPrimaryItemId, world.LocalPlayer.GameplayLoadoutState.PrimaryItemId);
        Assert.Equal(GameplayEquipmentSlot.Primary, world.LocalPlayer.GameplayLoadoutState.EquippedSlot);
        Assert.Equal(
            playerClass == PlayerClass.Sniper
                ? BuiltInGameplayBehaviorIds.SniperBow
                : BuiltInGameplayBehaviorIds.MedigunCrit,
            world.LocalPlayer.PrimaryBehaviorId);
    }

    [Fact]
    public void AlternatePrimaryClassWithSecondaryUsesSwapForSecondaryAwayFromStation()
    {
        var world = CreateWorldWithLocalClass(PlayerClass.Scout);
        world.LocalPlayer.SetSpawnRoomState(false);
        world.TeleportLocalPlayer(512f, 256f);
        Assert.False(world.IsNearPrimaryWeaponSwapStation(world.LocalPlayer));
        Assert.True(world.TrySetNetworkPlayerGameplaySecondaryItem(
            SimulationWorld.LocalPlayerSlot,
            "weapon.soldier-shotgun"));
        Assert.True(world.LocalPlayer.HasAlternatePrimaryWeapons);
        Assert.True(world.LocalPlayer.HasExperimentalOffhandWeapon);
        var primaryItemId = world.LocalPlayer.GameplayLoadoutState.PrimaryItemId;

        PressWeaponSwap(world);

        Assert.Equal(primaryItemId, world.LocalPlayer.GameplayLoadoutState.PrimaryItemId);
        Assert.Equal(GameplayEquipmentSlot.Secondary, world.LocalPlayer.GameplayLoadoutState.EquippedSlot);
        Assert.Equal("weapon.soldier-shotgun", world.LocalPlayer.GameplayLoadoutState.EquippedItemId);
    }

    [Fact]
    public void LastToDieObjectiveSpawnMovesRemoteParticipantToControlPoint()
    {
        const float pointX = 320f;
        const float pointY = 240f;
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        world.CombatTestSetLevel(new SimpleLevel(
            name: "ltd_network_objective_spawn",
            mode: GameModeKind.KingOfTheHill,
            bounds: new WorldBounds(640f, 480f),
            mapScale: 1f,
            backgroundAssetName: null,
            mapAreaIndex: 1,
            mapAreaCount: 1,
            localSpawn: new SpawnPoint(64f, 64f),
            redSpawns: [new SpawnPoint(64f, 64f)],
            blueSpawns: [new SpawnPoint(576f, 64f)],
            intelBases: [],
            roomObjects:
            [
                new RoomObjectMarker(
                    RoomObjectType.ControlPoint,
                    pointX - 21f,
                    pointY - 21f,
                    42f,
                    42f,
                    "ControlPointNeutralS",
                    SourceName: "ControlPoint1"),
            ],
            floorY: 320f,
            solids: [new LevelSolid(0f, 320f, 640f, 160f)],
            importedFromSource: false));

        Assert.True(world.TryPrepareNetworkPlayerJoin(2));
        Assert.True(world.TrySetNetworkPlayerTeam(2, PlayerTeam.Red));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(2, PlayerClass.Medic));
        Assert.True(world.TryGetNetworkPlayer(2, out var remotePlayer));
        Assert.True(world.TryMoveNetworkPlayerToLastToDieObjectiveSpawn(2));

        Assert.InRange(remotePlayer.X, pointX - 64f, pointX + 64f);
        Assert.InRange(remotePlayer.Y, pointY - 96f, pointY + 96f);
        Assert.False(remotePlayer.IsInSpawnRoom);
    }

    [Fact]
    public void LastToDieEnemyCanUseOpposingSideIngressWithoutEnteringItsSpawnRoom()
    {
        const byte enemySlot = 3;
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        world.CombatTestSetLevel(new SimpleLevel(
            name: "ltd_enemy_ingress",
            mode: GameModeKind.KingOfTheHill,
            bounds: new WorldBounds(640f, 240f),
            mapScale: 1f,
            backgroundAssetName: null,
            mapAreaIndex: 1,
            mapAreaCount: 1,
            localSpawn: new SpawnPoint(48f, 156f),
            redSpawns: [new SpawnPoint(48f, 156f)],
            blueSpawns: [new SpawnPoint(592f, 156f)],
            intelBases: [],
            roomObjects:
            [
                new RoomObjectMarker(
                    RoomObjectType.SpawnRoom,
                    0f,
                    100f,
                    96f,
                    80f,
                    string.Empty,
                    SourceName: "RedSpawnRoom"),
                new RoomObjectMarker(
                    RoomObjectType.TeamGate,
                    96f,
                    100f,
                    8f,
                    80f,
                    string.Empty,
                    PlayerTeam.Red,
                    "RedTeamGate"),
            ],
            floorY: 180f,
            solids: [new LevelSolid(0f, 180f, 640f, 60f)],
            importedFromSource: false));

        Assert.True(world.TryPrepareNetworkPlayerJoin(enemySlot));
        Assert.True(world.TrySetNetworkPlayerTeam(enemySlot, PlayerTeam.Blue));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(enemySlot, PlayerClass.Scout));
        Assert.True(world.TryMoveNetworkPlayerToLastToDieEnemySpawn(enemySlot, PlayerTeam.Red));
        Assert.True(world.TryGetNetworkPlayer(enemySlot, out var enemy));

        var ingressX = enemy.X;
        Assert.Equal(PlayerTeam.Blue, enemy.Team);
        Assert.InRange(ingressX, 104.01f, 319.99f);
        Assert.False(enemy.IsInSpawnRoom);
        Assert.False(enemy.IsInsideBlockingTeamGate(world.Level, enemy.Team));

        Assert.True(world.ForceKillNetworkPlayer(enemySlot));
        Assert.True(world.TryConfigureNetworkPlayerLastToDieEnemySpawn(
            enemySlot,
            PlayerTeam.Blue,
            repositionAlivePlayer: false));
        Assert.True(world.TryForceNetworkPlayerClassSelectionAndRespawn(enemySlot, PlayerClass.Scout));
        Assert.InRange(enemy.X, 320.01f, 639.99f);
        Assert.NotEqual(ingressX, enemy.X);
        Assert.False(enemy.IsInSpawnRoom);
        Assert.False(enemy.IsInsideBlockingTeamGate(world.Level, enemy.Team));

        Assert.True(world.ForceKillNetworkPlayer(enemySlot));
        Assert.True(world.TryConfigureNetworkPlayerLastToDieEnemySpawn(
            enemySlot,
            PlayerTeam.Red,
            repositionAlivePlayer: false));
        Assert.True(world.TryForceNetworkPlayerClassSelectionAndRespawn(enemySlot, PlayerClass.Scout));
        Assert.Equal(ingressX, enemy.X);
    }

    [Theory]
    [InlineData("Truefort")]
    [InlineData("Conflict")]
    [InlineData("Waterway")]
    [InlineData("Dirtbowl")]
    [InlineData("Egypt")]
    [InlineData("Montane")]
    [InlineData("Lumberyard")]
    [InlineData("Valley")]
    [InlineData("Corinth")]
    [InlineData("Harvest")]
    [InlineData("Gallery")]
    [InlineData("Eiger")]
    public void LastToDieEnemyOpposingIngressIsSafeOnDefaultRotationMaps(string levelName)
    {
        const byte enemySlot = 3;
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        Assert.True(world.TryLoadLevel(levelName, mapAreaIndex: 1, preservePlayerStats: false));
        Assert.True(world.TryPrepareNetworkPlayerJoin(enemySlot));
        Assert.True(world.TrySetNetworkPlayerTeam(enemySlot, PlayerTeam.Blue));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(enemySlot, PlayerClass.Scout));

        Assert.True(world.TryMoveNetworkPlayerToLastToDieEnemySpawn(enemySlot, PlayerTeam.Red));
        Assert.True(world.TryGetNetworkPlayer(enemySlot, out var enemy));
        Assert.Equal(PlayerTeam.Blue, enemy.Team);
        Assert.False(enemy.IsInSpawnRoom);
        Assert.False(enemy.IsInsideBlockingTeamGate(world.Level, enemy.Team));
        Assert.True(enemy.CanOccupy(world.Level, enemy.Team, enemy.X, enemy.Y));
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
        world.LocalPlayer.SetSpawnRoomState(false);
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

    private static void PressWeaponSwap(SimulationWorld world)
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

    private static void InstallPrimaryWeaponSwapCabinetAtLocalPlayer(SimulationWorld world)
    {
        var spawn = new SpawnPoint(world.LocalPlayer.X, world.LocalPlayer.Y);
        world.CombatTestSetLevel(new SimpleLevel(
            name: "locked_primary_respawn",
            mode: GameModeKind.TeamDeathmatch,
            bounds: new WorldBounds(640f, 480f),
            mapScale: 1f,
            backgroundAssetName: null,
            mapAreaIndex: 1,
            mapAreaCount: 1,
            localSpawn: spawn,
            redSpawns: [spawn],
            blueSpawns: [spawn],
            intelBases: [],
            roomObjects:
            [
                new RoomObjectMarker(
                    RoomObjectType.HealingCabinet,
                    world.LocalPlayer.X - 16f,
                    world.LocalPlayer.Y - 24f,
                    32f,
                    48f,
                    "sprite74",
                    SourceName: "HealingCabinet"),
            ],
            floorY: world.LocalPlayer.Y + 64f,
            solids: [],
            importedFromSource: false));

        Assert.True(world.IsNearPrimaryWeaponSwapStation(world.LocalPlayer));
    }

    private static void AdvanceUntilRespawn(SimulationWorld world, byte slot)
    {
        for (var tick = 0; tick < world.Config.TicksPerSecond * 6 && world.GetNetworkPlayerRespawnTicks(slot) > 0; tick += 1)
        {
            world.AdvanceOneTick();
        }
    }
}
