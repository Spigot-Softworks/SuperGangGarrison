using OpenGarrison.Client;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class CivviePresentationTests
{
    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(2, true)]
    public void CivvieMoneyParticlesRespectNormalDisabledAndAlternativeModes(int particleMode, bool expected)
    {
        Assert.Equal(expected, Game1.AreCivvieMoneyParticlesEnabled(particleMode));
    }

    [Fact]
    public void PogoCrunchFrameCanUsePredictedTickState()
    {
        Assert.Equal(1, PlayerEntity.GetCivviePogoSpriteFrameIndex(crunchTicksRemaining: 2, frameCount: 2));
        Assert.Equal(0, PlayerEntity.GetCivviePogoSpriteFrameIndex(crunchTicksRemaining: 0, frameCount: 2));
    }
}
