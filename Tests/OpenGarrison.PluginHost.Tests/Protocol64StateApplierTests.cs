using System.Reflection;
using OpenGarrison.Client;
using OpenGarrison.Core;
using OpenGarrison.Core.LastToDie;
using OpenGarrison.GameplayModding;
using OpenGarrison.Protocol;
using OpenGarrison.Server;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class Protocol64StateApplierTests
{
    [Fact]
    public void InlineClassIdentityCannotBeChangedByAnOlderGenerationOrStaleState()
    {
        var applier = new Protocol64StateApplier();
        var first = new Protocol64PlayerStateBatch(1, 10, [Player(2, 99, 4, "class.overweight", 150)]);
        var stale = new Protocol64PlayerStateBatch(0, 9, [Player(2, 99, 4, "class.rocketman", 1)]);

        Assert.Equal(Protocol64StateApplyStatus.Applied, applier.ApplyPlayerStateBatch(first).Status);
        Assert.Equal(Protocol64StateApplyStatus.RepairRequested, applier.ApplyPlayerStateBatch(stale).Status);
        Assert.Equal("class.overweight", Assert.Single(applier.Players).GameplayClassId);
        Assert.Equal(150, Assert.Single(applier.Players).Health);
    }

    [Fact]
    public void ProjectileKindReuseRequiresAGenerationChange()
    {
        var applier = new Protocol64StateApplier();
        var rocket = Projectile(7, 1, Protocol64ProjectileKind.Rocket, 10);
        var wrongKind = Projectile(7, 1, Protocol64ProjectileKind.Flame, 11);
        var replacement = Projectile(7, 2, Protocol64ProjectileKind.Flame, 12);

        Assert.Equal(Protocol64StateApplyStatus.Applied, applier.ApplyProjectileState(rocket).Status);
        Assert.Equal(Protocol64StateApplyStatus.RepairRequested, applier.ApplyProjectileState(wrongKind).Status);
        Assert.Equal(Protocol64StateApplyStatus.Applied, applier.ApplyProjectileState(replacement).Status);
        Assert.Equal(Protocol64ProjectileKind.Flame, Assert.Single(applier.Projectiles).EntityKind);
    }

    [Fact]
    public void InvalidResyncDoesNotPartiallyReplaceTheExistingView()
    {
        var applier = new Protocol64StateApplier();
        Assert.Equal(Protocol64StateApplyStatus.Applied, applier.ApplyPlayerStateBatch(
            new Protocol64PlayerStateBatch(1, 1, [Player(1, 1, 1, "class.scout", 100)])).Status);

        var invalid = new Protocol64StateResyncResponse(
            4,
            2,
            2,
            [Player(2, 2, 1, "class.medic", 100), Player(2, 2, 1, "class.spy", 100)],
            [],
            [],
            []);

        Assert.Equal(Protocol64StateApplyStatus.RepairRequested, applier.ApplyResyncResponse(invalid).Status);
        Assert.Equal("class.scout", Assert.Single(applier.Players).GameplayClassId);
    }

    [Fact]
    public void RosterRejectsCompetingIdentitiesForTheSameSlot()
    {
        var applier = new Protocol64StateApplier();
        var result = applier.ApplyRosterState(new Protocol64RosterState(
            1,
            1,
            [
                new Protocol64PlayerIdentity(2, 22, 1),
                new Protocol64PlayerIdentity(2, 23, 1),
            ],
            []));

        Assert.Equal(Protocol64StateApplyStatus.RepairRequested, result.Status);
        Assert.Empty(applier.Players);
    }

    [Fact]
    public void RosterRemovalTombstoneRejectsAnOlderPlayerBatchThatArrivesLate()
    {
        var applier = new Protocol64StateApplier();
        var player = Player(3, 33, 1, "class.scout", 100);

        Assert.Equal(
            Protocol64StateApplyStatus.Applied,
            applier.ApplyPlayerStateBatch(new Protocol64PlayerStateBatch(1, 1, [player])).Status);
        Assert.Equal(
            Protocol64StateApplyStatus.Applied,
            applier.ApplyRosterState(new Protocol64RosterState(
                1,
                2,
                [],
                [new Protocol64PlayerIdentity(player.Slot, player.PlayerId, player.Generation)])).Status);

        Assert.Equal(
            Protocol64StateApplyStatus.Applied,
            applier.ApplyPlayerStateBatch(new Protocol64PlayerStateBatch(2, 1, [player])).Status);
        Assert.Empty(applier.Players);
    }

    [Fact]
    public void ResyncResponseMustMatchARequestAndThenReplacesTheViewAtomically()
    {
        var applier = new Protocol64StateApplier();
        var request = applier.CreateResyncRequest(Protocol64StateResyncReason.ClientRequested);
        var response = new Protocol64StateResyncResponse(
            request.RequestId,
            2,
            2,
            [Player(4, 44, 1, "class.medic", 150)],
            [],
            [],
            []);

        Assert.Equal(Protocol64StateApplyStatus.Applied, applier.ApplyResyncResponse(response).Status);
        Assert.Equal("class.medic", Assert.Single(applier.Players).GameplayClassId);
    }

    [Fact]
    public void ValidatedStateIsCommittedIntoTheLiveSimulationWorld()
    {
        var applier = new Protocol64StateApplier();
        Assert.Equal(
            Protocol64StateApplyStatus.Applied,
            applier.ApplyPlayerStateBatch(new Protocol64PlayerStateBatch(
                1,
                1,
                [Player(2, 22, 1, StockGameplayModCatalog.GetClassId(PlayerClass.Scout), 75) with { X = 123f, Y = 45f }])).Status);

        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        applier.ApplyToWorld(world);

        Assert.True(world.TryGetNetworkPlayer(2, out var player));
        Assert.Equal(StockGameplayModCatalog.GetClassId(PlayerClass.Scout), player.GameplayClassId);
        Assert.Equal(75, player.Health);
        Assert.Equal(123f, player.X);
        Assert.Equal(45f, player.Y);
    }

    [Fact]
    public void Protocol64PublisherKeepsMedicNeedlegunAmmoInTheSecondaryState()
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        Assert.True(world.TrySetLocalClass(PlayerClass.Medic));

        var publisher = new Protocol64StatePublisher(world);
        var player = Assert.Single(publisher.BuildPlayerStateBatch(1).Players);

        Assert.Equal(CharacterClassCatalog.Medic.GameplayClassId, player.GameplayClassId);
        Assert.Equal(1, player.CurrentAmmo);
        Assert.Equal(1, player.MaxAmmo);
        Assert.Equal(40, player.OffhandAmmo);
        Assert.Equal(40, player.OffhandMaxAmmo);
    }

    [Fact]
    public void Protocol64PublisherAndWorldHydratePrimaryCooldownAndReloadTimers()
    {
        var source = CreateJoinedWorld(PlayerClass.Soldier);
        Assert.True(source.LocalPlayer.TryFirePrimaryWeapon());

        var state = Assert.Single(new Protocol64StatePublisher(source).BuildPlayerStateBatch(1).Players);
        Assert.True(state.PrimaryCooldownTicks > 0);
        Assert.True(state.PrimaryReloadTicks > 0);

        var receiver = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        Assert.True(receiver.ApplyProtocol64PlayerState(state));
        Assert.Equal(state.PrimaryCooldownTicks, receiver.LocalPlayer.PrimaryCooldownTicks);
        Assert.Equal(state.PrimaryReloadTicks, receiver.LocalPlayer.ReloadTicksUntilNextShell);
    }

    [Theory]
    [InlineData(PlayerClass.Sniper, "weapon.bow")]
    [InlineData(PlayerClass.Scout, "weapon.scout-nailgun")]
    [InlineData(PlayerClass.Medic, "weapon.medigun.crit")]
    public void Protocol64PublisherKeepsAlternatePrimaryInPrimarySlotAcrossRespawn(
        PlayerClass playerClass,
        string selectedPrimaryItemId)
    {
        var source = CreateJoinedWorld(playerClass);
        source.ConfigureExperimentalGameplaySettings(new ExperimentalGameplaySettings(
            EnableSecondaryAbilities: true));
        source.LocalPlayer.SetSpawnRoomState(false);
        Assert.True(source.LocalPlayer.TrySelectGameplayPrimaryItem(selectedPrimaryItemId));

        Assert.Equal(selectedPrimaryItemId, source.LocalPlayer.GameplayLoadoutState.PrimaryItemId);
        source.ForceKillLocalPlayer();
        AdvanceUntilRespawn(source);

        var state = Assert.Single(new Protocol64StatePublisher(source).BuildPlayerStateBatch(1).Players);
        Assert.Equal((byte)GameplayEquipmentSlot.Primary, state.ActiveWeapon);
        Assert.Equal(source.LocalPlayer.CurrentShells, state.CurrentAmmo);
        Assert.Equal(source.LocalPlayer.MaxShells, state.MaxAmmo);
    }

    [Fact]
    public void Protocol64PublisherAndWorldHydrateEffectiveMedicLinkState()
    {
        var source = CreateJoinedWorld(PlayerClass.Heavy);
        source.LocalPlayer.SetLastToDieMedicLinkProjection(
            stimulantDripActive: true,
            agilityDriveActive: true);
        var publisher = new Protocol64StatePublisher(source);

        var state = Assert.Single(publisher.BuildPlayerStateBatch(1).Players);
        Assert.Equal((byte)3, state.LastToDieMedicLinkState);

        var receiver = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        Assert.True(receiver.ApplyProtocol64PlayerState(state));
        Assert.True(receiver.LocalPlayer.LastToDieMedicStimulantDripLinkActive);
        Assert.True(receiver.LocalPlayer.LastToDieMedicAgilityDriveLinkActive);
    }

    [Fact]
    public void Protocol64PublisherAndWorldHydrateExactSpyCloakRuntime()
    {
        var source = CreateJoinedWorld(PlayerClass.Spy);
        Assert.True(source.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.RogueCommander, LastToDiePerkIds.Spy.Professional],
            resetDynamicState: true));
        Assert.True(source.LocalPlayer.TryToggleSpyCloak());
        Assert.True(source.LocalPlayer.TryBeginLastToDieProfessionalFireChord());
        for (var tick = 0; tick < 7; tick += 1)
        {
            source.AdvanceOneTick();
        }

        var publisher = new Protocol64StatePublisher(source);
        var state = Assert.Single(publisher.BuildPlayerStateBatch(7).Players);
        Assert.Equal(source.LocalPlayer.LastToDieSpyCloakMeterUnits, state.LastToDieSpyCloakMeterUnits);

        var receiver = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        Assert.True(receiver.ApplyProtocol64PlayerState(state));
        var hydratedMeter = receiver.LocalPlayer.LastToDieSpyCloakMeterUnits;
        Assert.True(receiver.TryApplyLastToDiePlayerPredictionProfile(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.RogueCommander.Value, LastToDiePerkIds.Spy.Professional.Value]));

        Assert.Equal(state.LastToDieSpyCloakMeterUnits, hydratedMeter);
        Assert.Equal(hydratedMeter, receiver.LocalPlayer.LastToDieSpyCloakMeterUnits);
        Assert.True(receiver.LocalPlayer.LastToDieRogueCommanderEnabled);
        Assert.True(receiver.LocalPlayer.LastToDieProfessionalEnabled);
        Assert.Equal((byte)1, state.LastToDieProfessionalFireChordState);
        Assert.Equal(state.LastToDieProfessionalFireChordState, receiver.LocalPlayer.LastToDieProfessionalFireChordState);
    }

    [Fact]
    public void Protocol64HydrationPreservesRogueRampTickRemainder()
    {
        var source = CreateJoinedWorld(PlayerClass.Spy);
        Assert.True(source.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.RogueCommander],
            resetDynamicState: true));

        for (var tick = 0; tick < source.Config.TicksPerSecond - 1; tick += 1)
        {
            source.LocalPlayer.AdvanceLastToDieSpyCloakMeter(source.Config.TicksPerSecond);
        }

        var state = Assert.Single(new Protocol64StatePublisher(source).BuildPlayerStateBatch(8).Players);
        Assert.Equal((ushort)(source.Config.TicksPerSecond - 1), state.LastToDieSpyRogueRampTicks);

        var receiver = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        Assert.True(receiver.ApplyProtocol64PlayerState(state));
        Assert.True(receiver.TryApplyLastToDiePlayerPredictionProfile(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.RogueCommander.Value]));
        Assert.Equal(source.Config.TicksPerSecond - 1, receiver.LocalPlayer.LastToDieSpyRogueRampTicks);

        receiver.LocalPlayer.AdvanceLastToDieSpyCloakMeter(receiver.Config.TicksPerSecond);

        Assert.Equal(1, receiver.LocalPlayer.LastToDieSpyRogueRampStacks);
        Assert.Equal(0, receiver.LocalPlayer.LastToDieSpyRogueRampTicks);
    }

    [Fact]
    public void Protocol64PublisherRedactsHiddenEnemySpyStatePerViewer()
    {
        var world = CreateJoinedWorld(PlayerClass.Scout);
        world.LocalPlayer.Spawn(PlayerTeam.Red, 100f, 0f);
        Assert.True(world.TryPrepareNetworkPlayerJoin(2));
        Assert.True(world.TrySetNetworkPlayerTeam(2, PlayerTeam.Blue));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(2, PlayerClass.Spy));
        Assert.True(world.TryGetNetworkPlayer(2, out var enemySpy));
        enemySpy.Spawn(PlayerTeam.Blue, 50f, 0f);
        Assert.True(enemySpy.TryToggleSpyCloak());
        for (var tick = 0; tick < 20; tick += 1)
        {
            enemySpy.AdvanceTickState(default, world.Config.FixedDeltaSeconds);
        }
        Assert.False(enemySpy.IsSpyVisibleToEnemies);

        var publisher = new Protocol64StatePublisher(world);

        Assert.Equal(2, publisher.BuildPlayerStateBatch(1).Players.Count);
        var viewerBatch = publisher.BuildPlayerStateBatch(2, SimulationWorld.LocalPlayerSlot);
        Assert.Contains(viewerBatch.Players, player => player.Slot == SimulationWorld.LocalPlayerSlot);
        Assert.DoesNotContain(viewerBatch.Players, player => player.Slot == 2);
        var resync = publisher.BuildResyncResponse(
            new Protocol64StateResyncRequest(1, 0, 0, 0, Protocol64StateResyncReason.ClientRequested),
            2,
            SimulationWorld.LocalPlayerSlot);
        Assert.DoesNotContain(resync.Players, player => player.Slot == 2);

        enemySpy.Spawn(PlayerTeam.Blue, 150f, 0f);
        Assert.Contains(
            publisher.BuildPlayerStateBatch(3, SimulationWorld.LocalPlayerSlot).Players,
            player => player.Slot == 2);
    }

    [Fact]
    public void FullPlayerBatchReplacementRemovesAndCanReapplySameGeneration()
    {
        var applier = new Protocol64StateApplier();
        var local = Player(1, 11, 1, StockGameplayModCatalog.GetClassId(PlayerClass.Scout), 125);
        var spy = Player(2, 22, 1, StockGameplayModCatalog.GetClassId(PlayerClass.Spy), 100);
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });

        Assert.Equal(
            Protocol64StateApplyStatus.Applied,
            applier.ApplyPlayerStateBatch(new Protocol64PlayerStateBatch(1, 1, [local, spy])).Status);
        applier.ApplyToWorld(world);
        Assert.Contains(world.EnumerateReplicatedNetworkPlayers(), entry => entry.Slot == 2);

        Assert.Equal(
            Protocol64StateApplyStatus.Applied,
            applier.ApplyPlayerStateBatch(new Protocol64PlayerStateBatch(2, 2, [local])).Status);
        applier.ApplyToWorld(world);
        Assert.DoesNotContain(world.EnumerateReplicatedNetworkPlayers(), entry => entry.Slot == 2);

        Assert.Equal(
            Protocol64StateApplyStatus.Applied,
            applier.ApplyPlayerStateBatch(new Protocol64PlayerStateBatch(3, 3, [local, spy])).Status);
        applier.ApplyToWorld(world);
        var reapplied = Assert.Single(world.EnumerateReplicatedNetworkPlayers(), entry => entry.Slot == 2);
        Assert.Equal(PlayerClass.Spy, reapplied.Player.ClassId);
    }

    [Fact]
    public void SameClassSlotReappearanceAdvancesGenerationPastRemovalTombstone()
    {
        var source = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        Assert.True(source.TryPrepareNetworkPlayerJoin(2));
        Assert.True(source.TrySetNetworkPlayerTeam(2, PlayerTeam.Red));
        Assert.True(source.TryApplyNetworkPlayerClassSelection(2, PlayerClass.Spy));
        var publisher = new Protocol64StatePublisher(source);
        var applier = new Protocol64StateApplier();

        var firstBatch = publisher.BuildPlayerStateBatch(1);
        var first = Assert.Single(firstBatch.Players, player => player.Slot == 2);
        Assert.Equal(1U, first.Generation);
        Assert.Equal(Protocol64StateApplyStatus.Applied, applier.ApplyPlayerStateBatch(firstBatch).Status);
        _ = publisher.BuildRosterState(1);

        Assert.True(source.TryReleaseNetworkPlayerSlot(2));
        _ = publisher.BuildPlayerStateBatch(2);
        var removedRoster = publisher.BuildRosterState(2);
        Assert.Equal(Protocol64StateApplyStatus.Applied, applier.ApplyRosterState(removedRoster).Status);

        Assert.True(source.TryPrepareNetworkPlayerJoin(2));
        Assert.True(source.TrySetNetworkPlayerTeam(2, PlayerTeam.Red));
        Assert.True(source.TryApplyNetworkPlayerClassSelection(2, PlayerClass.Spy));
        var rejoinedBatch = publisher.BuildPlayerStateBatch(3);
        var rejoined = Assert.Single(rejoinedBatch.Players, player => player.Slot == 2);
        Assert.Equal(2U, rejoined.Generation);
        Assert.Equal(Protocol64StateApplyStatus.Applied, applier.ApplyPlayerStateBatch(rejoinedBatch).Status);
        Assert.Contains(applier.Players, player => player.Slot == 2 && player.Generation == 2);
    }

    [Fact]
    public void Protocol64PlayerStateHydratesSpyProfileAndLuckyProgressBeforeAmmoClamp()
    {
        var source = CreateJoinedWorld(PlayerClass.Spy);
        Assert.True(source.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.Agent, LastToDiePerkIds.Spy.LuckyStrike],
            refillHealth: true));

        Assert.True(source.LocalPlayer.TryFirePrimaryWeapon());
        AdvanceUntilPrimaryReady(source);
        Assert.True(source.LocalPlayer.TryFirePrimaryWeapon());

        var publisher = new Protocol64StatePublisher(source);
        var state = Assert.Single(publisher.BuildPlayerStateBatch(12).Players);
        Assert.Equal(9, state.MaxAmmo);
        Assert.Equal(7, state.CurrentAmmo);
        Assert.Equal(
            (ushort)source.LocalPlayer.LastToDieSpyRevolverProfile.EncodeReplicatedState(2),
            state.LastToDieSpyRevolverState);
        Assert.True(source.LocalPlayer.TryToggleSpyCloak());
        state = Assert.Single(publisher.BuildPlayerStateBatch(13).Players);
        Assert.True(state.IsSpyCloaked);

        var receiver = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        Assert.True(receiver.ApplyProtocol64PlayerState(state));

        Assert.True(receiver.LocalPlayer.LastToDieSpyRevolverProfile.AgentEnabled);
        Assert.True(receiver.LocalPlayer.LastToDieSpyRevolverProfile.LuckyStrikeEnabled);
        Assert.Equal(2, receiver.LocalPlayer.LastToDieLuckyStrikeTriggerProgress);
        Assert.Equal(9, receiver.LocalPlayer.MaxShells);
        Assert.Equal(7, receiver.LocalPlayer.CurrentShells);
        Assert.True(receiver.LocalPlayer.IsSpyCloaked);
        Assert.Equal(source.LocalPlayer.SpyCloakAlpha, receiver.LocalPlayer.SpyCloakAlpha, precision: 2);
    }

    [Fact]
    public void Protocol64PublisherAndWorldRecreateImmutableSpyRevolverPayload()
    {
        var source = CreateJoinedWorld(PlayerClass.Spy);
        source.RandomSpreadEnabled = false;
        Assert.True(source.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [
                LastToDiePerkIds.Spy.Blunderbuss1,
                LastToDiePerkIds.Spy.Blunderbuss2,
                LastToDiePerkIds.Spy.Ricochet,
                LastToDiePerkIds.Spy.LuckyStrike,
            ],
            refillHealth: true));

        Assert.True(source.LocalPlayer.TryFirePrimaryWeapon());
        AdvanceUntilPrimaryReady(source);
        Assert.True(source.LocalPlayer.TryFirePrimaryWeapon());
        AdvanceUntilPrimaryReady(source);
        source.LocalPlayer.RefreshKritzCritBoost();
        Assert.True(source.LocalPlayer.TryFirePrimaryWeapon());
        InvokeFirePrimaryWeapon(source, source.LocalPlayer, 100f, 0f);

        Assert.Equal(LastToDieSpyRevolverProfile.BlunderbussBasePelletCount, source.RevolverShots.Count);
        Assert.All(source.RevolverShots, shot => Assert.True(shot.AppliesLuckyStrikeStun));
        var original = source.RevolverShots[0];
        original.AdvanceOneTick();
        var publisher = new Protocol64StatePublisher(source);
        var state = Assert.Single(
            publisher.BuildProjectileStates(20),
            projectile => projectile.EntityId == (ulong)original.Id);

        Assert.Equal(original.DamageValue, state.Damage);
        Assert.True(state.IsCritical);
        Assert.Equal((byte)original.LastToDieProfile.Encode(), state.LastToDieSpyRevolverProfile);
        Assert.True(state.AppliesLastToDieLuckyStrikeStun);
        Assert.Equal((uint)original.TicksRemaining, state.RemainingLifetimeTicks);

        var receiver = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        Assert.True(receiver.ApplyProtocol64ProjectileState(state));
        var recreated = Assert.Single(receiver.RevolverShots);

        Assert.Equal(original.DamageValue, recreated.DamageValue);
        Assert.True(recreated.IsCritical);
        Assert.Equal(original.LastToDieProfile, recreated.LastToDieProfile);
        Assert.True(recreated.AppliesLuckyStrikeStun);
        Assert.Equal(original.TicksRemaining, recreated.TicksRemaining);
    }

    [Fact]
    public void PlayerStateExposesTheLocalInputWatermarkForReconciliation()
    {
        var applier = new Protocol64StateApplier();
        var player = Player(2, 22, 1, StockGameplayModCatalog.GetClassId(PlayerClass.Scout), 75)
            with { LastProcessedInputSequence = 17 };

        Assert.Equal(
            Protocol64StateApplyStatus.Applied,
            applier.ApplyPlayerStateBatch(new Protocol64PlayerStateBatch(8, 100, [player])).Status);

        Assert.True(applier.TryGetPlayerState(2, out var applied));
        Assert.Equal(17U, applied.LastProcessedInputSequence);
    }

    [Fact]
    public void StateApplierResetDropsOldStateAndRepairRequests()
    {
        var applier = new Protocol64StateApplier();
        Assert.Equal(
            Protocol64StateApplyStatus.Applied,
            applier.ApplyPlayerStateBatch(new Protocol64PlayerStateBatch(3, 10, [Player(2, 22, 1, "class.scout", 75)])).Status);
        applier.CreateResyncRequest(Protocol64StateResyncReason.ClientRequested);

        applier.Reset();

        Assert.Equal(0UL, applier.PlayerStateSequence);
        Assert.Empty(applier.Players);
        Assert.False(applier.TryGetPlayerState(2, out _));
        var request = applier.CreateResyncRequest(Protocol64StateResyncReason.InitialState);
        Assert.Equal(1UL, request.RequestId);
    }

    [Fact]
    public void NewerStateSequenceCannotRollBackAnInputWatermark()
    {
        var applier = new Protocol64StateApplier();
        var current = Player(2, 22, 1, "class.scout", 100) with { LastProcessedInputSequence = 20 };
        var regressed = current with { LastProcessedInputSequence = 19 };

        Assert.Equal(
            Protocol64StateApplyStatus.Applied,
            applier.ApplyPlayerStateBatch(new Protocol64PlayerStateBatch(1, 1, [current])).Status);
        Assert.Equal(
            Protocol64StateApplyStatus.Stale,
            applier.ApplyPlayerStateBatch(new Protocol64PlayerStateBatch(2, 2, [regressed])).Status);
        Assert.Equal(20U, Assert.Single(applier.Players).LastProcessedInputSequence);
    }

    private static Protocol64PlayerState Player(ushort slot, ulong playerId, uint generation, string classId, int health)
        => new(slot, playerId, generation, classId, health, 200, 1, true, 0, 0, 0, 0, 0, 0, 1);

    private static Protocol64ProjectileState Projectile(ulong id, uint generation, Protocol64ProjectileKind kind, uint tick)
        => new(id, generation, kind, tick, 1, 1, 0, 0, 1, 0, 0, true, 20, 10);

    private static SimulationWorld CreateJoinedWorld(PlayerClass playerClass)
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        world.PrepareLocalPlayerJoin();
        world.CompleteLocalPlayerJoin(playerClass);
        return world;
    }

    private static void AdvanceUntilPrimaryReady(SimulationWorld world)
    {
        for (var tick = 0;
             tick < 120
                && (world.LocalPlayer.PrimaryCooldownTicks > 0
                    || world.LocalPlayer.CurrentShells < world.LocalPlayer.PrimaryWeapon.AmmoPerShot);
             tick += 1)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(0, world.LocalPlayer.PrimaryCooldownTicks);
        Assert.True(world.LocalPlayer.CurrentShells >= world.LocalPlayer.PrimaryWeapon.AmmoPerShot);
    }

    private static void AdvanceUntilRespawn(SimulationWorld world)
    {
        for (var tick = 0;
             tick < world.Config.TicksPerSecond * 6
                && world.GetNetworkPlayerRespawnTicks(SimulationWorld.LocalPlayerSlot) > 0;
             tick += 1)
        {
            world.AdvanceOneTick();
        }

        Assert.True(world.LocalPlayer.IsAlive);
    }

    private static void InvokeFirePrimaryWeapon(
        SimulationWorld world,
        PlayerEntity player,
        float aimWorldX,
        float aimWorldY)
    {
        var method = typeof(SimulationWorld).GetMethod(
            "FirePrimaryWeapon",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        _ = method!.Invoke(world, [player, aimWorldX, aimWorldY]);
    }
}
