using Microsoft.Xna.Framework.Input;
using OpenGarrison.Client;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class WeaponAbilityInputSeparationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CustomSwapAndQUtilityStaySeparateWithEitherAbilitySetting(bool enabled)
    {
        var world = CreateWorld(PlayerClass.Scout);
        world.ConfigureExperimentalGameplaySettings(new(EnableSecondaryAbilities: enabled));
        var bindings = new InputBindingsSettings
        {
            UseAbility = InputBinding.FromKey(Keys.Q),
            SwapWeaponsBinding = WeaponSwapBindingMode.Custom,
            SwapWeaponsCustomKey = InputBinding.FromKey(Keys.RightControl),
        };
        var equipped = world.LocalPlayer.GameplayLoadoutState.EquippedItemId;
        var utility = KeyboardInputMapper.BuildGameplaySnapshot(bindings,
            new KeyboardState(Keys.Q), new MouseState(), 0, 0, 0, 0);
        world.NetworkPlayers.SetLocalInput(utility);
        world.AdvanceOneTick();
        Assert.Equal(equipped, world.LocalPlayer.GameplayLoadoutState.EquippedItemId);
        world.NetworkPlayers.SetLocalInput(default);
        for (var tick = 0; tick < 30; tick++) world.AdvanceOneTick();
        var swap = KeyboardInputMapper.BuildGameplaySnapshot(bindings,
            new KeyboardState(Keys.RightControl), new MouseState(), 0, 0, 0, 0);
        Assert.True(swap.SwapWeapon);
        Assert.False(swap.UseAbility);
        world.NetworkPlayers.SetLocalInput(swap);
        world.AdvanceOneTick();
        Assert.True(world.LocalPlayer.IsExperimentalOffhandSelected);
    }
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

        world.NetworkPlayers.SetLocalInput(input);
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

        world.NetworkPlayers.SetLocalInput(input);
        world.AdvanceOneTick();

        Assert.True(player.IsExperimentalOffhandSelected);
        Assert.Equal(GameplayEquipmentSlot.Secondary, player.GameplayLoadoutState.EquippedSlot);
    }

    private static SimulationWorld CreateWorld(PlayerClass playerClass)
    {
        var world = new SimulationWorld();
        world.ConfigureExperimentalGameplaySettings(new ExperimentalGameplaySettings(
            EnableSecondaryAbilities: true));
        world.NetworkPlayers.PrepareLocalPlayerJoin();
        world.NetworkPlayers.CompleteLocalPlayerJoin(playerClass);
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
