using OpenGarrison.Core;
using OpenGarrison.Protocol;
using OpenGarrison.Core.BotBrain;
using OpenGarrison.Server;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

[Collection(ContentRootTestGroup.Name)]
public sealed class MapRoundEndTests
{
    [Theory]
    [InlineData(PlayerTeam.Red)]
    [InlineData(PlayerTeam.Blue)]
    public void ThreeSecondAnnouncementThenEightSecondCountdownAwardsTheConfiguredTeam(PlayerTeam team)
    {
        var world = CreateWorld();
        SetOwners(world, team);
        Tick(world, 1);
        Assert.Single(world.MapLogic.CaptureRoundEndCountdowns());
        Assert.Null(world.MapLogic.GetActiveRoundEndCountdown());
        Assert.False(Output(world, team));
        Tick(world, 89);
        Assert.Null(world.MapLogic.GetActiveRoundEndCountdown());
        Tick(world, 1);
        Assert.Equal(8f, world.MapLogic.GetActiveRoundEndCountdown()!.Value.SecondsRemaining);
        Assert.True(Output(world, team));
        // Re-evaluating ownership must preserve the countdown's stateful output.
        world.Level.LogicGraph.EvaluateCombinatorial(world.ControlPoints);
        Assert.True(Output(world, team));
        Tick(world, 239);
        Assert.False(world.MatchState.IsEnded);
        Assert.False(world.Level.DamageableZoneStates[3].IsDestroyed);
        Tick(world, 1);
        Assert.True(world.MatchState.IsEnded);
        Assert.Equal(team, world.MatchState.WinnerTeam);
        Assert.True(world.Level.LogicGraph.GetOutput(world.Level.LogicGraph.NodeIndexByKey["completed"]));
        Assert.True(world.Level.DamageableZoneStates[3].IsDestroyed);
        Assert.Equal(1u, world.Level.DamageableZoneStates[3].Revision);
        Assert.Null(world.MapLogic.GetActiveRoundEndCountdown());
    }

    [Theory]
    [InlineData(30)]
    [InlineData(180)]
    [InlineData(330)]
    public void LosingAnyPointCancelsAndRecapturingStartsTheWholeSequenceAgain(int beforeLoss)
    {
        var world = CreateWorld();
        SetOwners(world, PlayerTeam.Red);
        Tick(world, beforeLoss);
        world.ControlPoints[1].Team = null;
        Tick(world, 1);
        Assert.Empty(world.MapLogic.CaptureRoundEndCountdowns());
        Assert.False(Output(world, PlayerTeam.Red));
        Assert.False(world.MatchState.IsEnded);
        Assert.False(world.Level.DamageableZoneStates[3].IsDestroyed);
        world.ControlPoints[1].Team = PlayerTeam.Red;
        Tick(world, 330);
        Assert.False(world.MatchState.IsEnded);
        Tick(world, 1);
        Assert.Equal(PlayerTeam.Red, world.MatchState.WinnerTeam);
    }

    [Fact]
    public void TeamTakeoverCancelsTheOldCountdownAndStartsTheOtherTeamsLeadIn()
    {
        var world = CreateWorld();
        SetOwners(world, PlayerTeam.Red);
        Tick(world, 250);
        SetOwners(world, PlayerTeam.Blue);
        Tick(world, 1);
        Assert.False(Output(world, PlayerTeam.Red));
        Assert.Null(world.MapLogic.GetActiveRoundEndCountdown());
        Tick(world, 330);
        Assert.Equal(PlayerTeam.Blue, world.MatchState.WinnerTeam);
    }

    [Fact]
    public void RoundResetClearsPendingCountdownAndItsOutput()
    {
        var world = CreateWorld();
        SetOwners(world, PlayerTeam.Red);
        Tick(world, 180);
        Assert.True(Output(world, PlayerTeam.Red));
        world.MapLogic.EvaluateMapLogicGraph();
        Assert.Empty(world.MapLogic.CaptureRoundEndCountdowns());
        Assert.False(Output(world, PlayerTeam.Red));
    }

    [Fact]
    public void LateJoiningClientUsesAuthoritativeCountdownAcrossDeathAndCannotWinLocally()
    {
        var server = CreateWorld();
        SetOwners(server, PlayerTeam.Blue);
        Tick(server, 181);
        var states = server.MapLogic.CaptureRoundEndCountdowns();
        var client = CreateWorld();
        client.ClientPredictionMode = true;
        var alive = SimulationWorldSnapshotPresentationTests.CreatePlayerState(1, 101, "Local",
            PlayerTeam.Red, PlayerClass.Scout, true, 0);
        var snapshot = SimulationWorldSnapshotPresentationTests.CreateSnapshot(client, 1, alive) with
        {
            GameMode = (byte)GameModeKind.Conquest,
            MapRoundEndStates = states,
            ControlPoints = server.ControlPoints.Select(server.Snapshots.ToSnapshotControlPointState).ToArray(),
        };
        Assert.True(client.SnapshotApply.ApplySnapshot(snapshot));
        Assert.Equal(5f, client.MapLogic.GetActiveRoundEndCountdown()!.Value.SecondsRemaining);
        Assert.True(Output(client, PlayerTeam.Blue));
        Assert.True(client.SnapshotApply.ApplySnapshot(snapshot with { Frame = 2, Players = [alive with { IsAlive = false, Health = 0, RespawnTicks = 60 }] }));
        Assert.True(Output(client, PlayerTeam.Blue));
        Tick(client, 600);
        Assert.False(client.MatchState.IsEnded);
        Assert.False(client.Level.DamageableZoneStates[3].IsDestroyed);
        Assert.True(client.SnapshotApply.ApplySnapshot(snapshot with { Frame = 3, MapRoundEndStates = [] }));
        Assert.Null(client.MapLogic.GetActiveRoundEndCountdown());
        Assert.False(Output(client, PlayerTeam.Blue));
    }

    [Fact]
    public void InterceptedWinDoesNotPulseCompletionOrDestroyProps()
    {
        var world = CreateWorld();
        world.RoundEndDecisionInterceptor = _ => WorldDecisionResult.Cancel("test");
        SetOwners(world, PlayerTeam.Red);
        Tick(world, 331);
        Assert.False(world.MatchState.IsEnded);
        Assert.False(world.Level.DamageableZoneStates[3].IsDestroyed);
        Assert.False(world.Level.LogicGraph.GetOutput(world.Level.LogicGraph.NodeIndexByKey["completed"]));
    }

    [Fact]
    public void BuilderReferencesAndRuntimeImportsSupportTheNewConnections()
    {
        Assert.True(CustomMapBuilderEntityCatalog.TryGetDefinition(MapRoundEndMetadata.EntityType, out _));
        Assert.True(MapLogicMetadata.IsLogicOutputEntityType(MapRoundEndMetadata.EntityType));
        Assert.Contains(MapRoundEndMetadata.OnCompletePropertyKey, BuilderReferenceProperties.Logic);
        Assert.Contains(DamageableMetadata.DestroyWhenPropertyKey, BuilderReferenceProperties.Logic);
        var world = CreateWorld();
        var markers = world.Level.RoomObjects.ToList();
        MapDamageableRuntimePatch.ApplyHealWhenLinks(markers,
            [Entity(DamageableMetadata.DamageableEntityType, "zone", ("mapEntityId", "zone"), ("destroyWhen", "node:completed"))], world.Level.LogicGraph);
        Assert.Equal(world.Level.LogicGraph.NodeIndexByKey["completed"], markers[3].DamageableZone.DestroyWhenNodeIndex);
    }

    [Fact]
    public void OrdinarySimulationTicksDriveCountdownAndBroadcastItThroughTheProtocol()
    {
        var world = CreateWorld();
        SetOwners(world, PlayerTeam.Red);
        for (var tick = 0; tick < 181; tick++) world.AdvanceOneTick();
        var manager = new ServerBotManager(world, world.Config, new BotBrainPracticeBotController());
        var broadcaster = new SnapshotBroadcaster(world, world.Config, new Dictionary<byte, ClientSession>(),
            24, manager, 0, new ServerMapMetadataResolver(world), static (_, _, _) => { });
        var snapshot = broadcaster.CaptureCanonicalDemoSnapshot();
        Assert.Single(snapshot.MapRoundEndStates);
        Assert.Equal(150, snapshot.MapRoundEndStates[0].RemainingTicks);
        Assert.True(ProtocolCodec.TryDeserialize(ProtocolCodec.Serialize(snapshot, ProtocolCompressionSettings.Disabled), out var decoded));
        Assert.Equal(snapshot.MapRoundEndStates, Assert.IsType<SnapshotMessage>(decoded).MapRoundEndStates);
        for (var tick = 0; tick < 150; tick++) world.AdvanceOneTick();
        Assert.Equal(PlayerTeam.Red, world.MatchState.WinnerTeam);
    }

    [Fact]
    public void OwnershipLostOnTheLastSimulationTickCancelsBeforeTheGraphWouldNormallyRefresh()
    {
        var world = CreateWorld();
        SetOwners(world, PlayerTeam.Red);
        for (var tick = 0; tick < 330; tick++) world.AdvanceOneTick();
        Assert.Equal(1, Assert.Single(world.MapLogic.CaptureRoundEndCountdowns()).RemainingTicks);
        world.ControlPoints[1].Team = null;
        world.AdvanceOneTick();
        Assert.False(world.MatchState.IsEnded);
        Assert.Empty(world.MapLogic.CaptureRoundEndCountdowns());
        Assert.False(world.Level.DamageableZoneStates[3].IsDestroyed);
    }

    [Fact]
    public void CompletionBreaksEveryLinkedZoneAndRoundResetClearsTheCompletionPulse()
    {
        var world = CreateWorld(zoneCount: 3);
        SetOwners(world, PlayerTeam.Red);
        Tick(world, 331);
        for (var index = 3; index < 6; index++) Assert.True(world.Level.DamageableZoneStates[index].IsDestroyed);
        world.MapLogic.EvaluateMapLogicGraph();
        Assert.False(world.Level.LogicGraph.GetOutput(world.Level.LogicGraph.NodeIndexByKey["completed"]));
        for (var index = 3; index < 6; index++) Assert.False(world.Level.DamageableZoneStates[index].IsDestroyed);
    }

    [Fact]
    public void CountdownOutputCanHoldAnExistingLightColourAndRestoreItOnCancellation()
    {
        var world = CreateWorld();
        var light = MapLightMetadata.FromProperties(100, 100, new Dictionary<string, string>
            { [MapLightMetadata.TertiaryColorInputKey] = "node:redEnd" });
        var state = new MapLightRuntimeState();
        state.Reset([light]);
        SetOwners(world, PlayerTeam.Red);
        Tick(world, 91);
        state.Tick([light], world.Level.LogicGraph, 0.1f);
        Assert.Equal(MapLightColorState.Trigger, state.GetSample(0, light.Style).ColorState);
        world.ControlPoints[0].Team = null;
        Tick(world, 1);
        state.Tick([light], world.Level.LogicGraph, 0.1f);
        Assert.Equal(MapLightColorState.Normal, state.GetSample(0, light.Style).ColorState);
    }

    [Fact]
    public void NonContinuousCountdownCanFinishAfterAnInputPulse()
    {
        var world = CreateWorld();
        var graph = world.Level.LogicGraph;
        graph.RoundEnds = graph.RoundEnds.Select(definition => definition with { RequireContinuousSignal = false }).ToArray();
        SetOwners(world, PlayerTeam.Red);
        Tick(world, 1);
        SetOwners(world, null);
        Tick(world, 330);
        Assert.Equal(PlayerTeam.Red, world.MatchState.WinnerTeam);
    }

    private static bool Output(SimulationWorld world, PlayerTeam team) =>
        world.Level.LogicGraph.GetOutput(world.Level.LogicGraph.NodeIndexByKey[team == PlayerTeam.Red ? "redEnd" : "blueEnd"]);

    private static void SetOwners(SimulationWorld world, PlayerTeam? team)
    {
        foreach (var point in world.ControlPoints) point.Team = team;
    }

    private static void Tick(SimulationWorld world, int count)
    {
        for (var index = 0; index < count; index++)
        {
            world.Level.LogicGraph.EvaluateCombinatorial(world.ControlPoints);
            world.MapLogic.TickMapLogicTimers();
        }
    }

    private static MapImportedEntity Entity(string type, string key, params (string Key, string Value)[] properties)
    {
        var values = properties.ToDictionary(pair => pair.Key, pair => pair.Value);
        values["logicKey"] = key;
        return new(type, 0, 0, values);
    }

    private static SimulationWorld CreateWorld(int zoneCount = 1)
    {
        var entities = new List<MapImportedEntity>();
        foreach (var team in new[] { "red", "blue" })
        {
            for (var point = 1; point <= 3; point++)
                entities.Add(Entity("logicCpTrigger", $"{team}{point}", ("linkObjective", $"controlPoint{point}"), ("requiredOwner", team)));
            entities.Add(Entity("logicGate", $"{team}All", ("gateType", "and"),
                ("logicInput1", $"node:{team}1"), ("logicInput2", $"node:{team}2"), ("logicInput3", $"node:{team}3")));
            entities.Add(Entity(MapRoundEndMetadata.EntityType, $"{team}End", ("logicInput", $"node:{team}All"),
                ("winnerTeam", team), ("countdownDelay", "3"), ("countdownSeconds", "8"), ("onComplete", "node:completed")));
        }
        entities.Add(Entity("logicGate", "completed", ("gateType", "or")));
        var graph = MapLogicGraphImporter.BuildFromEntities(entities);
        var markers = Enumerable.Range(1, 3).Select(point => new RoomObjectMarker(RoomObjectType.ControlPoint,
            200 * point, 100, 32, 32, "", SourceName: $"controlPoint{point}")).ToList();
        for (var zone = 0; zone < zoneCount; zone++)
            markers.Add(new(RoomObjectType.DamageableZone, 800, 100 + zone * 50, 42, 42, "", SourceName: zone == 0 ? "zone" : $"zone{zone}",
                DamageableZone: new(100, -1, false, false, true, true, false,
                    DestroyWhenNodeIndex: graph.NodeIndexByKey["completed"])));
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false, TicksPerSecond = 30 });
        world.TestSetLevel(new SimpleLevel("cq_test", GameModeKind.Conquest, new WorldBounds(1024, 768), 1, null,
            1, 1, new SpawnPoint(100, 600), [new SpawnPoint(100, 600)], [new SpawnPoint(900, 600)], [],
            markers, 768, [], false, logicGraph: graph, controlPointSettings: new CustomMapControlPointSettings(true)));
        return world;
    }
}
