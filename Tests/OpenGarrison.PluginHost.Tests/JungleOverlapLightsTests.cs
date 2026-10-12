using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class JungleOverlapLightsTests
{
    [Fact]
    public void JunglesKeepLightsOffByDefault()
    {
        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["jungle"] = "true" };
        ForegroundSpriteMetadata.EnsurePlacementDefaults(properties, 1f);
        Assert.Equal("false", properties[ForegroundSpriteMetadata.OverlapLightsPropertyKey]);
        Assert.False(ForegroundSpriteMetadata.ParseConfiguration(properties).OverlapLights);

        properties[ForegroundSpriteMetadata.OverlapLightsPropertyKey] = "true";
        Assert.True(ForegroundSpriteMetadata.ParseConfiguration(properties).OverlapLights);
    }

    [Fact]
    public void OverlapLightsRowOnlyShowsForJungles()
    {
        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["jungle"] = "false" };
        Assert.False(ForegroundSpriteMetadata.ShouldShowJungleDependentProperty(properties, ForegroundSpriteMetadata.OverlapLightsPropertyKey));
        properties["jungle"] = "true";
        Assert.True(ForegroundSpriteMetadata.ShouldShowJungleDependentProperty(properties, ForegroundSpriteMetadata.OverlapLightsPropertyKey));
    }
}
