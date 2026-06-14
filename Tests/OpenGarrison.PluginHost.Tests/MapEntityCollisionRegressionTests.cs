using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class MapEntityCollisionRegressionTests
{
    [Fact]
    public void SpawnAndControlPointImportWithoutBlockingRoomObjects()
    {
        var context = new CustomMapEntityImportContext
        {
            RedSpawns = new List<SpawnPoint>(),
            BlueSpawns = new List<SpawnPoint>(),
            RoomObjects = new List<RoomObjectMarker>(),
            UseCenterOrigin = false,
        };

        Assert.True(CustomMapEntityRuntimeRegistry.TryImport(
            "spawn",
            20f,
            24f,
            1f,
            1f,
            new Dictionary<string, string> { ["team"] = "red", ["forward"] = "false" },
            context));
        Assert.True(CustomMapEntityRuntimeRegistry.TryImport(
            "controlPoint",
            40f,
            44f,
            1f,
            1f,
            new Dictionary<string, string> { ["index"] = "1" },
            context));

        Assert.DoesNotContain(context.RoomObjects, marker => marker.Type is RoomObjectType.Barrier or RoomObjectType.DirectionalWall or RoomObjectType.PlayerWall);
        Assert.Contains(context.RoomObjects, marker => marker.Type == RoomObjectType.ControlPoint);
        Assert.Single(context.RedSpawns);
    }

    [Fact]
    public void FullyAllowBarrierImportsAsSolidWallForCollision()
    {
        var context = new CustomMapEntityImportContext
        {
            RedSpawns = new List<SpawnPoint>(),
            BlueSpawns = new List<SpawnPoint>(),
            RoomObjects = new List<RoomObjectMarker>(),
            UseCenterOrigin = false,
        };

        Assert.True(CustomMapEntityRuntimeRegistry.TryImport(
            "barrier",
            30f,
            30f,
            1f,
            1f,
            BarrierTargetFilters.Default.ToProperties(),
            context));

        var barrier = Assert.Single(context.RoomObjects);
        Assert.True(barrier.Barrier.Blocks(BarrierTargetKind.RedPlayers));
        Assert.True(barrier.Barrier.Blocks(BarrierTargetKind.BlueIntel));
    }

    [Fact]
    public void IgnoredDirectionalWallDoesNotCreateRoomObject()
    {
        var context = new CustomMapEntityImportContext
        {
            RedSpawns = new List<SpawnPoint>(),
            BlueSpawns = new List<SpawnPoint>(),
            RoomObjects = new List<RoomObjectMarker>(),
            UseCenterOrigin = false,
        };

        Assert.True(CustomMapEntityRuntimeRegistry.TryImport(
            "directionalWall",
            12f,
            18f,
            1f,
            1f,
            new Dictionary<string, string>
            {
                [DirectionalWallConfiguration.PassDirectionPropertyKey] = DirectionalWallConfiguration.PassDirectionRightValue,
                [DirectionalWallConfiguration.PlayersPropertyKey] = DirectionalWallConfiguration.IgnoreValue,
                [DirectionalWallConfiguration.ProjectilesPropertyKey] = DirectionalWallConfiguration.IgnoreValue,
            },
            context));

        Assert.Empty(context.RoomObjects);
    }

    [Fact]
    public void UpDirectionalWallImportsAsFloorShape()
    {
        var context = new CustomMapEntityImportContext
        {
            RedSpawns = new List<SpawnPoint>(),
            BlueSpawns = new List<SpawnPoint>(),
            RoomObjects = new List<RoomObjectMarker>(),
            UseCenterOrigin = false,
        };

        Assert.True(CustomMapEntityRuntimeRegistry.TryImport(
            "directionalWall",
            12f,
            18f,
            1f,
            1f,
            new Dictionary<string, string>
            {
                [DirectionalWallConfiguration.PassDirectionPropertyKey] = DirectionalWallConfiguration.PassDirectionUpValue,
                [DirectionalWallConfiguration.PlayersPropertyKey] = DirectionalWallConfiguration.AffectValue,
                [DirectionalWallConfiguration.ProjectilesPropertyKey] = DirectionalWallConfiguration.IgnoreValue,
            },
            context));

        var wall = Assert.Single(context.RoomObjects);
        Assert.Equal(RoomObjectType.DirectionalWall, wall.Type);
        Assert.Equal(60f, wall.Width);
        Assert.Equal(6f, wall.Height);
    }

    [Fact]
    public void CatalogDefaultBarrierBlocksPlayersAndIntelCarriers()
    {
        Assert.True(CustomMapBuilderEntityCatalog.TryGetDefinition("barrier", out var definition));
        var entity = definition.CreateEntity(20f, 30f);

        var context = new CustomMapEntityImportContext
        {
            RedSpawns = new List<SpawnPoint>(),
            BlueSpawns = new List<SpawnPoint>(),
            RoomObjects = new List<RoomObjectMarker>(),
            UseCenterOrigin = false,
        };

        Assert.True(CustomMapEntityRuntimeRegistry.TryImport(
            entity.Type,
            entity.X,
            entity.Y,
            entity.XScale,
            entity.YScale,
            entity.Properties,
            context));

        var barrier = Assert.Single(context.RoomObjects);
        Assert.True(barrier.Barrier.Blocks(BarrierTargetKind.RedPlayers));
        Assert.True(barrier.Barrier.Blocks(BarrierTargetKind.BluePlayers));
        Assert.True(barrier.Barrier.Blocks(BarrierTargetKind.RedIntel));
        Assert.True(barrier.Barrier.Blocks(BarrierTargetKind.BlueIntel));

        var level = new SimpleLevel(
            "catalog-barrier-test",
            GameModeKind.CaptureTheFlag,
            new WorldBounds(256f, 256f),
            1f,
            null,
            0,
            1,
            new SpawnPoint(0f, 0f),
            Array.Empty<SpawnPoint>(),
            Array.Empty<SpawnPoint>(),
            Array.Empty<IntelBaseMarker>(),
            [barrier],
            0f,
            Array.Empty<LevelSolid>(),
            importedFromSource: false);

        const float insideX = 23f;
        const float insideY = 55f;
        Assert.True(SimpleLevelBarrierCollision.BlocksPointForPlayer(level, PlayerTeam.Red, isCarryingIntel: false, insideX, insideY));
        Assert.True(SimpleLevelBarrierCollision.BlocksPointForPlayer(level, PlayerTeam.Blue, isCarryingIntel: false, insideX, insideY));
        Assert.True(SimpleLevelBarrierCollision.BlocksPointForPlayer(level, PlayerTeam.Red, isCarryingIntel: true, insideX, insideY));
        Assert.True(SimpleLevelBarrierCollision.BlocksPointForPlayer(level, PlayerTeam.Blue, isCarryingIntel: true, insideX, insideY));
        Assert.False(SimpleLevelBarrierCollision.BlocksPointForProjectile(level, PlayerTeam.Red, insideX, insideY));
        Assert.False(SimpleLevelBarrierCollision.BlocksPointForProjectile(level, PlayerTeam.Blue, insideX, insideY));
    }

    [Fact]
    public void ModernBarrierCollisionMatchesBuilderTopLeftPlacement()
    {
        var context = new CustomMapEntityImportContext
        {
            RedSpawns = new List<SpawnPoint>(),
            BlueSpawns = new List<SpawnPoint>(),
            RoomObjects = new List<RoomObjectMarker>(),
            UseCenterOrigin = false,
        };

        Assert.True(CustomMapEntityRuntimeRegistry.TryImport(
            "barrier",
            50f,
            80f,
            1f,
            1f,
            new Dictionary<string, string>
            {
                [BarrierTargetFilterMetadata.RedPlayersPropertyKey] = BarrierTargetFilterMetadata.BlockValue,
                [BarrierTargetFilterMetadata.BluePlayersPropertyKey] = BarrierTargetFilterMetadata.BlockValue,
            },
            context));

        var barrier = Assert.Single(context.RoomObjects);
        Assert.Equal(6f, barrier.Width);
        Assert.Equal(60f, barrier.Height);
        Assert.Equal(50f, barrier.X);
        Assert.Equal(80f, barrier.Y);
        Assert.Equal(56f, barrier.Right);
        Assert.Equal(140f, barrier.Bottom);
    }
}
