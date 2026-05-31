using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class PlayerEntityTauntTests
{
    [Fact]
    public void TauntCannotImmediatelyRestartAfterCompleting()
    {
        var player = new PlayerEntity(1, CharacterClassCatalog.Scout);
        player.Spawn(PlayerTeam.Red, 100f, 100f);

        Assert.True(player.TryStartTaunt());
        player.ObserveTauntInput(false);

        AdvanceUntilTauntEnds(player);

        Assert.False(player.IsTaunting);
        Assert.True(player.TauntRestartCooldownTicksRemaining > 0);
        Assert.False(player.TryStartTaunt());

        for (var tick = 0; tick < 32; tick += 1)
        {
            player.AdvanceTickState(default, 1d / 30d);
        }

        Assert.True(player.TryStartTaunt());
    }

    private static void AdvanceUntilTauntEnds(PlayerEntity player)
    {
        for (var tick = 0; tick < 120 && player.IsTaunting; tick += 1)
        {
            player.AdvanceTickState(default, 1d / 30d);
        }
    }
}
