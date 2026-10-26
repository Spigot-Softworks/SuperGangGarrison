using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using OpenGarrison.Client;
using OpenGarrison.ClientShared;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class ElkondoWeaponAndFlameAssetTests
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [Theory]
    [InlineData("heavy", "weapon.tommy-gun", PlayerTeam.Red, "ElkondoHeavyRedTommyGunS", true)]
    [InlineData("heavy", "weapon.tommy-gun", PlayerTeam.Blue, "ElkondoHeavyBlueTommyGunS", true)]
    [InlineData("spy", "weapon.revolver", PlayerTeam.Red, "ElkondoSpyRedWeaponS", false)]
    [InlineData("spy", "weapon.revolver", PlayerTeam.Blue, "ElkondoSpyBlueWeaponS", false)]
    public void OnlyElkondoUsesNewWeaponStrips(string classId, string itemId, PlayerTeam team, string expected, bool torso)
    {
        var (game, player, renderer, settings) = CreateGame(classId, itemId, team);
        var definition = renderer.GetWeaponRenderDefinitionProxy(player);

        Assert.Equal(expected, definition.NormalSpriteName);
        Assert.True(definition.SingleTeamFrames);
        Assert.Equal(torso, definition.UseTorsoReplacement);
        Assert.Equal(20, definition.RecoilFramesPerSecond);
        Assert.Equal(1000f / 65, definition.ReloadFramesPerSecond);
        Assert.Equal(0, definition.ReloadSpriteXOffset);
        Assert.Equal(0, definition.ReloadSpriteYOffset);
        Assert.Equal(torso ? 0 : -32, definition.XOffset);
        Assert.Equal(torso ? -8 : -40, definition.YOffset);
        if (torso) Assert.Equal($"ElkondoHeavy{team}TommyTorsoS", definition.TorsoSpriteName);

        settings.ClassicVisualsEnabled = true;
        definition = renderer.GetWeaponRenderDefinitionProxy(player);
        Assert.Equal(torso ? "SmgS" : "RevolverS", definition.NormalSpriteName);
        Assert.Equal(torso ? "SmgFS" : "RevolverFS", definition.RecoilSpriteName);
        Assert.Equal(torso ? "SmgFRS" : "RevolverFRS", definition.ReloadSpriteName);
        Assert.False(definition.UseTorsoReplacement);
        Assert.False(definition.SingleTeamFrames);
    }

    [Fact]
    public void MinigunKeepsItsOwnSkinWhenTommyGunAlternativeExists()
    {
        var (_, player, renderer, _) = CreateGame("heavy", "weapon.minigun", PlayerTeam.Red);
        Assert.Equal("ElkondoHeavyRedWeaponS", renderer.GetWeaponRenderDefinitionProxy(player).NormalSpriteName);
    }

    [Theory]
    [InlineData("heavy", "weapon.tommy-gun", 8, 27)]
    [InlineData("spy", "weapon.revolver", 6, 11)]
    public void WeaponFramesUseAuthoredMillisecondsAndReloadNeverWraps(string classId, string itemId, int fireFrames, int reloadFrames)
    {
        var (game, player, renderer, _) = CreateGame(classId, itemId, PlayerTeam.Red);
        var definition = renderer.GetWeaponRenderDefinitionProxy(player);
        var state = game._playerRenderStates[player.Id];
        state.WeaponAnimationMode = Game1.WeaponAnimationMode.Recoil;
        state.WeaponAnimationDurationSeconds = definition.RecoilDurationSeconds;
        for (var index = 0; index < fireFrames; index++)
        {
            state.WeaponAnimationElapsedSeconds = index * 0.05f;
            Assert.Equal(index, renderer.GetWeaponSpriteFrameIndex(player, Game1.WeaponAnimationMode.Recoil, definition, fireFrames));
        }
        state.WeaponAnimationElapsedSeconds = fireFrames * 0.05f;
        Assert.Equal(classId == "heavy" ? 0 : fireFrames - 1,
            renderer.GetWeaponSpriteFrameIndex(player, Game1.WeaponAnimationMode.Recoil, definition, fireFrames));
        state.WeaponAnimationMode = Game1.WeaponAnimationMode.Reload;
        for (var index = 0; index < reloadFrames; index++)
        {
            state.WeaponAnimationElapsedSeconds = index * 0.065f;
            Assert.Equal(index, renderer.GetWeaponSpriteFrameIndex(player, Game1.WeaponAnimationMode.Reload, definition, reloadFrames));
        }
        state.WeaponAnimationElapsedSeconds = 5;
        Assert.Equal(reloadFrames - 1, renderer.GetWeaponSpriteFrameIndex(player, Game1.WeaponAnimationMode.Reload, definition, reloadFrames));
    }

    [Fact]
    public void HeldTommyFireRetainsElapsedAnimationAcrossShots()
    {
        var (game, player, renderer, _) = CreateGame("heavy", "weapon.tommy-gun", PlayerTeam.Red);
        var definition = renderer.GetWeaponRenderDefinitionProxy(player);
        Assert.True(definition.LoopRecoilWhileActive);
        var state = game._playerRenderStates[player.Id];
        Game1.StartWeaponAnimation(state, Game1.WeaponAnimationMode.Recoil, definition.RecoilDurationSeconds);
        state.WeaponAnimationElapsedSeconds = 0.25f;
        Game1.StartWeaponAnimation(state, Game1.WeaponAnimationMode.Recoil, definition.RecoilDurationSeconds,
            preserveElapsed: definition.LoopRecoilWhileActive);
        Assert.Equal(5, renderer.GetWeaponSpriteFrameIndex(player, Game1.WeaponAnimationMode.Recoil, definition, 8));
    }

    [Theory]
    [InlineData(PlayerTeam.Red, false, "AnimatedFlameRedS")]
    [InlineData(PlayerTeam.Blue, false, "AnimatedFlameBlueS")]
    [InlineData(PlayerTeam.Red, true, "AnimatedFlameCriticalRedS")]
    [InlineData(PlayerTeam.Blue, true, "AnimatedFlameCriticalBlueS")]
    public void EveryFlamePaletteHasTenCenteredFrames(PlayerTeam team, bool critical, string expected)
    {
        Assert.Equal(expected, AnimatedFlamePresentation.SpriteName(team, critical));
        var sprite = StockGameplayModCatalog.Definition.Assets.Sprites[expected];
        Assert.Equal(10, sprite.FramePaths.Count);
        Assert.Equal(12, sprite.OriginX);
        Assert.Equal(12, sprite.OriginY);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(120)]
    public void TravellingFlameStartsAtBirthAndRunsOnceRegardlessOfTickRate(int tickRate)
    {
        var total = tickRate / 2;
        var start = new FlameProjectileEntity(1, PlayerTeam.Red, 1, 0, 0, 1, 0, total);
        var middle = new FlameProjectileEntity(1, PlayerTeam.Red, 1, 0, 0, 1, 0, total / 2);
        var end = new FlameProjectileEntity(1, PlayerTeam.Red, 1, 0, 0, 1, 0, 0);
        Assert.Equal(0, AnimatedFlamePresentation.ProjectileFrame(start, 10, total, 0));
        Assert.InRange(AnimatedFlamePresentation.ProjectileFrame(middle, 10, total, 0.25), 5, 6);
        Assert.Equal(9, AnimatedFlamePresentation.ProjectileFrame(end, 10, total, 0.5));
        Assert.Equal(5, AnimatedFlamePresentation.LoopFrame(0.25, 10));
        Assert.Equal(0, AnimatedFlamePresentation.LoopFrame(0.5, 10));
    }

    [Theory]
    [InlineData("ElkondoSpyRedWeaponS", 1)]
    [InlineData("ElkondoSpyBlueFireS", 6)]
    [InlineData("ElkondoSpyRedReloadS", 11)]
    [InlineData("ElkondoHeavyBlueTommyGunS", 1)]
    [InlineData("ElkondoHeavyRedTommyFireS", 8)]
    [InlineData("ElkondoHeavyBlueTommyReloadS", 27)]
    public void AuthoredStripsHaveMatchingPixelPerfectRotations(string name, int frames)
    {
        var sprite = StockGameplayModCatalog.Definition.Assets.Sprites[name];
        Assert.Equal(frames, sprite.FramePaths.Count);
        Assert.Equal(32, sprite.OriginX);
        Assert.Equal(32, sprite.OriginY);
        using var manifest = JsonDocument.Parse(File.ReadAllText(ContentRoot.GetPath("Sprites", "WeaponsRotated", name, "manifest.json")));
        Assert.Equal(frames, manifest.RootElement.GetProperty("frameCount").GetInt32());
        Assert.Equal(24, manifest.RootElement.GetProperty("angleCount").GetInt32());
    }

    [Fact]
    public void DoubledCursorDefaultScalesTheTextureAndRetainsExplicitUserSizes()
    {
        Assert.Equal(200, new ClientSettings().CursorSizePercent);
        Assert.Equal(200, new HudRuntimeSettings().CursorSizePercent);
        Assert.Equal(2 * ClientSettings.GetCursorScale(100), ClientSettings.GetCursorScale(ClientSettings.DefaultCursorSizePercent));
        Assert.Equal(2, Game1.GetMenuCursorTextureScale(ClientSettings.DefaultCursorSizePercent));
        Assert.Equal(1, Game1.GetMenuCursorTextureScale(100));
        Assert.Equal(100, ClientSettings.NormalizeCursorSizePercent(new ClientSettings { CursorSizePercent = 100 }.CursorSizePercent));
    }

    private static (Game1 Game, PlayerEntity Player, GameplayWeaponRenderController Renderer, GameplayRuntimeSettings Settings)
        CreateGame(string classId, string itemId, PlayerTeam team)
    {
        var game = (Game1)RuntimeHelpers.GetUninitializedObject(typeof(Game1));
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        var player = world.LocalPlayer;
        player.SetClassDefinition(CharacterClassCatalog.RuntimeRegistry.CreateCharacterClassDefinition(classId));
        player.Spawn(team, 100, 100);
        Assert.True(player.TrySelectGameplayPrimaryItem(itemId));
        Set(game, "_gameplaySessionState", new Game1.GameplaySessionState());
        Set(game, "_world", world);
        Set(game, "_config", world.Config);
        Set(game, "_networkClient", new NetworkGameClient());
        Set(game, "_playerSkins", new Lazy<PlayerSkinCatalog>(PlayerSkinCatalog.Load));
        Set(game, "_playerRenderStates", new Dictionary<int, Game1.PlayerRenderState> { [player.Id] = new() });
        Set(game, "_backstabVisuals", new List<Game1.BackstabVisual>());
        var services = new ClientServiceContainer();
        Set(game, "_services", services);
        var gameplay = new GameplayManager((IGameplayContext)game);
        services.Register(gameplay);
        var renderer = new GameplayWeaponRenderController((IRenderContext)game);
        services.Register(renderer);
        return (game, player, renderer, gameplay.RuntimeSettings);
    }

    private static void Set(Game1 game, string name, object value) => typeof(Game1).GetField(name, Members)!.SetValue(game, value);
}
