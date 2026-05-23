using OpenGarrison.Client;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class ClientBubbleMenuInputSuppressionTests
{
    [Fact]
    public void BubbleMenuSuppressionOnlySuppressesInteractAndSwap()
    {
        var input = new PlayerInputSnapshot(
            Left: true,
            Right: true,
            Up: true,
            Down: true,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: true,
            FireSecondary: true,
            AimWorldX: 128f,
            AimWorldY: 96f,
            DebugKill: false,
            DropIntel: false,
            UseAbility: true,
            InteractWeapon: true,
            SwapWeapon: true);

        var result = Game1.ApplyBubbleMenuGameplaySuppression(input);

        Assert.True(result.FirePrimary);
        Assert.True(result.FireSecondary);
        Assert.True(result.UseAbility);
        Assert.False(result.InteractWeapon);
        Assert.False(result.SwapWeapon);
    }
}
