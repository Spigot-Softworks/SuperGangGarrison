using OpenGarrison.Client;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class ClientBuildFeedbackTests
{
    [Fact]
    public void DispenserUsesFixedCostWhileAutogunUsesMaxMetalThreshold()
    {
        Assert.True(Game1.IsEngineerBuildResourceInsufficient(99f, 100f));
        Assert.False(Game1.IsEngineerBuildResourceInsufficient(100f, 100f));
        Assert.False(Game1.IsEngineerBuildResourceInsufficient(120f, 100f));
        Assert.True(Game1.IsEngineerBuildResourceInsufficient(120f, 150f));
    }
}
