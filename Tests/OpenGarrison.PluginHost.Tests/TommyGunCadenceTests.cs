using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class TommyGunCadenceTests
{
    [Fact]
    public void HeldTommyGunFiresThirteenShotsDuringThirtySourceTicks()
    {
        var player = new PlayerEntity(1, CharacterClassCatalog.Heavy, "Test");
        player.Spawn(PlayerTeam.Red, 0f, 0f);
        Assert.True(player.TrySelectGameplayPrimaryItem("weapon.tommy-gun"));

        var shots = 0;
        for (var tick = 0; tick < 30; tick += 1)
        {
            player.AdvanceTickState(default, 1d / 30d);
            if (player.TryFirePrimaryWeapon())
            {
                shots += 1;
            }
        }

        Assert.Equal(13, shots);

        // The next shot lands exactly at the next 30-tick boundary.
        player.AdvanceTickState(default, 1d / 30d);
        Assert.True(player.TryFirePrimaryWeapon());
    }
}
