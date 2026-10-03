using OpenGarrison.Core;
using OpenGarrison.Core.BotBrain;
using OpenGarrison.GameplayModding;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class CombatPlaytestFixesTests
{
    [Fact]
    public void CenteredExplosiveSplashDestroysFullHealthNeutralJumpPad()
    {
        var world = CreateWorldWithNeutralJumpPad();
        world.ExplosionRules.ApplyExplosiveDamageToJumpPads(
            256f,
            256f,
            MineProjectileEntity.BlastRadius,
            MineProjectileEntity.BaseExplosionDamage,
            PlayerTeam.Red);

        Assert.Empty(world.JumpPads);
    }






    [Fact]
    public void ScopedRifleClearsScopeWhenSwitchingToSmg()
    {
        var world = CreateCombatWorld(PlayerClass.Sniper);
        var sniper = world.LocalPlayer;
        Assert.True(sniper.TryToggleSniperScope());
        Assert.True(sniper.IsSniperScoped);

        Assert.True(world.NetworkPlayerRules.TrySetNetworkPlayerGameplayEquippedSlot(
            SimulationWorld.LocalPlayerSlot,
            GameplayEquipmentSlot.Secondary));

        Assert.False(sniper.IsSniperScoped);
        Assert.Equal(0, sniper.SniperChargeTicks);
    }

    [Fact]
    public void DispenserDoesNotStealOverlappingPlayerStab()
    {
        var world = CreateCombatWorld(PlayerClass.Spy, openLevel: true);
        var spy = world.LocalPlayer;
        spy.TeleportTo(0f, 0f);
        var target = AddNetworkPlayer(world, 2, PlayerClass.Scout, PlayerTeam.Blue, 24f, 0f);
        world.CombatTestAddSentry(new SentryEntity(100, 2, PlayerTeam.Blue, 8f, 0f, 1f, isDispenser: true));
        var mask = new StabMaskEntity(101, spy.Id, spy.Team, spy.X, spy.Y, directionDegrees: 0f);

        var hit = Assert.NotNull(world.CombatTestGetNearestStabHit(mask, 1f, 0f));
        Assert.Same(target, hit.HitPlayer);
        Assert.Null(hit.HitSentry);
    }

    [Fact]
    public void RetreatingFlareKeepsStationaryForwardComponent()
    {
        var world = CreateCombatWorld(PlayerClass.Pyro);
        var pyro = world.LocalPlayer;
        Assert.True(world.NetworkPlayerRules.TrySetNetworkPlayerGameplayEquippedSlot(
            SimulationWorld.LocalPlayerSlot,
            GameplayEquipmentSlot.Secondary));
        pyro.AddImpulse(-600f, 0f);
        world.NetworkPlayerRules.SetLocalInput(default(PlayerInputSnapshot) with
        {
            FirePrimary = true,
            AimWorldX = pyro.X + 256f,
            AimWorldY = pyro.Y,
        });
        world.AdvanceOneTick();

        var flare = Assert.Single(world.Flares, candidate => candidate.OwnerId == pyro.Id);
        Assert.True(flare.VelocityX >= 14.9f);
    }

    private static SimulationWorld CreateWorldWithNeutralJumpPad()
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        var spawn = new SpawnPoint(256f, 256f);
        world.CombatTestSetLevel(new SimpleLevel(
            name: "combat-playtest-fixes-test",
            mode: GameModeKind.TeamDeathmatch,
            bounds: new WorldBounds(512f, 512f),
            mapScale: 1f,
            backgroundAssetName: null,
            mapAreaIndex: 1,
            mapAreaCount: 1,
            localSpawn: spawn,
            redSpawns: [spawn],
            blueSpawns: [spawn],
            intelBases: [],
            roomObjects: [],
            floorY: 512f,
            solids: [],
            importedFromSource: false,
            jumpPadSpawns: [new JumpPadSpawnMarker(spawn.X, spawn.Y)]));
        return world;
    }

    private static SimulationWorld CreateCombatWorld(
        PlayerClass playerClass,
        bool openLevel = false,
        int ticksPerSecond = 60)
    {
        var world = new SimulationWorld(new SimulationConfig
        {
            EnableLocalDummies = false,
            TicksPerSecond = ticksPerSecond,
        });
        if (openLevel)
        {
            world.CombatTestSetLevel(new SimpleLevel(
                name: "combat-playtest-fixes-open",
                mode: GameModeKind.TeamDeathmatch,
                bounds: new WorldBounds(2048f, 1024f),
                mapScale: 1f,
                backgroundAssetName: null,
                mapAreaIndex: 1,
                mapAreaCount: 1,
                localSpawn: new SpawnPoint(256f, 256f),
                redSpawns: [new SpawnPoint(256f, 256f)],
                blueSpawns: [new SpawnPoint(768f, 256f)],
                intelBases: [],
                roomObjects:
                [
                    // Place the player in a primary-swap station. This
                    // makes a SwapWeapon edge cycle the primary loadout,
                    // exposing accidental use of that edge for offhand
                    // selection.
                    new RoomObjectMarker(
                        RoomObjectType.HealingCabinet,
                        240f,
                        230f,
                        32f,
                        52f,
                        "healing-cabinet"),
                ],
                floorY: 1024f,
                solids: [],
                importedFromSource: false));
        }
        world.NetworkPlayerRules.PrepareLocalPlayerJoin();
        world.NetworkPlayerRules.CompleteLocalPlayerJoin(playerClass);
        world.LocalPlayer.SetSpawnRoomState(false);
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
        Assert.True(world.NetworkPlayerRules.TryPrepareNetworkPlayerJoin(slot));
        Assert.True(world.NetworkPlayerRules.TrySetNetworkPlayerTeam(slot, team));
        Assert.True(world.NetworkPlayerRules.TryApplyNetworkPlayerClassSelection(slot, playerClass));
        Assert.True(world.NetworkPlayerRules.TryGetNetworkPlayer(slot, out var player));
        player.TeleportTo(x, y);
        return player;
    }

    private static BotBrainCombatTarget CreateCombatTarget(PlayerEntity target)
        => new(BotBrainCombatTargetKind.Player, target.Team, target.X, target.Y, Player: target);
}
