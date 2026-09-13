using System.Reflection;
using System.Runtime.CompilerServices;
using OpenGarrison.Client;
using OpenGarrison.Core;
using Microsoft.Xna.Framework;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class BuildWheelTests
{
    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, true)]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    public void SuppliedIconsOccupyFourSeparateSectorsInEveryResourceAndOwnershipState(bool blue, bool exists, bool canBuild)
    {
        for (var slot = 1; slot <= 4; slot++)
        {
            var index = BuildWheelPresentation.GetIconFrame(slot, blue, exists, canBuild);
            var path = ProjectSourceLocator.FindFile($"Core/Content/Gameplay/stock.gg2/assets/hud/builder/BuildWheelS.images/image {index}.png")!;
            using var image = Image.Load<Rgba32>(path);
            int left = image.Width, top = image.Height, right = -1, bottom = -1, count = 0;
            for (var y = 0; y < image.Height; y++)
            for (var x = 0; x < image.Width; x++)
            {
                if (image[x, y].A == 0) continue;
                left = Math.Min(left, x); right = Math.Max(right, x);
                top = Math.Min(top, y); bottom = Math.Max(bottom, y); count++;
            }
            Assert.True(count > 100, $"Missing icon pixels in frame {index}");
            var bounds = new Microsoft.Xna.Framework.Rectangle(left, top, right - left + 1, bottom - top + 1);
            var destination = BuildWheelPresentation.GetIconOffset(slot);
            var origin = BuildWheelPresentation.GetIconOrigin(index);
            Assert.Equal(bounds.Center.ToVector2(), origin);
            var rendered = new Microsoft.Xna.Framework.Rectangle((int)(destination.X + left - origin.X),
                (int)(destination.Y + top - origin.Y), bounds.Width, bounds.Height);
            Assert.InRange(Vector2.Distance(rendered.Center.ToVector2(), destination), 0, 1f);
            Assert.False(rendered.Intersects(new Microsoft.Xna.Framework.Rectangle(-30, -30, 60, 60)),
                $"Frame {index} must not cover the cancel button");
            Assert.Equal(slot, RadialWheelSelection.GetSlot(MathF.Atan2(destination.Y, destination.X) * 180 / MathF.PI + 90,
                destination.Length(), 4, 45));
        }
    }

    [Theory]
    [InlineData(0, 1)] [InlineData(44, 1)] [InlineData(45, 2)]
    [InlineData(90, 2)] [InlineData(180, 3)] [InlineData(270, 4)]
    [InlineData(315, 1)] [InlineData(-90, 4)] [InlineData(360, 1)]
    public void CardinalArtworkMatchesWheelSelection(float angle, int expected)
        => Assert.Equal(expected, RadialWheelSelection.GetSlot(angle, 60, 4, 45));

    [Fact]
    public void CenterCancelsAndBubbleWheelKeepsItsNineOriginalSectors()
    {
        Assert.Equal(0, RadialWheelSelection.GetSlot(90, 29, 4, 45));
        Assert.Equal(0, RadialWheelSelection.GetSlot(float.NaN, 100, 4, 45));
        for (var angle = 0; angle < 360; angle++)
            Assert.Equal(angle / 40 + 1, RadialWheelSelection.GetSlot(angle, 30, 9));
    }

    [Fact]
    public void WheelConsumesCombatInputButKeepsMovementAndOneSelectedBuildCommand()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var game = (Game1)RuntimeHelpers.GetUninitializedObject(typeof(Game1));
        typeof(Game1).GetField("_world", flags)!.SetValue(game, new SimulationWorld(new SimulationConfig { EnableLocalDummies = false }));
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
        Assert.True(selected.UseAbility); Assert.False(selected.FirePrimary); Assert.False(selected.FireSecondary);
        Assert.False(selected.BuildSentry); Assert.False(selected.BuildDispenser);
    }
}
