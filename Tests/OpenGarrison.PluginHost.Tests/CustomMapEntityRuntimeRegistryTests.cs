using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class CustomMapEntityRuntimeRegistryTests
{
    [Fact]
    public void ModernSpawnImportsToTeamSpawnLists()
    {
        var redSpawns = new List<SpawnPoint>();
        var blueSpawns = new List<SpawnPoint>();
        var roomObjects = new List<RoomObjectMarker>();
        var context = new CustomMapEntityImportContext
        {
            RedSpawns = redSpawns,
            BlueSpawns = blueSpawns,
            RoomObjects = roomObjects,
        };

        Assert.True(CustomMapEntityRuntimeRegistry.TryImport(
            "spawn",
            10f,
            20f,
            1f,
            1f,
            new Dictionary<string, string> { ["team"] = "red", ["forward"] = "false" },
            context));
        Assert.True(CustomMapEntityRuntimeRegistry.TryImport(
            "spawn",
            30f,
            40f,
            1f,
            1f,
            new Dictionary<string, string> { ["team"] = "blue", ["forward"] = "true", ["objectiveIndex"] = "2" },
            context));

        Assert.Single(redSpawns);
        Assert.Single(blueSpawns);
        Assert.Equal(10f, redSpawns[0].X);
        Assert.Equal(30f, blueSpawns[0].X);
        Assert.True(redSpawns[0].IsStandardSpawn);
        Assert.True(blueSpawns[0].IsForwardSpawn);
        Assert.Equal(2, blueSpawns[0].LinkedControlPointIndex);
        Assert.Equal(ForwardSpawnUseCondition.ObjectiveOwnedByTeam, blueSpawns[0].UseCondition);
    }

    [Fact]
    public void ModernForwardSpawnImportsUseWhenCondition()
    {
        var context = new CustomMapEntityImportContext
        {
            RedSpawns = [],
            BlueSpawns = [],
            RoomObjects = [],
        };

        Assert.True(CustomMapEntityRuntimeRegistry.TryImport(
            "spawn",
            1f,
            2f,
            1f,
            1f,
            new Dictionary<string, string>
            {
                ["team"] = "red",
                ["forward"] = "true",
                ["linkObjective"] = "controlPoint1",
                ["priority"] = "3",
                ["useWhen"] = "enemyOwned",
            },
            context));

        Assert.Single(context.RedSpawns);
        Assert.Equal(ForwardSpawnUseCondition.ObjectiveOwnedByEnemy, context.RedSpawns[0].UseCondition);
        Assert.Equal(3, context.RedSpawns[0].Priority);
        Assert.Equal(1, context.RedSpawns[0].LinkedControlPointIndex);
    }


    [Fact]
    public void LegacyForwardSpawnsImportAsForwardSpawnPoints()
    {
        var context = new CustomMapEntityImportContext
        {
            RedSpawns = [],
            BlueSpawns = [],
            RoomObjects = [],
        };

        var emptyProperties = new Dictionary<string, string>();
        Assert.True(CustomMapEntityRuntimeRegistry.TryImport("redspawn", 10f, 20f, 1f, 1f, emptyProperties, context));
        Assert.True(CustomMapEntityRuntimeRegistry.TryImport("redspawn2", 30f, 40f, 1f, 1f, emptyProperties, context));
        Assert.True(CustomMapEntityRuntimeRegistry.TryImport("bluespawn3", 50f, 60f, 1f, 1f, emptyProperties, context));

        Assert.Equal(2, context.RedSpawns.Count);
        Assert.True(context.RedSpawns[0].IsStandardSpawn);
        Assert.True(context.RedSpawns[1].IsForwardSpawn);
        Assert.Equal(2, context.RedSpawns[1].LinkedControlPointIndex);
        Assert.Equal(2, context.RedSpawns[1].Priority);
        Assert.Single(context.BlueSpawns);
        Assert.True(context.BlueSpawns[0].IsForwardSpawn);
        Assert.Equal(3, context.BlueSpawns[0].LinkedControlPointIndex);

        context.RoomObjects.Add(new RoomObjectMarker(
            RoomObjectType.ControlPoint,
            0f,
            0f,
            42f,
            42f,
            "ControlPointNeutralS",
            SourceName: "controlPoint1"));
        context.RoomObjects.Add(new RoomObjectMarker(
            RoomObjectType.ControlPoint,
            0f,
            0f,
            42f,
            42f,
            "ControlPointNeutralS",
            SourceName: "controlPoint2"));
        context.RoomObjects.Add(new RoomObjectMarker(
            RoomObjectType.ControlPoint,
            0f,
            0f,
            42f,
            42f,
            "ControlPointNeutralS",
            SourceName: "controlPoint3"));
        ForwardSpawnMetadata.ApplyForwardSpawnControlPointLinks(
            context.RedSpawns,
            context.BlueSpawns,
            ForwardSpawnMetadata.CountMapControlPoints(context.RoomObjects.ToArray()));

        Assert.Equal(3, context.BlueSpawns[0].LinkedControlPointIndex);
    }

    [Fact]
    public void ModernTeleportEntitiesAreRegistered()
    {
        Assert.True(CustomMapEntityRuntimeRegistry.IsModernEntityType(TeleportMetadata.TeleportEntityType));
        Assert.True(CustomMapEntityRuntimeRegistry.IsModernEntityType(TeleportMetadata.TeleportExitEntityType));
    }
}
