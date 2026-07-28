using System;
using System.Linq;
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
                    GameplayClassId: "class.scout",
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
                    RemainingAirJumps: 1),
            ]);

        var decoded = RoundTrip(registry, value, 1);

        Assert.Equal(value.StateSequence, decoded.StateSequence);
        Assert.Equal(value.StateTick, decoded.StateTick);
        var player = Assert.Single(decoded.Players);
        Assert.Equal(2, player.Slot);
        Assert.Equal(0x1234UL, player.PlayerId);
        Assert.Equal(7U, player.Generation);
        Assert.Equal("class.scout", player.GameplayClassId);
        Assert.Equal(87, player.Health);
        Assert.Equal(44U, player.LastProcessedInputSequence);
        Assert.True(player.IsGrounded);
        Assert.Equal(1, player.RemainingAirJumps);
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
            Projectiles: [new Protocol64ProjectileState(5, 6, Protocol64ProjectileKind.Flame, 500, 1, 2, 0, 0, 1, 0, 0, true, 12, 4)],
            RemovedProjectiles: [new Protocol64ProjectileIdentity(12, 1, Protocol64ProjectileKind.Rocket)]);

        var decoded = RoundTrip(registry, value, 3);
        Assert.Equal(value.RequestId, decoded.RequestId);
        Assert.Equal(value.StateSequence, decoded.StateSequence);
        Assert.Equal("class.medic", Assert.Single(decoded.Players).GameplayClassId);
        Assert.Equal(0U, Assert.Single(decoded.Players).LastProcessedInputSequence);
        Assert.Equal(6U, Assert.Single(decoded.Projectiles).Generation);
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
