using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class MapLogicOscillatorTests
{
    [Fact]
    public void AutostartOscillatorAlternatesTrueAndFalse()
    {
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "osc",
                Kind = MapLogicNodeKind.Oscillator,
                TrueTimeSeconds = 0.5f,
                FalseTimeSeconds = 0.5f,
                InitialValue = true,
                Autostart = true,
            },
        ]);

        graph.ResetOscillatorStates();
        graph.EvaluateCombinatorial(Array.Empty<ControlPointState>());
        graph.AdvanceOscillators(0f);
        Assert.True(graph.GetOutput(graph.NodeIndexByKey["osc"]));

        graph.AdvanceOscillators(0.5f);
        Assert.False(graph.GetOutput(graph.NodeIndexByKey["osc"]));

        graph.AdvanceOscillators(0.5f);
        Assert.True(graph.GetOutput(graph.NodeIndexByKey["osc"]));
    }

    [Fact]
    public void OscillatorStartsOnStartWhenRisingEdge()
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
                LogicKey = "osc",
                Kind = MapLogicNodeKind.Oscillator,
                TrueTimeSeconds = 1f,
                FalseTimeSeconds = 1f,
                InitialValue = true,
                StartWhenRef = "node:src",
            },
        ]);

        var marker = new RoomObjectMarker(RoomObjectType.ControlPoint, 0f, 0f, 32f, 32f, "ControlPointRedS", PlayerTeam.Red, "controlPoint1");
        var points = new[] { new ControlPointState(1, marker) { Team = PlayerTeam.Blue } };

        graph.ResetOscillatorStates();
        graph.EvaluateCombinatorial(points);
        graph.AdvanceOscillators(0f);
        Assert.False(graph.GetOutput(graph.NodeIndexByKey["osc"]));

        points[0].Team = PlayerTeam.Red;
        graph.EvaluateCombinatorial(points);
        graph.AdvanceOscillators(0f);
        Assert.True(graph.GetOutput(graph.NodeIndexByKey["osc"]));
    }

    [Fact]
    public void OscillatorStopsAndOutputsFalseWhenEndWhenIsTrue()
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
                LogicKey = "osc",
                Kind = MapLogicNodeKind.Oscillator,
                TrueTimeSeconds = 2f,
                FalseTimeSeconds = 2f,
                InitialValue = true,
                Autostart = true,
                EndWhenRef = "node:src",
            },
        ]);

        var marker = new RoomObjectMarker(RoomObjectType.ControlPoint, 0f, 0f, 32f, 32f, "ControlPointRedS", PlayerTeam.Red, "controlPoint1");
        var points = new[] { new ControlPointState(1, marker) { Team = PlayerTeam.Blue } };

        graph.ResetOscillatorStates();
        graph.EvaluateCombinatorial(points);
        graph.AdvanceOscillators(0.25f);
        Assert.True(graph.GetOutput(graph.NodeIndexByKey["osc"]));

        points[0].Team = PlayerTeam.Red;
        graph.EvaluateCombinatorial(points);
        graph.AdvanceOscillators(0f);
        Assert.False(graph.GetOutput(graph.NodeIndexByKey["osc"]));

        graph.AdvanceOscillators(1f);
        Assert.False(graph.GetOutput(graph.NodeIndexByKey["osc"]));
    }

    [Fact]
    public void OscillatorWithoutAutostartOutputsFalseOnMapStart()
    {
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "osc",
                Kind = MapLogicNodeKind.Oscillator,
                TrueTimeSeconds = 0.5f,
                FalseTimeSeconds = 0.5f,
                InitialValue = true,
                Autostart = false,
            },
        ]);

        graph.ResetOscillatorStates();
        graph.EvaluateCombinatorial(Array.Empty<ControlPointState>());
        graph.AdvanceOscillators(0f);
        Assert.False(graph.GetOutput(graph.NodeIndexByKey["osc"]));
    }

    [Fact]
    public void OscillatorWithStartWhenHighAtMapStartWaitsForRisingEdge()
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
                LogicKey = "osc",
                Kind = MapLogicNodeKind.Oscillator,
                TrueTimeSeconds = 1f,
                FalseTimeSeconds = 1f,
                InitialValue = true,
                StartWhenRef = "node:src",
            },
        ]);

        var marker = new RoomObjectMarker(RoomObjectType.ControlPoint, 0f, 0f, 32f, 32f, "ControlPointRedS", PlayerTeam.Red, "controlPoint1");
        var points = new[] { new ControlPointState(1, marker) { Team = PlayerTeam.Red } };

        graph.ResetOscillatorStates();
        graph.EvaluateCombinatorial(points);
        graph.AdvanceOscillators(0f);
        Assert.False(graph.GetOutput(graph.NodeIndexByKey["osc"]));

        points[0].Team = PlayerTeam.Blue;
        graph.EvaluateCombinatorial(points);
        graph.AdvanceOscillators(0f);
        Assert.False(graph.GetOutput(graph.NodeIndexByKey["osc"]));

        points[0].Team = PlayerTeam.Red;
        graph.EvaluateCombinatorial(points);
        graph.AdvanceOscillators(0f);
        Assert.True(graph.GetOutput(graph.NodeIndexByKey["osc"]));
    }

    [Fact]
    public void OscillatorWithInitialValueFalseStartsFalsePhase()
    {
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "osc",
                Kind = MapLogicNodeKind.Oscillator,
                TrueTimeSeconds = 0.4f,
                FalseTimeSeconds = 0.2f,
                InitialValue = false,
                Autostart = true,
            },
        ]);

        graph.ResetOscillatorStates();
        graph.EvaluateCombinatorial(Array.Empty<ControlPointState>());
        graph.AdvanceOscillators(0f);
        Assert.False(graph.GetOutput(graph.NodeIndexByKey["osc"]));

        graph.AdvanceOscillators(0.2f);
        Assert.True(graph.GetOutput(graph.NodeIndexByKey["osc"]));
    }
}
