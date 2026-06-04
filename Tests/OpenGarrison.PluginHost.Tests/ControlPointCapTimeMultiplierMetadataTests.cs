using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class ControlPointCapTimeMultiplierMetadataTests
{
    [Fact]
    public void ResolveAutoMultiplierUsesSlowestControlPointAsReference()
    {
        var fastest = ControlPointCapTimeMultiplierMetadata.ResolveAutoMultiplier(3, 3, setupMode: false);
        var slowest = ControlPointCapTimeMultiplierMetadata.ResolveAutoMultiplier(3, 2, setupMode: false);

        Assert.Equal(1f, fastest, precision: 3);
        Assert.True(slowest > fastest);
    }

    [Fact]
    public void CustomMultiplierScalesReferenceCapTime()
    {
        var reference = ControlPointCapTimeRules.GetReferenceCapTime(3, setupMode: false);
        var ticks = ControlPointCapTimeMultiplierMetadata.ResolveCapTimeTicks(
            3,
            1,
            setupMode: false,
            storedMultiplier: 2f,
            isCustom: true);

        Assert.Equal(Math.Max(1, (int)MathF.Round(reference * 2f)), ticks);
    }
}
