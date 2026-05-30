using System.Reflection;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class ServerMapRotationTests
{
    [Fact]
    public void MapRotationAdvancesInConfiguredOrderByDefault()
    {
        var world = new SimulationWorld();
        var manager = new MapRotationManager(
            world,
            requestedMap: "Truefort",
            mapRotationFile: null,
            stockMapRotation: ["Truefort", "Conflict", "ClassicWell"],
            static _ => { });

        ForceMapChangeReady(world);

        Assert.True(manager.TryApplyPendingMapChange(out var transition));
        Assert.False(manager.MapRotationShuffleEnabled);
        Assert.Equal("Conflict", transition.NextLevelName);
        Assert.Equal("Conflict", world.Level.Name);
    }

    [Fact]
    public void MapRotationShuffleChoosesRandomNonCurrentMapWhenPossible()
    {
        var world = new SimulationWorld();
        var manager = new MapRotationManager(
            world,
            requestedMap: "Truefort",
            mapRotationFile: null,
            stockMapRotation: ["Truefort", "Conflict", "ClassicWell"],
            static _ => { },
            mapRotationShuffleEnabled: true,
            shuffleRandom: new Random(0));

        ForceMapChangeReady(world);

        Assert.True(manager.TryApplyPendingMapChange(out var transition));
        Assert.True(manager.MapRotationShuffleEnabled);
        Assert.Equal("ClassicWell", transition.NextLevelName);
        Assert.Equal("ClassicWell", world.Level.Name);
    }

    [Fact]
    public void MapRotationShuffleStillHonorsQueuedNextRoundMap()
    {
        var world = new SimulationWorld();
        var manager = new MapRotationManager(
            world,
            requestedMap: "Truefort",
            mapRotationFile: null,
            stockMapRotation: ["Truefort", "Conflict", "ClassicWell"],
            static _ => { },
            mapRotationShuffleEnabled: true,
            shuffleRandom: new Random(0));

        Assert.True(manager.TrySetNextRoundMap("Conflict"));
        ForceMapChangeReady(world);

        Assert.True(manager.TryApplyPendingMapChange(out var transition));
        Assert.Equal("Conflict", transition.NextLevelName);
        Assert.Equal("Conflict", world.Level.Name);
    }

    [Fact]
    public void MapRotationFileAcceptsCustomMapPathsAndExtensions()
    {
        var root = Path.Combine(Path.GetTempPath(), $"opengarrison-rotation-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(root);
            var rotationPath = Path.Combine(root, "rotation.txt");
            File.WriteAllLines(rotationPath,
            [
                "# comments are ignored",
                "Maps/my_custom.png",
                "my_package/my_package.json",
                "\"quoted_map.png\"",
                "koth_harvest",
            ]);

            var rotation = ServerHelpers.LoadMapRotation(rotationPath, []);

            Assert.Equal(
            [
                "my_custom",
                "my_package",
                "quoted_map",
                "Harvest",
            ], rotation);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static void ForceMapChangeReady(SimulationWorld world, PlayerTeam? winner = null)
    {
        var matchStateProperty = typeof(SimulationWorld).GetProperty(nameof(SimulationWorld.MatchState))
            ?? throw new InvalidOperationException("MatchState property was not found.");
        var matchStateSetter = matchStateProperty.GetSetMethod(nonPublic: true)
            ?? throw new InvalidOperationException("MatchState setter was not found.");
        matchStateSetter.Invoke(world, [world.MatchState with { Phase = MatchPhase.Ended, WinnerTeam = winner }]);

        var mapChangeReadyField = typeof(SimulationWorld).GetField("_mapChangeReady", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("_mapChangeReady field was not found.");
        mapChangeReadyField.SetValue(world, true);
    }
}
