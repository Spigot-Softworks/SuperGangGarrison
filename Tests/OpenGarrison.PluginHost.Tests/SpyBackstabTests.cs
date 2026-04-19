using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class SpyBackstabTests
{
    [Fact]
    public void StartingBackstabPreservesCurrentHorizontalSpeed()
    {
        var player = new PlayerEntity(id: 1, CharacterClassCatalog.Spy);
        player.ForceSetHealth(player.MaxHealth);

        Assert.True(player.TryToggleSpyCloak());

        player.AddImpulse(120f, 0f);
        Assert.True(player.HorizontalSpeed > 0f);
        var horizontalSpeedBeforeBackstab = player.HorizontalSpeed;

        Assert.True(player.TryStartSpyBackstab(0f));

        Assert.Equal(horizontalSpeedBeforeBackstab, player.HorizontalSpeed);
    }

    [Fact]
    public void BackstabAnimationSoftlySlowsHorizontalMovementWithoutInput()
    {
        var world = new SimulationWorld();
        Assert.True(world.TrySetLocalClass(PlayerClass.Spy));
        world.ForceRespawnLocalPlayer();

        var player = world.LocalPlayer;
        Assert.True(player.TryToggleSpyCloak());

        player.AddImpulse(120f, 0f);
        var horizontalSpeedBeforeBackstab = player.HorizontalSpeed;
        Assert.True(horizontalSpeedBeforeBackstab > 0f);
        Assert.True(player.TryStartSpyBackstab(0f));

        var neutralInput = new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: false,
            AimWorldX: player.X + 256f,
            AimWorldY: player.Y,
            DebugKill: false);

        var startedGrounded = player.PrepareMovement(
            neutralInput,
            world.Level,
            world.LocalPlayerTeam,
            world.Config.FixedDeltaSeconds,
            out _);
        player.CompleteMovement(
            world.Level,
            world.LocalPlayerTeam,
            world.Config.FixedDeltaSeconds,
            startedGrounded,
            jumped: false,
            allowDropdownFallThrough: false);

        Assert.InRange(player.HorizontalSpeed, 0.01f, horizontalSpeedBeforeBackstab - 0.01f);
    }
}