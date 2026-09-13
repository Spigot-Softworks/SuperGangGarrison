using OpenGarrison.Core;
using OpenGarrison.Protocol;
using OpenGarrison.Server;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class LastToDieEngineerAlternateWeaponReplicationTests
{
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
            world.SetLocalInput(default(PlayerInputSnapshot) with { SwapWeapon = true });
            world.AdvanceOneTick();
            world.SetLocalInput(default);
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
        Assert.True(source.TryMoveLocalPlayerToControlPointSpawn());
        Assert.True(source.TryPrepareNetworkPlayerJoin(2));
        Assert.True(source.TrySetNetworkPlayerTeam(2, PlayerTeam.Blue));
        Assert.True(source.TryApplyNetworkPlayerClassSelection(2, PlayerClass.Scout));
        Assert.True(source.TryGetNetworkPlayer(2, out var target));
        target.ForceSetHealth(999);
        target.TeleportTo(source.LocalPlayer.X + 96f, source.LocalPlayer.Y);
        source.AdvanceOneTick();
        source.SetLocalInput(default(PlayerInputSnapshot) with { ToggleSecondaryWeapon = true });
        source.AdvanceOneTick();
        source.SetLocalInput(default);
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
            source.SetLocalInput(input);
            source.AdvanceOneTick();
            Assert.True(source.LocalPlayer.IsExperimentalOffhandSelected);
            Assert.Equal(mode, source.LocalPlayer.ExperimentalEngineerAlternateWeaponMode);
            var packet = publisher.BuildPlayerStateBatch((uint)tick + 1).Players.First(p => p.Slot == 1);
            Assert.True(receiver.ApplyProtocol64PlayerState(packet));
            Assert.Equal(mode, receiver.LocalPlayer.ExperimentalEngineerAlternateWeaponMode);
            Assert.Equal(source.LocalPlayer.MedicHealTargetId, receiver.LocalPlayer.MedicHealTargetId);
            receiver.SetLocalInput(input);
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
        Assert.True(receiver.ApplyProtocol64PlayerState(state));

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
        world.PrepareLocalPlayerJoin();
        world.CompleteLocalPlayerJoin(PlayerClass.Engineer);
        world.LocalPlayer.SetSpawnRoomState(false);
        return world;
    }
}
