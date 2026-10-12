using Microsoft.Xna.Framework;
using OpenGarrison.Client;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class GameplayMessageSoundScopeTests
{
    private static GameplayMessageMarker Message(string placement, string scope) =>
        GameplayMessageMetadata.FromProperties(500, 400, new Dictionary<string, string>
        {
            ["placement"] = placement, ["soundScope"] = scope, ["style"] = "ltd",
            ["sound"] = "IntelPutSnd", ["onEndSound"] = "CPCapturedSnd",
        });

    [Theory]
    [InlineData("screen")]
    [InlineData("local")]
    public void LocalSoundIsAudibleNearbyFadesWithDistanceAndIsSilentFarAway(string placement)
    {
        var marker = Message(placement, "local");
        var near = Game1.ResolveGameplayMessageSoundMix(marker, new Vector2(500, 400));
        var midway = Game1.ResolveGameplayMessageSoundMix(marker, new Vector2(1100, 400));
        var far = Game1.ResolveGameplayMessageSoundMix(marker, new Vector2(2500, 400));
        Assert.Equal(0.88f, near.Volume);
        Assert.Equal(0f, near.Pan);
        Assert.InRange(midway.Volume, 0.01f, near.Volume - 0.01f);
        Assert.True(midway.Pan < 0);
        Assert.Equal(0f, far.Volume);
        var otherSide = Game1.ResolveGameplayMessageSoundMix(marker, new Vector2(-100, 400));
        Assert.Equal(midway.Volume, otherSide.Volume);
        Assert.True(otherSide.Pan > 0);
        Assert.Equal(0f, Game1.ResolveGameplayMessageSoundMix(marker, new Vector2(500, 2400)).Volume);
    }

    [Theory]
    [InlineData("screen")]
    [InlineData("local")]
    public void GlobalSoundIgnoresListenerDistanceAndVisualPlacement(string placement)
    {
        var marker = Message(placement, "global");
        Assert.Equal((0.88f, 0f), Game1.ResolveGameplayMessageSoundMix(marker, new Vector2(500, 400)));
        Assert.Equal((0.88f, 0f), Game1.ResolveGameplayMessageSoundMix(marker, new Vector2(50000, -40000)));
    }

    [Theory]
    [InlineData("ltd", "left", "screen")]
    [InlineData("basic", "center", "screen")]
    [InlineData("basic", "left", "local")]
    public void OpeningAnOlderMessagePreservesPlacementAndMakesBothSettingsEditable(string style, string align, string placement)
    {
        var properties = new Dictionary<string, string> { ["style"] = style, ["align"] = align };
        var before = GameplayMessageMetadata.FromProperties(500, 400, properties);
        GameplayMessageMetadata.EnsurePresentationDefaults(properties);
        var after = GameplayMessageMetadata.FromProperties(500, 400, properties);
        Assert.Equal(before.Placement, after.Placement);
        Assert.Equal(placement, properties[GameplayMessageMetadata.PlacementPropertyKey]);
        Assert.Equal("global", properties[GameplayMessageMetadata.SoundScopePropertyKey]);
        Assert.Equal(GameplayMessageSoundScope.Global, after.SoundScope);
    }
}
