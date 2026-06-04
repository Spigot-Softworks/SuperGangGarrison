using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class ControlPointIndexMetadataTests
{
    [Theory]
    [InlineData("1", 2)]
    [InlineData("5", 1)]
    [InlineData("invalid", 2)]
    public void CyclePropertyValueWrapsBetweenOneAndFive(string current, int expected)
    {
        Assert.Equal(expected.ToString(System.Globalization.CultureInfo.InvariantCulture), ControlPointIndexMetadata.CyclePropertyValue(current));
    }

    [Fact]
    public void MarkerOrderUsesExplicitControlPointNumbers()
    {
        var markerThree = new RoomObjectMarker(
            RoomObjectType.ControlPoint,
            300f,
            0f,
            42f,
            42f,
            "ControlPointNeutralS",
            SourceName: "controlPoint3");
        var markerOne = new RoomObjectMarker(
            RoomObjectType.ControlPoint,
            100f,
            0f,
            42f,
            42f,
            "ControlPointNeutralS",
            SourceName: "controlPoint1");

        Assert.True(ControlPointMarkerIndex.CompareMarkersForGameplayOrder(markerOne, markerThree) < 0);
        Assert.True(ControlPointMarkerIndex.CompareMarkersForGameplayOrder(markerThree, markerOne) > 0);
    }
}
