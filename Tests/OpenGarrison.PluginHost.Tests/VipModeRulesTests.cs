using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

[Collection(ContentRootTestGroup.Name)]
public sealed class VipModeRulesTests
{
    private const float TestPointX = 320f;
    private const float TestPointY = 240f;
    private const byte RedTeammateSlot = 2;
    private const byte BlueEnemySlot = 3;

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
    public void AttackDefenseVipMapsUseSingleAttackerVipWithWarmup()
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });

        Assert.True(world.TryLoadLevel("vip_dirtbowl"));

        Assert.True(world.IsVipModeActive);
        Assert.False(world.VipRequiresDualVip);
        Assert.True(world.VipWarmupActive);
        Assert.Equal(10 * world.Config.TicksPerSecond, world.VipWarmupTicksRemaining);
    }

    [Fact]
    public void AttackDefenseVipWarmupDefersControlPointSetupCountdown()
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });

        Assert.True(world.TryLoadLevel("vip_dirtbowl"));
        var setupTicksBefore = world.ControlPointSetupTicksRemaining;

        world.AdvanceOneTick();

        Assert.True(world.VipWarmupActive);
        Assert.Equal(setupTicksBefore, world.ControlPointSetupTicksRemaining);
    }

    [Fact]
    public void VipMapsWaitForJoinedPlayersInsteadOfEndingWhenVipIsMissing()
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });

        Assert.True(world.TryLoadLevel("vip_dirtbowl"));
        world.ResetPlayersToAwaitingJoinForFreshMap();

        for (var tick = 0; tick < 60; tick += 1)
        {
            world.AdvanceOneTick();
        }

        Assert.False(world.MatchState.IsEnded);
        Assert.False(world.TryGetVipSlot(PlayerTeam.Red, out _));
    }

    [Fact]
    public void VipMapsAssignVipAfterPlayerRejoinsWithoutPriorDefenderWin()
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });

        Assert.True(world.TryLoadLevel("vip_dirtbowl"));
        world.ResetPlayersToAwaitingJoinForFreshMap();
        var player = JoinPlayer(world, RedTeammateSlot, PlayerTeam.Red, PlayerClass.Scout);

        world.AdvanceOneTick();

        Assert.False(world.MatchState.IsEnded);
        Assert.True(world.TryGetVipSlot(PlayerTeam.Red, out var vipSlot));
        Assert.Equal(RedTeammateSlot, vipSlot);
        Assert.Equal(PlayerClass.Quote, player.ClassId);
    }

    [Fact]
    public void FiveControlPointVipWarmupDoesNotResolveRoundFromInitialOwnership()
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });

        Assert.True(world.TryLoadLevel("vip_egypt"));

        world.AdvanceOneTick();

        Assert.False(world.MatchState.IsEnded);
        Assert.True(world.VipWarmupActive);
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
        Assert.True(world.TryApplyNetworkPlayerClassSelection(botSlot, PlayerClass.Soldier));

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
        world.SetVipAllowDuplicateClasses(true);
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
        Assert.False(sawVipKilledResolution);
        Assert.Equal(PlayerClass.Quote, world.LocalPlayer.ClassId);
        Assert.True(world.TryGetVipSlot(PlayerTeam.Red, out vipSlot));
        Assert.Equal(SimulationWorld.LocalPlayerSlot, vipSlot);
    }

    [Fact]
    public void VipCaptureRequiresVipToStartProgressAndFinish()
    {
        var world = CreateSinglePointVipWorldWithLocalVip();
        var point = Assert.Single(world.ControlPoints);
        var teammate = JoinPlayer(world, RedTeammateSlot, PlayerTeam.Red, PlayerClass.Scout);
        MoveAwayFromPoint(world.LocalPlayer);
        MoveToPoint(teammate);

        world.AdvanceOneTick();

        Assert.Equal(0f, point.CappingTicks);
        Assert.Null(point.CappingTeam);
        Assert.Equal(PlayerTeam.Blue, point.Team);

        point.CappingTeam = PlayerTeam.Red;
        point.CappingTicks = point.CapTimeTicks - 1f;

        world.AdvanceOneTick();

        Assert.Equal(PlayerTeam.Blue, point.Team);

        MoveToPoint(world.LocalPlayer);

        world.AdvanceOneTick();

        Assert.Equal(PlayerTeam.Red, point.Team);
    }

    [Fact]
    public void VipCaptureProgressesAtFiveTimesNormalClassSpeed()
    {
        var world = CreateSinglePointVipWorldWithLocalVip();
        var point = Assert.Single(world.ControlPoints);
        MoveToPoint(world.LocalPlayer);

        world.AdvanceOneTick();

        Assert.Equal(5f, point.CappingTicks);
        Assert.Equal(PlayerTeam.Red, point.CappingTeam);
        Assert.Equal(5, point.Cappers);
    }

    [Fact]
    public void VipCaptureIgnoresConfiguredCaptureSpeedMultiplier()
    {
        var world = CreateSinglePointVipWorldWithLocalVip();
        var point = Assert.Single(world.ControlPoints);
        world.SetCaptureSpeedMultiplierPerPlayer(2f);
        MoveToPoint(world.LocalPlayer);

        world.AdvanceOneTick();

        Assert.Equal(5f, point.CappingTicks);
        Assert.Equal(PlayerTeam.Red, point.CappingTeam);
        Assert.Equal(5, point.Cappers);
    }

    [Fact]
    public void CaptureSpeedMultiplierScalesPerPlayerCaptureProgress()
    {
        var world = CreateSinglePointControlPointWorld(PlayerTeam.Red, PlayerClass.Scout);
        var point = Assert.Single(world.ControlPoints);
        point.Team = PlayerTeam.Blue;
        world.SetCaptureSpeedMultiplierPerPlayer(1.5f);
        MoveToPoint(world.LocalPlayer);

        world.AdvanceOneTick();

        Assert.Equal(3f, point.CappingTicks);
        Assert.Equal(PlayerTeam.Red, point.CappingTeam);
        Assert.Equal(2, point.Cappers);
    }

    [Fact]
    public void ClassLimitsRejectSecondSameClassOnSameTeam()
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        world.SetClassLimit(PlayerClass.Soldier, 1);

        Assert.True(world.TryPrepareNetworkPlayerJoin(2));
        Assert.True(world.TrySetNetworkPlayerTeam(2, PlayerTeam.Red));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(2, PlayerClass.Soldier));

        Assert.True(world.TryPrepareNetworkPlayerJoin(3));
        Assert.True(world.TrySetNetworkPlayerTeam(3, PlayerTeam.Red));
        Assert.False(world.TryApplyNetworkPlayerClassSelection(3, PlayerClass.Soldier));

        Assert.True(world.TryPrepareNetworkPlayerJoin(4));
        Assert.True(world.TrySetNetworkPlayerTeam(4, PlayerTeam.Blue));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(4, PlayerClass.Soldier));
    }

    [Fact]
    public void SetAllClassLimitsAppliesSameLimitToEveryClass()
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        world.SetAllClassLimits(1);

        Assert.Equal(1, world.GetUniformClassLimit());
        Assert.Equal(1, world.GetClassLimit(PlayerClass.Scout));
        Assert.Equal(1, world.GetClassLimit(PlayerClass.Engineer));
        Assert.Equal(1, world.GetClassLimit(PlayerClass.Pyro));
        Assert.Equal(1, world.GetClassLimit(PlayerClass.Soldier));
        Assert.Equal(1, world.GetClassLimit(PlayerClass.Demoman));
        Assert.Equal(1, world.GetClassLimit(PlayerClass.Heavy));
        Assert.Equal(1, world.GetClassLimit(PlayerClass.Sniper));
        Assert.Equal(1, world.GetClassLimit(PlayerClass.Medic));
        Assert.Equal(1, world.GetClassLimit(PlayerClass.Spy));
        Assert.Equal(1, world.GetClassLimit(PlayerClass.Quote));

        world.SetClassLimit(PlayerClass.Soldier, 2);

        Assert.Equal(0, world.GetUniformClassLimit());
    }

    [Fact]
    public void VipModeDefaultsToOneOfEachClassPerTeamUnlessDuplicatesAreAllowed()
    {
        var world = CreateSinglePointVipWorldWithLocalVip();
        Assert.True(world.TryPrepareNetworkPlayerJoin(2));
        Assert.True(world.TrySetNetworkPlayerTeam(2, PlayerTeam.Red));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(2, PlayerClass.Scout));

        Assert.True(world.TryPrepareNetworkPlayerJoin(3));
        Assert.True(world.TrySetNetworkPlayerTeam(3, PlayerTeam.Red));
        Assert.False(world.TryApplyNetworkPlayerClassSelection(3, PlayerClass.Scout));

        world.SetVipAllowDuplicateClasses(true);

        Assert.True(world.TryApplyNetworkPlayerClassSelection(3, PlayerClass.Scout));
    }

    [Fact]
    public void DeadVipTeammatePausesDecayButEnemyAloneReversesProgress()
    {
        var world = CreateSinglePointVipWorldWithLocalVip();
        var point = Assert.Single(world.ControlPoints);
        var teammate = JoinPlayer(world, RedTeammateSlot, PlayerTeam.Red, PlayerClass.Scout);
        var enemy = JoinPlayer(world, BlueEnemySlot, PlayerTeam.Blue, PlayerClass.Scout);
        MoveAwayFromPoint(world.LocalPlayer);
        MoveToPoint(teammate);
        MoveToPoint(enemy);

        point.Team = PlayerTeam.Blue;
        point.CappingTeam = PlayerTeam.Red;
        point.CappingTicks = 20f;

        world.AdvanceOneTick();

        Assert.True(
            point.CappingTicks < 20f,
            $"expected teammate not to pause decay while VIP is alive, after={point.CappingTicks}");

        MoveAwayFromPoint(enemy);
        world.ForceKillLocalPlayer();
        point.CappingTeam = PlayerTeam.Red;
        point.CappingTicks = 20f;
        MoveToPoint(teammate);

        world.AdvanceOneTick();

        Assert.Equal(20f, point.CappingTicks);
        Assert.Equal(PlayerTeam.Red, point.CappingTeam);

        MoveAwayFromPoint(teammate);
        MoveToPoint(enemy);
        var progressBeforeEnemyReverse = point.CappingTicks;

        world.AdvanceOneTick();

        Assert.True(
            point.CappingTicks < progressBeforeEnemyReverse,
            $"expected enemy alone to reverse VIP capture progress, before={progressBeforeEnemyReverse} after={point.CappingTicks}");
    }

    [Fact]
    public void KillingVipSubtractsFifteenSecondsFromMatchTimer()
    {
        var world = CreateSinglePointVipWorldWithLocalVip();
        var enemy = JoinPlayer(world, BlueEnemySlot, PlayerTeam.Blue, PlayerClass.Scout);
        var timeRemainingBeforeKill = world.MatchState.TimeRemainingTicks;

        Assert.True(world.TryApplyGameplayDamage(
            world.LocalPlayer.Id,
            world.LocalPlayer.Health + 100f,
            enemy.Id,
            "RocketKL"));

        Assert.False(world.LocalPlayer.IsAlive);
        Assert.Equal(
            Math.Max(0, timeRemainingBeforeKill - (15 * world.Config.TicksPerSecond)),
            world.MatchState.TimeRemainingTicks);
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

    private static SimulationWorld CreateSinglePointVipWorldWithLocalVip()
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        world.CombatTestSetLevel(CreateSinglePointVipLevel());
        Assert.True(world.TrySetNetworkPlayerTeam(SimulationWorld.LocalPlayerSlot, PlayerTeam.Red, respawnLivePlayerImmediately: true));
        Assert.True(world.TrySetPreferredVipSlot(PlayerTeam.Red, SimulationWorld.LocalPlayerSlot));

        world.AdvanceOneTick();

        Assert.True(world.TryGetVipSlot(PlayerTeam.Red, out var vipSlot));
        Assert.Equal(SimulationWorld.LocalPlayerSlot, vipSlot);
        Assert.Equal(PlayerClass.Quote, world.LocalPlayer.ClassId);
        MoveAwayFromPoint(world.LocalPlayer);
        AdvancePastVipWarmup(world);
        return world;
    }

    private static SimulationWorld CreateSinglePointControlPointWorld(PlayerTeam team, PlayerClass playerClass)
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        world.CombatTestSetLevel(CreateSinglePointLevel(GameModeKind.ControlPoint, "cp_test_single_point"));
        world.PrepareLocalPlayerJoin();
        Assert.True(world.TrySetNetworkPlayerTeam(SimulationWorld.LocalPlayerSlot, team, respawnLivePlayerImmediately: true));
        world.CompleteLocalPlayerJoin(playerClass);
        Assert.Equal(playerClass, world.LocalPlayer.ClassId);
        MoveAwayFromPoint(world.LocalPlayer);
        return world;
    }

    private static void AdvancePastVipWarmup(SimulationWorld world)
    {
        var safety = world.VipWarmupTicksRemaining + world.Config.TicksPerSecond + 5;
        while (world.VipWarmupActive && safety-- > 0)
        {
            world.AdvanceOneTick();
        }

        Assert.False(world.VipWarmupActive);
    }

    private static PlayerEntity JoinPlayer(SimulationWorld world, byte slot, PlayerTeam team, PlayerClass playerClass)
    {
        Assert.True(world.TryPrepareNetworkPlayerJoin(slot));
        Assert.True(world.TrySetNetworkPlayerTeam(slot, team));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(slot, playerClass));
        Assert.True(world.TryGetNetworkPlayer(slot, out var player));
        return player;
    }

    private static void MoveToPoint(PlayerEntity player)
    {
        player.TeleportTo(TestPointX, TestPointY);
    }

    private static void MoveAwayFromPoint(PlayerEntity player)
    {
        player.TeleportTo(64f, 64f);
    }

    private static SimpleLevel CreateSinglePointVipLevel()
    {
        return CreateSinglePointLevel(GameModeKind.Vip, "vip_test_single_point");
    }

    private static SimpleLevel CreateSinglePointLevel(GameModeKind mode, string name)
    {
        return new SimpleLevel(
            name: name,
            mode: mode,
            bounds: new WorldBounds(960f, 640f),
            mapScale: 1f,
            backgroundAssetName: null,
            mapAreaIndex: 1,
            mapAreaCount: 1,
            localSpawn: new SpawnPoint(64f, 64f),
            redSpawns: [new SpawnPoint(64f, 64f)],
            blueSpawns: [new SpawnPoint(860f, 64f)],
            intelBases:
            [
                new IntelBaseMarker(PlayerTeam.Red, 64f, 64f),
                new IntelBaseMarker(PlayerTeam.Blue, 860f, 64f),
            ],
            roomObjects:
            [
                new RoomObjectMarker(
                    RoomObjectType.ControlPoint,
                    TestPointX - 21f,
                    TestPointY - 21f,
                    42f,
                    42f,
                    "ControlPointNeutralS",
                    SourceName: "ControlPoint1"),
                new RoomObjectMarker(
                    RoomObjectType.CaptureZone,
                    TestPointX - 100f,
                    TestPointY - 90f,
                    200f,
                    180f,
                    string.Empty,
                    SourceName: "CaptureZone"),
                new RoomObjectMarker(
                    RoomObjectType.ControlPointSetupGate,
                    900f,
                    600f,
                    24f,
                    24f,
                    "SetupGateS",
                    SourceName: "ControlPointSetupGate"),
            ],
            floorY: 320f,
            solids: [new LevelSolid(0f, 320f, 960f, 320f)],
            importedFromSource: false);
    }
}
