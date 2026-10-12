using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class ThreeInputAndLogicTests
{
    [Fact]
    public void ThreeInputAndRequiresAllInputsInBothEvaluationPaths()
    {
        // The gate precedes its sources, so the third dependency must be ordered too.
        var graph = CreateGraph(MapLogicGateType.And, "node:c");
        var gateIndex = graph.NodeIndexByKey["gate"];
        var points = CreatePoints();
        for (var bits = 0; bits < 8; bits++)
        {
            for (var index = 0; index < points.Length; index++)
                points[index].Team = (bits & (1 << index)) != 0 ? PlayerTeam.Red : PlayerTeam.Blue;
            graph.EvaluateCombinatorial(points);
            Assert.Equal(bits == 7, graph.GetOutput(gateIndex));
            graph.EvaluateCombinatorialPropagators();
            Assert.Equal(bits == 7, graph.GetOutput(gateIndex));
        }

        // A third-source pulse also propagates through the gate immediately.
        points[0].Team = points[1].Team = PlayerTeam.Red;
        points[2].Team = PlayerTeam.Blue;
        graph.EvaluateCombinatorial(points);
        Assert.False(graph.GetOutput(gateIndex));
        Assert.True(graph.PulseExternalOutput(graph.NodeIndexByKey["c"]));
        graph.EvaluateCombinatorialPropagators();
        Assert.True(graph.GetOutput(gateIndex));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData(" ", true)]
    [InlineData("node:missing", false)]
    [InlineData("invalid", false)]
    public void OptionalInputPreservesTwoInputMapsButBrokenConnectionsFailClosed(string? thirdRef, bool expected)
    {
        var graph = CreateGraph(MapLogicGateType.And, thirdRef);
        var points = CreatePoints();
        points[0].Team = points[1].Team = PlayerTeam.Red;
        graph.Evaluate(points);
        Assert.Equal(expected, graph.GetOutput(graph.NodeIndexByKey["gate"]));
    }

    [Theory]
    [InlineData(MapLogicGateType.Or)]
    [InlineData(MapLogicGateType.Xor)]
    public void OtherGateTypesRetainTwoInputBehavior(MapLogicGateType type)
    {
        var graph = CreateGraph(type, "node:c");
        var points = CreatePoints();
        for (var bits = 0; bits < 8; bits++)
        {
            for (var index = 0; index < points.Length; index++)
                points[index].Team = (bits & (1 << index)) != 0 ? PlayerTeam.Red : PlayerTeam.Blue;
            graph.Evaluate(points);
            var expected = type == MapLogicGateType.Or
                ? (bits & 3) != 0 : (bits & 3) is 1 or 2;
            Assert.Equal(expected, graph.GetOutput(graph.NodeIndexByKey["gate"]));
        }
    }

    private static MapLogicGraph CreateGraph(MapLogicGateType type, string? thirdRef) =>
        MapLogicGraphBuilder.Build([
            new MapLogicNodeDefinition { LogicKey = "gate", Kind = MapLogicNodeKind.Gate,
                GateType = type, InputRef1 = "node:a", InputRef2 = "node:b", InputRef3 = thirdRef },
            new MapLogicNodeDefinition { LogicKey = "a", Kind = MapLogicNodeKind.CpTrigger,
                LinkedControlPointIndex = 1, OwnerRequirement = MapLogicCpTriggerOwnerRequirement.Red },
            new MapLogicNodeDefinition { LogicKey = "b", Kind = MapLogicNodeKind.CpTrigger,
                LinkedControlPointIndex = 2, OwnerRequirement = MapLogicCpTriggerOwnerRequirement.Red },
            new MapLogicNodeDefinition { LogicKey = "c", Kind = MapLogicNodeKind.CpTrigger,
                LinkedControlPointIndex = 3, OwnerRequirement = MapLogicCpTriggerOwnerRequirement.Red },
        ]);

    private static ControlPointState[] CreatePoints() => Enumerable.Range(1, 3)
        .Select(index => new ControlPointState(index,
            new RoomObjectMarker(RoomObjectType.ControlPoint, 0, 0, 32, 32,
                "ControlPointRedS", PlayerTeam.Blue, $"controlPoint{index}"))).ToArray();
}
