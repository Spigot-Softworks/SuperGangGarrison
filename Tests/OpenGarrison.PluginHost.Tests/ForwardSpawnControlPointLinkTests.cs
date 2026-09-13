using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class ForwardSpawnControlPointLinkTests
{
    [Theory]
    [InlineData(2, false)]
    [InlineData(5, false)]
    [InlineData(5, true)]
    public void LegacySlotsAreModeAwareAndExplicitEditorLinksRemainUntouched(int points, bool attackDefense)
    {
        var red = new List<SpawnPoint>();
        var blue = new List<SpawnPoint>();
        for (var slot = 1; slot <= points; slot++)
        {
            Assert.True(LegacyTeamSpawnRuntimeImport.TryCreateSpawnPoint($"redspawn{slot}", slot * 100, 100, out var r));
            Assert.True(LegacyTeamSpawnRuntimeImport.TryCreateSpawnPoint($"bluespawn{slot}", 1500 - slot * 100, 100, out var b));
            red.Add(r.SpawnPoint);
            blue.Add(b.SpawnPoint);
        }
        var explicitSpawn = new SpawnPoint(700, 100, SpawnPointRole.Forward, 1, ForwardSpawnUseCondition.ObjectiveNeutral, 4);
        blue.Add(explicitSpawn);
        ForwardSpawnMetadata.ApplyForwardSpawnControlPointLinks(red, blue, points, attackDefense);
        ForwardSpawnMetadata.ApplyForwardSpawnControlPointLinks(red, blue, points, attackDefense); // idempotent
        for (var index = 0; index < points; index++)
        {
            Assert.Equal(index + 1, red[index].LinkedControlPointIndex);
            Assert.Equal(attackDefense ? index + 1 : points - index, blue[index].LinkedControlPointIndex);
            Assert.Equal(index + 1, blue[index].Priority);
        }
        Assert.Equal(explicitSpawn, blue[^1]);
    }

    [Fact]
    public void BothTeamsUseSymmetricProgressionAcrossOwnershipChangesAndShuffledMarkers()
    {
        foreach (var reverse in new[] { false, true })
        {
            var red = new List<SpawnPoint>();
            var blue = new List<SpawnPoint>();
            for (var slot = 1; slot <= 2; slot++)
            {
                LegacyTeamSpawnRuntimeImport.TryCreateSpawnPoint($"redspawn{slot}", slot * 100, 100, out var r);
                LegacyTeamSpawnRuntimeImport.TryCreateSpawnPoint($"bluespawn{slot}", 1500 - slot * 100, 100, out var b);
                red.Add(r.SpawnPoint);
                blue.Add(b.SpawnPoint);
            }
            if (reverse) { red.Reverse(); blue.Reverse(); }
            ForwardSpawnMetadata.ApplyForwardSpawnControlPointLinks(red, blue, 2);
            var world = new SimulationWorld(new SimulationConfig { EnableEnemyTrainingDummy = false, EnableFriendlySupportDummy = false });
            world.CombatTestSetLevel(new SimpleLevel("spawn_matrix", GameModeKind.ControlPoint, new WorldBounds(1600, 600),
                1, null, 1, 1, red[0], red, blue, [],
                [new(RoomObjectType.ControlPoint, 600, 200, 48, 24, "ControlPointNeutralS", SourceName: "ControlPoint1"),
                 new(RoomObjectType.ControlPoint, 900, 200, 48, 24, "ControlPointNeutralS", SourceName: "ControlPoint2")],
                550, [new LevelSolid(0, 550, 1600, 50)], false));
            foreach (var first in new PlayerTeam?[] { PlayerTeam.Red, PlayerTeam.Blue, null, PlayerTeam.Red })
            foreach (var second in new PlayerTeam?[] { PlayerTeam.Red, PlayerTeam.Blue, null, PlayerTeam.Blue })
            {
                world.CombatTestSetControlPointOwner(1, first);
                world.CombatTestSetControlPointOwner(2, second);
                var r = Assert.Single(world.CombatTestGetTeamSpawnSelectionPool(PlayerTeam.Red));
                var b = Assert.Single(world.CombatTestGetTeamSpawnSelectionPool(PlayerTeam.Blue));
                Assert.Equal(second == PlayerTeam.Red ? 2 : 1, r.LegacySpawnSlot);
                Assert.Equal(first == PlayerTeam.Blue ? 2 : 1, b.LegacySpawnSlot);
            }
        }
    }

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
            new(10f, 10f, SpawnPointRole.Forward, LinkedControlPointIndex: 2, Priority: 2, LegacySpawnSlot: 2),
        };
        var blueSpawns = new List<SpawnPoint>
        {
            new(20f, 20f, SpawnPointRole.Forward, LinkedControlPointIndex: 1, Priority: 1, LegacySpawnSlot: 1),
            new(30f, 30f, SpawnPointRole.Forward, LinkedControlPointIndex: 2, Priority: 2, LegacySpawnSlot: 2),
        };

        ForwardSpawnMetadata.ApplyForwardSpawnControlPointLinks(redSpawns, blueSpawns, totalControlPoints: 4);

        Assert.Equal(2, redSpawns[1].LinkedControlPointIndex);
        Assert.Equal(4, blueSpawns[0].LinkedControlPointIndex);
        Assert.Equal(3, blueSpawns[1].LinkedControlPointIndex);
    }
}
