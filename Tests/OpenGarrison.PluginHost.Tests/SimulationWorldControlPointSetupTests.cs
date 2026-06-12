using OpenGarrison.Core;
using System.Reflection;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

[Collection(ContentRootTestGroup.Name)]
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

    [Fact]
    public void StockDirtbowlSetupGatesKeepLowerDoorBlocks()
    {
        var areaOne = SimpleLevelFactory.CreateImportedLevel("Dirtbowl", mapAreaIndex: 1);
        var areaTwo = SimpleLevelFactory.CreateImportedLevel("Dirtbowl", mapAreaIndex: 2);

        Assert.NotNull(areaOne);
        Assert.NotNull(areaTwo);

        var areaOneGates = areaOne!.GetRoomObjects(RoomObjectType.ControlPointSetupGate);
        var areaTwoGates = areaTwo!.GetRoomObjects(RoomObjectType.ControlPointSetupGate);

        Assert.Contains(areaOneGates, gate => IsGate(gate, x: 648f, y: 906f, width: 32f, height: 62f));
        Assert.Contains(areaTwoGates, gate => IsGate(gate, x: 726f, y: 2262f, width: 32f, height: 80f));
        Assert.DoesNotContain(areaOneGates, gate => Nearly(gate.X, 648f) && gate.Y < 800f && gate.Bottom > 900f);
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

    private static bool IsGate(RoomObjectMarker gate, float x, float y, float width, float height)
    {
        return Nearly(gate.X, x)
            && Nearly(gate.Y, y)
            && Nearly(gate.Width, width)
            && Nearly(gate.Height, height);
    }

    private static bool Nearly(float actual, float expected)
    {
        return MathF.Abs(actual - expected) <= 0.01f;
    }
}
