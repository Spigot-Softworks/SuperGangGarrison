using System.Reflection;
using System.Runtime.CompilerServices;
using OpenGarrison.Client;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class NetworkPresentationOwnershipTests
{
    [Fact]
    public void NetworkPresentationIsStableAcrossContextsAndIsolatedBetweenGames()
    {
        var first = CreateGameWithGameplayManager();
        var second = CreateGameWithGameplayManager();

        var firstGameplayView = ((IGameplayContext)first.Game).NetworkPresentation;
        var firstRenderView = ((IRenderContext)first.Game).NetworkPresentation;
        var secondGameplayView = ((IGameplayContext)second.Game).NetworkPresentation;
        var secondRenderView = ((IRenderContext)second.Game).NetworkPresentation;

        Assert.Same(first.GameplayManager.NetworkPresentation, firstGameplayView);
        Assert.Same(firstGameplayView, firstRenderView);
        Assert.Same(firstGameplayView, ((IGameplayContext)first.Game).NetworkPresentation);
        Assert.Same(second.GameplayManager.NetworkPresentation, secondGameplayView);
        Assert.Same(secondGameplayView, secondRenderView);
        Assert.Same(secondGameplayView, ((IRenderContext)second.Game).NetworkPresentation);
        Assert.NotSame(firstGameplayView, secondGameplayView);
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
