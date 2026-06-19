using System.Reflection;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class SimulationWorldRoundEndScorekeepingTests
{
    [Fact]
    public void KillBeforeRoundEndAwardsKillerPoints()
    {
        var world = CreateCombatWorld(out var killer);

        InvokeKillPlayer(world, world.LocalPlayer, killer, "RocketKL");

        Assert.Equal(1, killer.Kills);
        Assert.Equal(1f, killer.Points);
    }

    [Fact]
    public void KillAfterRoundEndDoesNotAwardKillerPoints()
    {
        var world = CreateCombatWorld(out var killer);
        SetMatchEnded(world, PlayerTeam.Red);

        InvokeKillPlayer(world, world.LocalPlayer, killer, "RocketKL");

        Assert.Equal(1, killer.Kills);
        Assert.Equal(0f, killer.Points);
    }

    [Fact]
    public void HealingAwardsOnePointPerEightHundredHealing()
    {
        var world = new SimulationWorld();
        var healer = new PlayerEntity(1, CharacterClassCatalog.Medic, "Medic");

        InvokeAwardHealingPoints(world, healer, 799);

        Assert.Equal(799, healer.HealPoints);
        Assert.Equal(0f, healer.Points);

        InvokeAwardHealingPoints(world, healer, 1);

        Assert.Equal(800, healer.HealPoints);
        Assert.Equal(1f, healer.Points);

        InvokeAwardHealingPoints(world, healer, 799);

        Assert.Equal(1599, healer.HealPoints);
        Assert.Equal(1f, healer.Points);

        InvokeAwardHealingPoints(world, healer, 1);

        Assert.Equal(1600, healer.HealPoints);
        Assert.Equal(2f, healer.Points);
    }

    [Fact]
    public void HealingAfterRoundEndTracksHealingWithoutAwardingPoints()
    {
        var world = new SimulationWorld();
        var healer = new PlayerEntity(1, CharacterClassCatalog.Medic, "Medic");
        SetMatchEnded(world, PlayerTeam.Red);

        InvokeAwardHealingPoints(world, healer, 800);

        Assert.Equal(800, healer.HealPoints);
        Assert.Equal(0f, healer.Points);
    }

    private static SimulationWorld CreateCombatWorld(out PlayerEntity killer)
    {
        var world = new SimulationWorld();
        world.CompleteLocalPlayerJoin(PlayerClass.Scout);
        Assert.True(world.TrySetNetworkPlayerTeam(SimulationWorld.LocalPlayerSlot, PlayerTeam.Red));
        Assert.True(world.TryPrepareNetworkPlayerJoin(2));
        Assert.True(world.TrySetNetworkPlayerTeam(2, PlayerTeam.Blue));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(2, PlayerClass.Soldier));
        Assert.True(world.TryGetNetworkPlayer(2, out killer!));
        return world;
    }

    private static void SetMatchEnded(SimulationWorld world, PlayerTeam winner)
    {
        var property = typeof(SimulationWorld).GetProperty(nameof(SimulationWorld.MatchState))
            ?? throw new InvalidOperationException("MatchState property was not found.");
        var setter = property.GetSetMethod(nonPublic: true)
            ?? throw new InvalidOperationException("MatchState setter was not found.");
        setter.Invoke(world, [world.MatchState with { Phase = MatchPhase.Ended, WinnerTeam = winner }]);
    }

    private static void InvokeKillPlayer(
        SimulationWorld world,
        PlayerEntity victim,
        PlayerEntity killer,
        string weaponSpriteName)
    {
        var method = typeof(SimulationWorld).GetMethod("KillPlayer", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("KillPlayer method was not found.");
        _ = method.Invoke(
            world,
            [
                victim,
                false,
                killer,
                weaponSpriteName,
                DeadBodyAnimationKind.Default,
                null,
                null,
                null,
                true,
                true,
                false,
                true,
            ]);
    }

    private static void InvokeAwardHealingPoints(SimulationWorld world, PlayerEntity healer, int healedAmount)
    {
        var method = typeof(SimulationWorld).GetMethod("AwardHealingPoints", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("AwardHealingPoints method was not found.");
        method.Invoke(world, [healer, healedAmount]);
    }
}
