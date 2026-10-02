using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using OpenGarrison.Client;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class AmogusPlayerSkinTests
{
    [Fact]
    public void ImpostorSkinIsBoundOnlyToQuoteAndCarriesAuthoredWeaponAndCorpseMetadata()
    {
        var catalog = PlayerSkinCatalog.Load();
        var red = catalog.Find("quote", PlayerTeam.Red);
        var blue = catalog.Find("plugin.quote-curly.quote", PlayerTeam.Blue);

        Assert.Equal("Elkondo", catalog.DefaultSet);
        Assert.NotNull(red);
        Assert.NotNull(blue);
        Assert.Same(red, catalog.Find("plugin.quote-curly.quote", PlayerTeam.Red));
        Assert.Same(blue, catalog.Find("quote", PlayerTeam.Blue));
        Assert.Null(catalog.Find("civilian", PlayerTeam.Red));
        Assert.Equal("ImpostorRedBodyS", red!.SpriteForTeam(red.BodySprite, PlayerTeam.Red));
        Assert.Equal("ImpostorBlueDeadS", red.SpriteForTeam(red.CorpseSprite!, PlayerTeam.Blue));
        Assert.Equal("ImpostorRedTauntS", red.SpriteForTeam(red.TauntSprite!, PlayerTeam.Red));
        Assert.Equal(8, red.Clips["run"].Frames.Length);
        Assert.Equal(9, red.Poses.Length);

        var weapon = Assert.IsType<PlayerSkinWeapon>(red.Weapon);
        Assert.Equal("weapon.blade", weapon.ItemId);
        Assert.Equal(new[] { "plugin.quote-curly.weapon.blade" }, weapon.ItemAliases);
        Assert.Equal("BladeS", weapon.MatchSprite);
        Assert.Equal(new[] { 19, 24 }, weapon.Pivot);
        Assert.Equal(new[] { 0, 0 }, weapon.AttachmentOffset);
        Assert.True(weapon.DrawBehindBody);

        int[][] expectedWeaponOffsets =
        [
            [0, 0], [0, 0], [0, -1], [0, -1], [0, 0], [0, 0], [0, -1], [0, -1], [0, 0],
        ];
        for (var index = 0; index < expectedWeaponOffsets.Length; index++)
        {
            Assert.Equal(expectedWeaponOffsets[index], red.Poses[index].WeaponOffset);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void QuoteWeaponPresentationUsesImpostorArtAndBehindBodyOrdering(bool legacy)
    {
        var (game, world, player) = CreateGame("quote", legacy);
        var skin = Assert.IsType<PlayerSkinDefinition>(game.GetPlayerSkin(player));
        var renderState = new Game1.PlayerRenderState();
        game._playerRenderStates[player.Id] = renderState;
        renderState.SkinAnimation.Update(skin, 0.1f, airborne: false, verticalSpeed: 0f,
            horizontalSpeed: 180f, facingScale: 1f, blastMovement: false, fired: false, grounded: true);

        var controller = new GameplayWeaponRenderController((IRenderContext)game);
        var weapon = controller.GetWeaponRenderDefinitionProxy(player);

        Assert.Equal(2, weapon.PoseFrameIndex);
        Assert.Equal("ImpostorRedWeaponS", weapon.NormalSpriteName);
        Assert.True(weapon.DrawBehindBody);
        Assert.Equal(-32f, weapon.XOffset);
        Assert.Equal(-54f, weapon.YOffset);
        Assert.Equal(6f, weapon.XOffset + (19 * skin.PixelScale));
        Assert.Equal(-6f, weapon.YOffset + (24 * skin.PixelScale));
        Assert.False(world.IsPlayerHumiliated(player));
    }

    [Fact]
    public void LegacyQuoteUsesIdleAndRunImpostorAliasesAndAuthoredCorpseUsesClassicPath()
    {
        var (game, world, player) = CreateGame("civilian", legacy: true);
        Assert.Equal(PlayerClass.Quote, player.ClassId);
        Assert.Equal("civilian", player.GameplayClassId);
        // A fresh player has not landed yet, and the legacy path treats airborne as running.
        for (var tick = 0; tick < 60 && !player.IsGrounded; tick += 1)
        {
            world.AdvanceOneTick();
        }

        Assert.True(player.IsGrounded);
        var renderState = new Game1.PlayerRenderState { AnimationHorizontalSpeed = 240f, BodyAnimationImage = 3f };
        game._playerRenderStates[player.Id] = renderState;
        var spriteController = new GameplayPlayerSpriteRenderController((IRenderContext)game);

        var running = spriteController.GetPlayerBodySpriteSelection(player);
        Assert.Equal("ImpostorRedRunS", running.SpriteName);
        Assert.Equal(3f, running.AnimationImage);

        renderState.AnimationHorizontalSpeed = 0f;
        renderState.BodyAnimationImage = 0f;
        var idle = spriteController.GetPlayerBodySpriteSelection(player);
        Assert.Equal("ImpostorRedS", idle.SpriteName);

        var corpseSpriteName = typeof(Game1).GetMethod("GetDynamicRagdollCorpseSpriteName",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        object?[] corpseArguments = ["civilian", PlayerClass.Quote, PlayerTeam.Red, DeadBodyAnimationKind.Default];
        Assert.Equal("ImpostorRedDeadS", corpseSpriteName.Invoke(game, corpseArguments));
        var resolve = typeof(Game1).GetMethod("TryResolveElkondoCorpseSprite",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        object?[] arguments = ["civilian", PlayerClass.Quote, PlayerTeam.Red, null, null, null];
        Assert.False((bool)resolve.Invoke(game, arguments)!);
    }

    [Fact]
    public void CivilianKeepsItsDefaultPresentationWithoutAnImpostorSkin()
    {
        var (game, _, player) = CreateGame("civilian", legacy: false);

        Assert.True(player.IsCivilian);
        Assert.Null(game.GetPlayerSkin(player));
        var weapon = new GameplayWeaponRenderController((IRenderContext)game)
            .GetWeaponRenderDefinitionProxy(player);
        Assert.False(weapon.DrawBehindBody);
        Assert.False(weapon.NormalSpriteName?.StartsWith("Impostor", StringComparison.Ordinal) == true);
    }

    private static (Game1 Game, SimulationWorld World, PlayerEntity Player) CreateGame(string gameplayClassId, bool legacy)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var game = (Game1)RuntimeHelpers.GetUninitializedObject(typeof(Game1));
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        var player = world.LocalPlayer;
        player.SetClassDefinition(CharacterClassCatalog.RuntimeRegistry.CreateCharacterClassDefinition(gameplayClassId));

        // GetUninitializedObject skips field initializers; skin pose lookup reads the session state.
        typeof(Game1).GetField("_gameplaySessionState", flags)!.SetValue(game, new Game1.GameplaySessionState());
        var services = new ClientServiceContainer();
        typeof(Game1).GetField("_services", flags)!.SetValue(game, services);
        services.Register(new GameplayManager((IGameplayContext)game));
        typeof(Game1).GetField("_world", flags)!.SetValue(game, world);
        var networkClient = new NetworkGameClient();
        typeof(Game1).GetField("_networkClient", flags)!.SetValue(game, networkClient);
        typeof(NetworkGameClient).GetField("<IsLegacyGg2Connection>k__BackingField", flags)!
            .SetValue(networkClient, legacy);
        typeof(Game1).GetField("_playerSkins", flags)!
            .SetValue(game, new Lazy<PlayerSkinCatalog>(PlayerSkinCatalog.Load));
        typeof(Game1).GetField("_playerRenderStates", flags)!
            .SetValue(game, new Dictionary<int, Game1.PlayerRenderState>());
        return (game, world, player);
    }
}
