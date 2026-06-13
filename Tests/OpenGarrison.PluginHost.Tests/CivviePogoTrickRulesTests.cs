using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class CivviePogoTrickRulesTests
{
    [Fact]
    public void DeterministicFrameIndexIsStableForSameInputs()
    {
        var first = CivviePogoTrickRules.GetDeterministicFrameIndex(
            sessionSeed: 12345,
            playerId: 7,
            startFrame: 500,
            frameCount: 4);
        var second = CivviePogoTrickRules.GetDeterministicFrameIndex(
            sessionSeed: 12345,
            playerId: 7,
            startFrame: 500,
            frameCount: 4);

        Assert.Equal(first, second);
        Assert.InRange(first, 0, 3);
    }

    [Fact]
    public void DeterministicFrameIndexVariesAcrossSessionSeeds()
    {
        var frames = new HashSet<int>();
        for (var sessionSeed = 0; sessionSeed < 32; sessionSeed += 1)
        {
            frames.Add(CivviePogoTrickRules.GetDeterministicFrameIndex(
                sessionSeed,
                playerId: 1,
                startFrame: 100,
                frameCount: 4));
        }

        Assert.True(frames.Count > 1);
    }

    [Fact]
    public void ResolveTrickFrameIndexUsesTrickStartFrameFromRemainingTicks()
    {
        const int sessionSeed = 4242;
        const int playerId = 5;
        const int durationTicks = 30;
        const int ticksRemaining = 27;
        const ulong currentFrame = 1000;
        const int frameCount = 2;

        var expectedStartFrame = CivviePogoTrickRules.ResolveTrickStartFrame(
            currentFrame,
            durationTicks,
            ticksRemaining);
        var expectedFrame = CivviePogoTrickRules.GetDeterministicFrameIndex(
            sessionSeed,
            playerId,
            expectedStartFrame,
            frameCount);
        var resolvedFrame = CivviePogoTrickRules.ResolveTrickFrameIndex(
            sessionSeed,
            playerId,
            currentFrame,
            durationTicks,
            ticksRemaining,
            frameCount);

        Assert.Equal(expectedFrame, resolvedFrame);
    }

    [Fact]
    public void PlayerEntityFrameIndexMatchesRulesForLocalSimulation()
    {
        var player = new PlayerEntity(9, CharacterClassCatalog.Civilian, "Test");
        player.Spawn(PlayerTeam.Red, 128f, 128f);
        Assert.True(player.TryToggleCivviePogo());
        Assert.True(player.TryStartCivviePogoTrick(trickFrameCount: 4, durationTicks: 30));

        const int sessionSeed = 777;
        const ulong currentFrame = 250;
        const int frameCount = 4;
        var fromPlayer = player.GetCivviePogoTrickFrameIndex(sessionSeed, currentFrame, frameCount);
        var fromRules = CivviePogoTrickRules.ResolveTrickFrameIndex(
            sessionSeed,
            player.Id,
            currentFrame,
            durationTicks: 30,
            player.CivviePogoTrickTicksRemaining,
            frameCount);

        Assert.Equal(fromRules, fromPlayer);
    }
}
