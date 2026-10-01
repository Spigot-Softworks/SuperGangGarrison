using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using OpenGarrison.Client;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class GameplayResourceOwnershipTests
{
    [Fact]
    public void BootstrapResourcesAreStableAndIsolatedPerGame()
    {
        var first = CreateGameWithGameplayManager();
        var second = CreateGameWithGameplayManager();

        var firstLevel = first.Manager.Bootstrap.LevelBackgroundResources;
        var firstLogo = first.Manager.Bootstrap.LastToDieLogoResources;
        var firstBrowser = first.Manager.Bootstrap.BrowserBootstrapResources;
        var firstCaches = first.Manager.Bootstrap.SpriteFrameCacheResources;
        var firstTargets = first.Manager.Bootstrap.RenderTargetResources;
        var secondLevel = second.Manager.Bootstrap.LevelBackgroundResources;
        var secondLogo = second.Manager.Bootstrap.LastToDieLogoResources;
        var secondBrowser = second.Manager.Bootstrap.BrowserBootstrapResources;
        var secondCaches = second.Manager.Bootstrap.SpriteFrameCacheResources;
        var secondTargets = second.Manager.Bootstrap.RenderTargetResources;

        AssertAlias(first.Game, "_levelBackgroundResources", firstLevel);
        AssertAlias(first.Game, "_lastToDieLogoResources", firstLogo);
        AssertAlias(first.Game, "_browserBootstrapResources", firstBrowser);
        AssertAlias(first.Game, "_spriteFrameCacheResources", firstCaches);
        AssertAlias(first.Game, "_renderTargetResources", firstTargets);
        AssertAlias(second.Game, "_levelBackgroundResources", secondLevel);
        AssertAlias(second.Game, "_lastToDieLogoResources", secondLogo);
        AssertAlias(second.Game, "_browserBootstrapResources", secondBrowser);
        AssertAlias(second.Game, "_spriteFrameCacheResources", secondCaches);
        AssertAlias(second.Game, "_renderTargetResources", secondTargets);

        Assert.NotSame(firstLevel, secondLevel);
        Assert.NotSame(firstLogo, secondLogo);
        Assert.NotSame(firstBrowser, secondBrowser);
        Assert.NotSame(firstCaches, secondCaches);
        Assert.NotSame(firstTargets, secondTargets);

        Assert.Null(firstLevel.Texture);
        Assert.Null(firstLevel.TexturePath);
        Assert.Null(firstLevel.FailedPath);
        Assert.Null(firstLevel.TextureLevel);
        Assert.Null(firstLogo.Texture);
        Assert.Null(firstLogo.TexturePath);
        Assert.Null(firstBrowser.AssetsTask);
        Assert.Null(firstBrowser.Assets);
        Assert.False(firstBrowser.AssetsApplied);
        Assert.Null(firstBrowser.AtlasTextureCache);
        Assert.Null(firstBrowser.AtlasResolver);
        Assert.Null(firstTargets.GameRenderTarget);
        Assert.Null(firstTargets.HudRenderTarget);
        Assert.Null(firstTargets.DeathCamCaptureTarget);
        Assert.False(firstTargets.DeathCamCaptureValid);

        var firstNeutralCache = firstCaches.NeutralSpriteFrameCache;
        var firstOpaqueBoundsCache = firstCaches.SpriteFontOpaqueBoundsCache;
        var secondNeutralCache = secondCaches.NeutralSpriteFrameCache;
        var secondOpaqueBoundsCache = secondCaches.SpriteFontOpaqueBoundsCache;

        Assert.Same(firstNeutralCache, first.Manager.Bootstrap.SpriteFrameCacheResources.NeutralSpriteFrameCache);
        Assert.Same(firstOpaqueBoundsCache, first.Manager.Bootstrap.SpriteFrameCacheResources.SpriteFontOpaqueBoundsCache);
        Assert.Same(secondNeutralCache, second.Manager.Bootstrap.SpriteFrameCacheResources.NeutralSpriteFrameCache);
        Assert.Same(secondOpaqueBoundsCache, second.Manager.Bootstrap.SpriteFrameCacheResources.SpriteFontOpaqueBoundsCache);
        Assert.NotSame(firstNeutralCache, secondNeutralCache);
        Assert.NotSame(firstOpaqueBoundsCache, secondOpaqueBoundsCache);
        Assert.Empty(firstNeutralCache);
        Assert.Empty(firstOpaqueBoundsCache);
        Assert.Empty(secondNeutralCache);
        Assert.Empty(secondOpaqueBoundsCache);
        Assert.Same(EqualityComparer<LoadedSpriteFrame>.Default, firstNeutralCache.Comparer);
        Assert.Same(EqualityComparer<LoadedSpriteFrame>.Default, firstOpaqueBoundsCache.Comparer);
        Assert.Same(EqualityComparer<LoadedSpriteFrame>.Default, secondNeutralCache.Comparer);
        Assert.Same(EqualityComparer<LoadedSpriteFrame>.Default, secondOpaqueBoundsCache.Comparer);
    }

    private static void AssertAlias(Game1 game, string name, object expected)
    {
        var property = typeof(Game1).GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(property);
        Assert.NotNull(property!.GetMethod);
        Assert.True(property.GetMethod!.IsPrivate);
        Assert.Same(expected, property.GetValue(game));
        Assert.Same(expected, property.GetValue(game));
    }

    private static (Game1 Game, GameplayManager Manager) CreateGameWithGameplayManager()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var game = (Game1)RuntimeHelpers.GetUninitializedObject(typeof(Game1));
        var services = new ClientServiceContainer();
        typeof(Game1).GetField("_services", flags)!.SetValue(game, services);
        var manager = new GameplayManager(game);
        services.Register(manager);
        return (game, manager);
    }
}
