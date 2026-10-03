using OpenGarrison.Core;
using OpenGarrison.Protocol;
using OpenGarrison.Server;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class LastToDieEngineerAlternateWeaponReplicationTests
{
    [Fact]
    public void ConstructorResourceUpdatesPreservePerkCapacityAndRecoverAcrossRepeatedRefills()
    {
        var source = JoinedEngineerWorld();
        var receiver = JoinedEngineerWorld();
        source.LocalPlayer.ConfigureExperimentalMetal(200, 0.25f);
        var publisher = new Protocol64StatePublisher(source);
        for (uint tick = 1; tick <= 3; tick++)
        {
            Assert.True(source.LocalPlayer.SpendMetal(100));
            Assert.True(receiver.SnapshotApply.ApplyProtocol64PlayerState(Assert.Single(publisher.BuildPlayerStateBatch(tick).Players)));
            Assert.Equal(source.LocalPlayer.Metal, receiver.LocalPlayer.Metal);
            Assert.Equal(200f, receiver.LocalPlayer.MaxMetal);
            Assert.Equal(0.25f, receiver.LocalPlayer.PassiveMetalRegenerationPerTick);
            source.LocalPlayer.AddMetal(100);
            source.LocalPlayer.SetSpawnRoomState(tick == 1);
            Assert.True(receiver.SnapshotApply.ApplyProtocol64PlayerState(Assert.Single(publisher.BuildPlayerStateBatch(tick + 3).Players)));
            Assert.Equal(200f, receiver.LocalPlayer.Metal);
            Assert.Equal(tick == 1, receiver.LocalPlayer.IsInSpawnRoom);
        }
        var invalid = Assert.Single(publisher.BuildPlayerStateBatch(7).Players) with
        { EngineerBuild = new(float.NaN, 200, 0.25f, false) };
        Assert.False(receiver.SnapshotApply.ApplyProtocol64PlayerState(invalid));
        Assert.Equal(200f, receiver.LocalPlayer.Metal);
    }

    [Fact]
    public void AuthoritativeBuildResourcesRecoverAfterSpendingMetalAndStartingAnotherSession()
    {
        var source = JoinedEngineerWorld();
        var receiver = JoinedEngineerWorld();
        Assert.True(receiver.LocalPlayer.SpendMetal(100));
        receiver.LocalPlayer.SetSpawnRoomState(true);
        var state = Assert.Single(new Protocol64StatePublisher(source).BuildPlayerStateBatch(1).Players);
        Assert.True(receiver.SnapshotApply.ApplyProtocol64PlayerState(state));
        Assert.Equal(source.LocalPlayer.Metal, receiver.LocalPlayer.Metal);
        Assert.False(receiver.LocalPlayer.IsInSpawnRoom);
    }

    [Fact]
    public void QCyclesBothEngineerBeamsAndReturnsToShotgunWithoutBeingStowedOnTheNextTick()
    {
        var world = JoinedEngineerWorld();
        world.ConfigureExperimentalGameplaySettings(new ExperimentalGameplaySettings(
            EnableEngineerEssenceExtractor: true, EnableEngineerFreezeRay: true));
        foreach (var mode in new[] { ExperimentalEngineerAlternateWeaponMode.EssenceExtractor,
            ExperimentalEngineerAlternateWeaponMode.FreezeRay, ExperimentalEngineerAlternateWeaponMode.None,
            ExperimentalEngineerAlternateWeaponMode.EssenceExtractor })
        {
            world.NetworkPlayerRules.SetLocalInput(default(PlayerInputSnapshot) with { SwapWeapon = true });
            world.AdvanceOneTick();
            world.NetworkPlayerRules.SetLocalInput(default);
            for (var tick = 0; tick < 10; tick++)
            {
                world.AdvanceOneTick();
                Assert.Equal(mode, world.LocalPlayer.ExperimentalEngineerAlternateWeaponMode);
                Assert.Equal(mode != ExperimentalEngineerAlternateWeaponMode.None, world.LocalPlayer.IsExperimentalOffhandSelected);
            }
        }
    }

    [Theory]
    [InlineData(ExperimentalEngineerAlternateWeaponMode.EssenceExtractor)]
    [InlineData(ExperimentalEngineerAlternateWeaponMode.FreezeRay)]
    public void EngineerBeamRemainsUsableThroughRepeatedAuthorityAndPredictionTicks(ExperimentalEngineerAlternateWeaponMode mode)
    {
        var source = JoinedEngineerWorld();
        Assert.True(source.TryLoadLevel("Harvest"));
        var settings = new ExperimentalGameplaySettings(
            EnableEngineerEssenceExtractor: mode == ExperimentalEngineerAlternateWeaponMode.EssenceExtractor,
            EnableEngineerFreezeRay: mode == ExperimentalEngineerAlternateWeaponMode.FreezeRay);
        source.ConfigureExperimentalGameplaySettings(settings);
        Assert.True(source.Spawns.TryMoveLocalPlayerToControlPointSpawn());
        Assert.True(source.NetworkPlayerRules.TryPrepareNetworkPlayerJoin(2));
        Assert.True(source.NetworkPlayerRules.TrySetNetworkPlayerTeam(2, PlayerTeam.Blue));
        Assert.True(source.NetworkPlayerRules.TryApplyNetworkPlayerClassSelection(2, PlayerClass.Scout));
        Assert.True(source.NetworkPlayerRules.TryGetNetworkPlayer(2, out var target));
        target.ForceSetHealth(999);
        target.TeleportTo(source.LocalPlayer.X + 96f, source.LocalPlayer.Y);
        source.AdvanceOneTick();
        source.NetworkPlayerRules.SetLocalInput(default(PlayerInputSnapshot) with { ToggleSecondaryWeapon = true });
        source.AdvanceOneTick();
        source.NetworkPlayerRules.SetLocalInput(default);
        source.AdvanceOneTick();
        Assert.True(source.LocalPlayer.IsExperimentalOffhandSelected);
        Assert.Equal(mode, source.LocalPlayer.ExperimentalEngineerAlternateWeaponMode);

        var receiver = JoinedEngineerWorld();
        receiver.ConfigureExperimentalGameplaySettings(settings);
        var publisher = new Protocol64StatePublisher(source);
        var initialHealth = target.Health;
        for (var tick = 0; tick < source.Config.TicksPerSecond * 3; tick++)
        {
            var input = default(PlayerInputSnapshot) with { FirePrimary = true, AimWorldX = target.X, AimWorldY = target.Y };
            source.NetworkPlayerRules.SetLocalInput(input);
            source.AdvanceOneTick();
            Assert.True(source.LocalPlayer.IsExperimentalOffhandSelected);
            Assert.Equal(mode, source.LocalPlayer.ExperimentalEngineerAlternateWeaponMode);
            var packet = publisher.BuildPlayerStateBatch((uint)tick + 1).Players.First(p => p.Slot == 1);
            Assert.True(receiver.SnapshotApply.ApplyProtocol64PlayerState(packet));
            Assert.Equal(mode, receiver.LocalPlayer.ExperimentalEngineerAlternateWeaponMode);
            Assert.Equal(source.LocalPlayer.MedicHealTargetId, receiver.LocalPlayer.MedicHealTargetId);
            receiver.NetworkPlayerRules.SetLocalInput(input);
            receiver.AdvanceOneTick();
            Assert.True(receiver.LocalPlayer.IsExperimentalOffhandSelected);
            Assert.Equal(mode, receiver.LocalPlayer.ExperimentalEngineerAlternateWeaponMode);
        }
        if (mode == ExperimentalEngineerAlternateWeaponMode.EssenceExtractor)
            Assert.True(target.Health < initialHealth);
        else
            Assert.True(target.IsExperimentalCryoFrozen);
    }

    [Theory]
    [InlineData(ExperimentalEngineerAlternateWeaponMode.EssenceExtractor)]
    [InlineData(ExperimentalEngineerAlternateWeaponMode.FreezeRay)]
    public void Protocol64PreservesEngineerBeamIdentityWithGenericMedigunSecondary(
        ExperimentalEngineerAlternateWeaponMode mode)
    {
        var source = JoinedEngineerWorld();
        source.LocalPlayer.SetExperimentalOffhandWeapon(CharacterClassCatalog.Medigun);
        source.LocalPlayer.SetExperimentalEngineerAlternateWeaponMode(mode);
        source.LocalPlayer.EquipExperimentalOffhandWeapon();

        var state = Assert.Single(new Protocol64StatePublisher(source).BuildPlayerStateBatch(1).Players);
        Assert.Equal("weapon.medigun", state.Equipment!.SecondaryItemId);
        Assert.Equal((byte)mode, state.Equipment.EngineerAlternateWeaponMode);

        var receiver = JoinedEngineerWorld();
        Assert.True(receiver.SnapshotApply.ApplyProtocol64PlayerState(state));

        Assert.True(receiver.LocalPlayer.IsExperimentalOffhandSelected);
        Assert.Equal(mode, receiver.LocalPlayer.ExperimentalEngineerAlternateWeaponMode);
        Assert.Equal(
            mode == ExperimentalEngineerAlternateWeaponMode.EssenceExtractor,
            receiver.LocalPlayer.IsExperimentalEngineerEssenceExtractorPresented);
        Assert.Equal(
            mode == ExperimentalEngineerAlternateWeaponMode.FreezeRay,
            receiver.LocalPlayer.IsExperimentalEngineerFreezeRayPresented);
    }

    private static SimulationWorld JoinedEngineerWorld()
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        world.NetworkPlayerRules.PrepareLocalPlayerJoin();
        world.NetworkPlayerRules.CompleteLocalPlayerJoin(PlayerClass.Engineer);
        world.LocalPlayer.SetSpawnRoomState(false);
        return world;
    }
}
