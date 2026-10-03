using OpenGarrison.Core;
using OpenGarrison.Protocol;
using OpenGarrison.Server;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

[Collection(ContentRootTestGroup.Name)]
public sealed class WhippingCordSnapshotHydrationTests
{
    [Fact]
    public void SnapshotHydratesWhippingCordLatchAndAnchorEvenWhenRuntimeStateStorageIsFull()
    {
        var source = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        source.PrepareLocalPlayerJoin();
        source.SetLocalPlayerTeam(PlayerTeam.Red);
        source.CompleteLocalPlayerJoin(PlayerClass.Engineer);
        Assert.True(source.LocalPlayer.TrySelectGameplayPrimaryItem(WhippingCordCatalog.ItemId));
        source.LocalPlayer.LatchWhippingCord(480f, 320f, 75f);

        var stringCache = new SnapshotStringCache();
        var sourcePlayer = source.Snapshots.ToSnapshotPlayerState(
            2,
            source.LocalPlayer,
            source.LocalPlayer,
            value => stringCache.GetOrAddCacheId(value));
        var saturatedState = Enumerable.Range(0, 20)
            .Select(index => new SnapshotReplicatedStateEntry(
                "test.mod",
                $"occupied_{index}",
                SnapshotReplicatedStateValueKind.Toggle,
                0,
                0f,
                true))
            .Concat(
            [
                new SnapshotReplicatedStateEntry(
                    PlayerEntity.CoreReplicatedStateOwnerId,
                    WhippingCordCatalog.ReplicatedLatchKey,
                    SnapshotReplicatedStateValueKind.Toggle,
                    0,
                    0f,
                    true),
                new SnapshotReplicatedStateEntry(
                    PlayerEntity.CoreReplicatedStateOwnerId,
                    WhippingCordCatalog.ReplicatedAnchorXKey,
                    SnapshotReplicatedStateValueKind.Scalar,
                    0,
                    480f,
                    false),
                new SnapshotReplicatedStateEntry(
                    PlayerEntity.CoreReplicatedStateOwnerId,
                    WhippingCordCatalog.ReplicatedAnchorYKey,
                    SnapshotReplicatedStateValueKind.Scalar,
                    0,
                    320f,
                    false),
                new SnapshotReplicatedStateEntry(
                    PlayerEntity.CoreReplicatedStateOwnerId,
                    WhippingCordCatalog.ReplicatedRopeLengthKey,
                    SnapshotReplicatedStateValueKind.Scalar,
                    0,
                    75f,
                    false),
            ])
            .ToArray();
        var remotePlayer = sourcePlayer with
        {
            PlayerId = 202,
            Name = "Remote Engineer",
            ReplicatedStates = saturatedState,
        };

        var receiver = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        var localPlayer = SimulationWorldSnapshotPresentationTests.CreatePlayerState(
            1,
            101,
            "Local",
            PlayerTeam.Red,
            PlayerClass.Scout,
            isAlive: true,
            gibDeaths: 0);
        var snapshot = SimulationWorldSnapshotPresentationTests.CreateSnapshot(
            receiver,
            frame: 1,
            localPlayer,
            remotePlayer);

        Assert.True(receiver.ApplySnapshot(snapshot, localPlayerSlot: 1));

        var appliedPlayer = Assert.Single(receiver.RemoteSnapshotPlayers);
        Assert.Equal(24, remotePlayer.ReplicatedStates!.Count);
        Assert.False(appliedPlayer.TryGetReplicatedStateBool(
            PlayerEntity.CoreReplicatedStateOwnerId,
            WhippingCordCatalog.ReplicatedLatchKey,
            out _));
        Assert.False(appliedPlayer.TryGetReplicatedStateFloat(
            PlayerEntity.CoreReplicatedStateOwnerId,
            WhippingCordCatalog.ReplicatedAnchorXKey,
            out _));
        Assert.True(appliedPlayer.IsWhippingCordLatched);
        Assert.Equal(480f, appliedPlayer.WhippingCordAnchorX);
        Assert.Equal(320f, appliedPlayer.WhippingCordAnchorY);
        Assert.Equal(75f, appliedPlayer.WhippingCordRopeLength);
    }
}
