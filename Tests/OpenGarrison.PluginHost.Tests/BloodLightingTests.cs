using Microsoft.Xna.Framework;
using OpenGarrison.Client;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class BloodLightingTests
{
    [Theory]
    [InlineData("original")]
    [InlineData("team")]
    [InlineData("2080ff")]
    public void BloodSettingsRoundTripAndKeepOtherLightingSettings(string color)
    {
        var metadata = new Dictionary<string, string> { ["lighting"] = "night", ["lightBloodColor"] = color,
            ["lightBloodGlow"] = "40", ["lightHealGlow"] = "23" };
        var lighting = MapLightingMetadata.Parse(metadata);
        Assert.Equal(40, lighting.BloodGlow);
        Assert.Equal(color == "team", lighting.BloodTeamColor);
        Assert.Equal(23, lighting.HealGlow);
        MapLightingMetadata.Write(metadata, lighting);
        Assert.Equal(color, metadata[MapLightingMetadata.BloodColorKey]);
        Assert.Equal(lighting, MapLightingMetadata.Parse(metadata));
        MapLightingMetadata.Write(metadata, MapLighting.None);
        Assert.DoesNotContain(MapLightingMetadata.BloodColorKey, metadata.Keys);
        Assert.DoesNotContain(MapLightingMetadata.BloodGlowKey, metadata.Keys);
    }

    [Fact]
    public void TeamColoursPreserveBloodShadingAndAlpha()
    {
        var settings = MapLightingMetadata.GetPresetDefaults(MapLightingPreset.Night) with { BloodTeamColor = true };
        var fill = new Color(218, 22, 28, 230);
        var outline = new Color(145, 8, 14, 230);
        var blue = BloodLighting.ResolveColor(settings, PlayerTeam.Blue, fill);
        var red = BloodLighting.ResolveColor(settings, PlayerTeam.Red, fill);
        Assert.True(blue.B > blue.R);
        Assert.True(red.R > red.B);
        Assert.Equal(fill.A, blue.A);
        Assert.True(BloodLighting.ResolveColor(settings, PlayerTeam.Blue, outline).B < blue.B);
        Assert.Equal(fill, BloodLighting.ResolveColor(settings with { BloodTeamColor = false }, PlayerTeam.Blue, fill));
        Assert.Equal(fill, BloodLighting.ResolveColor(MapLighting.None with { BloodTeamColor = true }, PlayerTeam.Blue, fill));
    }

    [Fact]
    public void PaletteColoursDoNotMultiplyAgainstRedSpritePixels()
    {
        var pixels = new[] { new Color(218, 22, 28, 255), new Color(70, 3, 4, 128), Color.Transparent };
        BloodLighting.MakeNeutralPixels(pixels);
        Assert.Equal(new Color(218, 218, 218, 255), pixels[0]);
        Assert.Equal(new Color(70, 70, 70, 128), pixels[1]);
        Assert.Equal(Color.Transparent, pixels[2]);
        var settings = MapLightingMetadata.GetPresetDefaults(MapLightingPreset.Night) with { BloodColor = new(0, 0, 255) };
        var tint = BloodLighting.ResolveColor(settings, PlayerTeam.Red, Color.White);
        Assert.Equal(Color.Blue, tint);
    }

    [Fact]
    public void PoolCacheInvalidatesWhenSourceTeamChangesWithoutAmountChanging()
    {
        var source = new Dictionary<(int X, int Y), Game1.SettledBloodCell>
            { [(0, 0)] = new() { Amount = 1, Team = PlayerTeam.Blue } };
        var cache = new SettledBloodPoolDrawCache();
        cache.BeginBuild();
        cache.CaptureSourceCell((0, 0), 1, false, PlayerTeam.Blue);
        cache.CompleteBuild();
        Assert.True(cache.TryUse(source));
        source[(0, 0)].Team = PlayerTeam.Red;
        Assert.False(cache.TryUse(source));
    }

    [Fact]
    public void MergingBloodDoesNotLoseItsSourceTeam()
    {
        var red = new BloodDropEntity(1, 10, 10, 0, 0, team: PlayerTeam.Red);
        var blue = new BloodDropEntity(2, 10, 10, 0, 0, team: PlayerTeam.Blue);
        red.ApplyNetworkState(10, 10, 0, 0, true, 100, 1);
        blue.ApplyNetworkState(10, 10, 0, 0, true, 100, 1);
        Assert.False(red.CanMergeWith(blue));
        var otherRed = new BloodDropEntity(3, 10, 10, 0, 0, team: PlayerTeam.Red);
        otherRed.ApplyNetworkState(10, 10, 0, 0, true, 100, 1);
        Assert.True(red.CanMergeWith(otherRed));
    }
}
