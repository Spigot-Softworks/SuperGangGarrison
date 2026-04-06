#nullable enable

using OpenGarrison.BotAI;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class OfflineSessionController
    {
        private readonly Game1 _game;
        private readonly GameplaySessionController _sessionController;

        public OfflineSessionController(Game1 game, GameplaySessionController sessionController)
        {
            _game = game;
            _sessionController = sessionController;
        }

        public void TryStartPracticeFromSetup()
        {
            var selectedMap = _game.GetSelectedPracticeMapEntry();
            if (selectedMap is null)
            {
                _game._menuStatusMessage = "Select a local map before starting Practice.";
                return;
            }

            BeginPracticeSession(selectedMap.LevelName);
        }

        public void RestartPracticeSession()
        {
            if (!_game.IsPracticeSessionActive)
            {
                return;
            }

            var levelName = _game._world.Level.Name;
            _game._practiceMapEntries = Game1.BuildPracticeMapEntries();
            if (!_game.SelectPracticeMapEntry(levelName))
            {
                _game.NormalizePracticeSetupState();
                levelName = _game.GetSelectedPracticeMapEntry()?.LevelName ?? levelName;
            }

            BeginPracticeSession(levelName);
        }

        public void BeginPracticeSession(string levelName)
        {
            _ = TryBeginOfflineBotSession(
                levelName,
                GameplaySessionKind.Practice,
                _game._practiceTickRate,
                _game.GetPracticeExperimentalGameplaySettings(),
                _game._practiceTimeLimitMinutes,
                _game._practiceCapLimit,
                _game._practiceRespawnSeconds,
                openJoinMenus: true,
                consoleSessionName: "practice");
        }

        public bool BeginLastToDieStage(string levelName)
        {
            if (_game._lastToDieRun is null)
            {
                return false;
            }

            var settings = Game1.BuildLastToDieExperimentalGameplaySettings(_game._lastToDieRun);
            if (!TryBeginOfflineBotSession(
                    levelName,
                    GameplaySessionKind.LastToDie,
                    _game._practiceTickRate,
                    settings,
                    LastToDieMatchTimeLimitMinutes,
                    LastToDieCapLimit,
                    LastToDieRespawnSeconds,
                    openJoinMenus: false,
                    consoleSessionName: "last to die"))
            {
                return false;
            }

            _game._lastToDiePerkMenuOpen = false;
            _game._lastToDiePerkHoverIndex = -1;
            _game._lastToDieStageClearOverlayOpen = false;
            _game._lastToDieStageClearOverlayTicks = 0;
            _game.ClearLastToDieDeathFocusPresentation();
            _game.ResetLastToDieBotReactionState();
            _game.ResetLastToDieCombatFeedbackPresentation();
            _game._lastToDieFailureOverlayOpen = false;
            _game._lastToDieFailureOverlayTicks = 0;

            _game._lastToDieRun.CurrentLevelName = levelName;
            _game.ApplySelectedLastToDieSurvivorToCurrentStage();
            _game.SyncPracticeBotRoster(PlayerTeam.Red);
            _game._world.DespawnEnemyDummy();
            _game._world.DespawnFriendlyDummy();
            _game.ObserveLastToDieBotReactionState();
            _game.ObserveLastToDieCombatFeedbackState();
            _game._lastToDieRun.ObservedStageKills = 0;

            _game._lastToDieRun.StageRemainingTicks = _game._lastToDieRun.StageDurationMinutes * 60 * _game._config.TicksPerSecond;
            _game._lastToDieRun.StageIntroTicksRemaining = _game.GetLastToDieStageIntroDurationTicks();

            _game.AddConsoleLine(
                $"last to die stage={_game._lastToDieRun.StageNumber} map={levelName} survivor={_game._lastToDieRun.SurvivorKind} enemies={_game._lastToDieRun.EnemyBotCount} minutes={_game._lastToDieRun.StageDurationMinutes}");
            return true;
        }

        public bool TryBeginOfflineBotSession(
            string levelName,
            GameplaySessionKind sessionKind,
            int tickRate,
            ExperimentalGameplaySettings experimentalSettings,
            int timeLimitMinutes,
            int capLimit,
            int respawnSeconds,
            bool openJoinMenus,
            string consoleSessionName)
        {
            if (sessionKind != GameplaySessionKind.LastToDie)
            {
                _game.ResetLastToDieState();
            }

            _game.ResetPracticeBotManagerState(releaseWorldSlots: true);
            _game.ResetPracticeNavigationState();
            _game._botDiagnosticLatestSnapshot = BotControllerDiagnosticsSnapshot.Empty;
            _game.ResetBotDiagnosticSample();
            _game._networkClient.Disconnect();
            _game._networkClient.ClearPendingTeamSelection();
            _game._networkClient.ClearPendingClassSelection();
            _game.StopHostedServer();
            _game.ResetGameplayTransitionEffects();
            _game.StopMenuMusic();
            _game.StopLastToDieMenuMusic();
            _game.StopFaucetMusic();
            _game.StopLastToDieIngameMusic();
            _game.StopLastToDieGameOverSound();

            _game.ReinitializeSimulationForTickRate(tickRate);
            _game.ResetGameplayRuntimeState();
            _game._world.ConfigureExperimentalGameplaySettings(experimentalSettings);
            _game._world.ConfigureMatchDefaults(
                timeLimitMinutes: timeLimitMinutes,
                capLimit: capLimit,
                respawnSeconds: respawnSeconds);
            if (!_game._world.TryLoadLevel(levelName))
            {
                _game._menuStatusMessage = $"Failed to load local map: {levelName}";
                return false;
            }

            _game._world.Level.ForcedBlockingTeamGates = sessionKind == GameplaySessionKind.LastToDie
                ? TeamGateLockMask.Red
                : TeamGateLockMask.None;

            _game._world.AutoRestartOnMapChange = sessionKind != GameplaySessionKind.LastToDie;
            _game.LoadPracticeNavigationAssetsForCurrentLevel();
            _sessionController.EnterGameplaySession(sessionKind, openJoinMenus, statusMessage: string.Empty);
            _game.InitializePracticeBotNamePoolForMatch();

            if (openJoinMenus)
            {
                _game._world.PrepareLocalPlayerJoin();
                _game.ApplyPracticeDummyPreferencesBeforeJoin();
            }

            _game.AddConsoleLine($"{consoleSessionName} started on {levelName} tickrate={tickRate}");
            return true;
        }
    }
}
