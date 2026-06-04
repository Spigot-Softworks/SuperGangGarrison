using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class ForwardSpawnControlPointLinkTests
{
    [Fact]
    public void BlueForwardSpawnSlotMapsToDescendingControlPointIndex()
    {
        Assert.Equal(5, ForwardSpawnMetadata.ResolveForwardSpawnControlPointIndex(PlayerTeam.Blue, 1, 5));
        Assert.Equal(4, ForwardSpawnMetadata.ResolveForwardSpawnControlPointIndex(PlayerTeam.Blue, 2, 5));
        Assert.Equal(3, ForwardSpawnMetadata.ResolveForwardSpawnControlPointIndex(PlayerTeam.Blue, 3, 5));
        Assert.Equal(1, ForwardSpawnMetadata.ResolveForwardSpawnControlPointIndex(PlayerTeam.Blue, 3, 3));
    }

    [Fact]
    public void RedForwardSpawnSlotMapsToAscendingControlPointIndex()
    {
        Assert.Equal(1, ForwardSpawnMetadata.ResolveForwardSpawnControlPointIndex(PlayerTeam.Red, 1, 5));
        Assert.Equal(3, ForwardSpawnMetadata.ResolveForwardSpawnControlPointIndex(PlayerTeam.Red, 3, 5));
    }

    [Fact]
    public void ApplyForwardSpawnControlPointLinksRemapsBlueForwardSpawns()
    {
        var redSpawns = new List<SpawnPoint>
        {
            new(0f, 0f),
            new(10f, 10f, SpawnPointRole.Forward, LinkedControlPointIndex: 2, Priority: 2),
        };
        var blueSpawns = new List<SpawnPoint>
        {
            new(20f, 20f, SpawnPointRole.Forward, LinkedControlPointIndex: 1, Priority: 1),
            new(30f, 30f, SpawnPointRole.Forward, LinkedControlPointIndex: 2, Priority: 2),
        };

        ForwardSpawnMetadata.ApplyForwardSpawnControlPointLinks(redSpawns, blueSpawns, totalControlPoints: 4);

        Assert.Equal(2, redSpawns[1].LinkedControlPointIndex);
        Assert.Equal(4, blueSpawns[0].LinkedControlPointIndex);
        Assert.Equal(3, blueSpawns[1].LinkedControlPointIndex);
    }
}
