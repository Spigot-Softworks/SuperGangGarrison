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
}
