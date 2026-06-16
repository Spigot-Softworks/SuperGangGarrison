using OpenGarrison.Core;
using OpenGarrison.Protocol;
using Xunit;
using static ServerHelpers;

namespace OpenGarrison.PluginHost.Tests;

public sealed class RelativeAimInputTests
{
    [Fact]
    public void ToCoreInputPreservesRelativeAimOffsets()
    {
        var message = new InputStateMessage(
            Sequence: 7,
            Buttons: InputButtons.FirePrimary,
            AimRelX: 120f,
            AimRelY: -48f,
            ChatBubbleFrameIndex: -1);

        var input = ToCoreInput(message);

        Assert.Equal(120f, input.AimWorldX);
        Assert.Equal(-48f, input.AimWorldY);
    }

    [Fact]
    public void ConvertRelativeAimToWorldUsesCurrentPlayerPosition()
    {
        var relativeInput = new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: false,
            AimWorldX: 120f,
            AimWorldY: -48f,
            DebugKill: false);

        var worldInput = ConvertRelativeAimToWorld(relativeInput, playerX: 512f, playerY: 320f);

        Assert.Equal(632f, worldInput.AimWorldX);
        Assert.Equal(272f, worldInput.AimWorldY);
    }
}
