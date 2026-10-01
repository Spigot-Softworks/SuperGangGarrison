using System.Reflection;
using System.Runtime.CompilerServices;
using OpenGarrison.Client;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class MenuAssetResourcesOwnershipTests
{
    [Fact]
    public void AssetResourcesAreStableAcrossContextsAndIsolatedBetweenGames()
    {
        var first = CreateGameWithGameplayManager();
        var second = CreateGameWithGameplayManager();

        var firstGameplayContext = (IGameplayContext)first.Game;
        var firstMenuContext = (IMenuContext)first.Game;
        var secondGameplayContext = (IGameplayContext)second.Game;
        var secondMenuContext = (IMenuContext)second.Game;

        var firstLoadout = firstGameplayContext.GameplayLoadoutResources;
        var firstMenu = firstGameplayContext.MenuResources;
        var secondLoadout = secondGameplayContext.GameplayLoadoutResources;
        var secondMenu = secondGameplayContext.MenuResources;

        Assert.Same(first.GameplayManager.Bootstrap.GameplayLoadoutResources, firstLoadout);
        Assert.Same(first.GameplayManager.Bootstrap.MenuResources, firstMenu);
        Assert.Same(firstMenu, firstMenuContext.MenuResources);
        Assert.Same(firstMenu, firstGameplayContext.MenuResources);
        Assert.Same(firstLoadout, firstGameplayContext.GameplayLoadoutResources);
        Assert.Same(second.GameplayManager.Bootstrap.GameplayLoadoutResources, secondLoadout);
        Assert.Same(second.GameplayManager.Bootstrap.MenuResources, secondMenu);
        Assert.Same(secondMenu, secondMenuContext.MenuResources);
        Assert.Same(secondMenu, secondGameplayContext.MenuResources);
        Assert.Same(secondLoadout, secondGameplayContext.GameplayLoadoutResources);
        Assert.NotSame(firstLoadout, secondLoadout);
        Assert.NotSame(firstMenu, secondMenu);

        Assert.Null(firstLoadout.ClassStripTexture);
        Assert.Empty(firstLoadout.SelectionAtlasChunks);
        Assert.Same(firstLoadout.SelectionAtlasChunks, firstLoadout.SelectionAtlasChunks);
        Assert.Null(firstMenu.BackgroundTexture);
        Assert.Null(firstMenu.BackgroundTexturePath);
        Assert.Equal(0, firstMenu.ImageFrame);
        Assert.Null(firstMenu.BitmapFontTexture);
        Assert.Empty(firstMenu.BitmapFontGlyphs);
        Assert.Same(firstMenu.BitmapFontGlyphs, firstMenu.BitmapFontGlyphs);
        Assert.Equal(0, firstMenu.BitmapFontLineHeight);
        Assert.Equal(1, firstMenu.BitmapFontSpacing);
        Assert.NotSame(firstLoadout.SelectionAtlasChunks, secondLoadout.SelectionAtlasChunks);
        Assert.NotSame(firstMenu.BitmapFontGlyphs, secondMenu.BitmapFontGlyphs);
    }

    private static (Game1 Game, GameplayManager GameplayManager) CreateGameWithGameplayManager()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var game = (Game1)RuntimeHelpers.GetUninitializedObject(typeof(Game1));
        var services = new ClientServiceContainer();
        typeof(Game1).GetField("_services", flags)!.SetValue(game, services);
        var gameplayManager = new GameplayManager(game);
        services.Register(gameplayManager);
        return (game, gameplayManager);
    }
}
