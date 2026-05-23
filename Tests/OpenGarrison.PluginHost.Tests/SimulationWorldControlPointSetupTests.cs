using OpenGarrison.Core;
using System.Reflection;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class SimulationWorldControlPointSetupTests
{
    private static readonly MethodInfo CombatTestSetLevelMethod = GetRequiredSimulationWorldMethod("CombatTestSetLevel");

    [Fact]
    public void AttackDefenseControlPointSetupUsesModernThirtySecondSetupAndThreeMinuteRoundTimer()
    {
        var world = new SimulationWorld();
        SetAttackDefenseControlPointLevel(world);

        Assert.Equal(world.Config.TicksPerSecond * 30, world.ControlPointSetupTicksRemaining);
        Assert.Equal(world.ControlPointSetupTicksRemaining, world.ControlPointSetupDurationTicks);
        Assert.True(world.ControlPointSetupActive);
        Assert.True(world.Level.ControlPointSetupGatesActive);
        Assert.Equal(3, world.MatchRules.TimeLimitMinutes);
        Assert.Equal(world.Config.TicksPerSecond * 60 * 3, world.MatchRules.TimeLimitTicks);
    }

    [Fact]
    public void AttackDefenseControlPointSetupSirenResetsAttackRoundTimerToThreeMinutes()
    {
        var world = new SimulationWorld();
        SetAttackDefenseControlPointLevel(world);

        for (var tick = 0; tick < world.ControlPointSetupDurationTicks - world.Config.TicksPerSecond; tick += 1)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(world.Config.TicksPerSecond, world.ControlPointSetupTicksRemaining);
        Assert.Equal(world.MatchRules.TimeLimitTicks - 1, world.MatchState.TimeRemainingTicks);
    }

    private static void SetAttackDefenseControlPointLevel(SimulationWorld world)
    {
        CombatTestSetLevelMethod.Invoke(
            world,
            [
                new SimpleLevel(
                    name: "adcp_setup_test",
                    mode: GameModeKind.ControlPoint,
                    bounds: new WorldBounds(1024f, 768f),
                    mapScale: 1f,
                    backgroundAssetName: null,
                    mapAreaIndex: 1,
                    mapAreaCount: 1,
                    localSpawn: new SpawnPoint(100f, 100f),
                    redSpawns: [new SpawnPoint(100f, 100f)],
                    blueSpawns: [new SpawnPoint(900f, 100f)],
                    intelBases: [],
                    roomObjects:
                    [
                        new RoomObjectMarker(
                            RoomObjectType.ControlPoint,
                            320f,
                            92f,
                            48f,
                            24f,
                            "",
                            SourceName: "ControlPoint1"),
                        new RoomObjectMarker(
                            RoomObjectType.ControlPoint,
                            620f,
                            92f,
                            48f,
                            24f,
                            "",
                            SourceName: "ControlPoint2"),
                        new RoomObjectMarker(
                            RoomObjectType.ControlPointSetupGate,
                            240f,
                            72f,
                            60f,
                            6f,
                            "SetupGateS",
                            SourceName: "ControlPointSetupGate"),
                    ],
                    floorY: 768f,
                    solids: [],
                    importedFromSource: false),
            ]);
    }

    private static MethodInfo GetRequiredSimulationWorldMethod(string name)
    {
        return typeof(SimulationWorld).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Could not find SimulationWorld.{name}.");
    }
}
