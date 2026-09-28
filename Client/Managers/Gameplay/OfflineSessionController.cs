#nullable enable

using OpenGarrison.Core.BotBrain;
using OpenGarrison.Core;
using System.Diagnostics;

using static OpenGarrison.Client.Game1;

namespace OpenGarrison.Client;

public sealed class OfflineSessionController
    {
        private readonly IGameplayContext _context;

        public OfflineSessionController(IGameplayContext context)
        {
            _context = context;
        }

        public void TryStartPracticeFromSetup()
        {
            if (!_context.Gameplay.Bootstrap.CanEnterGameplaySession(out var bootstrapReason))
            {
                _context.SetPersistedMenuStatusMessage(bootstrapReason ?? "Browser client assets are still loading.");
                return;
            }

            var selectedMap = _context.GetSelectedPracticeMapEntry();
            if (selectedMap is null)
            {
                _context._menuStatusMessage = "Select a local map before starting Practice.";
                return;
            }

            BeginPracticeSession(selectedMap.LevelName);
        }

        public void RestartPracticeSession()
        {
            if (!_context.IsPracticeSessionActive)
            {
                return;
            }

            if (!_context.Gameplay.Bootstrap.CanEnterGameplaySession(out var bootstrapReason))
            {
                _context.SetPersistedMenuStatusMessage(bootstrapReason ?? "Browser client assets are still loading.");
                return;
            }

            var levelName = _context._world.Level.Name;
            _context._practiceMapEntries = Game1.BuildPracticeMapEntries();
            if (!_context.SelectPracticeMapEntry(levelName))
            {
                _context.NormalizePracticeSetupState();
                levelName = _context.GetSelectedPracticeMapEntry()?.LevelName ?? levelName;
            }

            BeginPracticeSession(levelName);
        }

        public void BeginPracticeSession(string levelName)
        {
            if (!_context.Gameplay.Bootstrap.CanEnterGameplaySession(out var bootstrapReason))
            {
                _context.SetPersistedMenuStatusMessage(bootstrapReason ?? "Browser client assets are still loading.");
                return;
            }

            _ = TryBeginOfflineBotSession(
                levelName,
                GameplaySessionKind.Practice,
                _context._practiceTickRate,
                _context.GetPracticeExperimentalGameplaySettings(),
                _context._practiceTimeLimitMinutes,
                _context._practiceCapLimit,
                _context._practiceRespawnSeconds,
                enableInstantRedTeamIntelCaptureWin: false,
                openJoinMenus: true,
                consoleSessionName: "practice");
        }

        public bool BeginLastToDieStage(string levelName)
        {
            if (_context._lastToDieRun is null)
            {
                return false;
            }

            var settings = Game1.BuildLastToDieExperimentalGameplaySettings(_context._lastToDieRun);
            var stageRules = Game1.ResolveLastToDieStageRuleProfile(_context._lastToDieRun.SurvivorKind, levelName);
            if (!TryBeginOfflineBotSession(
                    levelName,
                    GameplaySessionKind.LastToDie,
                    _context._practiceTickRate,
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

            _context._lastToDiePerkMenuOpen = false;
            _context._lastToDiePerkHoverIndex = -1;
            _context._lastToDieStageClearOverlayOpen = false;
            _context._lastToDieStageClearOverlayTicks = 0;
            _context.ClearLastToDieDeathFocusPresentation();
            _context.ResetLastToDieBotReactionState();
            _context.ResetLastToDieCombatFeedbackPresentation();
            _context._lastToDieFailureOverlayOpen = false;
            _context._lastToDieFailureOverlayTicks = 0;

            _context._lastToDieRun.CurrentLevelName = levelName;
            _context.ApplySelectedLastToDieSurvivorToCurrentStage();
            _context.SyncPracticeBotRoster(PlayerTeam.Red);
            _context.ApplyLastToDieStageEnemyModifiers();
            _context.SpawnLastToDieDroneSwarmForCurrentStage();
            _context._world.DespawnEnemyDummy();
            _context._world.DespawnFriendlyDummy();
            _context.ObserveLastToDieBotReactionState();
            _context.ObserveLastToDieCombatFeedbackState();
            _context._lastToDieRun.ObservedStageKills = 0;
            _context._lastToDieRun.ObservedStageHealingReceived = 0;

            _context._lastToDieRun.StageRemainingTicks = _context._lastToDieRun.StageDurationMinutes * 60 * _context._config.TicksPerSecond;
            _context._lastToDieRun.StageIntroTicksRemaining = _context.GetLastToDieStageIntroDurationTicks();

            _context.AddConsoleLine(
                $"last to die stage={_context._lastToDieRun.StageNumber} map={levelName} survivor={_context._lastToDieRun.SurvivorKind} difficulty={_context._lastToDieRun.Difficulty} special={_context._lastToDieRun.CurrentSpecialRound.Kind} enemies={_context._lastToDieRun.EnemyBotCount} minutes={_context._lastToDieRun.StageDurationMinutes}");
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
                if (!OperatingSystem.IsBrowser() && !_context.IsClientPerformanceDiagnosticsEnabled())
                {
                    return;
                }

                var now = Stopwatch.GetTimestamp();
                var stepMilliseconds = (now - stepStartTimestamp) * 1000d / Stopwatch.Frequency;
                var totalMilliseconds = (now - sessionStartTimestamp) * 1000d / Stopwatch.Frequency;
                if (OperatingSystem.IsBrowser())
                {
                    Console.WriteLine($"Browser practice startup {label}: step={stepMilliseconds:0.0}ms total={totalMilliseconds:0.0}ms");
                }
                else
                {
                    _context.LogClientPerformanceLine(
                        $"event=client_perf_startup_step label={label} stepMs={stepMilliseconds:0.0} totalMs={totalMilliseconds:0.0}");
                }

                stepStartTimestamp = now;
            }

            if (!_context.Gameplay.Bootstrap.CanEnterGameplaySession(out var bootstrapReason))
            {
                _context.SetPersistedMenuStatusMessage(bootstrapReason ?? "Browser client assets are still loading.");
                return false;
            }
            LogBrowserPracticeStartupStep("bootstrap-ready");

            if (sessionKind != GameplaySessionKind.LastToDie)
            {
                _context.ResetLastToDieState();
            }
            LogBrowserPracticeStartupStep("reset-last-to-die");

            _context.ClearManualPracticeBotRequests();
            _context.ResetPracticeBotManagerState(releaseWorldSlots: true);
            Game1.ResetPracticeNavigationState();
            _context._botDiagnosticLatestSnapshot = BotControllerDiagnosticsSnapshot.Empty;
            _context.ResetBotDiagnosticSample();
            _context._networkClient.Disconnect();
            _context.LeaveManagedRoom();
            _context._networkClient.ClearPendingTeamSelection();
            _context._networkClient.ClearPendingClassSelection();
            _context.StopHostedServer();
            _context.ResetGameplayTransitionEffects();
            _context.StopMenuMusic();
            _context.StopLastToDieMenuMusic();
            _context.StopFaucetMusic();
            _context.StopLastToDieIngameMusic();
            _context.StopLastToDieGameOverSound();
            LogBrowserPracticeStartupStep("reset-runtime");

            _context.ReinitializeSimulationForTickRate(tickRate);
            _context.ResetGameplayRuntimeState();
            _context._world.ConfigureExperimentalGameplaySettings(experimentalSettings);
            _context._world.ConfigureSpecialCaptureTheFlagRules(enableInstantRedTeamIntelCaptureWin);
            _context._world.ConfigureMatchDefaults(
                timeLimitMinutes: timeLimitMinutes,
                capLimit: capLimit,
                respawnSeconds: respawnSeconds);
            if (sessionKind == GameplaySessionKind.Practice)
            {
                _context.ResetPracticeRoundPoints();
            }
            LogBrowserPracticeStartupStep("configure-world");

            if (!_context._world.TryLoadLevel(levelName))
            {
                _context._menuStatusMessage = $"Failed to load local map: {levelName}";
                return false;
            }
            LogBrowserPracticeStartupStep("load-level");

            var forcedBlockingTeamGates = TeamGateLockMask.None;
            if (sessionKind == GameplaySessionKind.LastToDie && _context._lastToDieRun is not null)
            {
                forcedBlockingTeamGates = Game1.ResolveLastToDieStageRuleProfile(_context._lastToDieRun.SurvivorKind, levelName).ForcedBlockingTeamGates;
            }

            _context._world.Level.ForcedBlockingTeamGates = forcedBlockingTeamGates;

            _context._world.AutoRestartOnMapChange = sessionKind != GameplaySessionKind.LastToDie
                && sessionKind != GameplaySessionKind.Jump;
            LogBrowserPracticeStartupStep("configure-level");
            _context.Gameplay.Session.EnterGameplaySession(sessionKind, openJoinMenus, statusMessage: string.Empty);
            LogBrowserPracticeStartupStep("enter-session");
            // The offline bot counts are finalized as part of entering the
            // session. Warm the shared alpha routes after that point so the
            // first bot Think cannot pay a full spawn-to-objective A* search.
            _context.InitializePracticeBotNamePoolForMatch();
            LogBrowserPracticeStartupStep("bot-names");

            if (openJoinMenus)
            {
                _context._world.PrepareLocalPlayerJoin();
                _context.ApplyPracticeDummyPreferencesBeforeJoin();
                LogBrowserPracticeStartupStep("prepare-local-join");
            }

            // Queue the graph warmup after all callers have finished their
            // synchronous session setup. The next gameplay update starts the
            // background task while the reusable loading overlay is visible,
            // and gameplay remains paused until it has completed.
            _context.QueuePracticeNavigationWarmupForCurrentLevel();
            LogBrowserPracticeStartupStep("queue-navigation");
            _context.AddConsoleLine($"{consoleSessionName} started on {levelName} tickrate={tickRate}");
            LogBrowserPracticeStartupStep("complete");
            return true;
        }
}
