using System.Reflection;
using System.Runtime.CompilerServices;
using OpenGarrison.Client;
using OpenGarrison.ClientShared;
using OpenGarrison.Core;
using Microsoft.Xna.Framework;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class BuildWheelTests
{



    [Theory]
    [InlineData(0, 1)]
    [InlineData(59, 1)]
    [InlineData(60, 3)]
    [InlineData(120, 3)]
    [InlineData(180, 2)]
    [InlineData(240, 2)]
    [InlineData(300, 1)]
    [InlineData(-90, 2)]
    [InlineData(360, 1)]
    public void ThreeSectorArtworkMatchesWheelSelection(float angle, int expected)
        => Assert.Equal(expected, BuildWheelPresentation.GetSlotFromPointer(angle, 60));

    [Fact]
    public void CenterCancelsAndBubbleWheelKeepsItsNineOriginalSectors()
    {
        Assert.Equal(0, BuildWheelPresentation.GetSlotFromPointer(90, 29));
        Assert.Equal(0, BuildWheelPresentation.GetSlotFromPointer(float.NaN, 100));
        Assert.Equal(0, RadialWheelSelection.GetSlot(90, 29, 4, 45));
        for (var angle = 0; angle < 360; angle++)
            Assert.Equal(angle / 40 + 1, RadialWheelSelection.GetSlot(angle, 30, 9));
    }


    [Fact]
    public void WheelConsumesCombatInputButKeepsMovementAndOneSelectedBuildCommand()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var game = (Game1)RuntimeHelpers.GetUninitializedObject(typeof(Game1));
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        world.PrepareLocalPlayerJoin();
        world.CompleteLocalPlayerJoin(PlayerClass.Engineer);
        typeof(Game1).GetField("_world", flags)!.SetValue(game, world);
        // _clientSettings is now a computed property backed by the service container.
        var services = new ClientServiceContainer();
        services.Register(new ClientSettings { BuildMenuStyle = BuildMenuStyle.Wheel });
        typeof(Game1).GetField("_services", flags)!.SetValue(game, services);
        // The GG2-interop guards read _networkClient; the ctor never ran on this
        // uninitialized instance, so provide the production default explicitly.
        typeof(Game1).GetField("_networkClient", flags)!.SetValue(game, new NetworkGameClient());
        var state = typeof(Game1).GetField("_uiShellState", flags)!;
        state.SetValue(game, Activator.CreateInstance(state.FieldType, true));
        typeof(Game1).GetProperty("_buildMenuOpen", flags)!.SetValue(game, true);
        var apply = typeof(Game1).GetMethod("ApplyBuildMenuInputSelection", flags)!;
        var input = default(PlayerInputSnapshot) with { Left = true, FirePrimary = true, FireSecondary = true,
            UseAbility = true, SwapWeapon = true, ToggleSecondaryWeapon = true, InteractWeapon = true };
        var suppressed = (PlayerInputSnapshot)apply.Invoke(game, [input])!;
        Assert.True(suppressed.Left);
        Assert.False(suppressed.FirePrimary); Assert.False(suppressed.FireSecondary);
        Assert.False(suppressed.UseAbility); Assert.False(suppressed.SwapWeapon);
        Assert.False(suppressed.ToggleSecondaryWeapon); Assert.False(suppressed.InteractWeapon);
        typeof(Game1).GetProperty("_buildMenuOpen", flags)!.SetValue(game, false);
        typeof(Game1).GetField("_buildMenuNumericInputConsumed", flags)!.SetValue(game, true);
        typeof(Game1).GetField("_buildMenuJumpPadSelectionPressed", flags)!.SetValue(game, true);
        var selected = (PlayerInputSnapshot)apply.Invoke(game, [input])!;
        Assert.False(selected.UseAbility);
        Assert.True(selected.BuildJumpPad);
        Assert.False(selected.FirePrimary);
        Assert.False(selected.FireSecondary);
        Assert.False(selected.BuildSentry);
        Assert.False(selected.BuildDispenser);
    }
}
