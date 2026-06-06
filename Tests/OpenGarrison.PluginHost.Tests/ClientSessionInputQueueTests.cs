using System.Net;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class ClientSessionInputQueueTests
{
    [Fact]
    public void QueuedInputsAreConsumedInSequenceInsteadOfOverwritingEdges()
    {
        var client = CreateClient();
        var neutralInput = CreateInput(useAbility: false);
        var abilityPressedInput = CreateInput(useAbility: true);
        var abilityReleasedInput = CreateInput(useAbility: false);

        Assert.True(client.TrySetLatestInput(1, neutralInput));
        Assert.True(client.TrySetLatestInput(2, abilityPressedInput));
        Assert.True(client.TrySetLatestInput(3, abilityReleasedInput));

        Assert.Equal(3u, client.LastReceivedInputSequence);
        Assert.Equal(0u, client.LastProcessedInputSequence);
        Assert.Equal(3, client.PendingInputCount);

        Assert.True(client.TryGetInputForNextTick(out var firstTickInput));
        Assert.False(firstTickInput.UseAbility);
        Assert.Equal(1u, client.LastProcessedInputSequence);

        Assert.True(client.TryGetInputForNextTick(out var secondTickInput));
        Assert.True(secondTickInput.UseAbility);
        Assert.Equal(2u, client.LastProcessedInputSequence);

        Assert.True(client.TryGetInputForNextTick(out var thirdTickInput));
        Assert.False(thirdTickInput.UseAbility);
        Assert.Equal(3u, client.LastProcessedInputSequence);

        Assert.True(client.TryGetInputForNextTick(out var heldInput));
        Assert.False(heldInput.UseAbility);
        Assert.Equal(3u, client.LastProcessedInputSequence);
    }

    [Fact]
    public void QueuedInputsCanArriveOutOfOrderBeforeTheyAreProcessed()
    {
        var client = CreateClient();
        var neutralInput = CreateInput(useAbility: false);
        var abilityPressedInput = CreateInput(useAbility: true);

        Assert.True(client.TrySetLatestInput(2, abilityPressedInput));
        Assert.True(client.TrySetLatestInput(1, neutralInput));

        Assert.Equal(2u, client.LastReceivedInputSequence);
        Assert.Equal(2, client.PendingInputCount);

        Assert.True(client.TryGetInputForNextTick(out var firstTickInput));
        Assert.False(firstTickInput.UseAbility);
        Assert.Equal(1u, client.LastProcessedInputSequence);

        Assert.True(client.TryGetInputForNextTick(out var secondTickInput));
        Assert.True(secondTickInput.UseAbility);
        Assert.Equal(2u, client.LastProcessedInputSequence);
    }

    [Fact]
    public void ProcessedInputsAreRejectedIfTheyArriveAgain()
    {
        var client = CreateClient();
        var neutralInput = CreateInput(useAbility: false);

        Assert.True(client.TrySetLatestInput(1, neutralInput));
        Assert.True(client.TryGetInputForNextTick(out _));

        Assert.False(client.TrySetLatestInput(1, CreateInput(useAbility: true)));
        Assert.Equal(1u, client.LastProcessedInputSequence);
        Assert.Equal(0, client.PendingInputCount);
    }

    private static ClientSession CreateClient()
    {
        return new ClientSession(1, 101, new IPEndPoint(IPAddress.Loopback, 8190), "Tester", TimeSpan.Zero);
    }

    private static PlayerInputSnapshot CreateInput(bool useAbility)
    {
        return new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: false,
            AimWorldX: 0f,
            AimWorldY: 0f,
            DebugKill: false,
            UseAbility: useAbility);
    }
}
