using System.Reflection;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class KillAssistTests
{
    private static object? Invoke(SimulationWorld world, string name, params object?[] provided)
    {
        var method = typeof(SimulationWorld).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
        var arguments = method.GetParameters().Select(p => p.DefaultValue).ToArray();
        Array.Copy(provided, arguments, provided.Length);
        return method.Invoke(world, arguments);
    }

    private static PlayerEntity Join(SimulationWorld world, byte slot, PlayerTeam team)
    {
        Assert.True(world.TryPrepareNetworkPlayerJoin(slot));
        Assert.True(world.TrySetNetworkPlayerTeam(slot, team));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(slot, PlayerClass.Soldier));
        Assert.True(world.TryGetNetworkPlayer(slot, out var player));
        return player;
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(209, true)]
    [InlineData(210, false)]
    [InlineData(211, false)]
    public void OnlyMostRecentOtherDamageWithinSevenSecondsEarnsAssist(int elapsedTicks, bool earnsAssist)
    {
        var world = new SimulationWorld();
        world.CompleteLocalPlayerJoin(PlayerClass.Soldier);
        world.TrySetNetworkPlayerTeam(SimulationWorld.LocalPlayerSlot, PlayerTeam.Red);
        var victim = world.LocalPlayer;
        var killer = Join(world, 2, PlayerTeam.Blue);
        var earlier = Join(world, 3, PlayerTeam.Blue);
        var latest = Join(world, 4, PlayerTeam.Blue);
        Invoke(world, "ApplyPlayerDamage", victim, 5, earlier);
        Invoke(world, "ApplyPlayerDamage", victim, 5, latest);
        for (var tick = 0; tick < elapsedTicks; tick++) victim.AdvanceAssistTracking();
        // Repeated killer hits must not overwrite the most recent other contributor.
        Invoke(world, "ApplyPlayerDamage", victim, 5, killer);
        Invoke(world, "ApplyPlayerDamage", victim, 5, killer);
        Invoke(world, "KillPlayer", victim, false, killer, "RocketKL");
        Assert.Equal(0, earlier.Assists);
        Assert.Equal(earnsAssist ? 1 : 0, latest.Assists);
        Assert.Equal(earnsAssist ? 0.5f : 0f, latest.Points);
        var entry = Assert.Single(world.KillFeed);
        Assert.Equal(earnsAssist ? latest.Id : -1, entry.AssistPlayerId);
        Assert.Equal(earnsAssist ? latest.DisplayName : "", entry.AssistName);
        Assert.Equal(killer.Id, entry.KillerPlayerId);
        Assert.Equal(victim.Id, entry.VictimPlayerId);
    }

    [Fact]
    public void HealingDoesNotReplaceTheMostRecentDamageContributor()
    {
        var world = new SimulationWorld();
        world.CompleteLocalPlayerJoin(PlayerClass.Soldier);
        world.TrySetNetworkPlayerTeam(SimulationWorld.LocalPlayerSlot, PlayerTeam.Red);
        var victim = world.LocalPlayer;
        var killer = Join(world, 2, PlayerTeam.Blue);
        var contributor = Join(world, 3, PlayerTeam.Blue);
        var medic = Join(world, 4, PlayerTeam.Blue);
        Assert.True(world.TryForceNetworkPlayerClassSelectionAndRespawn(4, PlayerClass.Medic));
        medic.SetMedicHealingTarget(killer);
        Assert.Equal(medic.Id, (int)Invoke(world, "FindHealingMedicPlayerId", killer.Id)!);
        Invoke(world, "ApplyPlayerDamage", victim, 5, contributor);
        Invoke(world, "ApplyPlayerDamage", victim, 5, killer);
        Invoke(world, "KillPlayer", victim, false, killer, "RocketKL");
        Assert.Equal(0, medic.Assists);
        Assert.Equal(1, contributor.Assists);
        Assert.Equal(contributor.Id, Assert.Single(world.KillFeed).AssistPlayerId);
    }

    [Fact]
    public void ContributorStillGetsAssistAfterDying()
    {
        var world = new SimulationWorld();
        world.CompleteLocalPlayerJoin(PlayerClass.Soldier);
        world.TrySetNetworkPlayerTeam(SimulationWorld.LocalPlayerSlot, PlayerTeam.Red);
        var victim = world.LocalPlayer;
        var killer = Join(world, 2, PlayerTeam.Blue);
        var contributor = Join(world, 3, PlayerTeam.Blue);
        Invoke(world, "ApplyPlayerDamage", victim, 5, contributor);
        contributor.ApplyDamage(contributor.Health);
        Invoke(world, "ApplyPlayerDamage", victim, 5, killer);
        Invoke(world, "KillPlayer", victim, false, killer, "RocketKL");
        Assert.Equal(1, contributor.Assists);
        Assert.Equal(contributor.Id, Assert.Single(world.KillFeed).AssistPlayerId);
    }
}
