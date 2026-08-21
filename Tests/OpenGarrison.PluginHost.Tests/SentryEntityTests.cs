using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class SentryEntityTests
{
    [Fact]
    public void SentryAimsImmediatelyWhenTargetChanges()
    {
        var sentry = new SentryEntity(
            id: 1,
            ownerPlayerId: 2,
            team: PlayerTeam.Red,
            x: 100f,
            y: 100f,
            startDirectionX: 1f);
        sentry.ForceBuilt();

        sentry.SetTarget(playerId: 3, targetX: 0f, targetY: 100f);

        Assert.False(sentry.IsRotating);
        Assert.Equal(0, sentry.RotationTicksRemaining);
        Assert.Equal(-1f, sentry.FacingDirectionX);
        Assert.Equal(180f, sentry.AimDirectionDegrees);
        Assert.True(sentry.CanFire());
    }
}
