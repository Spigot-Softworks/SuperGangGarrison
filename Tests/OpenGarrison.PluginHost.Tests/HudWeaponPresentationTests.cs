using OpenGarrison.Client;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class HudWeaponPresentationTests
{
    [Fact]
    public void BothFlaregunHudFramesFitAboveTheReloadBarWithoutChangingTheWorldPivot()
    {
        var packRoot = ProjectSourceLocator.FindDirectory("Core/Content/Gameplay/stock.gg2")!;
        using var item = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(packRoot, "items/weapons/weapon.pyro-flaregun.json")));
        var presentation = item.RootElement.GetProperty("presentation");
        Assert.Equal("FlaregunS", presentation.GetProperty("worldSpriteName").GetString());
        var hudName = presentation.GetProperty("hudSpriteName").GetString();
        using var hud = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(packRoot, $"sprites/weapons/secondaries/{hudName}.json")));
        var frames = hud.RootElement.GetProperty("framePaths").EnumerateArray().ToArray();
        Assert.Equal(2, frames.Length);
        foreach (var frame in frames)
        {
            var image = TextureDecodeUtility.DecodeTextureData(File.ReadAllBytes(Path.Combine(packRoot, frame.GetString()!)), applyLegacyChromaKey: true);
            var loaded = new LoadedSpriteFrame(null!, new Microsoft.Xna.Framework.Rectangle(0, 0, image.Width, image.Height), OpaqueBounds: image.OpaqueBounds);
            var layout = WeaponHudIconLayout.Fit(loaded);
            var bounds = image.OpaqueBounds;
            var left = layout.Offset.X + (bounds.Left - layout.Origin.X) * layout.Scale;
            var top = layout.Offset.Y + (bounds.Top - layout.Origin.Y) * layout.Scale;
            var right = left + bounds.Width * layout.Scale;
            var bottom = top + bounds.Height * layout.Scale;
            Assert.InRange(left, -55.2f, 21f);
            Assert.InRange(right, left, 21.1f);
            Assert.InRange(top, -14.5f, 2f);
            Assert.InRange(bottom, top, 2.1f);
        }
        using var world = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(packRoot, "sprites/weapons/secondaries/FlaregunS.json")));
        Assert.Equal(-1, world.RootElement.GetProperty("originY").GetInt32());
    }

    [Fact]
    public void HeavyAmmoHudFollowsActualMinigunShotAndKeepsShotgunSlotSeparate()
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        world.PrepareLocalPlayerJoin();
        world.CompleteLocalPlayerJoin(PlayerClass.Heavy);
        world.LocalPlayer.SetSpawnRoomState(false);

        var primaryBefore = Game1.ResolveWeaponHudAmmoState(world.LocalPlayer, offhandSelected: false);
        world.SetLocalInput(default(PlayerInputSnapshot) with
        {
            FirePrimary = true,
            AimWorldX = world.LocalPlayer.X + 256f,
            AimWorldY = world.LocalPlayer.Y,
        });
        world.AdvanceOneTick();

        var primaryAfter = Game1.ResolveWeaponHudAmmoState(world.LocalPlayer, offhandSelected: false);
        Assert.Equal(primaryBefore.MaxShells, primaryAfter.MaxShells);
        Assert.True(primaryAfter.CurrentShells < primaryBefore.CurrentShells);

        Assert.True(world.TrySetNetworkPlayerGameplayEquippedSlot(
            SimulationWorld.LocalPlayerSlot,
            GameplayEquipmentSlot.Secondary));
        var shotgun = Game1.ResolveWeaponHudAmmoState(world.LocalPlayer, offhandSelected: true);
        Assert.Equal(world.LocalPlayer.ExperimentalOffhandCurrentShells, shotgun.CurrentShells);
        Assert.Equal(world.LocalPlayer.ExperimentalOffhandMaxShells, shotgun.MaxShells);
        Assert.Equal(primaryAfter.CurrentShells, Game1.ResolveWeaponHudAmmoState(world.LocalPlayer, offhandSelected: false).CurrentShells);
    }

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    public void StowedPrimaryPanelAppearsBesideAnEquippedSecondary(
        bool showOnlyActiveWeapon,
        bool hasStowedPrimaryWeapon,
        bool expected)
    {
        Assert.Equal(
            expected,
            Game1.ShouldShowStowedPrimaryWeaponHud(showOnlyActiveWeapon, hasStowedPrimaryWeapon));
    }

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    public void SecondaryPanelRepresentsOnlyARealSecondaryWeapon(
        bool showOnlyActiveWeapon,
        bool secondaryWeaponAvailable,
        bool expected)
    {
        Assert.Equal(
            expected,
            Game1.ShouldShowSecondaryWeaponHud(
                showOnlyActiveWeapon,
                secondaryWeaponAvailable));
    }

    [Theory]
    [InlineData(PlayerClass.Scout, true)]
    [InlineData(PlayerClass.Heavy, true)]
    [InlineData(PlayerClass.Medic, false)]
    public void MedicNeedlegunPanelDoesNotRemainVisibleWhileStowed(
        PlayerClass classId,
        bool expected)
    {
        Assert.Equal(expected, Game1.ShouldShowSecondaryWeaponHudWhileStowed(classId));
    }

    [Fact]
    public void StowedWeaponGrantedAbilityRemainsVisibleUnlessMetadataHidesIt()
    {
        Assert.False(Game1.ShouldShowStowedGrantedAbilityHud(null));
        Assert.True(Game1.ShouldShowStowedGrantedAbilityHud(new GameplayItemHudPresentationDefinition()));
        Assert.False(Game1.ShouldShowStowedGrantedAbilityHud(
            new GameplayItemHudPresentationDefinition(ShowWhenEquippedOnly: true)));
    }
}
