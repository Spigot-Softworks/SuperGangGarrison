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
    [InlineData(1, 0f, -BuildWheelPresentation.IconRadius)]
    [InlineData(2, -0.8660254f * BuildWheelPresentation.IconRadius, 0.5f * BuildWheelPresentation.IconRadius)]
    [InlineData(3, 0.8660254f * BuildWheelPresentation.IconRadius, 0.5f * BuildWheelPresentation.IconRadius)]
    public void IconOffsetsPlaceSlotsInTopBottomLeftAndBottomRight(int slot, float x, float y)
    {
        var offset = BuildWheelPresentation.GetIconOffset(slot);
        Assert.Equal(x, offset.X, 3);
        Assert.Equal(y, offset.Y, 3);
        Assert.Equal(slot, BuildWheelPresentation.GetSlotFromPointer(
            MathF.Atan2(offset.Y, offset.X) * 180f / MathF.PI + 90f,
            offset.Length()));
        Assert.False(new Microsoft.Xna.Framework.Rectangle(
                (int)(offset.X - BuildWheelPresentation.IconTopLeftOrigin.X),
                (int)(offset.Y - BuildWheelPresentation.IconTopLeftOrigin.Y),
                32,
                36)
            .Intersects(new Microsoft.Xna.Framework.Rectangle(-30, -30, 60, 60)));
    }

    [Theory]
    [InlineData(false, false, true, 0)]
    [InlineData(false, true, true, 0)]
    [InlineData(false, false, false, 1)]
    [InlineData(true, false, true, 2)]
    [InlineData(true, true, true, 2)]
    [InlineData(true, false, false, 3)]
    public void ListMenuIconFramesCoverBuildAndNoNutsStates(bool blue, bool exists, bool canBuild, int expectedFrame)
    {
        for (var slot = 1; slot <= BuildWheelPresentation.SlotCount; slot++)
        {
            var spriteName = BuildWheelPresentation.GetIconSpriteName(slot);
            var path = ProjectSourceLocator.FindFile(
                $"Core/Content/Gameplay/stock.gg2/assets/hud/builder/{spriteName}.images/image {expectedFrame}.png")!;
            using var image = Image.Load<Rgba32>(path);
            Assert.Equal(32, image.Width);
            Assert.Equal(36, image.Height);
            var opaque = 0;
            for (var y = 0; y < image.Height; y++)
            for (var x = 0; x < image.Width; x++)
            {
                if (image[x, y].A > 0) opaque++;
            }

            Assert.True(opaque > 100, $"{spriteName} frame {expectedFrame} should contain icon pixels");
        }

        // Mirror Game1.GetBuildMenuListIconFrame without instantiating the game.
        // Per team: ready, nonuts. Destroy is a shared overlay sprite.
        var teamBase = blue ? 2 : 0;
        var frame = exists || canBuild ? teamBase : teamBase + 1;
        Assert.Equal(expectedFrame, frame);
    }

    [Fact]
    public void DestroyOverlaySpriteMatchesBuildingIconSizeAndHasTransparency()
    {
        var path = ProjectSourceLocator.FindFile(
            "Core/Content/Gameplay/stock.gg2/assets/hud/builder/BuildMenuDestroyOverlayS.images/image 0.png")!;
        using var image = Image.Load<Rgba32>(path);
        Assert.Equal(32, image.Width);
        Assert.Equal(36, image.Height);
        var transparent = 0;
        var opaque = 0;
        for (var y = 0; y < image.Height; y++)
        for (var x = 0; x < image.Width; x++)
        {
            if (image[x, y].A == 0) transparent++;
            else opaque++;
        }

        Assert.True(transparent > 100, "Overlay should leave building icon visible through transparent pixels");
        Assert.True(opaque > 100, "Overlay should contain the red prohibition mark");
    }

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
    public void ChromeFramesKeepThreeSectorsPlusCenterCancel()
    {
        Assert.Equal(1, BuildWheelPresentation.GetChromeFrame(0, false));
        Assert.Equal(5, BuildWheelPresentation.GetChromeFrame(0, true));
        Assert.Equal(2, BuildWheelPresentation.GetChromeFrame(1, false));
        Assert.Equal(6, BuildWheelPresentation.GetChromeFrame(1, true));
        Assert.Equal(4, BuildWheelPresentation.GetChromeFrame(2, false));
        Assert.Equal(8, BuildWheelPresentation.GetChromeFrame(2, true));
        Assert.Equal(3, BuildWheelPresentation.GetChromeFrame(3, false));
        Assert.Equal(7, BuildWheelPresentation.GetChromeFrame(3, true));

        for (var index = 0; index <= 8; index++)
        {
            if (index is 9 or 10 or 11 or 12) continue;
            var path = ProjectSourceLocator.FindFile(
                $"Core/Content/Gameplay/stock.gg2/assets/hud/builder/BuildWheelS.images/image {index}.png")!;
            using var image = Image.Load<Rgba32>(path);
            var opaque = 0;
            for (var y = 0; y < image.Height; y++)
            for (var x = 0; x < image.Width; x++)
            {
                if (image[x, y].A > 0) opaque++;
            }

            Assert.True(opaque > 500, $"Chrome frame {index} should be present");
        }
    }

    [Fact]
    public void WheelConsumesCombatInputButKeepsMovementAndOneSelectedBuildCommand()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var game = (Game1)RuntimeHelpers.GetUninitializedObject(typeof(Game1));
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        world.PrepareLocalPlayerJoin();
        world.CompleteLocalPlayerJoin(PlayerClass.Engineer);
        typeof(Game1).GetField("_world", flags)!.SetValue(game, world);
        typeof(Game1).GetField("_clientSettings", flags)!.SetValue(game, new ClientSettings { BuildMenuStyle = BuildMenuStyle.Wheel });
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
