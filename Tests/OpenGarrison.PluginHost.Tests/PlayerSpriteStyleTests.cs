using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using OpenGarrison.Client;
using OpenGarrison.ClientShared;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class PlayerSkinRenderingTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static PlayerSkinCatalog ReadCatalog()
    {
        using var stream = File.OpenRead(ProjectSourceLocator.FindFile("Core/Content/PlayerSkins.json")!);
        return PlayerSkinCatalog.Read(stream);
    }

    [Theory]
    [InlineData(PlayerTeam.Red)]
    [InlineData(PlayerTeam.Blue)]
    public void SetsSelectIndependentClassSkinsAndLeaveUnlistedClassesAlone(PlayerTeam team)
    {
        var catalog = ReadCatalog();
        Assert.Equal("Elkondo", catalog.DefaultSet);
        foreach (var classId in new[] { "scout", "soldier", "pyro", "demoman", "heavy", "medic", "engineer", "sniper", "spy" })
        {
            var skin = Assert.IsType<PlayerSkinDefinition>(catalog.Find(classId, team, "Elkondo"));
            Assert.StartsWith("Elkondo", skin.BodySprite);
            Assert.Equal(8, skin.Clips["run"].Frames.Length);
            Assert.Null(skin.Weapon);
            Assert.NotSame(skin, catalog.Find(classId, team, "Kelly"));
        }
        foreach (var set in new[] { "Kelly", "Elkondo" })
        foreach (var classId in new[] { "quote", "civvie", "plugin.custom.soldier" })
            Assert.Null(catalog.Find(classId, team, set));
    }

    [Theory]
    [InlineData(PlayerTeam.Red)]
    [InlineData(PlayerTeam.Blue)]
    public void CloakedSpyUsesKnifeBodyAndSuppressesSeparateWeaponOnlyForThatBody(PlayerTeam team)
    {
        var game = (Game1)RuntimeHelpers.GetUninitializedObject(typeof(Game1));
        var world = new SimulationWorld();
        typeof(Game1).GetField("_world", PrivateInstance)!.SetValue(game, world);
        typeof(Game1).GetField("_playerSkins", PrivateInstance)!.SetValue(game, new Lazy<PlayerSkinCatalog>(ReadCatalog));
        var statesField = typeof(Game1).GetField("_playerRenderStates", PrivateInstance)!;
        statesField.SetValue(game, Activator.CreateInstance(statesField.FieldType));
        // The GG2-interop guards read _networkClient; the ctor never ran on this
        // uninitialized instance, so provide the production default explicitly.
        typeof(Game1).GetField("_networkClient", PrivateInstance)!.SetValue(game, new NetworkGameClient());
        var player = new PlayerEntity(12, CharacterClassCatalog.Spy, "Spy");
        player.Spawn(team, 0, 0);
        var select = typeof(Game1).GetMethod("TryGetPlayerSkinBody", PrivateInstance)!;
        var includesWeapon = typeof(Game1).GetMethod("PlayerSkinIncludesCloakedWeapon", PrivateInstance)!;
        object?[] args = [player, null];
        Assert.True((bool)select.Invoke(game, args)!);
        var normalBody = args[1]!;
        Assert.Equal($"ElkondoSpy{team}BodyS", normalBody.GetType().GetProperty("SpriteName")!.GetValue(normalBody));
        Assert.False((bool)includesWeapon.Invoke(game, [player, normalBody])!);

        Assert.True(player.TryToggleSpyCloak());
        Assert.True((bool)select.Invoke(game, args)!);
        var cloakedBody = args[1]!;
        Assert.Equal($"ElkondoSpy{team}CloakedBodyS", cloakedBody.GetType().GetProperty("SpriteName")!.GetValue(cloakedBody));
        Assert.True((bool)includesWeapon.Invoke(game, [player, cloakedBody])!);
        Assert.False((bool)includesWeapon.Invoke(game, [player, normalBody])!);

        // The knife stays part of the held airborne body, including cloak opacity.
        var stateType = typeof(Game1).GetNestedType("PlayerRenderState", BindingFlags.Public | BindingFlags.NonPublic)!;
        var state = Activator.CreateInstance(stateType)!;
        var animation = (PlayerSkinAnimator)stateType.GetProperty("SkinAnimation")!.GetValue(state)!;
        var catalog = ((Lazy<PlayerSkinCatalog>)typeof(Game1).GetField("_playerSkins", PrivateInstance)!.GetValue(game)!).Value;
        animation.Update(catalog.Skins["elkondo-spy"], 0.1f, true, -100, 0, 1, false, false);
        ((IDictionary)statesField.GetValue(game)!).Add(player.Id, state);
        Assert.True((bool)select.Invoke(game, args)!);
        var airborneBody = args[1]!;
        Assert.Equal($"ElkondoSpy{team}CloakedBodyS", airborneBody.GetType().GetProperty("SpriteName")!.GetValue(airborneBody));
        Assert.Equal(2f, airborneBody.GetType().GetProperty("AnimationImage")!.GetValue(airborneBody));
        Assert.True((bool)includesWeapon.Invoke(game, [player, airborneBody])!);
    }

    [Theory]
    [InlineData("scout")]
    [InlineData("soldier")]
    [InlineData("pyro")]
    [InlineData("demoman")]
    [InlineData("heavy")]
    [InlineData("medic")]
    [InlineData("engineer")]
    [InlineData("sniper")]
    [InlineData("spy")]
    public void ElkondoUsesTheAuthoredJumpSequenceAndResumesLocomotionOnLanding(string classId)
    {
        var skin = ReadCatalog().Skins["elkondo-" + classId];
        Assert.Equal([1], skin.Clips["jumpStart"].Frames);
        Assert.Equal([2], skin.Clips["rise"].Frames);
        Assert.Equal([3], skin.Clips["fall"].Frames);
        Assert.Equal([4], skin.Clips["land"].Frames);
        Assert.Equal(18, skin.Clips["jumpStart"].FramesPerSecond);
        Assert.Equal(15, skin.Clips["land"].FramesPerSecond);

        var animation = new PlayerSkinAnimator();
        animation.Update(skin, 0.1f, false, 0, 180, 1, false, false);
        animation.Update(skin, 1f / 60, true, -180, 180, 1, false, false);
        Assert.Equal("jumpStart", animation.ClipName);
        Assert.Equal(1, animation.Pose);
        animation.Update(skin, 0.1f, true, -180, 180, 1, false, false);
        Assert.Equal("rise", animation.ClipName);
        Assert.Equal(2, animation.Pose);
        animation.Update(skin, 1f / 60, true, 180, 180, 1, false, false);
        Assert.Equal("fall", animation.ClipName);
        Assert.Equal(3, animation.Pose);
        animation.Update(skin, 1f / 60, false, 0, 180, 1, false, false);
        Assert.Equal("land", animation.ClipName);
        Assert.Equal(4, animation.Pose);
        animation.Update(skin, 0.1f, false, 0, 180, 1, false, false);
        Assert.Equal("run", animation.ClipName);
        Assert.InRange(animation.Pose, 1, 8);
    }

    [Fact]
    public void AllIdleSpritesPlaceTheirVisibleFeetOnTheSameGroundLine()
    {
        var catalog = ReadCatalog();
        var root = ProjectSourceLocator.FindDirectory("Core/Content/Gameplay/stock.gg2")!;
        var pack = GameplayModPackDirectoryLoader.LoadFromDirectory(root);
        foreach (var skin in catalog.Skins.Values)
        foreach (var team in new[] { PlayerTeam.Red, PlayerTeam.Blue })
        foreach (var name in new[] { skin.BodySprite, skin.CloakedBodySprite }.OfType<string>())
        {
            var sprite = pack.Assets.Sprites[skin.SpriteForTeam(name, team)];
            var idleFrame = skin.Clips["idle"].Frames[0];
            using var image = Image.Load<Rgba32>(Path.Combine(root, sprite.FramePaths[idleFrame]));
            var bottom = 0;
            for (var y = 0; y < image.Height; y++)
            for (var x = 0; x < image.Width; x++)
                if (image[x, y].A > 0) bottom = y + 1;
            Assert.True(bottom - sprite.OriginY == 24,
                $"{sprite.Id}: visible feet end {bottom - sprite.OriginY} pixels below the anchor, expected 24.");
        }
    }
}
