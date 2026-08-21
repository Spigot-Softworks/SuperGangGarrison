using System;
using System.Linq;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class Protocol64StateEventTests
{
    [Fact]
    public void PlayerStateBatchRoundTripsInlineIdentityClassAndHealth()
    {
        var registry = CreateRegistry(new Protocol64PlayerStateBatchSchema());
        var value = new Protocol64PlayerStateBatch(
            StateSequence: 9,
            StateTick: 120,
            Players:
            [
                new Protocol64PlayerState(
                    Slot: 2,
                    PlayerId: 0x1234,
                    Generation: 7,
                    GameplayClassId: "spy",
                    Health: 87,
                    MaxHealth: 125,
                    Team: 1,
                    IsAlive: true,
                    X: 10.5f,
                    Y: -4.25f,
                    VelocityX: 2f,
                    VelocityY: -1f,
                    ActiveWeapon: 3,
                    AbilityState: 0x10,
                    StateTick: 120,
                    LastProcessedInputSequence: 44,
                    IsGrounded: true,
                    RemainingAirJumps: 1,
                    CurrentAmmo: 17,
                    MaxAmmo: 40,
                    OffhandAmmo: 4,
                    OffhandMaxAmmo: 6,
                    OffhandCooldownTicks: 2,
                    OffhandReloadTicks: 3,
                    LastToDieSpyRevolverState: 0x2D0,
                    IsSpyCloaked: true,
                    SpyCloakAlpha: 0.37f,
                    LastToDieSpyCloakMeterUnits: 731,
                    LastToDieSpyRogueRampStacks: 6,
                    LastToDieSpyRogueRampTicks: 41,
                    IsSpySuperjumping: true,
                    SpySuperjumpHorizontalVelocity: 123.5f,
                    SpySuperjumpCooldownTicksRemaining: 91,
                    SpySuperjumpAvailableCharges: 1,
                    SpySuperjumpMaximumCharges: 2,
                    SpySuperjumpChargeTicks: 12,
                    SpySuperjumpChargeDirectionDegrees: 271.5f,
                    SpySuperjumpChargeStartMovementButtons: 5,
                    SpySuperjumpChargeStartBlockedUntilAbilityRelease: true,
                    LastToDieMedicLinkState: 15,
                    AcquiredAmmo: 7,
                    AcquiredMaxAmmo: 10,
                    AcquiredCooldownTicks: 4,
                    AcquiredReloadTicks: 9,
                    MedicNeedleCooldownTicks: 2,
                    MedicNeedleRefillTicks: 21,
                    PyroPrimaryFuelScaled: 1234,
                    LastToDieMedicHailMaryTicksRemaining: 15,
                    ServerStunTicksRemaining: 60,
                    KritzCritBoostTicksRemaining: 12,
                    KritzCritBoostProviderPlayerId: 91,
                    KritzCritBoostProviderSlot: 3,
                    KritzCritBoostDamageMultiplier: 3.5f,
                    LastToDieProfessionalFireChordState: 2),
            ]);

        var decoded = RoundTrip(registry, value, 1);

        Assert.Equal(value.StateSequence, decoded.StateSequence);
        Assert.Equal(value.StateTick, decoded.StateTick);
        var player = Assert.Single(decoded.Players);
        Assert.Equal(2, player.Slot);
        Assert.Equal(0x1234UL, player.PlayerId);
        Assert.Equal(7U, player.Generation);
        Assert.Equal("spy", player.GameplayClassId);
        Assert.Equal(87, player.Health);
        Assert.Equal(44U, player.LastProcessedInputSequence);
        Assert.True(player.IsGrounded);
        Assert.Equal(1, player.RemainingAirJumps);
        Assert.Equal(17, player.CurrentAmmo);
        Assert.Equal(40, player.MaxAmmo);
        Assert.Equal(4, player.OffhandAmmo);
        Assert.Equal(6, player.OffhandMaxAmmo);
        Assert.Equal(2, player.OffhandCooldownTicks);
        Assert.Equal(3, player.OffhandReloadTicks);
        Assert.Equal((ushort)0x2D0, player.LastToDieSpyRevolverState);
        Assert.True(player.IsSpyCloaked);
        Assert.InRange(player.SpyCloakAlpha, 0.368f, 0.372f);
        Assert.Equal((ushort)731, player.LastToDieSpyCloakMeterUnits);
        Assert.Equal((byte)6, player.LastToDieSpyRogueRampStacks);
        Assert.Equal((ushort)41, player.LastToDieSpyRogueRampTicks);
        Assert.True(player.IsSpySuperjumping);
        Assert.Equal(123.5f, player.SpySuperjumpHorizontalVelocity);
        Assert.Equal((ushort)91, player.SpySuperjumpCooldownTicksRemaining);
        Assert.Equal((byte)1, player.SpySuperjumpAvailableCharges);
        Assert.Equal((byte)2, player.SpySuperjumpMaximumCharges);
        Assert.Equal((ushort)12, player.SpySuperjumpChargeTicks);
        Assert.Equal(271.5f, player.SpySuperjumpChargeDirectionDegrees);
        Assert.Equal((byte)5, player.SpySuperjumpChargeStartMovementButtons);
        Assert.True(player.SpySuperjumpChargeStartBlockedUntilAbilityRelease);
        Assert.Equal((byte)15, player.LastToDieMedicLinkState);
        Assert.Equal(7, player.AcquiredAmmo);
        Assert.Equal(10, player.AcquiredMaxAmmo);
        Assert.Equal(4, player.AcquiredCooldownTicks);
        Assert.Equal(9, player.AcquiredReloadTicks);
        Assert.Equal(2, player.MedicNeedleCooldownTicks);
        Assert.Equal(21, player.MedicNeedleRefillTicks);
        Assert.Equal(1234, player.PyroPrimaryFuelScaled);
        Assert.Equal((ushort)15, player.LastToDieMedicHailMaryTicksRemaining);
        Assert.Equal((ushort)60, player.ServerStunTicksRemaining);
        Assert.Equal(12, player.KritzCritBoostTicksRemaining);
        Assert.Equal(91, player.KritzCritBoostProviderPlayerId);
        Assert.Equal(3, player.KritzCritBoostProviderSlot);
        Assert.Equal(3.5f, player.KritzCritBoostDamageMultiplier);
        Assert.Equal((byte)2, player.LastToDieProfessionalFireChordState);
        Assert.Equal((ushort)20, registry.Get<Protocol64PlayerStateBatch>().Descriptor.Key.Revision);
    }

    [Fact]
    public void MedicUberDeliveryRuntimeRoundTripsModeTargetAndMeter()
    {
        var registry = CreateRegistry(new Protocol64PlayerStateBatchSchema());
        var value = new Protocol64PlayerStateBatch(
            StateSequence: 10,
            StateTick: 121,
            Players:
            [
                new Protocol64PlayerState(
                    Slot: 1,
                    PlayerId: 9,
                    Generation: 2,
                    GameplayClassId: "medic",
                    Health: 150,
                    MaxHealth: 150,
                    Team: 1,
                    IsAlive: true,
                    X: 0,
                    Y: 0,
                    VelocityX: 0,
                    VelocityY: 0,
                    ActiveWeapon: 0,
                    AbilityState: 0,
                    StateTick: 121,
                    MedicUberDeliveryState: 0x83,
                    MedicHealTargetId: 17,
                    MedicUberCharge: 1234.5f),
            ]);

        var decoded = RoundTrip(registry, value, 2);
        var medic = Assert.Single(decoded.Players);
        Assert.Equal((byte)0x83, medic.MedicUberDeliveryState);
        Assert.Equal(17, medic.MedicHealTargetId);
        Assert.Equal(1234.5f, medic.MedicUberCharge);
    }

    [Fact]
    public void InvalidMedicUberDeliveryRuntimeIsRejectedBeforeEncoding()
    {
        var registry = CreateRegistry(new Protocol64PlayerStateBatchSchema());
        var invalid = new Protocol64PlayerStateBatch(
            StateSequence: 1,
            StateTick: 1,
            Players:
            [
                new Protocol64PlayerState(
                    1, 9, 2, "medic", 150, 150, 1, true,
                    0, 0, 0, 0, 0, 0, 1,
                    MedicUberDeliveryState: 0x80),
            ]);

        var encoded = Protocol64FrameCodec.Encode(registry, invalid, connectionEpoch: 1, frameId: 1);

        Assert.False(encoded.Succeeded);
        Assert.Equal(Protocol64FaultKind.ValidationFailed, encoded.Fault!.Kind);
    }

    [Fact]
    public void RevolverProjectileStateRoundTripsExactLastToDieGameplayPayload()
    {
        var registry = CreateRegistry(new Protocol64ProjectileStateSchema());
        var value = new Protocol64ProjectileState(
            EntityId: 45,
            Generation: 3,
            EntityKind: Protocol64ProjectileKind.RevolverShot,
            StateTick: 121,
            OwnerSlot: 2,
            OwnerGeneration: 7,
            X: 1,
            Y: 2,
            VelocityX: 3,
            VelocityY: 4,
            Rotation: 0.5f,
            IsActive: true,
            RemainingLifetimeTicks: 39,
            Damage: 11.2f,
            IsCritical: true,
            LastToDieSpyRevolverProfile: 210,
            AppliesLastToDieLuckyStrikeStun: true,
            CriticalDamageMultiplier: 3.5f);

        var decoded = RoundTrip(registry, value, 2);

        Assert.Equal(value, decoded);
        Assert.Equal((ushort)10, registry.Get<Protocol64ProjectileState>().Descriptor.Key.Revision);
    }

    [Fact]
    public void MedicKritzM2PayloadRoundTripsStateAndLifecycle()
    {
        var stateRegistry = CreateRegistry(new Protocol64ProjectileStateSchema());
        var state = new Protocol64ProjectileState(
            EntityId: 47,
            Generation: 4,
            EntityKind: Protocol64ProjectileKind.Needle,
            StateTick: 122,
            OwnerSlot: 1,
            OwnerGeneration: 2,
            X: 10f,
            Y: 20f,
            VelocityX: 8f,
            VelocityY: -1f,
            Rotation: 0f,
            IsActive: true,
            RemainingLifetimeTicks: 30,
            Damage: 22f,
            IsCritical: true,
            LastToDieMedicKritzM2Payload: 0b111,
            CriticalDamageMultiplier: 3.5f);
        Assert.Equal(state, RoundTrip(stateRegistry, state, frameId: 7));

        var lifecycleRegistry = CreateRegistry(new Protocol64ProjectileLifecycleSchema());
        var lifecycle = new Protocol64ProjectileLifecycle(
            Lifecycle: Protocol64ProjectileLifecycleKind.Spawn,
            EntityId: state.EntityId,
            Generation: state.Generation,
            EntityKind: state.EntityKind,
            StateTick: state.StateTick,
            OwnerSlot: state.OwnerSlot,
            OwnerGeneration: state.OwnerGeneration,
            X: state.X,
            Y: state.Y,
            VelocityX: state.VelocityX,
            VelocityY: state.VelocityY,
            Rotation: state.Rotation,
            IsActive: state.IsActive,
            RemainingLifetimeTicks: state.RemainingLifetimeTicks,
            Damage: state.Damage,
            IsCritical: state.IsCritical,
            LastToDieMedicKritzM2Payload: state.LastToDieMedicKritzM2Payload,
            CriticalDamageMultiplier: state.CriticalDamageMultiplier);
        Assert.Equal(lifecycle, RoundTrip(lifecycleRegistry, lifecycle, frameId: 8));
    }

    [Fact]
    public void MedicJavelinStateRoundTripsImmutableSourceAndLifecycleState()
    {
        var stateRegistry = CreateRegistry(new Protocol64ProjectileStateSchema());
        var state = new Protocol64ProjectileState(
            EntityId: 48,
            Generation: 5,
            EntityKind: Protocol64ProjectileKind.Needle,
            StateTick: 123,
            OwnerSlot: 0,
            OwnerGeneration: 0,
            X: 40f,
            Y: 50f,
            VelocityX: 0f,
            VelocityY: 0f,
            Rotation: 0f,
            IsActive: true,
            RemainingLifetimeTicks: 20,
            Damage: 22f,
            LastToDieMedicKritzM2Payload: 0b1001,
            LastToDieMedicJavelinOwnerPlayerId: 321,
            LastToDieMedicJavelinTeam: 2,
            IsLastToDieMedicJavelinAnchored: true,
            LastToDieMedicJavelinFuseTicksRemaining: 20,
            HasLastToDieMedicJavelinExploded: false);
        Assert.Equal(state, RoundTrip(stateRegistry, state, frameId: 9));

        var lifecycleRegistry = CreateRegistry(new Protocol64ProjectileLifecycleSchema());
        var lifecycle = new Protocol64ProjectileLifecycle(
            Lifecycle: Protocol64ProjectileLifecycleKind.Despawn,
            EntityId: state.EntityId,
            Generation: state.Generation,
            EntityKind: state.EntityKind,
            StateTick: 124,
            OwnerSlot: state.OwnerSlot,
            OwnerGeneration: state.OwnerGeneration,
            X: state.X,
            Y: state.Y,
            VelocityX: state.VelocityX,
            VelocityY: state.VelocityY,
            Rotation: state.Rotation,
            IsActive: false,
            RemainingLifetimeTicks: 0,
            Damage: state.Damage,
            LastToDieMedicKritzM2Payload: state.LastToDieMedicKritzM2Payload,
            LastToDieMedicJavelinOwnerPlayerId: state.LastToDieMedicJavelinOwnerPlayerId,
            LastToDieMedicJavelinTeam: state.LastToDieMedicJavelinTeam,
            IsLastToDieMedicJavelinAnchored: true,
            LastToDieMedicJavelinFuseTicksRemaining: 0,
            HasLastToDieMedicJavelinExploded: true);
        Assert.Equal(lifecycle, RoundTrip(lifecycleRegistry, lifecycle, frameId: 10));
    }

    [Fact]
    public void SniperPlayerStateAcceptsAllClassSpecificProfileBits()
    {
        var registry = CreateRegistry(new Protocol64PlayerStateBatchSchema());
        var player = new Protocol64PlayerState(
            1, 9, 1, "sniper", 100, 100, 1, true,
            0, 0, 0, 0, 0, 0, 1,
            LastToDieSpyRevolverState: 0xFFF0,
            LastToDieSniperRuntimeState: (ushort)(17 | (100 << 7)),
            LastToDieSniperExtensionState: (ushort)(0b11 | (300 << 3) | (1 << 12) | (1 << 13) | (1 << 14)),
            LastToDieSniperVolleyState: new Protocol64LastToDieSniperVolleyState(
                QueuedArrowCount: 1,
                DueArrowCount: 1,
                SourceTicksUntilNextArrow: 2,
                VelocityX: 12f,
                VelocityY: -3f,
                Damage: 120,
                FakeSpeedMultiplier: 1.5f,
                PayloadFlags: 0x7f,
                PoisonDamagePerSecond: 14f,
                GhostDamageMultiplier: 3f));

        var decoded = RoundTrip(
            registry,
            new Protocol64PlayerStateBatch(1, 1, [player]),
            frameId: 9);

        var decodedPlayer = Assert.Single(decoded.Players);
        Assert.Equal((ushort)0xFFF0, decodedPlayer.LastToDieSpyRevolverState);
        Assert.Equal((ushort)(17 | (100 << 7)), decodedPlayer.LastToDieSniperRuntimeState);
        Assert.Equal((ushort)(0b11 | (300 << 3) | (1 << 12) | (1 << 13) | (1 << 14)), decodedPlayer.LastToDieSniperExtensionState);
        Assert.Equal(player.LastToDieSniperVolleyState, decodedPlayer.LastToDieSniperVolleyState);
    }

    [Fact]
    public void ArrowProjectileStateRoundTripsHuntsmanGameplayPayload()
    {
        var registry = CreateRegistry(new Protocol64ProjectileStateSchema());
        var value = new Protocol64ProjectileState(
            EntityId: 46,
            Generation: 3,
            EntityKind: Protocol64ProjectileKind.Arrow,
            StateTick: 121,
            OwnerSlot: 2,
            OwnerGeneration: 7,
            X: 1,
            Y: 2,
            VelocityX: 3,
            VelocityY: 4,
            Rotation: 0.5f,
            IsActive: true,
            RemainingLifetimeTicks: 39,
            Damage: 75f,
            IsCritical: true,
            ArrowFakeSpeedMultiplier: 1.5f,
            IsArrowLanded: true,
            AppliesLastToDieGuardian: true,
            PiercesPlayers: true,
            AppliesLastToDieTranqDarts: true,
            LastToDiePoisonDamagePerSecond: 14.5f,
            LastToDieGhostDamageMultiplier: 3f,
            AppliesLastToDieDecapitator: true,
            IsLastToDieDecapitatorFullyCharged: true,
            LastToDieAttachedHeadClassId: (byte)PlayerClass.Heavy,
            LastToDieAttachedHeadTeam: (byte)PlayerTeam.Blue,
            AppliesLastToDieExplosiveTip: true);

        Assert.Equal(value, RoundTrip(registry, value, frameId: 10));
    }

    [Fact]
    public void ProjectileLifecycleRoundTripsKindAndGenerationWithDeliveryMetadata()
    {
        var registry = CreateRegistry(new Protocol64ProjectileLifecycleSchema());
        var schema = registry.Get<Protocol64ProjectileLifecycle>();
        Assert.Equal(Protocol64DeliveryKind.ReliableUnordered, schema.Descriptor.Delivery.Kind);
        Assert.Equal(ChannelType.GameplayEvents, schema.Descriptor.Delivery.Channel);

        var value = new Protocol64ProjectileLifecycle(
            Lifecycle: Protocol64ProjectileLifecycleKind.Spawn,
            EntityId: 44,
            Generation: 3,
            EntityKind: Protocol64ProjectileKind.Rocket,
            StateTick: 121,
            OwnerSlot: 2,
            OwnerGeneration: 7,
            X: 1,
            Y: 2,
            VelocityX: 3,
            VelocityY: 4,
            Rotation: 0.5f,
            IsActive: true,
            RemainingLifetimeTicks: 80,
            Damage: 90);

        var decoded = RoundTrip(registry, value, 2);
        Assert.Equal(value, decoded);
        Assert.Equal((ushort)10, schema.Descriptor.Key.Revision);
    }

    [Fact]
    public void StateResyncResponseCanRebuildWithoutAnEarlierBaseline()
    {
        var registry = CreateRegistry(new Protocol64StateResyncResponseSchema());
        var value = new Protocol64StateResyncResponse(
            RequestId: 71,
            StateSequence: 18,
            StateTick: 500,
            Players: [new Protocol64PlayerState(1, 9, 2, "class.medic", 100, 150, 2, true, 0, 0, 0, 0, 1, 3, 500)],
            RemovedPlayers: [new Protocol64PlayerIdentity(3, 11, 4)],
            Projectiles:
            [
                new Protocol64ProjectileState(
                    5,
                    6,
                    Protocol64ProjectileKind.Needle,
                    500,
                    1,
                    2,
                    0,
                    0,
                    1,
                    0,
                    0,
                    true,
                    12,
                    22,
                    LastToDieMedicKritzM2Payload: 0b111),
            ],
            RemovedProjectiles: [new Protocol64ProjectileIdentity(12, 1, Protocol64ProjectileKind.Rocket)]);

        var decoded = RoundTrip(registry, value, 3);
        Assert.Equal(value.RequestId, decoded.RequestId);
        Assert.Equal(value.StateSequence, decoded.StateSequence);
        Assert.Equal("class.medic", Assert.Single(decoded.Players).GameplayClassId);
        Assert.Equal(0U, Assert.Single(decoded.Players).LastProcessedInputSequence);
        Assert.Equal(6U, Assert.Single(decoded.Projectiles).Generation);
        Assert.Equal((byte)0b111, Assert.Single(decoded.Projectiles).LastToDieMedicKritzM2Payload);
        Assert.Equal(Protocol64DeliveryKind.ReliableOrdered, registry.Get<Protocol64StateResyncResponse>().Descriptor.Delivery.Kind);
    }

    [Fact]
    public void InvalidPlayerIdentityIsRejectedBeforeEncoding()
    {
        var registry = CreateRegistry(new Protocol64PlayerStateBatchSchema());
        var invalid = new Protocol64PlayerStateBatch(
            StateSequence: 1,
            StateTick: 1,
            Players: [new Protocol64PlayerState(1, 0, 2, "class.scout", 10, 10, 1, true, 0, 0, 0, 0, 0, 0, 1)]);

        var encoded = Protocol64FrameCodec.Encode(registry, invalid, connectionEpoch: 1, frameId: 1);

        Assert.False(encoded.Succeeded);
        Assert.Equal(Protocol64FaultKind.ValidationFailed, encoded.Fault!.Kind);
    }

    [Fact]
    public void InvalidProjectileGenerationAndUnknownKindAreRejected()
    {
        var registry = CreateRegistry(new Protocol64ProjectileStateSchema());
        var invalid = new Protocol64ProjectileState(8, 0, (Protocol64ProjectileKind)255, 1, 1, 1, 0, 0, 0, 0, 0, true, 1, 1);

        var encoded = Protocol64FrameCodec.Encode(registry, invalid, connectionEpoch: 1, frameId: 1);

        Assert.False(encoded.Succeeded);
        Assert.Equal(Protocol64FaultKind.ValidationFailed, encoded.Fault!.Kind);
    }

    [Fact]
    public void SchemasUseDisjointStableIdsAndExplicitStateDelivery()
    {
        var schemas = new IProtocol64EventSchema[]
        {
            new Protocol64PlayerStateBatchSchema(),
            new Protocol64RosterStateSchema(),
            new Protocol64ProjectileStateSchema(),
            new Protocol64ProjectileLifecycleSchema(),
            new Protocol64StateResyncRequestSchema(),
            new Protocol64StateResyncResponseSchema(),
        };

        Assert.Equal(new ushort[] { 32, 33, 34, 35, 36, 37 },
            schemas.Select(schema => schema.Descriptor.Key.SchemaId).ToArray());
        Assert.Equal(ChannelType.State, schemas[0].Descriptor.Delivery.Channel);
        Assert.Equal(Protocol64DeliveryKind.LastWins, schemas[2].Descriptor.Delivery.Kind);
        Assert.Equal(ChannelType.Control, schemas[4].Descriptor.Delivery.Channel);
        Assert.Equal(
            new ushort[] { 20, 1, 10, 10, 1, 24 },
            schemas.Select(schema => schema.Descriptor.Key.Revision).ToArray());
    }

    [Fact]
    public void InvalidDecapitatorArrowAttachmentIsRejectedBeforeEncoding()
    {
        var registry = CreateRegistry(new Protocol64ProjectileStateSchema());
        var value = new Protocol64ProjectileState(
            EntityId: 46,
            Generation: 3,
            EntityKind: Protocol64ProjectileKind.Arrow,
            StateTick: 121,
            OwnerSlot: 2,
            OwnerGeneration: 7,
            X: 1,
            Y: 2,
            VelocityX: 3,
            VelocityY: 4,
            Rotation: 0.5f,
            IsActive: true,
            RemainingLifetimeTicks: 39,
            Damage: 75f,
            AppliesLastToDieDecapitator: true,
            IsLastToDieDecapitatorFullyCharged: true,
            LastToDieAttachedHeadClassId: (byte)PlayerClass.Heavy,
            LastToDieAttachedHeadTeam: 0);

        var encoded = Protocol64FrameCodec.Encode(
            registry,
            value,
            connectionEpoch: 1,
            frameId: 11);

        Assert.False(encoded.Succeeded);
        Assert.Equal(Protocol64FaultKind.ValidationFailed, encoded.Fault!.Kind);
    }

    [Theory]
    [InlineData("sniper", 0x1000, 41)]
    [InlineData("sniper", 0x2000, 12928)]
    [InlineData("class.scout", 0, 1)]
    public void InvalidSniperRuntimeStateIsRejectedBeforeEncoding(
        string gameplayClassId,
        int profile,
        int runtime)
    {
        var registry = CreateRegistry(new Protocol64PlayerStateBatchSchema());
        var player = new Protocol64PlayerState(
            1, 9, 1, gameplayClassId, 100, 100, 1, true,
            0, 0, 0, 0, 0, 0, 1,
            LastToDieSpyRevolverState: checked((ushort)profile),
            LastToDieSniperRuntimeState: checked((ushort)runtime));

        var encoded = Protocol64FrameCodec.Encode(
            registry,
            new Protocol64PlayerStateBatch(1, 1, [player]),
            connectionEpoch: 1,
            frameId: 1);

        Assert.False(encoded.Succeeded);
        Assert.Equal(Protocol64FaultKind.ValidationFailed, encoded.Fault!.Kind);
    }

    [Fact]
    public void RogueCommanderRampAboveTenIsRejectedBeforeEncoding()
    {
        var registry = CreateRegistry(new Protocol64PlayerStateBatchSchema());
        var player = new Protocol64PlayerState(
            1, 9, 1, "class.infiltrator", 100, 100, 1, true,
            0, 0, 0, 0, 0, 0, 1,
            LastToDieSpyRogueRampStacks: 11);

        var encoded = Protocol64FrameCodec.Encode(
            registry,
            new Protocol64PlayerStateBatch(1, 1, [player]),
            connectionEpoch: 1,
            frameId: 1);

        Assert.False(encoded.Succeeded);
        Assert.Equal(Protocol64FaultKind.ValidationFailed, encoded.Fault!.Kind);
    }

    [Fact]
    public void UnknownMedicLinkRoleBitIsRejectedBeforeEncoding()
    {
        var registry = CreateRegistry(new Protocol64PlayerStateBatchSchema());
        var player = new Protocol64PlayerState(
            1, 9, 1, "class.medic", 100, 100, 1, true,
            0, 0, 0, 0, 0, 0, 1,
            LastToDieMedicLinkState: 1 << 4);

        var encoded = Protocol64FrameCodec.Encode(
            registry,
            new Protocol64PlayerStateBatch(1, 1, [player]),
            connectionEpoch: 1,
            frameId: 1);

        Assert.False(encoded.Succeeded);
        Assert.Equal(Protocol64FaultKind.ValidationFailed, encoded.Fault!.Kind);
    }

    private static Protocol64SchemaRegistry CreateRegistry(IProtocol64EventSchema schema)
    {
        var registry = new Protocol64SchemaRegistry();
        registry.Register(schema);
        return registry;
    }

    private static TEvent RoundTrip<TEvent>(Protocol64SchemaRegistry registry, TEvent value, ulong frameId)
    {
        var encoded = Protocol64FrameCodec.Encode(
            registry,
            value!,
            connectionEpoch: 1,
            frameId,
            options: new Protocol64FrameEncodeOptions { Compression = Protocol64Compression.None });
        Assert.True(encoded.Succeeded, encoded.Fault?.Message);

        var decoded = Protocol64FrameCodec.Decode<TEvent>(encoded.Payload!, registry);
        Assert.True(decoded.Succeeded, decoded.Fault?.Message);
        return decoded.Event!;
    }
}
