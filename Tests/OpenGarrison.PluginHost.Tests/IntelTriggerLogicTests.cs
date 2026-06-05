using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class IntelTriggerLogicTests
{
    [Fact]
    public void IntelTriggerLatchIsTrueWhenIntelIsAtBase()
    {
        var redIntel = CreateIntel(PlayerTeam.Red, 0f, 0f);
        var blueIntel = CreateIntel(PlayerTeam.Blue, 100f, 100f);
        var graph = BuildLatchGraph(IntelTriggerIntelFilter.Red, IntelTriggerLatchState.AtBase);
        var context = new IntelTriggerEvaluationContext(redIntel, blueIntel);

        graph.EvaluateIntelTriggers(context);

        Assert.True(graph.GetOutput(graph.NodeIndexByKey["intel"]));
    }

    [Fact]
    public void IntelTriggerLatchIsFalseWhenIntelIsCarried()
    {
        var redIntel = CreateIntel(PlayerTeam.Red, 0f, 0f);
        redIntel.PickUp();
        var blueIntel = CreateIntel(PlayerTeam.Blue, 100f, 100f);
        var graph = BuildLatchGraph(IntelTriggerIntelFilter.Red, IntelTriggerLatchState.AtBase);
        var context = new IntelTriggerEvaluationContext(redIntel, blueIntel);

        graph.EvaluateIntelTriggers(context);

        Assert.False(graph.GetOutput(graph.NodeIndexByKey["intel"]));
    }

    [Fact]
    public void IntelTriggerLatchAnyIsTrueWhenEitherIntelMatches()
    {
        var redIntel = CreateIntel(PlayerTeam.Red, 0f, 0f);
        redIntel.PickUp();
        var blueIntel = CreateIntel(PlayerTeam.Blue, 100f, 100f);
        var graph = BuildLatchGraph(IntelTriggerIntelFilter.Any, IntelTriggerLatchState.Carried);
        var context = new IntelTriggerEvaluationContext(redIntel, blueIntel);

        graph.EvaluateIntelTriggers(context);

        Assert.True(graph.GetOutput(graph.NodeIndexByKey["intel"]));
    }

    [Fact]
    public void IntelTriggerImpulseFiresOnPickup()
    {
        var redIntel = CreateIntel(PlayerTeam.Red, 0f, 0f);
        var blueIntel = CreateIntel(PlayerTeam.Blue, 100f, 100f);
        var graph = BuildImpulseGraph(IntelTriggerIntelFilter.Red, onPickup: true);
        var context = new IntelTriggerEvaluationContext(redIntel, blueIntel);
        graph.ResetIntelTriggerStates(context);
        graph.EvaluateIntelTriggers(context);
        Assert.False(graph.GetOutput(graph.NodeIndexByKey["intel"]));

        redIntel.PickUp();
        context = new IntelTriggerEvaluationContext(redIntel, blueIntel);
        graph.EvaluateIntelTriggers(context);
        Assert.True(graph.GetOutput(graph.NodeIndexByKey["intel"]));

        graph.EvaluateIntelTriggers(context);
        Assert.False(graph.GetOutput(graph.NodeIndexByKey["intel"]));
    }

    [Fact]
    public void IntelTriggerImpulseFiresOnDrop()
    {
        var redIntel = CreateIntel(PlayerTeam.Red, 0f, 0f);
        redIntel.PickUp();
        var blueIntel = CreateIntel(PlayerTeam.Blue, 100f, 100f);
        var graph = BuildImpulseGraph(IntelTriggerIntelFilter.Red, onDrop: true);
        var context = new IntelTriggerEvaluationContext(redIntel, blueIntel);
        graph.ResetIntelTriggerStates(context);
        graph.EvaluateIntelTriggers(context);
        Assert.False(graph.GetOutput(graph.NodeIndexByKey["intel"]));

        redIntel.Drop(10f, 10f, 300);
        context = new IntelTriggerEvaluationContext(redIntel, blueIntel);
        graph.EvaluateIntelTriggers(context);
        Assert.True(graph.GetOutput(graph.NodeIndexByKey["intel"]));
    }

    [Fact]
    public void IntelTriggerImpulseFiresOnCapture()
    {
        var redIntel = CreateIntel(PlayerTeam.Red, 0f, 0f);
        redIntel.PickUp();
        var blueIntel = CreateIntel(PlayerTeam.Blue, 100f, 100f);
        var graph = BuildImpulseGraph(IntelTriggerIntelFilter.Red, onCapture: true);
        var context = new IntelTriggerEvaluationContext(redIntel, blueIntel);
        graph.ResetIntelTriggerStates(context);
        graph.EvaluateIntelTriggers(context);
        Assert.False(graph.GetOutput(graph.NodeIndexByKey["intel"]));

        redIntel.ResetToBase();
        context = new IntelTriggerEvaluationContext(redIntel, blueIntel);
        graph.EvaluateIntelTriggers(context);
        Assert.True(graph.GetOutput(graph.NodeIndexByKey["intel"]));
    }

    [Fact]
    public void IntelTriggerImpulseFiresOnResetAfterDrop()
    {
        var redIntel = CreateIntel(PlayerTeam.Red, 0f, 0f);
        redIntel.Drop(10f, 10f, 300);
        var blueIntel = CreateIntel(PlayerTeam.Blue, 100f, 100f);
        var graph = BuildImpulseGraph(IntelTriggerIntelFilter.Red, onReset: true);
        var context = new IntelTriggerEvaluationContext(redIntel, blueIntel);
        graph.ResetIntelTriggerStates(context);
        graph.EvaluateIntelTriggers(context);
        Assert.False(graph.GetOutput(graph.NodeIndexByKey["intel"]));

        redIntel.ResetToBase();
        context = new IntelTriggerEvaluationContext(redIntel, blueIntel);
        graph.EvaluateIntelTriggers(context);
        Assert.True(graph.GetOutput(graph.NodeIndexByKey["intel"]));
    }

    [Fact]
    public void ImporterBuildsIntelTriggerNode()
    {
        var entities = new[]
        {
            new MapImportedEntity(
                IntelTriggerMetadata.IntelTriggerEntityType,
                50f,
                60f,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [IntelTriggerMetadata.IntelPropertyKey] = "blue",
                    [MapLogicSignalMetadata.SignalPropertyKey] = "impulse",
                    [IntelTriggerMetadata.OnPickupPropertyKey] = "true",
                    ["logicKey"] = "intelNode",
                }),
        };

        var graph = MapLogicGraphImporter.BuildFromEntities(entities);
        var node = graph.Nodes[0];

        Assert.Equal(MapLogicNodeKind.IntelTrigger, node.Kind);
        Assert.Equal(IntelTriggerIntelFilter.Blue, node.IntelTriggerIntelFilter);
        Assert.Equal(MapLogicSignalMode.Impulse, node.SignalMode);
        Assert.True(node.IntelTriggerOnPickup);
    }

    [Fact]
    public void GateReadsIntelTriggerOutput()
    {
        var redIntel = CreateIntel(PlayerTeam.Red, 0f, 0f);
        var blueIntel = CreateIntel(PlayerTeam.Blue, 100f, 100f);
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "intel",
                Kind = MapLogicNodeKind.IntelTrigger,
                SignalMode = MapLogicSignalMode.Latch,
                IntelTriggerIntelFilter = IntelTriggerIntelFilter.Red,
                IntelTriggerLatchState = IntelTriggerLatchState.AtBase,
            },
            new MapLogicNodeDefinition
            {
                LogicKey = "gate",
                Kind = MapLogicNodeKind.Gate,
                GateType = MapLogicGateType.And,
                InputRef1 = "node:intel",
                InputRef2 = "node:intel",
            },
        ]);
        var context = new IntelTriggerEvaluationContext(redIntel, blueIntel);
        graph.EvaluateIntelTriggers(context);

        Assert.True(graph.GetOutput(graph.NodeIndexByKey["gate"]));
    }

    private static MapLogicGraph BuildLatchGraph(IntelTriggerIntelFilter filter, IntelTriggerLatchState latchState)
    {
        return MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "intel",
                Kind = MapLogicNodeKind.IntelTrigger,
                SignalMode = MapLogicSignalMode.Latch,
                IntelTriggerIntelFilter = filter,
                IntelTriggerLatchState = latchState,
            },
        ]);
    }

    private static MapLogicGraph BuildImpulseGraph(
        IntelTriggerIntelFilter filter,
        bool onPickup = false,
        bool onDrop = false,
        bool onCapture = false,
        bool onReset = false)
    {
        return MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "intel",
                Kind = MapLogicNodeKind.IntelTrigger,
                SignalMode = MapLogicSignalMode.Impulse,
                IntelTriggerIntelFilter = filter,
                IntelTriggerOnPickup = onPickup,
                IntelTriggerOnDrop = onDrop,
                IntelTriggerOnCapture = onCapture,
                IntelTriggerOnReset = onReset,
            },
        ]);
    }

    private static TeamIntelligenceState CreateIntel(PlayerTeam team, float homeX, float homeY)
    {
        return new TeamIntelligenceState(team, homeX, homeY);
    }
}
