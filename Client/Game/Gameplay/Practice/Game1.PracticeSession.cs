#nullable enable

using Microsoft.Xna.Framework;
using OpenGarrison.Core.BotBrain;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private int _practiceRedRoundPoints;
    private int _practiceBlueRoundPoints;

    private bool IsPracticeSessionActive => _gameplaySessionKind == GameplaySessionKind.Practice;

    private void ResetPracticeRoundPoints()
    {
        _practiceRedRoundPoints = 0;
        _practiceBlueRoundPoints = 0;
    }

    private void ObservePracticeRoundCompletion()
    {
        if (!IsPracticeSessionActive
            || _wasMatchEnded
            || !_world.MatchState.IsEnded)
        {
            return;
        }

        if (_world.MatchState.WinnerTeam == PlayerTeam.Red)
        {
            _practiceRedRoundPoints += 1;
        }
        else if (_world.MatchState.WinnerTeam == PlayerTeam.Blue)
        {
            _practiceBlueRoundPoints += 1;
        }
    }

    private int GetPracticeRoundPoints(PlayerTeam team)
    {
        return team == PlayerTeam.Red ? _practiceRedRoundPoints : _practiceBlueRoundPoints;
    }

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

        return "Spectator mode requires a network session.";
    }

    private static PlayerTeam GetOpposingTeam(PlayerTeam localTeam)
    {
        return localTeam == PlayerTeam.Blue ? PlayerTeam.Red : PlayerTeam.Blue;
    }
}
