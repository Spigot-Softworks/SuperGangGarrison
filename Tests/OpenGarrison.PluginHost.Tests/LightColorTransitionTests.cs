using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class LightColorTransitionTests
{
    [Theory]
    [InlineData(MapLightTransition.Instant)]
    [InlineData(MapLightTransition.Fade)]
    [InlineData(MapLightTransition.Warmup)]
    [InlineData(MapLightTransition.Blink)]
    public void ColourTransitionsUseTheSameCurvesAsPowerOn(MapLightTransition transition)
    {
        var powerStyle = MapLightStyle.Default with { Transition = transition };
        var colourStyle = MapLightStyle.Default with { ColorTransition = transition };
        foreach (var progress in new[] { 0f, 0.1f, 0.25f, 0.5f, 0.9f, 1f })
        {
            var expected = MapLightEmission.GetPowerLevel(powerStyle,
                new(true, progress, MapLightColorState.Normal), 2.37, 7);
            var actual = MapLightEmission.GetPowerLevel(colourStyle,
                new(true, 1, MapLightColorState.Swapped, ColorProgress: progress), 2.37, 7);
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void HeldColourChangesAnimateOnceAndRestartOnTheNextChange()
    {
        var rig = new Rig();
        rig.Tick(0);
        Assert.Equal(1, rig.Sample.ColorProgress);
        rig.Points[0].Team = PlayerTeam.Red;
        rig.Tick(0);
        Assert.Equal(MapLightColorState.Swapped, rig.Sample.ColorState);
        Assert.Equal(0, rig.Sample.ColorProgress);
        rig.Tick(0.25f);
        Assert.Equal(0.5f, rig.Sample.ColorProgress);
        rig.Tick(0.25f);
        Assert.Equal(1, rig.Sample.ColorProgress);
        rig.Tick(0.1f);
        Assert.Equal(1, rig.Sample.ColorProgress);
        rig.Points[0].Team = PlayerTeam.Blue;
        rig.Tick(0.125f);
        Assert.Equal(MapLightColorState.Normal, rig.Sample.ColorState);
        Assert.Equal(0.25f, rig.Sample.ColorProgress);
    }

    [Fact]
    public void TertiarySelectionOnlyRestartsWhenTheVisibleSelectionChanges()
    {
        var rig = new Rig();
        rig.Points[1].Team = PlayerTeam.Red;
        rig.Tick(0.25f);
        Assert.Equal(MapLightColorState.Trigger, rig.Sample.ColorState);
        Assert.Equal(0.5f, rig.Sample.ColorProgress);
        rig.Points[0].Team = PlayerTeam.Red;
        rig.Tick(0.125f);
        Assert.Equal(MapLightColorState.Trigger, rig.Sample.ColorState);
        Assert.Equal(0.75f, rig.Sample.ColorProgress);
        rig.Points[1].Team = PlayerTeam.Blue;
        rig.Tick(0.125f);
        Assert.Equal(MapLightColorState.Swapped, rig.Sample.ColorState);
        Assert.Equal(0.25f, rig.Sample.ColorProgress);
    }

    [Fact]
    public void NetworkChangesAnimateButRepeatedSnapshotsAndLocalResetsDoNotRestartThem()
    {
        var rig = new Rig();
        var client = new MapLightRuntimeState();
        client.Bind(rig.Lights, MapLogicGraph.Empty);
        client.ApplyNetworkState(0, 3); // Initially powered, secondary colour selected.
        client.Tick(rig.Lights, MapLogicGraph.Empty, 0);
        Assert.Equal(1, client.GetSample(0, rig.Light.Style).ColorProgress);
        client.ApplyNetworkState(0, 5); // Tertiary selected.
        client.Tick(rig.Lights, MapLogicGraph.Empty, 0.125f);
        Assert.Equal(0.25f, client.GetSample(0, rig.Light.Style).ColorProgress);
        client.ApplyNetworkState(0, 5);
        client.Reset(rig.Lights);
        client.Tick(rig.Lights, MapLogicGraph.Empty, 0.125f);
        var sample = client.GetSample(0, rig.Light.Style);
        Assert.Equal(MapLightColorState.Trigger, sample.ColorState);
        Assert.Equal(0.5f, sample.ColorProgress);
        client.Tick(rig.Lights, MapLogicGraph.Empty, 0.25f);
        Assert.Equal(1, client.GetSample(0, rig.Light.Style).ColorProgress);
    }

    [Fact]
    public void ColourAndPowerTransitionsComposeWithoutChangingLogicPower()
    {
        var rig = new Rig(new()
        {
            [MapLightMetadata.StartOnKey] = "false", [MapLightMetadata.FollowInputKey] = "node:power",
            [MapLightMetadata.TransitionKey] = "fade", [MapLightMetadata.FadeTimeKey] = "1",
        });
        rig.Points[0].Team = rig.Points[2].Team = PlayerTeam.Red;
        rig.Tick(0.25f);
        Assert.True(rig.State.IsPowered(0, rig.Light.Style));
        Assert.Equal(0.25f, rig.Sample.Progress);
        Assert.Equal(0.5f, rig.Sample.ColorProgress);
        Assert.Equal(0.078125f, MapLightEmission.GetPowerLevel(rig.Light.Style, rig.Sample, 0, 0));
        rig.Points[2].Team = PlayerTeam.Blue;
        rig.Tick(1);
        Assert.False(rig.State.IsPowered(0, rig.Light.Style));
        Assert.Equal(0, MapLightEmission.GetPowerLevel(rig.Light.Style, rig.Sample, 0, 0));
    }

    [Theory]
    [InlineData("instant", "0.5")]
    [InlineData("fade", "0")]
    public void InstantOrZeroDurationColourChangesStayFullyLit(string effect, string duration)
    {
        var rig = new Rig(new() { [MapLightMetadata.ColorTransitionKey] = effect,
            [MapLightMetadata.ColorTransitionTimeKey] = duration });
        rig.Points[0].Team = PlayerTeam.Red;
        rig.Tick(0);
        Assert.Equal(MapLightColorState.Swapped, rig.Sample.ColorState);
        Assert.Equal(1, rig.Sample.ColorProgress);
        Assert.Equal(1, MapLightEmission.GetPowerLevel(rig.Light.Style, rig.Sample, 0, 0));
    }

    private sealed class Rig
    {
        public MapLightMarker Light { get; }
        public MapLightMarker[] Lights { get; }
        public MapLightRuntimeState State { get; } = new();
        public ControlPointState[] Points { get; }
        private readonly MapLogicGraph _graph;
        public MapLightRuntimeSample Sample => State.GetSample(0, Light.Style);

        public Rig(Dictionary<string, string>? overrides = null)
        {
            var properties = new Dictionary<string, string>
            {
                ["color"] = "0000ff", ["altColor"] = "ff0000", ["altColor2"] = "00ff00",
                ["colorInput"] = "node:secondary", ["tertiaryColorInput"] = "node:tertiary",
                ["colorTransition"] = "fade", ["colorTransitionTime"] = "0.5",
            };
            if (overrides is not null)
                foreach (var pair in overrides) properties[pair.Key] = pair.Value;
            Light = MapLightMetadata.FromProperties(0, 0, properties);
            Lights = [Light];
            var keys = new[] { "secondary", "tertiary", "power" };
            _graph = MapLogicGraphBuilder.Build(keys.Select((key, index) => new MapLogicNodeDefinition
            {
                LogicKey = key, Kind = MapLogicNodeKind.CpTrigger, LinkedControlPointIndex = index + 1,
                OwnerRequirement = MapLogicCpTriggerOwnerRequirement.Red,
            }).ToArray());
            Points = Enumerable.Range(1, 3).Select(index => new ControlPointState(index,
                new RoomObjectMarker(RoomObjectType.ControlPoint, 0, 0, 32, 32,
                    "ControlPointRedS", PlayerTeam.Blue, $"controlPoint{index}")) { Team = PlayerTeam.Blue }).ToArray();
            State.Bind(Lights, _graph);
        }

        public void Tick(float delta)
        {
            _graph.Evaluate(Points);
            State.Tick(Lights, _graph, delta);
        }
    }
}
