using System.Reflection;
using OpenGarrison.Core;
using OpenGarrison.Core.LastToDie;
using OpenGarrison.Protocol;
using OpenGarrison.Server;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class LastToDieMedicKritzM2RuntimeTests
{
    [Fact]
    public void KritzM2CapturesHailAndNeuroPayloadAtSpawn()
    {
        var world = CreateWorld();
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Medic.HailMary, LastToDiePerkIds.Medic.Neurotoxin]));

        var needle = SpawnKritzM2(world, x: 120f, y: 100f, velocityX: 10f);
        Assert.True(needle.LastToDiePayload.IsMedicKritzM2);
        Assert.True(needle.AppliesLastToDieHailMary);
        Assert.True(needle.AppliesLastToDieNeurotoxin);
        Assert.Equal(0b111, needle.LastToDiePayload.Encode());

        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            []));

        Assert.True(needle.AppliesLastToDieHailMary);
        Assert.True(needle.AppliesLastToDieNeurotoxin);
    }

    [Fact]
    public void HailMaryAppliesAtFullHealthRefreshesAndUsesDedicatedInvulnerability()
    {
        var world = CreateWorld();
        var target = AddPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Red);
        target.ForceSetAmmo(target.MaxShells - 5);
        var ammoBefore = target.CurrentShells;
        target.IgniteAfterburn(
            ownerPlayerId: 999,
            durationIncreaseSourceTicks: 60f,
            intensityIncrease: PlayerEntity.BurnMaxIntensity,
            afterburnFalloff: false,
            burnFalloffAmount: 0f);
        var needle = new MedicHealNeedleProjectileEntity(
            100,
            PlayerTeam.Red,
            world.LocalPlayer.Id,
            100f,
            100f,
            1f,
            0f,
            lastToDiePayload: LastToDieMedicKritzM2Payload.Create(
                appliesHailMary: true,
                appliesNeurotoxin: false));

        InvokeMedicHealNeedleTeammateHit(world, world.LocalPlayer, target, needle);

        var expectedTicks = (int)Math.Ceiling(
            LastToDieDerivedModifiers.MedicHailMaryInvulnerabilitySeconds
                * world.Config.TicksPerSecond);
        Assert.Equal(target.MaxHealth, target.Health);
        Assert.Equal(ammoBefore, target.CurrentShells);
        Assert.Equal(expectedTicks, target.LastToDieMedicHailMaryTicksRemaining);
        Assert.True(target.IsLastToDieMedicHailMaryInvulnerable);
        Assert.False(target.IsUbered);
        Assert.True(world.CanPlayerContributeToControlPoint(target));

        var healthBefore = target.Health;
        Assert.False(target.ApplyDamage(50));
        Assert.False(target.ApplyContinuousDamage(50f));
        var afterburn = target.AdvanceAfterburn(
            1f / LegacyMovementModel.SourceTicksPerSecond);
        Assert.False(afterburn.IsFatal);
        Assert.Equal(healthBefore, target.Health);

        target.AdvanceTickState(default, 1d / world.Config.TicksPerSecond);
        Assert.Equal(expectedTicks - 1, target.LastToDieMedicHailMaryTicksRemaining);
        InvokeMedicHealNeedleTeammateHit(world, world.LocalPlayer, target, needle);
        Assert.Equal(expectedTicks, target.LastToDieMedicHailMaryTicksRemaining);
        Assert.True(target.SetServerStunTicks(60));

        var predictionShadow = new PlayerEntity(
            90,
            CharacterClassCatalog.Heavy,
            "prediction-shadow");
        predictionShadow.RestorePredictionState(target.CapturePredictionState());
        Assert.Equal(expectedTicks, predictionShadow.LastToDieMedicHailMaryTicksRemaining);
        Assert.Equal(60, predictionShadow.ServerStunTicksRemaining);

        var legacyShadow = new PlayerEntity(
            91,
            CharacterClassCatalog.Heavy,
            "legacy-shadow");
        legacyShadow.ReplaceReplicatedStateEntries(target.GetReplicatedStateEntries());
        Assert.Equal(expectedTicks, legacyShadow.LastToDieMedicHailMaryTicksRemaining);
        Assert.Equal(60, legacyShadow.ServerStunTicksRemaining);
    }

    [Fact]
    public void NeurotoxinTriggeringHitIsBaseAndPreStunnedHitIsTripleDamage()
    {
        var world = CreateWorld();
        var enemy = AddPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Blue);
        world.LocalPlayer.TeleportTo(100f, 100f);
        enemy.TeleportTo(180f, 100f);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Medic.Neurotoxin]));

        var healthBefore = enemy.Health;
        _ = SpawnKritzM2(world, x: 120f, y: 100f, velocityX: 20f);
        AdvanceUntilNeedlesAreGone(world);

        Assert.Equal(
            MedicHealNeedleProjectileEntity.DefaultEnemyDamagePerHit,
            healthBefore - enemy.Health);
        Assert.True(enemy.IsServerStunned);
        var firstStatus = Assert.Single(world.GetLastToDieStatusEffects(enemy.Id));
        Assert.Equal(LastToDieStatusEffectIds.MedicNeurotoxinStun, firstStatus.Id);
        Assert.Equal(world.LocalPlayer.Id, firstStatus.SourcePlayerId);

        var healthBeforeSecondHit = enemy.Health;
        _ = SpawnKritzM2(world, x: 120f, y: 100f, velocityX: 20f);
        AdvanceUntilNeedlesAreGone(world);

        Assert.Equal(
            MedicHealNeedleProjectileEntity.DefaultEnemyDamagePerHit
                * LastToDieDerivedModifiers.MedicNeurotoxinPreStunnedDamageMultiplier,
            healthBeforeSecondHit - enemy.Health);
        var refreshed = Assert.Single(world.GetLastToDieStatusEffects(enemy.Id));
        Assert.InRange(
            refreshed.RemainingTicks,
            (world.Config.TicksPerSecond * LastToDieDerivedModifiers.MedicNeurotoxinStunSeconds) - 1,
            world.Config.TicksPerSecond * LastToDieDerivedModifiers.MedicNeurotoxinStunSeconds);
    }

    [Fact]
    public void NeurotoxinDoesNotStunInvulnerableOrFullyShieldedTargets()
    {
        var invulnerableWorld = CreateWorld();
        var invulnerable = AddPlayer(
            invulnerableWorld,
            2,
            PlayerClass.Heavy,
            PlayerTeam.Blue);
        invulnerableWorld.LocalPlayer.TeleportTo(100f, 100f);
        invulnerable.TeleportTo(180f, 100f);
        Assert.True(invulnerableWorld.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Medic.Neurotoxin]));
        invulnerable.RefreshUber(invulnerableWorld.Config.TicksPerSecond);
        _ = SpawnKritzM2(invulnerableWorld, x: 120f, y: 100f, velocityX: 20f);
        AdvanceUntilNeedlesAreGone(invulnerableWorld);
        Assert.False(invulnerable.IsServerStunned);
        Assert.Empty(invulnerableWorld.GetLastToDieStatusEffects(invulnerable.Id));

        var shieldWorld = CreateWorld();
        var shielded = AddPlayer(shieldWorld, 2, PlayerClass.Heavy, PlayerTeam.Blue);
        shieldWorld.LocalPlayer.TeleportTo(100f, 100f);
        shielded.TeleportTo(180f, 100f);
        shielded.SetExperimentalShieldHealth(100f);
        Assert.True(shieldWorld.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Medic.Neurotoxin]));
        _ = SpawnKritzM2(shieldWorld, x: 120f, y: 100f, velocityX: 20f);
        AdvanceUntilNeedlesAreGone(shieldWorld);
        Assert.False(shielded.IsServerStunned);
        Assert.Empty(shieldWorld.GetLastToDieStatusEffects(shielded.Id));
    }

    [Fact]
    public void Protocol64CarriesKritzPayloadHailAndStunThroughStateLifecycleAndResync()
    {
        var source = CreateWorld();
        Assert.True(source.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Medic.HailMary, LastToDiePerkIds.Medic.Neurotoxin]));
        Assert.True(source.LocalPlayer.RefreshLastToDieMedicHailMaryInvulnerability(15));
        Assert.True(source.LocalPlayer.SetServerStunTicks(60));
        var original = SpawnKritzM2(source, x: 120f, y: 100f, velocityX: 10f);
        var publisher = new Protocol64StatePublisher(source);

        var playerState = Assert.Single(publisher.BuildPlayerStateBatch(10).Players);
        Assert.Equal((ushort)15, playerState.LastToDieMedicHailMaryTicksRemaining);
        Assert.Equal((ushort)60, playerState.ServerStunTicksRemaining);

        var projectileState = Assert.Single(publisher.BuildProjectileStates(10));
        Assert.Equal((byte)0b111, projectileState.LastToDieMedicKritzM2Payload);

        var receiver = new SimulationWorld(new SimulationConfig
        {
            EnableLocalDummies = false,
        });
        Assert.True(receiver.ApplyProtocol64PlayerState(playerState));
        Assert.True(receiver.ApplyProtocol64ProjectileState(projectileState));
        Assert.Equal(15, receiver.LocalPlayer.LastToDieMedicHailMaryTicksRemaining);
        Assert.Equal(60, receiver.LocalPlayer.ServerStunTicksRemaining);
        var recreated = Assert.IsType<MedicHealNeedleProjectileEntity>(
            Assert.Single(receiver.Needles));
        Assert.Equal(original.LastToDiePayload, recreated.LastToDiePayload);

        var resync = publisher.BuildResyncResponse(
            new Protocol64StateResyncRequest(
                RequestId: 7,
                LastPlayerStateSequence: 0,
                LastProjectileStateSequence: 0,
                LastStateTick: 0,
                Reason: Protocol64StateResyncReason.ClientRequested),
            stateTick: 11);
        var resyncPlayer = Assert.Single(resync.Players);
        Assert.Equal((ushort)15, resyncPlayer.LastToDieMedicHailMaryTicksRemaining);
        Assert.Equal((ushort)60, resyncPlayer.ServerStunTicksRemaining);
        Assert.Equal((byte)0b111, Assert.Single(resync.Projectiles).LastToDieMedicKritzM2Payload);

        original.Destroy();
        source.AdvanceOneTick();
        Assert.Empty(publisher.BuildProjectileStates(12));
        var lifecycle = Assert.Single(publisher.BuildProjectileLifecycleEvents());
        Assert.Equal(Protocol64ProjectileLifecycleKind.Despawn, lifecycle.Lifecycle);
        Assert.Equal((byte)0b111, lifecycle.LastToDieMedicKritzM2Payload);
    }

    private static SimulationWorld CreateWorld()
    {
        var world = new SimulationWorld(new SimulationConfig
        {
            EnableLocalDummies = false,
            EnableEnemyTrainingDummy = false,
            EnableFriendlySupportDummy = false,
        });
        world.CombatTestSetLevel(new SimpleLevel(
            name: "ltd_medic_kritz_m2_test",
            mode: GameModeKind.CaptureTheFlag,
            bounds: new WorldBounds(640f, 480f),
            mapScale: 1f,
            backgroundAssetName: null,
            mapAreaIndex: 1,
            mapAreaCount: 1,
            localSpawn: new SpawnPoint(100f, 100f),
            redSpawns: [new SpawnPoint(100f, 100f)],
            blueSpawns: [new SpawnPoint(500f, 100f)],
            intelBases: [],
            roomObjects: [],
            floorY: 400f,
            solids: [new LevelSolid(0f, 400f, 640f, 80f)],
            importedFromSource: false));
        world.PrepareLocalPlayerJoin();
        world.CompleteLocalPlayerJoin(PlayerClass.Medic);
        world.LocalPlayer.SetSpawnRoomState(false);
        return world;
    }

    private static PlayerEntity AddPlayer(
        SimulationWorld world,
        byte slot,
        PlayerClass playerClass,
        PlayerTeam team)
    {
        Assert.True(world.TryPrepareNetworkPlayerJoin(slot));
        Assert.True(world.TrySetNetworkPlayerTeam(slot, team));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(slot, playerClass));
        Assert.True(world.TryGetNetworkPlayer(slot, out var player));
        player.SetSpawnRoomState(false);
        return player;
    }

    private static MedicHealNeedleProjectileEntity SpawnKritzM2(
        SimulationWorld world,
        float x,
        float y,
        float velocityX)
    {
        var method = typeof(SimulationWorld).GetMethod(
            "SpawnMedicHealNeedle",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        _ = method!.Invoke(
            world,
            [
                world.LocalPlayer,
                x,
                y,
                velocityX,
                0f,
                MedicHealNeedleProjectileEntity.DefaultHealPerHit,
                MedicHealNeedleProjectileEntity.DefaultEnemyDamagePerHit,
            ]);
        return Assert.IsType<MedicHealNeedleProjectileEntity>(world.Needles[^1]);
    }

    private static void InvokeMedicHealNeedleTeammateHit(
        SimulationWorld world,
        PlayerEntity medic,
        PlayerEntity target,
        MedicHealNeedleProjectileEntity needle)
    {
        var method = typeof(SimulationWorld).GetMethod(
            "ApplyMedicHealNeedleTeammateHit",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        _ = method!.Invoke(world, [medic, target, needle]);
    }

    private static void AdvanceUntilNeedlesAreGone(SimulationWorld world)
    {
        for (var tick = 0; tick < 10 && world.Needles.Count > 0; tick += 1)
        {
            world.AdvanceOneTick();
        }

        Assert.Empty(world.Needles);
    }
}
