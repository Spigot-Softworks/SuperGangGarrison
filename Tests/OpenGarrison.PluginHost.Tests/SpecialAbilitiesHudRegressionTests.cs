using System.Reflection;
using System.Runtime.CompilerServices;
using OpenGarrison.Client;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class SpecialAbilitiesHudRegressionTests
{
    [Theory]
    [InlineData(PlayerClass.Scout)]
    [InlineData(PlayerClass.Soldier)]
    [InlineData(PlayerClass.Pyro)]
    [InlineData(PlayerClass.Heavy)]
    [InlineData(PlayerClass.Engineer)]
    [InlineData(PlayerClass.Medic)]
    [InlineData(PlayerClass.Sniper)]
    [InlineData(PlayerClass.Spy)]
    [InlineData(PlayerClass.Quote)]
    public void HudFiltersDisabledAbilitiesUsingTheSameRulesAsTheServer(PlayerClass playerClass)
    {
        const BindingFlags instance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        world.PrepareLocalPlayerJoin();
        world.CompleteLocalPlayerJoin(playerClass);
        var game = (Game1)RuntimeHelpers.GetUninitializedObject(typeof(Game1));
        typeof(Game1).GetField("_world", instance)!.SetValue(game, world);
        var type = typeof(Game1).GetNestedType("GameplayLocalStatusHudController", BindingFlags.NonPublic)!;
        var controller = Activator.CreateInstance(type, instance, null, [game], null)!;
        GameplayItemDefinition[] Items() => ((IEnumerable<GameplayItemDefinition>)type.GetMethod(
            "GetLocalConfiguredAbilityHudItems", instance)!.Invoke(controller, null)!).ToArray();
        var enabled = Items();
        world.ConfigureExperimentalGameplaySettings(new(EnableSecondaryAbilities: false));
        var disabled = Items();
        Assert.Equal(enabled.Where(item => item.Ability is null
            || !world.IsGameplayAbilityBlockedBySpecialAbilitiesSetting(item.Ability)).Select(item => item.Id),
            disabled.Select(item => item.Id));
        Assert.DoesNotContain(disabled, item => item.Ability is { } ability
            && world.IsGameplayAbilityBlockedBySpecialAbilitiesSetting(ability));
        world.ConfigureExperimentalGameplaySettings(new(EnableSecondaryAbilities: true));
        Assert.Equal(enabled.Select(item => item.Id), Items().Select(item => item.Id));
    }
}
