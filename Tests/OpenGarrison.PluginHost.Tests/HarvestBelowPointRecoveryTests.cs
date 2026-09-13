using OpenGarrison.Core;
using OpenGarrison.Core.BotBrain;
using Xunit;
using Xunit.Abstractions;

namespace OpenGarrison.PluginHost.Tests;

[Collection(ContentRootTestGroup.Name)]
public sealed class HarvestBelowPointRecoveryTests(ITestOutputHelper output)
{
    public static IEnumerable<object[]> Starts()
    {
        foreach (var playerClass in new[] { PlayerClass.Soldier, PlayerClass.Heavy, PlayerClass.Scout })
            foreach (var offset in new[] { -192f, -144f, -96f, -72f, -48f, -24f, 0f, 24f, 48f, 72f, 96f, 144f, 192f })
                foreach (var thinkInterval in new[] { 1, 4 })
                    foreach (var enemyOnPoint in new[] { false, true })
                        yield return [playerClass, offset, thinkInterval, enemyOnPoint];
    }

    [Theory]
    [MemberData(nameof(Starts))]
    public void BotReachesHarvestPointFromBelow(PlayerClass playerClass, float offset, int thinkInterval, bool enemyOnPoint)
    {
        var oldRoot = ContentRoot.Path;
        ContentRoot.Initialize(ProjectSourceLocator.FindDirectory(Path.Combine("Core", "Content"))!);
        try
        {
            var world = new SimulationWorld(new SimulationConfig { EnableEnemyTrainingDummy = false, EnableFriendlySupportDummy = false });
            Assert.True(world.TryLoadLevel("gg2_koth_harvest"));
            world.SetPendingLocalPlayerClass(playerClass);
            Assert.True(world.TrySetNetworkPlayerTeam(SimulationWorld.LocalPlayerSlot, PlayerTeam.Blue));
            world.ForceRespawnLocalPlayer();
            var player = world.LocalPlayer;
            var point = Assert.Single(world.ControlPoints);
            point.Team = null;
            point.IsLocked = false;
            PlayerEntity? enemy = null;
            if (enemyOnPoint)
            {
                Assert.True(world.TryPrepareNetworkPlayerJoin(2));
                Assert.True(world.TrySetNetworkPlayerTeam(2, PlayerTeam.Red));
                Assert.True(world.TryApplyNetworkPlayerClassSelection(2, PlayerClass.Heavy));
                Assert.True(world.TryGetNetworkPlayer(2, out enemy));
                enemy.TeleportTo(point.HealingAuraCenterX, point.HealingAuraCenterY);
                // Let the target settle onto the point's platform first.
                for (var tick = 0; tick < 30; tick++) world.AdvanceOneTick();
            }
            var x = point.HealingAuraCenterX + offset;
            var y = float.NaN;
            for (var candidate = point.HealingAuraCenterY + 24; candidate < point.HealingAuraCenterY + 300; candidate++)
            {
                if (player.CanOccupy(world.Level, player.Team, x, candidate)
                    && !player.CanOccupy(world.Level, player.Team, x, candidate + 1))
                { y = candidate; }
            }
            Assert.False(float.IsNaN(y), $"No floor below point {point.HealingAuraCenterX},{point.HealingAuraCenterY} at x={x}");
            player.TeleportTo(x, y);
            player.RestoreMovementProbeState(true, null, offset < 0 ? 1f : -1f);
            var controller = new BotBrainController();
            var captured = false;
            PlayerInputSnapshot input = default;
            for (var tick = 0; tick < 600; tick++)
            {
                enemy?.ForceSetHealth(enemy.MaxHealth);
                player.ForceSetHealth(player.MaxHealth);
                if (tick % thinkInterval == 0)
                    input = controller.Think(player, world, player.Team);
                else if (controller.RequiresPerTickNavigationThink
                    && controller.TryAdvanceCachedNavigation(player, world, player.Team, input, out var updated))
                    input = updated;
                world.TrySetNetworkPlayerInput(SimulationWorld.LocalPlayerSlot, input);
                world.AdvanceOneTick();
                if (tick % 30 == 0)
                    output.WriteLine($"{tick}: ({player.X:0},{player.Y:0}) point=({point.HealingAuraCenterX},{point.HealingAuraCenterY}) graph={controller.LastNavigationGraphSource} direct={controller.LastDirectDriveTrace} path={controller.CurrentPathIndex}/{controller.CurrentPathCount} traversal={controller.LastTraversalTrace}");
                if (world.IsPlayerInControlPointCaptureZone(player, point.Index)) { captured = true; break; }
            }
            Assert.True(captured, $"Never reached point from ({x},{y}); ended ({player.X},{player.Y}) {controller.LastDirectDriveTrace} {controller.LastTraversalTrace}");
        }
        finally { ContentRoot.Initialize(oldRoot); }
    }
}
