using OpenGarrison.Client;
using OpenGarrison.ClientShared;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class ClassicVisualsSettingsTests
{
    [Fact]
    public void ClassicVisualsForcesTheOldGoreButKeepsThePlayersChoices()
    {
        var settings = new GameplayRuntimeSettings
        {
            BloodRenderMode = 0,
            DynamicRagdollEnabled = true,
            BurnCharredCorpsesEnabled = true,
            CorpseFadeMode = 1,
            CorpseDurationMode = ClientSettings.CorpseDurationInfinite,
        };

        settings.ClassicVisualsEnabled = true;
        Assert.Equal(GameplayRuntimeSettings.ClassicBloodRenderMode, settings.BloodRenderMode);
        Assert.False(settings.DynamicRagdollEnabled);
        Assert.False(settings.BurnCharredCorpsesEnabled);
        Assert.Equal(GameplayRuntimeSettings.ClassicCorpseFadeMode, settings.CorpseFadeMode);
        Assert.Equal(ClientSettings.CorpseDurationDefault, settings.CorpseDurationMode);

        // What gets saved is still the player's own choice...
        Assert.Equal(0, settings.StoredBloodRenderMode);
        Assert.True(settings.StoredDynamicRagdollEnabled);
        Assert.Equal(ClientSettings.CorpseDurationInfinite, settings.StoredCorpseDurationMode);

        // ...and it comes back when Classic visuals is turned off.
        settings.ClassicVisualsEnabled = false;
        Assert.Equal(0, settings.BloodRenderMode);
        Assert.True(settings.DynamicRagdollEnabled);
        Assert.True(settings.BurnCharredCorpsesEnabled);
        Assert.Equal(1, settings.CorpseFadeMode);
        Assert.Equal(ClientSettings.CorpseDurationInfinite, settings.CorpseDurationMode);
    }
}
