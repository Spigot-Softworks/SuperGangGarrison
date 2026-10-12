using OpenGarrison.Core;
using OpenGarrison.Core.BotBrain;
using OpenGarrison.Protocol;
using OpenGarrison.Server;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

[Collection(ContentRootTestGroup.Name)]
public sealed class ConquestModeTests
{
    [Theory]
    [InlineData("cq_test")]
    [InlineData("CQ_Test")]
    public void PrefixTakesPrecedenceOverControlPointEntities(string name)
    {
        var metadata = new GameMakerRoomMetadata(name, new WorldBounds(1024, 768), "", [], [], [],
            CreateMarkers(), []);
        Assert.Equal(GameModeKind.Conquest, SimpleLevelFactory.DetectMode(metadata));
        Assert.Equal(GameModeKind.ControlPoint, SimpleLevelFactory.DetectMode(metadata with { Name = "cp_test" }));
        Assert.Equal(GameModeKind.Scr, SimpleLevelFactory.DetectMode(metadata with { ExplicitGameMode = GameModeKind.Scr }));
        Assert.Equal("cq", CustomMapDirectoryLayout.GetModeFolderName(GameModeKind.Conquest));
        Assert.Equal("cq", CustomMapDirectoryLayout.GetModeFolderName(name));
    }

    [Fact]
    public void BuilderMetadataAndValidationRequireThreePointsAndZones()
    {
        Assert.True(MapGameModeMetadata.TryParseGameMode("CQ", out var mode));
        Assert.Equal(GameModeKind.Conquest, mode);
        Assert.Equal("cq", MapGameModeMetadata.ToPropertyValue(mode));
        var entities = new List<CustomMapBuilderEntity>
        {
            CustomMapBuilderEntity.Create("redspawn", 0, 0),
            CustomMapBuilderEntity.Create("bluespawn", 900, 0),
        };
        for (var i = 1; i <= 3; i++)
        {
            entities.Add(CustomMapBuilderEntity.Create("controlPoint", i * 200, 100,
                new Dictionary<string, string> { ["index"] = i.ToString(), ["initialOwner"] = "red" }));
            entities.Add(CustomMapBuilderEntity.Create("CapturePoint", i * 200, 100));
        }

        var document = CustomMapBuilderDocument.CreateEmpty("cq_test") with
        {
            Metadata = new Dictionary<string, string> { ["gameMode"] = "cq" },
            Entities = entities,
        };
        var validation = CustomMapBuilderValidator.Validate(document);
        Assert.Equal(CustomMapBuilderGameMode.Conquest, validation.Mode);
        Assert.Equal(CustomMapBuilderGameMode.Conquest,
            CustomMapBuilderValidator.Validate(document with { Metadata = new Dictionary<string, string>() }).Mode);
        Assert.DoesNotContain(validation.Issues, issue => issue.Code.StartsWith("cq_"));
        Assert.Contains(CustomMapBuilderValidator.Validate(document with { Entities = entities.Take(6).ToArray() }).Issues,
            issue => issue.Code == "cq_points");
        Assert.Contains(CustomMapBuilderValidator.Validate(document with
        {
            Entities = entities.Where(e => !e.Type.Equals("CapturePoint", StringComparison.OrdinalIgnoreCase)).ToArray(),
        }).Issues, issue => issue.Code == "cq_capture_zones");
        Assert.Null(ControlPointOwnershipResolver.ResolveBuilderInitialTeam(entities[2], validation.Mode, entities,
            overrideInitialOwnership: true));
    }

    [Fact]
    public void RoundStartsWithThreeNeutralUnlockedPointsAndFixedScoreTarget()
    {
        var world = CreateWorld();
        Assert.Equal(3, world.ControlPoints.Count);
        Assert.All(world.ControlPoints, point =>
        {
            Assert.Null(point.Team);
            Assert.False(point.IsLocked);
            Assert.Equal(world.Config.TicksPerSecond * 10, point.CapTimeTicks);
        });
        Assert.False(world.ControlPointSetupActive);
        Assert.False(world.Level.ControlPointSetupGatesActive);
        Assert.Equal(0, world.ObjectiveRules.KothUnlockTicksRemaining);
        Assert.Equal(0, world.RedCaps);
        Assert.Equal(0, world.BlueCaps);
        world.MapLifecycle.SetTimeLimitMinutes(1);
        world.MapLifecycle.SetCapLimit(5);
        Assert.Equal(200, world.MatchRules.CapLimit);
        Assert.Equal(15, world.MatchRules.TimeLimitMinutes);
        Assert.Equal(world.Config.TicksPerSecond * 60 * 15, world.MatchRules.TimeLimitTicks);
        Assert.Equal(world.MatchRules.TimeLimitTicks, world.MatchState.TimeRemainingTicks);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(120)]
    public void EachPointAwardsOneScorePerThreeSecondsAtEveryTickRate(int ticksPerSecond)
    {
        var world = CreateWorld(ticksPerSecond);
        world.ControlPoints[0].Team = PlayerTeam.Red;
        world.ControlPoints[1].Team = PlayerTeam.Red;
        world.ControlPoints[2].Team = PlayerTeam.Blue;
        var interval = ticksPerSecond * 3;
        Advance(world, interval - 1);
        Assert.Equal(0, world.RedCaps);
        Assert.Equal(0, world.BlueCaps);
        Advance(world, 1);
        Assert.Equal(2, world.RedCaps);
        Assert.Equal(1, world.BlueCaps);
        Advance(world, interval);
        Assert.Equal(4, world.RedCaps);
        Assert.Equal(2, world.BlueCaps);
        Assert.False(world.MatchState.IsEnded);
    }

    [Fact]
    public void OwnershipChangeStartsANewIntervalAndNeutralPointsDoNotScore()
    {
        var world = CreateWorld();
        var point = world.ControlPoints[0];
        point.Team = PlayerTeam.Red;
        Advance(world, 89);
        point.Team = PlayerTeam.Blue;
        Advance(world, 89);
        Assert.Equal(0, world.RedCaps);
        Assert.Equal(0, world.BlueCaps);
        Advance(world, 1);
        Assert.Equal(1, world.BlueCaps);
        point.Team = null;
        Advance(world, 180);
        Assert.Equal(1, world.BlueCaps);
        Assert.False(world.MatchState.IsEnded);
    }

    [Fact]
    public void OpponentCanCaptureAnyOwnedPointWithoutCapturingItsNeighbors()
    {
        var world = CreateWorld();
        foreach (var point in world.ControlPoints)
        {
            point.Team = PlayerTeam.Blue;
        }

        world.NetworkPlayers.PrepareLocalPlayerJoin();
        Assert.True(world.NetworkPlayers.TrySetNetworkPlayerTeam(SimulationWorld.LocalPlayerSlot,
            PlayerTeam.Red, respawnLivePlayerImmediately: true));
        world.NetworkPlayers.CompleteLocalPlayerJoin(PlayerClass.Scout);
        var target = world.ControlPoints[2];
        target.IsLocked = true;
        target.CappingTeam = PlayerTeam.Red;
        target.CappingTicks = target.CapTimeTicks - 2;
        world.LocalPlayer.TeleportTo(target.Marker.CenterX, target.Marker.CenterY);
        world.AdvanceOneTick();
        Assert.Equal(PlayerTeam.Red, target.Team);
        Assert.All(world.ControlPoints, point => Assert.False(point.IsLocked));
        Assert.Equal(PlayerTeam.Blue, world.ControlPoints[1].Team);
        Assert.False(world.MatchState.IsEnded);
    }

    [Fact]
    public void HoldingAllThreePointsDoesNotEndRoundBeforeTwoHundred()
    {
        var world = CreateWorld();
        foreach (var point in world.ControlPoints)
        {
            point.Team = PlayerTeam.Red;
        }

        Advance(world, 90);
        Assert.Equal(3, world.RedCaps);
        Assert.False(world.MatchState.IsEnded);
    }

    [Fact]
    public void OwnedContestedPointStillScoresAndTwoHundredEndsRoundImmediately()
    {
        var world = CreateWorld();
        var point = world.ControlPoints[0];
        point.Team = PlayerTeam.Red;
        world.ObjectiveRules.UpdatePointScoringState();
        ((IObjectiveRulesHost)world).RedCaps = 199;
        point.ConquestOwnedTicks = 89;
        point.CappingTeam = PlayerTeam.Blue;
        point.CappingTicks = 30;
        point.BlueCappers = 1;
        world.ObjectiveRules.UpdatePointScoringState();
        world.ObjectiveRules.AdvancePointScoringMatchState();
        Assert.Equal(200, world.RedCaps);
        Assert.True(world.MatchState.IsEnded);
        Assert.Equal(PlayerTeam.Red, world.MatchState.WinnerTeam);
        Advance(world, 90);
        Assert.Equal(200, world.RedCaps);
    }

    [Fact]
    public void SimultaneousScoreThresholdIsADrawAndRoundRestartClearsAccrual()
    {
        var world = CreateWorld();
        world.ControlPoints[0].Team = PlayerTeam.Red;
        world.ControlPoints[1].Team = PlayerTeam.Blue;
        world.ObjectiveRules.UpdatePointScoringState();
        ((IObjectiveRulesHost)world).RedCaps = 199;
        ((IObjectiveRulesHost)world).BlueCaps = 199;
        world.ControlPoints[0].ConquestOwnedTicks = 89;
        world.ControlPoints[1].ConquestOwnedTicks = 89;
        Advance(world, 1);
        Assert.True(world.MatchState.IsEnded);
        Assert.Null(world.MatchState.WinnerTeam);
        world.MapLifecycle.RestartCurrentRound(preservePlayerStats: true);
        Assert.False(world.MatchState.IsEnded);
        Assert.Equal(0, world.RedCaps);
        Assert.Equal(0, world.BlueCaps);
        Assert.All(world.ControlPoints, point =>
        {
            Assert.Null(point.Team);
            Assert.Equal(0, point.ConquestOwnedTicks);
        });
    }

    [Fact]
    public void ScoresAndAllThreePointStatesRoundTripToClient()
    {
        var server = CreateWorld();
        server.ControlPoints[0].Team = PlayerTeam.Red;
        server.ControlPoints[1].Team = PlayerTeam.Blue;
        Advance(server, 90);
        var botManager = new ServerBotManager(server, server.Config, new BotBrainPracticeBotController());
        var broadcaster = new SnapshotBroadcaster(server, server.Config, new Dictionary<byte, ClientSession>(),
            24, botManager, 0, new ServerMapMetadataResolver(server), static (_, _, _) => { });
        var snapshot = broadcaster.CaptureCanonicalDemoSnapshot() with
        {
            // Canonical recordings have no viewer when the server has no connected sessions.
            Players = [server.Snapshots.ToSnapshotPlayerState(1, server.LocalPlayer, null, static _ => 0)],
        };
        Assert.True(ProtocolCodec.TryDeserialize(ProtocolCodec.Serialize(snapshot, ProtocolCompressionSettings.Disabled),
            out var decoded));
        var client = CreateWorld();
        Assert.True(client.SnapshotApply.ApplySnapshot(Assert.IsType<SnapshotMessage>(decoded)));
        Assert.Equal(GameModeKind.Conquest, client.MatchRules.Mode);
        Assert.Equal(200, client.MatchRules.CapLimit);
        Assert.Equal(server.MatchRules.TimeLimitTicks, client.MatchRules.TimeLimitTicks);
        Assert.Equal(1, client.RedCaps);
        Assert.Equal(1, client.BlueCaps);
        Assert.Equal(3, client.ControlPoints.Count);
        Assert.Equal(PlayerTeam.Red, client.ControlPoints[0].Team);
        Assert.Equal(PlayerTeam.Blue, client.ControlPoints[1].Team);
        Assert.Null(client.ControlPoints[2].Team);
        Assert.False(client.MatchState.IsEnded);
    }

    [Fact]
    public void CompetitiveSkirmishDoesNotCaptureOrAwardPoints()
    {
        var world = CreateWorld();
        world.ReadyUp.SetCompetitiveReadyUpEnabled(true);
        world.ControlPoints[0].Team = PlayerTeam.Red;
        Advance(world, 90);
        Assert.Equal(0, world.RedCaps);
        Assert.False(world.MatchState.IsEnded);
    }

    [Fact]
    public void PointScoringHonorsTheExistingScoreDecisionGate()
    {
        var world = CreateWorld();
        world.ControlPoints[0].Team = PlayerTeam.Red;
        WorldScoreDecisionRequest? request = null;
        world.DecisionGate.ScoreDecisionInterceptor = scoreRequest =>
        {
            request = scoreRequest;
            return WorldDecisionResult.Cancel();
        };
        Advance(world, 90);
        Assert.Equal(0, world.RedCaps);
        Assert.True(request.HasValue);
        Assert.Equal("conquest_point_held", request.Value.Reason);
        world.DecisionGate.ScoreDecisionInterceptor = null;
        Advance(world, 90);
        Assert.Equal(1, world.RedCaps);
    }

    [Theory]
    [InlineData(25, 10, PlayerTeam.Red)]
    [InlineData(10, 25, PlayerTeam.Blue)]
    [InlineData(10, 10, null)]
    public void FifteenMinuteTimeoutUsesTheHigherScoreOrDraw(int redScore, int blueScore, PlayerTeam? winner)
    {
        var world = CreateWorld();
        ((IObjectiveRulesHost)world).RedCaps = redScore;
        ((IObjectiveRulesHost)world).BlueCaps = blueScore;
        world.TestSetMatchState(world.MatchState with { TimeRemainingTicks = 2 });
        Advance(world, 1);
        Assert.False(world.MatchState.IsEnded);
        Assert.Equal(1, world.MatchState.TimeRemainingTicks);
        Advance(world, 1);
        Assert.True(world.MatchState.IsEnded);
        Assert.Equal(winner, world.MatchState.WinnerTeam);
        Assert.Equal(0, world.MatchState.TimeRemainingTicks);
    }

    [Fact]
    public void BotsChooseCaptureZonesInConquest()
    {
        var world = CreateWorld();
        var goal = ObjectiveEvaluator.EvaluateGoal(world.LocalPlayer, world, PlayerTeam.Red, combatTarget: null);
        Assert.Contains(world.ControlPoints, point => point.HealingAuraCenterX == goal.X && point.HealingAuraCenterY == goal.Y);
    }

    private static SimulationWorld CreateWorld(int ticksPerSecond = 30)
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false, TicksPerSecond = ticksPerSecond });
        world.TestSetLevel(new SimpleLevel("cq_test", GameModeKind.Conquest, new WorldBounds(1024, 768), 1, null,
            1, 1, new SpawnPoint(100, 100), [new SpawnPoint(100, 100)], [new SpawnPoint(900, 100)], [],
            CreateMarkers(), 768, [], false, controlPointSettings: new CustomMapControlPointSettings(true)));
        return world;
    }

    private static RoomObjectMarker[] CreateMarkers()
    {
        var markers = new List<RoomObjectMarker>();
        for (var i = 1; i <= 3; i++)
        {
            markers.Add(new RoomObjectMarker(RoomObjectType.ControlPoint, 200 * i, 92, 48, 24, "",
                SourceName: $"ControlPoint{i}", InitialOwnership: ControlPointInitialOwnership.Red));
            markers.Add(new RoomObjectMarker(RoomObjectType.CaptureZone, 200 * i - 16, 72, 80, 64, ""));
        }

        markers.Add(new RoomObjectMarker(RoomObjectType.ControlPointSetupGate, 240, 72, 60, 6, ""));
        return markers.ToArray();
    }

    private static void Advance(SimulationWorld world, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            world.AdvanceOneTick();
        }
    }
}
