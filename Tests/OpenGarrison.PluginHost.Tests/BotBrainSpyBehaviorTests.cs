using OpenGarrison.Core;
using OpenGarrison.Core.BotBrain;
using System.Reflection;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class BotBrainSpyBehaviorTests
{
    [Fact]
    public void SpyRequestsCloakWhenIdle()
    {
        var world = CreateSpyWorld(out var spy);

        var decision = CombatDecisionResolver.Resolve(world, spy, null, null, new CombatDecisionMemory());

        Assert.False(decision.FirePrimary);
        Assert.True(decision.FireSecondary);
    }

    [Fact]
    public void CloakedLowHealthSpyStaysCloakedWithoutEnemy()
    {
        var world = CreateSpyWorld(out var spy);
        Assert.True(spy.TryToggleSpyCloak());
        spy.ForceSetHealth(Math.Max(1, spy.MaxHealth / 5));

        var decision = CombatDecisionResolver.Resolve(world, spy, null, null, new CombatDecisionMemory());

        Assert.False(decision.FirePrimary);
        Assert.False(decision.FireSecondary);
    }

    [Fact]
    public void CloakedSpyBackstabsIsolatedStableTargetFromBehind()
    {
        var world = CreateSpyWorld(out var spy);
        var target = AddNetworkPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Blue, 300f, 100f);
        spy.TeleportTo(target.X + 26f, target.Y);
        Assert.True(spy.TryToggleSpyCloak());

        var combatTarget = CreateCombatTarget(target);
        var plan = CombatDecisionResolver.ResolveSpyBackstabPlan(world, spy, combatTarget);
        var decision = CombatDecisionResolver.Resolve(world, spy, combatTarget, null, new CombatDecisionMemory());

        Assert.True(plan.ShouldAttempt);
        Assert.True(plan.ReadyToStab);
        Assert.True(decision.FirePrimary);
        Assert.False(decision.FireSecondary);
    }

    [Fact]
    public void CloakedSpyBreaksCloakToShootGroupedTarget()
    {
        var world = CreateSpyWorld(out var spy);
        var target = AddNetworkPlayer(world, 2, PlayerClass.Soldier, PlayerTeam.Blue, 300f, 100f);
        _ = AddNetworkPlayer(world, 3, PlayerClass.Heavy, PlayerTeam.Blue, target.X + 80f, target.Y);
        spy.TeleportTo(target.X + 120f, target.Y);
        Assert.True(spy.TryToggleSpyCloak());
        FadeCloakToToggleThreshold(spy, world);

        var combatTarget = CreateCombatTarget(target);
        var decision = CombatDecisionResolver.Resolve(world, spy, combatTarget, null, new CombatDecisionMemory());

        Assert.False(CombatDecisionResolver.ResolveSpyBackstabPlan(world, spy, combatTarget).ShouldAttempt);
        Assert.False(decision.FirePrimary);
        Assert.True(decision.FireSecondary);
    }

    private static SimulationWorld CreateSpyWorld(out PlayerEntity spy)
    {
        var world = new SimulationWorld();
        Assert.True(world.TrySetLocalClass(PlayerClass.Spy));
        SetCombatLevel(world);
        Assert.True(world.TrySetNetworkPlayerTeam(SimulationWorld.LocalPlayerSlot, PlayerTeam.Red));
        world.ForceRespawnLocalPlayer();
        spy = world.LocalPlayer;
        spy.TeleportTo(100f, 100f);
        return world;
    }

    private static BotBrainCombatTarget CreateCombatTarget(PlayerEntity target)
    {
        return new BotBrainCombatTarget(
            BotBrainCombatTargetKind.Player,
            target.Team,
            target.X,
            target.Y,
            Player: target);
    }

    private static void FadeCloakToToggleThreshold(PlayerEntity spy, SimulationWorld world)
    {
        while (spy.SpyCloakAlpha > PlayerEntity.SpyCloakToggleThreshold)
        {
            spy.Advance(default, jumpPressed: false, world.Level, spy.Team, world.Config.FixedDeltaSeconds);
        }
    }

    private static void SetCombatLevel(SimulationWorld world)
    {
        var method = typeof(SimulationWorld).GetMethod("CombatTestSetLevel", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        _ = method!.Invoke(
            world,
            [
                new SimpleLevel(
                    name: "botbrain_spy_behavior_test",
                    mode: GameModeKind.CaptureTheFlag,
                    bounds: new WorldBounds(2048f, 2048f),
                    mapScale: 1f,
                    backgroundAssetName: null,
                    mapAreaIndex: 1,
                    mapAreaCount: 1,
                    localSpawn: new SpawnPoint(100f, 100f),
                    redSpawns: [new SpawnPoint(100f, 100f)],
                    blueSpawns: [new SpawnPoint(400f, 100f)],
                    intelBases:
                    [
                        new IntelBaseMarker(PlayerTeam.Red, 100f, 100f),
                        new IntelBaseMarker(PlayerTeam.Blue, 400f, 100f),
                    ],
                    roomObjects: [],
                    floorY: 2048f,
                    solids: [],
                    importedFromSource: false),
            ]);
    }

    private static PlayerEntity AddNetworkPlayer(
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
}
