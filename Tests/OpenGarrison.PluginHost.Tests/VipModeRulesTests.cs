using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

[Collection(ContentRootTestGroup.Name)]
public sealed class VipModeRulesTests
{
    [Theory]
    [InlineData("vip_egypt", "vip_egypt", 1)]
    [InlineData("vip_dirtbowl", "vip_dirtbowl", 3)]
    [InlineData("vip_dustbowl", "vip_dustbowl", 3)]
    public void VipPrefixedControlPointMapsLoadAsVipAliases(string requestedLevelName, string expectedRuntimeName, int expectedAreaCount)
    {
        var level = SimpleLevelFactory.CreateImportedLevel(requestedLevelName);

        Assert.NotNull(level);
        Assert.Equal(expectedRuntimeName, level!.Name);
        Assert.Equal(GameModeKind.Vip, level.Mode);
        Assert.Equal(expectedAreaCount, level.MapAreaCount);
        Assert.NotEmpty(level.GetRoomObjects(RoomObjectType.ControlPoint));
    }

    [Fact]
    public void FiveControlPointVipMapsStartWithDualVipWarmup()
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });

        Assert.True(world.TryLoadLevel("vip_egypt"));

        Assert.True(world.IsVipModeActive);
        Assert.True(world.VipRequiresDualVip);
        Assert.True(world.VipWarmupActive);
        Assert.Equal(10 * world.Config.TicksPerSecond, world.VipWarmupTicksRemaining);
    }

    [Fact]
    public void AttackDefenseVipMapsUseSingleAttackerVipWithoutDualWarmup()
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });

        Assert.True(world.TryLoadLevel("vip_dirtbowl"));

        Assert.True(world.IsVipModeActive);
        Assert.False(world.VipRequiresDualVip);
        Assert.False(world.VipWarmupActive);
        Assert.Equal(0, world.VipWarmupTicksRemaining);
    }

    [Fact]
    public void PracticeVipRulesDeferAssignmentUntilSetupEndsAndPreservePlayerCivilianSelection()
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });

        Assert.True(world.TryLoadLevel("Dirtbowl"));
        world.ConfigurePracticeVipRules(enabled: true);
        world.SetPendingLocalPlayerClass(PlayerClass.Quote);

        world.AdvanceOneTick();

        Assert.True(world.IsVipModeActive);
        Assert.True(world.ControlPointSetupActive);
        Assert.False(world.TryGetVipSlot(PlayerTeam.Red, out _));

        AdvancePastSetup(world);

        Assert.True(world.TryGetVipSlot(PlayerTeam.Red, out var vipSlot));
        Assert.Equal(SimulationWorld.LocalPlayerSlot, vipSlot);
        Assert.Equal(PlayerClass.Quote, world.LocalPlayer.ClassId);
    }

    [Fact]
    public void PracticeVipRulesAssignBotAtSetupEndWhenNoCivilianVipExists()
    {
        const byte botSlot = 2;
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });

        Assert.True(world.TryLoadLevel("Dirtbowl"));
        world.ConfigurePracticeVipRules(enabled: true);
        Assert.True(world.TryPrepareNetworkPlayerJoin(botSlot));
        Assert.True(world.TrySetNetworkPlayerTeam(botSlot, PlayerTeam.Red));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(botSlot, PlayerClass.Scout));

        world.AdvanceOneTick();

        Assert.False(world.TryGetVipSlot(PlayerTeam.Red, out _));

        AdvancePastSetup(world);

        Assert.True(world.TryGetVipSlot(PlayerTeam.Red, out var vipSlot));
        Assert.Equal(botSlot, vipSlot);
        Assert.Equal(PlayerClass.Scout, world.LocalPlayer.ClassId);
        Assert.True(world.TryGetNetworkPlayer(botSlot, out var bot));
        Assert.Equal(PlayerClass.Quote, bot.ClassId);
    }

    [Fact]
    public void PracticeVipRulesPreferLocalCivilianOverCivilianBotAtSetupEnd()
    {
        const byte botSlot = 2;
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });

        Assert.True(world.TryLoadLevel("Dirtbowl"));
        world.ConfigurePracticeVipRules(enabled: true);
        world.SetPendingLocalPlayerClass(PlayerClass.Quote);
        Assert.True(world.TryPrepareNetworkPlayerJoin(botSlot));
        Assert.True(world.TrySetNetworkPlayerTeam(botSlot, PlayerTeam.Red));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(botSlot, PlayerClass.Quote));

        AdvancePastSetup(world);

        Assert.True(world.TryGetVipSlot(PlayerTeam.Red, out var vipSlot));
        Assert.Equal(SimulationWorld.LocalPlayerSlot, vipSlot);
        Assert.Equal(PlayerClass.Quote, world.LocalPlayer.ClassId);
        Assert.True(world.TryGetNetworkPlayer(botSlot, out var bot));
        Assert.Equal(PlayerClass.Scout, bot.ClassId);
    }

    [Fact]
    public void PracticeVipRulesKeepVipCivilianClassAcrossRespawn()
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });

        Assert.True(world.TryLoadLevel("Dirtbowl"));
        world.ConfigurePracticeVipRules(enabled: true);
        world.SetPendingLocalPlayerClass(PlayerClass.Quote);
        AdvancePastSetup(world);
        Assert.True(world.TryGetVipSlot(PlayerTeam.Red, out var vipSlot));
        Assert.Equal(SimulationWorld.LocalPlayerSlot, vipSlot);

        var sawVipKilledResolution = false;
        world.RoundEndDecisionInterceptor = request =>
        {
            if (request.Reason == "vip_killed")
            {
                sawVipKilledResolution = true;
                return WorldDecisionResult.Cancel("test keeps round active");
            }

            return WorldDecisionResult.Continue;
        };
        world.ForceKillLocalPlayer();
        Assert.False(world.LocalPlayer.IsAlive);

        for (var tick = 0; tick < 5 && !world.LocalPlayer.IsAlive; tick += 1)
        {
            world.AdvanceOneTick();
        }

        Assert.True(world.LocalPlayer.IsAlive);
        Assert.True(sawVipKilledResolution);
        Assert.Equal(PlayerClass.Quote, world.LocalPlayer.ClassId);
        Assert.True(world.TryGetVipSlot(PlayerTeam.Red, out vipSlot));
        Assert.Equal(SimulationWorld.LocalPlayerSlot, vipSlot);
    }

    private static void AdvancePastSetup(SimulationWorld world)
    {
        var safety = world.ControlPointSetupDurationTicks + 5;
        while (world.ControlPointSetupTicksRemaining > 0 && safety-- > 0)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(0, world.ControlPointSetupTicksRemaining);
        world.AdvanceOneTick();
    }
}
