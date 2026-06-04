using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class BarrierCollisionTests
{
    [Fact]
    public void BlockTargetBlocksMatchingEntities()
    {
        var configuration = new BarrierConfiguration(new BarrierTargetFilters(
            RedPlayers: BarrierTargetFilter.Block,
            BluePlayers: BarrierTargetFilter.Allow,
            RedShots: BarrierTargetFilter.Allow,
            BlueShots: BarrierTargetFilter.Block,
            RedIntel: BarrierTargetFilter.Allow,
            BlueIntel: BarrierTargetFilter.Allow));

        Assert.True(BarrierCollision.MatchesPlayerTarget(configuration.Targets, PlayerTeam.Red, false));
        Assert.False(BarrierCollision.MatchesPlayerTarget(configuration.Targets, PlayerTeam.Blue, false));
        Assert.True(BarrierCollision.BlocksProjectile(configuration, PlayerTeam.Blue));
        Assert.False(BarrierCollision.BlocksProjectile(configuration, PlayerTeam.Red));
    }

    [Fact]
    public void DisplayTeamUsesExclusiveBlockedTier()
    {
        var configuration = new BarrierConfiguration(new BarrierTargetFilters(
            RedPlayers: BarrierTargetFilter.Block,
            BluePlayers: BarrierTargetFilter.Allow,
            RedShots: BarrierTargetFilter.Allow,
            BlueShots: BarrierTargetFilter.Allow,
            RedIntel: BarrierTargetFilter.Allow,
            BlueIntel: BarrierTargetFilter.Allow));

        Assert.Equal(PlayerTeam.Blue, BarrierConfiguration.ResolveDisplayTeam(configuration));
    }

    [Fact]
    public void NeutralDisplayUsesNullTeam()
    {
        Assert.Null(BarrierConfiguration.ResolveDisplayTeam(BarrierConfiguration.Default));
    }

    [Fact]
    public void SolidWallBlocksIntelCarriersForBothTeams()
    {
        var configuration = new BarrierConfiguration(BarrierTargetFilters.SolidWall);
        Assert.True(BarrierCollision.MatchesPlayerTarget(configuration.Targets, PlayerTeam.Red, isCarryingIntel: true));
        Assert.True(BarrierCollision.MatchesPlayerTarget(configuration.Targets, PlayerTeam.Blue, isCarryingIntel: true));
        Assert.True(BarrierCollision.MatchesPlayerTarget(configuration.Targets, PlayerTeam.Red, isCarryingIntel: false));
        Assert.True(BarrierCollision.MatchesPlayerTarget(configuration.Targets, PlayerTeam.Blue, isCarryingIntel: false));
    }

    [Fact]
    public void ModernBarrierImportsAsNativeRoomObject()
    {
        var context = new CustomMapEntityImportContext
        {
            RedSpawns = new List<SpawnPoint>(),
            BlueSpawns = new List<SpawnPoint>(),
            RoomObjects = new List<RoomObjectMarker>(),
        };

        Assert.True(CustomMapEntityRuntimeRegistry.TryImport(
            "barrier",
            5f,
            6f,
            2f,
            1f,
            new Dictionary<string, string>
            {
                [BarrierTargetFilterMetadata.BluePlayersPropertyKey] = BarrierTargetFilterMetadata.BlockValue,
                [BarrierTargetFilterMetadata.BlueShotsPropertyKey] = BarrierTargetFilterMetadata.BlockValue,
            },
            context));

        var marker = Assert.Single(context.RoomObjects);
        Assert.Equal(RoomObjectType.Barrier, marker.Type);
        Assert.True(marker.Barrier.Blocks(BarrierTargetKind.BluePlayers));
        Assert.Equal(PlayerTeam.Red, marker.Team);
        Assert.Equal(12f, marker.Width);
        Assert.Equal(60f, marker.Height);
    }

    [Fact]
    public void BlocksPointForProjectileHonorsShotFilters()
    {
        var marker = BarrierConfiguration.CreateMarker(
            0f,
            0f,
            1f,
            1f,
            new BarrierConfiguration(new BarrierTargetFilters(
                RedPlayers: BarrierTargetFilter.Allow,
                BluePlayers: BarrierTargetFilter.Allow,
                RedShots: BarrierTargetFilter.Block,
                BlueShots: BarrierTargetFilter.Block,
                RedIntel: BarrierTargetFilter.Allow,
                BlueIntel: BarrierTargetFilter.Allow)));
        var level = new SimpleLevel(
            "barrier-shot-test",
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
            [marker],
            0f,
            Array.Empty<LevelSolid>(),
            importedFromSource: false);

        Assert.True(SimpleLevelBarrierCollision.BlocksPointForProjectile(level, PlayerTeam.Red, 3f, 30f));
        Assert.True(SimpleLevelBarrierCollision.BlocksPointForProjectile(level, PlayerTeam.Blue, 3f, 30f));
        Assert.False(SimpleLevelBarrierCollision.BlocksPointForProjectile(level, PlayerTeam.Red, 20f, 3f));
    }

    [Fact]
    public void SubstepRaycastDetectsBarrierOnLongProjectileStep()
    {
        var marker = BarrierConfiguration.CreateMarker(
            0f,
            0f,
            1f,
            1f,
            new BarrierConfiguration(new BarrierTargetFilters(
                RedPlayers: BarrierTargetFilter.Allow,
                BluePlayers: BarrierTargetFilter.Allow,
                RedShots: BarrierTargetFilter.Block,
                BlueShots: BarrierTargetFilter.Block,
                RedIntel: BarrierTargetFilter.Allow,
                BlueIntel: BarrierTargetFilter.Allow)));
        marker = marker with { X = 20f, Y = 27f };

        Assert.True(BarrierProjectileRaycast.TryRaycastMarker(
            marker.Barrier,
            PlayerTeam.Red,
            marker,
            originX: 0f,
            originY: 30f,
            directionX: 1f,
            directionY: 0f,
            maxDistance: 40f,
            out var hitDistance,
            maxStepLength: 6f));

        Assert.InRange(hitDistance, 19f, 21f);
    }

    [Fact]
    public void RaycastIgnoresSegmentWhollyInsideShotAllowBarrier()
    {
        var marker = BarrierConfiguration.CreateMarker(
            0f,
            0f,
            1f,
            1f,
            new BarrierConfiguration(new BarrierTargetFilters(
                RedPlayers: BarrierTargetFilter.Block,
                BluePlayers: BarrierTargetFilter.Block,
                RedShots: BarrierTargetFilter.Allow,
                BlueShots: BarrierTargetFilter.Allow,
                RedIntel: BarrierTargetFilter.Allow,
                BlueIntel: BarrierTargetFilter.Allow)));
        marker = marker with { X = 0f, Y = 0f };

        Assert.False(BarrierProjectileRaycast.TryRaycastMarker(
            marker.Barrier,
            PlayerTeam.Red,
            marker,
            originX: 3f,
            originY: 30f,
            directionX: 1f,
            directionY: 0f,
            maxDistance: 2f,
            out _));
    }

    [Fact]
    public void CreateMarkerEnforcesMinimumExtents()
    {
        var marker = BarrierConfiguration.CreateMarker(0f, 0f, 0.01f, 0.01f, BarrierConfiguration.Default);
        Assert.Equal(BarrierConfiguration.MinExtent, marker.Width);
        Assert.Equal(BarrierConfiguration.MinExtent, marker.Height);
    }
}
