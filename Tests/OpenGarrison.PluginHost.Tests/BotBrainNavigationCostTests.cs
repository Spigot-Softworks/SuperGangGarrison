using OpenGarrison.Core;
using OpenGarrison.Core.BotBrain;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class BotBrainNavigationCostTests
{
    private static readonly JsonSerializerOptions AssetJsonOptions = CreateAssetJsonOptions();

    [Fact]
    public void FindPathPenalizesCheapVerticalWalkRelay()
    {
        var nodes = new[]
        {
            new NavNode(0f, 0f, NavNodeKind.Surface, 0),
            new NavNode(50f, -80f, NavNodeKind.Surface, 1),
            new NavNode(100f, 0f, NavNodeKind.Surface, 2),
            new NavNode(50f, 0f, NavNodeKind.Surface, 3),
        };
        var adjacency = new List<NavEdge>[nodes.Length];
        for (var i = 0; i < adjacency.Length; i += 1)
        {
            adjacency[i] = [];
        }

        adjacency[0].Add(new NavEdge(1, NavEdgeKind.Walk, 2f));
        adjacency[1].Add(new NavEdge(2, NavEdgeKind.Walk, 2f));
        adjacency[0].Add(new NavEdge(3, NavEdgeKind.Walk, 50f));
        adjacency[3].Add(new NavEdge(2, NavEdgeKind.Walk, 50f));

        var graph = new NavGraph(nodes, adjacency, levelName: "Synthetic", mode: GameModeKind.CaptureTheFlag);

        var path = graph.FindPath(0, 2, PlayerClass.Heavy, team: PlayerTeam.Blue);

        Assert.NotNull(path);
        Assert.Equal(3, path.Count);
        Assert.Equal(0, path.GetWaypoint(0));
        Assert.Equal(3, path.GetWaypoint(1));
        Assert.Equal(2, path.GetWaypoint(2));
    }

    [Fact]
    public void ConflictCarrierReturnPenalizesSpawnAdjacentShortcut()
    {
        var nodes = new[]
        {
            new NavNode(-300f, 0f, NavNodeKind.Surface, 0),
            new NavNode(0f, 0f, NavNodeKind.Surface, 1),
            new NavNode(300f, 0f, NavNodeKind.Surface, 2),
            new NavNode(0f, 160f, NavNodeKind.Surface, 3),
            new NavNode(0f, 20f, NavNodeKind.Spawn, null),
        };
        var adjacency = new List<NavEdge>[nodes.Length];
        for (var i = 0; i < adjacency.Length; i += 1)
        {
            adjacency[i] = [];
        }

        adjacency[0].Add(new NavEdge(1, NavEdgeKind.Walk, 10f));
        adjacency[1].Add(new NavEdge(2, NavEdgeKind.Walk, 10f));
        adjacency[0].Add(new NavEdge(3, NavEdgeKind.Walk, 100f));
        adjacency[3].Add(new NavEdge(2, NavEdgeKind.Walk, 100f));

        var graph = new NavGraph(nodes, adjacency, levelName: "Conflict", mode: GameModeKind.CaptureTheFlag);

        var path = graph.FindPath(0, 2, PlayerClass.Heavy, team: PlayerTeam.Blue, carryingIntel: true);

        Assert.NotNull(path);
        Assert.Equal(3, path!.Count);
        Assert.Equal(0, path.GetWaypoint(0));
        Assert.Equal(3, path.GetWaypoint(1));
        Assert.Equal(2, path.GetWaypoint(2));
    }

    [Fact]
    public void ConflictShippedNavigationAssetMatchesCurrentLevelFingerprint()
    {
        var level = SimpleLevelFactory.CreateImportedLevel("Conflict");
        Assert.NotNull(level);

        var diagnostic = BotNavigationAssetStore.GetLoadDiagnostic(level!);
        Assert.True(
            BotNavigationAssetStore.TryLoadShipped(level!, out var asset),
            $"Conflict shipped nav failed: {diagnostic.ShippedStatus}");
        Assert.Equal(BotNavigationAssetStore.ComputeLevelFingerprint(level!), asset.LevelFingerprint);
    }

    [Fact]
    public void ConflictCarrierReturnKeepsLegacyAllSpawnPenalty()
    {
        var nodes = new[]
        {
            new NavNode(-300f, 0f, NavNodeKind.Surface, 0),
            new NavNode(0f, 0f, NavNodeKind.Surface, 1),
            new NavNode(300f, 0f, NavNodeKind.Surface, 2),
            new NavNode(0f, 160f, NavNodeKind.Surface, 3),
            new NavNode(0f, 20f, NavNodeKind.Spawn, null),
        };
        var adjacency = new List<NavEdge>[nodes.Length];
        for (var i = 0; i < adjacency.Length; i += 1)
        {
            adjacency[i] = [];
        }

        adjacency[0].Add(new NavEdge(1, NavEdgeKind.Walk, 10f));
        adjacency[1].Add(new NavEdge(2, NavEdgeKind.Walk, 10f));
        adjacency[0].Add(new NavEdge(3, NavEdgeKind.Walk, 100f));
        adjacency[3].Add(new NavEdge(2, NavEdgeKind.Walk, 100f));

        var graph = new NavGraph(
            nodes,
            adjacency,
            levelName: "Conflict",
            mode: GameModeKind.CaptureTheFlag,
            spawnAnchors: [new NavSpawnAnchor(0f, 20f, PlayerTeam.Blue)]);

        var path = graph.FindPath(0, 2, PlayerClass.Heavy, team: PlayerTeam.Blue, carryingIntel: true);

        Assert.NotNull(path);
        Assert.Equal(3, path!.Count);
        Assert.Equal(0, path.GetWaypoint(0));
        Assert.Equal(3, path.GetWaypoint(1));
        Assert.Equal(2, path.GetWaypoint(2));
    }

    [Fact]
    public void WaterwayCarrierReturnKeepsSpawnAdjacentRouteAvailable()
    {
        var nodes = new[]
        {
            new NavNode(-300f, 0f, NavNodeKind.Surface, 0),
            new NavNode(0f, 0f, NavNodeKind.Surface, 1),
            new NavNode(300f, 0f, NavNodeKind.Surface, 2),
            new NavNode(0f, 160f, NavNodeKind.Surface, 3),
            new NavNode(0f, 20f, NavNodeKind.Spawn, null),
        };
        var adjacency = new List<NavEdge>[nodes.Length];
        for (var i = 0; i < adjacency.Length; i += 1)
        {
            adjacency[i] = [];
        }

        adjacency[0].Add(new NavEdge(1, NavEdgeKind.Walk, 10f));
        adjacency[1].Add(new NavEdge(2, NavEdgeKind.Walk, 10f));
        adjacency[0].Add(new NavEdge(3, NavEdgeKind.Walk, 100f));
        adjacency[3].Add(new NavEdge(2, NavEdgeKind.Walk, 100f));

        var graph = new NavGraph(
            nodes,
            adjacency,
            levelName: "Waterway",
            mode: GameModeKind.CaptureTheFlag,
            spawnAnchors: [new NavSpawnAnchor(0f, 20f, PlayerTeam.Blue)]);

        var path = graph.FindPath(0, 2, PlayerClass.Heavy, team: PlayerTeam.Blue, carryingIntel: true);

        Assert.NotNull(path);
        Assert.Equal(3, path!.Count);
        Assert.Equal(0, path.GetWaypoint(0));
        Assert.Equal(1, path.GetWaypoint(1));
        Assert.Equal(2, path.GetWaypoint(2));
    }

    [Fact]
    public void WaterwayCarrierReturnPenalizesEnemySpawnAdjacentShortcut()
    {
        var nodes = new[]
        {
            new NavNode(-300f, 0f, NavNodeKind.Surface, 0),
            new NavNode(0f, 0f, NavNodeKind.Surface, 1),
            new NavNode(300f, 0f, NavNodeKind.Surface, 2),
            new NavNode(0f, 160f, NavNodeKind.Surface, 3),
            new NavNode(0f, 20f, NavNodeKind.Spawn, null),
        };
        var adjacency = new List<NavEdge>[nodes.Length];
        for (var i = 0; i < adjacency.Length; i += 1)
        {
            adjacency[i] = [];
        }

        adjacency[0].Add(new NavEdge(1, NavEdgeKind.Walk, 10f));
        adjacency[1].Add(new NavEdge(2, NavEdgeKind.Walk, 10f));
        adjacency[0].Add(new NavEdge(3, NavEdgeKind.Walk, 100f));
        adjacency[3].Add(new NavEdge(2, NavEdgeKind.Walk, 100f));

        var graph = new NavGraph(
            nodes,
            adjacency,
            levelName: "Waterway",
            mode: GameModeKind.CaptureTheFlag,
            spawnAnchors: [new NavSpawnAnchor(0f, 20f, PlayerTeam.Red)]);

        var path = graph.FindPath(0, 2, PlayerClass.Heavy, team: PlayerTeam.Blue, carryingIntel: true);

        Assert.NotNull(path);
        Assert.Equal(3, path!.Count);
        Assert.Equal(0, path.GetWaypoint(0));
        Assert.Equal(3, path.GetWaypoint(1));
        Assert.Equal(2, path.GetWaypoint(2));
    }

    [Theory]
    [MemberData(nameof(EigerAndWaterwayCaptureRouteCases))]
    public void CaptureRoutesRemainAvailableOnEigerAndWaterway(string levelName, PlayerTeam team, PlayerClass playerClass)
    {
        var level = SimpleLevelFactory.CreateImportedLevel(levelName);
        Assert.NotNull(level);
        var graph = LoadShippedNavigationGraph(level);
        var enemyTeam = team == PlayerTeam.Red ? PlayerTeam.Blue : PlayerTeam.Red;
        var ownBase = level!.GetIntelBase(team);
        var enemyBase = level.GetIntelBase(enemyTeam);
        Assert.True(ownBase.HasValue, $"{levelName} missing {team} intel base.");
        Assert.True(enemyBase.HasValue, $"{levelName} missing {enemyTeam} intel base.");

        AssertRouteExists(
            graph,
            ownBase.Value,
            enemyBase.Value,
            playerClass,
            team,
            carryingIntel: false,
            routeLabel: $"{levelName} {team} {playerClass} pickup");
        AssertRouteExists(
            graph,
            enemyBase.Value,
            ownBase.Value,
            playerClass,
            team,
            carryingIntel: true,
            routeLabel: $"{levelName} {team} {playerClass} return");
    }

    public static IEnumerable<object[]> EigerAndWaterwayCaptureRouteCases()
    {
        foreach (var levelName in new[] { "Eiger", "Waterway" })
        {
            foreach (var team in new[] { PlayerTeam.Red, PlayerTeam.Blue })
            {
                foreach (var playerClass in CaptureRouteClasses())
                {
                    yield return [levelName, team, playerClass];
                }
            }
        }
    }

    private static PlayerClass[] CaptureRouteClasses() =>
    [
        PlayerClass.Scout,
        PlayerClass.Engineer,
        PlayerClass.Pyro,
        PlayerClass.Soldier,
        PlayerClass.Demoman,
        PlayerClass.Heavy,
        PlayerClass.Sniper,
        PlayerClass.Medic,
        PlayerClass.Spy,
        PlayerClass.Quote,
    ];

    private static NavGraph LoadShippedNavigationGraph(SimpleLevel level)
    {
        var assetFileName = BotNavigationAssetStore.GetAssetFileName(level.Name, level.MapAreaIndex);
        var assetPath = ProjectSourceLocator.FindFile(Path.Combine("Core", "Content", "BotBrainNav", assetFileName));
        Assert.False(string.IsNullOrWhiteSpace(assetPath), $"Missing shipped bot navigation asset {assetFileName}.");

        using var stream = File.OpenRead(assetPath!);
        var asset = JsonSerializer.Deserialize<BotNavigationAsset>(stream, AssetJsonOptions);
        Assert.NotNull(asset);
        Assert.Equal(BotNavigationAssetStore.CurrentFormatVersion, asset!.FormatVersion);
        Assert.Equal(level.Name, asset.LevelName, ignoreCase: true);
        Assert.Equal(level.MapAreaIndex, asset.MapAreaIndex);

        return BotNavigationAssetBuilder.ToGraph(asset, level);
    }

    private static void AssertRouteExists(
        NavGraph graph,
        IntelBaseMarker start,
        IntelBaseMarker target,
        PlayerClass playerClass,
        PlayerTeam team,
        bool carryingIntel,
        string routeLabel)
    {
        var startNode = graph.FindNearestTraversalStartNode(start.X, start.Y);
        Assert.True(startNode >= 0, $"{routeLabel} has no start node near ({start.X:0.0},{start.Y:0.0}).");

        var goalNode = graph.FindNearestReachableNode(
            target.X,
            target.Y,
            startNode,
            playerClass,
            team: team,
            carryingIntel: carryingIntel,
            verticalWeight: 8f,
            penalizeLowerCandidate: true);
        Assert.True(goalNode >= 0, $"{routeLabel} has no reachable goal node near ({target.X:0.0},{target.Y:0.0}).");

        var goal = graph.GetNode(goalNode);
        var goalDx = goal.X - target.X;
        var goalDy = goal.Y - target.Y;
        var goalDistance = MathF.Sqrt((goalDx * goalDx) + (goalDy * goalDy));
        Assert.True(goalDistance <= 320f, $"{routeLabel} goal proxy is too far from target: {goalDistance:0.0}px.");

        var path = graph.FindPath(startNode, goalNode, playerClass, team: team, carryingIntel: carryingIntel);
        Assert.NotNull(path);
        Assert.True(path!.Count > 1 || startNode == goalNode, $"{routeLabel} produced an empty route.");
    }

    private static JsonSerializerOptions CreateAssetJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
