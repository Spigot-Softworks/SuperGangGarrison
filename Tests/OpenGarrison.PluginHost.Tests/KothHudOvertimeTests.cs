using System.Collections.Generic;
using System.Linq;
using OpenGarrison.Client;
using OpenGarrison.Core;
using OpenGarrison.Core.BotBrain;
using OpenGarrison.Protocol;
using OpenGarrison.Server;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class KothHudOvertimeTests
{
    [Theory]
    [InlineData(0f, 1)]
    [InlineData(1f, 0)]
    public void ContestedExhaustedKothClockPublishesOvertimeThroughSnapshot(float cappingTicks, int blueCappers)
    {
        var server = CreateServerKothWorld();
        var point = Assert.Single(server.ControlPoints.Where(point => point.Marker.IsSingleKothControlPoint()));
        point.Team = PlayerTeam.Red;
        point.CappingTicks = cappingTicks;
        point.CappingTeam = null;
        point.BlueCappers = blueCappers;
        point.Cappers = blueCappers;
        server.Objectives.Koth.RedTimerTicksRemaining = 0;
        server.Objectives.Koth.BlueTimerTicksRemaining = server.Config.TicksPerSecond * 180;
        server.Objectives.Koth.UnlockTicksRemaining = 0;

        Assert.Null(server.ObjectiveRules.ResolveKothObjectiveWinner());
        server.ObjectiveRules.AdvanceKothMatchStateCore();

        Assert.True(server.MatchState.IsOvertime);
        var botManager = new ServerBotManager(server, server.Config, new BotBrainPracticeBotController());
        var broadcaster = new SnapshotBroadcaster(
            server,
            server.Config,
            new Dictionary<byte, ClientSession>(),
            maxPlayableClients: 24,
            botManager,
            transientEventReplayTicks: 0,
            new ServerMapMetadataResolver(server),
            static (_, _, _) => { });
        var snapshot = broadcaster.CaptureCanonicalDemoSnapshot();
        Assert.Equal((byte)MatchPhase.Overtime, snapshot.MatchPhase);
        Assert.Equal((byte)blueCappers, Assert.Single(snapshot.ControlPoints).Cappers);
        Assert.True(ProtocolCodec.TryDeserialize(
            ProtocolCodec.Serialize(snapshot, ProtocolCompressionSettings.Disabled),
            out var decodedSnapshot));

        var client = new SimulationWorld();
        Assert.True(client.SnapshotApply.ApplySnapshot(Assert.IsType<SnapshotMessage>(decodedSnapshot)));
        Assert.True(client.MatchState.IsOvertime);
        Assert.True(IsTeamTimerOvertime(client, PlayerTeam.Red));
    }

    [Fact]
    public void KothOvertimeClearsWhenTheContestEndsAndNormalObjectiveWinResolves()
    {
        var world = CreateServerKothWorld();
        var point = Assert.Single(world.ControlPoints.Where(point => point.Marker.IsSingleKothControlPoint()));
        point.Team = PlayerTeam.Red;
        point.CappingTicks = 1f;
        world.Objectives.Koth.RedTimerTicksRemaining = 0;
        world.Objectives.Koth.UnlockTicksRemaining = 0;

        world.ObjectiveRules.AdvanceKothMatchStateCore();
        Assert.True(world.MatchState.IsOvertime);

        point.CappingTicks = 0f;
        point.CappingTeam = null;
        point.BlueCappers = 0;
        point.Cappers = 0;
        world.ObjectiveRules.AdvanceKothMatchStateCore();

        Assert.True(world.MatchState.IsEnded);
        Assert.False(world.MatchState.IsOvertime);
        Assert.Equal(PlayerTeam.Red, world.MatchState.WinnerTeam);
    }

    [Fact]
    public void ExhaustedKothClockDoesNotShowOvertimeAfterItsTeamLosesThePoint()
    {
        var world = CreateServerKothWorld();
        var point = Assert.Single(world.ControlPoints.Where(point => point.Marker.IsSingleKothControlPoint()));
        point.Team = PlayerTeam.Blue;
        point.CappingTicks = 1f;
        point.CappingTeam = PlayerTeam.Red;
        point.RedCappers = 1;
        point.Cappers = 1;
        world.Objectives.Koth.RedTimerTicksRemaining = 0;
        world.Objectives.Koth.BlueTimerTicksRemaining = world.Config.TicksPerSecond * 60;
        world.Objectives.Koth.UnlockTicksRemaining = 0;

        world.ObjectiveRules.AdvanceKothMatchStateCore();

        Assert.False(world.MatchState.IsOvertime);
        Assert.False(world.MatchState.IsEnded);
        Assert.False(IsTeamTimerOvertime(world, PlayerTeam.Red));
    }

    [Fact]
    public void HudOvertimeRequiresAuthoritativeLiveKothPhaseAndOwnedExhaustedPoint()
    {
        var redHome = CreatePoint("KothRedControlPoint", PlayerTeam.Blue);
        var blueHome = CreatePoint("KothBlueControlPoint", PlayerTeam.Red);
        IReadOnlyList<ControlPointState> points = [redHome, blueHome];

        Assert.True(IsOvertime(GameModeKind.DoubleKingOfTheHill, PlayerTeam.Red, points, redTimer: 0, blueTimer: 60, matchOvertime: true));
        Assert.False(IsOvertime(GameModeKind.DoubleKingOfTheHill, PlayerTeam.Red, points, redTimer: 1, blueTimer: 60, matchOvertime: true));
        Assert.True(IsOvertime(GameModeKind.DoubleKingOfTheHill, PlayerTeam.Blue, points, redTimer: 60, blueTimer: 0, matchOvertime: true));
        Assert.False(IsOvertime(GameModeKind.DoubleKingOfTheHill, PlayerTeam.Blue, points, redTimer: 60, blueTimer: 0, matchOvertime: false));
        Assert.False(IsOvertime(GameModeKind.DoubleKingOfTheHill, PlayerTeam.Red, points, redTimer: 0, blueTimer: 60, matchTime: 0, matchOvertime: true));
        Assert.False(IsOvertime(GameModeKind.DoubleKingOfTheHill, PlayerTeam.Red, points, redTimer: 0, blueTimer: 60, unlockTicks: 1, matchOvertime: true));
        Assert.False(IsOvertime(GameModeKind.DoubleKingOfTheHill, PlayerTeam.Red, points, redTimer: 0, blueTimer: 60, matchEnded: true, matchOvertime: true));
    }

    private static bool IsTeamTimerOvertime(SimulationWorld world, PlayerTeam team)
    {
        return KothHudOvertimeResolver.IsTeamInOvertime(
            world.MatchRules.Mode,
            team,
            world.ControlPoints,
            world.ObjectiveRules.KothRedTimerTicksRemaining,
            world.ObjectiveRules.KothBlueTimerTicksRemaining,
            world.ObjectiveRules.KothUnlockTicksRemaining,
            world.MatchState.TimeRemainingTicks,
            world.MatchState.IsOvertime,
            world.MatchState.IsEnded);
    }

    private static SimulationWorld CreateServerKothWorld()
    {
        var world = new SimulationWorld();
        Assert.True(world.MapLifecycle.TryLoadLevel("Harvest"));
        Assert.Equal(GameModeKind.KingOfTheHill, world.MatchRules.Mode);
        return world;
    }

    private static bool IsOvertime(
        GameModeKind mode,
        PlayerTeam team,
        IReadOnlyList<ControlPointState> points,
        int redTimer,
        int blueTimer,
        int unlockTicks = 0,
        int matchTime = 1,
        bool matchOvertime = false,
        bool matchEnded = false)
    {
        return KothHudOvertimeResolver.IsTeamInOvertime(
            mode,
            team,
            points,
            redTimer,
            blueTimer,
            unlockTicks,
            matchTime,
            matchOvertime,
            matchEnded);
    }

    private static ControlPointState CreatePoint(string sourceName, PlayerTeam? owner)
    {
        var marker = new RoomObjectMarker(
            RoomObjectType.ControlPoint,
            0f,
            0f,
            42f,
            42f,
            string.Empty,
            SourceName: sourceName);
        return new ControlPointState(0, marker) { Team = owner };
    }
}
