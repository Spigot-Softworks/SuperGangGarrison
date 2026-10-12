using OpenGarrison.ClientShared;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class LightColorLogicTests
{
    private static readonly MapLightingColor Primary = new(255, 0, 0);
    private static readonly MapLightingColor Secondary = new(0, 0, 255);
    private static readonly MapLightingColor Tertiary = new(0, 255, 0);

    private sealed class Rig
    {
        public MapLightMarker Light { get; }
        public MapLightRuntimeState Server { get; } = new();
        private readonly MapLightMarker[] _lights;
        private readonly MapLogicGraph _graph;
        private readonly ControlPointState[] _points;

        public Rig(string action = "holdAlt", bool secondaryWired = true)
        {
            Light = MapLightMetadata.FromProperties(0, 0, new Dictionary<string, string>
            {
                [MapLightMetadata.ColorKey] = "ff0000",
                [MapLightMetadata.AltColorKey] = "0000ff",
                [MapLightMetadata.AltColor2Key] = "00ff00",
                [MapLightMetadata.ColorActionKey] = action,
                [MapLightMetadata.ColorInputKey] = secondaryWired ? "node:secondary" : "",
                [MapLightMetadata.TertiaryColorInputKey] = "node:tertiary",
            });
            _lights = [Light];
            _graph = MapLogicGraphBuilder.Build([
                new MapLogicNodeDefinition { LogicKey = "secondary", Kind = MapLogicNodeKind.CpTrigger,
                    LinkedControlPointIndex = 1, OwnerRequirement = MapLogicCpTriggerOwnerRequirement.Red },
                new MapLogicNodeDefinition { LogicKey = "tertiary", Kind = MapLogicNodeKind.CpTrigger,
                    LinkedControlPointIndex = 2, OwnerRequirement = MapLogicCpTriggerOwnerRequirement.Red },
            ]);
            _points = Enumerable.Range(1, 2).Select(index => new ControlPointState(index,
                new RoomObjectMarker(RoomObjectType.ControlPoint, 0, 0, 32, 32, "ControlPointRedS", PlayerTeam.Blue, $"controlPoint{index}"))).ToArray();
            Server.Bind(_lights, _graph);
        }

        public void Tick(bool secondary, bool tertiary)
        {
            _points[0].Team = secondary ? PlayerTeam.Red : PlayerTeam.Blue;
            _points[1].Team = tertiary ? PlayerTeam.Red : PlayerTeam.Blue;
            _graph.Evaluate(_points);
            Server.Tick(_lights, _graph, 1f / 60f);
        }

        public void AssertColour(MapLightingColor expected)
        {
            Assert.Equal(expected, Emit(Server.GetSample(0, Light.Style)));
            var client = new MapLightRuntimeState();
            client.Bind(_lights, MapLogicGraph.Empty);
            client.ApplyNetworkState(0, Server.GetNetworkState(0));
            client.Tick(_lights, MapLogicGraph.Empty, 1f / 60f);
            Assert.Equal(expected, Emit(client.GetSample(0, Light.Style)));
        }

        private MapLightingColor Emit(MapLightRuntimeSample sample)
        {
            var emitters = new List<MapLightEmitter>();
            MapLightEmission.Evaluate(Light, 0, 0, sample, default, emitters, null);
            return Assert.Single(emitters).Color;
        }

        public void Reset() => Server.Reset(_lights);
    }

    [Fact]
    public void SeparateInputsSelectSecondaryAndTertiaryAndRestoreOnRelease()
    {
        var rig = new Rig();
        rig.Tick(false, false); rig.AssertColour(Primary);
        rig.Tick(true, false); rig.AssertColour(Secondary);
        rig.Tick(true, true); rig.AssertColour(Tertiary);
        rig.Tick(false, true); rig.AssertColour(Tertiary);
        rig.Tick(false, false); rig.AssertColour(Primary);
        rig.Tick(true, true); rig.AssertColour(Tertiary);
        rig.Tick(true, false); rig.AssertColour(Secondary);
        rig.Reset(); rig.AssertColour(Primary);
    }

    [Fact]
    public void TertiaryInputAloneIsBoundAndReplicated()
    {
        var rig = new Rig(secondaryWired: false);
        Assert.True(rig.Light.Style.HasLogicInputs);
        Assert.True(rig.Server.HasLogic);
        Assert.Equal(0, Assert.Single(rig.Server.LogicLightIndices));
        Assert.Contains(MapLightMetadata.TertiaryColorInputKey, MapLightMetadata.LogicInputKeys);
        rig.Tick(false, true); rig.AssertColour(Tertiary);
        rig.Tick(false, false); rig.AssertColour(Primary);
    }

    [Theory]
    [InlineData("swap")]
    [InlineData("cycle")]
    public void TertiaryHoldRetainsFirstInputsLatchedStateAndProcessesItsPulses(string action)
    {
        var rig = new Rig(action);
        rig.Tick(false, true); rig.AssertColour(Tertiary);
        rig.Tick(true, true); rig.AssertColour(Tertiary);
        rig.Tick(false, true); rig.AssertColour(Tertiary);
        rig.Tick(false, false); rig.AssertColour(Secondary);
        rig.Tick(true, true); rig.AssertColour(Tertiary);
        rig.Tick(false, false); rig.AssertColour(action == "swap" ? Primary : Tertiary);
    }

    [Fact]
    public void BuiltInTertiaryTakesPriorityOverObsoleteExtraColour()
    {
        var light = MapLightMetadata.FromProperties(0, 0, new Dictionary<string, string>
        {
            [MapLightMetadata.AltColor2Key] = "00ff00",
            [MapLightMetadata.TriggerColorKey] = "ff00ff",
        });
        Assert.Equal(Tertiary, Assert.Single(MapLightEmission.ResolvePalette(light, MapLightColorState.Trigger, out var solid)));
        Assert.True(solid);
    }

    [Fact]
    public void MissingSecondaryDoesNotMakeItsInputSelectTheTertiaryColour()
    {
        var light = MapLightMetadata.FromProperties(0, 0, new Dictionary<string, string>
        {
            [MapLightMetadata.ColorKey] = "ff0000",
            [MapLightMetadata.AltColor2Key] = "00ff00",
        });
        Assert.Equal(Primary, MapLightEmission.ResolvePalette(light, MapLightColorState.Swapped, out _)[0]);
        Assert.Equal(Tertiary, Assert.Single(MapLightEmission.ResolvePalette(light, MapLightColorState.Trigger, out _)));
    }

    [Fact]
    public void BuilderMovesOldThirdColourAndHeldWireIntoExistingControls()
    {
        var properties = new Dictionary<string, string>
        {
            [MapLightMetadata.TriggerColorKey] = "00ff00",
            [MapLightMetadata.ColorInputKey] = "node:old",
            [MapLightMetadata.ColorActionKey] = "trigger",
        };
        BuilderLightColors.NormalizeForEditing(properties);
        Assert.False(properties.ContainsKey(MapLightMetadata.TriggerColorKey));
        Assert.Equal("00ff00", properties[MapLightMetadata.AltColor2Key]);
        Assert.Equal("node:old", properties[MapLightMetadata.TertiaryColorInputKey]);
        Assert.Equal(string.Empty, properties[MapLightMetadata.ColorInputKey]);
        Assert.Equal("holdAlt", properties[MapLightMetadata.ColorActionKey]);
        var once = properties.ToArray();
        BuilderLightColors.NormalizeForEditing(properties);
        Assert.Equal(once, properties.ToArray());
    }

    [Fact]
    public void BuilderRetainsAnExistingTertiarySelectionAndSecondWire()
    {
        var properties = new Dictionary<string, string>
        {
            [MapLightMetadata.TriggerColorKey] = "ff00ff",
            [MapLightMetadata.AltColor2Key] = "00ff00",
            [MapLightMetadata.ColorInputKey] = "node:first",
            [MapLightMetadata.TertiaryColorInputKey] = "node:second",
            [MapLightMetadata.ColorActionKey] = "swap",
        };
        BuilderLightColors.NormalizeForEditing(properties);
        Assert.Equal("00ff00", properties[MapLightMetadata.AltColor2Key]);
        Assert.Equal("node:first", properties[MapLightMetadata.ColorInputKey]);
        Assert.Equal("node:second", properties[MapLightMetadata.TertiaryColorInputKey]);
        Assert.Equal("swap", properties[MapLightMetadata.ColorActionKey]);
    }

    [Fact]
    public void NewLightsExposeTwoInputsAndUseTheBuiltInThreeColours()
    {
        Assert.True(CustomMapBuilderEntityCatalog.TryGetDefinition("light", out var definition));
        Assert.Contains(MapLightMetadata.TertiaryColorInputKey, definition.DefaultProperties.Keys);
        Assert.Contains(MapLightMetadata.ColorInputKey, definition.DefaultProperties.Keys);
        Assert.Contains(MapLightMetadata.AltColor2Key, definition.DefaultProperties.Keys);
        Assert.DoesNotContain(MapLightMetadata.TriggerColorKey, definition.DefaultProperties.Keys);
        Assert.Equal(MapLightColorAction.HoldAlt, MapLightStyleMetadata.Parse(definition.DefaultProperties).ColorAction);
    }
}
