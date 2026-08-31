using OpenGarrison.Core;
using OpenGarrison.GameplayModding;
using OpenGarrison.Server;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class ServerReadOnlyStateViewTests
{
    [Fact]
    public void PlayerGameplayAbilitiesIncludeOnlyActiveWeaponGrantedAltFire()
    {
        var world = new SimulationWorld();
        world.PrepareLocalPlayerJoin();
        world.CompleteLocalPlayerJoin(PlayerClass.Pyro);
        var clients = new Dictionary<byte, ClientSession>();
        var view = new ServerReadOnlyStateView(
            () => "test",
            () => world,
            () => clients);

        Assert.Contains(
            view.GetPlayerGameplayAbilities(world.LocalPlayer.Id),
            ability => ability.ItemId == "ability.pyro-airblast"
                && ability.Category == GameplayAbilityConstants.WeaponAltFireCategory);

        Assert.True(world.TrySetNetworkPlayerGameplaySecondaryItem(
            SimulationWorld.LocalPlayerSlot,
            "weapon.rocketlauncher"));
        Assert.True(world.TrySetNetworkPlayerGameplayEquippedSlot(
            SimulationWorld.LocalPlayerSlot,
            GameplayEquipmentSlot.Secondary));

        Assert.DoesNotContain(
            view.GetPlayerGameplayAbilities(world.LocalPlayer.Id),
            ability => ability.ItemId == "ability.pyro-airblast");
        Assert.Contains(
            view.GetPlayerGameplayAbilities(world.LocalPlayer.Id),
            ability => ability.ItemId == "ability.pyro-utility");
    }
}
