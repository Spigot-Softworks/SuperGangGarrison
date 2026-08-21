using System.Reflection;
using OpenGarrison.Core;
using OpenGarrison.Core.BotBrain;
using OpenGarrison.Core.LastToDie;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class BotBrainMartyrTargetingTests
{
    [Fact]
    public void BotPrioritizesVisibleMartyrProtectorAndRedirectsCachedProtectedTarget()
    {
        var world = CreateWorld([]);
        var bot = world.LocalPlayer;
        bot.TeleportTo(100f, 100f);
        var protectedTarget = AddPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Blue, 220f, 100f);
        var protector = AddPlayer(world, 3, PlayerClass.Medic, PlayerTeam.Blue, 300f, 100f);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(3, [LastToDiePerkIds.Medic.Martyr]));
        protector.SetMedicHealingTarget(protectedTarget);
        RefreshMedicLinks(world);

        var selected = TargetSelector.SelectCombatTarget(bot, world, PlayerTeam.Red);

        Assert.NotNull(selected);
        Assert.Same(protector, selected!.Value.Player);

        var refreshMethod = typeof(BotBrainController).GetMethod(
            "TryRefreshReusableGraphlessCombatTarget",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(refreshMethod);
        object?[] arguments =
        [
            new BotBrainCombatTarget(
                BotBrainCombatTargetKind.Player,
                protectedTarget.Team,
                protectedTarget.X,
                protectedTarget.Y,
                Player: protectedTarget),
            bot,
            world,
            PlayerTeam.Red,
            null,
        ];

        Assert.True((bool)refreshMethod!.Invoke(null, arguments)!);
        var refreshed = Assert.IsType<BotBrainCombatTarget>(arguments[4]);
        Assert.Same(protector, refreshed.Player);
    }

    [Fact]
    public void BotKeepsProtectedTargetWhenProtectorIsOutOfRange()
    {
        var world = CreateWorld([]);
        var bot = world.LocalPlayer;
        bot.TeleportTo(100f, 100f);
        var protectedTarget = AddPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Blue, 350f, 100f);
        var protector = AddPlayer(world, 3, PlayerClass.Medic, PlayerTeam.Blue, 600f, 100f);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(3, [LastToDiePerkIds.Medic.Martyr]));
        protector.SetMedicHealingTarget(protectedTarget);
        RefreshMedicLinks(world);
        Assert.True(protectedTarget.LastToDieMedicMartyrProtectedLinkActive);

        var selected = TargetSelector.SelectCombatTarget(bot, world, PlayerTeam.Red);

        Assert.NotNull(selected);
        Assert.Same(protectedTarget, selected!.Value.Player);
    }

    [Fact]
    public void BotDoesNotRedirectThroughBlockedLineOfSight()
    {
        var world = CreateWorld([new LevelSolid(175f, 140f, 25f, 40f)]);
        var bot = world.LocalPlayer;
        bot.TeleportTo(100f, 100f);
        var protectedTarget = AddPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Blue, 250f, 100f);
        var protector = AddPlayer(world, 3, PlayerClass.Medic, PlayerTeam.Blue, 250f, 200f);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(3, [LastToDiePerkIds.Medic.Martyr]));
        protector.SetMedicHealingTarget(protectedTarget);
        RefreshMedicLinks(world);
        Assert.True(protectedTarget.LastToDieMedicMartyrProtectedLinkActive);
        Assert.True(CombatDecisionResolver.HasCombatLineOfSight(
            world,
            bot.X,
            bot.Y,
            protectedTarget.X,
            protectedTarget.Y));
        Assert.False(CombatDecisionResolver.HasCombatLineOfSight(
            world,
            bot.X,
            bot.Y,
            protector.X,
            protector.Y));

        var selected = TargetSelector.SelectCombatTarget(bot, world, PlayerTeam.Red);

        Assert.NotNull(selected);
        Assert.Same(protectedTarget, selected!.Value.Player);
    }

    private static SimulationWorld CreateWorld(IReadOnlyList<LevelSolid> solids)
    {
        var world = new SimulationWorld(new SimulationConfig
        {
            EnableLocalDummies = false,
            EnableEnemyTrainingDummy = false,
            EnableFriendlySupportDummy = false,
        });
        world.CombatTestSetLevel(new SimpleLevel(
            name: "martyr_bot_targeting_test",
            mode: GameModeKind.CaptureTheFlag,
            bounds: new WorldBounds(1000f, 600f),
            mapScale: 1f,
            backgroundAssetName: null,
            mapAreaIndex: 1,
            mapAreaCount: 1,
            localSpawn: new SpawnPoint(100f, 100f),
            redSpawns: [new SpawnPoint(100f, 100f)],
            blueSpawns: [new SpawnPoint(800f, 100f)],
            intelBases: [],
            roomObjects: [],
            floorY: 600f,
            solids: solids,
            importedFromSource: false));
        world.PrepareLocalPlayerJoin();
        Assert.True(world.TrySetNetworkPlayerTeam(
            SimulationWorld.LocalPlayerSlot,
            PlayerTeam.Red,
            respawnLivePlayerImmediately: true));
        world.CompleteLocalPlayerJoin(PlayerClass.Pyro);
        return world;
    }

    private static PlayerEntity AddPlayer(
        SimulationWorld world,
        byte slot,
        PlayerClass playerClass,
        PlayerTeam team,
        float x,
        float y)
    {
        Assert.True(world.TryPrepareNetworkPlayerJoin(slot));
        Assert.True(world.TrySetNetworkPlayerTeam(slot, team));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(slot, playerClass));
        Assert.True(world.TryGetNetworkPlayer(slot, out var player));
        player.TeleportTo(x, y);
        return player;
    }

    private static void RefreshMedicLinks(SimulationWorld world)
    {
        var method = typeof(SimulationWorld).GetMethod(
            "RefreshLastToDieMedicLinkProjections",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        _ = method!.Invoke(world, null);
    }
}
