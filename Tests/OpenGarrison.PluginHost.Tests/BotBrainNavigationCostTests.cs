using OpenGarrison.Core;
using OpenGarrison.Core.BotBrain;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class BotBrainNavigationCostTests
{
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
}
