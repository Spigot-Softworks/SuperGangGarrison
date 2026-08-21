using System;
using System.Globalization;
using System.IO;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Client;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class InputBindingsSettingsTests
{
    [Fact]
    public void WeaponSwapDefaultsToSpaceOutsideExclusiveMultiplayerWeapons()
    {
        Assert.Equal(WeaponSwapBindingMode.Space, new InputBindingsSettings().SwapWeaponsBinding);
        Assert.True(KeyboardInputMapper.UsesMultiplayerExclusivePrimarySwapBinding(
            isNetworkMultiplayerSession: true,
            isLastToDieSession: false,
            isLockedPrimaryWeaponClass: true));
        Assert.False(KeyboardInputMapper.UsesMultiplayerExclusivePrimarySwapBinding(
            isNetworkMultiplayerSession: true,
            isLastToDieSession: true,
            isLockedPrimaryWeaponClass: true));
    }

    [Fact]
    public void ExclusiveMultiplayerQSwapDoesNotAlsoTriggerWeaponInteraction()
    {
        var bindings = new InputBindingsSettings
        {
            SwapWeaponsBinding = WeaponSwapBindingMode.Space,
            InteractWeapon = InputBinding.FromKey(Keys.Q),
        };

        var snapshot = KeyboardInputMapper.BuildGameplaySnapshot(
            bindings,
            new KeyboardState(Keys.Q),
            new MouseState(),
            cameraX: 0f,
            cameraY: 0f,
            localPlayerX: 0f,
            localPlayerY: 0f,
            useMultiplayerExclusivePrimarySwapBinding: true);

        Assert.True(snapshot.SwapWeapon);
        Assert.False(snapshot.InteractWeapon);
    }

    [Fact]
    public void LastToDieKeepsQAsWeaponInteractionWhenUsingNormalSwapBinding()
    {
        var bindings = new InputBindingsSettings
        {
            SwapWeaponsBinding = WeaponSwapBindingMode.Space,
            InteractWeapon = InputBinding.FromKey(Keys.Q),
        };

        var snapshot = KeyboardInputMapper.BuildGameplaySnapshot(
            bindings,
            new KeyboardState(Keys.Q),
            new MouseState(),
            cameraX: 0f,
            cameraY: 0f,
            localPlayerX: 0f,
            localPlayerY: 0f,
            useMultiplayerExclusivePrimarySwapBinding: false);

        Assert.False(snapshot.SwapWeapon);
        Assert.True(snapshot.InteractWeapon);
    }

    [Fact]
    public void ParseBindingSupportsLegacyIntegerKeyValues()
    {
        Assert.True(InputBindingsSettings.TryParseBinding(((int)Keys.Tab).ToString(CultureInfo.InvariantCulture), out var binding));

        Assert.Equal(InputBinding.FromKey(Keys.Tab), binding);
    }

    [Theory]
    [InlineData("Mouse3", InputMouseButton.Middle)]
    [InlineData("Mouse4", InputMouseButton.XButton1)]
    [InlineData("Mouse5", InputMouseButton.XButton2)]
    [InlineData("Mouse:XButton1", InputMouseButton.XButton1)]
    public void ParseBindingSupportsMouseButtonAliases(string text, InputMouseButton expectedButton)
    {
        Assert.True(InputBindingsSettings.TryParseBinding(text, out var binding));

        Assert.Equal(InputBinding.FromMouse(expectedButton), binding);
    }

    [Fact]
    public void SaveAndLoadRoundTripsMouseBindings()
    {
        var path = Path.Combine(Path.GetTempPath(), "opengarrison-controls-tests", Guid.NewGuid().ToString("N"), InputBindingsSettings.DefaultFileName);
        var settings = new InputBindingsSettings
        {
            ShowScoreboard = InputBinding.FromMouse(InputMouseButton.XButton1),
            ToggleConsole = InputBinding.FromMouse(InputMouseButton.Middle),
        };

        try
        {
            settings.Save(path);

            var loaded = InputBindingsSettings.Load(path);

            Assert.Equal(InputBinding.FromMouse(InputMouseButton.XButton1), loaded.ShowScoreboard);
            Assert.Equal(InputBinding.FromMouse(InputMouseButton.Middle), loaded.ToggleConsole);
        }
        finally
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
