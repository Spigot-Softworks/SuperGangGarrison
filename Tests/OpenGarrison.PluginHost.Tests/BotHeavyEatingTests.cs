using OpenGarrison.Core;
using OpenGarrison.Core.BotBrain;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

[Collection(ContentRootTestGroup.Name)]
public sealed class BotHeavyEatingTests
{
    [Theory]
    [InlineData(75, 75, false)]
    [InlineData(75, 74, false)]
    [InlineData(75, 38, false)]
    [InlineData(75, 37, true)]
    [InlineData(200, 101, false)]
    [InlineData(200, 100, true)]
    [InlineData(300, 151, false)]
    [InlineData(300, 150, true)]
    public void IdleEatingUsesHalfOfEffectiveMaximumHealth(int maxHealth, int health, bool expected)
    {
        var world = CreateWorld(maxHealth);
        world.LocalPlayer.ForceSetHealth(health);
        var decision = CombatDecisionResolver.Resolve(world, world.LocalPlayer, null, null, new CombatDecisionMemory());
        Assert.Equal(expected, decision.FireSecondary);
    }

    [Theory]
    [InlineData(75, 12, false)]
    [InlineData(75, 11, true)]
    [InlineData(200, 31, false)]
    [InlineData(200, 30, true)]
    [InlineData(300, 46, false)]
    [InlineData(300, 45, true)]
    public void CombatEatingAndPrimaryFireAgreeOnTheCriticalHealthFraction(int maxHealth, int health, bool eats)
    {
        var world = CreateWorld(maxHealth);
        var heavy = world.LocalPlayer;
        heavy.ForceSetHealth(health);
        Assert.True(world.NetworkPlayers.TryPrepareNetworkPlayerJoin(2));
        Assert.True(world.NetworkPlayers.TrySetNetworkPlayerTeam(2, PlayerTeam.Blue));
        Assert.True(world.NetworkPlayers.TryCompleteNetworkPlayerJoinState(2, PlayerClass.Scout));
        Assert.True(world.NetworkPlayers.TryGetNetworkPlayer(2, out var enemy));
        enemy.TeleportTo(heavy.X + 240, heavy.Y);
        var target = new BotBrainCombatTarget(BotBrainCombatTargetKind.Player, enemy.Team, enemy.X, enemy.Y, Player: enemy);
        var decision = CombatDecisionResolver.Resolve(world, heavy, target, null, new CombatDecisionMemory());
        Assert.Equal(eats, decision.FireSecondary);
        Assert.Equal(!eats, decision.FirePrimary);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HardcoreHeavyFinishesAtMostOneMealAndDoesNotRestartAtFullHealth(bool initiallyInjured)
    {
        var world = CreateWorld(75);
        var heavy = world.LocalPlayer;
        if (initiallyInjured) heavy.ForceSetHealth(30);
        var memory = new CombatDecisionMemory();
        var previousInput = default(PlayerInputSnapshot);
        var mealStarts = 0;
        for (var tick = 0; tick < world.Config.TicksPerSecond * 12; tick++)
        {
            var decision = CombatDecisionResolver.Resolve(world, heavy, null, null, memory);
            var input = BotInputSynthesizer.Synthesize(heavy, default, heavy.X + 100, heavy.Y, decision, previousInput);
            var wasEating = heavy.IsHeavyEating;
            world.NetworkPlayers.SetLocalInput(input);
            world.AdvanceOneTick();
            if (!wasEating && heavy.IsHeavyEating) mealStarts++;
            previousInput = input;
        }
        Assert.Equal(initiallyInjured ? 1 : 0, mealStarts);
        Assert.True(heavy.IsAlive);
        Assert.Equal(75, heavy.Health);
        Assert.False(heavy.IsHeavyEating);
    }

    private static SimulationWorld CreateWorld(int maxHealth)
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        world.TestSetLevel(new SimpleLevel("bot_heavy_eating", GameModeKind.CaptureTheFlag, new WorldBounds(700, 400),
            1, null, 1, 1, new SpawnPoint(100, 250), [new SpawnPoint(100, 250)], [new SpawnPoint(600, 250)],
            [], [], 300, [new LevelSolid(0, 300, 700, 100)], false));
        world.ConfigureExperimentalGameplaySettings(new ExperimentalGameplaySettings(EnableHardcore: maxHealth == 75));
        world.NetworkPlayers.PrepareLocalPlayerJoin();
        world.NetworkPlayers.SetLocalPlayerTeam(PlayerTeam.Red);
        world.NetworkPlayers.CompleteLocalPlayerJoin(PlayerClass.Heavy);
        if (maxHealth != 75)
            Assert.True(world.ServerTuning.TrySetNetworkPlayerMaxHealthOverride(SimulationWorld.LocalPlayerSlot, maxHealth));
        Assert.Equal(maxHealth, world.LocalPlayer.MaxHealth);
        return world;
    }
}
