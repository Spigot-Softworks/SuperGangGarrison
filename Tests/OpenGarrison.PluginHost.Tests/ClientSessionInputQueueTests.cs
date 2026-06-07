using System.Net;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class ClientSessionInputQueueTests
{
    [Fact]
    public void EdgeInputsArePreservedWithoutMovementBacklog()
    {
        var client = CreateClient();
        var neutralInput = CreateInput(useAbility: false);
        var abilityPressedInput = CreateInput(useAbility: true);
        var abilityReleasedInput = CreateInput(useAbility: false, right: true, aimWorldX: 64f, aimWorldY: 12f);

        Assert.True(client.TrySetLatestInput(1, neutralInput));
        Assert.True(client.TrySetLatestInput(2, abilityPressedInput));
        Assert.True(client.TrySetLatestInput(3, abilityReleasedInput));

        Assert.Equal(3u, client.LastReceivedInputSequence);
        Assert.Equal(0u, client.LastProcessedInputSequence);
        Assert.Equal(1, client.PendingInputCount);

        Assert.True(client.TryGetInputForNextTick(out var edgeTickInput));
        Assert.True(edgeTickInput.UseAbility);
        Assert.True(edgeTickInput.Right);
        Assert.Equal(64f, edgeTickInput.AimWorldX);
        Assert.Equal(12f, edgeTickInput.AimWorldY);
        Assert.Equal(2u, client.LastProcessedInputSequence);

        Assert.True(client.TryGetInputForNextTick(out var latestTickInput));
        Assert.False(latestTickInput.UseAbility);
        Assert.True(latestTickInput.Right);
        Assert.Equal(64f, latestTickInput.AimWorldX);
        Assert.Equal(12f, latestTickInput.AimWorldY);
        Assert.Equal(3u, client.LastProcessedInputSequence);

        Assert.True(client.TryGetInputForNextTick(out var heldInput));
        Assert.False(heldInput.UseAbility);
        Assert.True(heldInput.Right);
        Assert.Equal(3u, client.LastProcessedInputSequence);
    }

    [Fact]
    public void OrdinaryInputsCollapseToLatestState()
    {
        var client = CreateClient();

        Assert.True(client.TrySetLatestInput(1, CreateInput(left: true, aimWorldX: 8f)));
        Assert.True(client.TrySetLatestInput(2, CreateInput(right: true, aimWorldX: 24f)));
        Assert.True(client.TrySetLatestInput(3, CreateInput(right: true, aimWorldX: 48f)));

        Assert.Equal(3u, client.LastReceivedInputSequence);
        Assert.Equal(0, client.PendingInputCount);

        Assert.True(client.TryGetInputForNextTick(out var input));
        Assert.False(input.Left);
        Assert.True(input.Right);
        Assert.Equal(48f, input.AimWorldX);
        Assert.Equal(3u, client.LastProcessedInputSequence);
    }

    [Fact]
    public void EdgeInputsCanArriveOutOfOrderBeforeTheyAreProcessed()
    {
        var client = CreateClient();
        var neutralInput = CreateInput(useAbility: false);
        var abilityPressedInput = CreateInput(useAbility: true, right: true, aimWorldX: 48f);

        Assert.True(client.TrySetLatestInput(2, abilityPressedInput));
        Assert.True(client.TrySetLatestInput(1, neutralInput));

        Assert.Equal(2u, client.LastReceivedInputSequence);
        Assert.Equal(1, client.PendingInputCount);

        Assert.True(client.TryGetInputForNextTick(out var input));
        Assert.True(input.UseAbility);
        Assert.True(input.Right);
        Assert.Equal(48f, input.AimWorldX);
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

    [Fact]
    public void MultiplePendingEdgesAreCoalescedIntoOneTick()
    {
        var client = CreateClient();

        Assert.True(client.TrySetLatestInput(1, CreateInput()));
        Assert.True(client.TrySetLatestInput(2, CreateInput(useAbility: true)));
        Assert.True(client.TrySetLatestInput(3, CreateInput(firePrimary: true)));
        Assert.True(client.TrySetLatestInput(4, CreateInput(right: true, aimWorldX: 80f)));

        Assert.Equal(2, client.PendingInputCount);

        Assert.True(client.TryGetInputForNextTick(out var edgeInput));
        Assert.True(edgeInput.UseAbility);
        Assert.True(edgeInput.FirePrimary);
        Assert.True(edgeInput.Right);
        Assert.Equal(80f, edgeInput.AimWorldX);
        Assert.Equal(3u, client.LastProcessedInputSequence);
        Assert.Equal(0, client.PendingInputCount);

        Assert.True(client.TryGetInputForNextTick(out var latestInput));
        Assert.False(latestInput.UseAbility);
        Assert.False(latestInput.FirePrimary);
        Assert.True(latestInput.Right);
        Assert.Equal(4u, client.LastProcessedInputSequence);
    }

    private static ClientSession CreateClient()
    {
        return new ClientSession(1, 101, new IPEndPoint(IPAddress.Loopback, 8190), "Tester", TimeSpan.Zero);
    }

    private static PlayerInputSnapshot CreateInput(
        bool useAbility = false,
        bool firePrimary = false,
        bool left = false,
        bool right = false,
        float aimWorldX = 0f,
        float aimWorldY = 0f)
    {
        return new PlayerInputSnapshot(
            Left: left,
            Right: right,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: firePrimary,
            FireSecondary: false,
            AimWorldX: aimWorldX,
            AimWorldY: aimWorldY,
            DebugKill: false,
            UseAbility: useAbility);
    }
}
