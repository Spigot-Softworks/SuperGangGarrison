using System.Reflection;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class DirtbowlStageTransitionTests
{
    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 3)]
    public void AutomaticRoundChangeAdvancesStockDirtbowlAfterRedStageWin(
        int currentArea,
        int expectedArea)
    {
        var world = new SimulationWorld();
        Assert.True(world.TryLoadLevel("Dirtbowl", currentArea, preservePlayerStats: false));
        Assert.Equal(3, world.Level.MapAreaCount);
        Assert.True(world.AutoRestartOnMapChange);
        world.LocalPlayer.AddKill();

        ForceAutomaticMapChangeDue(world, PlayerTeam.Red);
        world.AdvanceOneTick();

        Assert.Equal("Dirtbowl", world.Level.Name);
        Assert.Equal(expectedArea, world.Level.MapAreaIndex);
        Assert.False(world.MatchState.IsEnded);
        Assert.Equal(1, world.LocalPlayer.Kills);
    }

    [Fact]
    public void AutomaticRoundChangeRestartsCurrentDirtbowlStageAfterBlueWin()
    {
        var world = new SimulationWorld();
        Assert.True(world.TryLoadLevel("Dirtbowl", mapAreaIndex: 2, preservePlayerStats: false));
        world.LocalPlayer.AddKill();

        ForceAutomaticMapChangeDue(world, PlayerTeam.Blue);
        world.AdvanceOneTick();

        Assert.Equal("Dirtbowl", world.Level.Name);
        Assert.Equal(2, world.Level.MapAreaIndex);
        Assert.False(world.MatchState.IsEnded);
        Assert.Equal(0, world.LocalPlayer.Kills);
    }

    [Fact]
    public void ServerRoundChangeAdvancesStockDirtbowlAfterRedStageWin()
    {
        var world = new SimulationWorld();
        var manager = new MapRotationManager(
            world,
            requestedMap: "Dirtbowl",
            mapRotationFile: null,
            stockMapRotation: ["Dirtbowl", "Truefort"],
            static _ => { });

        ForceServerMapChangeReady(world, PlayerTeam.Red);

        Assert.True(manager.TryApplyPendingMapChange(out var transition));
        Assert.Equal("Dirtbowl", transition.NextLevelName);
        Assert.Equal(2, transition.NextAreaIndex);
        Assert.True(transition.PreservePlayerStats);
        Assert.Equal(2, world.Level.MapAreaIndex);
    }

    private static void ForceAutomaticMapChangeDue(SimulationWorld world, PlayerTeam winner)
    {
        SetEndedMatchState(world, winner);
        SetPrivateField(world, "_pendingMapChangeTicks", 0);
        SetPrivateField(world, "_mapChangeReady", false);
    }

    private static void ForceServerMapChangeReady(SimulationWorld world, PlayerTeam winner)
    {
        SetEndedMatchState(world, winner);
        SetPrivateField(world, "_mapChangeReady", true);
    }

    private static void SetEndedMatchState(SimulationWorld world, PlayerTeam winner)
    {
        var property = typeof(SimulationWorld).GetProperty(nameof(SimulationWorld.MatchState))
            ?? throw new InvalidOperationException("MatchState property was not found.");
        var setter = property.GetSetMethod(nonPublic: true)
            ?? throw new InvalidOperationException("MatchState setter was not found.");
        setter.Invoke(world, [world.MatchState with { Phase = MatchPhase.Ended, WinnerTeam = winner }]);
    }

    private static void SetPrivateField(SimulationWorld world, string fieldName, object value)
    {
        var field = typeof(SimulationWorld).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"{fieldName} was not found.");
        field.SetValue(world, value);
    }
}
