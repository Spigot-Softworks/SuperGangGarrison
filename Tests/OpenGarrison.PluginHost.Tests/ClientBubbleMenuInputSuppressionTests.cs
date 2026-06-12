using OpenGarrison.Client;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class ClientBubbleMenuInputSuppressionTests
{
    [Fact]
    public void BubbleMenuSuppressionBlocksFiringAndWeaponActions()
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

        Assert.False(result.FirePrimary);
        Assert.False(result.FireSecondary);
        Assert.False(result.UseAbility);
        Assert.False(result.InteractWeapon);
        Assert.False(result.SwapWeapon);
    }
}
