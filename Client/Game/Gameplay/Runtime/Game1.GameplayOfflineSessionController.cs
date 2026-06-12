#nullable enable

using OpenGarrison.Core.BotBrain;
using OpenGarrison.Core;
using System.Diagnostics;

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
            if (!_game._bootstrapController.CanEnterGameplaySession(out var bootstrapReason))
            {
                _game.SetPersistedMenuStatusMessage(bootstrapReason ?? "Browser client assets are still loading.");
                return;
            }

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

            if (!_game._bootstrapController.CanEnterGameplaySession(out var bootstrapReason))
            {
                _game.SetPersistedMenuStatusMessage(bootstrapReason ?? "Browser client assets are still loading.");
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
            if (!_game._bootstrapController.CanEnterGameplaySession(out var bootstrapReason))
            {
                _game.SetPersistedMenuStatusMessage(bootstrapReason ?? "Browser client assets are still loading.");
                return;
            }

            _ = TryBeginOfflineBotSession(
                levelName,
                GameplaySessionKind.Practice,
                _game._practiceTickRate,
                _game.GetPracticeExperimentalGameplaySettings(),
                _game._practiceTimeLimitMinutes,
                _game._practiceCapLimit,
                _game._practiceRespawnSeconds,
                enableInstantRedTeamIntelCaptureWin: false,
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
            var stageRules = Game1.ResolveLastToDieStageRuleProfile(_game._lastToDieRun.SurvivorKind, levelName);
            if (!TryBeginOfflineBotSession(
                    levelName,
                    GameplaySessionKind.LastToDie,
                    _game._practiceTickRate,
                    settings,
                    LastToDieMatchTimeLimitMinutes,
                    stageRules.CapLimit,
                    LastToDieRespawnSeconds,
                    stageRules.EndMatchOnRedTeamIntelCapture,
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
            _game.ApplyLastToDieStageEnemyModifiers();
            _game.SpawnLastToDieDroneSwarmForCurrentStage();
            _game._world.DespawnEnemyDummy();
            _game._world.DespawnFriendlyDummy();
            _game.ObserveLastToDieBotReactionState();
            _game.ObserveLastToDieCombatFeedbackState();
            _game._lastToDieRun.ObservedStageKills = 0;
            _game._lastToDieRun.ObservedStageHealingReceived = 0;

            _game._lastToDieRun.StageRemainingTicks = _game._lastToDieRun.StageDurationMinutes * 60 * _game._config.TicksPerSecond;
            _game._lastToDieRun.StageIntroTicksRemaining = _game.GetLastToDieStageIntroDurationTicks();

            _game.AddConsoleLine(
                $"last to die stage={_game._lastToDieRun.StageNumber} map={levelName} survivor={_game._lastToDieRun.SurvivorKind} difficulty={_game._lastToDieRun.Difficulty} special={_game._lastToDieRun.CurrentSpecialRound.Kind} enemies={_game._lastToDieRun.EnemyBotCount} minutes={_game._lastToDieRun.StageDurationMinutes}");
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
            bool enableInstantRedTeamIntelCaptureWin,
            bool openJoinMenus,
            string consoleSessionName)
        {
            var sessionStartTimestamp = Stopwatch.GetTimestamp();
            var stepStartTimestamp = sessionStartTimestamp;
            void LogBrowserPracticeStartupStep(string label)
            {
                if (!OperatingSystem.IsBrowser())
                {
                    return;
                }

                var now = Stopwatch.GetTimestamp();
                var stepMilliseconds = (now - stepStartTimestamp) * 1000d / Stopwatch.Frequency;
                var totalMilliseconds = (now - sessionStartTimestamp) * 1000d / Stopwatch.Frequency;
                Console.WriteLine($"Browser practice startup {label}: step={stepMilliseconds:0.0}ms total={totalMilliseconds:0.0}ms");
                stepStartTimestamp = now;
            }

            if (!_game._bootstrapController.CanEnterGameplaySession(out var bootstrapReason))
            {
                _game.SetPersistedMenuStatusMessage(bootstrapReason ?? "Browser client assets are still loading.");
                return false;
            }
            LogBrowserPracticeStartupStep("bootstrap-ready");

            if (sessionKind != GameplaySessionKind.LastToDie)
            {
                _game.ResetLastToDieState();
            }
            LogBrowserPracticeStartupStep("reset-last-to-die");

            _game.ClearManualPracticeBotRequests();
            _game.ResetPracticeBotManagerState(releaseWorldSlots: true);
            Game1.ResetPracticeNavigationState();
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
            LogBrowserPracticeStartupStep("reset-runtime");

            _game.ReinitializeSimulationForTickRate(tickRate);
            _game.ResetGameplayRuntimeState();
            _game._world.ConfigureExperimentalGameplaySettings(experimentalSettings);
            _game._world.ConfigureSpecialCaptureTheFlagRules(enableInstantRedTeamIntelCaptureWin);
            _game._world.ConfigureMatchDefaults(
                timeLimitMinutes: timeLimitMinutes,
                capLimit: capLimit,
                respawnSeconds: respawnSeconds);
            LogBrowserPracticeStartupStep("configure-world");

            if (!_game._world.TryLoadLevel(levelName))
            {
                _game._menuStatusMessage = $"Failed to load local map: {levelName}";
                return false;
            }
            LogBrowserPracticeStartupStep("load-level");

            _game._world.ConfigurePracticeVipRules(
                sessionKind == GameplaySessionKind.Practice
                && _game._practiceVipRulesEnabled
                && _game._world.MatchRules.Mode == GameModeKind.ControlPoint);

            var forcedBlockingTeamGates = TeamGateLockMask.None;
            if (sessionKind == GameplaySessionKind.LastToDie && _game._lastToDieRun is not null)
            {
                forcedBlockingTeamGates = Game1.ResolveLastToDieStageRuleProfile(_game._lastToDieRun.SurvivorKind, levelName).ForcedBlockingTeamGates;
            }

            _game._world.Level.ForcedBlockingTeamGates = forcedBlockingTeamGates;

            _game._world.AutoRestartOnMapChange = sessionKind != GameplaySessionKind.LastToDie
                && sessionKind != GameplaySessionKind.Jump;
            LogBrowserPracticeStartupStep("configure-level");
            _game.LoadPracticeNavigationAssetsForCurrentLevel();
            LogBrowserPracticeStartupStep("load-navigation");
            _sessionController.EnterGameplaySession(sessionKind, openJoinMenus, statusMessage: string.Empty);
            LogBrowserPracticeStartupStep("enter-session");
            _game.InitializePracticeBotNamePoolForMatch();
            LogBrowserPracticeStartupStep("bot-names");

            if (openJoinMenus)
            {
                _game._world.PrepareLocalPlayerJoin();
                _game.ApplyPracticeDummyPreferencesBeforeJoin();
                LogBrowserPracticeStartupStep("prepare-local-join");
            }

            _game.AddConsoleLine($"{consoleSessionName} started on {levelName} tickrate={tickRate}");
            LogBrowserPracticeStartupStep("complete");
            return true;
        }
    }
}
