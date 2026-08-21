using OpenGarrison.Core;
using OpenGarrison.Core.LastToDie;
using OpenGarrison.Protocol;
using OpenGarrison.Server;
using OpenGarrison.Server.LastToDie;
using System.Net;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class LastToDieSpyAfterlifeRuntimeTests
{
    [Fact]
    public void LethalDamageStartsAnImmuneAttackingWindowAndHostileKillResurrects()
    {
        var world = CreateSpyWorld();
        var spy = world.LocalPlayer;
        var attacker = AddNetworkPlayer(world, 2, PlayerClass.Soldier, PlayerTeam.Blue);
        var victim = AddNetworkPlayer(world, 3, PlayerClass.Scout, PlayerTeam.Blue);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.Afterlife],
            resetDynamicState: true));
        var deathsBefore = spy.Deaths;
        var attackerKillsBefore = attacker.Kills;

        Assert.True(world.TryApplyGameplayDamage(spy.Id, spy.MaxHealth * 2f, attacker.Id, "RocketKL"));

        Assert.True(spy.IsAlive);
        Assert.True(spy.IsLastToDieSpyAfterlifeActive);
        Assert.True(world.IsLastToDieSpyAfterlifeWindowActive(SimulationWorld.LocalPlayerSlot));
        Assert.Equal(1, spy.Health);
        Assert.Equal(5 * world.Config.TicksPerSecond, spy.LastToDieSpyAfterlifeWindowTicksRemaining);
        Assert.Equal(60 * world.Config.TicksPerSecond, spy.LastToDieSpyAfterlifeCooldownTicksRemaining);
        Assert.Equal(deathsBefore, spy.Deaths);
        Assert.Equal(attackerKillsBefore, attacker.Kills);
        Assert.False(world.CanPlayerContributeToControlPoint(spy));

        Assert.True(world.TryApplyGameplayDamage(spy.Id, 10_000f, attacker.Id, null));
        world.ForceKillLocalPlayer();
        Assert.Equal(1, spy.Health);
        Assert.True(spy.IsLastToDieSpyAfterlifeActive);

        victim.ForceSetHealth(1);
        var spyKillsBefore = spy.Kills;
        Assert.True(world.TryApplyGameplayDamage(victim.Id, 10f, spy.Id, "RevolverKL"));

        Assert.True(spy.IsAlive);
        Assert.False(spy.IsLastToDieSpyAfterlifeActive);
        Assert.Equal((spy.MaxHealth * 3 + 4) / 5, spy.Health);
        Assert.Equal(spyKillsBefore + 1, spy.Kills);
        Assert.Equal(60 * world.Config.TicksPerSecond, spy.LastToDieSpyAfterlifeCooldownTicksRemaining);
        Assert.Equal(deathsBefore, spy.Deaths);
    }

    [Fact]
    public void CreditedPeriodicStatusKillAlsoResurrectsTheGhost()
    {
        var world = CreateSpyWorld();
        var spy = world.LocalPlayer;
        var attacker = AddNetworkPlayer(world, 2, PlayerClass.Soldier, PlayerTeam.Blue);
        var victim = AddNetworkPlayer(world, 3, PlayerClass.Scout, PlayerTeam.Blue);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.Afterlife],
            resetDynamicState: true));
        Assert.True(world.TryApplyGameplayDamage(spy.Id, spy.MaxHealth * 2f, attacker.Id, null));
        victim.ForceSetHealth(1);
        Assert.True(world.TryApplyLastToDieStatusEffect(
            victim.Id,
            spy.Id,
            LastToDieStatusEffectSpec.Bleed(
                LastToDieStatusEffectIds.SpyBlunderbussBleed,
                durationTicks: world.Config.TicksPerSecond,
                damagePerSecond: world.Config.TicksPerSecond)));

        world.AdvanceOneTick();

        Assert.False(victim.IsAlive);
        Assert.True(spy.IsAlive);
        Assert.False(spy.IsLastToDieSpyAfterlifeActive);
        Assert.Equal((spy.MaxHealth * 3 + 4) / 5, spy.Health);
        Assert.Equal(1, spy.Kills);
    }

    [Fact]
    public void ExpiryCompletesOriginalDeathOnceAndCooldownPreventsRetrigger()
    {
        var world = CreateSpyWorld();
        var spy = world.LocalPlayer;
        var attacker = AddNetworkPlayer(world, 2, PlayerClass.Soldier, PlayerTeam.Blue);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.Afterlife],
            resetDynamicState: true));
        Assert.True(world.TryApplyGameplayDamage(spy.Id, spy.MaxHealth * 2f, attacker.Id, "RocketKL"));

        for (var tick = 1; tick < spy.LastToDieSpyAfterlifeWindowTicks; tick += 1)
        {
            world.AdvanceOneTick();
        }

        Assert.True(spy.IsLastToDieSpyAfterlifeActive);
        Assert.Equal(1, spy.LastToDieSpyAfterlifeWindowTicksRemaining);
        Assert.Equal(0, spy.Deaths);
        Assert.Equal(0, attacker.Kills);

        world.AdvanceOneTick();

        Assert.False(spy.IsAlive);
        Assert.False(spy.IsLastToDieSpyAfterlifeActive);
        Assert.Equal(1, spy.Deaths);
        Assert.Equal(1, attacker.Kills);
        Assert.Equal(55 * world.Config.TicksPerSecond, spy.LastToDieSpyAfterlifeCooldownTicksRemaining);

        world.ForceRespawnLocalPlayer();
        Assert.True(spy.IsAlive);
        Assert.True(world.TryApplyGameplayDamage(spy.Id, spy.MaxHealth * 2f, attacker.Id, null));
        Assert.False(spy.IsAlive);
        Assert.False(spy.IsLastToDieSpyAfterlifeActive);
        Assert.Equal(2, spy.Deaths);
        Assert.Equal(2, attacker.Kills);
    }

    [Fact]
    public void DisconnectFailsTheWindowAndProducesNoLiveReservationMarker()
    {
        var world = CreateSpyWorld();
        var spy = world.LocalPlayer;
        var attacker = AddNetworkPlayer(world, 2, PlayerClass.Soldier, PlayerTeam.Blue);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.Afterlife],
            resetDynamicState: true));
        Assert.True(world.TryApplyGameplayDamage(spy.Id, spy.MaxHealth * 2f, attacker.Id, null));

        Assert.True(world.TryReleaseNetworkPlayerSlot(SimulationWorld.LocalPlayerSlot));

        Assert.False(spy.IsAlive);
        Assert.False(world.IsLastToDieSpyAfterlifeWindowActive(SimulationWorld.LocalPlayerSlot));
        Assert.True(world.ConsumeLastToDieSpyAfterlifeDisconnectFailure(SimulationWorld.LocalPlayerSlot));
        Assert.False(world.ConsumeLastToDieSpyAfterlifeDisconnectFailure(SimulationWorld.LocalPlayerSlot));
    }

    [Fact]
    public void AfterlifeDisconnectFailureCannotRestoreLifeOnReconnect()
    {
        var runId = Guid.Parse("214879f2-14df-4876-9321-53d6c209d766");
        var director = LastToDieServerDirector.CreateFirstSlice(
            ["Truefort"],
            LastToDieDifficulty.Standard,
            seed: 88,
            ticksPerSecond: 30,
            runId: runId,
            maximumPlayers: 1);
        var controller = new LastToDieProtocolController(director);
        long serverTick = 1;
        var disconnectFailures = new HashSet<byte> { 1 };
        var session = new LastToDieNetworkSession(
            controller,
            () => serverTick,
            (_, _) => { },
            ticksPerSecond: 30,
            consumeAfterlifeDisconnectFailure: disconnectFailures.Remove);
        var original = new ClientSession(
            slot: 1,
            userId: 100,
            new IPEndPoint(IPAddress.Loopback, 8191),
            name: "Host",
            lastSeen: TimeSpan.Zero);
        var replacement = new ClientSession(
            slot: 1,
            userId: 100,
            new IPEndPoint(IPAddress.Loopback, 8291),
            name: "Host",
            lastSeen: TimeSpan.FromMilliseconds(1));
        Assert.Equal([(byte)1], session.SynchronizeAuthorizedClients([original]));

        AssertAccepted(LastToDieCommandKind.Ready);
        AssertAccepted(LastToDieCommandKind.RequestStart);
        AssertAccepted(LastToDieCommandKind.ChooseSurvivor, LastToDieSurvivorCatalog.SpyId.Value);
        var offer = Assert.Single(controller.CreateSnapshot(1, ++serverTick).Players);
        AssertAccepted(
            LastToDieCommandKind.SelectReward,
            offer.ActiveOfferChoices[0],
            offer.ActiveOfferId);
        var loading = controller.CreateSnapshot(1, ++serverTick);
        Assert.True(controller.TryOpenStageBarrier(loading.StageInstanceId, 500, out var barrierError), barrierError);
        AssertAccepted(LastToDieCommandKind.StageContentReady, loading.CurrentMap);
        Assert.True(controller.TryAcknowledgeWorldBaseline(1, 500, out _, out var baselineError), baselineError);
        Assert.True(controller.Director.TryBeginStage(++serverTick, out var beginError), beginError);
        Assert.True(Assert.Single(controller.CreateSnapshot(1, serverTick).Players).IsAlive);

        Assert.Empty(session.SynchronizeAuthorizedClients([]));
        Assert.False(Assert.Single(controller.CreateSnapshot(1, ++serverTick).Players).IsAlive);
        Assert.Empty(disconnectFailures);

        Assert.Equal([(byte)1], session.SynchronizeAuthorizedClients([replacement]));
        var reconnected = Assert.Single(controller.CreateSnapshot(1, ++serverTick).Players);
        Assert.True(reconnected.IsConnected);
        Assert.False(reconnected.IsAlive);
        return;

        void AssertAccepted(
            LastToDieCommandKind kind,
            string selectedId = "",
            ulong offerId = 0)
        {
            var snapshot = controller.CreateSnapshot(1, ++serverTick);
            var result = controller.HandleCommand(
                1,
                new LastToDieCommandMessage(
                    CommandId: checked((ulong)serverTick),
                    runId,
                    snapshot.StructuralRevision,
                    kind,
                    snapshot.StageInstanceId,
                    offerId,
                    selectedId));
            Assert.Equal(LastToDieCommandResultKind.Accepted, result.Result.Result);
        }
    }

    [Fact]
    public void LegacyPredictionAndProtocol64PreserveHudTimers()
    {
        var source = CreateSpyWorld();
        var player = source.LocalPlayer;
        Assert.True(source.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.Afterlife],
            resetDynamicState: true));
        Assert.True(player.TryStartLastToDieSpyAfterlife(source.Config.TicksPerSecond));
        player.AdvanceTickState(default, source.Config.FixedDeltaSeconds);
        Assert.True(player.TryGetReplicatedStateInt(
            PlayerEntity.LastToDieSpyAfterlifeReplicatedStateOwnerId,
            PlayerEntity.LastToDieSpyAfterlifeReplicatedStateKey,
            out var legacyState));
        Assert.Equal(unchecked((int)player.LastToDieSpyAfterlifeState), legacyState);

        var predictionClone = new PlayerEntity(5000, CharacterClassCatalog.Spy, "PredictionClone");
        predictionClone.ConfigureLastToDieSpyAfterlife(
            enabled: true,
            ticksPerSecond: source.Config.TicksPerSecond,
            resetDynamicState: false);
        predictionClone.RestorePredictionState(player.CapturePredictionState());
        Assert.Equal(player.LastToDieSpyAfterlifeState, predictionClone.LastToDieSpyAfterlifeState);
        Assert.True(predictionClone.IsLastToDieSpyAfterlifeActive);

        var published = new Protocol64StatePublisher(source).BuildPlayerStateBatch(44);
        var publishedPlayer = Assert.Single(published.Players);
        Assert.Equal(player.LastToDieSpyAfterlifeState, publishedPlayer.LastToDieSpyAfterlifeState);
        var schema = new Protocol64PlayerStateBatchSchema();
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            schema.WriteBody(published, writer);
        }

        stream.Position = 0;
        Protocol64PlayerStateBatch decoded;
        using (var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            decoded = schema.ReadBody(reader);
        }

        var decodedPlayer = Assert.Single(decoded.Players);
        Assert.Equal(publishedPlayer.LastToDieSpyAfterlifeState, decodedPlayer.LastToDieSpyAfterlifeState);
        Assert.Equal((ushort)20, schema.Descriptor.Key.Revision);

        var receiver = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        Assert.True(receiver.ApplyProtocol64PlayerState(decodedPlayer));
        Assert.True(receiver.TryApplyLastToDiePlayerPredictionProfile(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.Afterlife.Value]));
        Assert.Equal(player.LastToDieSpyAfterlifeState, receiver.LocalPlayer.LastToDieSpyAfterlifeState);
        Assert.Equal(149, receiver.LocalPlayer.LastToDieSpyAfterlifeWindowTicksRemaining);
        Assert.Equal(1_799, receiver.LocalPlayer.LastToDieSpyAfterlifeCooldownTicksRemaining);
        Assert.True(receiver.LocalPlayer.IsLastToDieSpyAfterlifeActive);
    }

    private static SimulationWorld CreateSpyWorld()
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        world.PrepareLocalPlayerJoin();
        world.CompleteLocalPlayerJoin(PlayerClass.Spy);
        var spawn = new SpawnPoint(100f, 100f);
        world.CombatTestSetLevel(new SimpleLevel(
            "ltd-spy-afterlife-test",
            GameModeKind.TeamDeathmatch,
            new WorldBounds(1024f, 512f),
            1f,
            null,
            1,
            1,
            spawn,
            [spawn],
            [new SpawnPoint(800f, 100f)],
            [],
            [],
            floorY: 512f,
            [],
            importedFromSource: false));
        world.LocalPlayer.Spawn(PlayerTeam.Red, spawn.X, spawn.Y);
        return world;
    }

    private static PlayerEntity AddNetworkPlayer(
        SimulationWorld world,
        byte slot,
        PlayerClass playerClass,
        PlayerTeam team)
    {
        Assert.True(world.TryPrepareNetworkPlayerJoin(slot));
        Assert.True(world.TrySetNetworkPlayerTeam(slot, team));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(slot, playerClass));
        Assert.True(world.TryGetNetworkPlayer(slot, out var player));
        return player;
    }
}
