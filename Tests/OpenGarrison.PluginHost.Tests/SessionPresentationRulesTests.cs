using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class SessionPresentationRulesTests
{
    [Fact]
    public void DerivePresentationSeedIsStableForSameSessionInputs()
    {
        var first = SessionPresentationRules.DerivePresentationSeed("ctf_conflict", "sha256:abc", tickRate: 30);
        var second = SessionPresentationRules.DerivePresentationSeed("ctf_conflict", "sha256:abc", tickRate: 30);

        Assert.Equal(first, second);
    }

    [Fact]
    public void DerivePresentationSeedVariesByMapHashAndTickRate()
    {
        var baseline = SessionPresentationRules.DerivePresentationSeed("ctf_conflict", string.Empty, tickRate: 30);
        var otherHash = SessionPresentationRules.DerivePresentationSeed("ctf_conflict", "sha256:abc", tickRate: 30);
        var otherTickRate = SessionPresentationRules.DerivePresentationSeed("ctf_conflict", string.Empty, tickRate: 60);

        Assert.NotEqual(baseline, otherHash);
        Assert.NotEqual(baseline, otherTickRate);
    }

    [Fact]
    public void ConfigureSessionPresentationSeedMatchesWelcomeInputs()
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false, TicksPerSecond = 30 });
        world.ConfigureSessionPresentationSeed("ctf_conflict", "sha256:deadbeef");

        var expected = SessionPresentationRules.DerivePresentationSeed(
            "ctf_conflict",
            "sha256:deadbeef",
            tickRate: 30);
        Assert.Equal(expected, world.SessionPresentationSeed);
    }
}
