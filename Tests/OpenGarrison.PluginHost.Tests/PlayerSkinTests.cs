using System.Text;
using System.Text.Json;
using System.Reflection;
using System.Runtime.CompilerServices;
using OpenGarrison.Client;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;
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
    public void DefaultsCoverBothTeamsWithoutReplacingCustomClasses()
    {
        var catalog = ReadCatalog();
        foreach (var classId in new[] { "medic", "spy", "soldier" })
        {
            Assert.NotNull(catalog.Find(classId, PlayerTeam.Red));
            Assert.NotNull(catalog.Find(classId, PlayerTeam.Blue));
        }
        Assert.Null(catalog.Find("plugin.custom.medic", PlayerTeam.Red));
        Assert.NotNull(catalog.Find("scout", PlayerTeam.Red));
        Assert.Null(catalog.Find("scout", PlayerTeam.Red, "Kelly"));
        catalog.Sets["Kelly"].Remove("medic");
        Assert.Null(catalog.Find("medic", PlayerTeam.Red, "Kelly"));
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

    [Theory]
    [InlineData(-90, 1, "runBackward", 9, 16)]
    [InlineData(90, -1, "runBackward", 9, 16)]
    [InlineData(90, 1, "run", 1, 8)]
    public void InfiltratorChoosesCycleRelativeToFacing(float speed, float facing, string clip, int min, int max)
    {
        var skin = ReadCatalog().Skins["infiltrator"];
        var animation = new PlayerSkinAnimator();
        for (var i = 0; i < 120; i++)
        {
            animation.Update(skin, 1f / 60, false, 0, speed, facing, false, false);
            Assert.Equal(clip, animation.ClipName);
            Assert.InRange(animation.Pose, min, max);
        }
    }

    [Theory]
    [InlineData("healer")]
    [InlineData("elkondo-scout")]
    public void SkinsWithoutDedicatedBackwardClipReverseTheCurrentRunCycle(string skinId)
    {
        var skin = ReadCatalog().Skins[skinId];
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

    [Theory]
    [InlineData("healer")]
    [InlineData("infiltrator")]
    [InlineData("rocketman")]
    public void KellyJumpStartupUsesTheFasterTiming(string skinId)
    {
        Assert.Equal(18, ReadCatalog().Skins[skinId].Clips["jumpStart"].FramesPerSecond);
    }

    [Theory]
    [InlineData("rocketman", false, "jumpStart", 10, 11, 12)]
    [InlineData("rocketman", true, "blastStart", 14, 15, 16)]
    [InlineData("elkondo-soldier", true, "blastStart", 10, 11, 12)]
    public void RocketmanRetainsAirborneVariantThroughLanding(string skinId, bool blast, string start, int rise, int fall, int land)
    {
        var skin = ReadCatalog().Skins[skinId];
        var animation = new PlayerSkinAnimator();
        animation.Update(skin, 0.016f, true, -150, 0, 1, blast, false);
        Assert.Equal(start, animation.ClipName);
        animation.Update(skin, 0.1f, true, -100, 0, 1, false, false);
        Assert.Equal(rise, animation.Pose);
        animation.Update(skin, 0.1f, true, 100, 0, 1, false, false);
        Assert.Equal(fall, animation.Pose);
        animation.Update(skin, 0.016f, false, 0, 0, 1, false, false);
        Assert.Equal(land, animation.Pose);
        animation.Update(skin, 0.1f, false, 0, 0, 1, false, false);
        Assert.Equal(0, animation.Pose);
        animation.Update(skin, 0.016f, true, -100, 0, 1, false, false);
        Assert.Equal("jumpStart", animation.ClipName);
    }

    [Fact]
    public void InfiltratorFallTransitionsOnceAndHoldsUntilLandingOnEveryJump()
    {
        var skin = ReadCatalog().Skins["infiltrator"];
        var animation = new PlayerSkinAnimator();
        for (var jump = 0; jump < 2; jump++)
        {
            animation.Update(skin, 0.016f, true, -150, 0, 1, false, false);
            animation.Update(skin, 0.1f, true, -100, 0, 1, false, false);
            Assert.Equal("rise", animation.ClipName);
            animation.Update(skin, 0.016f, true, 100, 0, 1, false, false);
            Assert.Equal(3, animation.Pose);
            animation.Update(skin, 0.04f, true, 100, 0, 1, false, false);
            Assert.Equal(3, animation.Pose);
            for (var frame = 0; frame < 180; frame++)
            {
                animation.Update(skin, 0.05f, true, 100, 0, 1, false, false);
                Assert.Equal("fall", animation.ClipName);
                Assert.Equal(4, animation.Pose);
            }
            animation.Update(skin, 0.016f, false, 0, 0, 1, false, false);
            Assert.Equal("land", animation.ClipName);
            Assert.Equal(5, animation.Pose);
        }
    }

    [Theory]
    [InlineData(PlayerTeam.Red)]
    [InlineData(PlayerTeam.Blue)]
    public void RocketmanSkinSpeedsUpOnlyTheFiringPresentation(PlayerTeam team)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var game = (Game1)RuntimeHelpers.GetUninitializedObject(typeof(Game1));
        typeof(Game1).GetField("_playerSkins", flags)!.SetValue(game, new Lazy<PlayerSkinCatalog>(ReadCatalog));
        var player = new PlayerEntity(12, CharacterClassCatalog.Soldier, "Soldier");
        player.Spawn(team, 0, 0);
        Assert.True(CharacterClassCatalog.RuntimeRegistry.TryGetItem("weapon.rocketlauncher", out var item));
        var definitionType = typeof(Game1).GetNestedType("WeaponRenderDefinition", BindingFlags.NonPublic)!;
        var definition = Activator.CreateInstance(definitionType)!;
        definitionType.GetProperty("RecoilDurationSeconds")!.SetValue(definition, 1f);
        definitionType.GetProperty("ReloadDurationSeconds")!.SetValue(definition, 0.7f);
        var apply = typeof(Game1).GetMethod("ApplyPlayerSkinWeapon", flags)!;
        var result = apply.Invoke(game, [player, item.Presentation, definition, true])!;
        Assert.Equal(1f / 1.6f, (float)definitionType.GetProperty("RecoilDurationSeconds")!.GetValue(result)!, 5);
        Assert.Equal(0.7f, (float)definitionType.GetProperty("ReloadDurationSeconds")!.GetValue(result)!);
        Assert.Equal($"Rocketman{team}FireS", definitionType.GetProperty("RecoilSpriteName")!.GetValue(result));
        Assert.Equal(-28f, definitionType.GetProperty("XOffset")!.GetValue(result)); // Two source pixels forward at 2x scale.
        Assert.Equal(0f, definitionType.GetProperty("ReloadSpriteXOffset")!.GetValue(result));
        // A different item's presentation must not inherit the skin's timing.
        Assert.Equal(definition, apply.Invoke(game, [player, new GameplayItemPresentationDefinition(), definition, true]));
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
    public void MidairShotChangesRocketmanPoseButHealerNeverUsesUnusedFrames()
    {
        var catalog = ReadCatalog();
        var animation = new PlayerSkinAnimator();
        animation.Update(catalog.Skins["rocketman"], 0.016f, true, 100, 0, 1, false, true);
        Assert.Equal(15, animation.Pose);
        animation.Update(catalog.Skins["healer"], 0.016f, true, 100, 0, 1, false, false);
        Assert.Equal(3, animation.Pose);
        animation.Update(catalog.Skins["healer"], 0.016f, false, 0, 0, 1, false, false);
        Assert.Equal(4, animation.Pose);
    }

    [Fact]
    public void InvalidPoseReferencesFailWithSkinAndClipName()
    {
        var path = ProjectSourceLocator.FindFile("Core/Content/PlayerSkins.json")!;
        var data = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!;
        data["skins"]!["healer"]!["clips"]!["run"]!["frames"]![0] = 99;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(data.ToJsonString()));
        var error = Assert.Throws<InvalidDataException>(() => PlayerSkinCatalog.Read(stream));
        Assert.Contains("healer", error.Message);
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
