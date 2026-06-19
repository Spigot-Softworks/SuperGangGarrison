using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class PlayerEntitySniperStateTests
{
    [Fact]
    public void ForceEndSniperScopeForHumiliationClearsScopeAndCharge()
    {
        var player = new PlayerEntity(1, CharacterClassCatalog.Sniper);
        player.Spawn(PlayerTeam.Red, 100f, 100f);

        Assert.True(player.TryToggleSniperScope());
        for (var tick = 0; tick < 3; tick += 1)
        {
            player.AdvanceTickState(default, 1d / 30d);
        }

        Assert.True(player.IsSniperScoped);
        Assert.True(player.SniperChargeTicks > 0);

        player.ForceEndSniperScopeForHumiliation();

        Assert.False(player.IsSniperScoped);
        Assert.Equal(0, player.SniperChargeTicks);
    }
}
