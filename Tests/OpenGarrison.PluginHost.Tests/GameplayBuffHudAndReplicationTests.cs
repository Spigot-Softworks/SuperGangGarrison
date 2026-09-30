using System.Linq;
using OpenGarrison.Client;
using OpenGarrison.Core;
using OpenGarrison.Core.LastToDie;
using OpenGarrison.Protocol;
using OpenGarrison.Server;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class GameplayBuffHudAndReplicationTests
{
    [Fact]
    public void SpecialAbilitiesSettingFollowsServerChangesInBothSnapshotPaths()
    {
        var source = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        var receiver = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        var publisher = new Protocol64StatePublisher(source);
        var stringCache = new SnapshotStringCache();
        foreach (var enabled in new[] { false, true, false })
        {
            source.ConfigureExperimentalGameplaySettings(new(EnableSecondaryAbilities: enabled));
            var state = Assert.Single(publisher.BuildPlayerStateBatch(1).Players);
            Assert.True(receiver.ApplyProtocol64PlayerState(state, SimulationWorld.LocalPlayerSlot));
            Assert.Equal(enabled, receiver.ExperimentalGameplaySettings.EnableSecondaryAbilities);
            receiver.ConfigureExperimentalGameplaySettings(new(EnableSecondaryAbilities: !enabled));
            var player = source.Snapshots.ToSnapshotPlayerState(
                SimulationWorld.LocalPlayerSlot,
                source.LocalPlayer,
                source.LocalPlayer,
                value => stringCache.GetOrAddCacheId(value));
            var snapshot = CreateSnapshot(player);
            var bytes = ProtocolCodec.Serialize(snapshot, ProtocolCompressionSettings.Disabled);
            Assert.True(ProtocolCodec.TryDeserialize(bytes, out var message));
            Assert.True(receiver.ApplySnapshot(Assert.IsType<SnapshotMessage>(message)));
            Assert.Equal(enabled, receiver.ExperimentalGameplaySettings.EnableSecondaryAbilities);
        }
    }

    [Fact]
    public void LegacySnapshotDispenserBuffRoundTripsAppliesAndMergesFromDelta()
    {
        var source = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        source.LocalPlayer.SetDispenserBuffed(true, 1.25f);
        source.LocalPlayer.RegisterCombatComboHit(120);
        var stringCache = new SnapshotStringCache();
        var player = source.Snapshots.ToSnapshotPlayerState(
            SimulationWorld.LocalPlayerSlot,
            source.LocalPlayer,
            source.LocalPlayer,
            value => stringCache.GetOrAddCacheId(value)) with
        {
            ExperimentalCryoSlowTicksRemaining = 42,
            ExperimentalCryoFreezeTicksRemaining = 21,
            ExperimentalCryoExposureFraction = 0.5f,
            ExperimentalGhostVisibilityTicksRemaining = 33,
            ExperimentalGhostTrailAlpha = 0.75f,
        };
        var snapshot = CreateSnapshot(player);

        var payload = ProtocolCodec.Serialize(snapshot, ProtocolCompressionSettings.Disabled);
        Assert.True(ProtocolCodec.TryDeserialize(payload, out var decodedMessage));
        var decoded = Assert.IsType<SnapshotMessage>(decodedMessage);
        var decodedPlayer = Assert.Single(decoded.Players);
        Assert.True(decodedPlayer.IsDispenserBuffed);
        Assert.Equal(1.25f, decodedPlayer.DispenserAttackReloadSpeedMultiplier);
        Assert.Equal(1, decodedPlayer.CurrentCombo);
        Assert.Equal(120, decodedPlayer.ComboTicksRemaining);
        Assert.Equal(42, decodedPlayer.ExperimentalCryoSlowTicksRemaining);
        Assert.Equal(21, decodedPlayer.ExperimentalCryoFreezeTicksRemaining);
        Assert.InRange(decodedPlayer.ExperimentalCryoExposureFraction, 0.5f, 0.51f);
        Assert.Equal(33, decodedPlayer.ExperimentalGhostVisibilityTicksRemaining);
        Assert.Equal(191f / byte.MaxValue, decodedPlayer.ExperimentalGhostTrailAlpha);

        var receiver = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        Assert.True(receiver.ApplySnapshot(decoded));
        Assert.True(receiver.LocalPlayer.IsDispenserBuffed);
        Assert.Equal(1.25f, receiver.LocalPlayer.DispenserAttackReloadSpeedMultiplier);
        Assert.Equal(1, receiver.LocalPlayer.CurrentCombo);
        Assert.Equal(120, receiver.LocalPlayer.ComboTicksRemaining);
        Assert.True(receiver.LocalPlayer.IsExperimentalCryoSlowed);
        Assert.True(receiver.LocalPlayer.IsExperimentalCryoFrozen);
        Assert.InRange(receiver.LocalPlayer.ExperimentalCryoExposureFraction, 0.5f, 0.51f);
        Assert.True(receiver.LocalPlayer.IsExperimentalGhostDashVisible);
        Assert.Equal(191f / byte.MaxValue, receiver.LocalPlayer.ExperimentalGhostDashTrailAlpha);

        var baseline = CreateSnapshot(player with
        {
            IsDispenserBuffed = false,
            DispenserAttackReloadSpeedMultiplier = 1f,
        }) with { Frame = 20 };
        var delta = baseline with
        {
            Frame = 21,
            BaselineFrame = 20,
            IsDelta = true,
            Players = [],
            PlayerStatusStates =
            [
                new SnapshotPlayerStatusState(
                    SimulationWorld.LocalPlayerSlot,
                    player.Health,
                    player.MaxHealth,
                    player.Ammo,
                    player.MaxAmmo,
                    player.Metal,
                    player.IsCarryingIntel,
                    player.IntelRechargeTicks,
                    CurrentCombo: 4,
                    ComboTicksRemaining: 75),
            ],
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
                    IsKritzCritBoosted: false,
                    IsHeavyEating: false,
                    HeavyEatTicksRemaining: 0,
                    IsSniperScoped: false,
                    IsDispenserBuffed: true,
                    DispenserAttackReloadSpeedMultiplier: 1.25f,
                    ExperimentalCryoSlowTicksRemaining: 55,
                    ExperimentalCryoFreezeTicksRemaining: 28,
                    ExperimentalCryoExposureFraction: 0.25f,
                    ExperimentalGhostVisibilityTicksRemaining: 18,
                    ExperimentalGhostTrailAlpha: 0.25f),
            ],
        };

        var deltaPayload = ProtocolCodec.Serialize(delta, ProtocolCompressionSettings.Disabled);
        Assert.True(ProtocolCodec.TryDeserialize(deltaPayload, out var decodedDeltaMessage));
        var decodedDelta = Assert.IsType<SnapshotMessage>(decodedDeltaMessage);
        var merged = SnapshotDelta.ToFullSnapshot(decodedDelta, baseline);
        var mergedPlayer = Assert.Single(merged.Players);
        Assert.True(mergedPlayer.IsDispenserBuffed);
        Assert.Equal(1.25f, mergedPlayer.DispenserAttackReloadSpeedMultiplier);
        Assert.Equal(4, mergedPlayer.CurrentCombo);
        Assert.Equal(75, mergedPlayer.ComboTicksRemaining);
        Assert.Equal(55, mergedPlayer.ExperimentalCryoSlowTicksRemaining);
        Assert.Equal(28, mergedPlayer.ExperimentalCryoFreezeTicksRemaining);
        Assert.InRange(mergedPlayer.ExperimentalCryoExposureFraction, 0.25f, 0.26f);
        Assert.Equal(18, mergedPlayer.ExperimentalGhostVisibilityTicksRemaining);
        Assert.InRange(mergedPlayer.ExperimentalGhostTrailAlpha, 0.25f, 0.26f);
        Assert.True(receiver.ApplySnapshot(merged));
        Assert.Equal(4, receiver.LocalPlayer.CurrentCombo);
        Assert.Equal(75, receiver.LocalPlayer.ComboTicksRemaining);
        Assert.True(receiver.LocalPlayer.IsExperimentalCryoFrozen);
        Assert.InRange(receiver.LocalPlayer.ExperimentalCryoExposureFraction, 0.25f, 0.26f);
        Assert.InRange(receiver.LocalPlayer.ExperimentalGhostDashTrailAlpha, 0.25f, 0.26f);
    }

    [Fact]
    public void Protocol64DispenserBuffRoundTripsAndApplies()
    {
        var source = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        source.LocalPlayer.SetDispenserBuffed(true, 1.25f);
        source.LocalPlayer.RegisterCombatComboHit(120);
        var published = Assert.Single(
            new Protocol64StatePublisher(source).BuildPlayerStateBatch(12).Players);
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
        var decodedPlayer = Assert.Single(decoded.Event!.Players);
        Assert.True(decodedPlayer.IsDispenserBuffed);
        Assert.Equal(1.25f, decodedPlayer.DispenserAttackReloadSpeedMultiplier);
        Assert.Equal(1, decodedPlayer.CurrentCombo);
        Assert.Equal(120, decodedPlayer.ComboTicksRemaining);

        var receiver = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        Assert.True(receiver.ApplyProtocol64PlayerState(decodedPlayer));
        Assert.True(receiver.LocalPlayer.IsDispenserBuffed);
        Assert.Equal(1.25f, receiver.LocalPlayer.DispenserAttackReloadSpeedMultiplier);
        Assert.Equal(1, receiver.LocalPlayer.CurrentCombo);
        Assert.Equal(120, receiver.LocalPlayer.ComboTicksRemaining);
    }

    [Fact]
    public void Protocol64RageStateRoundTripsAndApplies()
    {
        var source = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        source.LocalPlayer.AddRageCharge(
            ExperimentalGameplaySettings.RageMaxCharge,
            ExperimentalGameplaySettings.RageMaxCharge);
        var published = Assert.Single(
            new Protocol64StatePublisher(source).BuildPlayerStateBatch(12).Players);

        Assert.Equal(ExperimentalGameplaySettings.RageMaxCharge, published.RageCharge);
        Assert.True(published.IsRageReady);
        Assert.Equal(0, published.RageTicksRemaining);

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
        var decodedPlayer = Assert.Single(decoded.Event!.Players);

        var receiver = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        Assert.True(receiver.ApplyProtocol64PlayerState(decodedPlayer));
        Assert.Equal(ExperimentalGameplaySettings.RageMaxCharge, receiver.LocalPlayer.RageCharge);
        Assert.True(receiver.LocalPlayer.IsRageReady);
        Assert.Equal(0, receiver.LocalPlayer.RageTicksRemaining);

        Assert.True(source.LocalPlayer.TryStartRage(17));
        var activePublished = Assert.Single(
            new Protocol64StatePublisher(source).BuildPlayerStateBatch(13).Players);
        Assert.Equal(0f, activePublished.RageCharge);
        Assert.False(activePublished.IsRageReady);
        Assert.Equal(17, activePublished.RageTicksRemaining);
    }

    [Fact]
    public void LegacySnapshotRageStateRoundTripsAndExtendedDeltaMerges()
    {
        var source = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        source.LocalPlayer.AddRageCharge(250f, ExperimentalGameplaySettings.RageMaxCharge);
        var stringCache = new SnapshotStringCache();
        var player = source.Snapshots.ToSnapshotPlayerState(
            SimulationWorld.LocalPlayerSlot,
            source.LocalPlayer,
            source.LocalPlayer,
            value => stringCache.GetOrAddCacheId(value));
        var fullSnapshot = CreateSnapshot(player);

        var payload = ProtocolCodec.Serialize(fullSnapshot, ProtocolCompressionSettings.Disabled);
        Assert.True(ProtocolCodec.TryDeserialize(payload, out var decodedMessage));
        var decoded = Assert.IsType<SnapshotMessage>(decodedMessage);
        var decodedPlayer = Assert.Single(decoded.Players);
        Assert.Equal(250f, decodedPlayer.RageCharge);
        Assert.False(decodedPlayer.IsRageReady);
        Assert.Equal(0, decodedPlayer.RageTicksRemaining);

        var receiver = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        Assert.True(receiver.ApplySnapshot(decoded));
        Assert.Equal(250f, receiver.LocalPlayer.RageCharge);
        Assert.False(receiver.LocalPlayer.IsRageReady);

        var baseline = CreateSnapshot(player with
        {
            RageCharge = 0f,
            IsRageReady = false,
            RageTicksRemaining = 0,
        }) with { Frame = 20 };
        var rageExtendedStatus = new SnapshotPlayerExtendedStatusState(
            Slot: SimulationWorld.LocalPlayerSlot,
            IsSpyCloaked: false,
            SpyCloakAlpha: 1f,
            IsSpySuperjumping: false,
            SpySuperjumpHorizontalVelocity: 0f,
            SpySuperjumpCooldownTicksRemaining: 0,
            SpyBackstabVisualTicksRemaining: 0,
            IsUbered: false,
            IsKritzCritBoosted: false,
            IsHeavyEating: false,
            HeavyEatTicksRemaining: 0,
            IsSniperScoped: false,
            RageCharge: 250f,
            IsRageReady: false,
            RageTicksRemaining: 0);
        var delta = baseline with
        {
            Frame = 21,
            BaselineFrame = 20,
            IsDelta = true,
            Players = [],
            PlayerExtendedStatusStates = [rageExtendedStatus],
        };

        var deltaPayload = ProtocolCodec.Serialize(delta, ProtocolCompressionSettings.Disabled);
        Assert.True(ProtocolCodec.TryDeserialize(deltaPayload, out var decodedDeltaMessage));
        var decodedDelta = Assert.IsType<SnapshotMessage>(decodedDeltaMessage);
        var merged = SnapshotDelta.ToFullSnapshot(decodedDelta, baseline);
        var mergedPlayer = Assert.Single(merged.Players);
        Assert.Equal(250f, mergedPlayer.RageCharge);
        Assert.False(mergedPlayer.IsRageReady);
        Assert.Equal(0, mergedPlayer.RageTicksRemaining);
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
