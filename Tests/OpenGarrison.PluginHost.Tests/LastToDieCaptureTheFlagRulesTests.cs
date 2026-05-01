using System;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class LastToDieCaptureTheFlagRulesTests
{
    [Fact]
    public void TryMoveLocalPlayerToIntelSpawnPlacesPlayerNearOwnIntelBase()
    {
        var world = CreateJoinedEngineerWorld();

        Assert.True(world.TryMoveLocalPlayerToIntelSpawn());
        var ownIntelBase = world.Level.GetIntelBase(PlayerTeam.Red);
        Assert.True(ownIntelBase.HasValue);
        Assert.InRange(DistanceBetween(world.LocalPlayer.X, world.LocalPlayer.Y, ownIntelBase.Value.X, ownIntelBase.Value.Y), 0f, 128f);
    }

    [Fact]
    public void SpecialCaptureRuleEndsMatchImmediatelyWhenRedCaps()
    {
        var world = CreateJoinedEngineerWorld();
        world.ConfigureSpecialCaptureTheFlagRules(endMatchOnRedTeamIntelCapture: true);

        Assert.True(world.TryMoveLocalPlayerToIntelSpawn());
        Assert.True(world.ForceGiveEnemyIntelToLocalPlayer());

        world.AdvanceOneTick();

        Assert.True(world.MatchState.IsEnded);
        Assert.Equal(PlayerTeam.Red, world.MatchState.WinnerTeam);
        Assert.Equal(1, world.RedCaps);
    }

    private static SimulationWorld CreateJoinedEngineerWorld()
    {
        var world = new SimulationWorld();
        Assert.True(world.TryLoadLevel("TwodFortTwo"));
        world.ConfigureMatchDefaults(capLimit: 3);
        world.PrepareLocalPlayerJoin();
        world.SetLocalPlayerTeam(PlayerTeam.Red);
        world.CompleteLocalPlayerJoin(PlayerClass.Engineer);
        return world;
    }

    private static float DistanceBetween(float x1, float y1, float x2, float y2)
    {
        var deltaX = x2 - x1;
        var deltaY = y2 - y1;
        return MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
    }
}
