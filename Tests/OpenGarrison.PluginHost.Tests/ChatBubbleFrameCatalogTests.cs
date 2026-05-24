using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class ChatBubbleFrameCatalogTests
{
    [Theory]
    [InlineData(PlayerClass.Scout, 0)]
    [InlineData(PlayerClass.Pyro, 1)]
    [InlineData(PlayerClass.Soldier, 2)]
    [InlineData(PlayerClass.Demoman, 3)]
    [InlineData(PlayerClass.Heavy, 4)]
    [InlineData(PlayerClass.Medic, 5)]
    [InlineData(PlayerClass.Engineer, 6)]
    [InlineData(PlayerClass.Spy, 7)]
    [InlineData(PlayerClass.Sniper, 8)]
    public void ClassPortraitFramesMatchBubblesSpriteOrder(PlayerClass playerClass, int redFrame)
    {
        Assert.Equal(redFrame, ChatBubbleFrameCatalog.GetClassPortraitFrame(playerClass, PlayerTeam.Red));
        Assert.Equal(redFrame + 10, ChatBubbleFrameCatalog.GetClassPortraitFrame(playerClass, PlayerTeam.Blue));
    }

    [Theory]
    [InlineData(0, 1000)]
    [InlineData(1, 1001)]
    [InlineData(2, 1002)]
    public void CustomBubbleFramesRoundTripSlotIndex(int slotIndex, int expectedFrame)
    {
        var frame = ChatBubbleFrameCatalog.GetCustomBubbleFrame(slotIndex);

        Assert.Equal(expectedFrame, frame);
        Assert.True(ChatBubbleFrameCatalog.TryGetCustomBubbleSlot(frame, out var resolvedSlot));
        Assert.Equal(slotIndex, resolvedSlot);
    }

    [Theory]
    [InlineData(999)]
    [InlineData(1003)]
    public void CustomBubbleSlotRejectsNonCustomFrames(int frame)
    {
        Assert.False(ChatBubbleFrameCatalog.TryGetCustomBubbleSlot(frame, out var slotIndex));
        Assert.Equal(-1, slotIndex);
    }
}
