using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class DestructiblePropsTests
{
    [Fact]
    public void DisableResetsOwnershipButAllowsRecaptureWhileDestructionSignalStaysHigh()
    {
        var graph = MapLogicGraphBuilder.Build([new MapLogicNodeDefinition
        { LogicKey = "destroy", Kind = MapLogicNodeKind.DamageTrigger, DamageableRoomObjectIndex = 0,
          TriggerWhenDestroyed = true, SignalMode = MapLogicSignalMode.Latch }]);
        RoomObjectMarker[] objects = [Zone() with { DamageableZone = Zone().DamageableZone with
            { OnDestroyedNodeIndex = -1, OnRedDestroyedNodeIndex = -1, OnBlueDestroyedNodeIndex = -1 } },
            new(RoomObjectType.ControlPoint, 200, 200, 32, 32, "", SourceName: "controlPoint2"),
            new(RoomObjectType.CaptureZone, 180, 180, 96, 96, "")];
        var world = CreateWorld(objects, graph, new([new(0, 1, MapLogicActivatorBehavior.Disable, false)]), GameModeKind.Conquest);
        var point = world.ControlPoints[0];
        point.Team = PlayerTeam.Blue;
        point.CappingTeam = PlayerTeam.Red;
        point.CappingTicks = 50;
        world.MapLogic.TryApplyDamageableZoneDamage(0, 100, PlayerTeam.Red);
        Assert.Null(point.Team);
        Assert.Null(point.CappingTeam);
        Assert.Equal(0, point.CappingTicks);
        Assert.True(world.Level.IsRoomObjectActive(1));

        world.NetworkPlayers.PrepareLocalPlayerJoin();
        Assert.True(world.NetworkPlayers.TrySetNetworkPlayerTeam(SimulationWorld.LocalPlayerSlot,
            PlayerTeam.Red, respawnLivePlayerImmediately: true));
        world.NetworkPlayers.CompleteLocalPlayerJoin(PlayerClass.Scout);
        world.LocalPlayer.TeleportTo(point.Marker.CenterX, point.Marker.CenterY);
        point.CapTimeTicks = 2;
        for (var tick = 0; tick < 6; tick++) world.AdvanceOneTick();
        Assert.True(graph.GetOutput(0));
        Assert.Equal(PlayerTeam.Red, point.Team);
    }

    [Theory]
    [InlineData("zoneInput", "disable")]
    [InlineData("zoneInput", "toggle")]
    [InlineData("destroyedSlot", "disable")]
    [InlineData("destroyedSlot", "toggle")]
    [InlineData("damageTrigger", "disable")]
    [InlineData("damageTrigger", "toggle")]
    public void DestructionWiresDirectlyToActivatorsAndChangesIndexedPointOwnership(string wiring, string behavior)
    {
        var zone = Zone(1) with { SourceName = "7cbc4f41", DamageableZone = Zone(1).DamageableZone with
            { OnDestroyedNodeIndex = -1, OnRedDestroyedNodeIndex = -1, OnBlueDestroyedNodeIndex = -1 } };
        CustomMapRoomObjectFactory.TryCreate("controlPoint", 200, 200, 1, 1,
            new Dictionary<string, string> { ["index"] = "2" }, out var point);
        var objects = new List<RoomObjectMarker> { zone, point };
        var zoneProperties = new Dictionary<string, string>
        { ["mapEntityId"] = "7cbc4f41", ["logicKey"] = "zone", ["health"] = "100", ["respawnTime"] = "1",
          ["onDestroyed"] = wiring == "destroyedSlot" ? "node:activate" : "" };
        MapImportedEntity[] entities = [new("logicDamageable", 100, 100, zoneProperties),
            new("controlPoint", 200, 200, new Dictionary<string, string> { ["mapEntityId"] = "8ff59841", ["index"] = "2" }),
            new("logicDamageTrigger", 100, 150, new Dictionary<string, string>
            { ["logicKey"] = "destroy", ["damageableEntity"] = "entity:logicDamageable@id:7cbc4f41",
              ["triggerWhenDestroyed"] = "true", ["signal"] = "impulse" }),
            new("logicActivator", 100, 200, new Dictionary<string, string>
            { ["logicKey"] = "activate", ["activatorEntity"] = "entity:controlPoint@id:8ff59841",
              ["logicInput"] = wiring == "zoneInput" ? "node:zone" : wiring == "damageTrigger" ? "node:destroy" : "",
              ["activatorBehavior"] = behavior })];
        var graph = MapLogicGraphImporter.BuildFromEntities(entities, objects);
        MapDamageableRuntimePatch.ApplyHealWhenLinks(objects, entities, graph);
        var activators = MapLogicActivatorImporter.BuildFromEntities(entities, objects, graph);
        Assert.True(MapLogicMetadata.IsLogicOutputEntityType("logicDamageable"));
        Assert.True(MapLogicMetadata.IsLogicOutputEntityType("logicActivator"));
        var world = CreateWorld(objects, graph, activators, GameModeKind.Conquest);
        var cp = world.ControlPoints[0];
        cp.Team = PlayerTeam.Red;
        world.MapLogic.TryApplyDamageableZoneDamage(0, 40, PlayerTeam.Red);
        Assert.Equal(PlayerTeam.Red, cp.Team);
        world.MapLogic.TryApplyDamageableZoneDamage(0, 60, PlayerTeam.Red);
        Assert.Equal(behavior == "toggle" ? PlayerTeam.Blue : (PlayerTeam?)null, cp.Team);
        for (var tick = 0; tick < 5; tick++) world.MapLogic.TickMapLogicTimers();
        Assert.Equal(behavior == "toggle" ? PlayerTeam.Blue : (PlayerTeam?)null, cp.Team);
        world.TestSetFrame(world.Config.TicksPerSecond);
        world.MapLogic.TickMapLogicTimers();
        Assert.Equal(100, world.MapLogic.GetDamageableZoneHealth(0));
        world.MapLogic.TryApplyDamageableZoneDamage(0, 100, PlayerTeam.Blue);
        Assert.Equal(behavior == "toggle" ? PlayerTeam.Red : (PlayerTeam?)null, cp.Team);
    }

    [Theory]
    [InlineData("7cbc4f41")]
    [InlineData("entity:logicDamageable@id:7cbc4f41")]
    public void DamageZoneLinksAcceptBuilderIdsAndPickerReferences(string reference)
    {
        RoomObjectMarker[] objects = [Zone() with { SourceName = "7cbc4f41" }];
        Assert.True(DamageableMetadata.TryResolveRoomObjectIndex(objects, reference, out var index));
        Assert.Equal(0, index);
        var sprite = CustomMapBuilderEntity.Create("logicCustomSprite", 100, 100,
            new Dictionary<string, string> { [DestructiblePropMetadata.ZoneKey] = reference }).NormalizeForEditing();
        Assert.Equal("entity:logicDamageable@id:7cbc4f41", sprite.Properties[DestructiblePropMetadata.ZoneKey]);
        Assert.False(DamageableMetadata.TryResolveRoomObjectIndex(objects, "missing", out _));
    }

    [Theory]
    [InlineData(MapLogicActivatorBehavior.Toggle, false)]
    [InlineData(MapLogicActivatorBehavior.Disable, false)]
    [InlineData(MapLogicActivatorBehavior.Toggle, true)]
    [InlineData(MapLogicActivatorBehavior.Disable, true)]
    public void IndexedBuilderPointImportsItsDamageActivatorAndConsumesDestruction(MapLogicActivatorBehavior behavior, bool exported)
    {
        var zone = Zone() with { SourceName = "7cbc4f41", DamageableZone = Zone().DamageableZone with
            { OnDestroyedNodeIndex = -1, OnRedDestroyedNodeIndex = -1, OnBlueDestroyedNodeIndex = -1 } };
        Assert.True(CustomMapRoomObjectFactory.TryCreate("controlPoint", 200, 200, 1, 1,
            new Dictionary<string, string> { ["index"] = "2" }, out var point));
        RoomObjectMarker[] objects = [zone, point];
        var graph = MapLogicGraphBuilder.Build([new MapLogicNodeDefinition
            { LogicKey = "766a48fc", Kind = MapLogicNodeKind.DamageTrigger, DamageableRoomObjectIndex = 0,
              TriggerWhenDestroyed = true, SignalMode = MapLogicSignalMode.Impulse }]);
        var builderPoint = CustomMapBuilderEntity.Create("controlPoint", 200, 200,
            new Dictionary<string, string> { ["mapEntityId"] = "8ff59841", ["index"] = "2" });
        var importedPoint = exported ? CustomMapBuilderEntityNormalization.ResolveEntityForExport(builderPoint) : builderPoint;
        Assert.True(MapLogicEntityReference.TryFindBuilderEntityIndex([importedPoint],
            "entity:controlPoint@id:8ff59841", out _));
        MapImportedEntity[] entities = [new(importedPoint.Type, importedPoint.X, importedPoint.Y, importedPoint.Properties),
            new("logicActivator", 100, 200, new Dictionary<string, string>
            { ["activatorEntity"] = "entity:controlPoint@id:8ff59841", ["logicInput"] = "node:766a48fc",
              ["activatorBehavior"] = behavior == MapLogicActivatorBehavior.Toggle ? "toggle" : "disable" })];
        var activators = MapLogicActivatorImporter.BuildFromEntities(entities, objects, graph);
        Assert.Single(activators.Activators);
        var world = CreateWorld(objects, graph, activators, GameModeKind.Conquest);
        world.ControlPoints[0].Team = PlayerTeam.Red;
        world.MapLogic.TryApplyDamageableZoneDamage(0, 100, PlayerTeam.Red);
        Assert.Equal(behavior == MapLogicActivatorBehavior.Toggle ? PlayerTeam.Blue : (PlayerTeam?)null,
            world.ControlPoints[0].Team);
        world.MapLogic.TickMapLogicTimers();
        Assert.Equal(behavior == MapLogicActivatorBehavior.Toggle ? PlayerTeam.Blue : (PlayerTeam?)null,
            world.ControlPoints[0].Team);
    }

    private static MapLogicGraph CreateSignals() => MapLogicGraphBuilder.Build(
        [new MapLogicNodeDefinition { LogicKey = "red", Kind = MapLogicNodeKind.Latch },
         new MapLogicNodeDefinition { LogicKey = "blue", Kind = MapLogicNodeKind.Latch },
         new MapLogicNodeDefinition { LogicKey = "any", Kind = MapLogicNodeKind.Latch }]);

    private static RoomObjectMarker Zone(float respawn = 0) => new(RoomObjectType.DamageableZone, 100, 100, 42, 42, "",
        SourceName: "zone", DamageableZone: new(100, -1, true, true, true, true, false, respawn, 0, 1, 2));

    [Theory]
    [InlineData(MapLogicSignalMode.Impulse)]
    [InlineData(MapLogicSignalMode.Latch)]
    public void DamageNodeOffTriggerDisablesLaserBeforeItsDestructionSignalIsReevaluated(MapLogicSignalMode signalMode)
    {
        var graph = MapLogicGraphBuilder.Build([new MapLogicNodeDefinition
        { LogicKey = "destroy", Kind = MapLogicNodeKind.DamageTrigger, DamageableRoomObjectIndex = 0,
          TriggerWhenDestroyed = true, SignalMode = signalMode }]);
        var light = MapLightMetadata.FromProperties(220, 100, new Dictionary<string, string>
        { [MapLightMetadata.KindKey] = "laser", [MapLightMetadata.OffInputKey] = "node:destroy" });
        var world = CreateWorld([Zone(1) with { DamageableZone = Zone(1).DamageableZone with
            { OnDestroyedNodeIndex = -1, OnRedDestroyedNodeIndex = -1, OnBlueDestroyedNodeIndex = -1 } }], graph);
        var level = world.Level;
        world.TestSetLevel(new SimpleLevel(level.Name, level.Mode, level.Bounds, 1, null, 0, 1,
            new SpawnPoint(0, 0), [], [], [], level.RoomObjects, 480, [], false, logicGraph: graph, mapLights: [light]));
        world.MapLogic.TickSpritesheetPlayback();
        Assert.True(world.Level.MapLightState.IsPowered(0, light.Style));
        world.MapLogic.TryApplyDamageableZoneDamage(0, 100, PlayerTeam.Red);
        world.MapLogic.TickMapLogicTimers();
        world.MapLogic.TickSpritesheetPlayback();
        Assert.False(world.Level.MapLightState.IsPowered(0, light.Style));
        world.AdvanceOneTick();
        Assert.False(world.Level.MapLightState.IsPowered(0, light.Style));
    }

    private static SimulationWorld CreateWorld(IReadOnlyList<RoomObjectMarker> markers, MapLogicGraph graph,
        MapLogicActivatorSet? activators = null, GameModeKind mode = GameModeKind.TeamDeathmatch)
    {
        var world = new SimulationWorld();
        world.TestSetLevel(new SimpleLevel("destructible-test", mode, new WorldBounds(512, 512), 1, null,
            0, 1, new SpawnPoint(0, 0), [], [], [], markers, 480, [], importedFromSource: false,
            logicGraph: graph, logicActivators: activators));
        return world;
    }

    [Theory]
    [InlineData(PlayerTeam.Red, 0, 1)]
    [InlineData(PlayerTeam.Blue, 1, 0)]
    public void DestructionFiresTheKillingTeamsSlotOnlyOnceAndRespawnRearmsIt(PlayerTeam team, int matching, int other)
    {
        var graph = CreateSignals();
        var world = CreateWorld([Zone(2)], graph);
        Assert.True(world.MapLogic.TryApplyDamageableZoneDamage(0, 40, team));
        Assert.False(graph.GetOutput(matching));
        Assert.False(graph.GetOutput(2));
        Assert.True(world.MapLogic.TryApplyDamageableZoneDamage(0, 70, team));
        Assert.True(graph.GetOutput(matching));
        Assert.False(graph.GetOutput(other));
        Assert.True(graph.GetOutput(2));
        Assert.Equal(1u, world.Level.DamageableZoneStates[0].Revision);
        Assert.False(world.MapLogic.TryApplyDamageableZoneDamage(0, 1, team));
        Assert.False(world.MapLogic.BlocksProjectileDamageableZone(0));
        world.TestSetFrame(world.Config.TicksPerSecond * 2 - 1);
        world.MapLogic.TickMapLogicTimers();
        Assert.Equal(0, world.MapLogic.GetDamageableZoneHealth(0));
        world.TestSetFrame(world.Config.TicksPerSecond * 2);
        world.MapLogic.TickMapLogicTimers();
        Assert.Equal(100, world.MapLogic.GetDamageableZoneHealth(0));
        Assert.Equal(2u, world.Level.DamageableZoneStates[0].Revision);
        Assert.Equal(0, world.Level.DamageableZoneStates[0].StateAgeTicks);
        Assert.True(world.MapLogic.BlocksProjectileDamageableZone(0));
        graph.EvaluateCombinatorial([]);
        graph.EvaluateCombinatorial([]);
        Assert.True(world.MapLogic.TryApplyDamageableZoneDamage(0, 100, team));
        Assert.True(graph.GetOutput(matching));
        Assert.Equal(3u, world.Level.DamageableZoneStates[0].Revision);
    }

    [Fact]
    public void RespawnOffKeepsZoneDestroyedAndUnknownTeamDoesNotFireTeamSlots()
    {
        var graph = CreateSignals();
        var world = CreateWorld([Zone()], graph);
        world.MapLogic.TryApplyDamageableZoneDamage(0, 100);
        Assert.False(graph.GetOutput(0));
        Assert.False(graph.GetOutput(1));
        Assert.True(graph.GetOutput(2));
        world.TestSetFrame(10000);
        world.MapLogic.TickMapLogicTimers();
        Assert.Equal(0, world.MapLogic.GetDamageableZoneHealth(0));
    }

    [Fact]
    public void ClientsKeepAuthoritativeDestructionInsteadOfPredictingHealthOrRespawn()
    {
        var world = CreateWorld([Zone(1)], CreateSignals());
        world.ClientPredictionMode = true;
        world.MapLogic.ApplyAuthoritativeDamageableZones([new(0, 0, 5, 700)]);
        Assert.False(world.MapLogic.TryApplyDamageableZoneDamage(0, 5, PlayerTeam.Red));
        world.TestSetFrame(10000);
        world.MapLogic.TickMapLogicTimers();
        Assert.True(world.Level.DamageableZoneStates[0].IsDestroyed);
        Assert.Equal(5u, world.Level.DamageableZoneStates[0].Revision);
        world.MapLogic.ApplyAuthoritativeDamageableZones([new(0, 100, 6, 0)]);
        Assert.False(world.Level.DamageableZoneStates[0].IsDestroyed);
        Assert.True(world.MapLogic.BlocksProjectileDamageableZone(0));
    }

    [Fact]
    public void AuthoritativeDestroyedPropsSurvivePlayerDeathAndRespawnSnapshots()
    {
        var world = CreateWorld([Zone(1)], CreateSignals());
        world.ClientPredictionMode = true;
        var player = SimulationWorldSnapshotPresentationTests.CreatePlayerState(1, 101, "Local", PlayerTeam.Red,
            PlayerClass.Scout, isAlive: true, gibDeaths: 0);
        foreach (var (alive, frame) in new[] { (true, 100ul), (false, 101ul), (true, 102ul) })
        {
            var snapshot = SimulationWorldSnapshotPresentationTests.CreateSnapshot(world, frame, player with { IsAlive = alive }) with
                { DamageableZoneStates = [new(0, 0, 5, 700)] };
            Assert.True(world.SnapshotApply.ApplySnapshot(snapshot, 1));
            Assert.True(world.Level.DamageableZoneStates[0].IsDestroyed);
            Assert.Equal(5u, world.Level.DamageableZoneStates[0].Revision);
            Assert.Equal(0, world.MapLogic.GetDamageableZoneHealth(0));
        }
    }

    [Theory]
    [InlineData(MapLogicActivatorBehavior.Disable)]
    [InlineData(MapLogicActivatorBehavior.Toggle)]
    public void CapturePointActivatorChangesOwnershipOncePerRisingEdge(MapLogicActivatorBehavior behavior)
    {
        var graph = MapLogicGraphBuilder.Build([new MapLogicNodeDefinition
        { LogicKey = "trigger", Kind = MapLogicNodeKind.CpTrigger, LinkedControlPointIndex = 2,
          OwnerRequirement = MapLogicCpTriggerOwnerRequirement.Red }]);
        RoomObjectMarker[] points = [new(RoomObjectType.ControlPoint, 50, 50, 32, 32, "", SourceName: "controlPoint1"),
            new(RoomObjectType.ControlPoint, 200, 50, 32, 32, "", SourceName: "controlPoint2")];
        var world = CreateWorld(points, graph, new([new(0, 0, behavior, false)]), GameModeKind.Conquest);
        var point = world.ControlPoints[0];
        var trigger = world.ControlPoints[1];
        point.Team = PlayerTeam.Red;
        point.CappingTeam = PlayerTeam.Blue;
        point.CappingTicks = 50;
        trigger.Team = PlayerTeam.Red;
        world.MapLogic.EvaluateMapLogicGraph(resetStatefulNodes: false);
        Assert.Equal(behavior == MapLogicActivatorBehavior.Toggle ? PlayerTeam.Blue : (PlayerTeam?)null, point.Team);
        Assert.Null(point.CappingTeam);
        Assert.Equal(0, point.CappingTicks);
        Assert.True(world.Level.IsRoomObjectActive(0));
        for (var i = 0; i < 4; i++) world.MapLogic.EvaluateMapLogicGraph(resetStatefulNodes: false);
        Assert.Equal(behavior == MapLogicActivatorBehavior.Toggle ? PlayerTeam.Blue : (PlayerTeam?)null, point.Team);
        if (behavior != MapLogicActivatorBehavior.Toggle) return;
        trigger.Team = PlayerTeam.Blue;
        world.MapLogic.EvaluateMapLogicGraph(resetStatefulNodes: false);
        trigger.Team = PlayerTeam.Red;
        world.MapLogic.EvaluateMapLogicGraph(resetStatefulNodes: false);
        Assert.Equal(PlayerTeam.Red, point.Team);
    }

    [Theory]
    [InlineData("fadeOut", 0.2f)]
    [InlineData("breakApart", 0f)]
    public void DestructionAndRespawnReachTheConfiguredOpacity(string effect, float minimum)
    {
        var settings = DestructiblePropMetadata.Parse(new Dictionary<string, string>
        { ["onDestruction"] = effect, ["destroyedOpacity"] = "20", ["destructionTransitionTime"] = "0.5", ["onRespawn"] = "staticWarp" });
        Assert.Equal(minimum, settings.GetOpacity(true, 0.5f), 4);
        Assert.Equal(1f, settings.GetOpacity(false, 0.5f));
        Assert.Equal(PropRespawnEffect.StaticWarp, settings.Respawn);
    }

    [Theory]
    [InlineData("NaN", 0f)]
    [InlineData("-20", 0f)]
    [InlineData("200", 100f)]
    public void MalformedOpacityIsSafe(string value, float expected)
        => Assert.Equal(expected / 100, DestructiblePropMetadata.Parse(new Dictionary<string, string> { ["destroyedOpacity"] = value }).MinimumOpacity);

    [Fact]
    public void HeldGenericToggleDoesNotOscillateEachEvaluation()
    {
        var graph = CreateSignals();
        var runtime = new MapLogicActivatorRuntimeState();
        var activators = new MapLogicActivatorSet([new(0, 0, MapLogicActivatorBehavior.Toggle, false)]);
        var mask = new[] { true };
        var start = new[] { false };
        graph.PulseExternalOutput(0);
        for (var i = 0; i < 5; i++)
        {
            MapLogicActivatorRuntime.Apply(graph, activators, mask, start, runtime);
            Assert.False(mask[0]);
        }
    }

    [Fact]
    public void MapScalingKeepsPropLinksAndDestructionSettings()
    {
        var settings = new DestructiblePropSettings(PropDestructionEffect.BreakApart, 0.3f, PropRespawnEffect.StaticWarp, 1f, "break", "spawn");
        var marker = new RoomObjectMarker(RoomObjectType.CustomMapSprite, 50, 50, 32, 32, "") { DestructibleZoneIndex = 0 };
        var world = CreateWorld([Zone() with { DestructionSettings = settings }, marker], CreateSignals());
        var scaled = SimpleLevelScaling.ApplyUniformScale(world.Level, 2);
        Assert.Equal(settings, scaled.GetRoomObject(0).DestructionSettings);
        Assert.Equal(0, scaled.GetRoomObject(1).DestructibleZoneIndex);
        Assert.Equal(100, scaled.GetRoomObject(1).X);
    }

    [Fact]
    public void CoincidentLightsKeepIndependentDamageZoneLinksByStableId()
    {
        var roomObjects = new List<RoomObjectMarker> { Zone() };
        var lights = new List<MapLightMarker>
        { MapLightMetadata.FromProperties(100, 100, new Dictionary<string, string> { ["mapEntityId"] = "first" }),
          MapLightMetadata.FromProperties(100, 100, new Dictionary<string, string> { ["mapEntityId"] = "second" }) };
        var entity = new MapImportedEntity("light", 100, 100, new Dictionary<string, string>
            { ["mapEntityId"] = "first", ["damageableEntity"] = MapLogicEntityReference.FormatEntityRef("logicDamageable", "zone") });
        DestructiblePropMetadata.ResolveLinks(roomObjects, lights, [entity]);
        Assert.Equal(0, lights[0].DestructibleZoneIndex);
        Assert.Equal(-1, lights[1].DestructibleZoneIndex);
    }

    [Fact]
    public void DestructionOutputsExpireAndCanPulseAgainWithoutAnyDamageTrigger()
    {
        var graph = CreateSignals();
        var world = CreateWorld([Zone(1)], graph);
        world.MapLogic.TryApplyDamageableZoneDamage(0, 100, PlayerTeam.Red);
        Assert.True(graph.GetOutput(0));
        world.MapLogic.TickMapLogicTimers();
        world.MapLogic.TickMapLogicTimers();
        Assert.False(graph.GetOutput(0));
        world.TestSetFrame(world.Config.TicksPerSecond);
        world.MapLogic.TickMapLogicTimers();
        Assert.True(world.MapLogic.TryApplyDamageableZoneDamage(0, 100, PlayerTeam.Red));
        Assert.True(graph.GetOutput(0));
    }
}
