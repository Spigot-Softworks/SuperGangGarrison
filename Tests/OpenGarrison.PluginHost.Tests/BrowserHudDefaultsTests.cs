using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Client;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class BrowserHudDefaultsTests
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    [Theory]
    [InlineData(800, 600)]
    [InlineData(960, 540)]
    [InlineData(1280, 720)]
    public void BuffIconSitsBesideHealthInsteadOfApplyingItsOffsetTwice(int width, int height)
    {
        var profile = new HudLayoutProfile();
        Assert.True(profile.TryResolve(HudElementId.LocalHealth, width, height, out var health));
        Assert.True(profile.TryResolve(HudElementId.LastToDieBuffIcon, width, height, out var buff));
        Assert.Equal(new Rectangle(96, height - 83, 35, 35), buff.Bounds);
        Assert.InRange(buff.Bounds.Left - health.Bounds.Right, -5, 12);
        Assert.InRange(buff.Bounds.Top - health.Bounds.Top, -12, 12);
    }

    [Fact]
    public void ExistingCustomizedBuffPositionIsPreserved()
    {
        var profile = new HudLayoutDocument
        {
            Elements = { [HudElementId.LastToDieBuffIcon] = new() { OffsetX = 40, OffsetY = -50 } }
        }.ToProfile();
        Assert.True(profile.TryResolve(HudElementId.LastToDieBuffIcon, 1280, 720, out var buff));
        Assert.Equal(new Rectangle(136, 587, 35, 35), buff.Bounds);
    }

    [Fact]
    public void BrowserUnbindsOldCustomBubbleDefaultButPreservesOtherControls()
    {
        var desktop = new InputBindingsSettings();
        Assert.True(desktop.CustomBubble.IsKeyboardKey(Keys.R));
        var browser = InputBindingsSettings.ApplyBrowserDefaults(desktop);
        Assert.True(browser.CustomBubble.IsKeyboardKey(Keys.None));
        Assert.True(browser.UseAbility.IsKeyboardKey(Keys.Space));
        Assert.True(browser.ShowScoreboard.IsKeyboardKey(Keys.Tab));
        var custom = new InputBindingsSettings { CustomBubble = InputBinding.FromKey(Keys.T) };
        Assert.True(InputBindingsSettings.ApplyBrowserDefaults(custom).CustomBubble.IsKeyboardKey(Keys.T));
    }

    [Theory]
    [InlineData(PlayerClass.Soldier)]
    [InlineData(PlayerClass.Heavy)]
    [InlineData(PlayerClass.Spy)]
    [InlineData(PlayerClass.Demoman)]
    public void ActualAbilityWidgetsClearBothWeaponPanelsAndRefreshAfterEquipmentChanges(PlayerClass playerClass)
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        world.PrepareLocalPlayerJoin();
        world.CompleteLocalPlayerJoin(playerClass);
        var game = (Game1)RuntimeHelpers.GetUninitializedObject(typeof(Game1));
        typeof(Game1).GetField("_world", Instance)!.SetValue(game, world);
        typeof(Game1).GetField("_menuBitmapFontLineHeight", Instance)!.SetValue(game, 11);
        foreach (var name in new[] { "_networkClient", "_clientSettings", "_gameplaySessionState", "_uiShellState" })
        {
            var field = typeof(Game1).GetField(name, Instance)!;
            field.SetValue(game, Activator.CreateInstance(field.FieldType, true));
        }
        var controllerType = typeof(Game1).GetNestedType("GameplayLocalStatusHudController", BindingFlags.NonPublic)!;
        var controller = Activator.CreateInstance(controllerType, Instance, null, [game], null)!;
        object Invoke(string name, params object[] args) => controllerType.GetMethod(name, Instance | BindingFlags.Static)!.Invoke(controller, args)!;
        var weapons = Invoke("GetWeaponHudWidgets");
        var abilities = Invoke("GetAbilityHudWidgets");
        Assert.Same(weapons, Invoke("GetWeaponHudWidgets"));
        Assert.Same(abilities, Invoke("GetAbilityHudWidgets"));
        var weaponBounds = ((IEnumerable)weapons).Cast<object>().Select(w => Resolve(Invoke("CreateWeaponHudElementLayout", w))).ToArray();
        var abilityBounds = ((IEnumerable)abilities).Cast<object>().Select(w => Resolve(Invoke("CreateAbilityHudElementLayout", w))).ToArray();
        Assert.NotEmpty(weaponBounds);
        Assert.NotEmpty(abilityBounds);
        foreach (var ability in abilityBounds)
            foreach (var weapon in weaponBounds)
                Assert.True(ability.Bottom <= weapon.Top - 9, $"{playerClass}: {ability} overlaps {weapon}");
        world.CompleteLocalPlayerJoin(PlayerClass.Soldier);
        Invoke("BeginHudFrame");
        Assert.NotSame(weapons, Invoke("GetWeaponHudWidgets"));
        Assert.NotSame(abilities, Invoke("GetAbilityHudWidgets"));
    }

    [Fact]
    public void SelectingDemomanGrenadeLauncherReplacesItsStowedHudRowInsteadOfDuplicatingIt()
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        world.PrepareLocalPlayerJoin();
        world.CompleteLocalPlayerJoin(PlayerClass.Demoman);
        var game = (Game1)RuntimeHelpers.GetUninitializedObject(typeof(Game1));
        typeof(Game1).GetField("_world", Instance)!.SetValue(game, world);
        typeof(Game1).GetField("_menuBitmapFontLineHeight", Instance)!.SetValue(game, 11);
        foreach (var name in new[] { "_networkClient", "_clientSettings", "_gameplaySessionState", "_uiShellState" })
        {
            var field = typeof(Game1).GetField(name, Instance)!;
            field.SetValue(game, Activator.CreateInstance(field.FieldType, true));
        }

        var controllerType = typeof(Game1).GetNestedType("GameplayLocalStatusHudController", BindingFlags.NonPublic)!;
        var controller = Activator.CreateInstance(controllerType, Instance, null, [game], null)!;
        string[] BuildRowIds()
        {
            var rows = (IEnumerable)controllerType.GetMethod("BuildWeaponHudRows", Instance)!.Invoke(controller, null)!;
            return rows.Cast<object>()
                .Select(row => (string)row.GetType().GetProperty("Id", Instance)!.GetValue(row)!)
                .ToArray();
        }

        var stowedIds = BuildRowIds();
        Assert.Contains(HudElementId.LocalWeaponUtility, stowedIds);
        Assert.Contains(HudElementId.LocalWeaponPrimary, stowedIds);

        Assert.True(world.TrySetNetworkPlayerGameplayEquippedSlot(
            SimulationWorld.LocalPlayerSlot,
            GameplayEquipmentSlot.Secondary));
        Assert.True(world.LocalPlayer.IsExperimentalOffhandSelected);

        var selectedIds = BuildRowIds();
        Assert.True(selectedIds.Length == 2, $"Unexpected selected HUD rows: {string.Join(", ", selectedIds)}");
        Assert.Single(selectedIds, id => id == HudElementId.LocalWeaponUtility);
        Assert.Single(selectedIds, id => id == HudElementId.LocalWeaponPrimary);
    }

    private static Rectangle Resolve(object value)
    {
        var layout = (HudElementLayout)value;
        return layout.ResolveBounds(HudLayoutResolver.ResolveOrigin(layout.Anchor, layout.Offset, 1280, 720));
    }
}
