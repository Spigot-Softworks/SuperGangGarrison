using System.Collections.Generic;
using OpenGarrison.Core;
using Xunit;
namespace OpenGarrison.PluginHost.Tests;
public sealed class MapLogicActivatorTests
{
    [Fact]
    public void ActivatorDisablesBarrierWhileTriggerSignalTrue()
    {
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "trigger",
                Kind = MapLogicNodeKind.CpTrigger,
                LinkedControlPointIndex = 1,
                OwnerRequirement = MapLogicCpTriggerOwnerRequirement.Red,
            },
        ]);
        var barrier = BarrierConfiguration.CreateMarker(
            40f,
            40f,
            1f,
            1f,
            BarrierConfiguration.FromProperties(new Dictionary<string, string>()));
        var roomObjects = new List<RoomObjectMarker> { barrier };
        var activators = new MapLogicActivatorSet(
        [
            new MapLogicActivator(graph.NodeIndexByKey["trigger"], 0, MapLogicActivatorBehavior.Disable, false),
        ]);
        var mask = new bool[roomObjects.Count];
        var startApplied = new bool[activators.Activators.Count];
        var runtimeState = MapLogicActivatorRuntimeState.CreateForActivatorCount(activators.Activators.Count);
        var points = new[]
        {
            new ControlPointState(1, new RoomObjectMarker(RoomObjectType.ControlPoint, 0f, 0f, 32f, 32f, "ControlPointRedS", PlayerTeam.Red, "controlPoint1"))
            {
                Team = PlayerTeam.Blue,
            },
        };
        graph.Evaluate(points);
        MapLogicActivatorRuntime.Apply(graph, activators, mask, startApplied, runtimeState);
        Assert.True(mask[0]);
        points[0].Team = PlayerTeam.Red;
        graph.Evaluate(points);
        MapLogicActivatorRuntime.Apply(graph, activators, mask, startApplied, runtimeState);
        Assert.False(mask[0]);
    }
    [Fact]
    public void ImporterResolvesActivatorTargetByBuilderTopLeftAnchor()
    {
        var barrier = BarrierConfiguration.CreateMarker(
            100f,
            200f,
            1f,
            1f,
            BarrierConfiguration.FromProperties(new Dictionary<string, string>()));
        var roomObjects = new List<RoomObjectMarker> { barrier };
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "gate",
                Kind = MapLogicNodeKind.Gate,
                GateType = MapLogicGateType.Or,
                InputRef1 = string.Empty,
            },
        ]);
        var entities = new[]
        {
            new MapImportedEntity(
                MapLogicMetadata.ActivatorEntityType,
                0f,
                0f,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [MapLogicMetadata.ActivatorEntityPropertyKey] = MapLogicEntityReference.FormatEntityRef("barrier", 100f, 200f),
                    [MapLogicMetadata.ActivatorBehaviorPropertyKey] = "disable",
                    [MapLogicMetadata.LogicInputPropertyKey] = "node:gate",
                }),
        };
        var activators = MapLogicActivatorImporter.BuildFromEntities(entities, roomObjects, graph);
        Assert.Single(activators.Activators);
        Assert.Equal(0, activators.Activators[0].TargetRoomObjectIndex);
    }
    [Fact]
    public void DisableActivatorFiresAfterImpulseCaptureTimerDelay()
    {
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "capture",
                Kind = MapLogicNodeKind.CpTrigger,
                LinkedControlPointIndex = 1,
                SignalMode = MapLogicSignalMode.Impulse,
                CpCaptureDetectMode = MapLogicCpCaptureDetectMode.AnyCapture,
            },
            new MapLogicNodeDefinition
            {
                LogicKey = "timer",
                Kind = MapLogicNodeKind.Timer,
                InputRef = "node:capture",
                CountdownSeconds = 0.1f,
                DelayedTrue = true,
                SignalMode = MapLogicSignalMode.Impulse,
            },
        ]);
        var barrier = BarrierConfiguration.CreateMarker(
            0f,
            0f,
            1f,
            1f,
            BarrierConfiguration.FromProperties(new Dictionary<string, string>()));
        var roomObjects = new List<RoomObjectMarker> { barrier };
        var activators = new MapLogicActivatorSet(
        [
            new MapLogicActivator(graph.NodeIndexByKey["timer"], 0, MapLogicActivatorBehavior.Disable, false),
        ]);
        var mask = new bool[roomObjects.Count];
        var startApplied = new bool[activators.Activators.Count];
        var runtimeState = MapLogicActivatorRuntimeState.CreateForActivatorCount(activators.Activators.Count);
        var points = new[]
        {
            new ControlPointState(1, new RoomObjectMarker(RoomObjectType.ControlPoint, 0f, 0f, 32f, 32f, "ControlPointRedS", PlayerTeam.Red, "controlPoint1"))
            {
                Team = PlayerTeam.Blue,
            },
        };

        graph.ResetCpTriggerStates(points);
        graph.ResetTimerStates();
        graph.EvaluateCombinatorial(points);
        MapLogicActivatorRuntime.Apply(graph, activators, mask, startApplied, runtimeState);
        Assert.True(mask[0]);

        points[0].Team = PlayerTeam.Red;
        graph.EvaluateCombinatorial(points);
        graph.AdvanceTimers(0f);
        MapLogicActivatorRuntime.Apply(graph, activators, mask, startApplied, runtimeState);
        Assert.True(mask[0]);

        graph.EvaluateCombinatorial(points);
        graph.AdvanceTimers(0.05f);
        MapLogicActivatorRuntime.Apply(graph, activators, mask, startApplied, runtimeState);
        Assert.True(mask[0]);

        graph.AdvanceTimers(0.05f);
        MapLogicActivatorRuntime.Apply(graph, activators, mask, startApplied, runtimeState);
        Assert.False(mask[0]);
    }

    [Fact]
    public void EnableActivatorReopensBarrierAfterCpCapture()
    {
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "disableGate",
                Kind = MapLogicNodeKind.CpTrigger,
                LinkedControlPointIndex = 1,
                OwnerRequirement = MapLogicCpTriggerOwnerRequirement.Blue,
            },
            new MapLogicNodeDefinition
            {
                LogicKey = "enableGate",
                Kind = MapLogicNodeKind.CpTrigger,
                LinkedControlPointIndex = 1,
                OwnerRequirement = MapLogicCpTriggerOwnerRequirement.Red,
            },
        ]);
        var barrier = BarrierConfiguration.CreateMarker(
            0f,
            0f,
            1f,
            1f,
            BarrierConfiguration.FromProperties(new Dictionary<string, string>()));
        var roomObjects = new List<RoomObjectMarker> { barrier };
        var activators = new MapLogicActivatorSet(
        [
            new MapLogicActivator(graph.NodeIndexByKey["disableGate"], 0, MapLogicActivatorBehavior.Disable, false),
            new MapLogicActivator(graph.NodeIndexByKey["enableGate"], 0, MapLogicActivatorBehavior.Enable, false),
        ]);
        var mask = new bool[roomObjects.Count];
        var startApplied = new bool[activators.Activators.Count];
        var runtimeState = MapLogicActivatorRuntimeState.CreateForActivatorCount(activators.Activators.Count);
        var points = new[]
        {
            new ControlPointState(1, new RoomObjectMarker(RoomObjectType.ControlPoint, 0f, 0f, 32f, 32f, "ControlPointRedS", PlayerTeam.Red, "controlPoint1"))
            {
                Team = PlayerTeam.Red,
            },
        };
        points[0].Team = PlayerTeam.Blue;
        graph.Evaluate(points);
        MapLogicActivatorRuntime.Apply(graph, activators, mask, startApplied, runtimeState);
        Assert.False(mask[0]);
        points[0].Team = PlayerTeam.Red;
        graph.Evaluate(points);
        MapLogicActivatorRuntime.Apply(graph, activators, mask, startApplied, runtimeState);
        Assert.True(mask[0]);
    }
    [Fact]
    public void EnableWinsOnTiedNodePriorityWhenBothSignalsFire()
    {
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "trigger",
                Kind = MapLogicNodeKind.CpTrigger,
                LinkedControlPointIndex = 1,
                OwnerRequirement = MapLogicCpTriggerOwnerRequirement.Red,
            },
        ]);
        var activators = new MapLogicActivatorSet(
        [
            new MapLogicActivator(graph.NodeIndexByKey["trigger"], 0, MapLogicActivatorBehavior.Disable, false),
            new MapLogicActivator(graph.NodeIndexByKey["trigger"], 0, MapLogicActivatorBehavior.Enable, false),
        ]);
        var mask = new bool[1];
        var startApplied = new bool[activators.Activators.Count];
        var runtimeState = MapLogicActivatorRuntimeState.CreateForActivatorCount(activators.Activators.Count);
        var points = new[]
        {
            new ControlPointState(1, new RoomObjectMarker(RoomObjectType.ControlPoint, 0f, 0f, 32f, 32f, "ControlPointRedS", PlayerTeam.Red, "controlPoint1"))
            {
                Team = PlayerTeam.Red,
            },
        };
        graph.Evaluate(points);
        MapLogicActivatorRuntime.Apply(graph, activators, mask, startApplied, runtimeState);
        Assert.True(mask[0]);
    }
    [Fact]
    public void HigherNodePriorityEnableWinsOverLowerPriorityDisableOnSameTarget()
    {
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "trigger",
                Kind = MapLogicNodeKind.CpTrigger,
                LinkedControlPointIndex = 1,
                OwnerRequirement = MapLogicCpTriggerOwnerRequirement.Red,
            },
        ]);
        var activators = new MapLogicActivatorSet(
        [
            new MapLogicActivator(graph.NodeIndexByKey["trigger"], 0, MapLogicActivatorBehavior.Disable, false, nodePriority: 0),
            new MapLogicActivator(graph.NodeIndexByKey["trigger"], 0, MapLogicActivatorBehavior.Enable, false, nodePriority: 5),
        ]);
        var mask = new bool[1];
        var startApplied = new bool[activators.Activators.Count];
        var runtimeState = MapLogicActivatorRuntimeState.CreateForActivatorCount(activators.Activators.Count);
        var points = new[]
        {
            new ControlPointState(1, new RoomObjectMarker(RoomObjectType.ControlPoint, 0f, 0f, 32f, 32f, "ControlPointRedS", PlayerTeam.Red, "controlPoint1"))
            {
                Team = PlayerTeam.Red,
            },
        };
        graph.Evaluate(points);
        MapLogicActivatorRuntime.Apply(graph, activators, mask, startApplied, runtimeState);
        Assert.True(mask[0]);
    }
    [Fact]
    public void HigherNodePriorityDisableWinsOverLowerPriorityEnableOnSameTarget()
    {
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "trigger",
                Kind = MapLogicNodeKind.CpTrigger,
                LinkedControlPointIndex = 1,
                OwnerRequirement = MapLogicCpTriggerOwnerRequirement.Red,
            },
        ]);
        var activators = new MapLogicActivatorSet(
        [
            new MapLogicActivator(graph.NodeIndexByKey["trigger"], 0, MapLogicActivatorBehavior.Disable, false, nodePriority: 10),
            new MapLogicActivator(graph.NodeIndexByKey["trigger"], 0, MapLogicActivatorBehavior.Enable, false, nodePriority: 0),
        ]);
        var mask = new bool[1];
        var startApplied = new bool[activators.Activators.Count];
        var runtimeState = MapLogicActivatorRuntimeState.CreateForActivatorCount(activators.Activators.Count);
        var points = new[]
        {
            new ControlPointState(1, new RoomObjectMarker(RoomObjectType.ControlPoint, 0f, 0f, 32f, 32f, "ControlPointRedS", PlayerTeam.Red, "controlPoint1"))
            {
                Team = PlayerTeam.Red,
            },
        };
        graph.Evaluate(points);
        MapLogicActivatorRuntime.Apply(graph, activators, mask, startApplied, runtimeState);
        Assert.False(mask[0]);
    }
    [Fact]
    public void LatestRisingEdgeWinsOverHigherNodePriorityOnSameTarget()
    {
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "disableTrigger",
                Kind = MapLogicNodeKind.CpTrigger,
                LinkedControlPointIndex = 1,
                OwnerRequirement = MapLogicCpTriggerOwnerRequirement.Blue,
            },
            new MapLogicNodeDefinition
            {
                LogicKey = "enableTrigger",
                Kind = MapLogicNodeKind.CpTrigger,
                LinkedControlPointIndex = 1,
                OwnerRequirement = MapLogicCpTriggerOwnerRequirement.Red,
            },
        ]);
        var activators = new MapLogicActivatorSet(
        [
            new MapLogicActivator(
                graph.NodeIndexByKey["disableTrigger"],
                0,
                MapLogicActivatorBehavior.Disable,
                false,
                nodePriority: 100),
            new MapLogicActivator(
                graph.NodeIndexByKey["enableTrigger"],
                0,
                MapLogicActivatorBehavior.Enable,
                false,
                nodePriority: 0),
        ]);
        var mask = new bool[1];
        var startApplied = new bool[activators.Activators.Count];
        var runtimeState = MapLogicActivatorRuntimeState.CreateForActivatorCount(activators.Activators.Count);
        var points = new[]
        {
            new ControlPointState(1, new RoomObjectMarker(RoomObjectType.ControlPoint, 0f, 0f, 32f, 32f, "ControlPointRedS", PlayerTeam.Red, "controlPoint1"))
            {
                Team = PlayerTeam.Blue,
            },
        };
        graph.Evaluate(points);
        MapLogicActivatorRuntime.Apply(graph, activators, mask, startApplied, runtimeState);
        Assert.False(mask[0]);

        points[0].Team = PlayerTeam.Red;
        graph.Evaluate(points);
        MapLogicActivatorRuntime.Apply(graph, activators, mask, startApplied, runtimeState);
        Assert.True(mask[0]);

        points[0].Team = PlayerTeam.Blue;
        graph.Evaluate(points);
        MapLogicActivatorRuntime.Apply(graph, activators, mask, startApplied, runtimeState);
        Assert.False(mask[0]);
    }
    [Fact]
    public void NodePriorityDoesNotInheritFromConnectedLogicInput()
    {
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "blueTrigger",
                Kind = MapLogicNodeKind.CpTrigger,
                LinkedControlPointIndex = 1,
                OwnerRequirement = MapLogicCpTriggerOwnerRequirement.Blue,
                NodePriority = 100,
            },
            new MapLogicNodeDefinition
            {
                LogicKey = "invertBlue",
                Kind = MapLogicNodeKind.Not,
                InputRef = "node:blueTrigger",
                NodePriority = 100,
            },
        ]);
        var activators = new MapLogicActivatorSet(
        [
            new MapLogicActivator(graph.NodeIndexByKey["invertBlue"], 0, MapLogicActivatorBehavior.Disable, false, nodePriority: 10),
            new MapLogicActivator(graph.NodeIndexByKey["invertBlue"], 0, MapLogicActivatorBehavior.Enable, false, nodePriority: 0),
        ]);
        var mask = new bool[1];
        var startApplied = new bool[activators.Activators.Count];
        var runtimeState = MapLogicActivatorRuntimeState.CreateForActivatorCount(activators.Activators.Count);
        var points = new[]
        {
            new ControlPointState(1, new RoomObjectMarker(RoomObjectType.ControlPoint, 0f, 0f, 32f, 32f, "ControlPointRedS", PlayerTeam.Red, "controlPoint1"))
            {
                Team = PlayerTeam.Red,
            },
        };
        graph.Evaluate(points);
        MapLogicActivatorRuntime.Apply(graph, activators, mask, startApplied, runtimeState);
        Assert.False(mask[0]);
    }
    [Fact]
    public void ActivatorBehaviorParserAndCycleIncludesToggle()
    {
        Assert.True(MapLogicMetadata.TryParseActivatorBehavior("toggle", out var behavior));
        Assert.Equal(MapLogicActivatorBehavior.Toggle, behavior);
        Assert.Equal("enable", MapLogicMetadata.CycleActivatorBehaviorPropertyValue("disable"));
        Assert.Equal("toggle", MapLogicMetadata.CycleActivatorBehaviorPropertyValue("enable"));
        Assert.Equal("disable", MapLogicMetadata.CycleActivatorBehaviorPropertyValue("toggle"));
        Assert.Equal("Toggle", MapLogicMetadata.GetActivatorBehaviorDisplayLabel("toggle"));
    }

    [Fact]
    public void ParseNodePriorityClampsToConfiguredRange()
    {
        Assert.Equal(0, MapLogicMetadata.ParseNodePriority((string?)null));
        Assert.Equal(100, MapLogicMetadata.ParseNodePriority("100"));
        Assert.Equal(100, MapLogicMetadata.ParseNodePriority("150"));
        Assert.Equal(42, MapLogicMetadata.AdjustNodePriority("40", 2));
        Assert.Equal(0, MapLogicMetadata.AdjustNodePriority("2", -5));
    }
    [Fact]
    public void ParseNodePriorityReadsLegacySignalPriorityProperty()
    {
        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [MapLogicMetadata.SignalPriorityPropertyKey] = "7",
        };
        Assert.Equal(7, MapLogicMetadata.ParseNodePriority(properties));
    }
    [Fact]
    public void ToggleAlternatesDisableAndEnableOnInputRisingEdges()
    {
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "trigger",
                Kind = MapLogicNodeKind.CpTrigger,
                LinkedControlPointIndex = 1,
                OwnerRequirement = MapLogicCpTriggerOwnerRequirement.Red,
            },
        ]);
        var activators = new MapLogicActivatorSet(
        [
            new MapLogicActivator(graph.NodeIndexByKey["trigger"], 0, MapLogicActivatorBehavior.Toggle, false),
        ]);
        var mask = new bool[1];
        var startApplied = new bool[activators.Activators.Count];
        var runtimeState = MapLogicActivatorRuntimeState.CreateForActivatorCount(activators.Activators.Count);
        var points = new[]
        {
            new ControlPointState(1, new RoomObjectMarker(RoomObjectType.ControlPoint, 0f, 0f, 32f, 32f, "ControlPointRedS", PlayerTeam.Red, "controlPoint1"))
            {
                Team = PlayerTeam.Blue,
            },
        };

        graph.Evaluate(points);
        MapLogicActivatorRuntime.Apply(graph, activators, mask, startApplied, runtimeState);
        Assert.True(mask[0]);

        points[0].Team = PlayerTeam.Red;
        graph.Evaluate(points);
        MapLogicActivatorRuntime.Apply(graph, activators, mask, startApplied, runtimeState);
        Assert.False(mask[0]);

        points[0].Team = PlayerTeam.Blue;
        graph.Evaluate(points);
        MapLogicActivatorRuntime.Apply(graph, activators, mask, startApplied, runtimeState);
        Assert.False(mask[0]);

        points[0].Team = PlayerTeam.Red;
        graph.Evaluate(points);
        MapLogicActivatorRuntime.Apply(graph, activators, mask, startApplied, runtimeState);
        Assert.True(mask[0]);

        points[0].Team = PlayerTeam.Blue;
        graph.Evaluate(points);
        MapLogicActivatorRuntime.Apply(graph, activators, mask, startApplied, runtimeState);
        Assert.True(mask[0]);

        points[0].Team = PlayerTeam.Red;
        graph.Evaluate(points);
        MapLogicActivatorRuntime.Apply(graph, activators, mask, startApplied, runtimeState);
        Assert.False(mask[0]);
    }

    [Fact]
    public void ToggleWithActivateOnStartDisablesThenEnablesOnFirstInputRisingEdge()
    {
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "trigger",
                Kind = MapLogicNodeKind.CpTrigger,
                LinkedControlPointIndex = 1,
                OwnerRequirement = MapLogicCpTriggerOwnerRequirement.Red,
            },
        ]);
        var activators = new MapLogicActivatorSet(
        [
            new MapLogicActivator(graph.NodeIndexByKey["trigger"], 0, MapLogicActivatorBehavior.Toggle, activateOnStart: true),
        ]);
        var mask = new bool[1];
        var startApplied = new bool[activators.Activators.Count];
        var runtimeState = MapLogicActivatorRuntimeState.CreateForActivatorCount(activators.Activators.Count);
        var points = new[]
        {
            new ControlPointState(1, new RoomObjectMarker(RoomObjectType.ControlPoint, 0f, 0f, 32f, 32f, "ControlPointRedS", PlayerTeam.Red, "controlPoint1"))
            {
                Team = PlayerTeam.Blue,
            },
        };

        graph.Evaluate(points);
        MapLogicActivatorRuntime.Apply(graph, activators, mask, startApplied, runtimeState);
        Assert.False(mask[0]);

        MapLogicActivatorRuntime.Apply(graph, activators, mask, startApplied, runtimeState);
        Assert.False(mask[0]);

        points[0].Team = PlayerTeam.Red;
        graph.Evaluate(points);
        MapLogicActivatorRuntime.Apply(graph, activators, mask, startApplied, runtimeState);
        Assert.True(mask[0]);

        points[0].Team = PlayerTeam.Blue;
        graph.Evaluate(points);
        MapLogicActivatorRuntime.Apply(graph, activators, mask, startApplied, runtimeState);
        Assert.True(mask[0]);

        points[0].Team = PlayerTeam.Red;
        graph.Evaluate(points);
        MapLogicActivatorRuntime.Apply(graph, activators, mask, startApplied, runtimeState);
        Assert.False(mask[0]);
    }

    [Fact]
    public void ActivateOnStartDisableStaysActiveUntilLinkedLogicFires()
    {
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "timer",
                Kind = MapLogicNodeKind.Timer,
                CountdownSeconds = 2f,
                TriggerOnStart = true,
            },
            new MapLogicNodeDefinition
            {
                LogicKey = "osc",
                Kind = MapLogicNodeKind.Oscillator,
                TrueTimeSeconds = 1f,
                FalseTimeSeconds = 1f,
                InitialValue = true,
                StartWhenRef = "node:timer",
            },
        ]);

        var sprite = new RoomObjectMarker(
            RoomObjectType.CustomMapSprite,
            10f,
            10f,
            32f,
            32f,
            string.Empty,
            SourceName: "customSprite",
            CustomMapSprite: new CustomMapSpriteConfiguration("icon", CustomMapSpriteLayerKind.Bg, 0, 1f));
        var activators = new MapLogicActivatorSet(
        [
            new MapLogicActivator(
                graph.NodeIndexByKey["osc"],
                0,
                MapLogicActivatorBehavior.Disable,
                activateOnStart: true),
        ]);
        var mask = new bool[1];
        Array.Fill(mask, true);
        var startApplied = new bool[activators.Activators.Count];
        var runtimeState = MapLogicActivatorRuntimeState.CreateForActivatorCount(activators.Activators.Count);

        graph.ResetTimerStates();
        graph.ResetOscillatorStates();
        graph.EvaluateCombinatorial(Array.Empty<ControlPointState>());
        graph.AdvanceTimers(0f);
        graph.AdvanceOscillators(0f);
        MapLogicActivatorRuntime.Apply(graph, activators, mask, startApplied, runtimeState);
        Assert.False(mask[0]);

        MapLogicActivatorRuntime.Apply(graph, activators, mask, startApplied, runtimeState);
        Assert.False(mask[0]);

        graph.AdvanceTimers(1.5f);
        graph.AdvanceOscillators(0f);
        MapLogicActivatorRuntime.Apply(graph, activators, mask, startApplied, runtimeState);
        Assert.False(mask[0]);
    }

    [Fact]
    public void ActivateOnStartRunsOncePerRound()
    {
        var graph = MapLogicGraph.Empty;
        var barrier = BarrierConfiguration.CreateMarker(
            10f,
            10f,
            1f,
            1f,
            BarrierConfiguration.FromProperties(new Dictionary<string, string>()));
        var activators = new MapLogicActivatorSet(
        [
            new MapLogicActivator(-1, 0, MapLogicActivatorBehavior.Disable, activateOnStart: true),
        ]);
        var mask = new bool[1];
        var startApplied = new bool[1];
        var runtimeState = MapLogicActivatorRuntimeState.CreateForActivatorCount(activators.Activators.Count);
        MapLogicActivatorRuntime.Apply(graph, activators, mask, startApplied, runtimeState);
        Assert.False(mask[0]);
        MapLogicActivatorRuntime.Apply(graph, activators, mask, startApplied, runtimeState);
        Assert.False(mask[0]);
    }

    [Fact]
    public void DisableActivatorLatchesTargetInactiveAfterSignalClears()
    {
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "trigger",
                Kind = MapLogicNodeKind.CpTrigger,
                LinkedControlPointIndex = 1,
                OwnerRequirement = MapLogicCpTriggerOwnerRequirement.Red,
            },
        ]);
        var barrier = BarrierConfiguration.CreateMarker(
            0f,
            0f,
            1f,
            1f,
            BarrierConfiguration.FromProperties(new Dictionary<string, string>()));
        var activators = new MapLogicActivatorSet(
        [
            new MapLogicActivator(graph.NodeIndexByKey["trigger"], 0, MapLogicActivatorBehavior.Disable, false),
        ]);
        var mask = new bool[1];
        var startApplied = new bool[activators.Activators.Count];
        var runtimeState = MapLogicActivatorRuntimeState.CreateForActivatorCount(activators.Activators.Count);
        var points = new[]
        {
            new ControlPointState(1, new RoomObjectMarker(RoomObjectType.ControlPoint, 0f, 0f, 32f, 32f, "ControlPointRedS", PlayerTeam.Red, "controlPoint1"))
            {
                Team = PlayerTeam.Blue,
            },
        };

        points[0].Team = PlayerTeam.Red;
        graph.Evaluate(points);
        MapLogicActivatorRuntime.Apply(graph, activators, mask, startApplied, runtimeState);
        Assert.False(mask[0]);

        points[0].Team = PlayerTeam.Blue;
        graph.Evaluate(points);
        MapLogicActivatorRuntime.Apply(graph, activators, mask, startApplied, runtimeState);
        Assert.False(mask[0]);
    }
}
