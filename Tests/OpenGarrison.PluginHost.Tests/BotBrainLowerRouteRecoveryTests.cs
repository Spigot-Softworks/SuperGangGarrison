using OpenGarrison.Core;
using OpenGarrison.Core.BotBrain;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

[Collection(ContentRootTestGroup.Name)]
public sealed class BotBrainLowerRouteRecoveryTests
{
    [Theory]
    [InlineData("Valley", PlayerTeam.Blue, 1770f, 786f)]
    [InlineData("Valley", PlayerTeam.Red, 2898f, 786f)]
    [InlineData("Gallery", PlayerTeam.Red, 1602f, 1152f)]
    public void BotRecoversWhenControlPointRouteCollapsesToLowerLaneProxy(
        string levelName,
        PlayerTeam team,
        float startX,
        float startBottom)
    {
        using var contentRoot = CoreContentRootScope.Create();
        var world = new SimulationWorld(new SimulationConfig
        {
            EnableEnemyTrainingDummy = false,
            EnableFriendlySupportDummy = false,
        });
        Assert.True(world.TryLoadLevel(levelName, 1, preservePlayerStats: false));
        world.SetPendingLocalPlayerClass(PlayerClass.Scout);
        Assert.True(world.TrySetNetworkPlayerTeam(SimulationWorld.LocalPlayerSlot, team));
        world.ForceRespawnLocalPlayer();
        foreach (var point in world.ControlPoints)
        {
            point.Team = null;
            point.IsLocked = false;
            point.CappingTeam = null;
            point.CappingTicks = 0f;
        }

        var player = world.LocalPlayer;
        var startY = startBottom - player.CollisionBottomOffset;
        player.TeleportTo(startX, startY);
        player.RestoreMovementProbeState(
            isGrounded: true,
            remainingAirJumps: null,
            facingDirectionX: startX < world.Level.Bounds.Width * 0.5f ? 1f : -1f);
        var controller = new BotBrainController();
        var activeTicks = 0;

        for (var tick = 0; tick < 120; tick += 1)
        {
            var input = controller.Think(player, world, team);
            if (input.Left || input.Right || input.Up || input.Down)
            {
                activeTicks += 1;
            }

            Assert.True(world.TrySetNetworkPlayerInput(SimulationWorld.LocalPlayerSlot, input));
            world.AdvanceOneTick();
        }

        var displacement = MathF.Sqrt(((player.X - startX) * (player.X - startX)) + ((player.Y - startY) * (player.Y - startY)));
        Assert.True(controller.HasNavigationGraph);
        Assert.True(
            activeTicks >= 30,
            $"activeTicks:{activeTicks} displacement:{displacement:0.0} path:{controller.CurrentPathIndex}/{controller.CurrentPathCount} direct:{controller.LastDirectDriveTrace} traversal:{controller.LastTraversalTrace}");
        Assert.True(
            displacement > 24f,
            $"activeTicks:{activeTicks} displacement:{displacement:0.0} path:{controller.CurrentPathIndex}/{controller.CurrentPathCount} direct:{controller.LastDirectDriveTrace} traversal:{controller.LastTraversalTrace}");
    }

    private sealed class CoreContentRootScope : IDisposable
    {
        private readonly string _originalContentRoot;

        private CoreContentRootScope(string originalContentRoot)
        {
            _originalContentRoot = originalContentRoot;
        }

        public static CoreContentRootScope Create()
        {
            var originalContentRoot = ContentRoot.Path;
            var coreContent = ProjectSourceLocator.FindDirectory(Path.Combine("Core", "Content"));
            Assert.False(string.IsNullOrWhiteSpace(coreContent));
            ContentRoot.Initialize(coreContent!);
            return new CoreContentRootScope(originalContentRoot);
        }

        public void Dispose()
        {
            ContentRoot.Initialize(_originalContentRoot);
        }
    }
}
