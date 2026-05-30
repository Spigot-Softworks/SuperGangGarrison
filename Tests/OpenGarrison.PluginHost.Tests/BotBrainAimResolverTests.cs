using System;
using System.Collections.Generic;
using OpenGarrison.Core;
using OpenGarrison.Core.BotBrain;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class BotBrainAimResolverTests
{
    [Fact]
    public void TraversalAimFacesWaypointWithoutLookingDownAtWaypointElevation()
    {
        var self = new PlayerEntity(1, CharacterClassCatalog.Medic);
        self.Spawn(PlayerTeam.Red, 100f, 100f);
        var graph = new NavGraph(
            [new NavNode(220f, 220f, NavNodeKind.Surface)],
            [new List<NavEdge>()]);
        var path = new NavPath([0], 0f);

        var aim = new AimResolver().Resolve(self, null, null, graph, path, new SteeringOutput());

        Assert.Equal(220f, aim.AimX);
        Assert.Equal(GetExpectedAimFocusY(self), aim.AimY);
    }

    [Fact]
    public void MedicHealAimUsesStableTargetFocus()
    {
        var medic = new PlayerEntity(1, CharacterClassCatalog.Medic);
        medic.Spawn(PlayerTeam.Red, 100f, 100f);
        var target = new PlayerEntity(2, CharacterClassCatalog.Heavy);
        target.Spawn(PlayerTeam.Red, 180f, 124f);

        var aim = new AimResolver().Resolve(medic, null, target, null, null, new SteeringOutput());

        Assert.Equal(target.X, aim.AimX);
        Assert.Equal(GetExpectedAimFocusY(target), aim.AimY);
    }

    private static float GetExpectedAimFocusY(PlayerEntity player) =>
        player.Y - MathF.Min(8f, player.Height * 0.25f);
}
