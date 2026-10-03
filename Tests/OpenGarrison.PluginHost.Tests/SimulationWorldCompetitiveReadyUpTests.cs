using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class SimulationWorldCompetitiveReadyUpTests
{

    [Fact]
    public void CompetitiveReadyUpStartsInSkirmishWithObjectivesHeld()
    {
        var world = new SimulationWorld();
        SetAttackDefenseControlPointLevel(world);

        world.ReadyUp.SetCompetitiveReadyUpEnabled(true);

        Assert.True(world.ReadyUp.CompetitiveReadyUpEnabled);
        Assert.Equal(CompetitiveReadyUpPhase.Skirmish, world.ReadyUp.CompetitiveReadyUpPhase);
        Assert.True(world.ReadyUp.CompetitiveObjectivesLocked);
        Assert.Equal(0, world.ReadyUp.CompetitiveReadyUpTicksRemaining);
        Assert.False(world.ControlPointSetupActive);
        Assert.Equal(0, world.ControlPointSetupTicksRemaining);
        Assert.False(world.Level.ControlPointSetupGatesActive);
        Assert.Equal(TeamGateLockMask.None, world.Level.ForcedBlockingTeamGates);
    }

    [Fact]
    public void CompetitiveReadyMajorityRunsCountdownThenLocksSpawnDoorsForConfiguredSetup()
    {
        var world = new SimulationWorld();
        SetAttackDefenseControlPointLevel(world);
        world.ReadyUp.SetCompetitiveSetupSeconds(10);
        world.ReadyUp.SetCompetitiveReadyUpEnabled(true);
        var playableSlots = new byte[] { 1, 2 };

        Assert.True(world.ReadyUp.TrySetNetworkPlayerReady(1, ready: true));
        world.ReadyUp.AdvanceCompetitiveReadyUp(playableSlots);
        Assert.Equal(CompetitiveReadyUpPhase.Skirmish, world.ReadyUp.CompetitiveReadyUpPhase);

        Assert.True(world.ReadyUp.TrySetNetworkPlayerReady(2, ready: true));
        world.ReadyUp.AdvanceCompetitiveReadyUp(playableSlots);
        Assert.Equal(CompetitiveReadyUpPhase.Countdown, world.ReadyUp.CompetitiveReadyUpPhase);
        Assert.Equal(world.Config.TicksPerSecond * 3, world.ReadyUp.CompetitiveReadyUpTicksRemaining);

        AdvanceCompetitiveReadyUpTicks(world, playableSlots, world.Config.TicksPerSecond * 3);

        Assert.Equal(CompetitiveReadyUpPhase.Setup, world.ReadyUp.CompetitiveReadyUpPhase);
        Assert.Equal(world.Config.TicksPerSecond * 10, world.ReadyUp.CompetitiveReadyUpTicksRemaining);
        Assert.Equal(TeamGateLockMask.Red | TeamGateLockMask.Blue, world.Level.ForcedBlockingTeamGates);
        Assert.False(world.ControlPointSetupActive);
        Assert.Equal(0, world.ControlPointSetupTicksRemaining);
        Assert.Equal(world.MatchRules.TimeLimitTicks, world.MatchState.TimeRemainingTicks);
    }

    [Fact]
    public void CompetitiveSetupFinishesThenStartsNormalControlPointSetup()
    {
        var world = new SimulationWorld();
        SetAttackDefenseControlPointLevel(world);
        world.ReadyUp.SetCompetitiveSetupSeconds(10);
        world.ReadyUp.SetCompetitiveReadyUpEnabled(true);
        var playableSlots = new byte[] { 1, 2 };

        world.ReadyUp.TrySetNetworkPlayerReady(1, ready: true);
        world.ReadyUp.TrySetNetworkPlayerReady(2, ready: true);
        world.ReadyUp.AdvanceCompetitiveReadyUp(playableSlots);
        AdvanceCompetitiveReadyUpTicks(world, playableSlots, world.Config.TicksPerSecond * 3);
        AdvanceCompetitiveReadyUpTicks(world, playableSlots, world.Config.TicksPerSecond * 10);

        Assert.Equal(CompetitiveReadyUpPhase.Live, world.ReadyUp.CompetitiveReadyUpPhase);
        Assert.Equal(0, world.ReadyUp.CompetitiveReadyUpTicksRemaining);
        Assert.Equal(TeamGateLockMask.None, world.Level.ForcedBlockingTeamGates);
        Assert.True(world.ControlPointSetupActive);
        Assert.True(world.Level.ControlPointSetupGatesActive);
        Assert.Equal(world.ObjectiveRules.ControlPointSetupDurationTicks, world.ControlPointSetupTicksRemaining);
        Assert.Equal(world.Config.TicksPerSecond * 30, world.ControlPointSetupTicksRemaining);
        Assert.Equal(world.MatchRules.TimeLimitTicks, world.MatchState.TimeRemainingTicks);
        Assert.False(world.ReadyUp.IsNetworkPlayerReady(1));
        Assert.False(world.ReadyUp.IsNetworkPlayerReady(2));
    }

    private static void AdvanceCompetitiveReadyUpTicks(
        SimulationWorld world,
        IReadOnlyCollection<byte> playableSlots,
        int ticks)
    {
        for (var tick = 0; tick < ticks; tick += 1)
        {
            world.ReadyUp.AdvanceCompetitiveReadyUp(playableSlots);
        }
    }

    private static void SetAttackDefenseControlPointLevel(SimulationWorld world)
    {
        world.TestSetLevel(
            new SimpleLevel(
                name: "adcp_competitive_ready_test",
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
                    new RoomObjectMarker(
                        RoomObjectType.TeamGate,
                        140f,
                        72f,
                        16f,
                        80f,
                        "",
                        Team: PlayerTeam.Red,
                        SourceName: "RedTeamGate"),
                    new RoomObjectMarker(
                        RoomObjectType.TeamGate,
                        860f,
                        72f,
                        16f,
                        80f,
                        "",
                        Team: PlayerTeam.Blue,
                        SourceName: "BlueTeamGate"),
                ],
                floorY: 768f,
                solids: [],
                importedFromSource: false));
    }
}
