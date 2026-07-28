using OpenGarrison.Client;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class Protocol64StateApplierTests
{
    [Fact]
    public void InlineClassIdentityCannotBeChangedByAnOlderGenerationOrStaleState()
    {
        var applier = new Protocol64StateApplier();
        var first = new Protocol64PlayerStateBatch(1, 10, [Player(2, 99, 4, "class.overweight", 150)]);
        var stale = new Protocol64PlayerStateBatch(0, 9, [Player(2, 99, 4, "class.rocketman", 1)]);

        Assert.Equal(Protocol64StateApplyStatus.Applied, applier.ApplyPlayerStateBatch(first).Status);
        Assert.Equal(Protocol64StateApplyStatus.RepairRequested, applier.ApplyPlayerStateBatch(stale).Status);
        Assert.Equal("class.overweight", Assert.Single(applier.Players).GameplayClassId);
        Assert.Equal(150, Assert.Single(applier.Players).Health);
    }

    [Fact]
    public void ProjectileKindReuseRequiresAGenerationChange()
    {
        var applier = new Protocol64StateApplier();
        var rocket = Projectile(7, 1, Protocol64ProjectileKind.Rocket, 10);
        var wrongKind = Projectile(7, 1, Protocol64ProjectileKind.Flame, 11);
        var replacement = Projectile(7, 2, Protocol64ProjectileKind.Flame, 12);

        Assert.Equal(Protocol64StateApplyStatus.Applied, applier.ApplyProjectileState(rocket).Status);
        Assert.Equal(Protocol64StateApplyStatus.RepairRequested, applier.ApplyProjectileState(wrongKind).Status);
        Assert.Equal(Protocol64StateApplyStatus.Applied, applier.ApplyProjectileState(replacement).Status);
        Assert.Equal(Protocol64ProjectileKind.Flame, Assert.Single(applier.Projectiles).EntityKind);
    }

    [Fact]
    public void InvalidResyncDoesNotPartiallyReplaceTheExistingView()
    {
        var applier = new Protocol64StateApplier();
        Assert.Equal(Protocol64StateApplyStatus.Applied, applier.ApplyPlayerStateBatch(
            new Protocol64PlayerStateBatch(1, 1, [Player(1, 1, 1, "class.scout", 100)])).Status);

        var invalid = new Protocol64StateResyncResponse(
            4,
            2,
            2,
            [Player(2, 2, 1, "class.medic", 100), Player(2, 2, 1, "class.spy", 100)],
            [],
            [],
            []);

        Assert.Equal(Protocol64StateApplyStatus.RepairRequested, applier.ApplyResyncResponse(invalid).Status);
        Assert.Equal("class.scout", Assert.Single(applier.Players).GameplayClassId);
    }

    [Fact]
    public void RosterRejectsCompetingIdentitiesForTheSameSlot()
    {
        var applier = new Protocol64StateApplier();
        var result = applier.ApplyRosterState(new Protocol64RosterState(
            1,
            1,
            [
                new Protocol64PlayerIdentity(2, 22, 1),
                new Protocol64PlayerIdentity(2, 23, 1),
            ],
            []));

        Assert.Equal(Protocol64StateApplyStatus.RepairRequested, result.Status);
        Assert.Empty(applier.Players);
    }

    [Fact]
    public void RosterRemovalTombstoneRejectsAnOlderPlayerBatchThatArrivesLate()
    {
        var applier = new Protocol64StateApplier();
        var player = Player(3, 33, 1, "class.scout", 100);

        Assert.Equal(
            Protocol64StateApplyStatus.Applied,
            applier.ApplyPlayerStateBatch(new Protocol64PlayerStateBatch(1, 1, [player])).Status);
        Assert.Equal(
            Protocol64StateApplyStatus.Applied,
            applier.ApplyRosterState(new Protocol64RosterState(
                1,
                2,
                [],
                [new Protocol64PlayerIdentity(player.Slot, player.PlayerId, player.Generation)])).Status);

        Assert.Equal(
            Protocol64StateApplyStatus.Applied,
            applier.ApplyPlayerStateBatch(new Protocol64PlayerStateBatch(2, 1, [player])).Status);
        Assert.Empty(applier.Players);
    }

    [Fact]
    public void ResyncResponseMustMatchARequestAndThenReplacesTheViewAtomically()
    {
        var applier = new Protocol64StateApplier();
        var request = applier.CreateResyncRequest(Protocol64StateResyncReason.ClientRequested);
        var response = new Protocol64StateResyncResponse(
            request.RequestId,
            2,
            2,
            [Player(4, 44, 1, "class.medic", 150)],
            [],
            [],
            []);

        Assert.Equal(Protocol64StateApplyStatus.Applied, applier.ApplyResyncResponse(response).Status);
        Assert.Equal("class.medic", Assert.Single(applier.Players).GameplayClassId);
    }

    [Fact]
    public void ValidatedStateIsCommittedIntoTheLiveSimulationWorld()
    {
        var applier = new Protocol64StateApplier();
        Assert.Equal(
            Protocol64StateApplyStatus.Applied,
            applier.ApplyPlayerStateBatch(new Protocol64PlayerStateBatch(
                1,
                1,
                [Player(2, 22, 1, StockGameplayModCatalog.GetClassId(PlayerClass.Scout), 75) with { X = 123f, Y = 45f }])).Status);

        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        applier.ApplyToWorld(world);

        Assert.True(world.TryGetNetworkPlayer(2, out var player));
        Assert.Equal(StockGameplayModCatalog.GetClassId(PlayerClass.Scout), player.GameplayClassId);
        Assert.Equal(75, player.Health);
        Assert.Equal(123f, player.X);
        Assert.Equal(45f, player.Y);
    }

    [Fact]
    public void PlayerStateExposesTheLocalInputWatermarkForReconciliation()
    {
        var applier = new Protocol64StateApplier();
        var player = Player(2, 22, 1, StockGameplayModCatalog.GetClassId(PlayerClass.Scout), 75)
            with { LastProcessedInputSequence = 17 };

        Assert.Equal(
            Protocol64StateApplyStatus.Applied,
            applier.ApplyPlayerStateBatch(new Protocol64PlayerStateBatch(8, 100, [player])).Status);

        Assert.True(applier.TryGetPlayerState(2, out var applied));
        Assert.Equal(17U, applied.LastProcessedInputSequence);
    }

    [Fact]
    public void StateApplierResetDropsOldStateAndRepairRequests()
    {
        var applier = new Protocol64StateApplier();
        Assert.Equal(
            Protocol64StateApplyStatus.Applied,
            applier.ApplyPlayerStateBatch(new Protocol64PlayerStateBatch(3, 10, [Player(2, 22, 1, "class.scout", 75)])).Status);
        applier.CreateResyncRequest(Protocol64StateResyncReason.ClientRequested);

        applier.Reset();

        Assert.Equal(0UL, applier.PlayerStateSequence);
        Assert.Empty(applier.Players);
        Assert.False(applier.TryGetPlayerState(2, out _));
        var request = applier.CreateResyncRequest(Protocol64StateResyncReason.InitialState);
        Assert.Equal(1UL, request.RequestId);
    }

    [Fact]
    public void NewerStateSequenceCannotRollBackAnInputWatermark()
    {
        var applier = new Protocol64StateApplier();
        var current = Player(2, 22, 1, "class.scout", 100) with { LastProcessedInputSequence = 20 };
        var regressed = current with { LastProcessedInputSequence = 19 };

        Assert.Equal(
            Protocol64StateApplyStatus.Applied,
            applier.ApplyPlayerStateBatch(new Protocol64PlayerStateBatch(1, 1, [current])).Status);
        Assert.Equal(
            Protocol64StateApplyStatus.Stale,
            applier.ApplyPlayerStateBatch(new Protocol64PlayerStateBatch(2, 2, [regressed])).Status);
        Assert.Equal(20U, Assert.Single(applier.Players).LastProcessedInputSequence);
    }

    private static Protocol64PlayerState Player(ushort slot, ulong playerId, uint generation, string classId, int health)
        => new(slot, playerId, generation, classId, health, 200, 1, true, 0, 0, 0, 0, 0, 0, 1);

    private static Protocol64ProjectileState Projectile(ulong id, uint generation, Protocol64ProjectileKind kind, uint tick)
        => new(id, generation, kind, tick, 1, 1, 0, 0, 1, 0, 0, true, 20, 10);
}
