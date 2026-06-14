using System.Reflection;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class TeleportRuntimeTests
{
    [Fact]
    public void TeleportMovesPlayerOncePerZoneEntry()
    {
        var exit = CreateTeleportExit(180f, 180f);
        var zone = CreateTeleportZone(0f, 0f, 42f, 42f, new TeleportZoneConfiguration(
            TeleportTeamFilter.All,
            exit.CenterX,
            exit.CenterY,
            HasExit: true));
        var world = CreateWorld([zone, exit]);
        PreparePlayer(world, PlayerTeam.Red, 10f, 10f);

        InvokeApplyTeleportZones(world, world.LocalPlayer);
        Assert.Equal(exit.CenterX, world.LocalPlayer.X, precision: 2);
        Assert.Equal(exit.CenterY, world.LocalPlayer.Y, precision: 2);

        var afterStayingInside = world.LocalPlayer.X;
        InvokeApplyTeleportZones(world, world.LocalPlayer);
        Assert.Equal(afterStayingInside, world.LocalPlayer.X, precision: 2);
    }

    [Fact]
    public void TeleportTriggersOnHitboxOverlapWhenOriginIsOutsideZone()
    {
        var exit = CreateTeleportExit(180f, 180f);
        var zone = CreateTeleportZone(0f, 0f, 42f, 42f, new TeleportZoneConfiguration(
            TeleportTeamFilter.All,
            exit.CenterX,
            exit.CenterY,
            HasExit: true));
        var world = CreateWorld([zone, exit]);
        PreparePlayer(world, PlayerTeam.Red, 20f, -5f);

        Assert.False(AreaExtensionMetadata.IsPointInsideMarker(world.LocalPlayer.X, world.LocalPlayer.Y, zone));

        InvokeApplyTeleportZones(world, world.LocalPlayer);

        Assert.Equal(exit.CenterX, world.LocalPlayer.X, precision: 2);
        Assert.Equal(exit.CenterY, world.LocalPlayer.Y, precision: 2);
    }

    [Fact]
    public void TeleportWithoutExitDoesNotMovePlayer()
    {
        var zone = CreateTeleportZone(0f, 0f, 42f, 42f, new TeleportZoneConfiguration(
            TeleportTeamFilter.All,
            0f,
            0f,
            HasExit: false));
        var world = CreateWorld([zone]);
        PreparePlayer(world, PlayerTeam.Red, 10f, 10f);

        InvokeApplyTeleportZones(world, world.LocalPlayer);

        Assert.Equal(10f, world.LocalPlayer.X, precision: 2);
        Assert.Equal(10f, world.LocalPlayer.Y, precision: 2);
    }

    [Fact]
    public void TeleportRespectsTeamFilter()
    {
        var exit = CreateTeleportExit(180f, 180f);
        var zone = CreateTeleportZone(0f, 0f, 42f, 42f, new TeleportZoneConfiguration(
            TeleportTeamFilter.Red,
            exit.CenterX,
            exit.CenterY,
            HasExit: true));
        var world = CreateWorld([zone, exit]);
        PreparePlayer(world, PlayerTeam.Blue, 10f, 10f);

        InvokeApplyTeleportZones(world, world.LocalPlayer);

        Assert.Equal(10f, world.LocalPlayer.X, precision: 2);
        Assert.Equal(10f, world.LocalPlayer.Y, precision: 2);
    }

    [Fact]
    public void DisabledTeleportZoneDoesNotMovePlayer()
    {
        var exit = CreateTeleportExit(180f, 180f);
        var zone = CreateTeleportZone(0f, 0f, 42f, 42f, new TeleportZoneConfiguration(
            TeleportTeamFilter.All,
            exit.CenterX,
            exit.CenterY,
            HasExit: true));
        var world = CreateWorld([zone, exit]);
        world.Level.RoomObjectLogicActiveMask[0] = false;
        PreparePlayer(world, PlayerTeam.Red, 10f, 10f);

        InvokeApplyTeleportZones(world, world.LocalPlayer);

        Assert.Equal(10f, world.LocalPlayer.X, precision: 2);
        Assert.Equal(10f, world.LocalPlayer.Y, precision: 2);
    }

    [Fact]
    public void ActivatorCanDisableTeleportZone()
    {
        var exit = CreateTeleportExit(180f, 180f);
        var zone = CreateTeleportZone(0f, 0f, 42f, 42f, new TeleportZoneConfiguration(
            TeleportTeamFilter.All,
            exit.CenterX,
            exit.CenterY,
            HasExit: true));
        var world = CreateWorld([zone, exit]);
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "trigger",
                Kind = MapLogicNodeKind.CpTrigger,
                LinkedControlPointIndex = 1,
                OwnerRequirement = MapLogicCpTriggerOwnerRequirement.Red,
            },
        ]);
        var activators = new MapLogicActivatorSet(
        [
            new MapLogicActivator(graph.NodeIndexByKey["trigger"], 0, MapLogicActivatorBehavior.Disable, false),
        ]);
        var startApplied = new bool[activators.Activators.Count];
        var points = new[]
        {
            new ControlPointState(1, new RoomObjectMarker(RoomObjectType.ControlPoint, 0f, 0f, 32f, 32f, "ControlPointRedS", PlayerTeam.Red, "controlPoint1"))
            {
                Team = PlayerTeam.Red,
            },
        };
        graph.Evaluate(points);
        MapLogicActivatorRuntime.Apply(graph, activators, world.Level.RoomObjectLogicActiveMask, startApplied);

        PreparePlayer(world, PlayerTeam.Red, 10f, 10f);
        InvokeApplyTeleportZones(world, world.LocalPlayer);

        Assert.Equal(10f, world.LocalPlayer.X, precision: 2);
        Assert.Equal(10f, world.LocalPlayer.Y, precision: 2);
    }

    [Fact]
    public void ModernTeleportEntitiesImportZoneAndExit()
    {
        var roomObjects = new List<RoomObjectMarker>();
        var context = new CustomMapEntityImportContext
        {
            RedSpawns = [],
            BlueSpawns = [],
            RoomObjects = roomObjects,
            UseCenterOrigin = false,
        };

        Assert.True(CustomMapEntityRuntimeRegistry.TryImport(
            TeleportMetadata.TeleportExitEntityType,
            100f,
            120f,
            1f,
            1f,
            new Dictionary<string, string>(),
            context));
        Assert.True(CustomMapEntityRuntimeRegistry.TryImport(
            TeleportMetadata.TeleportEntityType,
            20f,
            30f,
            2f,
            1f,
            new Dictionary<string, string>
            {
                [TeleportMetadata.TeamPropertyKey] = "blue",
                [TeleportMetadata.TeleportExitPropertyKey] = MapLogicEntityReference.FormatEntityRef(
                    TeleportMetadata.TeleportExitEntityType,
                    100f,
                    120f),
            },
            context));

        MapTeleportRuntimePatch.ApplyExitLinks(roomObjects, []);
        var zone = Assert.Single(roomObjects, marker => marker.Type == RoomObjectType.TeleportZone);
        var exit = Assert.Single(roomObjects, marker => marker.Type == RoomObjectType.TeleportExit);
        Assert.Equal(TeleportTeamFilter.Blue, zone.TeleportZone.TeamFilter);
        Assert.True(zone.TeleportZone.HasExit);
        Assert.Equal(exit.CenterX, zone.TeleportZone.ExitX);
        Assert.Equal(exit.CenterY, zone.TeleportZone.ExitY);
        Assert.Equal(84f, zone.Width);
        Assert.Equal(42f, zone.Height);
    }

    [Fact]
    public void ModernTeleportStableIdReferenceResolvesExitInsteadOfOrigin()
    {
        var roomObjects = new List<RoomObjectMarker>();
        var context = new CustomMapEntityImportContext
        {
            RedSpawns = [],
            BlueSpawns = [],
            RoomObjects = roomObjects,
            UseCenterOrigin = false,
        };
        var exitProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [MapLogicMetadata.MapEntityIdPropertyKey] = "exit01",
        };
        var zoneProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [TeleportMetadata.TeleportExitPropertyKey] = MapLogicEntityReference.FormatEntityRef(
                TeleportMetadata.TeleportExitEntityType,
                "exit01"),
        };

        Assert.True(CustomMapEntityRuntimeRegistry.TryImport(
            TeleportMetadata.TeleportEntityType,
            20f,
            30f,
            1f,
            1f,
            zoneProperties,
            context));
        Assert.True(CustomMapEntityRuntimeRegistry.TryImport(
            TeleportMetadata.TeleportExitEntityType,
            100f,
            120f,
            1f,
            1f,
            exitProperties,
            context));

        MapTeleportRuntimePatch.ApplyExitLinks(
            roomObjects,
            [
                new MapImportedEntity(TeleportMetadata.TeleportEntityType, 20f, 30f, zoneProperties),
                new MapImportedEntity(TeleportMetadata.TeleportExitEntityType, 100f, 120f, exitProperties),
            ]);

        var zone = Assert.Single(roomObjects, marker => marker.Type == RoomObjectType.TeleportZone);
        var exit = Assert.Single(roomObjects, marker => marker.Type == RoomObjectType.TeleportExit);
        Assert.True(zone.TeleportZone.HasExit);
        Assert.Equal(exit.CenterX, zone.TeleportZone.ExitX);
        Assert.Equal(exit.CenterY, zone.TeleportZone.ExitY);
        Assert.NotEqual(0f, zone.TeleportZone.ExitX);
        Assert.NotEqual(0f, zone.TeleportZone.ExitY);
    }

    private static RoomObjectMarker CreateTeleportZone(
        float left,
        float top,
        float width,
        float height,
        TeleportZoneConfiguration configuration)
    {
        return new RoomObjectMarker(
            RoomObjectType.TeleportZone,
            left,
            top,
            width,
            height,
            "sprite64",
            SourceName: TeleportMetadata.TeleportEntityType,
            TeleportZone: configuration);
    }

    private static RoomObjectMarker CreateTeleportExit(float left, float top)
    {
        return new RoomObjectMarker(
            RoomObjectType.TeleportExit,
            left,
            top,
            TeleportMetadata.ExitMarkerSize,
            TeleportMetadata.ExitMarkerSize,
            "spawnS",
            SourceName: TeleportMetadata.TeleportExitEntityType);
    }

    private static SimulationWorld CreateWorld(IReadOnlyList<RoomObjectMarker> roomObjects)
    {
        var world = new SimulationWorld();
        var setLevel = typeof(SimulationWorld).GetMethod(
            "CombatTestSetLevel",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(setLevel);
        setLevel.Invoke(
            world,
            [
                new SimpleLevel(
                    "teleport-runtime-test",
                    GameModeKind.TeamDeathmatch,
                    new WorldBounds(512f, 512f),
                    1f,
                    null,
                    0,
                    1,
                    new SpawnPoint(0f, 0f),
                    [],
                    [],
                    [],
                    roomObjects,
                    0f,
                    [],
                    importedFromSource: false),
            ]);
        return world;
    }

    private static void PreparePlayer(SimulationWorld world, PlayerTeam team, float x, float y)
    {
        world.PrepareLocalPlayerJoin();
        world.SetLocalPlayerTeam(team);
        world.CompleteLocalPlayerJoin(PlayerClass.Scout);
        world.LocalPlayer.TeleportTo(x, y);
    }

    private static void InvokeApplyTeleportZones(SimulationWorld world, PlayerEntity player)
    {
        var method = typeof(SimulationWorld).GetMethod(
            "ApplyTeleportZones",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method.Invoke(world, [player]);
    }
}
