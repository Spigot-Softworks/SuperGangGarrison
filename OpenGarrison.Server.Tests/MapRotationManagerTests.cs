using System;
using System.Collections.Generic;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.Server.Tests;

public sealed class MapRotationManagerTests
{
    [Fact]
    public void TryApplyPendingMapChange_WhenNextRoundMapIsQueued_UsesItOnceThenReturnsToRotation()
    {
        EnsureContentRootInitialized();
        var world = new SimulationWorld();
        world.AutoRestartOnMapChange = false;
        var logs = new List<string>();
        var manager = new MapRotationManager(
            world,
            requestedMap: "Truefort",
            mapRotationFile: null,
            stockMapRotation: ["Truefort", "Waterway"],
            logs.Add);

        Assert.True(manager.TrySetNextRoundMap("Truefort"));
        world.CompleteLocalPlayerJoin(PlayerClass.Scout);
        Assert.True(world.LocalPlayer.IsAlive);

        QueueCaptureTheFlagRoundEnd(world);
        Assert.True(manager.TryApplyPendingMapChange(out var firstTransition));
        Assert.Equal("Truefort", firstTransition.NextLevelName, StringComparer.OrdinalIgnoreCase);
        Assert.Equal("Truefort", world.Level.Name, StringComparer.OrdinalIgnoreCase);

        world.CompleteLocalPlayerJoin(PlayerClass.Scout);
        Assert.True(world.LocalPlayer.IsAlive);

        QueueCaptureTheFlagRoundEnd(world);
        Assert.True(manager.TryApplyPendingMapChange(out var secondTransition));
        Assert.Equal("Waterway", secondTransition.NextLevelName, StringComparer.OrdinalIgnoreCase);
        Assert.Equal("Waterway", world.Level.Name, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(logs, line => line.Contains("queued next round map", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TrySetNextRoundMap_WhenMapIsUnknown_ReturnsFalse()
    {
        EnsureContentRootInitialized();
        var manager = new MapRotationManager(
            new SimulationWorld(),
            requestedMap: null,
            mapRotationFile: null,
            stockMapRotation: ["Truefort"],
            _ => { });

        Assert.False(manager.TrySetNextRoundMap("definitely-not-a-map"));
    }

    private static void QueueCaptureTheFlagRoundEnd(SimulationWorld world)
    {
        Assert.Equal(GameModeKind.CaptureTheFlag, world.MatchRules.Mode);
        world.SetCapLimit(1);
        Assert.True(world.ForceGiveEnemyIntelToLocalPlayer());
        var ownBase = world.Level.GetIntelBase(world.LocalPlayerTeam);
        Assert.True(ownBase.HasValue);
        world.TeleportLocalPlayer(ownBase.Value.X, ownBase.Value.Y);
        world.AdvanceOneTick();
        Assert.True(world.IsMapChangePending);

        for (var tick = 0; tick < 400 && !world.IsMapChangeReady; tick += 1)
        {
            world.AdvanceOneTick();
        }

        Assert.True(world.IsMapChangeReady);
    }

    private static void EnsureContentRootInitialized()
    {
        var contentDirectory = ProjectSourceLocator.FindDirectory("OpenGarrison.Core/Content");
        Assert.False(string.IsNullOrWhiteSpace(contentDirectory));
        ContentRoot.Initialize(contentDirectory!);
    }
}
