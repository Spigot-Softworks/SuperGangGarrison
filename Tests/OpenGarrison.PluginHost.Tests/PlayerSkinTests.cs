using System.Text;
using System.Text.Json;
using OpenGarrison.Client;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class PlayerSkinTests
{
    private static PlayerSkinCatalog ReadCatalog()
    {
        using var stream = File.OpenRead(ProjectSourceLocator.FindFile("Core/Content/PlayerSkins.json")!);
        return PlayerSkinCatalog.Read(stream);
    }

    [Fact]
    public void ElkondoDefaultCoversBothTeamsWithoutReplacingCustomClasses()
    {
        var catalog = ReadCatalog();
        Assert.Equal("Elkondo", catalog.DefaultSet);
        Assert.Single(catalog.Sets);
        foreach (var classId in new[] { "medic", "spy", "soldier" })
        {
            Assert.NotNull(catalog.Find(classId, PlayerTeam.Red));
            Assert.NotNull(catalog.Find(classId, PlayerTeam.Blue));
        }
        Assert.Null(catalog.Find("plugin.custom.medic", PlayerTeam.Red));
        Assert.NotNull(catalog.Find("scout", PlayerTeam.Red));
    }

    [Fact]
    public void GeneratedSpritesLoadThroughStockPackWithEveryReferencedFrameAndPivot()
    {
        var catalog = ReadCatalog();
        var root = ProjectSourceLocator.FindDirectory("Core/Content/Gameplay/stock.gg2")!;
        var pack = GameplayModPackDirectoryLoader.LoadFromDirectory(root);
        foreach (var skin in catalog.Skins.Values)
        foreach (var team in new[] { PlayerTeam.Red, PlayerTeam.Blue })
        {
            var body = pack.Assets.Sprites[skin.SpriteForTeam(skin.BodySprite, team)];
            Assert.Equal(skin.Poses.Length, body.FramePaths.Count);
            Assert.Equal(24, 64 - body.OriginY); // Same foot anchor as the stock characters.
            Assert.Equal(skin.Origin[0] * skin.PixelScale, body.OriginX);
            if (skin.Weapon is { } weaponDefinition)
            {
                var weapon = pack.Assets.Sprites[skin.SpriteForTeam(weaponDefinition.Sprite, team)];
                Assert.Equal(body.FramePaths.Count, weapon.FramePaths.Count);
                Assert.Equal(weaponDefinition.Pivot[0] * skin.PixelScale, weapon.OriginX);
                Assert.Equal(weaponDefinition.Pivot[1] * skin.PixelScale, weapon.OriginY);
                foreach (var name in new[] { weaponDefinition.FireSprite, weaponDefinition.ReloadSprite }.OfType<string>())
                {
                    var action = pack.Assets.Sprites[skin.SpriteForTeam(name, team)];
                    Assert.Equal(weapon.OriginX, action.OriginX);
                    Assert.Equal(weapon.OriginY, action.OriginY);
                }
            }
            if (skin.CloakedBodySprite is { } cloakedName)
            {
                var cloaked = pack.Assets.Sprites[skin.SpriteForTeam(cloakedName, team)];
                Assert.Equal(body.FramePaths.Count, cloaked.FramePaths.Count);
                Assert.Equal(body.OriginX, cloaked.OriginX);
                Assert.Equal(body.OriginY, cloaked.OriginY);
            }
            if (skin.LegsBodySprite is { } legsName)
            {
                var legs = pack.Assets.Sprites[skin.SpriteForTeam(legsName, team)];
                Assert.Equal(body.FramePaths.Count, legs.FramePaths.Count);
                Assert.Equal(body.OriginX, legs.OriginX);
                Assert.Equal(body.OriginY, legs.OriginY);
            }
            foreach (var name in new[] { skin.BodySprite, skin.CloakedBodySprite, skin.LegsBodySprite, skin.Weapon?.Sprite, skin.Weapon?.FireSprite, skin.Weapon?.ReloadSprite }.OfType<string>())
            {
                var sprite = pack.Assets.Sprites[skin.SpriteForTeam(name, team)];
                Assert.All(sprite.FramePaths, path => Assert.True(File.Exists(Path.Combine(root, path)), path));
            }
        }
    }

    [Fact]
    public void ElkondoScoutWithoutDedicatedBackwardClipReversesTheCurrentRunCycle()
    {
        var skin = ReadCatalog().Skins["elkondo-scout"];
        Assert.False(skin.Clips.ContainsKey("runBackward"));
        var animation = new PlayerSkinAnimator();

        animation.Update(skin, 0.1f, false, 0, 180, 1, false, false);
        Assert.Equal("run", animation.ClipName);
        Assert.Equal(2, animation.Pose);

        // Changing direction keeps the current frame instead of jumping to the
        // opposite end of the cycle, then the normal run strip advances backward.
        animation.Update(skin, 1f / 60, false, 0, -180, 1, false, false);
        Assert.Equal(2, animation.Pose);
        animation.Update(skin, 0.1f, false, 0, -180, 1, false, false);
        Assert.Equal(1, animation.Pose);
    }

    [Fact]
    public void ElkondoLandingPoseWaitsForThePlayerToBeGrounded()
    {
        var skin = ReadCatalog().Skins["elkondo-scout"];
        var animation = new PlayerSkinAnimator();

        animation.Update(skin, 1f / 60, true, -180, 0, 1, false, false, grounded: false);
        animation.Update(skin, 0.1f, true, 180, 0, 1, false, false, grounded: false);
        animation.Update(skin, 1f / 60, false, 0, 0, 1, false, false, grounded: false);
        Assert.Equal("fall", animation.ClipName);
        Assert.Equal(3, animation.Pose);

        animation.Update(skin, 1f / 60, false, 0, 0, 1, false, false, grounded: true);
        Assert.Equal("land", animation.ClipName);
        Assert.Equal(4, animation.Pose);
    }

    [Fact]
    public void ElkondoRocketjumpRetainsItsAirborneVariantThroughLanding()
    {
        var skin = ReadCatalog().Skins["elkondo-soldier"];
        var animation = new PlayerSkinAnimator();
        animation.Update(skin, 0.016f, true, -150, 0, 1, true, false);
        Assert.Equal("blastStart", animation.ClipName);
        animation.Update(skin, 0.1f, true, -100, 0, 1, false, false);
        Assert.Equal(10, animation.Pose);
        animation.Update(skin, 0.1f, true, 100, 0, 1, false, false);
        Assert.Equal(11, animation.Pose);
        animation.Update(skin, 0.016f, false, 0, 0, 1, false, false);
        Assert.Equal(12, animation.Pose);
        animation.Update(skin, 0.1f, false, 0, 0, 1, false, false);
        Assert.Equal(0, animation.Pose);
        animation.Update(skin, 0.016f, true, -100, 0, 1, false, false);
        Assert.Equal("jumpStart", animation.ClipName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void InvalidFirePlaybackRateFailsWithSkinAndPropertyName(float rate)
    {
        var data = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(ProjectSourceLocator.FindFile("Core/Content/PlayerSkins.json")!))!;
        data["skins"]!["rocketman"]!["weapon"]!["firePlaybackRate"] = rate;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(data.ToJsonString()));
        var error = Assert.Throws<InvalidDataException>(() => PlayerSkinCatalog.Read(stream));
        Assert.Contains("rocketman", error.Message);
        Assert.Contains("firePlaybackRate", error.Message);
    }

    [Fact]
    public void InvalidPoseReferencesFailWithSkinAndClipName()
    {
        var path = ProjectSourceLocator.FindFile("Core/Content/PlayerSkins.json")!;
        var data = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!;
        data["skins"]!["elkondo-scout"]!["clips"]!["run"]!["frames"]![0] = 99;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(data.ToJsonString()));
        var error = Assert.Throws<InvalidDataException>(() => PlayerSkinCatalog.Read(stream));
        Assert.Contains("elkondo-scout", error.Message);
        Assert.Contains("run", error.Message);
        Assert.Contains("99", error.Message);
    }

    [Fact]
    public void EmbeddedCatalogMatchesLooseCatalogForBrowserAndDesktop()
    {
        using var stream = typeof(Game1).Assembly.GetManifestResourceStream("OpenGarrison.PlayerSkins.json")!;
        var embedded = PlayerSkinCatalog.Read(stream);
        var loose = ReadCatalog();
        Assert.Equal(JsonSerializer.Serialize(loose, PlayerSkinJsonContext.Default.PlayerSkinCatalog),
            JsonSerializer.Serialize(embedded, PlayerSkinJsonContext.Default.PlayerSkinCatalog));
    }
}
