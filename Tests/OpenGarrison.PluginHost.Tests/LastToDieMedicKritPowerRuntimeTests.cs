using System.Reflection;
using OpenGarrison.Core;
using OpenGarrison.Core.LastToDie;
using OpenGarrison.GameplayModding;
using OpenGarrison.Protocol;
using OpenGarrison.Server;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class LastToDieMedicKritPowerRuntimeTests
{
    [Fact]
    public void KritPowerKritzGrantsThreePointFiveToMedicAndLinkedTarget()
    {
        var world = CreateWorld(PlayerClass.Medic);
        var medic = world.LocalPlayer;
        var target = AddPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Red, 220f, 100f);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Medic.KritPower]));
        Assert.True(medic.TrySelectGameplayEquippedSlot(GameplayEquipmentSlot.Secondary));
        medic.SetMedicHealingTarget(target);
        medic.FillMedicUberCharge();
        Assert.True(medic.TryStartMedicUber());

        InvokePrivate(world, "AdvanceMedicUberEffects");

        AssertKritPowerGrant(medic, medic.Id, SimulationWorld.LocalPlayerSlot);
        AssertKritPowerGrant(target, medic.Id, SimulationWorld.LocalPlayerSlot);
    }

    [Fact]
    public void KritzProviderArbitrationIsStrongestThenLowestSlotThenLowestPlayerId()
    {
        var player = CreateWorld(PlayerClass.Heavy).LocalPlayer;
        player.RefreshKritzCritBoost(30, 3, 3.5f, 10);
        player.RefreshKritzCritBoost(5, 1, 3f, 10);
        Assert.Equal(30, player.KritzCritBoostProviderPlayerId);
        Assert.Equal(3, player.KritzCritBoostProviderSlot);
        Assert.Equal(3.5f, player.ActiveKritzCritDamageMultiplier);

        player.RefreshKritzCritBoost(20, 2, 3.5f, 10);
        player.RefreshKritzCritBoost(10, 2, 3.5f, 10);
        Assert.Equal(10, player.KritzCritBoostProviderPlayerId);
        Assert.Equal(2, player.KritzCritBoostProviderSlot);

        var prediction = player.CapturePredictionState();
        player.HydrateKritzCritBoost(false, 0, 0, int.MaxValue, 1f);
        player.RestorePredictionState(prediction);
        Assert.Equal(10, player.KritzCritBoostProviderPlayerId);
        Assert.Equal(2, player.KritzCritBoostProviderSlot);
        Assert.Equal(3.5f, player.KritzCritBoostDamageMultiplier);

        player.HydrateKritzCritBoost(true, 4, 7, 1, 1f);
        Assert.Equal(3f, player.ActiveKritzCritDamageMultiplier);
        player.HydrateKritzCritBoost(false, 0, 0, int.MaxValue, 3.5f);
        Assert.False(player.IsKritzCritBoosted);
        Assert.Equal(1f, player.ActiveKritzCritDamageMultiplier);
    }

    [Fact]
    public void EveryProjectileFamilyCapturesReleaseMultiplierAndNaturalCritStaysStock()
    {
        var world = CreateWorld(PlayerClass.Medic);
        var owner = world.LocalPlayer;
        owner.RefreshKritzCritBoost(99, 3, 3.5f, 30);

        InvokePrivate(world, "SpawnShot", owner, 100f, 100f, 1f, 0f);
        InvokePrivate(world, "SpawnBubble", owner, 100f, 100f, 1f, 0f);
        InvokePrivate(world, "SpawnBlade", owner, 100f, 100f, 1f, 0f, 10);
        InvokePrivate(world, "SpawnNail", owner, 100f, 100f, 1f, 0f);
        InvokePrivate(world, "SpawnArrow", owner, 100f, 100f, 1f, 0f, 40, 1f);
        InvokePrivate(world, "SpawnNeedle", owner, 100f, 100f, 1f, 0f);
        InvokePrivate(world, "SpawnMedicHealNeedle", owner, 100f, 100f, 1f, 0f);
        InvokePrivate(world, "SpawnRevolverShot", owner, 100f, 100f, 1f, 0f);
        InvokePrivate(world, "SpawnFlame", owner, 100f, 100f, 1f, 0f);
        InvokePrivate(world, "SpawnFlare", owner, 100f, 100f, 1f, 0f);
        InvokePrivate(world, "SpawnRocket", owner, 100f, 100f, 1f, 0f);
        InvokePrivate(world, "SpawnMine", owner, 100f, 100f, 1f, 0f);
        InvokePrivate(world, "SpawnGrenade", owner, 100f, 100f, 1f, 0f);

        AssertCritical(world.Shots[^1]);
        AssertCritical(world.Bubbles[^1]);
        AssertCritical(world.Blades[^1]);
        AssertCritical(Assert.IsType<NailProjectileEntity>(world.Needles[0]));
        AssertCritical(Assert.IsType<ArrowProjectileEntity>(world.Needles[1]));
        AssertCritical(Assert.IsType<NeedleProjectileEntity>(world.Needles[2]));
        AssertCritical(Assert.IsType<MedicHealNeedleProjectileEntity>(world.Needles[3]));
        AssertCritical(world.RevolverShots[^1]);
        AssertCritical(world.Flames[^1]);
        AssertCritical(world.Flares[^1]);
        AssertCritical(world.Rockets[^1]);
        AssertCritical(world.Mines[^1]);
        AssertCritical(world.Grenades[^1]);
        Assert.Equal(3.5f, Assert.Single(world.PendingRocketSpawnEvents).CriticalDamageMultiplier);

        owner.HydrateKritzCritBoost(false, 0, 0, int.MaxValue, 1f);
        Assert.Equal(3.5f, world.Rockets[^1].CriticalDamageMultiplier);
        Assert.Equal(3.5f, world.Needles[1].CriticalDamageMultiplier);

        InvokePrivate(
            world,
            "SpawnRevolverShot",
            owner,
            100f,
            100f,
            1f,
            0f,
            RevolverProjectileEntity.DamagePerHit,
            null,
            null,
            true,
            false);
        Assert.True(world.RevolverShots[^1].IsCritical);
        Assert.Equal(3f, world.RevolverShots[^1].CriticalDamageMultiplier);
    }

    [Theory]
    [InlineData(3f)]
    [InlineData(3.5f)]
    public void RifleUsesCapturedKritzMultiplierExactlyOnce(float multiplier)
    {
        var world = CreateWorld(PlayerClass.Sniper);
        var sniper = world.LocalPlayer;
        sniper.TeleportTo(100f, 100f);
        var target = AddPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Blue, 220f, 100f);
        sniper.RefreshKritzCritBoost(9, 1, multiplier, 10);
        var baseDamage = sniper.GetSniperRifleDamage();

        Assert.True(sniper.TryFirePrimaryWeapon());
        InvokePrivate(world, "FirePrimaryWeapon", sniper, target.X, target.Y);

        Assert.Equal(
            Math.Max(1, (int)MathF.Round(baseDamage * multiplier)),
            target.MaxHealth - target.Health);
        Assert.True(Assert.Single(world.CombatTraces).IsCritical);
    }

    [Fact]
    public void JavelinKeepsThreePointFiveDamageAfterGrantEnds()
    {
        var world = CreateWorld(PlayerClass.Medic);
        var owner = world.LocalPlayer;
        var target = AddPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Blue, 240f, 100f);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Medic.Javelin]));
        owner.RefreshKritzCritBoost(77, 2, 3.5f, 10);
        InvokePrivate(world, "SpawnMedicHealNeedle", owner, 240f, 100f, 0f, 0f);
        var javelin = Assert.IsType<MedicHealNeedleProjectileEntity>(world.Needles[^1]);
        owner.HydrateKritzCritBoost(false, 0, 0, int.MaxValue, 1f);
        var healthBefore = target.Health;

        Assert.True(Assert.IsType<bool>(InvokePrivate(world, "TryExplodeLastToDieMedicJavelin", javelin)));

        Assert.Equal(
            (int)MathF.Round(LastToDieDerivedModifiers.MedicJavelinEnemyCenterDamage * 3.5f),
            healthBefore - target.Health);
        var damageEvent = Assert.Single(world.DrainPendingDamageEvents());
        Assert.True(damageEvent.Flags.HasFlag(DamageEventFlags.Critical));
    }

    [Fact]
    public void MenageVolleyKeepsCapturedMultiplierAfterProviderDisconnects()
    {
        var world = CreateWorld(PlayerClass.Sniper);
        var sniper = world.LocalPlayer;
        var provider = AddPlayer(world, 2, PlayerClass.Medic, PlayerTeam.Red, 160f, 100f);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Sniper.MenageATrois]));
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            2,
            [LastToDiePerkIds.Medic.KritPower]));
        sniper.EquipExperimentalOffhandWeapon();
        Assert.True(sniper.IsSniperBowEquipped);
        Assert.True(provider.TrySelectGameplayEquippedSlot(GameplayEquipmentSlot.Secondary));
        provider.SetMedicHealingTarget(sniper);
        provider.FillMedicUberCharge();
        Assert.True(provider.TryStartMedicUber());
        InvokePrivate(world, "AdvanceMedicUberEffects");

        InvokePrivate(
            world,
            "SpawnArrow",
            sniper,
            100f,
            100f,
            1f,
            0f,
            PlayerEntity.SniperBowMaxDamage,
            PlayerEntity.SniperBowMaxFakeSpeedMultiplier);

        Assert.Equal(3.5f, Assert.IsType<ArrowProjectileEntity>(world.Needles[0]).CriticalDamageMultiplier);
        Assert.Equal(3.5f, sniper.LastToDieSniperVolleyState.Payload.CriticalDamageMultiplier);
        Assert.True(world.TryReleaseNetworkPlayerSlot(2));
        sniper.HydrateKritzCritBoost(false, 0, 0, int.MaxValue, 1f);

        for (var tick = 0; tick < LastToDieSniperProfile.MenageATroisArrowIntervalSourceTicks; tick += 1)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(2, world.Needles.Count);
        Assert.Equal(3.5f, Assert.IsType<ArrowProjectileEntity>(world.Needles[1]).CriticalDamageMultiplier);
    }

    [Fact]
    public void ExplosiveProjectilesApplyCapturedMultiplierOnceAfterGrantEnds()
    {
        AssertExplosionMultiplier(
            "SpawnRocket",
            static world => world.Rockets[^1],
            static (world, projectile) => world.CombatTestExplodeRocket((RocketProjectileEntity)projectile),
            RocketProjectileEntity.ExplosionDamage);
        AssertExplosionMultiplier(
            "SpawnMine",
            static world => world.Mines[^1],
            static (world, projectile) => world.CombatTestExplodeMine((MineProjectileEntity)projectile),
            MineProjectileEntity.BaseExplosionDamage);
        AssertExplosionMultiplier(
            "SpawnGrenade",
            static world => world.Grenades[^1],
            static (world, projectile) => world.CombatTestExplodeGrenade((GrenadeProjectileEntity)projectile),
            GrenadeProjectileEntity.BaseExplosionDamage);
    }

    [Fact]
    public void ExplosiveTipKeepsCapturedMultiplierAfterGrantEnds()
    {
        var world = CreateWorld(PlayerClass.Sniper);
        var sniper = world.LocalPlayer;
        var target = AddPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Blue, 240f, 100f);
        target.SetExperimentalMaxHealthOverride(600, refillHealth: true);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Sniper.ExplosiveTip]));
        sniper.RefreshKritzCritBoost(41, 2, 3.5f, 10);
        InvokePrivate(
            world,
            "SpawnArrow",
            sniper,
            target.X,
            target.Y,
            0f,
            0f,
            PlayerEntity.SniperBowMaxDamage,
            PlayerEntity.SniperBowMaxFakeSpeedMultiplier);
        var arrow = Assert.IsType<ArrowProjectileEntity>(world.Needles[^1]);
        sniper.HydrateKritzCritBoost(false, 0, 0, int.MaxValue, 1f);
        var healthBefore = target.Health;

        Assert.True(Assert.IsType<bool>(InvokePrivate(world, "TryExplodeLastToDieSniperArrow", arrow)));

        Assert.Equal(
            (int)MathF.Round(LastToDieSniperProfile.ExplosiveTipCenterDamage * 3.5f),
            healthBefore - target.Health);
        var damageEvent = Assert.Single(world.DrainPendingDamageEvents());
        Assert.True(damageEvent.Flags.HasFlag(DamageEventFlags.Critical));
    }

    [Theory]
    [InlineData(false, true, PlayerEntity.CivvieUmbrellaImpactDrain)]
    [InlineData(true, false, PlayerEntity.CivvieUmbrellaImpactDrain * PlayerEntity.CivvieUmbrellaCriticalBoostDrainMultiplier)]
    public void DelayedShotUmbrellaUsesFrozenCriticalAndProjectileThreat(
        bool criticalAtRelease,
        bool criticalAtImpact,
        int expectedDrain)
    {
        var world = CreateWorld(PlayerClass.Quote);
        var civilian = world.LocalPlayer;
        var attacker = AddPlayer(world, 2, PlayerClass.Scout, PlayerTeam.Blue, 100f, 100f);
        civilian.TeleportTo(240f, 100f);
        civilian.SetAimWorldPosition(civilian.X - 100f, civilian.Y);
        SetAimDirectionDegrees(civilian, 180f);
        Assert.True(civilian.TryActivateCivvieUmbrella());
        civilian.GetCollisionBounds(out var left, out var top, out _, out var bottom);
        if (criticalAtRelease)
        {
            attacker.RefreshKritzCritBoost(41, 3, 3.5f, 10);
        }

        InvokePrivate(world, "SpawnShot", attacker, left - 5f, (top + bottom) * 0.5f, 10f, 0f);
        attacker.HydrateKritzCritBoost(
            criticalAtImpact,
            criticalAtImpact ? 10 : 0,
            criticalAtImpact ? 41 : 0,
            criticalAtImpact ? 3 : int.MaxValue,
            criticalAtImpact ? 3.5f : 1f);
        attacker.TeleportTo(civilian.X + 100f, civilian.Y);
        var chargeBefore = civilian.CivvieUmbrellaChargeTicks;

        InvokePrivate(world, "AdvanceShots");

        Assert.Equal(civilian.MaxHealth, civilian.Health);
        Assert.Equal(chargeBefore - expectedDrain, civilian.CivvieUmbrellaChargeTicks);
        var damageEvent = Assert.Single(world.DrainPendingDamageEvents());
        Assert.True(damageEvent.Flags.HasFlag(DamageEventFlags.CivvieUmbrellaBlock));
    }

    [Fact]
    public void KritPowerStateAndProjectileRoundTripLegacyProtocol64AndResync()
    {
        var source = CreateWorld(PlayerClass.Medic);
        var medic = source.LocalPlayer;
        medic.RefreshKritzCritBoost(41, 2, 3.5f, 17);
        InvokePrivate(source, "SpawnRocket", medic, 150f, 100f, 1f, 0f);
        var rocket = source.Rockets[^1];

        var legacyPlayer = ServerHelpers.ToSnapshotPlayerState(
            source,
            SimulationWorld.LocalPlayerSlot,
            medic,
            medic,
            new SnapshotStringCache());
        var legacyRocket = ServerHelpers.ToSnapshotRocketState(rocket);
        var legacySnapshot = CreateSnapshot(legacyPlayer) with { Rockets = [legacyRocket] };
        var legacyPayload = ProtocolCodec.Serialize(legacySnapshot, ProtocolCompressionSettings.Disabled);
        Assert.True(ProtocolCodec.TryDeserialize(legacyPayload, out var decodedMessage));
        var decodedSnapshot = Assert.IsType<SnapshotMessage>(decodedMessage);
        Assert.Equal(41, Assert.Single(decodedSnapshot.Players).KritzCritBoostProviderPlayerId);
        Assert.Equal(2, Assert.Single(decodedSnapshot.Players).KritzCritBoostProviderSlot);
        Assert.Equal(3.5f, Assert.Single(decodedSnapshot.Players).KritzCritBoostDamageMultiplier);
        Assert.Equal(3.5f, Assert.Single(decodedSnapshot.Rockets).CriticalDamageMultiplier);

        var legacyReceiver = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        Assert.True(legacyReceiver.ApplySnapshot(decodedSnapshot));
        Assert.Equal(3.5f, legacyReceiver.LocalPlayer.ActiveKritzCritDamageMultiplier);
        Assert.Equal(3.5f, Assert.Single(legacyReceiver.Rockets).CriticalDamageMultiplier);

        var baselinePlayer = legacyPlayer with
        {
            IsKritzCritBoosted = false,
            KritzCritBoostProviderPlayerId = 0,
            KritzCritBoostProviderSlot = int.MaxValue,
            KritzCritBoostDamageMultiplier = 1f,
        };
        var baseline = CreateSnapshot(baselinePlayer) with { Frame = 20 };
        var delta = baseline with
        {
            Frame = 21,
            BaselineFrame = 20,
            IsDelta = true,
            Players = [],
            PlayerExtendedStatusStates =
            [
                new SnapshotPlayerExtendedStatusState(
                    SimulationWorld.LocalPlayerSlot,
                    IsSpyCloaked: false,
                    SpyCloakAlpha: 1f,
                    IsSpySuperjumping: false,
                    SpySuperjumpHorizontalVelocity: 0f,
                    SpySuperjumpCooldownTicksRemaining: 0,
                    SpyBackstabVisualTicksRemaining: 0,
                    IsUbered: false,
                    IsKritzCritBoosted: true,
                    IsHeavyEating: false,
                    HeavyEatTicksRemaining: 0,
                    IsSniperScoped: false,
                    KritzCritBoostProviderPlayerId: 41,
                    KritzCritBoostProviderSlot: 2,
                    KritzCritBoostDamageMultiplier: 3.5f),
            ],
        };
        var deltaPayload = ProtocolCodec.Serialize(delta, ProtocolCompressionSettings.Disabled);
        Assert.True(ProtocolCodec.TryDeserialize(deltaPayload, out var decodedDeltaMessage));
        var decodedDelta = Assert.IsType<SnapshotMessage>(decodedDeltaMessage);
        var merged = SnapshotDelta.ToFullSnapshot(decodedDelta, baseline);
        var mergedPlayer = Assert.Single(merged.Players);
        Assert.True(mergedPlayer.IsKritzCritBoosted);
        Assert.Equal(41, mergedPlayer.KritzCritBoostProviderPlayerId);
        Assert.Equal(2, mergedPlayer.KritzCritBoostProviderSlot);
        Assert.Equal(3.5f, mergedPlayer.KritzCritBoostDamageMultiplier);

        var publisher = new Protocol64StatePublisher(source);
        var playerState = Assert.Single(
            publisher.BuildPlayerStateBatch(10).Players,
            state => state.Slot == SimulationWorld.LocalPlayerSlot);
        var projectileState = Assert.Single(publisher.BuildProjectileStates(10));
        Assert.Equal(17, playerState.KritzCritBoostTicksRemaining);
        Assert.Equal(41, playerState.KritzCritBoostProviderPlayerId);
        Assert.Equal(2, playerState.KritzCritBoostProviderSlot);
        Assert.Equal(3.5f, playerState.KritzCritBoostDamageMultiplier);
        Assert.Equal(3.5f, projectileState.CriticalDamageMultiplier);

        var protocolReceiver = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        Assert.True(protocolReceiver.ApplyProtocol64PlayerState(playerState));
        Assert.True(protocolReceiver.ApplyProtocol64ProjectileState(projectileState));
        Assert.Equal(3.5f, protocolReceiver.LocalPlayer.ActiveKritzCritDamageMultiplier);
        Assert.Equal(3.5f, Assert.Single(protocolReceiver.Rockets).CriticalDamageMultiplier);

        var resync = publisher.BuildResyncResponse(
            new Protocol64StateResyncRequest(55, 0, 0, 0, Protocol64StateResyncReason.ClientRequested),
            stateTick: 11);
        Assert.Equal(3.5f, Assert.Single(resync.Players).KritzCritBoostDamageMultiplier);
        Assert.Equal(3.5f, Assert.Single(resync.Projectiles).CriticalDamageMultiplier);

        source.CombatTestExplodeRocket(rocket);
        source.AdvanceOneTick();
        Assert.Empty(publisher.BuildProjectileStates(12));
        var lifecycle = Assert.Single(publisher.BuildProjectileLifecycleEvents());
        Assert.Equal(Protocol64ProjectileLifecycleKind.Despawn, lifecycle.Lifecycle);
        Assert.True(lifecycle.IsCritical);
        Assert.Equal(3.5f, lifecycle.CriticalDamageMultiplier);
    }

    private static void AssertExplosionMultiplier(
        string spawnMethod,
        Func<SimulationWorld, SimulationEntity> getProjectile,
        Action<SimulationWorld, SimulationEntity> explode,
        float baseDamage)
    {
        var world = CreateWorld(PlayerClass.Medic);
        var owner = world.LocalPlayer;
        var target = AddPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Blue, 240f, 100f);
        target.SetExperimentalMaxHealthOverride(600, refillHealth: true);
        owner.RefreshKritzCritBoost(41, 2, 3.5f, 10);
        InvokePrivate(world, spawnMethod, owner, target.X, target.Y, 0f, 0f);
        var projectile = getProjectile(world);
        owner.HydrateKritzCritBoost(false, 0, 0, int.MaxValue, 1f);
        var healthBefore = target.Health;

        explode(world, projectile);

        Assert.Equal((int)(baseDamage * 3.5f), healthBefore - target.Health);
        var damageEvent = Assert.Single(world.DrainPendingDamageEvents());
        Assert.True(damageEvent.Flags.HasFlag(DamageEventFlags.Critical));
    }

    private static void AssertKritPowerGrant(PlayerEntity player, int providerPlayerId, int providerSlot)
    {
        Assert.True(player.IsKritzCritBoosted);
        Assert.Equal(providerPlayerId, player.KritzCritBoostProviderPlayerId);
        Assert.Equal(providerSlot, player.KritzCritBoostProviderSlot);
        Assert.Equal(3.5f, player.ActiveKritzCritDamageMultiplier);
    }

    private static void SetAimDirectionDegrees(PlayerEntity player, float degrees)
    {
        var field = typeof(PlayerEntity).GetField(
            "<AimDirectionDegrees>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(player, degrees);
    }

    private static void AssertCritical(ShotProjectileEntity projectile)
    {
        Assert.True(projectile.IsCritical);
        Assert.Equal(3.5f, projectile.CriticalDamageMultiplier);
    }

    private static void AssertCritical(BubbleProjectileEntity projectile)
    {
        Assert.True(projectile.IsCritical);
        Assert.Equal(3.5f, projectile.CriticalDamageMultiplier);
    }

    private static void AssertCritical(BladeProjectileEntity projectile)
    {
        Assert.True(projectile.IsCritical);
        Assert.Equal(3.5f, projectile.CriticalDamageMultiplier);
    }

    private static void AssertCritical(NeedleProjectileEntity projectile)
    {
        Assert.True(projectile.IsCritical);
        Assert.Equal(3.5f, projectile.CriticalDamageMultiplier);
    }

    private static void AssertCritical(RevolverProjectileEntity projectile)
    {
        Assert.True(projectile.IsCritical);
        Assert.Equal(3.5f, projectile.CriticalDamageMultiplier);
    }

    private static void AssertCritical(FlameProjectileEntity projectile)
    {
        Assert.True(projectile.IsCritical);
        Assert.Equal(3.5f, projectile.CriticalDamageMultiplier);
    }

    private static void AssertCritical(FlareProjectileEntity projectile)
    {
        Assert.True(projectile.IsCritical);
        Assert.Equal(3.5f, projectile.CriticalDamageMultiplier);
    }

    private static void AssertCritical(RocketProjectileEntity projectile)
    {
        Assert.True(projectile.IsCritical);
        Assert.Equal(3.5f, projectile.CriticalDamageMultiplier);
    }

    private static void AssertCritical(MineProjectileEntity projectile)
    {
        Assert.True(projectile.IsCritical);
        Assert.Equal(3.5f, projectile.CriticalDamageMultiplier);
    }

    private static void AssertCritical(GrenadeProjectileEntity projectile)
    {
        Assert.True(projectile.IsCritical);
        Assert.Equal(3.5f, projectile.CriticalDamageMultiplier);
    }

    private static SimulationWorld CreateWorld(PlayerClass playerClass)
    {
        var world = new SimulationWorld(new SimulationConfig
        {
            EnableLocalDummies = false,
            EnableEnemyTrainingDummy = false,
            EnableFriendlySupportDummy = false,
        });
        world.CombatTestSetLevel(new SimpleLevel(
            name: "ltd_medic_krit_power_test",
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
            floorY: 480f,
            solids: [],
            importedFromSource: false));
        world.PrepareLocalPlayerJoin();
        world.CompleteLocalPlayerJoin(playerClass);
        world.LocalPlayer.SetSpawnRoomState(false);
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
        player.SetSpawnRoomState(false);
        return player;
    }

    private static object? InvokePrivate(object target, string methodName, params object?[] suppliedArguments)
    {
        var method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var parameters = method!.GetParameters();
        Assert.True(suppliedArguments.Length <= parameters.Length);
        var arguments = new object?[parameters.Length];
        for (var index = 0; index < arguments.Length; index += 1)
        {
            arguments[index] = index < suppliedArguments.Length
                ? suppliedArguments[index]
                : Type.Missing;
        }

        return method.Invoke(target, arguments);
    }

    private static SnapshotMessage CreateSnapshot(SnapshotPlayerState player)
    {
        return new SnapshotMessage(
            Frame: 10,
            TickRate: 30,
            LevelName: "ctf_truefort",
            MapAreaIndex: 1,
            MapAreaCount: 1,
            GameMode: 1,
            MatchPhase: 1,
            WinnerTeam: 0,
            TimeRemainingTicks: 300,
            RedCaps: 0,
            BlueCaps: 0,
            SpectatorCount: 0,
            LastProcessedInputSequence: 0,
            RedIntel: new SnapshotIntelState(1, 0f, 0f, true, false, 0),
            BlueIntel: new SnapshotIntelState(2, 0f, 0f, true, false, 0),
            Players: [player],
            CombatTraces: [],
            SniperAimIndicators: [],
            Sentries: [],
            Shots: [],
            Bubbles: [],
            Blades: [],
            Needles: [],
            RevolverShots: [],
            Rockets: [],
            Flames: [],
            Flares: [],
            Mines: [],
            DeadBodies: [],
            ControlPointSetupTicksRemaining: 0,
            KothUnlockTicksRemaining: 0,
            KothRedTimerTicksRemaining: 0,
            KothBlueTimerTicksRemaining: 0,
            ControlPoints: [],
            Generators: [],
            LocalDeathCam: null,
            KillFeed: [],
            VisualEvents: [],
            DamageEvents: [],
            SoundEvents: []);
    }
}
