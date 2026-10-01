using System.Reflection;
using System.Runtime.CompilerServices;
using OpenGarrison.Client;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class GameplayTransitionStateOwnershipTests
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [Fact]
    public void TransitionStateHasStableManagerOwnershipAndPerGameIsolation()
    {
        var first = CreateGameWithGameplayManager();
        var second = CreateGameWithGameplayManager();

        var firstGameplay = (IGameplayContext)first.Game;
        var firstSession = (ISessionContext)first.Game;
        var firstHosting = (IHostingContext)first.Game;
        var secondGameplay = (IGameplayContext)second.Game;
        var secondSession = (ISessionContext)second.Game;
        var secondHosting = (IHostingContext)second.Game;

        var teamSelection = firstGameplay.TeamClassSelection;
        var sessionTransitions = firstGameplay.SessionTransitions;
        var pendingHostedConnect = firstHosting.PendingHostedConnect;
        var loadingOverlay = first.GameplayManager.Frame.LoadingOverlay;

        Assert.Same(first.GameplayManager.OverlayState.TeamClassSelection, teamSelection);
        Assert.Same(teamSelection, firstSession.TeamClassSelection);
        Assert.Same(teamSelection, firstGameplay.TeamClassSelection);
        Assert.Same(first.GameplayManager.SessionState.SessionTransitions, sessionTransitions);
        Assert.Same(sessionTransitions, firstSession.SessionTransitions);
        Assert.Same(sessionTransitions, firstGameplay.SessionTransitions);
        Assert.Same(first.GameplayManager.SessionState.PendingHostedConnect, pendingHostedConnect);
        Assert.Same(pendingHostedConnect, firstHosting.PendingHostedConnect);

        Assert.False(teamSelection.TeamSelectOpen);
        Assert.Equal(0.01f, teamSelection.TeamSelectAlpha);
        Assert.Equal(-120f, teamSelection.TeamSelectPanelY);
        Assert.Equal(-1, teamSelection.TeamSelectHoverIndex);
        Assert.Null(teamSelection.PendingClassSelectTeam);
        Assert.False(teamSelection.ClassSelectOpen);
        Assert.Equal(0.01f, teamSelection.ClassSelectAlpha);
        Assert.Equal(-120f, teamSelection.ClassSelectPanelY);
        Assert.Equal(-1, teamSelection.ClassSelectHoverIndex);
        Assert.Equal(-1, teamSelection.ClassSelectPortraitAnimationHoverIndex);
        Assert.Null(teamSelection.ClassSelectPortraitAnimationTeam);
        Assert.Equal(0f, teamSelection.ClassSelectPortraitAnimationFrame);
        Assert.False(teamSelection.PendingMapTeamSelection);
        Assert.False(sessionTransitions.LastToDieConnectionPresentationPending);
        Assert.Equal(-1, pendingHostedConnect.Ticks);
        Assert.Equal(OpenGarrisonPreferencesDocument.DefaultServerPort, pendingHostedConnect.Port);
        Assert.False(loadingOverlay.Visible);
        Assert.False(loadingOverlay.IsJoining);
        Assert.Equal(string.Empty, loadingOverlay.Message);
        Assert.Equal(string.Empty, loadingOverlay.JoiningServerLabel);
        Assert.Null(loadingOverlay.Progress);
        Assert.False(loadingOverlay.BrowserProgressVisible);

        Assert.Same(second.GameplayManager.OverlayState.TeamClassSelection, secondGameplay.TeamClassSelection);
        Assert.Same(second.GameplayManager.SessionState.SessionTransitions, secondSession.SessionTransitions);
        Assert.Same(second.GameplayManager.SessionState.PendingHostedConnect, secondHosting.PendingHostedConnect);
        Assert.NotSame(teamSelection, secondGameplay.TeamClassSelection);
        Assert.NotSame(sessionTransitions, secondSession.SessionTransitions);
        Assert.NotSame(pendingHostedConnect, secondHosting.PendingHostedConnect);
        Assert.NotSame(loadingOverlay, second.GameplayManager.Frame.LoadingOverlay);

        firstSession.TeamClassSelection.TeamSelectOpen = true;
        firstGameplay.TeamClassSelection.PendingMapTeamSelection = true;
        firstSession.SessionTransitions.LastToDieConnectionPresentationPending = true;
        firstHosting.PendingHostedConnect.Ticks = 4;
        first.GameplayManager.Frame.LoadingOverlay.Visible = true;

        Assert.True(first.GameplayManager.OverlayState.TeamClassSelection.TeamSelectOpen);
        Assert.True(first.GameplayManager.OverlayState.TeamClassSelection.PendingMapTeamSelection);
        Assert.True(first.GameplayManager.SessionState.SessionTransitions.LastToDieConnectionPresentationPending);
        Assert.Equal(4, first.GameplayManager.SessionState.PendingHostedConnect.Ticks);
        Assert.True(first.GameplayManager.Frame.LoadingOverlay.Visible);
        Assert.False(secondGameplay.TeamClassSelection.TeamSelectOpen);
        Assert.False(secondGameplay.TeamClassSelection.PendingMapTeamSelection);
        Assert.False(secondSession.SessionTransitions.LastToDieConnectionPresentationPending);
        Assert.Equal(-1, secondHosting.PendingHostedConnect.Ticks);
        Assert.False(second.GameplayManager.Frame.LoadingOverlay.Visible);
    }

    private static (Game1 Game, GameplayManager GameplayManager) CreateGameWithGameplayManager()
    {
        var game = (Game1)RuntimeHelpers.GetUninitializedObject(typeof(Game1));
        var services = new ClientServiceContainer();
        typeof(Game1).GetField("_services", Instance)!.SetValue(game, services);
        var gameplayManager = new GameplayManager((IGameplayContext)game);
        services.Register(gameplayManager);
        return (game, gameplayManager);
    }
}
