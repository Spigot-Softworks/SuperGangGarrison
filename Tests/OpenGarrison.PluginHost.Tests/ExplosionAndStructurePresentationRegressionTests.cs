using OpenGarrison.Client;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class ExplosionAndStructurePresentationRegressionTests
{

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
