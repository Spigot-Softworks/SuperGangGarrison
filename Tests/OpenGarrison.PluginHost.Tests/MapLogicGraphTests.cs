using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class MapLogicGraphTests
{
    [Fact]
    public void AndGateOutputsTrueOnlyWhenBothInputsTrue()
    {
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition { LogicKey = "a", Kind = MapLogicNodeKind.CpTrigger, LinkedControlPointIndex = 1, OwnerRequirement = MapLogicCpTriggerOwnerRequirement.Red },
            new MapLogicNodeDefinition { LogicKey = "b", Kind = MapLogicNodeKind.CpTrigger, LinkedControlPointIndex = 2, OwnerRequirement = MapLogicCpTriggerOwnerRequirement.Red },
            new MapLogicNodeDefinition
            {
                LogicKey = "gate",
                Kind = MapLogicNodeKind.Gate,
                GateType = MapLogicGateType.And,
                InputRef1 = "node:a",
                InputRef2 = "node:b",
            },
        ]);

        var marker = new RoomObjectMarker(RoomObjectType.ControlPoint, 0f, 0f, 32f, 32f, "ControlPointRedS", PlayerTeam.Red, "controlPoint1");
        var points = new[]
        {
            new ControlPointState(1, marker with { SourceName = "controlPoint1" }) { Team = PlayerTeam.Red },
            new ControlPointState(2, marker with { SourceName = "controlPoint2" }) { Team = PlayerTeam.Blue },
        };

        graph.Evaluate(points);
        Assert.False(graph.GetOutput(graph.NodeIndexByKey["gate"]));

        points[1].Team = PlayerTeam.Red;
        graph.Evaluate(points);
        Assert.True(graph.GetOutput(graph.NodeIndexByKey["gate"]));
    }

    [Fact]
    public void NotGateInvertsInput()
    {
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition { LogicKey = "a", Kind = MapLogicNodeKind.CpTrigger, LinkedControlPointIndex = 1, OwnerRequirement = MapLogicCpTriggerOwnerRequirement.Red },
            new MapLogicNodeDefinition { LogicKey = "not", Kind = MapLogicNodeKind.Not, InputRef = "node:a" },
        ]);

        var marker = new RoomObjectMarker(RoomObjectType.ControlPoint, 0f, 0f, 32f, 32f, "ControlPointRedS", PlayerTeam.Red, "controlPoint1");
        var points = new[] { new ControlPointState(1, marker) { Team = PlayerTeam.Red } };
        graph.Evaluate(points);
        Assert.False(graph.GetOutput(graph.NodeIndexByKey["not"]));

        points[0].Team = PlayerTeam.Blue;
        graph.Evaluate(points);
        Assert.True(graph.GetOutput(graph.NodeIndexByKey["not"]));
    }

    [Fact]
    public void CpTriggerOwnedRequirementIsTrueForEitherTeamAndFalseWhenNeutral()
    {
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "owned",
                Kind = MapLogicNodeKind.CpTrigger,
                LinkedControlPointIndex = 1,
                OwnerRequirement = MapLogicCpTriggerOwnerRequirement.Owned,
            },
        ]);

        var marker = new RoomObjectMarker(RoomObjectType.ControlPoint, 0f, 0f, 32f, 32f, "ControlPointNeutralS", SourceName: "controlPoint1");
        var points = new[] { new ControlPointState(1, marker) };

        graph.Evaluate(points);
        Assert.False(graph.GetOutput(graph.NodeIndexByKey["owned"]));

        points[0].Team = PlayerTeam.Red;
        graph.Evaluate(points);
        Assert.True(graph.GetOutput(graph.NodeIndexByKey["owned"]));

        points[0].Team = PlayerTeam.Blue;
        graph.Evaluate(points);
        Assert.True(graph.GetOutput(graph.NodeIndexByKey["owned"]));

        points[0].Team = null;
        graph.Evaluate(points);
        Assert.False(graph.GetOutput(graph.NodeIndexByKey["owned"]));
    }

    [Theory]
    [InlineData("owned")]
    [InlineData("not-neutral")]
    [InlineData("notNeutral")]
    public void CpTriggerOwnerRequirementParserAcceptsNotNeutralAliases(string value)
    {
        Assert.True(MapLogicMetadata.TryParseCpTriggerOwnerRequirement(value, out var requirement));
        Assert.Equal(MapLogicCpTriggerOwnerRequirement.Owned, requirement);
    }

    [Fact]
    public void TimerWithTriggerOnStartFiresAfterCountdown()
    {
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "timer",
                Kind = MapLogicNodeKind.Timer,
                CountdownSeconds = 0.5f,
                TriggerOnStart = true,
            },
        ]);

        graph.ResetTimerStates();
        graph.EvaluateCombinatorial(Array.Empty<ControlPointState>());
        Assert.False(graph.GetOutput(graph.NodeIndexByKey["timer"]));

        graph.AdvanceTimers(0.25f);
        Assert.False(graph.GetOutput(graph.NodeIndexByKey["timer"]));

        graph.AdvanceTimers(0.25f);
        Assert.True(graph.GetOutput(graph.NodeIndexByKey["timer"]));
    }

    [Fact]
    public void TimerStartsOnTriggerRisingEdgeAndFiresAfterCountdown()
    {
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "src",
                Kind = MapLogicNodeKind.CpTrigger,
                LinkedControlPointIndex = 1,
                OwnerRequirement = MapLogicCpTriggerOwnerRequirement.Red,
            },
            new MapLogicNodeDefinition
            {
                LogicKey = "timer",
                Kind = MapLogicNodeKind.Timer,
                CountdownSeconds = 1f,
                InputRef = "node:src",
            },
        ]);

        var marker = new RoomObjectMarker(RoomObjectType.ControlPoint, 0f, 0f, 32f, 32f, "ControlPointRedS", PlayerTeam.Red, "controlPoint1");
        var points = new[] { new ControlPointState(1, marker) { Team = PlayerTeam.Blue } };

        graph.ResetTimerStates();
        graph.EvaluateCombinatorial(points);
        graph.AdvanceTimers(0.5f);
        Assert.False(graph.GetOutput(graph.NodeIndexByKey["timer"]));

        points[0].Team = PlayerTeam.Red;
        graph.EvaluateCombinatorial(points);
        graph.AdvanceTimers(0.5f);
        Assert.False(graph.GetOutput(graph.NodeIndexByKey["timer"]));

        graph.AdvanceTimers(0.5f);
        Assert.True(graph.GetOutput(graph.NodeIndexByKey["timer"]));
    }
}
