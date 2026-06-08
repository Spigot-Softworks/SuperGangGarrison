using System.Collections.Generic;
using System.Globalization;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class ScrGamemodeTests
{
    [Fact]
    public void InferGameModeTreatsLogicScoreAsScr()
    {
        var entities = new[]
        {
            CustomMapBuilderEntity.Create(
                MapLogicScoreTriggerMetadata.ScoreTriggerEntityType,
                0f,
                0f,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
        };

        Assert.Equal(CustomMapBuilderGameMode.Scr, CustomMapBuilderValidator.InferGameMode(entities));
    }

    [Theory]
    [InlineData("moreEqual", ScrWinWhenScore.MoreEqual)]
    [InlineData("lessEqual", ScrWinWhenScore.LessEqual)]
    public void ParseWinWhenScoreReadsMetadata(string rawValue, ScrWinWhenScore expected)
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ScrMapSettingsMetadata.WinWhenScorePropertyKey] = rawValue,
        };

        Assert.Equal(expected, ScrMapSettingsMetadata.ParseWinWhenScore(metadata));
    }

    [Fact]
    public void ResolveRoundEndWinnerPrefersHigherScoreByDefault()
    {
        var settings = CustomMapScrSettings.Default with { RoundEndWin = ScrRoundEndWin.MorePoints };

        Assert.Equal(PlayerTeam.Red, settings.ResolveRoundEndWinner(redCaps: 12, blueCaps: 8));
        Assert.Equal(PlayerTeam.Blue, settings.ResolveRoundEndWinner(redCaps: 3, blueCaps: 9));
        Assert.Null(settings.ResolveRoundEndWinner(redCaps: 5, blueCaps: 5));
    }

    [Fact]
    public void ScoreTriggerImporterBuildsRisingEdgeConsumer()
    {
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "pulse",
                Kind = MapLogicNodeKind.RisingEdge,
                InputRef1 = string.Empty,
            },
        ]);
        var entities = new[]
        {
            new MapImportedEntity(
                MapLogicScoreTriggerMetadata.ScoreTriggerEntityType,
                0f,
                0f,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [MapLogicMetadata.LogicInputPropertyKey] = "node:pulse",
                    [MapLogicScoreTriggerMetadata.ScoreTeamPropertyKey] = "blue",
                    [MapLogicScoreTriggerMetadata.ChangePropertyKey] = "subtract",
                    [MapLogicScoreTriggerMetadata.ValuePropertyKey] = "3",
                }),
        };

        var triggers = MapLogicScoreTriggerImporter.BuildFromEntities(entities, graph);

        Assert.True(triggers.HasTriggers);
        var trigger = triggers.Triggers[0];
        Assert.Equal(MapLogicScoreTeamTarget.Blue, trigger.TeamTarget);
        Assert.Equal(MapLogicScoreChangeMode.Subtract, trigger.ChangeMode);
        Assert.Equal(3, trigger.Value);
        Assert.Equal(-3, trigger.SignedDelta);
        Assert.Equal(graph.NodeIndexByKey["pulse"], trigger.InputNodeIndex);
    }

    [Fact]
    public void TryModifyTeamScoreEndsRoundOnMoreEqualThresholdCrossing()
    {
        var world = CreateScrWorld(new CustomMapScrSettings(
            ScoreToWin: 10,
            WinWhenScore: ScrWinWhenScore.MoreEqual,
            RoundEndWin: ScrRoundEndWin.MorePoints,
            RedStartingScore: 9,
            BlueStartingScore: 2));

        world.CombatTestFinalizeScrRoundStart();
        Assert.False(world.MatchState.IsEnded);

        Assert.True(world.TryModifyTeamScore(PlayerTeam.Red, 1, "logic_score"));
        Assert.True(world.MatchState.IsEnded);
        Assert.Equal(PlayerTeam.Red, world.MatchState.WinnerTeam);
    }

    [Fact]
    public void RoundStartEndsImmediatelyWhenOnlyOneTeamMeetsLessEqualThreshold()
    {
        var world = CreateScrWorld(new CustomMapScrSettings(
            ScoreToWin: 5,
            WinWhenScore: ScrWinWhenScore.LessEqual,
            RoundEndWin: ScrRoundEndWin.MorePoints,
            RedStartingScore: 8,
            BlueStartingScore: 4));

        world.CombatTestFinalizeScrRoundStart();

        Assert.True(world.MatchState.IsEnded);
        Assert.Equal(PlayerTeam.Blue, world.MatchState.WinnerTeam);
    }

    [Fact]
    public void RoundStartUsesTiebreakWhenScoreToWinIsZeroAndBothTeamsQualify()
    {
        var world = CreateScrWorld(new CustomMapScrSettings(
            ScoreToWin: 0,
            WinWhenScore: ScrWinWhenScore.MoreEqual,
            RoundEndWin: ScrRoundEndWin.MorePoints,
            RedStartingScore: 12,
            BlueStartingScore: 7));

        world.CombatTestFinalizeScrRoundStart();

        Assert.True(world.MatchState.IsEnded);
        Assert.Equal(PlayerTeam.Red, world.MatchState.WinnerTeam);
    }

    [Fact]
    public void ResolveBuilderGameModePrefersPersistedMetadata()
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [MapGameModeMetadata.GameModePropertyKey] = MapGameModeMetadata.ScrPropertyValue,
        };

        Assert.Equal(
            CustomMapBuilderGameMode.Scr,
            MapGameModeMetadata.ResolveBuilderGameMode(metadata, Array.Empty<CustomMapBuilderEntity>()));
    }

    [Fact]
    public void ScoreTriggerAddsOnceForCpImpulseCapture()
    {
        var world = CreateScrWorldWithCpScoreTrigger(MapLogicSignalMode.Impulse);
        Assert.Equal(0, world.RedCaps);

        world.CombatTestSetControlPointOwner(1, PlayerTeam.Red);
        world.RefreshMapLogicRuntimeIfControlPointInputsChanged();
        Assert.Equal(1, world.RedCaps);

        for (var tick = 0; tick < 10; tick += 1)
        {
            world.TickMapLogicTimers();
        }

        Assert.Equal(1, world.RedCaps);
    }

    [Fact]
    public void ScoreTriggerAddsOnceForDamageTriggerImpulseAnyDamage()
    {
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "dmg",
                Kind = MapLogicNodeKind.DamageTrigger,
                DamageableRoomObjectIndex = 0,
                TriggerOnAnyDamage = true,
                SignalMode = MapLogicSignalMode.Impulse,
            },
        ]);
        var scoreTriggers = new MapLogicScoreTriggerSet(
        [
            new MapLogicScoreTrigger(
                graph.NodeIndexByKey["dmg"],
                MapLogicScoreTeamTarget.Red,
                MapLogicScoreChangeMode.Add,
                value: 1,
                nodePriority: 0),
        ]);
        var settings = new CustomMapScrSettings(
            ScoreToWin: 100,
            WinWhenScore: ScrWinWhenScore.MoreEqual,
            RoundEndWin: ScrRoundEndWin.MorePoints,
            RedStartingScore: 0,
            BlueStartingScore: 0);
        var world = new SimulationWorld();
        world.CombatTestSetLevel(new SimpleLevel(
            name: "scr_damage_score_test",
            mode: GameModeKind.Scr,
            bounds: new WorldBounds(1024f, 768f),
            mapScale: 1f,
            backgroundAssetName: null,
            mapAreaIndex: 1,
            mapAreaCount: 1,
            localSpawn: new SpawnPoint(10f, 10f),
            redSpawns: [new SpawnPoint(10f, 10f)],
            blueSpawns: [new SpawnPoint(900f, 100f)],
            intelBases: [],
            roomObjects:
            [
                new RoomObjectMarker(
                    RoomObjectType.DamageableZone,
                    100f,
                    200f,
                    48f,
                    24f,
                    string.Empty,
                    SourceName: DamageableMetadata.DamageableEntityType,
                    DamageableZone: new DamageableZoneConfiguration(100f, -1, false, false, true, SentryTarget: true, Stabbable: false)),
            ],
            floorY: 768f,
            solids: [],
            importedFromSource: false,
            scrSettings: settings,
            logicGraph: graph,
            logicScoreTriggers: scoreTriggers));
        world.CombatTestFinalizeScrRoundStart();
        Assert.Equal(0, world.RedCaps);

        Assert.True(world.TryApplyDamageableZoneDamage(0, 10f, PlayerTeam.Red));
        Assert.Equal(1, world.RedCaps);

        world.TickMapLogicTimers();

        Assert.True(world.TryApplyDamageableZoneDamage(0, 10f, PlayerTeam.Red));
        Assert.Equal(2, world.RedCaps);
    }

    [Fact]
    public void ScoreTriggerAddsOnceForLatchedCpOwnership()
    {
        var world = CreateScrWorldWithCpScoreTrigger(MapLogicSignalMode.Latch);
        Assert.Equal(0, world.RedCaps);

        world.CombatTestSetControlPointOwner(1, PlayerTeam.Red);
        world.RefreshMapLogicRuntimeIfControlPointInputsChanged();
        Assert.Equal(1, world.RedCaps);

        for (var tick = 0; tick < 10; tick += 1)
        {
            world.TickMapLogicTimers();
        }

        Assert.Equal(1, world.RedCaps);
    }

    [Fact]
    public void ScoreTriggerRuntimeDoesNotRepeatAfterPastRisingEdge()
    {
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "cp",
                Kind = MapLogicNodeKind.CpTrigger,
                LinkedControlPointIndex = 1,
                SignalMode = MapLogicSignalMode.Impulse,
                CpCaptureDetectMode = MapLogicCpCaptureDetectMode.RedCapture,
            },
        ]);
        var scoreTriggers = new MapLogicScoreTriggerSet(
        [
            new MapLogicScoreTrigger(
                graph.NodeIndexByKey["cp"],
                MapLogicScoreTeamTarget.Red,
                MapLogicScoreChangeMode.Add,
                value: 1,
                nodePriority: 0),
        ]);
        var world = CreateScrWorld(new CustomMapScrSettings(
            ScoreToWin: 100,
            WinWhenScore: ScrWinWhenScore.MoreEqual,
            RoundEndWin: ScrRoundEndWin.MorePoints,
            RedStartingScore: 0,
            BlueStartingScore: 0));
        world.CombatTestFinalizeScrRoundStart();

        var marker = new RoomObjectMarker(
            RoomObjectType.ControlPoint,
            0f,
            0f,
            32f,
            32f,
            "ControlPointRedS",
            SourceName: "ControlPoint1");
        var points = new[] { new ControlPointState(1, marker) { Team = PlayerTeam.Blue } };
        var runtimeState = MapLogicActivatorRuntimeState.CreateForActivatorCount(1);

        graph.ResetCpTriggerStates(points);
        graph.EvaluateCombinatorial(points);
        MapLogicScoreTriggerRuntime.Apply(world, graph, scoreTriggers, runtimeState);
        Assert.Equal(0, world.RedCaps);

        points[0].Team = PlayerTeam.Red;
        graph.EvaluateCombinatorial(points);
        MapLogicScoreTriggerRuntime.Apply(world, graph, scoreTriggers, runtimeState);
        Assert.Equal(1, world.RedCaps);

        graph.EvaluateCombinatorial(points);
        MapLogicScoreTriggerRuntime.Apply(world, graph, scoreTriggers, runtimeState);
        Assert.Equal(1, world.RedCaps);

        for (var tick = 0; tick < 5; tick += 1)
        {
            graph.EvaluateCombinatorial(points);
            MapLogicScoreTriggerRuntime.Apply(world, graph, scoreTriggers, runtimeState);
        }

        Assert.Equal(1, world.RedCaps);
    }

    private static SimulationWorld CreateScrWorldWithCpScoreTrigger(MapLogicSignalMode cpSignalMode)
    {
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "cp",
                Kind = MapLogicNodeKind.CpTrigger,
                LinkedControlPointIndex = 1,
                SignalMode = cpSignalMode,
                CpCaptureDetectMode = MapLogicCpCaptureDetectMode.RedCapture,
                OwnerRequirement = MapLogicCpTriggerOwnerRequirement.Red,
            },
        ]);
        var scoreTriggers = new MapLogicScoreTriggerSet(
        [
            new MapLogicScoreTrigger(
                graph.NodeIndexByKey["cp"],
                MapLogicScoreTeamTarget.Red,
                MapLogicScoreChangeMode.Add,
                value: 1,
                nodePriority: 0),
        ]);
        var settings = new CustomMapScrSettings(
            ScoreToWin: 100,
            WinWhenScore: ScrWinWhenScore.MoreEqual,
            RoundEndWin: ScrRoundEndWin.MorePoints,
            RedStartingScore: 0,
            BlueStartingScore: 0);
        var world = new SimulationWorld();
        world.CombatTestSetLevel(new SimpleLevel(
            name: "scr_cp_score_test",
            mode: GameModeKind.Scr,
            bounds: new WorldBounds(1024f, 768f),
            mapScale: 1f,
            backgroundAssetName: null,
            mapAreaIndex: 1,
            mapAreaCount: 1,
            localSpawn: new SpawnPoint(10f, 10f),
            redSpawns: [new SpawnPoint(10f, 10f)],
            blueSpawns: [new SpawnPoint(900f, 100f)],
            intelBases: [],
            roomObjects:
            [
                new RoomObjectMarker(
                    RoomObjectType.ControlPoint,
                    100f,
                    200f,
                    48f,
                    24f,
                    "ControlPointNeutralS",
                    SourceName: "ControlPoint1"),
            ],
            floorY: 768f,
            solids: [],
            importedFromSource: false,
            scrSettings: settings,
            logicGraph: graph,
            logicScoreTriggers: scoreTriggers));
        world.CombatTestFinalizeScrRoundStart();
        return world;
    }

    private static SimulationWorld CreateScrWorld(CustomMapScrSettings settings)
    {
        var world = new SimulationWorld();
        world.CombatTestSetLevel(new SimpleLevel(
            name: "scr_test",
            mode: GameModeKind.Scr,
            bounds: new WorldBounds(1024f, 768f),
            mapScale: 1f,
            backgroundAssetName: null,
            mapAreaIndex: 1,
            mapAreaCount: 1,
            localSpawn: new SpawnPoint(10f, 10f),
            redSpawns: [new SpawnPoint(10f, 10f)],
            blueSpawns: [new SpawnPoint(900f, 100f)],
            intelBases: [],
            roomObjects: [],
            floorY: 768f,
            solids: [],
            importedFromSource: false,
            scrSettings: settings));
        return world;
    }

    [Theory]
    [InlineData("0", -1, 0)]
    [InlineData("3", -1, 2)]
    [InlineData("999", 1, 999)]
    [InlineData(null, 0, 1)]
    public void ScoreTriggerValuePropertyStepsByOneAndClampsAtMinimum(string? current, int delta, int expected)
    {
        Assert.Equal(expected, MapLogicScoreTriggerMetadata.StepValueProperty(current, delta));
        Assert.Equal(expected.ToString(CultureInfo.InvariantCulture), MapLogicScoreTriggerMetadata.ToValuePropertyValue(expected));
    }
}
