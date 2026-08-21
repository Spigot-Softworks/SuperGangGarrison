using OpenGarrison.Core;
using OpenGarrison.Core.BotBrain;
using System.Reflection;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class BotBrainLineOfSightTests
{
    [Fact]
    public void BotLineOfSightUsesSolidSpatialCacheWithoutChangingBlockingSemantics()
    {
        var world = CreateWorld(
            roomObjects: [],
            solids: [new LevelSolid(200f, 90f, 32f, 60f)]);

        Assert.False(CombatDecisionResolver.HasLineOfSight(world, 100f, 100f, 400f, 100f, PlayerTeam.Red, carryingIntel: false));
        Assert.True(CombatDecisionResolver.HasLineOfSight(world, 100f, 200f, 400f, 200f, PlayerTeam.Red, carryingIntel: false));
    }

    [Fact]
    public void BotLineOfSightUsesStaticRoomObjectBlockerSpatialCache()
    {
        var world = CreateWorld(
            roomObjects:
            [
                new RoomObjectMarker(RoomObjectType.PlayerWall, 200f, 90f, 32f, 60f, "PlayerWall"),
            ],
            solids: []);

        Assert.False(CombatDecisionResolver.HasLineOfSight(world, 100f, 100f, 400f, 100f, PlayerTeam.Red, carryingIntel: false));
        Assert.True(CombatDecisionResolver.HasLineOfSight(world, 100f, 200f, 400f, 200f, PlayerTeam.Red, carryingIntel: false));
    }

    [Fact]
    public void BotLineOfSightFrameCacheIncludesForcedGateState()
    {
        var world = CreateWorld(
            roomObjects:
            [
                new RoomObjectMarker(RoomObjectType.TeamGate, 200f, 90f, 32f, 60f, "RedGate", PlayerTeam.Red),
            ],
            solids: []);

        Assert.True(CombatDecisionResolver.HasLineOfSight(world, 100f, 100f, 400f, 100f, PlayerTeam.Red, carryingIntel: false));

        world.Level.ForcedBlockingTeamGates = TeamGateLockMask.Red;

        Assert.False(CombatDecisionResolver.HasLineOfSight(world, 100f, 100f, 400f, 100f, PlayerTeam.Red, carryingIntel: false));
    }

    [Fact]
    public void BotCombatLineOfSightTreatsOwnTeamGateAsCombatBlocker()
    {
        var world = CreateWorld(
            roomObjects:
            [
                new RoomObjectMarker(RoomObjectType.TeamGate, 200f, 90f, 32f, 60f, "RedGate", PlayerTeam.Red),
            ],
            solids: []);

        Assert.True(CombatDecisionResolver.HasLineOfSight(world, 100f, 100f, 400f, 100f, PlayerTeam.Red, carryingIntel: false));
        Assert.False(CombatDecisionResolver.HasCombatLineOfSight(world, 100f, 100f, 400f, 100f));
    }

    [Fact]
    public void BotTargetSelectionCanAcquireEnemyBehindOwnSpawnDoor()
    {
        var world = CreateWorld(
            roomObjects:
            [
                new RoomObjectMarker(RoomObjectType.TeamGate, 200f, 90f, 32f, 60f, "RedGate", PlayerTeam.Red),
            ],
            solids: []);
        Assert.True(world.TrySetNetworkPlayerTeam(SimulationWorld.LocalPlayerSlot, PlayerTeam.Red));
        world.ForceRespawnLocalPlayer();
        world.LocalPlayer.TeleportTo(100f, 100f);
        world.LocalPlayer.RestoreMovementProbeState(isGrounded: true, remainingAirJumps: null, facingDirectionX: 1f);
        var enemy = AddNetworkPlayer(world, 2, PlayerClass.Scout, PlayerTeam.Blue, 400f, 100f);

        var target = TargetSelector.SelectCombatTarget(world.LocalPlayer, world, PlayerTeam.Red);

        Assert.NotNull(target);
        Assert.Equal(enemy.Id, target!.Value.Player!.Id);
        Assert.False(CombatDecisionResolver.HasCombatLineOfSight(world, world.LocalPlayer.X, world.LocalPlayer.Y, enemy.X, enemy.Y));

        var fireDecision = CombatDecisionResolver.Resolve(
            world,
            world.LocalPlayer,
            target,
            healTarget: null,
            new CombatDecisionMemory());
        Assert.False(fireDecision.FirePrimary);
        Assert.False(fireDecision.FireSecondary);
        Assert.False(fireDecision.UseAbility);
    }

    [Fact]
    public void BotPursuesDistantEnemyWithoutFiringUntilWithinPracticalRange()
    {
        var world = CreateWorld(roomObjects: [], solids: []);
        Assert.True(world.TrySetNetworkPlayerTeam(SimulationWorld.LocalPlayerSlot, PlayerTeam.Red));
        world.ForceRespawnLocalPlayer();
        world.LocalPlayer.TeleportTo(100f, 100f);
        var enemy = AddNetworkPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Blue, 700f, 100f);

        var target = TargetSelector.SelectCombatTarget(world.LocalPlayer, world, PlayerTeam.Red);
        var fireDecision = CombatDecisionResolver.Resolve(
            world,
            world.LocalPlayer,
            target,
            healTarget: null,
            new CombatDecisionMemory());

        Assert.NotNull(target);
        Assert.Equal(enemy.Id, target!.Value.Player!.Id);
        Assert.True(CombatDecisionResolver.HasCombatLineOfSight(world, world.LocalPlayer.X, world.LocalPlayer.Y, enemy.X, enemy.Y));
        Assert.False(fireDecision.FirePrimary);
        Assert.False(fireDecision.FireSecondary);
        Assert.False(fireDecision.UseAbility);
    }

    [Fact]
    public void BotFiresWhenEnemyIsVisibleAndWithinPracticalRange()
    {
        var world = CreateWorld(roomObjects: [], solids: []);
        Assert.True(world.TrySetNetworkPlayerTeam(SimulationWorld.LocalPlayerSlot, PlayerTeam.Red));
        world.ForceRespawnLocalPlayer();
        world.LocalPlayer.TeleportTo(100f, 100f);
        _ = AddNetworkPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Blue, 400f, 100f);

        var target = TargetSelector.SelectCombatTarget(world.LocalPlayer, world, PlayerTeam.Red);
        var fireDecision = CombatDecisionResolver.Resolve(
            world,
            world.LocalPlayer,
            target,
            healTarget: null,
            new CombatDecisionMemory());

        Assert.NotNull(target);
        Assert.True(fireDecision.FirePrimary);
    }

    [Fact]
    public void BotNearestEnemyFallbackCanAcquireEnemyBehindSpawnDoor()
    {
        var world = CreateWorld(
            roomObjects:
            [
                new RoomObjectMarker(RoomObjectType.TeamGate, 200f, 90f, 32f, 60f, "RedGate", PlayerTeam.Red),
            ],
            solids: []);
        Assert.True(world.TrySetNetworkPlayerTeam(SimulationWorld.LocalPlayerSlot, PlayerTeam.Red));
        world.ForceRespawnLocalPlayer();
        world.LocalPlayer.TeleportTo(100f, 100f);
        var enemy = AddNetworkPlayer(world, 2, PlayerClass.Scout, PlayerTeam.Blue, 400f, 100f);
        var method = typeof(BotBrainController).GetMethod("TryFindNearestEnemyPlayer", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        object?[] args = [world, world.LocalPlayer, PlayerTeam.Red, float.PositiveInfinity, null];

        var found = (bool)method!.Invoke(null, args)!;

        Assert.True(found);
        Assert.NotNull(args[4]);
        Assert.Equal(enemy.Id, ((PlayerEntity)args[4]!).Id);
    }

    [Fact]
    public void BotTargetSelectionDoesNotTreatBulletWallsAsScreenVisibilityBlockers()
    {
        var world = CreateWorld(
            roomObjects:
            [
                new RoomObjectMarker(RoomObjectType.BulletWall, 200f, 90f, 32f, 60f, "KulayBulletWall"),
            ],
            solids: []);
        Assert.True(world.TrySetNetworkPlayerTeam(SimulationWorld.LocalPlayerSlot, PlayerTeam.Red));
        world.ForceRespawnLocalPlayer();
        world.LocalPlayer.TeleportTo(100f, 100f);
        var enemy = AddNetworkPlayer(world, 2, PlayerClass.Scout, PlayerTeam.Blue, 400f, 100f);

        var target = TargetSelector.SelectCombatTarget(world.LocalPlayer, world, PlayerTeam.Red);

        Assert.NotNull(target);
        Assert.Equal(enemy.Id, target!.Value.Player!.Id);
        Assert.False(CombatDecisionResolver.HasCombatLineOfSight(world, world.LocalPlayer.X, world.LocalPlayer.Y, enemy.X, enemy.Y));
    }

    private static SimulationWorld CreateWorld(
        IReadOnlyList<RoomObjectMarker> roomObjects,
        IReadOnlyList<LevelSolid> solids)
    {
        var world = new SimulationWorld(new SimulationConfig
        {
            EnableLocalDummies = false,
            EnableEnemyTrainingDummy = false,
            EnableFriendlySupportDummy = false,
        });
        var method = typeof(SimulationWorld).GetMethod("CombatTestSetLevel", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        _ = method!.Invoke(
            world,
            [
                new SimpleLevel(
                    name: "botbrain_los_test",
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
                    roomObjects: roomObjects,
                    floorY: 2048f,
                    solids: solids,
                    importedFromSource: false),
            ]);
        return world;
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
        player.RestoreMovementProbeState(isGrounded: true, remainingAirJumps: null, facingDirectionX: -1f);
        return player;
    }
}
