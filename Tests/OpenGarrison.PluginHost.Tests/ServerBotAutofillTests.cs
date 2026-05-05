using OpenGarrison.BotAI;
using OpenGarrison.Core;
using OpenGarrison.Server;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class ServerBotAutofillTests
{
    [Theory]
    [InlineData(0, 0, 12, 6, 6, 6)]
    [InlineData(6, 6, 12, 6, 0, 0)]
    [InlineData(6, 5, 12, 6, 0, 1)]
    [InlineData(5, 4, 12, 6, 1, 2)]
    [InlineData(9, 0, 12, 6, 0, 3)]
    [InlineData(0, 0, 20, 6, 6, 6)]
    public void ComputeBotAutofillTargetsBalancesTeamsWithoutOvershoot(
        int humanRedCount,
        int humanBlueCount,
        int minimumTotalPlayers,
        int maxBotsPerTeam,
        int expectedRedBots,
        int expectedBlueBots)
    {
        var targets = GameServer.ComputeBotAutofillTargets(
            humanRedCount,
            humanBlueCount,
            minimumTotalPlayers,
            maxBotsPerTeam);

        Assert.Equal(expectedRedBots, targets.RedBots);
        Assert.Equal(expectedBlueBots, targets.BlueBots);
    }

    [Fact]
    public void TrimTeamRemovesExcessBotsFromRequestedTeam()
    {
        var world = new SimulationWorld();
        var botManager = new ServerBotManager(
            world,
            new SimulationConfig(),
            new ModernPracticeBotController());

        Assert.True(botManager.TryAddBot(2, PlayerTeam.Red, PlayerClass.Scout, "Red 1"));
        Assert.True(botManager.TryAddBot(3, PlayerTeam.Red, PlayerClass.Soldier, "Red 2"));
        Assert.True(botManager.TryAddBot(4, PlayerTeam.Blue, PlayerClass.Medic, "Blue 1"));

        var removed = botManager.TrimTeam(PlayerTeam.Red, 1);

        Assert.Equal(1, removed);
        Assert.Single(botManager.BotSlots.Values, state => state.Team == PlayerTeam.Red);
        Assert.Single(botManager.BotSlots.Values, state => state.Team == PlayerTeam.Blue);
    }

    [Fact]
    public void ReactivateBotsAfterMapChangeRestoresAwaitingJoinBotsToActivePlay()
    {
        var world = new SimulationWorld();
        var botManager = new ServerBotManager(
            world,
            new SimulationConfig(),
            new ModernPracticeBotController());

        Assert.True(botManager.TryAddBot(2, PlayerTeam.Red, PlayerClass.Soldier, "Red 1"));
        Assert.True(botManager.TryAddBot(3, PlayerTeam.Blue, PlayerClass.Medic, "Blue 1"));
        Assert.False(world.IsNetworkPlayerAwaitingJoin(2));
        Assert.False(world.IsNetworkPlayerAwaitingJoin(3));

        world.ResetPlayersToAwaitingJoinForFreshMap();

        Assert.True(world.IsNetworkPlayerAwaitingJoin(2));
        Assert.True(world.IsNetworkPlayerAwaitingJoin(3));

        var restored = botManager.ReactivateBotsAfterMapChange();

        Assert.Equal(2, restored);
        Assert.False(world.IsNetworkPlayerAwaitingJoin(2));
        Assert.False(world.IsNetworkPlayerAwaitingJoin(3));
        Assert.True(world.TryGetNetworkPlayer(2, out var redBot));
        Assert.True(world.TryGetNetworkPlayer(3, out var blueBot));
        Assert.True(redBot!.IsAlive);
        Assert.True(blueBot!.IsAlive);
        Assert.Equal(PlayerTeam.Red, redBot.Team);
        Assert.Equal(PlayerClass.Soldier, redBot.ClassId);
        Assert.Equal(PlayerTeam.Blue, blueBot.Team);
        Assert.Equal(PlayerClass.Medic, blueBot.ClassId);
    }
}
