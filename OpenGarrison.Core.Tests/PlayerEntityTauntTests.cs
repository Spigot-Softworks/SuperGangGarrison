using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.Core.Tests;

public sealed class PlayerEntityTauntTests
{
    [Theory]
    [InlineData(PlayerTeam.Red)]
    [InlineData(PlayerTeam.Blue)]
    public void Taunt_UsesSameFrameRangeForEachTeam(PlayerTeam team)
    {
        var player = new PlayerEntity(71, CharacterClassCatalog.Scout, "Taunter");
        player.Spawn(team, 128f, 192f);

        Assert.True(player.TryStartTaunt());
        Assert.True(player.IsTaunting);
        Assert.Equal(0f, player.TauntFrameIndex);

        var ticks = 0;
        while (player.IsTaunting && ticks < 256)
        {
            player.Advance(
                default,
                jumpPressed: false,
                SimpleLevelFactory.CreateScoutPrototypeLevel(),
                player.Team,
                1d / LegacyMovementModel.SourceTicksPerSecond);
            ticks += 1;
        }

        Assert.False(player.IsTaunting);
        Assert.InRange(ticks, 1, 255);
    }
}
