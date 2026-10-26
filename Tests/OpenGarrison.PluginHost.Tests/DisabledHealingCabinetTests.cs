using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class DisabledHealingCabinetTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DisabledCabinetDoesNotHealRefillOrStartCooldownsAndWorksAfterEnabling(bool script)
    {
        var world = CreateWorld(script);
        var player = world.LocalPlayer;
        player.ForceSetHealth(50);
        player.ForceSetAmmo(0);
        Assert.False(world.Level.IsRoomObjectActive(0));

        world.RoomEffects.ApplyHealingCabinets(player);

        Assert.Equal(50, player.Health);
        Assert.Equal(0, player.CurrentShells);
        Assert.False(player.IsUsingHealingCabinet);
        Assert.False(player.IsHealingCabinetResupplyOnCooldown());
        Assert.Equal(0, player.HealingCabinetSoundCooldownSecondsRemaining);
        Assert.Empty(world.PendingSoundEvents);

        world.Level.SetMapScriptObjectOverride(0, true);
        world.RoomEffects.ApplyHealingCabinets(player);

        Assert.Equal(player.MaxHealth, player.Health);
        Assert.Equal(player.MaxShells, player.CurrentShells);
        Assert.True(player.IsUsingHealingCabinet);
        Assert.True(player.IsHealingCabinetResupplyOnCooldown());
        Assert.Contains(world.PendingSoundEvents, sound => sound.SoundName == "CbntHealSnd");
    }

    [Fact]
    public void DisablingCabinetDuringResupplyCooldownClearsUsingCabinetState()
    {
        var world = CreateWorld(script: false);
        var player = world.LocalPlayer;
        world.Level.SetMapScriptObjectOverride(0, true);
        player.ForceSetHealth(50);
        world.RoomEffects.ApplyHealingCabinets(player);
        Assert.True(player.IsUsingHealingCabinet);
        Assert.True(player.IsHealingCabinetResupplyOnCooldown());
        world.DrainPendingSoundEvents();
        player.ForceSetHealth(50);
        player.ForceSetAmmo(0);
        world.Level.SetMapScriptObjectOverride(0, false);

        world.RoomEffects.ApplyHealingCabinets(player);

        Assert.False(player.IsUsingHealingCabinet);
        Assert.Equal(50, player.Health);
        Assert.Equal(0, player.CurrentShells);
        Assert.Empty(world.PendingSoundEvents);
    }

    [Fact]
    public void DisabledCabinetDoesNotSuppressAnEnabledCabinetAtTheSamePosition()
    {
        var world = CreateWorld(script: false, overlappingCabinet: true);
        world.LocalPlayer.ForceSetHealth(50);
        world.LocalPlayer.ForceSetAmmo(0);
        Assert.False(world.Level.IsRoomObjectActive(0));
        Assert.True(world.Level.IsRoomObjectActive(1));

        world.RoomEffects.ApplyHealingCabinets(world.LocalPlayer);

        Assert.Equal(world.LocalPlayer.MaxHealth, world.LocalPlayer.Health);
        Assert.Equal(world.LocalPlayer.MaxShells, world.LocalPlayer.CurrentShells);
        Assert.True(world.LocalPlayer.IsUsingHealingCabinet);
    }

    private static SimulationWorld CreateWorld(bool script, bool overlappingCabinet = false)
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        var cabinet = new RoomObjectMarker(RoomObjectType.HealingCabinet, 100, 100, 80, 80, "")
            { ObjectName = "cabinet", MapEntityId = "cabinet" };
        var spawn = new SpawnPoint(cabinet.CenterX, cabinet.CenterY);
        world.TestSetLevel(new SimpleLevel("cabinet_test", GameModeKind.CaptureTheFlag,
            new WorldBounds(700, 400), 1, null, 1, 1, spawn, [spawn], [new SpawnPoint(600, 250)], [],
            overlappingCabinet ? [cabinet, cabinet with { ObjectName = "other", MapEntityId = "other" }] : [cabinet],
            300, [], false,
            logicActivators: script ? null : new MapLogicActivatorSet([new(-1, 0, MapLogicActivatorBehavior.Disable, true)]),
            mapScriptSource: script ? "function on_start() object('cabinet'):disable() end" : ""));
        world.NetworkPlayers.PrepareLocalPlayerJoin();
        world.NetworkPlayers.CompleteLocalPlayerJoin(PlayerClass.Soldier);
        world.LocalPlayer.TeleportTo(cabinet.CenterX, cabinet.CenterY);
        world.DrainPendingSoundEvents();
        return world;
    }
}
