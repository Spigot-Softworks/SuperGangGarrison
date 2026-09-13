using System.Reflection;
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
        world.ApplyExplosiveDamageToJumpPads(
            256f,
            256f,
            MineProjectileEntity.BlastRadius,
            MineProjectileEntity.BaseExplosionDamage,
            PlayerTeam.Red);

        Assert.Empty(world.JumpPads);
    }

    [Fact]
    public void SpyAwarenessSamplesOneGlobalDelayForEachFreshAcquisition()
    {
        var world = CreateCombatWorld(PlayerClass.Pyro, openLevel: true);
        var bot = world.LocalPlayer;
        var spy = AddNetworkPlayer(world, 2, PlayerClass.Spy, PlayerTeam.Blue, bot.X + 320f, bot.Y);
        Assert.True(spy.TryToggleSpyCloak());
        spy.RevealSpy(PlayerEntity.SpyDamageRevealAlpha);

        var selected = new BotBrainCombatTarget(
            BotBrainCombatTargetKind.Player,
            spy.Team,
            spy.X,
            spy.Y,
            Player: spy);
        var memory = new CombatDecisionMemory();

        Assert.Null(CombatDecisionResolver.ApplySpyAwarenessDelay(world, bot, selected, memory));
        var sampledDelay = memory.SpyAwarenessDelayMilliseconds;
        Assert.InRange(sampledDelay, 150, 350);
        Assert.Equal(spy.Id, memory.SpyAwarenessTargetId);
        var remainingAfterAcquisition = memory.SpyAwarenessTicksRemaining;
        Assert.Null(CombatDecisionResolver.ApplySpyAwarenessDelay(world, bot, null, memory));
        Assert.Equal(remainingAfterAcquisition, memory.SpyAwarenessTicksRemaining);

        var observed = (BotBrainCombatTarget?)null;
        for (var tick = 0; tick < 30 && observed is null; tick += 1)
        {
            observed = CombatDecisionResolver.ApplySpyAwarenessDelay(world, bot, selected, memory);
            Assert.Equal(sampledDelay, memory.SpyAwarenessDelayMilliseconds);
            if (observed is null)
            {
                world.AdvanceOneTick();
            }
        }

        Assert.True(observed.HasValue);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(120)]
    public void SpyAwarenessDelayUsesOnlyDurationsWithinGlobalWindow(int ticksPerSecond)
    {
        var world = new SimulationWorld(new SimulationConfig
        {
            EnableLocalDummies = false,
            TicksPerSecond = ticksPerSecond,
        });

        for (var sample = 0; sample < 100; sample += 1)
        {
            var delayTicks = world.NextBotSpyAwarenessDelayTicks(ticksPerSecond);
            var delayMilliseconds = delayTicks * 1000f / ticksPerSecond;
            Assert.InRange(delayMilliseconds, 150f, 350f);
        }
    }

    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(120)]
    public void SpyAwarenessUsesSimulationFramesAcrossThrottledDecisionPasses(int ticksPerSecond)
    {
        var world = CreateCombatWorld(PlayerClass.Pyro, openLevel: true, ticksPerSecond: ticksPerSecond);
        var bot = world.LocalPlayer;
        var spy = AddNetworkPlayer(world, 2, PlayerClass.Spy, PlayerTeam.Blue, bot.X + 320f, bot.Y);
        Assert.True(spy.TryToggleSpyCloak());
        spy.RevealSpy(PlayerEntity.SpyDamageRevealAlpha);
        var selected = CreateCombatTarget(spy);
        var memory = new CombatDecisionMemory();

        Assert.Null(CombatDecisionResolver.ApplySpyAwarenessDelay(world, bot, selected, memory));
        var acquisitionFrame = world.Frame;
        var acquisitionSerial = memory.SpyAwarenessAcquisitionSerial;
        var observed = (BotBrainCombatTarget?)null;
        for (var tick = 0; tick <= ticksPerSecond; tick += 1)
        {
            world.AdvanceOneTick();
            observed = CombatDecisionResolver.ApplySpyAwarenessDelay(world, bot, selected, memory);
            if (observed is not null)
            {
                break;
            }
        }

        Assert.True(observed.HasValue);
        Assert.Equal(acquisitionSerial, memory.SpyAwarenessAcquisitionSerial);
        var elapsedMilliseconds = (world.Frame - acquisitionFrame) * 1000f / ticksPerSecond;
        Assert.InRange(elapsedMilliseconds, 150f, 350f);
    }

    [Fact]
    public void HeavyKeepsMinigunPreferenceThroughCooldownAndDwell()
    {
        var world = CreateCombatWorld(PlayerClass.Heavy, openLevel: true);
        var heavy = world.LocalPlayer;
        var target = AddNetworkPlayer(world, 2, PlayerClass.Scout, PlayerTeam.Blue, heavy.X + 140f, heavy.Y);
        var combatTarget = CreateCombatTarget(target);
        var memory = new CombatDecisionMemory();

        // Close range selects the shotgun once, regardless of minigun firing
        // cooldown eligibility. After the actual transition, moving through
        // the hysteresis band cannot immediately select the minigun again.
        var decision = CombatDecisionResolver.Resolve(world, heavy, combatTarget, null, memory);
        Assert.True(decision.SelectSecondaryWeapon);
        heavy.EquipExperimentalOffhandWeapon();
        target.TeleportTo(heavy.X + 260f, heavy.Y);
        combatTarget = CreateCombatTarget(target);

        for (var tick = 0; tick < (int)MathF.Ceiling(world.Config.TicksPerSecond * 0.5f); tick += 1)
        {
            decision = CombatDecisionResolver.Resolve(world, heavy, combatTarget, null, memory);
            Assert.True(decision.SelectSecondaryWeapon);
            world.AdvanceOneTick();
        }

        target.TeleportTo(heavy.X + 360f, heavy.Y);
        decision = CombatDecisionResolver.Resolve(world, heavy, CreateCombatTarget(target), null, memory);
        Assert.False(decision.SelectSecondaryWeapon);
    }

    [Fact]
    public void BotOffhandIntentUsesDedicatedToggleAtPrimaryLocker()
    {
        var world = CreateCombatWorld(PlayerClass.Heavy, openLevel: true);
        var heavy = world.LocalPlayer;
        heavy.StowExperimentalOffhandWeapon();
        var primaryBeforeToggle = heavy.SelectedGameplayPrimaryItemId;
        var combat = new CombatFireDecision(
            FirePrimary: false,
            FireSecondary: false,
            UseAbility: false,
            SelectSecondaryWeapon: true);

        var input = BotInputSynthesizer.Synthesize(
            heavy,
            default,
            heavy.X + 100f,
            heavy.Y,
            combat,
            default);

        Assert.True(input.ToggleSecondaryWeapon);
        Assert.False(input.SwapWeapon);
        world.SetLocalInput(input);
        world.AdvanceOneTick();

        Assert.True(heavy.IsExperimentalOffhandSelected);
        Assert.Equal(primaryBeforeToggle, heavy.SelectedGameplayPrimaryItemId);
    }

    [Fact]
    public void ScopedRifleClearsScopeWhenSwitchingToSmg()
    {
        var world = CreateCombatWorld(PlayerClass.Sniper);
        var sniper = world.LocalPlayer;
        Assert.True(sniper.TryToggleSniperScope());
        Assert.True(sniper.IsSniperScoped);

        Assert.True(world.TrySetNetworkPlayerGameplayEquippedSlot(
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

        var hitMethod = typeof(SimulationWorld).GetMethod(
            "CombatTestGetNearestStabHit",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(hitMethod);
        var result = hitMethod!.Invoke(world, [mask, 1f, 0f]);
        Assert.NotNull(result);
        var hit = ((float Distance, float HitX, float HitY, PlayerEntity? HitPlayer, SentryEntity? HitSentry, int HitDamageableZoneRoomObjectIndex))result!;
        Assert.Same(target, hit.HitPlayer);
        Assert.Null(hit.HitSentry);
    }

    [Fact]
    public void RetreatingFlareKeepsStationaryForwardComponent()
    {
        var world = CreateCombatWorld(PlayerClass.Pyro);
        var pyro = world.LocalPlayer;
        Assert.True(world.TrySetNetworkPlayerGameplayEquippedSlot(
            SimulationWorld.LocalPlayerSlot,
            GameplayEquipmentSlot.Secondary));
        pyro.AddImpulse(-600f, 0f);
        world.SetLocalInput(default(PlayerInputSnapshot) with
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
        var setLevel = typeof(SimulationWorld).GetMethod("CombatTestSetLevel", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(setLevel);
        setLevel!.Invoke(world,
        [
            new SimpleLevel(
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
                jumpPadSpawns: [new JumpPadSpawnMarker(spawn.X, spawn.Y)]),
        ]);
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
            var setLevel = typeof(SimulationWorld).GetMethod("CombatTestSetLevel", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(setLevel);
            setLevel!.Invoke(world,
            [
                new SimpleLevel(
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
                    importedFromSource: false),
            ]);
        }
        world.PrepareLocalPlayerJoin();
        world.CompleteLocalPlayerJoin(playerClass);
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
        Assert.True(world.TryPrepareNetworkPlayerJoin(slot));
        Assert.True(world.TrySetNetworkPlayerTeam(slot, team));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(slot, playerClass));
        Assert.True(world.TryGetNetworkPlayer(slot, out var player));
        player.TeleportTo(x, y);
        return player;
    }

    private static BotBrainCombatTarget CreateCombatTarget(PlayerEntity target)
        => new(BotBrainCombatTargetKind.Player, target.Team, target.X, target.Y, Player: target);
}
