using OpenGarrison.Client;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class ClientBubbleMenuInputSuppressionTests
{
    [Fact]
    public void BubbleMenuSuppressionCanPreservePrimaryFireForHeldHealing()
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

        var preserved = Game1.ApplyBubbleMenuGameplaySuppression(input, preservePrimaryFire: true);
        var suppressed = Game1.ApplyBubbleMenuGameplaySuppression(input, preservePrimaryFire: false);

        Assert.True(preserved.FirePrimary);
        Assert.False(preserved.FireSecondary);
        Assert.False(preserved.UseAbility);
        Assert.False(preserved.InteractWeapon);
        Assert.False(preserved.SwapWeapon);
        Assert.False(suppressed.FirePrimary);
    }
}
