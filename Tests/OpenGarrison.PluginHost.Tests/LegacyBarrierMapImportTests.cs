using System.Collections.ObjectModel;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class LegacyBarrierMapImportTests
{
    [Theory]
    [InlineData("redteamgate", RoomObjectType.TeamGate, PlayerTeam.Red)]
    [InlineData("blueteamgate", RoomObjectType.TeamGate, PlayerTeam.Blue)]
    [InlineData("redteamgate2", RoomObjectType.TeamGate, PlayerTeam.Red)]
    [InlineData("blueteamgate2", RoomObjectType.TeamGate, PlayerTeam.Blue)]
    [InlineData("playerwall", RoomObjectType.PlayerWall, null)]
    [InlineData("playerwall_horizontal", RoomObjectType.PlayerWall, null)]
    [InlineData("bulletwall", RoomObjectType.BulletWall, null)]
    [InlineData("bulletwall_horizontal", RoomObjectType.BulletWall, null)]
    [InlineData("leftdoor", RoomObjectType.PlayerWall, null)]
    [InlineData("rightdoor", RoomObjectType.PlayerWall, null)]
    [InlineData("redintelgate", RoomObjectType.IntelGate, PlayerTeam.Red)]
    [InlineData("blueintelgate", RoomObjectType.IntelGate, PlayerTeam.Blue)]
    [InlineData("intelgatevertical", RoomObjectType.IntelGate, null)]
    public void LegacyEntityTypesImportThroughRoomObjectFactory(string entityType, RoomObjectType expectedType, PlayerTeam? expectedTeam)
    {
        Assert.True(CustomMapRoomObjectFactory.TryCreate(entityType, 10f, 20f, 1f, 1f, new Dictionary<string, string>(), out var marker));
        Assert.Equal(expectedType, marker.Type);
        Assert.Equal(entityType, marker.SourceName);
        Assert.Equal(expectedTeam, marker.Team);
    }

    [Fact]
    public void LegacyEntityTypesImportThroughPngPipeline()
    {
        var context = new CustomMapEntityImportContext
        {
            RedSpawns = new List<SpawnPoint>(),
            BlueSpawns = new List<SpawnPoint>(),
            RoomObjects = new List<RoomObjectMarker>(),
        };

        Assert.False(CustomMapEntityRuntimeRegistry.TryImport(
            "redteamgate",
            4f,
            8f,
            2f,
            1f,
            new Dictionary<string, string>(),
            context));

        Assert.True(CustomMapRoomObjectFactory.TryCreate(
            "redteamgate",
            4f,
            8f,
            2f,
            1f,
            new Dictionary<string, string>(),
            out var marker));

        context.RoomObjects.Add(marker);
        var gate = Assert.Single(context.RoomObjects);
        Assert.Equal(RoomObjectType.TeamGate, gate.Type);
        Assert.Equal(PlayerTeam.Red, gate.Team);
    }

    [Fact]
    public void RedTeamGateBlocksBluePlayersUsingLegacyCollisionPath()
    {
        Assert.True(CustomMapRoomObjectFactory.TryCreate("redteamgate", 0f, 0f, 1f, 1f, new Dictionary<string, string>(), out var gate));
        var level = CreateLevel(gate);
        var blockingForBlue = level.GetBlockingTeamGates(PlayerTeam.Blue, carryingIntel: false);
        Assert.Contains(blockingForBlue, candidate => candidate.SourceName == "redteamgate");
        var blockingForRed = level.GetBlockingTeamGates(PlayerTeam.Red, carryingIntel: false);
        Assert.DoesNotContain(blockingForRed, candidate => candidate.SourceName == "redteamgate");
    }

    [Fact]
    public void PlayerWallBlocksAllPlayersUsingLegacyCollisionPath()
    {
        Assert.True(CustomMapRoomObjectFactory.TryCreate("playerwall", 0f, 0f, 1f, 1f, new Dictionary<string, string>(), out var wall));
        var level = CreateLevel(wall);
        Assert.Single(level.GetRoomObjects(RoomObjectType.PlayerWall));
    }

    [Fact]
    public void CenterOriginImportPlacesHorizontalWallAroundStoredCenter()
    {
        const float centerX = 120f;
        const float centerY = 48f;

        Assert.True(CustomMapRoomObjectFactory.TryCreate(
            "playerwall_horizontal",
            centerX,
            centerY,
            1f,
            1f,
            new Dictionary<string, string>(),
            out var wall,
            useCenterOrigin: true));

        Assert.Equal(centerX - 30f, wall.X);
        Assert.Equal(centerY - 3f, wall.Y);
        Assert.Equal(60f, wall.Width);
        Assert.Equal(6f, wall.Height);
    }

    [Fact]
    public void PlayerWallHorizontalImportsAsSixtyBySixCollisionBox()
    {
        Assert.True(CustomMapRoomObjectFactory.TryCreate("playerwall_horizontal", 0f, 0f, 1f, 1f, new Dictionary<string, string>(), out var wall));
        Assert.Equal(60f, wall.Width);
        Assert.Equal(6f, wall.Height);
    }

    [Fact]
    public void BarrierImportWithFloorAxisUsesHorizontalCollisionDimensions()
    {
        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["axis"] = "floor",
            [BarrierTargetFilterMetadata.RedPlayersPropertyKey] = BarrierTargetFilterMetadata.BlockValue,
            [BarrierTargetFilterMetadata.BluePlayersPropertyKey] = BarrierTargetFilterMetadata.BlockValue,
        };

        var context = new CustomMapEntityImportContext
        {
            RedSpawns = new List<SpawnPoint>(),
            BlueSpawns = new List<SpawnPoint>(),
            RoomObjects = new List<RoomObjectMarker>(),
            UseCenterOrigin = false,
        };

        Assert.True(CustomMapEntityRuntimeRegistry.TryImport("barrier", 50f, 80f, 1f, 1f, properties, context));

        var barrier = Assert.Single(context.RoomObjects);
        Assert.Equal(RoomObjectType.Barrier, barrier.Type);
        Assert.Equal(60f, barrier.Width);
        Assert.Equal(6f, barrier.Height);
        Assert.Equal(50f, barrier.X);
        Assert.Equal(80f, barrier.Y);
    }

    [Fact]
    public void ResolveForExportConvertsFloorBarrierBackToPlayerWallHorizontal()
    {
        var normalized = CustomMapBuilderEntityNormalization.NormalizeEntityForEditor(
            CustomMapBuilderEntity.Create("playerwall_horizontal", 10f, 20f));

        var exported = CustomMapBuilderEntityNormalization.ResolveEntityForExport(normalized);

        Assert.Equal("playerwall_horizontal", exported.Type);
        Assert.True(CustomMapRoomObjectFactory.TryCreate(
            exported.Type,
            exported.X,
            exported.Y,
            exported.XScale,
            exported.YScale,
            exported.Properties,
            out var marker,
            useCenterOrigin: false));
        Assert.Equal(60f, marker.Width);
        Assert.Equal(6f, marker.Height);
        Assert.Equal(RoomObjectType.PlayerWall, marker.Type);
        Assert.Equal(exported.X, marker.X);
        Assert.Equal(exported.Y, marker.Y);
    }

    [Fact]
    public void PlayerWallHorizontalNormalizedToBarrierUsesFloorCollisionDimensions()
    {
        var normalized = CustomMapBuilderEntityNormalization.NormalizeEntityForEditor(
            CustomMapBuilderEntity.Create("playerwall_horizontal", 50f, 80f, xScale: 1f, yScale: 1f));

        Assert.Equal("barrier", normalized.Type);
        Assert.Equal("floor", normalized.Properties["axis"]);

        var context = new CustomMapEntityImportContext
        {
            RedSpawns = new List<SpawnPoint>(),
            BlueSpawns = new List<SpawnPoint>(),
            RoomObjects = new List<RoomObjectMarker>(),
            UseCenterOrigin = false,
        };

        Assert.True(CustomMapEntityRuntimeRegistry.TryImport(
            normalized.Type,
            normalized.X,
            normalized.Y,
            normalized.XScale,
            normalized.YScale,
            normalized.Properties,
            context));

        var barrier = Assert.Single(context.RoomObjects);
        Assert.Equal(RoomObjectType.Barrier, barrier.Type);
        Assert.Equal(60f, barrier.Width);
        Assert.Equal(6f, barrier.Height);
        Assert.Equal(50f, barrier.X);
        Assert.Equal(80f, barrier.Y);
    }

    [Fact]
    public void BarrierEntityWithLegacyPropertyBagMatchesRedTeamGateSemantics()
    {
        var context = new CustomMapEntityImportContext
        {
            RedSpawns = new List<SpawnPoint>(),
            BlueSpawns = new List<SpawnPoint>(),
            RoomObjects = new List<RoomObjectMarker>(),
        };

        Assert.True(CustomMapEntityRuntimeRegistry.TryImport(
            "barrier",
            0f,
            0f,
            1f,
            1f,
            new Dictionary<string, string>
            {
                ["team"] = "red",
                ["orientation"] = "wall",
                ["blockPlayers"] = "true",
                ["blockBullets"] = "true",
            },
            context));

        var barrier = Assert.Single(context.RoomObjects);
        Assert.Equal(RoomObjectType.Barrier, barrier.Type);
        Assert.True(barrier.Barrier.Blocks(BarrierTargetKind.BluePlayers));
        Assert.True(barrier.Barrier.Blocks(BarrierTargetKind.BlueShots));
        Assert.False(barrier.Barrier.Blocks(BarrierTargetKind.RedPlayers));

        var level = CreateLevel(barrier);
        Assert.True(SimpleLevelBarrierCollision.BlocksPointForPlayer(level, PlayerTeam.Blue, isCarryingIntel: false, 3f, 30f));
        Assert.False(SimpleLevelBarrierCollision.BlocksPointForPlayer(level, PlayerTeam.Red, isCarryingIntel: false, 3f, 30f));
    }

    [Fact]
    public void LegacyBarrierNormalizationMatchesRuntimeMigration()
    {
        var normalized = CustomMapBuilderEntityNormalization.NormalizeEntityForEditor(
            CustomMapBuilderEntity.Create(
                "redteamgate",
                1f,
                2f,
                new Dictionary<string, string> { ["xscale"] = "2" },
                2f,
                1f));

        var fromEditor = BarrierConfiguration.FromProperties(normalized.Properties);
        var fromLegacyBag = BarrierConfiguration.FromProperties(new Dictionary<string, string>
        {
            ["team"] = "red",
            ["blockPlayers"] = "true",
            ["blockBullets"] = "true",
        });

        Assert.True(fromEditor.Blocks(BarrierTargetKind.BluePlayers));
        Assert.True(fromLegacyBag.Blocks(BarrierTargetKind.BluePlayers));
        Assert.True(fromEditor.Blocks(BarrierTargetKind.BlueShots));
        Assert.True(fromLegacyBag.Blocks(BarrierTargetKind.BlueShots));
    }

    private static SimpleLevel CreateLevel(RoomObjectMarker roomObject)
    {
        return new SimpleLevel(
            "legacy-barrier-test",
            GameModeKind.CaptureTheFlag,
            new WorldBounds(128f, 128f),
            1f,
            null,
            0,
            1,
            new SpawnPoint(0f, 0f),
            Array.Empty<SpawnPoint>(),
            Array.Empty<SpawnPoint>(),
            Array.Empty<IntelBaseMarker>(),
            [roomObject],
            0f,
            Array.Empty<LevelSolid>(),
            importedFromSource: false);
    }
}
