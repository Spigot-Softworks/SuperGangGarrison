using OpenGarrison.Core;
using OpenGarrison.GameplayModding;
using OpenGarrison.Protocol;
using OpenGarrison.Server;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class BuffBannerRuntimeTests
{
    [Fact]
    public void EnemyKillsChargeBannerWithoutReplacingShotgunOrRage()
    {
        var world = CreateSoldierWorld();
        var soldier = world.LocalPlayer;

        Assert.Equal("weapon.soldier-shotgun", soldier.GameplayLoadoutState.SecondaryItemId);
        Assert.True(soldier.HasGameplayAbilityBehavior(
            GameplayAbilityConstants.UtilityChannel,
            BuiltInGameplayBehaviorIds.SoldierBuffBanner));

        for (byte slot = 2; slot <= 5; slot += 1)
        {
            var enemy = AddNetworkPlayer(world, slot, PlayerClass.Scout, PlayerTeam.Blue);
            Assert.True(world.TryApplyGameplayDamage(enemy.Id, 10_000f, soldier.Id, "RocketKL"));
        }

        Assert.Equal(PlayerEntity.BuffBannerDefaultMaxChargeKills, soldier.BuffBannerChargeKills);
        Assert.True(soldier.IsBuffBannerReady);
        Assert.False(soldier.IsRageReady);

        var teammate = AddNetworkPlayer(world, 6, PlayerClass.Scout, PlayerTeam.Red);
        Assert.False(world.TryApplyGameplayDamage(teammate.Id, 10_000f, soldier.Id, "RocketKL"));
        Assert.Equal(PlayerEntity.BuffBannerDefaultMaxChargeKills, soldier.BuffBannerChargeKills);
    }

    [Fact]
    public void AbilityInputDeploysBannerAndBlocksMovementAndWeapons()
    {
        var world = CreateSoldierWorld();
        var soldier = world.LocalPlayer;
        Assert.True(soldier.TryAddBuffBannerKillCharge(4));
        soldier.AddRageCharge(100f, ExperimentalGameplaySettings.RageMaxCharge);

        world.SetLocalPreviousInput(default);
        world.SetLocalInput(default(PlayerInputSnapshot) with { UseAbility = true });
        world.AdvanceOneTick();

        Assert.True(soldier.IsBuffBannerDeploying);
        Assert.Equal(PlayerEntity.BuffBannerDefaultDeployTicks, soldier.BuffBannerDeployTicksRemaining);
        Assert.Equal(0, soldier.BuffBannerChargeKills);
        Assert.Equal(100f, soldier.RageCharge);
        Assert.Contains(world.PendingSoundEvents, sound => sound.SoundName == "BuffbannerSnd");

        var ammoBefore = soldier.CurrentShells;
        var xBefore = soldier.X;
        world.SetLocalInput(default(PlayerInputSnapshot) with
        {
            FirePrimary = true,
            Right = true,
            AimWorldX = soldier.X + 128f,
            AimWorldY = soldier.Y,
        });
        world.AdvanceOneTick();

        Assert.Empty(world.Rockets);
        Assert.Equal(ammoBefore, soldier.CurrentShells);
        Assert.Equal(xBefore, soldier.X);
    }

    [Fact]
    public void ActiveBannerBuffsNearbyTeammatesAndDefersToKritz()
    {
        var world = CreateSoldierWorld();
        var soldier = world.LocalPlayer;
        var nearbyTeammate = AddNetworkPlayer(world, 2, PlayerClass.Scout, PlayerTeam.Red);
        var enemy = AddNetworkPlayer(world, 3, PlayerClass.Scout, PlayerTeam.Blue);
        var distantTeammate = AddNetworkPlayer(world, 4, PlayerClass.Scout, PlayerTeam.Red);
        nearbyTeammate.TeleportTo(soldier.X + 64f, soldier.Y);
        enemy.TeleportTo(soldier.X + 64f, soldier.Y);
        distantTeammate.TeleportTo(soldier.X + 256f, soldier.Y);

        Assert.True(soldier.TryAddBuffBannerKillCharge(4));
        Assert.True(soldier.TryStartBuffBanner());
        Advance(world, PlayerEntity.BuffBannerDefaultDeployTicks);

        Assert.True(soldier.IsBuffBannerActive);
        Assert.Equal(PlayerEntity.BuffBannerDefaultActiveTicks, soldier.BuffBannerActiveTicksRemaining);
        Assert.Equal(PlayerEntity.BuffBannerDefaultDamageMultiplier, soldier.ActiveKritzCritDamageMultiplier);
        Assert.Equal(PlayerEntity.BuffBannerDefaultDamageMultiplier, nearbyTeammate.ActiveKritzCritDamageMultiplier);
        Assert.False(enemy.IsKritzCritBoosted);
        Assert.False(distantTeammate.IsKritzCritBoosted);

        nearbyTeammate.RefreshKritzCritBoost(
            providerPlayerId: 999,
            providerSlot: 1,
            criticalDamageMultiplier: ExperimentalGameplaySettings.KritzCriticalDamageMultiplier,
            ticks: 100);
        world.AdvanceOneTick();
        Assert.Equal(
            ExperimentalGameplaySettings.KritzCriticalDamageMultiplier,
            nearbyTeammate.ActiveKritzCritDamageMultiplier);
    }

    [Fact]
    public void BannerStateResetsOnDeathAndRoundTripsThroughProtocol64()
    {
        var source = CreateSoldierWorld();
        Assert.True(source.LocalPlayer.TryAddBuffBannerKillCharge(4));
        Assert.True(source.LocalPlayer.TryStartBuffBanner());
        Advance(source, 7);

        var published = Assert.Single(
            new Protocol64StatePublisher(source).BuildPlayerStateBatch(12).Players);
        Assert.Equal(37, published.BuffBannerDeployTicksRemaining);

        var registry = new Protocol64SchemaRegistry();
        registry.Register(new Protocol64PlayerStateBatchSchema());
        var encoded = Protocol64FrameCodec.Encode(
            registry,
            new Protocol64PlayerStateBatch(1, 12, [published]),
            1,
            1,
            new Protocol64FrameEncodeOptions { Compression = Protocol64Compression.None });
        Assert.True(encoded.Succeeded, encoded.Fault?.Message);
        var decoded = Protocol64FrameCodec.Decode<Protocol64PlayerStateBatch>(encoded.Payload!, registry);
        Assert.True(decoded.Succeeded, decoded.Fault?.Message);

        var receiver = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        Assert.True(receiver.ApplyProtocol64PlayerState(Assert.Single(decoded.Event!.Players)));
        Assert.True(receiver.LocalPlayer.IsBuffBannerDeploying);
        Assert.Equal(37, receiver.LocalPlayer.BuffBannerDeployTicksRemaining);

        source.ForceKillLocalPlayer();
        Assert.Equal(0, source.LocalPlayer.BuffBannerChargeKills);
        Assert.False(source.LocalPlayer.IsBuffBannerDeploying);
        Assert.False(source.LocalPlayer.IsBuffBannerActive);
    }

    private static SimulationWorld CreateSoldierWorld()
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        world.PrepareLocalPlayerJoin();
        world.CompleteLocalPlayerJoin(PlayerClass.Soldier);
        world.LocalPlayer.SetSpawnRoomState(false);
        world.SetLocalInput(default);
        world.SetLocalPreviousInput(default);
        _ = world.DrainPendingSoundEvents();
        return world;
    }

    private static PlayerEntity AddNetworkPlayer(
        SimulationWorld world,
        byte slot,
        PlayerClass playerClass,
        PlayerTeam team)
    {
        Assert.True(world.TryPrepareNetworkPlayerJoin(slot));
        Assert.True(world.TrySetNetworkPlayerTeam(slot, team));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(slot, playerClass));
        Assert.True(world.TryGetNetworkPlayer(slot, out var player));
        return player;
    }

    private static void Advance(SimulationWorld world, int ticks)
    {
        world.SetLocalInput(default);
        for (var tick = 0; tick < ticks; tick += 1)
        {
            world.AdvanceOneTick();
        }
    }
}
