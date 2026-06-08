using System.Collections.Generic;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class MapLogicNodeColorTests
{
    [Fact]
    public void TryResolveFillColor_ReturnsDefaultWhenPropertyMissing()
    {
        Assert.False(MapLogicNodeColorMetadata.TryResolveFillColor(null, out var red, out var green, out var blue));
        Assert.Equal(MapLogicNodeColorMetadata.DefaultRed, red);
        Assert.Equal(MapLogicNodeColorMetadata.DefaultGreen, green);
        Assert.Equal(MapLogicNodeColorMetadata.DefaultBlue, blue);
    }

    [Fact]
    public void TryParseColor_ParsesHexValues()
    {
        Assert.True(MapLogicNodeColorMetadata.TryParseColor("#FF5500", out var red, out var green, out var blue));
        Assert.Equal(255, red);
        Assert.Equal(85, green);
        Assert.Equal(0, blue);
        Assert.Equal("#FF5500", MapLogicNodeColorMetadata.FormatColor(red, green, blue));
    }

    [Fact]
    public void SupportsRecolor_IncludesScoreTriggerButNotDamageableZone()
    {
        Assert.True(MapLogicNodeColorMetadata.SupportsRecolor(MapLogicMetadata.ScoreTriggerEntityType));
        Assert.False(MapLogicNodeColorMetadata.SupportsRecolor(DamageableMetadata.DamageableEntityType));
        Assert.False(MapLogicNodeColorMetadata.SupportsRecolor(MapLogicMetadata.AreaEntityType));
    }

    [Fact]
    public void ClearColor_RemovesStoredValue()
    {
        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [MapLogicNodeColorMetadata.PropertyKey] = "#AA00AA",
        };

        MapLogicNodeColorMetadata.ClearColor(properties);

        Assert.False(properties.ContainsKey(MapLogicNodeColorMetadata.PropertyKey));
    }
}
