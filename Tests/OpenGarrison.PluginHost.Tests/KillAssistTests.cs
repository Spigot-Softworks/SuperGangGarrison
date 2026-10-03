using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class KillAssistTests
{
    private static PlayerEntity Join(SimulationWorld world, byte slot, PlayerTeam team)
    {
        Assert.True(world.NetworkPlayers.TryPrepareNetworkPlayerJoin(slot));
        Assert.True(world.NetworkPlayers.TrySetNetworkPlayerTeam(slot, team));
        Assert.True(world.NetworkPlayers.TryApplyNetworkPlayerClassSelection(slot, PlayerClass.Soldier));
        Assert.True(world.NetworkPlayers.TryGetNetworkPlayer(slot, out var player));
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
        world.NetworkPlayers.CompleteLocalPlayerJoin(PlayerClass.Soldier);
        world.NetworkPlayers.TrySetNetworkPlayerTeam(SimulationWorld.LocalPlayerSlot, PlayerTeam.Red);
        var victim = world.LocalPlayer;
        var killer = Join(world, 2, PlayerTeam.Blue);
        var earlier = Join(world, 3, PlayerTeam.Blue);
        var latest = Join(world, 4, PlayerTeam.Blue);
        world.Combat.ApplyPlayerDamage(victim, 5, earlier);
        world.Combat.ApplyPlayerDamage(victim, 5, latest);
        for (var tick = 0; tick < elapsedTicks; tick++) victim.AdvanceAssistTracking();
        // Repeated killer hits must not overwrite the most recent other contributor.
        world.Combat.ApplyPlayerDamage(victim, 5, killer);
        world.Combat.ApplyPlayerDamage(victim, 5, killer);
        world.PlayerDeaths.KillPlayer(victim, false, killer, "RocketKL");
        Assert.Equal(0, earlier.Assists);
        Assert.Equal(earnsAssist ? 1 : 0, latest.Assists);
        Assert.Equal(earnsAssist ? 0.5f : 0f, latest.Points);
        var entry = Assert.Single(world.KillFeedEntries);
        Assert.Equal(earnsAssist ? latest.Id : -1, entry.AssistPlayerId);
        Assert.Equal(earnsAssist ? latest.DisplayName : "", entry.AssistName);
        Assert.Equal(killer.Id, entry.KillerPlayerId);
        Assert.Equal(victim.Id, entry.VictimPlayerId);
    }

    [Fact]
    public void HealingDoesNotReplaceTheMostRecentDamageContributor()
    {
        var world = new SimulationWorld();
        world.NetworkPlayers.CompleteLocalPlayerJoin(PlayerClass.Soldier);
        world.NetworkPlayers.TrySetNetworkPlayerTeam(SimulationWorld.LocalPlayerSlot, PlayerTeam.Red);
        var victim = world.LocalPlayer;
        var killer = Join(world, 2, PlayerTeam.Blue);
        var contributor = Join(world, 3, PlayerTeam.Blue);
        var medic = Join(world, 4, PlayerTeam.Blue);
        Assert.True(world.NetworkPlayers.TryForceNetworkPlayerClassSelectionAndRespawn(4, PlayerClass.Medic));
        medic.SetMedicHealingTarget(killer);
        Assert.Equal(killer.Id, medic.MedicHealTargetId);
        world.Combat.ApplyPlayerDamage(victim, 5, contributor);
        world.Combat.ApplyPlayerDamage(victim, 5, killer);
        world.PlayerDeaths.KillPlayer(victim, false, killer, "RocketKL");
        Assert.Equal(0, medic.Assists);
        Assert.Equal(1, contributor.Assists);
        Assert.Equal(contributor.Id, Assert.Single(world.KillFeedEntries).AssistPlayerId);
    }

    [Fact]
    public void ContributorStillGetsAssistAfterDying()
    {
        var world = new SimulationWorld();
        world.NetworkPlayers.CompleteLocalPlayerJoin(PlayerClass.Soldier);
        world.NetworkPlayers.TrySetNetworkPlayerTeam(SimulationWorld.LocalPlayerSlot, PlayerTeam.Red);
        var victim = world.LocalPlayer;
        var killer = Join(world, 2, PlayerTeam.Blue);
        var contributor = Join(world, 3, PlayerTeam.Blue);
        world.Combat.ApplyPlayerDamage(victim, 5, contributor);
        contributor.ApplyDamage(contributor.Health);
        world.Combat.ApplyPlayerDamage(victim, 5, killer);
        world.PlayerDeaths.KillPlayer(victim, false, killer, "RocketKL");
        Assert.Equal(1, contributor.Assists);
        Assert.Equal(contributor.Id, Assert.Single(world.KillFeedEntries).AssistPlayerId);
    }

    [Fact]
    public void AssisterCanEarnRevengeFromTheKillTheyHelpedComplete()
    {
        var world = new SimulationWorld();
        world.NetworkPlayers.CompleteLocalPlayerJoin(PlayerClass.Soldier);
        world.NetworkPlayers.TrySetNetworkPlayerTeam(SimulationWorld.LocalPlayerSlot, PlayerTeam.Red);
        var victim = world.LocalPlayer;
        var killer = Join(world, 2, PlayerTeam.Blue);
        var assistant = Join(world, 3, PlayerTeam.Blue);
        for (var count = 0; count < 4; count += 1)
        {
            victim.IncrementDominationKillCount(assistant.Id);
        }

        world.Combat.ApplyPlayerDamage(victim, 5, assistant);
        world.Combat.ApplyPlayerDamage(victim, 5, killer);
        world.PlayerDeaths.KillPlayer(victim, false, killer, "RocketKL");

        Assert.Equal(1, assistant.GetDominationKillCount(victim.Id));
        Assert.Equal(0, victim.GetDominationKillCount(assistant.Id));
        Assert.Equal(1, killer.GetDominationKillCount(victim.Id));
        var revenge = Assert.Single(world.KillFeedEntries.Where(entry => entry.SpecialType == KillFeedSpecialType.Revenge));
        Assert.Equal(assistant.Id, revenge.KillerPlayerId);
        Assert.Equal(victim.Id, revenge.VictimPlayerId);
    }

    [Fact]
    public void AssisterCanEarnDominationOnTheKillTheyHelpedComplete()
    {
        var world = new SimulationWorld();
        world.NetworkPlayers.CompleteLocalPlayerJoin(PlayerClass.Soldier);
        world.NetworkPlayers.TrySetNetworkPlayerTeam(SimulationWorld.LocalPlayerSlot, PlayerTeam.Red);
        var victim = world.LocalPlayer;
        var killer = Join(world, 2, PlayerTeam.Blue);
        var assistant = Join(world, 3, PlayerTeam.Blue);
        for (var count = 0; count < 3; count += 1)
        {
            assistant.IncrementDominationKillCount(victim.Id);
        }

        world.Combat.ApplyPlayerDamage(victim, 5, assistant);
        world.Combat.ApplyPlayerDamage(victim, 5, killer);
        world.PlayerDeaths.KillPlayer(victim, false, killer, "RocketKL");

        Assert.Equal(4, assistant.GetDominationKillCount(victim.Id));
        Assert.Equal(1, killer.GetDominationKillCount(victim.Id));
        var domination = Assert.Single(world.KillFeedEntries.Where(entry => entry.SpecialType == KillFeedSpecialType.Domination));
        Assert.Equal(assistant.Id, domination.KillerPlayerId);
        Assert.Equal(victim.Id, domination.VictimPlayerId);
    }

    [Fact]
    public void SamePlayerPassedAsKillerAndAssisterCountsOnceForDomination()
    {
        var world = new SimulationWorld();
        var victim = Join(world, 2, PlayerTeam.Red);
        var killer = Join(world, 3, PlayerTeam.Blue);
        for (var count = 0; count < 3; count += 1)
        {
            killer.IncrementDominationKillCount(victim.Id);
        }

        world.CombatFeedback.UpdateDominationStateForKill(victim, killer, killer);

        Assert.Equal(4, killer.GetDominationKillCount(victim.Id));
        Assert.Single(world.KillFeedEntries.Where(entry => entry.SpecialType == KillFeedSpecialType.Domination));
    }
}
