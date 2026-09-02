using System;
using System.Globalization;
using System.IO;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Client;
using OpenGarrison.Core.LastToDie;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class InputBindingsSettingsTests
{
    [Fact]
    public void WeaponSwapDefaultsToQAndUtilityAbilityDefaultsToSpace()
    {
        var bindings = new InputBindingsSettings();
        Assert.Equal(WeaponSwapBindingMode.Q, bindings.SwapWeaponsBinding);
        Assert.Equal(InputBinding.FromKey(Keys.Space), bindings.UseAbility);
        Assert.Equal(InputBinding.FromKey(Keys.G), bindings.InteractWeapon);
        Assert.True(bindings.ScrollWheelWeaponSwapEnabled);
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
    public void DefaultQAndSpaceInputsAreMutuallyExclusive()
    {
        var bindings = new InputBindingsSettings();

        var qInput = KeyboardInputMapper.BuildGameplaySnapshot(
            bindings,
            new KeyboardState(Keys.Q),
            new MouseState(),
            cameraX: 0f,
            cameraY: 0f,
            localPlayerX: 0f,
            localPlayerY: 0f);
        var spaceInput = KeyboardInputMapper.BuildGameplaySnapshot(
            bindings,
            new KeyboardState(Keys.Space),
            new MouseState(),
            cameraX: 0f,
            cameraY: 0f,
            localPlayerX: 0f,
            localPlayerY: 0f);

        Assert.True(qInput.SwapWeapon);
        Assert.False(qInput.UseAbility);
        Assert.True(spaceInput.UseAbility);
        Assert.False(spaceInput.SwapWeapon);
    }

    [Theory]
    [InlineData("G", "Press G to equip the Freeze Ray.")]
    [InlineData("Mouse 4", "Press Mouse 4 to equip the Freeze Ray.")]
    public void LastToDiePerkDescriptionsResolveConfiguredInteractionBinding(
        string bindingLabel,
        string expected)
    {
        var description = $"Press {LastToDieExpansionPerkCatalog.InteractWeaponBindingToken} to equip the Freeze Ray.";

        Assert.Equal(
            expected,
            Game1.ResolveLastToDiePerkDescriptionBindingLabels(description, bindingLabel));
    }

    [Fact]
    public void LoadingOldSpaceCollisionMigratesSwapToQ()
    {
        var path = Path.Combine(Path.GetTempPath(), "opengarrison-controls-tests", Guid.NewGuid().ToString("N"), InputBindingsSettings.DefaultFileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(
            path,
            "[Controls]" + Environment.NewLine
            + "secondaryWeapon=Keyboard:Space" + Environment.NewLine
            + "swapWeapons=Space" + Environment.NewLine
            + "interactWeapon=Keyboard:Q" + Environment.NewLine);

        try
        {
            var loaded = InputBindingsSettings.Load(path);

            Assert.Equal(WeaponSwapBindingMode.Q, loaded.SwapWeaponsBinding);
            Assert.Equal(InputBinding.FromKey(Keys.Space), loaded.UseAbility);
            Assert.Equal(InputBinding.FromKey(Keys.G), loaded.InteractWeapon);
            Assert.True(loaded.ScrollWheelWeaponSwapEnabled);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
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
            ScrollWheelWeaponSwapEnabled = false,
        };

        try
        {
            settings.Save(path);

            var loaded = InputBindingsSettings.Load(path);

            Assert.Equal(InputBinding.FromMouse(InputMouseButton.XButton1), loaded.ShowScoreboard);
            Assert.Equal(InputBinding.FromMouse(InputMouseButton.Middle), loaded.ToggleConsole);
            Assert.False(loaded.ScrollWheelWeaponSwapEnabled);
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

    [Theory]
    [InlineData(120)]
    [InlineData(-120)]
    public void ScrollWheelMovementRequestsDedicatedSecondaryWeaponToggle(int wheelValue)
    {
        var bindings = new InputBindingsSettings();
        var previousMouse = CreateMouseState(scrollWheelValue: 0);
        var currentMouse = CreateMouseState(scrollWheelValue: wheelValue);

        var snapshot = KeyboardInputMapper.BuildGameplaySnapshot(
            bindings,
            new KeyboardState(),
            currentMouse,
            cameraX: 0f,
            cameraY: 0f,
            localPlayerX: 0f,
            localPlayerY: 0f,
            previousMouse: previousMouse);

        Assert.True(snapshot.ToggleSecondaryWeapon);
        Assert.False(snapshot.SwapWeapon);
    }

    [Fact]
    public void ScrollWheelWeaponSwapCanBeDisabledAndRequiresWheelMovement()
    {
        var disabled = new InputBindingsSettings { ScrollWheelWeaponSwapEnabled = false };
        var enabled = new InputBindingsSettings();
        var previousMouse = CreateMouseState(scrollWheelValue: 120);
        var currentMouse = CreateMouseState(scrollWheelValue: 120);
        var movedMouse = CreateMouseState(scrollWheelValue: 240);

        Assert.False(KeyboardInputMapper.BuildGameplaySnapshot(
            disabled,
            new KeyboardState(),
            movedMouse,
            0f,
            0f,
            0f,
            0f,
            previousMouse: previousMouse).ToggleSecondaryWeapon);
        Assert.False(KeyboardInputMapper.BuildGameplaySnapshot(
            enabled,
            new KeyboardState(),
            currentMouse,
            0f,
            0f,
            0f,
            0f,
            previousMouse: previousMouse).ToggleSecondaryWeapon);
    }

    private static MouseState CreateMouseState(int scrollWheelValue)
        => new(
            0,
            0,
            scrollWheelValue,
            ButtonState.Released,
            ButtonState.Released,
            ButtonState.Released,
            ButtonState.Released,
            ButtonState.Released);
}
