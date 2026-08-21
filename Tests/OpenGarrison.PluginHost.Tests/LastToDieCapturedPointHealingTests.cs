using OpenGarrison.Core;
using System.Reflection;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class LastToDieCapturedPointHealingTests
{
    private static readonly MethodInfo ApplyPassiveEffectsMethod =
        typeof(SimulationWorld).GetMethod(
            "ApplyExperimentalPassivePlayerEffects",
            BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Could not find LTD passive effect method.");

    [Theory]
    [InlineData(PlayerClass.Scout)]
    [InlineData(PlayerClass.Medic)]
    [InlineData(PlayerClass.Sniper)]
    [InlineData(PlayerClass.Spy)]
    public void CapturedKothPointHealsEverySurvivorClassIncludingRemoteCoopPlayers(PlayerClass playerClass)
    {
        var world = CreateKothWorld();
        world.ConfigureExperimentalGameplaySettings(
            world.ExperimentalGameplaySettings with
            {
                EnableSecondaryAbilities = true,
                EnableCapturedPointHealingAura = true,
            });

        Assert.True(world.TryPrepareNetworkPlayerJoin(2));
        Assert.True(world.TrySetNetworkPlayerTeam(2, PlayerTeam.Red));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(2, playerClass));
        Assert.True(world.TryGetNetworkPlayer(2, out var remotePlayer));

        var point = Assert.Single(world.ControlPoints);
        point.Team = PlayerTeam.Red;
        point.HasHealingAura = true;
        remotePlayer.TeleportTo(point.HealingAuraCenterX, point.HealingAuraCenterY);
        remotePlayer.ForceSetHealth(remotePlayer.MaxHealth - 20);
        var healthBefore = remotePlayer.Health;

        Assert.True(world.IsPlayerInsideCapturedPointHealingAuraForVisuals(remotePlayer));
        for (var tick = 0; tick < world.Config.TicksPerSecond; tick += 1)
        {
            ApplyPassiveEffectsMethod.Invoke(world, [remotePlayer]);
        }

        Assert.True(remotePlayer.Health > healthBefore);
    }

    private static SimulationWorld CreateKothWorld()
    {
        var world = new SimulationWorld(new SimulationConfig
        {
            EnableLocalDummies = false,
            EnableEnemyTrainingDummy = false,
            EnableFriendlySupportDummy = false,
        });
        var setLevelMethod = typeof(SimulationWorld).GetMethod(
            "CombatTestSetLevel",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(setLevelMethod);
        _ = setLevelMethod!.Invoke(
            world,
            [
                new SimpleLevel(
                    name: "ltd_koth_healing_test",
                    mode: GameModeKind.KingOfTheHill,
                    bounds: new WorldBounds(1024f, 512f),
                    mapScale: 1f,
                    backgroundAssetName: null,
                    mapAreaIndex: 1,
                    mapAreaCount: 1,
                    localSpawn: new SpawnPoint(100f, 100f),
                    redSpawns: [new SpawnPoint(100f, 100f)],
                    blueSpawns: [new SpawnPoint(700f, 100f)],
                    intelBases: [],
                    roomObjects:
                    [
                        new RoomObjectMarker(
                            RoomObjectType.ControlPoint,
                            400f,
                            100f,
                            40f,
                            20f,
                            string.Empty,
                            SourceName: "KothControlPoint"),
                        new RoomObjectMarker(
                            RoomObjectType.CaptureZone,
                            400f,
                            100f,
                            100f,
                            60f,
                            string.Empty,
                            SourceName: "CaptureZone"),
                    ],
                    floorY: 512f,
                    solids: [],
                    importedFromSource: false),
            ]);

        return world;
    }
}
