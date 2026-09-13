using System.IO;
using System.Linq;
using OpenGarrison.Client;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using OpenGarrison.Server;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class PlaytestEquipmentReplicationTests
{
    [Theory]
    [InlineData(PlayerClass.Sniper, "weapon.bow")]
    [InlineData(PlayerClass.Scout, "weapon.scout-nailgun")]
    [InlineData(PlayerClass.Medic, "weapon.medigun.crit")]
    public void FastStateHydratesLockerIdentityBeforeAmmoWithoutALegacySnapshot(PlayerClass playerClass, string itemId)
    {
        var source = JoinedWorld(playerClass);
        Assert.True(source.LocalPlayer.TrySelectGameplayPrimaryItem(itemId));
        var state = Assert.Single(new Protocol64StatePublisher(source).BuildPlayerStateBatch(1).Players);
        var receiver = JoinedWorld(playerClass);

        Assert.True(receiver.ApplyProtocol64PlayerState(state));

        Assert.Equal(itemId, receiver.LocalPlayer.SelectedGameplayPrimaryItemId);
        Assert.Equal(itemId, receiver.LocalPlayer.GameplayLoadoutState.EquippedItemId);
        Assert.Equal(source.LocalPlayer.PrimaryWeapon, receiver.LocalPlayer.PrimaryWeapon);
        Assert.Equal(state.CurrentAmmo, receiver.LocalPlayer.CurrentShells);
        Assert.Equal(state.MaxAmmo, receiver.LocalPlayer.MaxShells);
    }

    [Fact]
    public void MalformedEquipmentCannotAdvanceTheInputAcknowledgement()
    {
        var source = JoinedWorld(PlayerClass.Scout);
        var state = Assert.Single(new Protocol64StatePublisher(source).BuildPlayerStateBatch(1).Players)
            with { LastProcessedInputSequence = 12 };
        var applier = new Protocol64StateApplier();
        Assert.True(applier.ApplyPlayerStateBatch(new Protocol64PlayerStateBatch(1, 1, [state])).Applied);
        var invalid = state with
        {
            LastProcessedInputSequence = 20,
            Equipment = state.Equipment! with { ModPackId = "unavailable-pack" },
        };
        var result = applier.ApplyPlayerStateBatch(new Protocol64PlayerStateBatch(2, 2, [invalid]));
        Assert.Equal(Protocol64StateApplyStatus.RepairRequested, result.Status);
        Assert.Equal(12u, Assert.Single(applier.Players).LastProcessedInputSequence);
    }

    [Fact]
    public void EquipmentAndWatermarkSurviveTheWireTogether()
    {
        var source = JoinedWorld(PlayerClass.Scout);
        Assert.True(source.LocalPlayer.TrySelectGameplayPrimaryItem("weapon.scout-nailgun"));
        var state = Assert.Single(new Protocol64StatePublisher(source).BuildPlayerStateBatch(1).Players)
            with { LastProcessedInputSequence = 83 };
        var schema = new Protocol64PlayerStateBatchSchema();
        var batch = new Protocol64PlayerStateBatch(4, 123, [state]);
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
            schema.WriteBody(batch, writer);
        stream.Position = 0;
        using var reader = new BinaryReader(stream);
        var decoded = Assert.Single(schema.ReadBody(reader).Players);

        Assert.Equal(state.Equipment, decoded.Equipment);
        Assert.Equal(83u, decoded.LastProcessedInputSequence);
        Assert.Equal(state.CurrentAmmo, decoded.CurrentAmmo);
        Assert.Equal(stream.Length, stream.Position);
    }

    [Fact]
    public void ClientSlotTwoReceivesItsOwnAmmoWhileSlotOneRemainsRemote()
    {
        var heavy = JoinedWorld(PlayerClass.Heavy);
        var scout = JoinedWorld(PlayerClass.Scout);
        var local = Assert.Single(new Protocol64StatePublisher(heavy).BuildPlayerStateBatch(1).Players)
            with { Slot = 2, PlayerId = 22, CurrentAmmo = 37 };
        var remote = Assert.Single(new Protocol64StatePublisher(scout).BuildPlayerStateBatch(1).Players)
            with { Slot = 1, PlayerId = 11, CurrentAmmo = 2 };
        var receiver = JoinedWorld(PlayerClass.Heavy);
        var applier = new Protocol64StateApplier();
        Assert.True(applier.ApplyPlayerStateBatch(new Protocol64PlayerStateBatch(1, 1, [local, remote])).Applied);

        applier.ApplyToWorld(receiver, clientLocalPlayerSlot: 2);

        Assert.Equal(PlayerClass.Heavy, receiver.LocalPlayer.ClassId);
        Assert.Equal(37, receiver.LocalPlayer.CurrentShells);
        var remotePlayer = Assert.Single(receiver.RemoteSnapshotPlayers);
        Assert.Equal(11, remotePlayer.Id);
        Assert.Equal(PlayerClass.Scout, remotePlayer.ClassId);
        Assert.Equal(2, remotePlayer.CurrentShells);
    }

    [Fact]
    public void SlotChangesRemapCachedStateAndSpectatingClearsTheLocalBody()
    {
        var source = JoinedWorld(PlayerClass.Heavy);
        var player = Assert.Single(new Protocol64StatePublisher(source).BuildPlayerStateBatch(1).Players)
            with { Slot = 2, PlayerId = 22, CurrentAmmo = 37 };
        var receiver = JoinedWorld(PlayerClass.Scout);
        var applier = new Protocol64StateApplier();
        Assert.True(applier.ApplyPlayerStateBatch(new Protocol64PlayerStateBatch(1, 1, [player])).Applied);
        applier.ApplyToWorld(receiver, 2);
        Assert.True(receiver.LocalPlayer.IsAlive);

        // No new player batch: changing assignment must still remap the view.
        applier.ApplyToWorld(receiver, SimulationWorld.FirstSpectatorSlot);
        Assert.False(receiver.LocalPlayer.IsAlive);
        Assert.True(receiver.LocalPlayerAwaitingJoin);
        Assert.Equal(22, Assert.Single(receiver.RemoteSnapshotPlayers).Id);

        applier.ApplyToWorld(receiver, 2);
        Assert.True(receiver.LocalPlayer.IsAlive);
        Assert.False(receiver.LocalPlayerAwaitingJoin);
        Assert.Equal(37, receiver.LocalPlayer.CurrentShells);
        Assert.Empty(receiver.RemoteSnapshotPlayers);
    }

    [Fact]
    public void ReusedRemoteSlotReplacesTheOldIdentity()
    {
        var source = JoinedWorld(PlayerClass.Scout);
        var player = Assert.Single(new Protocol64StatePublisher(source).BuildPlayerStateBatch(1).Players)
            with { Slot = 1, PlayerId = 11 };
        var receiver = JoinedWorld(PlayerClass.Heavy);
        var applier = new Protocol64StateApplier();
        applier.ApplyPlayerStateBatch(new Protocol64PlayerStateBatch(1, 1, [player]));
        applier.ApplyToWorld(receiver, 2);
        applier.ApplyPlayerStateBatch(new Protocol64PlayerStateBatch(2, 2,
            [player with { PlayerId = 99, Generation = 2 }]));
        applier.ApplyToWorld(receiver, 2);
        Assert.Equal(99, Assert.Single(receiver.RemoteSnapshotPlayers).Id);
    }

    [Fact]
    public void LateLegacyEquipmentCannotReplaceAnAcknowledgedLockerBaseline()
    {
        var source = JoinedWorld(PlayerClass.Scout);
        var receiver = JoinedWorld(PlayerClass.Scout);
        var originalItem = receiver.LocalPlayer.SelectedGameplayPrimaryItemId;
        Assert.True(source.LocalPlayer.TrySelectGameplayPrimaryItem("weapon.scout-nailgun"));
        var state = Assert.Single(new Protocol64StatePublisher(source).BuildPlayerStateBatch(1).Players)
            with { LastProcessedInputSequence = 12 };
        var applier = new Protocol64StateApplier();
        Assert.True(applier.ApplyPlayerStateBatch(new Protocol64PlayerStateBatch(1, 1, [state])).Applied);
        applier.ApplyToWorld(receiver, 1);

        // Equivalent equipment mutation to an older resolved snapshot arriving
        // after the fast state has already acknowledged the cycle input.
        Assert.True(receiver.LocalPlayer.TrySelectGameplayPrimaryItem(originalItem));
        Assert.True(applier.RestoreLocalPlayerBaseline(receiver, 1));

        Assert.Equal("weapon.scout-nailgun", receiver.LocalPlayer.SelectedGameplayPrimaryItemId);
        Assert.Equal(state.CurrentAmmo, receiver.LocalPlayer.CurrentShells);
        Assert.Equal(12u, Assert.Single(applier.Players).LastProcessedInputSequence);
    }

    private static SimulationWorld JoinedWorld(PlayerClass playerClass)
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        world.PrepareLocalPlayerJoin();
        world.CompleteLocalPlayerJoin(playerClass);
        world.LocalPlayer.SetSpawnRoomState(false);
        return world;
    }
}
