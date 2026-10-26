using OpenGarrison.Core;
using OpenGarrison.Protocol;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class MapScriptingTests
{
    [Fact]
    public void CaptureHandlerUsesLuaStateNamedObjectsAndGroupsAtTheSameOrigin()
    {
        var level = Level("""
            local captures = 0
            local function switch_spawns(team)
                if team == BLU then
                    group("red_spawn"):disable()
                    group("blue_spawn"):enable()
                end
            end
            function on_start() group("blue_spawn"):disable() end
            function on_capture(point, team)
                captures = captures + 1
                if point == 1 and captures == 2 then switch_spawns(team) end
            end
            """);
        var runtime = new MapScriptRuntime(level);
        runtime.Start();
        Assert.Null(runtime.Error);
        Assert.True(level.IsRoomObjectActive(0));
        Assert.False(level.IsRoomObjectActive(1));
        runtime.Capture(2, PlayerTeam.Red);
        runtime.Capture(1, PlayerTeam.Blue);
        Assert.Null(runtime.Error);
        Assert.False(level.IsRoomObjectActive(0));
        Assert.True(level.IsRoomObjectActive(1));
    }

    [Fact]
    public void ScriptOverrideSurvivesNodeEvaluationAndClearReturnsControlToNodes()
    {
        var level = Level("""
            function on_start() object("red"):disable() end
            function on_capture(point, team) object("red"):clear() end
            """);
        var runtime = new MapScriptRuntime(level);
        runtime.Start();
        level.RoomObjectLogicActiveMask[0] = true;
        Assert.False(level.IsRoomObjectActive(0));
        runtime.Capture(1, PlayerTeam.Red);
        Assert.True(level.IsRoomObjectActive(0));
    }

    [Fact]
    public void TimersAndTickCallbacksWorkWithoutNodes()
    {
        var level = Level("""
            local elapsed = 0
            function on_start()
                object("blue"):disable()
                map.after(0.1, function() object("blue"):enable() end)
            end
            function on_tick(dt)
                elapsed = elapsed + dt
                if elapsed > 0.15 then object("red"):disable() end
            end
            """);
        var world = World(level);
        world.AdvanceOneTick();
        Assert.Null(world.MapScriptError);
        Assert.False(level.IsRoomObjectActive(1));
        for (var tick = 0; tick < 8; tick++) world.AdvanceOneTick();
        Assert.True(level.IsRoomObjectActive(1));
        Assert.False(level.IsRoomObjectActive(0));
    }

    [Fact]
    public void ScriptCanPulseNamedNodesAndReceiveTheirSignals()
    {
        var graph = MapLogicGraphBuilder.Build([
            new MapLogicNodeDefinition { LogicKey = "raw-trigger-id", Kind = MapLogicNodeKind.Gate, GateType = MapLogicGateType.Or },
        ]);
        var level = Level("""
            function on_start() map.pulse("door_trigger") end
            function on_signal(name, active)
                if name == "door_trigger" and active then object("blue"):disable() end
            end
            """, graph);
        var world = World(level);
        world.MapLogic.EvaluateMapLogicGraph();
        Assert.Contains(world.MapRuntime.MapScriptSignalEvents, signal => signal.Name == "door_trigger" && signal.Active);
        world.AdvanceOneTick();
        Assert.Null(world.MapScriptError);
        Assert.False(level.IsRoomObjectActive(1));
        world.MapLifecycle.ResetModeStateForNewRound();
        Assert.True(level.IsRoomObjectActive(1));
        world.AdvanceOneTick();
        Assert.Null(world.MapScriptError);
        Assert.False(level.IsRoomObjectActive(1));
    }

    [Theory]
    [InlineData("while true do end")]
    [InlineData("function on_start() while true do end end")]
    public void InfiniteLoopsStopWithAnErrorInsteadOfHangingTheSimulation(string source)
    {
        var runtime = new MapScriptRuntime(Level(source));
        runtime.Start();
        Assert.Contains("budget", runtime.Error!);
    }

    [Fact]
    public void MissingNamesProduceUsefulErrorsAndDoNotAffectUnrelatedObjects()
    {
        var level = Level("function on_start() object('missing'):disable() end");
        var runtime = new MapScriptRuntime(level);
        runtime.Start();
        Assert.Contains("Unknown object 'missing'", runtime.Error!);
        Assert.Contains("map.lua", runtime.Error!);
        Assert.True(level.IsRoomObjectActive(0));
    }

    [Fact]
    public void ScriptsCannotAccessSystemOrFileLoadingApis()
    {
        var runtime = new MapScriptRuntime(Level("assert(io == nil and os == nil and require == nil and loadfile == nil and luanet == nil)"));
        runtime.Start();
        Assert.Null(runtime.Error);
    }

    [Fact]
    public void CaptureEventComesFromTheObjectiveSystemAndClientsOnlyApplyAuthoritativeObjectState()
    {
        var server = World(Level("function on_capture(point, team) if point == 1 and team == BLU then object('blue'):disable() end end"));
        server.MapLogic.EvaluateMapLogicGraph();
        server.MapLogic.NotifyMapScriptCapture(new ControlPointState(1,
            new RoomObjectMarker(RoomObjectType.ControlPoint, 0, 0, 42, 42, "", SourceName: "controlPoint1")), PlayerTeam.Blue);
        var state = server.Snapshots.ToSnapshotPlayerState(1, server.LocalPlayer, null, static _ => 0);
        var snapshot = SimulationWorldSnapshotPresentationTests.CreateSnapshot(server, 10, state) with
            { MapScriptObjectStates = server.MapLogic.CaptureMapScriptObjects(), MapScriptError = "map.lua:12: example diagnostic" };
        Assert.True(ProtocolCodec.TryDeserialize(ProtocolCodec.Serialize(snapshot), out var message));
        var client = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false }) { ClientPredictionMode = true };
        client.TestSetLevel(Level("error('clients must never execute the map script')"));
        Assert.True(client.SnapshotApply.ApplySnapshot(Assert.IsType<SnapshotMessage>(message)));
        Assert.Equal("map.lua:12: example diagnostic", client.MapScriptError);
        Assert.Null(client.MapRuntime.MapScript);
        Assert.False(client.Level.IsRoomObjectActive(1));
        client.MapLogic.ApplyAuthoritativeMapScriptObjects([]);
        Assert.True(client.Level.IsRoomObjectActive(1));
    }

    [Fact]
    public void SyntaxChecksReportLineNumbersWithoutRunningTheScript()
    {
        Assert.Null(MapScriptMetadata.Validate("while true do end"));
        Assert.Contains("map.lua", MapScriptMetadata.Validate("function on_start(\ninvalid")!);
    }

    [Fact]
    public void ScriptedSpawnGroupsChangeTheRealRespawnPool()
    {
        var home = new SpawnPoint(100, 250) { MapEntityId = "home", ObjectName = "home", ObjectGroups = "red_home" };
        var forward = new SpawnPoint(400, 250) { MapEntityId = "forward", ObjectName = "forward", ObjectGroups = "red_forward" };
        var level = new SimpleLevel("spawn_script", GameModeKind.CaptureTheFlag, new WorldBounds(700, 400), 1, null, 1, 1,
            home, [home, forward], [new SpawnPoint(600, 250)], [], [], 300, [new LevelSolid(0, 300, 700, 100)], false,
            mapScriptSource: """
                function on_start() group("red_forward"):disable() end
                function on_capture(point, team)
                    group("red_home"):disable()
                    group("red_forward"):enable()
                end
                """);
        var world = World(level);
        world.MapLogic.EvaluateMapLogicGraph();
        Assert.Equal(100, world.Spawns.ReserveSpawn(world.LocalPlayer, PlayerTeam.Red).X);
        world.MapLogic.NotifyMapScriptCapture(new ControlPointState(1,
            new RoomObjectMarker(RoomObjectType.ControlPoint, 0, 0, 42, 42, "", SourceName: "controlPoint1")), PlayerTeam.Blue);
        Assert.Equal(400, world.Spawns.ReserveSpawn(world.LocalPlayer, PlayerTeam.Red).X);
        world.MapLifecycle.ResetModeStateForNewRound();
        Assert.Equal(100, world.Spawns.ReserveSpawn(world.LocalPlayer, PlayerTeam.Red).X);
        Assert.Null(world.MapScriptError);
    }

    [Fact]
    public void DisablingEverySpawnDefersSpawningInsteadOfUsingADisabledFallback()
    {
        var home = new SpawnPoint(100, 250) { MapEntityId = "home", ObjectName = "home" };
        var level = new SimpleLevel("spawn_script", GameModeKind.CaptureTheFlag, new WorldBounds(700, 400), 1, null, 1, 1,
            home, [home], [new SpawnPoint(600, 250)], [], [], 300, [new LevelSolid(0, 300, 700, 100)], false,
            mapScriptSource: "function on_start() object('home'):disable() end");
        var world = World(level);
        world.MapLogic.EvaluateMapLogicGraph();
        world.LocalPlayer.Kill();
        Assert.False(world.Spawns.SpawnPlayerResolved(world.LocalPlayer, PlayerTeam.Red,
            world.Spawns.ReserveSpawn(world.LocalPlayer, PlayerTeam.Red)));
        Assert.False(world.LocalPlayer.IsAlive);
    }

    private static SimulationWorld World(SimpleLevel level)
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        world.TestSetLevel(level);
        return world;
    }

    private static SimpleLevel Level(string script, MapLogicGraph? graph = null)
    {
        var red = new RoomObjectMarker(RoomObjectType.CustomMapSprite, 100, 100, 48, 48, "",
            SourceName: "logicCustomSprite") { MapEntityId = "red-id", ObjectName = "red", ObjectGroups = "red_spawn" };
        var blue = red with { MapEntityId = "blue-id", ObjectName = "blue", ObjectGroups = "blue_spawn" };
        return new SimpleLevel("script_test", GameModeKind.CaptureTheFlag, new WorldBounds(700, 400), 1, null, 1, 1,
            new SpawnPoint(100, 250), [new SpawnPoint(100, 250)], [new SpawnPoint(600, 250)], [], [red, blue], 300,
            [new LevelSolid(0, 300, 700, 100)], false, logicGraph: graph, mapScriptSource: script,
            mapScriptNodeNames: new Dictionary<string, string> { ["door_trigger"] = "raw-trigger-id" });
    }
}
