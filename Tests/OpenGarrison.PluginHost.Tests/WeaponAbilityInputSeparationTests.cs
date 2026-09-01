using Microsoft.Xna.Framework.Input;
using OpenGarrison.Client;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class WeaponAbilityInputSeparationTests
{
    [Theory]
    [InlineData(PlayerClass.Scout)]
    [InlineData(PlayerClass.Pyro)]
    [InlineData(PlayerClass.Soldier)]
    [InlineData(PlayerClass.Heavy)]
    [InlineData(PlayerClass.Engineer)]
    [InlineData(PlayerClass.Medic)]
    [InlineData(PlayerClass.Sniper)]
    public void DefaultSpaceInputNeverSwapsTheEquippedWeapon(PlayerClass playerClass)
    {
        var world = CreateWorld(playerClass);
        var player = world.LocalPlayer;
        var equippedSlot = player.GameplayLoadoutState.EquippedSlot;
        var equippedItemId = player.GameplayLoadoutState.EquippedItemId;
        var input = BuildKeyboardInput(Keys.Space);

        Assert.True(input.UseAbility);
        Assert.False(input.SwapWeapon);

        world.SetLocalInput(input);
        world.AdvanceOneTick();

        Assert.Equal(equippedSlot, player.GameplayLoadoutState.EquippedSlot);
        Assert.Equal(equippedItemId, player.GameplayLoadoutState.EquippedItemId);
    }

    [Theory]
    [InlineData(PlayerClass.Scout)]
    [InlineData(PlayerClass.Pyro)]
    [InlineData(PlayerClass.Soldier)]
    [InlineData(PlayerClass.Heavy)]
    [InlineData(PlayerClass.Engineer)]
    [InlineData(PlayerClass.Medic)]
    [InlineData(PlayerClass.Sniper)]
    public void DefaultQInputSwapsWithoutInvokingTheUtilityAbility(PlayerClass playerClass)
    {
        var world = CreateWorld(playerClass);
        var player = world.LocalPlayer;
        Assert.True(player.HasExperimentalOffhandWeapon);
        var input = BuildKeyboardInput(Keys.Q);

        Assert.True(input.SwapWeapon);
        Assert.False(input.UseAbility);

        world.SetLocalInput(input);
        world.AdvanceOneTick();

        Assert.True(player.IsExperimentalOffhandSelected);
        Assert.Equal(GameplayEquipmentSlot.Secondary, player.GameplayLoadoutState.EquippedSlot);
    }

    private static SimulationWorld CreateWorld(PlayerClass playerClass)
    {
        var world = new SimulationWorld();
        world.ConfigureExperimentalGameplaySettings(new ExperimentalGameplaySettings(
            EnableSecondaryAbilities: true));
        world.PrepareLocalPlayerJoin();
        world.CompleteLocalPlayerJoin(playerClass);
        return world;
    }

    private static PlayerInputSnapshot BuildKeyboardInput(Keys key)
    {
        return KeyboardInputMapper.BuildGameplaySnapshot(
            new InputBindingsSettings(),
            new KeyboardState(key),
            new MouseState(),
            cameraX: 0f,
            cameraY: 0f,
            localPlayerX: 0f,
            localPlayerY: 0f);
    }
}
