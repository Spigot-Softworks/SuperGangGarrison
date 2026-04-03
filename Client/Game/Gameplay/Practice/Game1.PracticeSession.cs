#nullable enable

using Microsoft.Xna.Framework;
using OpenGarrison.BotAI;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private bool IsPracticeSessionActive => _gameplaySessionKind == GameplaySessionKind.Practice;

    private void TryStartPracticeFromSetup()
    {
        var selectedMap = GetSelectedPracticeMapEntry();
        if (selectedMap is null)
        {
            _menuStatusMessage = "Select a local map before starting Practice.";
            return;
        }

        BeginPracticeSession(selectedMap.LevelName);
    }

    private void RestartPracticeSession()
    {
        if (!IsPracticeSessionActive)
        {
            return;
        }

        var levelName = _world.Level.Name;
        _practiceMapEntries = BuildPracticeMapEntries();
        if (!SelectPracticeMapEntry(levelName))
        {
            NormalizePracticeSetupState();
            levelName = GetSelectedPracticeMapEntry()?.LevelName ?? levelName;
        }

        BeginPracticeSession(levelName);
    }

    private void BeginPracticeSession(string levelName)
    {
        if (!TryBeginOfflineBotSession(
                levelName,
                GameplaySessionKind.Practice,
                _practiceTickRate,
                GetPracticeExperimentalGameplaySettings(),
                _practiceTimeLimitMinutes,
                _practiceCapLimit,
                _practiceRespawnSeconds,
                openJoinMenus: true,
                consoleSessionName: "practice"))
        {
            return;
        }
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

        return IsPracticeSessionActive ? "Practice ended." : "Disconnected.";
    }

    private string GetOfflineSpectateUnavailableMessage()
    {
        if (IsLastToDieSessionActive)
        {
            return "Spectator mode is not available in Last To Die.";
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
