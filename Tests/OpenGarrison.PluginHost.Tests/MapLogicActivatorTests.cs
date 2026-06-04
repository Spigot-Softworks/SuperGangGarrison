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
        var points = new[]
        {
            new ControlPointState(1, new RoomObjectMarker(RoomObjectType.ControlPoint, 0f, 0f, 32f, 32f, "ControlPointRedS", PlayerTeam.Red, "controlPoint1"))
            {
                Team = PlayerTeam.Blue,
            },
        };
        graph.Evaluate(points);
        MapLogicActivatorRuntime.Apply(graph, activators, mask, startApplied);
        Assert.True(mask[0]);
        points[0].Team = PlayerTeam.Red;
        graph.Evaluate(points);
        MapLogicActivatorRuntime.Apply(graph, activators, mask, startApplied);
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
        var points = new[]
        {
            new ControlPointState(1, new RoomObjectMarker(RoomObjectType.ControlPoint, 0f, 0f, 32f, 32f, "ControlPointRedS", PlayerTeam.Red, "controlPoint1"))
            {
                Team = PlayerTeam.Red,
            },
        };
        points[0].Team = PlayerTeam.Blue;
        graph.Evaluate(points);
        MapLogicActivatorRuntime.Apply(graph, activators, mask, startApplied);
        Assert.False(mask[0]);
        points[0].Team = PlayerTeam.Red;
        graph.Evaluate(points);
        MapLogicActivatorRuntime.Apply(graph, activators, mask, startApplied);
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
        var points = new[]
        {
            new ControlPointState(1, new RoomObjectMarker(RoomObjectType.ControlPoint, 0f, 0f, 32f, 32f, "ControlPointRedS", PlayerTeam.Red, "controlPoint1"))
            {
                Team = PlayerTeam.Red,
            },
        };
        graph.Evaluate(points);
        MapLogicActivatorRuntime.Apply(graph, activators, mask, startApplied);
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
        var points = new[]
        {
            new ControlPointState(1, new RoomObjectMarker(RoomObjectType.ControlPoint, 0f, 0f, 32f, 32f, "ControlPointRedS", PlayerTeam.Red, "controlPoint1"))
            {
                Team = PlayerTeam.Red,
            },
        };
        graph.Evaluate(points);
        MapLogicActivatorRuntime.Apply(graph, activators, mask, startApplied);
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
        var points = new[]
        {
            new ControlPointState(1, new RoomObjectMarker(RoomObjectType.ControlPoint, 0f, 0f, 32f, 32f, "ControlPointRedS", PlayerTeam.Red, "controlPoint1"))
            {
                Team = PlayerTeam.Red,
            },
        };
        graph.Evaluate(points);
        MapLogicActivatorRuntime.Apply(graph, activators, mask, startApplied);
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
        var points = new[]
        {
            new ControlPointState(1, new RoomObjectMarker(RoomObjectType.ControlPoint, 0f, 0f, 32f, 32f, "ControlPointRedS", PlayerTeam.Red, "controlPoint1"))
            {
                Team = PlayerTeam.Red,
            },
        };
        graph.Evaluate(points);
        MapLogicActivatorRuntime.Apply(graph, activators, mask, startApplied);
        Assert.False(mask[0]);
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
        MapLogicActivatorRuntime.Apply(graph, activators, mask, startApplied);
        Assert.False(mask[0]);
        MapLogicActivatorRuntime.Apply(graph, activators, mask, startApplied);
        Assert.True(mask[0]);
    }
}
