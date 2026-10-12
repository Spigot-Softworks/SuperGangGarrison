using OpenGarrison.Core;
using OpenGarrison.Protocol;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

[Collection(ContentRootTestGroup.Name)]
public sealed class LightStateLifecycleTests
{
    [Fact]
    public void EachOwnedPointKeepsItsOwnLightColourWhenAnotherPointChanges()
    {
        var world = CreateWorld();
        var graph = MapLogicGraphBuilder.Build(Enumerable.Range(1, 2).SelectMany(cp => new[]
        {
            new MapLogicNodeDefinition { LogicKey = $"red{cp}", Kind = MapLogicNodeKind.CpTrigger,
                LinkedControlPointIndex = cp, OwnerRequirement = MapLogicCpTriggerOwnerRequirement.Red },
            new MapLogicNodeDefinition { LogicKey = $"blue{cp}", Kind = MapLogicNodeKind.CpTrigger,
                LinkedControlPointIndex = cp, OwnerRequirement = MapLogicCpTriggerOwnerRequirement.Blue },
        }).ToArray());
        var lights = Enumerable.Range(1, 2).Select(cp => MapLightMetadata.FromProperties(300 * cp, 100,
            new Dictionary<string, string>
            {
                [MapLightMetadata.ColorKey] = "ffffff", [MapLightMetadata.AltColorKey] = "ff0000",
                [MapLightMetadata.AltColor2Key] = "0000ff", [MapLightMetadata.ColorActionKey] = "holdAlt",
                [MapLightMetadata.ColorInputKey] = $"node:red{cp}",
                [MapLightMetadata.TertiaryColorInputKey] = $"node:blue{cp}",
            })).ToArray();
        world.TestSetLevel(new SimpleLevel("cq_independent_lights", GameModeKind.Conquest,
            new WorldBounds(1024, 768), 1, null, 1, 1, new SpawnPoint(100, 100),
            [new SpawnPoint(100, 100)], [new SpawnPoint(900, 100)], [], world.Level.RoomObjects,
            768, [], false, mapLights: lights, logicGraph: graph,
            controlPointSettings: new CustomMapControlPointSettings(true)));
        void Check(PlayerTeam cp1, PlayerTeam cp2)
        {
            world.ControlPoints[0].Team = cp1;
            world.ControlPoints[1].Team = cp2;
            world.AdvanceOneTick();
            foreach (var (team, index) in new[] { (cp1, 0), (cp2, 1) })
            {
                var expected = team == PlayerTeam.Red ? MapLightColorState.Swapped
                    : team == PlayerTeam.Blue ? MapLightColorState.Trigger : MapLightColorState.Normal;
                Assert.Equal(expected, world.Level.MapLightState.GetSample(index, lights[index].Style).ColorState);
            }
        }
        Check(PlayerTeam.Neutral, PlayerTeam.Neutral);
        Check(PlayerTeam.Red, PlayerTeam.Neutral);
        Check(PlayerTeam.Red, PlayerTeam.Red);
        Check(PlayerTeam.Red, PlayerTeam.Blue);
        world.LocalPlayer.Kill();
        for (var i = 0; i < 30; i++) world.AdvanceOneTick();
        Check(PlayerTeam.Red, PlayerTeam.Blue);
        world.NetworkPlayers.ForceRespawnLocalPlayer();
        Check(PlayerTeam.Red, PlayerTeam.Blue);
        Check(PlayerTeam.Blue, PlayerTeam.Blue);
        Check(PlayerTeam.Neutral, PlayerTeam.Blue);
        Check(PlayerTeam.Red, PlayerTeam.Blue);
    }

    private static SimulationWorld CreateWorld(string action = "holdAlt", bool tertiary = false)
    {
        var graph = MapLogicGraphBuilder.Build([
            new MapLogicNodeDefinition { LogicKey = "red", Kind = MapLogicNodeKind.CpTrigger,
                LinkedControlPointIndex = 1, OwnerRequirement = MapLogicCpTriggerOwnerRequirement.Red },
        ]);
        var light = MapLightMetadata.FromProperties(300, 100, new Dictionary<string, string>
        {
            [MapLightMetadata.ColorKey] = "ffffff",
            [MapLightMetadata.AltColorKey] = "ff0000",
            [MapLightMetadata.AltColor2Key] = "ff0000",
            [MapLightMetadata.ColorActionKey] = action,
            [tertiary ? MapLightMetadata.TertiaryColorInputKey : MapLightMetadata.ColorInputKey] = "node:red",
        });
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        world.TestSetLevel(new SimpleLevel("cp_light_lifecycle", GameModeKind.ControlPoint,
            new WorldBounds(1024, 768), 1, null, 1, 1, new SpawnPoint(100, 100),
            [new SpawnPoint(100, 100)], [new SpawnPoint(900, 100)], [],
            [new RoomObjectMarker(RoomObjectType.ControlPoint, 600, 100, 48, 24, "", SourceName: "ControlPoint1"),
             new RoomObjectMarker(RoomObjectType.ControlPoint, 800, 100, 48, 24, "", SourceName: "ControlPoint2"),
             new RoomObjectMarker(RoomObjectType.ControlPointSetupGate, 240, 72, 60, 6, "")],
            768, [], false, mapLights: [light], logicGraph: graph,
            controlPointSettings: new CustomMapControlPointSettings(true)));
        return world;
    }

    private static void AssertRed(SimulationWorld world)
    {
        var light = world.Level.MapLights[0];
        var emitters = new List<MapLightEmitter>();
        MapLightEmission.Evaluate(light, 0, 20, world.Level.MapLightState.GetSample(0, light.Style),
            MapLightMusic.Silent, emitters, null);
        Assert.Equal(new MapLightingColor(255, 0, 0), Assert.Single(emitters).Color);
        Assert.Equal(PlayerTeam.Red, world.ControlPoints[0].Team);
    }

    [Theory]
    [InlineData("holdAlt", false)]
    [InlineData("swap", false)]
    [InlineData("cycle", false)]
    [InlineData("holdAlt", true)]
    public void OfflineDeathAndRespawnPreserveOwnedPointLight(string action, bool tertiary)
    {
        var world = CreateWorld(action, tertiary);
        world.ControlPoints[0].Team = PlayerTeam.Blue;
        world.AdvanceOneTick();
        world.ControlPoints[0].Team = PlayerTeam.Red;
        world.AdvanceOneTick();
        AssertRed(world);
        world.LocalPlayer.Kill();
        for (var i = 0; i < 30; i++) world.AdvanceOneTick();
        AssertRed(world);
        world.NetworkPlayers.ForceRespawnLocalPlayer();
        world.AdvanceOneTick();
        Assert.True(world.LocalPlayer.IsAlive);
        AssertRed(world);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AuthoritativeColourSurvivesObjectiveResetDeathAndRespawn(bool tertiary)
    {
        var world = CreateWorld(tertiary: tertiary);
        world.ClientPredictionMode = true;
        var alive = SimulationWorldSnapshotPresentationTests.CreatePlayerState(1, 101, "Local",
            PlayerTeam.Red, PlayerClass.Scout, true, 0);
        var snapshot = SimulationWorldSnapshotPresentationTests.CreateSnapshot(world, 1, alive) with
        {
            GameMode = (byte)GameModeKind.ControlPoint,
            TimeRemainingTicks = 9000,
            ControlPoints = [new SnapshotControlPointState(1, 1, 0, 0, 300, 0, false),
                new SnapshotControlPointState(2, 2, 0, 0, 300, 0, false)],
            MapLightStates = [new SnapshotMapLightState(0, tertiary ? (byte)5 : (byte)3)],
        };
        // First leave setup, then enter it in a later snapshot to exercise graph reset.
        Assert.True(world.SnapshotApply.ApplySnapshot(snapshot));
        AssertRed(world);
        snapshot = snapshot with { Frame = 2, ControlPointSetupTicksRemaining = 30 * world.Config.TicksPerSecond };
        Assert.True(world.SnapshotApply.ApplySnapshot(snapshot));
        AssertRed(world);
        snapshot = snapshot with { Frame = 3, Players = [alive with { IsAlive = false, Health = 0, RespawnTicks = 120 }] };
        Assert.True(world.SnapshotApply.ApplySnapshot(snapshot));
        for (var i = 0; i < 30; i++) world.AdvanceOneTick();
        AssertRed(world);
        // Presentation/graph refresh while waiting for another server update must
        // not discard colours that the predicting client cannot reconstruct.
        world.MapLogic.EvaluateMapLogicGraph();
        world.AdvanceOneTick();
        AssertRed(world);
        Assert.True(world.SnapshotApply.ApplySnapshot(snapshot with { Frame = 4, Players = [alive] }));
        world.AdvanceOneTick();
        Assert.True(world.LocalPlayer.IsAlive);
        AssertRed(world);
        Assert.True(world.SnapshotApply.ApplySnapshot(snapshot with
        {
            Frame = 5, Players = [alive], MapLightStates = [new SnapshotMapLightState(0, 1)],
        }));
        var light = world.Level.MapLights[0];
        Assert.Equal(MapLightColorState.Normal, world.Level.MapLightState.GetSample(0, light.Style).ColorState);
    }
}
