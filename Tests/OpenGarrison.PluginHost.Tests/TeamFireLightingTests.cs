using Microsoft.Xna.Framework;
using OpenGarrison.Client;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class TeamFireLightingTests
{
    [Fact]
    public void OldMapsKeepOriginalFireAndPlayerColoursAndOffClearsNewKeys()
    {
        var metadata = new Dictionary<string, string> { ["lighting"] = "night", ["lightFireTeamColor"] = "on", ["lightPlayerTeamColor"] = "true" };
        var settings = MapLightingMetadata.Parse(metadata);
        Assert.True(settings.FireTeamColor);
        Assert.True(settings.PlayerTeamColor);
        MapLightingMetadata.Write(metadata, settings);
        Assert.Equal(settings, MapLightingMetadata.Parse(metadata));
        MapLightingMetadata.Write(metadata, MapLighting.None);
        Assert.DoesNotContain(MapLightingMetadata.FireTeamColorKey, metadata.Keys);
        Assert.DoesNotContain(MapLightingMetadata.PlayerTeamColorKey, metadata.Keys);
        var defaults = MapLightingMetadata.Parse(new Dictionary<string, string> { ["lighting"] = "night" });
        Assert.False(defaults.FireTeamColor);
        Assert.False(defaults.PlayerTeamColor);
    }

    [Theory]
    [InlineData(PlayerTeam.Red)]
    [InlineData(PlayerTeam.Blue)]
    public void FireSpriteRecolourKeepsItsBrightCoreShadingAndPremultipliedAlpha(PlayerTeam team)
    {
        var edge = TeamFireLighting.RecolorSpritePixel(team, new Color(255, 75, 0));
        var core = TeamFireLighting.RecolorSpritePixel(team, new Color(255, 242, 130));
        var translucent = TeamFireLighting.RecolorSpritePixel(team, new Color(128, 38, 0, 128));
        Assert.True(team == PlayerTeam.Blue ? edge.B > edge.R : edge.R > edge.B);
        Assert.True(core.G > edge.G);
        Assert.Equal(128, translucent.A);
        Assert.True(translucent.R <= translucent.A && translucent.G <= translucent.A && translucent.B <= translucent.A);
        Assert.Equal(Color.Transparent, TeamFireLighting.RecolorSpritePixel(team, Color.Transparent));
        Assert.Equal(team == PlayerTeam.Blue, TeamFireLighting.ResolveLightColor(true, team, Color.Orange).B >
            TeamFireLighting.ResolveLightColor(true, team, Color.Orange).R);
    }

    [Fact]
    public void DisabledAndUnattributedLightsAndFireKeepTheirOriginalColour()
    {
        Assert.Equal(Color.Orange, TeamFireLighting.ResolveLightColor(false, PlayerTeam.Blue, Color.Orange));
        Assert.Equal(Color.Orange, TeamFireLighting.ResolveLightColor(true, PlayerTeam.Neutral, Color.Orange));
        Assert.Equal(Color.Orange, TeamFireLighting.RecolorSpritePixel(PlayerTeam.Neutral, Color.Orange));
    }
}
