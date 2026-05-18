#nullable enable

using Microsoft.Xna.Framework;
using OpenGarrison.Core.BotBrain;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private bool IsPracticeSessionActive => _gameplaySessionKind == GameplaySessionKind.Practice;

    private void TryStartPracticeFromSetup()
    {
        _gameplaySessionController.TryStartPracticeFromSetup();
    }

    private void RestartPracticeSession()
    {
        _gameplaySessionController.RestartPracticeSession();
    }

    private void BeginPracticeSession(string levelName)
    {
        _gameplaySessionController.BeginPracticeSession(levelName);
    }

    private void ApplyPracticeTeamSelection(PlayerTeam localTeam)
    {
        if (!IsPracticeSessionActive)
        {
            return;
        }

        _world.DespawnEnemyDummy();
        SyncPracticeBotRoster(localTeam);
        _world.DespawnFriendlyDummy();
    }

    private void ApplyPracticeDummyPreferencesBeforeJoin()
    {
        if (!IsPracticeSessionActive)
        {
            return;
        }

        _world.DespawnEnemyDummy();
        _world.DespawnFriendlyDummy();
    }

    private void ApplyPracticeDummyPreferencesAfterJoin()
    {
        if (!IsPracticeSessionActive)
        {
            return;
        }

        SyncPracticeBotRoster(_world.LocalPlayerTeam);
        _world.DespawnEnemyDummy();
        _world.DespawnFriendlyDummy();
    }

    private string GetGameplayExitStatusMessage()
    {
        if (IsLastToDieSessionActive)
        {
            return "Last To Die ended.";
        }

        if (IsJumpSessionActive)
        {
            return "Jump ended.";
        }

        return IsPracticeSessionActive ? "Practice ended." : "Disconnected.";
    }

    private string GetOfflineSpectateUnavailableMessage()
    {
        if (IsLastToDieSessionActive)
        {
            return "Spectator mode is not available in Last To Die.";
        }

        if (IsJumpSessionActive)
        {
            return "Spectator mode is not available in Jump.";
        }

        return IsPracticeSessionActive
            ? "Spectator mode is not available in Practice."
            : "Spectator mode requires a network session.";
    }

    private static PlayerTeam GetOpposingTeam(PlayerTeam localTeam)
    {
        return localTeam == PlayerTeam.Blue ? PlayerTeam.Red : PlayerTeam.Blue;
    }
}
