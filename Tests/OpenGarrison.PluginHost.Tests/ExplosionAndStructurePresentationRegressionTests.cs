using OpenGarrison.Client;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class ExplosionAndStructurePresentationRegressionTests
{
    [Theory]
    [InlineData(0UL, 720UL, 720UL)]
    [InlineData(719UL, 720UL, 719UL)]
    public void NetworkEventSourceFrameUsesSnapshotFrameOnlyWhenMissing(
        ulong sourceFrame,
        ulong snapshotFrame,
        ulong expected)
    {
        Assert.Equal(expected, Game1.ResolveNetworkEventSourceFrame(sourceFrame, snapshotFrame));
    }

    [Fact]
    public void DelayedExplosionVisualIsSuppressedAfterSoundFallback()
    {
        var tracker = new AuthoritativeExplosionPresentationTracker();

        Assert.True(tracker.ShouldPresent(
            sourceFrame: 120,
            x: 48f,
            y: 96f,
            channel: AuthoritativeExplosionPresentationChannel.Sound));
        Assert.False(tracker.ShouldPresent(
            sourceFrame: 120,
            x: 48f,
            y: 96f,
            channel: AuthoritativeExplosionPresentationChannel.Visual));
    }

    [Fact]
    public void DelayedExplosionSoundFallbackIsSuppressedAfterVisual()
    {
        var tracker = new AuthoritativeExplosionPresentationTracker();

        Assert.True(tracker.ShouldPresent(
            sourceFrame: 240,
            x: -32f,
            y: 18f,
            channel: AuthoritativeExplosionPresentationChannel.Visual));
        Assert.False(tracker.ShouldPresent(
            sourceFrame: 240,
            x: -32f,
            y: 18f,
            channel: AuthoritativeExplosionPresentationChannel.Sound));
    }

    [Fact]
    public void SameFrameExplosionsAtDifferentPositionsRemainDistinct()
    {
        var tracker = new AuthoritativeExplosionPresentationTracker();

        Assert.True(tracker.ShouldPresent(360, 10f, 20f, AuthoritativeExplosionPresentationChannel.Sound));
        Assert.True(tracker.ShouldPresent(360, 10.25f, 20f, AuthoritativeExplosionPresentationChannel.Sound));
        Assert.False(tracker.ShouldPresent(360, 10f, 20f, AuthoritativeExplosionPresentationChannel.Visual));
        Assert.False(tracker.ShouldPresent(360, 10.25f, 20f, AuthoritativeExplosionPresentationChannel.Visual));
    }

    [Fact]
    public void SameIdentityTracksMultipleExplosionPairs()
    {
        var tracker = new AuthoritativeExplosionPresentationTracker();

        Assert.True(tracker.ShouldPresent(480, 10f, 20f, AuthoritativeExplosionPresentationChannel.Sound));
        Assert.True(tracker.ShouldPresent(480, 10f, 20f, AuthoritativeExplosionPresentationChannel.Sound));
        Assert.False(tracker.ShouldPresent(480, 10f, 20f, AuthoritativeExplosionPresentationChannel.Visual));
        Assert.False(tracker.ShouldPresent(480, 10f, 20f, AuthoritativeExplosionPresentationChannel.Visual));
    }

    [Fact]
    public void EngineerBrowserWarmupIncludesEverySentryLayer()
    {
        Assert.Equal(
            new[] { "SentryRed", "SentryBlue", "SentryTurretS" },
            Game1.GetBrowserStructureWarmupSpriteNames(PlayerClass.Engineer));
        Assert.Empty(Game1.GetBrowserStructureWarmupSpriteNames(PlayerClass.Scout));
    }

    [Fact]
    public void BuildWheelSentryUsesCapturedGameplayAimAfterCursorMoves()
    {
        var wheelSelectionInput = default(PlayerInputSnapshot) with
        {
            BuildSentry = true,
            AimWorldX = 25f,
            AimWorldY = 300f,
        };

        var result = Game1.ApplyBuildWheelSentryAim(
            wheelSelectionInput,
            playerX: 150f,
            playerY: 80f,
            hasCapturedAimOffset: true,
            capturedAimOffsetX: -120f,
            capturedAimOffsetY: 15f);

        Assert.Equal(30f, result.AimWorldX);
        Assert.Equal(95f, result.AimWorldY);
        Assert.True(result.BuildSentry);
    }

    [Fact]
    public void NonWheelSentryCommandKeepsCurrentAim()
    {
        var directInput = default(PlayerInputSnapshot) with
        {
            FireSecondary = true,
            AimWorldX = 25f,
            AimWorldY = 300f,
        };

        var result = Game1.ApplyBuildWheelSentryAim(
            directInput,
            playerX: 150f,
            playerY: 80f,
            hasCapturedAimOffset: true,
            capturedAimOffsetX: -120f,
            capturedAimOffsetY: 15f);

        Assert.Equal(directInput, result);
    }
}
