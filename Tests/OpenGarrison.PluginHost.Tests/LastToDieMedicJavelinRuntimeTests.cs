using System.Reflection;
using OpenGarrison.Core;
using OpenGarrison.Core.LastToDie;
using OpenGarrison.Protocol;
using OpenGarrison.Server;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class LastToDieMedicJavelinRuntimeTests
{
    [Fact]
    public void SpawnCapturesJavelinAndUsesSpawnRelativeFuse()
    {
        var world = CreateWorld();
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Medic.Javelin]));

        var javelin = SpawnKritzM2(world, world.LocalPlayer, 120f, 100f, 10f, 0f);
        var expectedFuse = (int)Math.Ceiling(
            LastToDieDerivedModifiers.MedicJavelinFuseSeconds
                * world.Config.TicksPerSecond);
        Assert.True(javelin.AppliesLastToDieJavelin);
        Assert.Equal(0b1001, javelin.LastToDiePayload.Encode());
        Assert.Equal(expectedFuse, javelin.LastToDieJavelinFuseTicksRemaining);

        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            []));
        Assert.True(javelin.AppliesLastToDieJavelin);
    }

    [Fact]
    public void InFlightFuseExpiryExplodesExactlyOnceAtCurrentLocation()
    {
        var world = CreateWorld();
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Medic.Javelin]));
        var javelin = SpawnKritzM2(world, world.LocalPlayer, 120f, 40f, 3f, 0f);
        var fuse = javelin.LastToDieJavelinFuseTicksRemaining;
        _ = world.DrainPendingVisualEvents();

        for (var tick = 0; tick < fuse - 1; tick += 1)
        {
            world.AdvanceOneTick();
        }

        Assert.Contains(javelin, world.Needles);
        Assert.False(javelin.IsLastToDieJavelinAnchored);
        Assert.False(javelin.HasLastToDieJavelinExploded);
        var expectedExplosionX = javelin.X + javelin.VelocityX;
        var expectedExplosionY = javelin.Y + javelin.VelocityY;

        world.AdvanceOneTick();

        Assert.DoesNotContain(javelin, world.Needles);
        Assert.True(javelin.HasLastToDieJavelinExploded);
        var explosion = Assert.Single(
            world.DrainPendingVisualEvents(),
            static visual => visual.EffectName == "Explosion");
        Assert.Equal(expectedExplosionX, explosion.X, precision: 4);
        Assert.Equal(expectedExplosionY, explosion.Y, precision: 4);

        world.AdvanceOneTick();
        Assert.DoesNotContain(
            world.DrainPendingVisualEvents(),
            static visual => visual.EffectName == "Explosion");
    }

    [Fact]
    public void PlayerContactAppliesDirectHitOnceAnchorsAndDoesNotRestartFuse()
    {
        var world = CreateWorld();
        var enemy = AddPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Blue);
        world.LocalPlayer.TeleportTo(100f, 100f);
        enemy.TeleportTo(180f, 100f);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Medic.Javelin]));
        var javelin = SpawnKritzM2(world, world.LocalPlayer, 120f, 100f, 20f, 0f);
        var initialFuse = javelin.LastToDieJavelinFuseTicksRemaining;
        var healthBefore = enemy.Health;

        AdvanceUntilAnchored(world, javelin);

        Assert.Equal(
            MedicHealNeedleProjectileEntity.DefaultEnemyDamagePerHit,
            healthBefore - enemy.Health);
        Assert.True(javelin.IsLastToDieJavelinAnchored);
        Assert.InRange(javelin.LastToDieJavelinFuseTicksRemaining, 1, initialFuse - 1);
        var anchoredX = javelin.X;
        var anchoredY = javelin.Y;
        var anchoredFuse = javelin.LastToDieJavelinFuseTicksRemaining;
        enemy.TeleportTo(anchoredX, anchoredY);

        Assert.True(InvokeExplodeJavelin(world, javelin));
        Assert.False(InvokeExplodeJavelin(world, javelin));

        Assert.Equal(
            MedicHealNeedleProjectileEntity.DefaultEnemyDamagePerHit
                + LastToDieDerivedModifiers.MedicJavelinEnemyCenterDamage,
            healthBefore - enemy.Health);
        Assert.Equal(anchoredX, javelin.X);
        Assert.Equal(anchoredY, javelin.Y);
        Assert.InRange(anchoredFuse, 1, initialFuse - 1);
        Assert.Equal(
            1,
            world.PendingVisualEvents.Count(static visual => visual.EffectName == "Explosion"));
    }

    [Fact]
    public void GeometryContactAnchorsWithoutRestartingFuse()
    {
        var world = CreateWorld(
            new LevelSolid(200f, 0f, 20f, 400f));
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Medic.Javelin]));
        var javelin = SpawnKritzM2(world, world.LocalPlayer, 120f, 100f, 30f, 0f);
        var initialFuse = javelin.LastToDieJavelinFuseTicksRemaining;

        AdvanceUntilAnchored(world, javelin);

        Assert.True(javelin.IsLastToDieJavelinAnchored);
        Assert.InRange(javelin.X, 190f, 200f);
        Assert.Equal(0f, javelin.VelocityX);
        Assert.Equal(0f, javelin.VelocityY);
        Assert.InRange(javelin.LastToDieJavelinFuseTicksRemaining, 1, initialFuse - 1);
        var anchoredX = javelin.X;
        var anchoredY = javelin.Y;
        var fuseBeforeTick = javelin.LastToDieJavelinFuseTicksRemaining;

        world.AdvanceOneTick();

        Assert.Equal(anchoredX, javelin.X);
        Assert.Equal(anchoredY, javelin.Y);
        Assert.Equal(fuseBeforeTick - 1, javelin.LastToDieJavelinFuseTicksRemaining);
    }

    [Fact]
    public void ExplosionUsesTeamPolarityLinearFalloffLineOfSightAndNoSelfEffect()
    {
        const float explosionX = 400f;
        const float explosionY = 200f;
        var world = CreateWorld(new LevelSolid(450f, 100f, 10f, 200f));
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Medic.Javelin]));
        world.LocalPlayer.TeleportTo(explosionX, explosionY);
        world.LocalPlayer.ForceSetHealth(world.LocalPlayer.MaxHealth - 50);

        var centerAlly = AddPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Red);
        var edgeAlly = AddPlayer(world, 3, PlayerClass.Heavy, PlayerTeam.Red);
        var centerEnemy = AddPlayer(world, 4, PlayerClass.Heavy, PlayerTeam.Blue);
        var edgeEnemy = AddPlayer(world, 5, PlayerClass.Heavy, PlayerTeam.Blue);
        var blockedEnemy = AddPlayer(world, 6, PlayerClass.Heavy, PlayerTeam.Blue);
        centerAlly.TeleportTo(explosionX, explosionY);
        centerEnemy.TeleportTo(explosionX, explosionY);
        PlaceHitboxRightAt(edgeAlly, explosionX - LastToDieDerivedModifiers.MedicJavelinBlastRadius, explosionY);
        PlaceHitboxRightAt(edgeEnemy, explosionX - LastToDieDerivedModifiers.MedicJavelinBlastRadius, explosionY);
        blockedEnemy.TeleportTo(470f, explosionY);
        centerAlly.ForceSetHealth(centerAlly.MaxHealth - 100);
        edgeAlly.ForceSetHealth(edgeAlly.MaxHealth - 100);
        var selfHealthBefore = world.LocalPlayer.Health;
        var centerEnemyHealthBefore = centerEnemy.Health;
        var edgeEnemyHealthBefore = edgeEnemy.Health;
        var blockedEnemyHealthBefore = blockedEnemy.Health;
        var javelin = SpawnKritzM2(
            world,
            world.LocalPlayer,
            explosionX,
            explosionY,
            0f,
            0f);

        Assert.True(InvokeExplodeJavelin(world, javelin));

        Assert.Equal(selfHealthBefore, world.LocalPlayer.Health);
        Assert.Equal(
            centerAlly.MaxHealth - 100 + LastToDieDerivedModifiers.MedicJavelinAllyCenterHealing,
            centerAlly.Health);
        Assert.Equal(
            edgeAlly.MaxHealth - 100 + LastToDieDerivedModifiers.MedicJavelinAllyEdgeHealing,
            edgeAlly.Health);
        Assert.Equal(
            LastToDieDerivedModifiers.MedicJavelinEnemyCenterDamage,
            centerEnemyHealthBefore - centerEnemy.Health);
        Assert.Equal(
            LastToDieDerivedModifiers.MedicJavelinEnemyEdgeDamage,
            edgeEnemyHealthBefore - edgeEnemy.Health);
        Assert.Equal(blockedEnemyHealthBefore, blockedEnemy.Health);
    }

    [Fact]
    public void ExplosionComposesCapturedHailAndNeurotoxin()
    {
        const float explosionX = 300f;
        const float explosionY = 180f;
        var world = CreateWorld();
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [
                LastToDiePerkIds.Medic.Javelin,
                LastToDiePerkIds.Medic.HailMary,
                LastToDiePerkIds.Medic.Neurotoxin,
            ]));
        var ally = AddPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Red);
        var unstunnedEnemy = AddPlayer(world, 3, PlayerClass.Heavy, PlayerTeam.Blue);
        var stunnedEnemy = AddPlayer(world, 4, PlayerClass.Heavy, PlayerTeam.Blue);
        ally.TeleportTo(explosionX, explosionY);
        unstunnedEnemy.TeleportTo(explosionX, explosionY);
        stunnedEnemy.TeleportTo(explosionX, explosionY);
        Assert.True(stunnedEnemy.SetServerStunTicks(1));
        var unstunnedHealthBefore = unstunnedEnemy.Health;
        var stunnedHealthBefore = stunnedEnemy.Health;
        var javelin = SpawnKritzM2(
            world,
            world.LocalPlayer,
            explosionX,
            explosionY,
            0f,
            0f);

        Assert.True(InvokeExplodeJavelin(world, javelin));

        Assert.True(ally.IsLastToDieMedicHailMaryInvulnerable);
        Assert.Equal(
            LastToDieDerivedModifiers.MedicJavelinEnemyCenterDamage,
            unstunnedHealthBefore - unstunnedEnemy.Health);
        Assert.Equal(
            LastToDieDerivedModifiers.MedicJavelinEnemyCenterDamage
                * LastToDieDerivedModifiers.MedicNeurotoxinPreStunnedDamageMultiplier,
            stunnedHealthBefore - stunnedEnemy.Health);
        Assert.True(unstunnedEnemy.IsServerStunned);
        Assert.True(stunnedEnemy.IsServerStunned);
        Assert.All(
            new[] { unstunnedEnemy, stunnedEnemy },
            target => Assert.Contains(
                world.GetLastToDieStatusEffects(target.Id),
                status => status.Id == LastToDieStatusEffectIds.MedicNeurotoxinStun
                    && status.SourcePlayerId == world.LocalPlayer.Id));
    }

    [Fact]
    public void OwnerDisconnectPreservesJavelinAndDamageAttribution()
    {
        var world = CreateWorld();
        var owner = AddPlayer(world, 2, PlayerClass.Medic, PlayerTeam.Red);
        var enemy = AddPlayer(world, 3, PlayerClass.Heavy, PlayerTeam.Blue);
        owner.TeleportTo(100f, 100f);
        enemy.TeleportTo(180f, 100f);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            2,
            [LastToDiePerkIds.Medic.Javelin]));
        var javelin = SpawnKritzM2(world, owner, 120f, 100f, 20f, 0f);
        var ownerId = owner.Id;

        Assert.True(world.TryReleaseNetworkPlayerSlot(2));
        Assert.Contains(javelin, world.Needles);
        AdvanceUntilAnchored(world, javelin);

        var directEvent = Assert.Single(world.DrainPendingDamageEvents());
        Assert.Equal(ownerId, directEvent.AttackerPlayerId);
        enemy.TeleportTo(javelin.X, javelin.Y);
        Assert.True(InvokeExplodeJavelin(world, javelin));
        var radialEvent = Assert.Single(world.DrainPendingDamageEvents());
        Assert.Equal(ownerId, radialEvent.AttackerPlayerId);
    }

    [Fact]
    public void LegacyAndProtocol64TransportAnchorFuseExplosionAndDisconnectedSource()
    {
        var source = CreateWorld();
        var owner = AddPlayer(source, 2, PlayerClass.Medic, PlayerTeam.Blue);
        Assert.True(source.TryConfigureLastToDiePlayerBuild(
            2,
            [LastToDiePerkIds.Medic.Javelin]));
        var javelin = SpawnKritzM2(source, owner, 240f, 120f, 10f, 0f);
        javelin.AdvanceOneTick();
        Assert.True(javelin.TryAnchorLastToDieJavelin(javelin.X, javelin.Y));
        var expectedFuse = javelin.LastToDieJavelinFuseTicksRemaining;

        var legacy = global::ServerHelpers.ToSnapshotNeedleState(javelin);
        Assert.Equal((byte)0b1001, legacy.LastToDieMedicKritzM2Payload);
        Assert.True(legacy.IsLastToDieMedicJavelinAnchored);
        Assert.Equal(expectedFuse, legacy.LastToDieMedicJavelinFuseTicksRemaining);
        Assert.False(legacy.HasLastToDieMedicJavelinExploded);

        var publisher = new Protocol64StatePublisher(source);
        _ = publisher.BuildProjectileStates(10);
        var ownerId = owner.Id;
        Assert.True(source.TryReleaseNetworkPlayerSlot(2));
        var state = Assert.Single(publisher.BuildProjectileStates(11));
        Assert.Equal((byte)0b1001, state.LastToDieMedicKritzM2Payload);
        Assert.Equal(ownerId, state.LastToDieMedicJavelinOwnerPlayerId);
        Assert.Equal((byte)PlayerTeam.Blue, state.LastToDieMedicJavelinTeam);
        Assert.True(state.IsLastToDieMedicJavelinAnchored);
        Assert.Equal((ushort)expectedFuse, state.LastToDieMedicJavelinFuseTicksRemaining);
        Assert.False(state.HasLastToDieMedicJavelinExploded);

        var receiver = CreateWorld();
        Assert.True(receiver.ApplyProtocol64ProjectileState(state));
        var recreated = Assert.IsType<MedicHealNeedleProjectileEntity>(
            Assert.Single(receiver.Needles));
        Assert.Equal(ownerId, recreated.OwnerId);
        Assert.Equal(PlayerTeam.Blue, recreated.Team);
        Assert.True(recreated.IsLastToDieJavelinAnchored);
        Assert.Equal(expectedFuse, recreated.LastToDieJavelinFuseTicksRemaining);

        var resync = publisher.BuildResyncResponse(
            new Protocol64StateResyncRequest(
                RequestId: 31,
                LastPlayerStateSequence: 0,
                LastProjectileStateSequence: 0,
                LastStateTick: 0,
                Reason: Protocol64StateResyncReason.ClientRequested),
            stateTick: 12);
        var resyncJavelin = Assert.Single(resync.Projectiles);
        Assert.Equal(ownerId, resyncJavelin.LastToDieMedicJavelinOwnerPlayerId);
        Assert.Equal((byte)PlayerTeam.Blue, resyncJavelin.LastToDieMedicJavelinTeam);
        Assert.Equal((ushort)expectedFuse, resyncJavelin.LastToDieMedicJavelinFuseTicksRemaining);

        Assert.True(InvokeExplodeJavelin(source, javelin));
        source.AdvanceOneTick();
        Assert.Empty(publisher.BuildProjectileStates(13));
        var lifecycle = Assert.Single(publisher.BuildProjectileLifecycleEvents());
        Assert.Equal(Protocol64ProjectileLifecycleKind.Despawn, lifecycle.Lifecycle);
        Assert.Equal(ownerId, lifecycle.LastToDieMedicJavelinOwnerPlayerId);
        Assert.Equal((byte)PlayerTeam.Blue, lifecycle.LastToDieMedicJavelinTeam);
        Assert.Equal((ushort)0, lifecycle.LastToDieMedicJavelinFuseTicksRemaining);
        Assert.True(lifecycle.HasLastToDieMedicJavelinExploded);
    }

    private static SimulationWorld CreateWorld(params LevelSolid[] extraSolids)
    {
        var solids = new List<LevelSolid>
        {
            new(0f, 400f, 1000f, 80f),
        };
        solids.AddRange(extraSolids);
        var world = new SimulationWorld(new SimulationConfig
        {
            EnableLocalDummies = false,
            EnableEnemyTrainingDummy = false,
            EnableFriendlySupportDummy = false,
        });
        world.CombatTestSetLevel(new SimpleLevel(
            name: "ltd_medic_javelin_test",
            mode: GameModeKind.CaptureTheFlag,
            bounds: new WorldBounds(1000f, 480f),
            mapScale: 1f,
            backgroundAssetName: null,
            mapAreaIndex: 1,
            mapAreaCount: 1,
            localSpawn: new SpawnPoint(100f, 100f),
            redSpawns: [new SpawnPoint(100f, 100f)],
            blueSpawns: [new SpawnPoint(800f, 100f)],
            intelBases: [],
            roomObjects: [],
            floorY: 400f,
            solids,
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
        PlayerEntity owner,
        float x,
        float y,
        float velocityX,
        float velocityY)
    {
        var method = typeof(SimulationWorld).GetMethod(
            "SpawnMedicHealNeedle",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        _ = method!.Invoke(
            world,
            [
                owner,
                x,
                y,
                velocityX,
                velocityY,
                MedicHealNeedleProjectileEntity.DefaultHealPerHit,
                MedicHealNeedleProjectileEntity.DefaultEnemyDamagePerHit,
            ]);
        return Assert.IsType<MedicHealNeedleProjectileEntity>(world.Needles[^1]);
    }

    private static bool InvokeExplodeJavelin(
        SimulationWorld world,
        MedicHealNeedleProjectileEntity javelin)
    {
        var method = typeof(SimulationWorld).GetMethod(
            "TryExplodeLastToDieMedicJavelin",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsType<bool>(method!.Invoke(world, [javelin]));
    }

    private static void AdvanceUntilAnchored(
        SimulationWorld world,
        MedicHealNeedleProjectileEntity javelin)
    {
        for (var tick = 0; tick < 12 && !javelin.IsLastToDieJavelinAnchored; tick += 1)
        {
            world.AdvanceOneTick();
        }

        Assert.True(javelin.IsLastToDieJavelinAnchored);
    }

    private static void PlaceHitboxRightAt(
        PlayerEntity player,
        float right,
        float centerY)
    {
        player.TeleportTo(
            right - player.CollisionRightOffset,
            centerY - ((player.CollisionTopOffset + player.CollisionBottomOffset) * 0.5f));
    }
}
