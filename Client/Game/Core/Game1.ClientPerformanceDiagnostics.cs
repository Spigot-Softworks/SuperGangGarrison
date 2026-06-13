#nullable enable

using Microsoft.Xna.Framework;
using OpenGarrison.Core.BotBrain;
using OpenGarrison.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const string ClientPerformanceLogEnvironmentVariable = "OG_CLIENT_PERF_LOG";
    private const string ClientPerformanceTestEnvironmentVariable = "OG_CLIENT_PERF_TEST";
    private const string ClientPerformanceModeEnvironmentVariable = "OG_CLIENT_PERF_MODE";
    private const string ClientPerformanceMapEnvironmentVariable = "OG_CLIENT_PERF_MAP";
    private const string ClientPerformanceFriendlyBotsEnvironmentVariable = "OG_CLIENT_PERF_FRIENDLY_BOTS";
    private const string ClientPerformanceEnemyBotsEnvironmentVariable = "OG_CLIENT_PERF_ENEMY_BOTS";
    private const string ClientPerformanceClassEnvironmentVariable = "OG_CLIENT_PERF_CLASS";
    private const string ClientPerformanceBotClassEnvironmentVariable = "OG_CLIENT_PERF_BOT_CLASS";
    private const string ClientPerformanceInputEnvironmentVariable = "OG_CLIENT_PERF_INPUT";
    private const string ClientPerformanceWarmupSecondsEnvironmentVariable = "OG_CLIENT_PERF_WARMUP_SECONDS";
    private const string ClientPerformanceMeasureSecondsEnvironmentVariable = "OG_CLIENT_PERF_MEASURE_SECONDS";
    private const string ClientPerformanceAutoExitEnvironmentVariable = "OG_CLIENT_PERF_AUTO_EXIT";
    private const string ClientPerformanceLastToDieSurvivorEnvironmentVariable = "OG_CLIENT_PERF_LTD_SURVIVOR";
    private const string ClientPerformanceLogFilePrefix = "client-perf";
    private const double ClientPerformanceSummaryIntervalSeconds = 1d;
    private const int ClientPerformanceDefaultFriendlyBots = 3;
    private const int ClientPerformanceDefaultEnemyBots = 3;
    private const int ClientPerformanceDefaultWarmupSeconds = 5;
    private const int ClientPerformanceDefaultMeasureSeconds = 30;
    private const int ClientPerformanceBotMovementThresholdPixels = 16;
    private const int ClientPerformanceBotRequiredDisplacementPixels = 96;
    private const int ClientPerformanceBotObjectiveImprovementThresholdPixels = 64;
    private const int ClientPerformanceBotMovementMaxTicks = 120;
    private const int ClientPerformanceBotRouteMaxTicks = 900;
    private const string ClientPerformanceDefaultMap = "Valley";
    private const string ClientPerformanceDefaultMode = "practice";
    private const string ClientPerformanceDefaultLastToDieSurvivor = "soldier";

    private static readonly bool ClientPerformanceLoggingEnabled = GetClientPerformanceEnvironmentFlag(ClientPerformanceLogEnvironmentVariable);
    private static readonly bool ClientPerformanceTestEnabled = GetClientPerformanceEnvironmentFlag(ClientPerformanceTestEnvironmentVariable);
    private readonly ClientPerformanceAccumulator _clientPerformance = new();
    private string? _clientPerformanceLogPath;
    private bool _clientPerformanceDiagnosticsInitialized;
    private double _clientPerformanceSummaryElapsedSeconds;
    private long _clientPerformanceLastFrameTimestamp;
    private bool _clientPerformanceTestSessionRequested;
    private bool _clientPerformanceTestMeasurementStarted;
    private bool _clientPerformanceTestMeasurementCompleted;
    private double _clientPerformanceTestMeasuredSeconds;
    private int _clientPerformanceTestSummarySamples;
    private double _clientPerformanceTestUpdateFpsTotal;
    private double _clientPerformanceTestDrawFpsTotal;
    private double _clientPerformanceTestMinUpdateFps = double.MaxValue;
    private double _clientPerformanceTestMinDrawFps = double.MaxValue;
    private readonly Dictionary<byte, ClientPerformanceBotBehaviorSlot> _clientPerformanceBotBehaviorSlots = new();
    private bool _clientPerformanceBotBehaviorActive;
    private int _clientPerformanceBotBehaviorInitialRedCaps;
    private int _clientPerformanceBotBehaviorInitialBlueCaps;
    private int _clientPerformanceBotBehaviorFirstPickupTick = -1;
    private int _clientPerformanceBotBehaviorFirstScoreTick = -1;
    private int _clientPerformanceBotBehaviorFirstControlTick = -1;
    private int _clientPerformanceBotBehaviorFirstRouteTick = -1;
    private int _clientPerformanceBotBehaviorPendingRouteTicks;
    private string _clientPerformanceBotBehaviorLastIssue = string.Empty;

    private enum ClientPerformanceMode
    {
        Practice,
        LastToDie,
    }

    private enum ClientPerformanceMetric
    {
        Update,
        Draw,
        Simulation,
        Presentation,
        WorldDraw,
        HudDraw,
        ModalDraw,
        PluginFrame,
        PluginEvents,
        Interpolation,
        RenderStates,
        Music,
        BotBuild,
        BotApply,
    }

    private bool IsClientPerformanceDiagnosticsEnabled()
    {
        return !OperatingSystem.IsBrowser()
            && (ClientPerformanceLoggingEnabled || ClientPerformanceTestEnabled);
    }

    private bool ShouldMeasureClientPerformanceDurations()
    {
        return OperatingSystem.IsBrowser() || IsClientPerformanceDiagnosticsEnabled();
    }

    private void BeginClientPerformanceDiagnosticsFrame(GameTime gameTime)
    {
        if (!IsClientPerformanceDiagnosticsEnabled())
        {
            return;
        }

        _ = gameTime;
        EnsureClientPerformanceDiagnosticsInitialized();
        var frameTimestamp = Stopwatch.GetTimestamp();
        if (_clientPerformanceLastFrameTimestamp > 0L)
        {
            _clientPerformanceSummaryElapsedSeconds += Math.Max(
                0d,
                (frameTimestamp - _clientPerformanceLastFrameTimestamp) / (double)Stopwatch.Frequency);
        }

        _clientPerformanceLastFrameTimestamp = frameTimestamp;
    }

    private void FinalizeClientPerformanceDiagnosticsFrame()
    {
        if (!IsClientPerformanceDiagnosticsEnabled()
            || _clientPerformanceSummaryElapsedSeconds < ClientPerformanceSummaryIntervalSeconds)
        {
            return;
        }

        PublishClientPerformanceSummary();
    }

    private void RecordClientPerformanceMetric(ClientPerformanceMetric metric, double milliseconds)
    {
        if (!IsClientPerformanceDiagnosticsEnabled())
        {
            return;
        }

        _clientPerformance.Record(metric, milliseconds);
    }

    private void AdvanceClientPerformanceAutomation()
    {
        AdvanceClientOnlineSmokeAutomation();
        if (!ClientPerformanceTestEnabled || OperatingSystem.IsBrowser())
        {
            return;
        }

        if (_clientPerformanceTestMeasurementCompleted)
        {
            return;
        }

        if (_startupSplashOpen && _bootstrapController.IsMenuBootstrapComplete)
        {
            _startupSplashOpen = false;
            _mainMenuOpen = true;
            StopFaucetMusic();
            LogClientPerformanceLine("event=client_perf_test_skip_startup_splash");
        }

        if (!_clientPerformanceTestSessionRequested)
        {
            if (_startupSplashOpen || !_mainMenuOpen || !_bootstrapController.CanEnterGameplaySession(out _))
            {
                return;
            }

            var mode = GetClientPerformanceMode();
            _practiceMapEntries = BuildPracticeMapEntries();
            NormalizePracticeSetupState();
            if (mode == ClientPerformanceMode.Practice
                && !SelectPracticeMapEntry(Environment.GetEnvironmentVariable(ClientPerformanceMapEnvironmentVariable) ?? ClientPerformanceDefaultMap))
            {
                SelectPracticeMapEntry(ClientPerformanceDefaultMap);
            }

            _practiceFriendlyBotCount = Math.Clamp(GetClientPerformanceEnvironmentInt(ClientPerformanceFriendlyBotsEnvironmentVariable, ClientPerformanceDefaultFriendlyBots), 0, 9);
            _practiceEnemyBotCount = Math.Clamp(GetClientPerformanceEnvironmentInt(ClientPerformanceEnemyBotsEnvironmentVariable, ClientPerformanceDefaultEnemyBots), 0, 9);
            _practiceTickRate = SimulationConfig.DefaultTicksPerSecond;
            _clientPerformanceTestSessionRequested = true;
            LogClientPerformanceLine(
                $"event=client_perf_test_setup mode={mode} map={GetSelectedPracticeMapEntry()?.LevelName ?? ClientPerformanceDefaultMap} friendlyBots={_practiceFriendlyBotCount} enemyBots={_practiceEnemyBotCount}");
            if (mode == ClientPerformanceMode.LastToDie)
            {
                TryStartLastToDieRun(LastToDieDifficulty.Standard);
            }
            else
            {
                TryStartPracticeFromSetup();
                ResetClientPerformanceBotBehavior();
            }
            return;
        }

        if (GetClientPerformanceMode() == ClientPerformanceMode.LastToDie)
        {
            AdvanceLastToDiePerformanceAutomation();
            return;
        }

        if (!IsPracticeSessionActive)
        {
            return;
        }

        if (_teamSelectOpen)
        {
            ApplyTeamSelection(3);
            return;
        }

        if (_classSelectOpen)
        {
            ApplyDirectClassSelection(GetClientPerformanceClass());
            CloseGameplaySelectionMenus();
            return;
        }

        if (_world.LocalPlayerAwaitingJoin)
        {
            return;
        }

        var warmupSeconds = Math.Max(0, GetClientPerformanceEnvironmentInt(ClientPerformanceWarmupSecondsEnvironmentVariable, ClientPerformanceDefaultWarmupSeconds));
        var warmupTicks = warmupSeconds * _config.TicksPerSecond;
        if (!_clientPerformanceTestMeasurementStarted)
        {
            if (_practiceSessionElapsedTicks < warmupTicks)
            {
                return;
            }

            _clientPerformanceTestMeasurementStarted = true;
            _clientPerformanceTestMeasuredSeconds = 0d;
            _clientPerformanceTestSummarySamples = 0;
            _clientPerformanceTestUpdateFpsTotal = 0d;
            _clientPerformanceTestDrawFpsTotal = 0d;
            _clientPerformanceTestMinUpdateFps = double.MaxValue;
            _clientPerformanceTestMinDrawFps = double.MaxValue;
            ResetClientPerformanceMeasurementWindow();
            LogClientPerformanceLine($"event=client_perf_test_measurement_started {FormatClientPerformanceBotBehaviorSummary()}");
        }
    }

    private void AdvanceLastToDiePerformanceAutomation()
    {
        if (!IsLastToDieSessionActive || _lastToDieRun is null)
        {
            return;
        }

        if (_lastToDieSurvivorMenuOpen)
        {
            ChooseLastToDieSurvivor(GetClientPerformanceLastToDieSurvivorIndex());
            return;
        }

        if (_lastToDiePerkMenuOpen)
        {
            ChooseLastToDiePerk(0);
            return;
        }

        if (_world.LocalPlayerAwaitingJoin)
        {
            return;
        }

        var warmupSeconds = Math.Max(0, GetClientPerformanceEnvironmentInt(ClientPerformanceWarmupSecondsEnvironmentVariable, ClientPerformanceDefaultWarmupSeconds));
        var warmupTicks = warmupSeconds * _config.TicksPerSecond;
        if (!_clientPerformanceTestMeasurementStarted)
        {
            if (_lastToDieRun.RunElapsedTicks < warmupTicks)
            {
                return;
            }

            _clientPerformanceTestMeasurementStarted = true;
            _clientPerformanceTestMeasuredSeconds = 0d;
            _clientPerformanceTestSummarySamples = 0;
            _clientPerformanceTestUpdateFpsTotal = 0d;
            _clientPerformanceTestDrawFpsTotal = 0d;
            _clientPerformanceTestMinUpdateFps = double.MaxValue;
            _clientPerformanceTestMinDrawFps = double.MaxValue;
            ResetClientPerformanceMeasurementWindow();
            LogClientPerformanceLine(
                $"event=client_perf_test_measurement_started mode=lasttodie survivor={_lastToDieRun.SurvivorKind} round={_lastToDieRun.StageNumber} enemyBots={_lastToDieRun.EnemyBotCount}");
        }
    }

    private void ResetClientPerformanceMeasurementWindow()
    {
        _clientPerformance.Reset();
        _clientPerformanceSummaryElapsedSeconds = 0d;
        _clientPerformanceLastFrameTimestamp = Stopwatch.GetTimestamp();
    }

    private void EnsureClientPerformanceDiagnosticsInitialized()
    {
        if (_clientPerformanceDiagnosticsInitialized)
        {
            return;
        }

        _clientPerformanceDiagnosticsInitialized = true;
        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        _clientPerformanceLogPath = RuntimePaths.GetLogPath($"{ClientPerformanceLogFilePrefix}-{timestamp}.log");
        var mode = GetClientPerformanceMode();
        var map = Environment.GetEnvironmentVariable(ClientPerformanceMapEnvironmentVariable) ?? ClientPerformanceDefaultMap;
        var friendlyBots = GetClientPerformanceEnvironmentInt(ClientPerformanceFriendlyBotsEnvironmentVariable, ClientPerformanceDefaultFriendlyBots);
        var enemyBots = GetClientPerformanceEnvironmentInt(ClientPerformanceEnemyBotsEnvironmentVariable, ClientPerformanceDefaultEnemyBots);
        var warmupSeconds = GetClientPerformanceEnvironmentInt(ClientPerformanceWarmupSecondsEnvironmentVariable, ClientPerformanceDefaultWarmupSeconds);
        var measureSeconds = GetClientPerformanceEnvironmentInt(ClientPerformanceMeasureSecondsEnvironmentVariable, ClientPerformanceDefaultMeasureSeconds);
        var playerClass = GetClientPerformanceClass();
        var botClass = TryGetClientPerformanceBotClass(out var parsedBotClass) ? parsedBotClass.ToString() : "mixed";
        var forcedInput = Environment.GetEnvironmentVariable(ClientPerformanceInputEnvironmentVariable) ?? "none";
        LogClientPerformanceLine(
            $"event=client_perf_started test={ClientPerformanceTestEnabled} mode={mode} map={map} friendlyBots={friendlyBots} enemyBots={enemyBots} class={playerClass} botClass={botClass} input={SanitizeClientPerformanceToken(forcedInput)} warmupSeconds={warmupSeconds} measureSeconds={measureSeconds}");
        AddConsoleLine($"client perf log: {_clientPerformanceLogPath}");
    }

    private void PublishClientPerformanceSummary()
    {
        var elapsedSeconds = Math.Max(_clientPerformanceSummaryElapsedSeconds, 0.0001d);
        var snapshot = _clientPerformance.GetSnapshot();
        var updateFps = snapshot.UpdateCount / elapsedSeconds;
        var drawFps = snapshot.DrawCount / elapsedSeconds;
        var currentMemoryMegabytes = GC.GetTotalMemory(forceFullCollection: false) / (1024d * 1024d);
        var line = string.Create(
            CultureInfo.InvariantCulture,
            $"event=client_perf_summary updateFps={updateFps:F1} drawFps={drawFps:F1} " +
            $"update={snapshot.GetSummary(ClientPerformanceMetric.Update)} " +
            $"draw={snapshot.GetSummary(ClientPerformanceMetric.Draw)} " +
            $"sim={snapshot.GetSummary(ClientPerformanceMetric.Simulation)} " +
            $"present={snapshot.GetSummary(ClientPerformanceMetric.Presentation)} " +
            $"world={snapshot.GetSummary(ClientPerformanceMetric.WorldDraw)} " +
            $"hud={snapshot.GetSummary(ClientPerformanceMetric.HudDraw)} " +
            $"modal={snapshot.GetSummary(ClientPerformanceMetric.ModalDraw)} " +
            $"pluginFrame={snapshot.GetSummary(ClientPerformanceMetric.PluginFrame)} " +
            $"pluginEvents={snapshot.GetSummary(ClientPerformanceMetric.PluginEvents)} " +
            $"interp={snapshot.GetSummary(ClientPerformanceMetric.Interpolation)} " +
            $"renderStates={snapshot.GetSummary(ClientPerformanceMetric.RenderStates)} " +
            $"music={snapshot.GetSummary(ClientPerformanceMetric.Music)} " +
            $"botBuild={snapshot.GetSummary(ClientPerformanceMetric.BotBuild)} " +
            $"botApply={snapshot.GetSummary(ClientPerformanceMetric.BotApply)} " +
            $"looseSheets={_looseSheetVisuals.Count} " +
            $"moneySheets={GetClientPerformanceMoneySheetCount()} " +
            $"pendingVisuals={_pendingNetworkVisualEvents.Count} " +
            $"memMb={currentMemoryMegabytes:F1}");
        LogClientPerformanceLine(line);

        if (_clientPerformanceTestMeasurementStarted && !_clientPerformanceTestMeasurementCompleted)
        {
            _clientPerformanceTestMeasuredSeconds += elapsedSeconds;
            _clientPerformanceTestSummarySamples += 1;
            _clientPerformanceTestUpdateFpsTotal += updateFps;
            _clientPerformanceTestDrawFpsTotal += drawFps;
            _clientPerformanceTestMinUpdateFps = Math.Min(_clientPerformanceTestMinUpdateFps, updateFps);
            _clientPerformanceTestMinDrawFps = Math.Min(_clientPerformanceTestMinDrawFps, drawFps);
            var measureSeconds = Math.Max(1, GetClientPerformanceEnvironmentInt(ClientPerformanceMeasureSecondsEnvironmentVariable, ClientPerformanceDefaultMeasureSeconds));
            if (_clientPerformanceTestMeasuredSeconds >= measureSeconds)
            {
                PublishClientPerformanceTestResult();
            }
        }

        _clientPerformance.Reset();
        _clientPerformanceSummaryElapsedSeconds = 0d;
    }

    private void PublishClientPerformanceTestResult()
    {
        _clientPerformanceTestMeasurementCompleted = true;
        var samples = Math.Max(1, _clientPerformanceTestSummarySamples);
        var averageUpdateFps = _clientPerformanceTestUpdateFpsTotal / samples;
        var averageDrawFps = _clientPerformanceTestDrawFpsTotal / samples;
        var minimumUpdateFps = _clientPerformanceTestMinUpdateFps == double.MaxValue ? 0d : _clientPerformanceTestMinUpdateFps;
        var minimumDrawFps = _clientPerformanceTestMinDrawFps == double.MaxValue ? 0d : _clientPerformanceTestMinDrawFps;
        var botBehaviorPass = IsClientPerformanceBotBehaviorPass(out var botBehaviorFailureReason);
        var pass = averageUpdateFps >= 60d
            && averageDrawFps >= 60d
            && minimumUpdateFps >= 60d
            && minimumDrawFps >= 60d
            && botBehaviorPass;
        LogClientPerformanceLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"event=client_perf_test_result pass={pass} avgUpdateFps={averageUpdateFps:F1} avgDrawFps={averageDrawFps:F1} minUpdateFps={minimumUpdateFps:F1} minDrawFps={minimumDrawFps:F1} seconds={_clientPerformanceTestMeasuredSeconds:F1} botBehaviorPass={botBehaviorPass} botBehaviorReason={botBehaviorFailureReason}"));
        LogClientPerformanceLine(
            $"event=client_perf_test_bot_behavior pass={botBehaviorPass} reason={botBehaviorFailureReason} {FormatClientPerformanceBotBehaviorSummary()}");

        if (GetClientPerformanceEnvironmentFlag(ClientPerformanceAutoExitEnvironmentVariable))
        {
            Exit();
        }
    }

    private void ResetClientPerformanceBotBehavior()
    {
        if (!ClientPerformanceTestEnabled)
        {
            return;
        }

        _clientPerformanceBotBehaviorSlots.Clear();
        _clientPerformanceBotBehaviorActive = true;
        _clientPerformanceBotBehaviorInitialRedCaps = _world.RedCaps;
        _clientPerformanceBotBehaviorInitialBlueCaps = _world.BlueCaps;
        _clientPerformanceBotBehaviorFirstPickupTick = -1;
        _clientPerformanceBotBehaviorFirstScoreTick = -1;
        _clientPerformanceBotBehaviorFirstControlTick = -1;
        _clientPerformanceBotBehaviorFirstRouteTick = -1;
        _clientPerformanceBotBehaviorPendingRouteTicks = 0;
        _clientPerformanceBotBehaviorLastIssue = string.Empty;
    }

    private void RecordClientPerformanceBotBehavior(
        IReadOnlyDictionary<byte, ControlledBotSlot> controlledSlots,
        BotControllerDiagnosticsSnapshot diagnostics)
    {
        if (!_clientPerformanceBotBehaviorActive || !ClientPerformanceTestEnabled)
        {
            return;
        }

        var tick = IsPracticeSessionActive ? _practiceSessionElapsedTicks : 0;
        foreach (var entry in controlledSlots)
        {
            if (!_world.TryGetNetworkPlayer(entry.Key, out var player)
                || _world.IsNetworkPlayerAwaitingJoin(entry.Key)
                || !player.IsAlive)
            {
                continue;
            }

            var centerX = (player.Left + player.Right) * 0.5f;
            var centerY = (player.Top + player.Bottom) * 0.5f;
            if (!_clientPerformanceBotBehaviorSlots.TryGetValue(entry.Key, out var slot))
            {
                slot = new ClientPerformanceBotBehaviorSlot(centerX, centerY);
                _clientPerformanceBotBehaviorSlots[entry.Key] = slot;
            }

            slot.Sample(centerX, centerY, tick, ClientPerformanceBotMovementThresholdPixels);
            var slotDiagnostic = FindClientPerformanceBotDiagnostic(diagnostics, entry.Key);
            if (slotDiagnostic.Slot == entry.Key)
            {
                slot.SampleDiagnostics(
                    slotDiagnostic,
                    centerX,
                    player.Bottom,
                    tick,
                    ClientPerformanceBotObjectiveImprovementThresholdPixels);
            }

            if (player.IsCarryingIntel && _clientPerformanceBotBehaviorFirstPickupTick < 0)
            {
                _clientPerformanceBotBehaviorFirstPickupTick = tick;
            }
        }

        if (_clientPerformanceBotBehaviorFirstScoreTick < 0
            && (_world.RedCaps > _clientPerformanceBotBehaviorInitialRedCaps
                || _world.BlueCaps > _clientPerformanceBotBehaviorInitialBlueCaps))
        {
            _clientPerformanceBotBehaviorFirstScoreTick = tick;
        }

        if (_clientPerformanceBotBehaviorFirstControlTick < 0)
        {
            foreach (var point in _world.ControlPoints)
            {
                if (point.Team.HasValue)
                {
                    _clientPerformanceBotBehaviorFirstControlTick = tick;
                    break;
                }
            }
        }

        foreach (var diagnostic in diagnostics.Entries)
        {
            if (diagnostic.NavigationIssueLabel is "route_plan_pending" or "route_field_pending")
            {
                _clientPerformanceBotBehaviorPendingRouteTicks += 1;
            }

            if (!string.IsNullOrWhiteSpace(diagnostic.NavigationIssueLabel))
            {
                _clientPerformanceBotBehaviorLastIssue = diagnostic.NavigationIssueLabel;
            }

            if (_clientPerformanceBotBehaviorFirstRouteTick < 0
                && IsClientPerformanceTraversalExecution(diagnostic))
            {
                _clientPerformanceBotBehaviorFirstRouteTick = tick;
            }
        }
    }

    private bool IsClientPerformanceBotBehaviorPass(out string failureReason)
    {
        if (GetClientPerformanceMode() != ClientPerformanceMode.Practice)
        {
            failureReason = "not_practice";
            return true;
        }

        var expectedBotCount = Math.Clamp(
            GetClientPerformanceEnvironmentInt(ClientPerformanceFriendlyBotsEnvironmentVariable, ClientPerformanceDefaultFriendlyBots),
            0,
            9)
            + Math.Clamp(
                GetClientPerformanceEnvironmentInt(ClientPerformanceEnemyBotsEnvironmentVariable, ClientPerformanceDefaultEnemyBots),
                0,
                9);
        if (expectedBotCount <= 0)
        {
            failureReason = "no_bots_requested";
            return true;
        }

        if (_clientPerformanceBotBehaviorSlots.Count < expectedBotCount)
        {
            failureReason = "missing_controlled_bots";
            return false;
        }

        if (_clientPerformanceBotBehaviorSlots.Count == 0)
        {
            failureReason = "no_controlled_bots";
            return false;
        }

        var movedCount = GetClientPerformanceMovedBotCount();
        if (movedCount < _clientPerformanceBotBehaviorSlots.Count)
        {
            failureReason = "not_all_bots_moved";
            return false;
        }

        if (GetClientPerformanceDisplacedBotCount() < _clientPerformanceBotBehaviorSlots.Count)
        {
            failureReason = "not_all_bots_displaced";
            return false;
        }

        var firstMoveTick = GetClientPerformanceFirstMoveTick();
        if (firstMoveTick < 0 || firstMoveTick > ClientPerformanceBotMovementMaxTicks)
        {
            failureReason = "first_move_late";
            return false;
        }

        if (_clientPerformanceBotBehaviorFirstRouteTick < 0)
        {
            failureReason = "no_link_execution";
            return false;
        }

        if (_clientPerformanceBotBehaviorFirstRouteTick > ClientPerformanceBotRouteMaxTicks)
        {
            failureReason = "certified_route_late";
            return false;
        }

        if (GetClientPerformanceSurfaceChangeBotCount() < _clientPerformanceBotBehaviorSlots.Count)
        {
            failureReason = "not_all_bots_changed_surface";
            return false;
        }

        if (GetClientPerformanceLinkExecutionBotCount() < _clientPerformanceBotBehaviorSlots.Count)
        {
            failureReason = "not_all_bots_executed_link";
            return false;
        }

        if (GetClientPerformanceObjectiveProgressBotCount() < _clientPerformanceBotBehaviorSlots.Count)
        {
            failureReason = "not_all_bots_improved_objective_distance";
            return false;
        }

        if (_clientPerformanceBotBehaviorFirstPickupTick < 0
            && _clientPerformanceBotBehaviorFirstScoreTick < 0
            && _clientPerformanceBotBehaviorFirstControlTick < 0)
        {
            failureReason = "no_objective_event";
            return false;
        }

        failureReason = "ok";
        return true;
    }

    private string FormatClientPerformanceBotBehaviorSummary()
    {
        var movedCount = GetClientPerformanceMovedBotCount();
        var displacedCount = GetClientPerformanceDisplacedBotCount();
        var firstMoveTick = GetClientPerformanceFirstMoveTick();
        var maxFirstMoveTick = GetClientPerformanceMaxFirstMoveTick();
        var firstSurfaceChangeTick = GetClientPerformanceFirstSurfaceChangeTick();
        var firstLinkExecutionTick = GetClientPerformanceFirstLinkExecutionTick();
        var firstObjectiveProgressTick = GetClientPerformanceFirstObjectiveProgressTick();
        GetClientPerformanceBotRange(out var minX, out var maxX);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"bots={_clientPerformanceBotBehaviorSlots.Count} movedBots={movedCount} displacedBots={displacedCount} surfaceBots={GetClientPerformanceSurfaceChangeBotCount()} linkBots={GetClientPerformanceLinkExecutionBotCount()} objectiveProgressBots={GetClientPerformanceObjectiveProgressBotCount()} firstMoveTick={firstMoveTick} maxFirstMoveTick={maxFirstMoveTick} firstSurfaceChangeTick={firstSurfaceChangeTick} firstLinkExecutionTick={firstLinkExecutionTick} firstObjectiveProgressTick={firstObjectiveProgressTick} xRange={minX:F1}-{maxX:F1} pendingRouteTicks={_clientPerformanceBotBehaviorPendingRouteTicks} firstRouteTick={_clientPerformanceBotBehaviorFirstRouteTick} firstPickupTick={_clientPerformanceBotBehaviorFirstPickupTick} firstScoreTick={_clientPerformanceBotBehaviorFirstScoreTick} firstControlTick={_clientPerformanceBotBehaviorFirstControlTick} lastIssue={SanitizeClientPerformanceToken(_clientPerformanceBotBehaviorLastIssue)} slots={FormatClientPerformanceBotSlotSummary()}");
    }

    private int GetClientPerformanceMovedBotCount()
    {
        var movedCount = 0;
        foreach (var slot in _clientPerformanceBotBehaviorSlots.Values)
        {
            if (slot.FirstMoveTick >= 0)
            {
                movedCount += 1;
            }
        }

        return movedCount;
    }

    private int GetClientPerformanceDisplacedBotCount()
    {
        var count = 0;
        foreach (var slot in _clientPerformanceBotBehaviorSlots.Values)
        {
            if (slot.MaxDisplacement >= ClientPerformanceBotRequiredDisplacementPixels)
            {
                count += 1;
            }
        }

        return count;
    }

    private int GetClientPerformanceSurfaceChangeBotCount()
    {
        var count = 0;
        foreach (var slot in _clientPerformanceBotBehaviorSlots.Values)
        {
            if (slot.FirstSurfaceChangeTick >= 0)
            {
                count += 1;
            }
        }

        return count;
    }

    private int GetClientPerformanceLinkExecutionBotCount()
    {
        var count = 0;
        foreach (var slot in _clientPerformanceBotBehaviorSlots.Values)
        {
            if (slot.FirstLinkExecutionTick >= 0)
            {
                count += 1;
            }
        }

        return count;
    }

    private int GetClientPerformanceObjectiveProgressBotCount()
    {
        var count = 0;
        foreach (var slot in _clientPerformanceBotBehaviorSlots.Values)
        {
            if (slot.FirstObjectiveProgressTick >= 0)
            {
                count += 1;
            }
        }

        return count;
    }

    private int GetClientPerformanceFirstMoveTick()
    {
        var firstMoveTick = -1;
        foreach (var slot in _clientPerformanceBotBehaviorSlots.Values)
        {
            if (slot.FirstMoveTick < 0)
            {
                continue;
            }

            firstMoveTick = firstMoveTick < 0
                ? slot.FirstMoveTick
                : Math.Min(firstMoveTick, slot.FirstMoveTick);
        }

        return firstMoveTick;
    }

    private int GetClientPerformanceMaxFirstMoveTick()
    {
        var maxFirstMoveTick = -1;
        foreach (var slot in _clientPerformanceBotBehaviorSlots.Values)
        {
            if (slot.FirstMoveTick >= 0)
            {
                maxFirstMoveTick = Math.Max(maxFirstMoveTick, slot.FirstMoveTick);
            }
        }

        return maxFirstMoveTick;
    }

    private int GetClientPerformanceFirstSurfaceChangeTick()
    {
        return GetClientPerformanceFirstSlotTick(static slot => slot.FirstSurfaceChangeTick);
    }

    private int GetClientPerformanceFirstLinkExecutionTick()
    {
        return GetClientPerformanceFirstSlotTick(static slot => slot.FirstLinkExecutionTick);
    }

    private int GetClientPerformanceFirstObjectiveProgressTick()
    {
        return GetClientPerformanceFirstSlotTick(static slot => slot.FirstObjectiveProgressTick);
    }

    private int GetClientPerformanceFirstSlotTick(Func<ClientPerformanceBotBehaviorSlot, int> selector)
    {
        var firstTick = -1;
        foreach (var slot in _clientPerformanceBotBehaviorSlots.Values)
        {
            var tick = selector(slot);
            if (tick < 0)
            {
                continue;
            }

            firstTick = firstTick < 0
                ? tick
                : Math.Min(firstTick, tick);
        }

        return firstTick;
    }

    private void GetClientPerformanceBotRange(out float minX, out float maxX)
    {
        minX = 0f;
        maxX = 0f;
        var found = false;
        foreach (var slot in _clientPerformanceBotBehaviorSlots.Values)
        {
            if (!found)
            {
                minX = slot.MinX;
                maxX = slot.MaxX;
                found = true;
                continue;
            }

            minX = MathF.Min(minX, slot.MinX);
            maxX = MathF.Max(maxX, slot.MaxX);
        }
    }

    private static string SanitizeClientPerformanceToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "none";
        }

        return value.Replace(' ', '_').Replace(';', ',');
    }

    private int GetClientPerformanceMoneySheetCount()
    {
        var count = 0;
        for (var index = 0; index < _looseSheetVisuals.Count; index += 1)
        {
            if (_looseSheetVisuals[index].IsCivvieMoney)
            {
                count += 1;
            }
        }

        return count;
    }

    private string FormatClientPerformanceBotSlotSummary()
    {
        if (_clientPerformanceBotBehaviorSlots.Count == 0)
        {
            return "none";
        }

        var parts = new List<string>(_clientPerformanceBotBehaviorSlots.Count);
        foreach (var entry in _clientPerformanceBotBehaviorSlots)
        {
            parts.Add(entry.Value.Format(entry.Key));
        }

        return string.Join("|", parts);
    }

    private static BotControllerDiagnosticsEntry FindClientPerformanceBotDiagnostic(
        BotControllerDiagnosticsSnapshot diagnostics,
        byte slot)
    {
        foreach (var entry in diagnostics.Entries)
        {
            if (entry.Slot == slot)
            {
                return entry;
            }
        }

        return default;
    }

    private static bool IsClientPerformanceTraversalExecution(BotControllerDiagnosticsEntry diagnostic)
    {
        var debug = diagnostic.MoveDebug ?? string.Empty;
        return debug.Contains("exec=ExecuteLinkProgram", StringComparison.Ordinal)
            || debug.Contains("exec=SettleLinkLanding", StringComparison.Ordinal)
            || debug.Contains("exec=SeekFinalTarget", StringComparison.Ordinal)
            || (TryParseClientPerformanceRouteLinkIndex(debug, out var linkIndex) && linkIndex > 0);
    }

    private static bool TryParseClientPerformanceRouteLinkIndex(string debug, out int linkIndex)
    {
        linkIndex = -1;
        const string Marker = "link=";
        var markerIndex = debug.IndexOf(Marker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return false;
        }

        var startIndex = markerIndex + Marker.Length;
        var endIndex = startIndex;
        while (endIndex < debug.Length && char.IsDigit(debug[endIndex]))
        {
            endIndex += 1;
        }

        return endIndex > startIndex
            && int.TryParse(
                debug.AsSpan(startIndex, endIndex - startIndex),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out linkIndex);
    }

    private void LogClientPerformanceLine(string line)
    {
        if (string.IsNullOrWhiteSpace(_clientPerformanceLogPath))
        {
            return;
        }

        try
        {
            var timestamp = DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture);
            File.AppendAllText(_clientPerformanceLogPath, $"[{timestamp}] {line}{Environment.NewLine}");
        }
        catch
        {
        }
    }

    private static bool GetClientPerformanceEnvironmentFlag(string variableName)
    {
        var value = Environment.GetEnvironmentVariable(variableName);
        return string.Equals(value, "1", StringComparison.Ordinal)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    private static int GetClientPerformanceEnvironmentInt(string variableName, int fallback)
    {
        var value = Environment.GetEnvironmentVariable(variableName);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    private static ClientPerformanceMode GetClientPerformanceMode()
    {
        var value = Environment.GetEnvironmentVariable(ClientPerformanceModeEnvironmentVariable);
        return string.Equals(value, "lasttodie", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "ltd", StringComparison.OrdinalIgnoreCase)
            ? ClientPerformanceMode.LastToDie
            : ClientPerformanceMode.Practice;
    }

    private static PlayerClass GetClientPerformanceClass()
    {
        return ParseClientPerformanceClass(
            Environment.GetEnvironmentVariable(ClientPerformanceClassEnvironmentVariable),
            PlayerClass.Scout);
    }

    private static bool TryGetClientPerformanceBotClass(out PlayerClass classId)
    {
        var value = Environment.GetEnvironmentVariable(ClientPerformanceBotClassEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(value))
        {
            classId = default;
            return false;
        }

        classId = ParseClientPerformanceClass(value, PlayerClass.Scout);
        return true;
    }

    private static PlayerClass ParseClientPerformanceClass(string? value, PlayerClass fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return value.Trim() switch
        {
            var className when string.Equals(className, "civilian", StringComparison.OrdinalIgnoreCase) => PlayerClass.Quote,
            var className when string.Equals(className, "employer", StringComparison.OrdinalIgnoreCase) => PlayerClass.Quote,
            var className when string.Equals(className, "civ", StringComparison.OrdinalIgnoreCase) => PlayerClass.Quote,
            var className when Enum.TryParse<PlayerClass>(className, ignoreCase: true, out var parsedClass) => parsedClass,
            _ => fallback,
        };
    }

    private static PlayerInputSnapshot ApplyClientPerformanceForcedInput(PlayerInputSnapshot input)
    {
        if (!ClientPerformanceTestEnabled && !ClientOnlineSmokeEnabled)
        {
            return input;
        }

        var value = Environment.GetEnvironmentVariable(ClientPerformanceInputEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(value))
        {
            return input;
        }

        var normalized = value.Trim();
        if (string.Equals(normalized, "secondary", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "umbrella", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "m2", StringComparison.OrdinalIgnoreCase))
        {
            return input with { FireSecondary = true };
        }

        if (string.Equals(normalized, "ability", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "utility", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "space", StringComparison.OrdinalIgnoreCase))
        {
            return input with { UseAbility = true };
        }

        if (string.Equals(normalized, "taunt", StringComparison.OrdinalIgnoreCase))
        {
            return input with { Taunt = true };
        }

        if (string.Equals(normalized, "primary", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "m1", StringComparison.OrdinalIgnoreCase))
        {
            return input with { FirePrimary = true };
        }

        return input;
    }

    private static int GetClientPerformanceLastToDieSurvivorIndex()
    {
        var value = Environment.GetEnvironmentVariable(ClientPerformanceLastToDieSurvivorEnvironmentVariable) ?? ClientPerformanceDefaultLastToDieSurvivor;
        return value.ToLowerInvariant() switch
        {
            "demoknight" or "demo" => 1,
            "engineer" or "engi" => 2,
            _ => 0,
        };
    }

    private sealed class ClientPerformanceAccumulator
    {
        private readonly MetricAccumulator[] _metrics = new MetricAccumulator[Enum.GetValues<ClientPerformanceMetric>().Length];

        public int UpdateCount => _metrics[(int)ClientPerformanceMetric.Update].Count;

        public int DrawCount => _metrics[(int)ClientPerformanceMetric.Draw].Count;

        public void Record(ClientPerformanceMetric metric, double milliseconds)
        {
            _metrics[(int)metric].Record(milliseconds);
        }

        public ClientPerformanceSnapshot GetSnapshot()
        {
            return new ClientPerformanceSnapshot(_metrics);
        }

        public void Reset()
        {
            for (var index = 0; index < _metrics.Length; index += 1)
            {
                _metrics[index].Reset();
            }
        }
    }

    private readonly record struct ClientPerformanceSnapshot(MetricAccumulator[] Metrics)
    {
        public int UpdateCount => Metrics[(int)ClientPerformanceMetric.Update].Count;

        public int DrawCount => Metrics[(int)ClientPerformanceMetric.Draw].Count;

        public string GetSummary(ClientPerformanceMetric metric)
        {
            var accumulator = Metrics[(int)metric];
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{accumulator.LastMilliseconds:0.00}/{accumulator.GetAverageMilliseconds():0.00}/{accumulator.MaxMilliseconds:0.00}ms");
        }
    }

    private struct MetricAccumulator
    {
        public int Count { get; private set; }

        public double LastMilliseconds { get; private set; }

        public double TotalMilliseconds { get; private set; }

        public double MaxMilliseconds { get; private set; }

        public void Record(double milliseconds)
        {
            LastMilliseconds = milliseconds;
            TotalMilliseconds += milliseconds;
            MaxMilliseconds = Math.Max(MaxMilliseconds, milliseconds);
            Count += 1;
        }

        public double GetAverageMilliseconds()
        {
            return Count <= 0 ? 0d : TotalMilliseconds / Count;
        }

        public void Reset()
        {
            Count = 0;
            LastMilliseconds = 0d;
            TotalMilliseconds = 0d;
            MaxMilliseconds = 0d;
        }
    }

    private sealed class ClientPerformanceBotBehaviorSlot
    {
        private readonly float _startX;
        private readonly float _startY;
        private int _firstSurfaceId = -1;
        private float _initialObjectiveDistance = float.PositiveInfinity;
        private float _bestObjectiveDistance = float.PositiveInfinity;

        public ClientPerformanceBotBehaviorSlot(float startX, float startY)
        {
            _startX = startX;
            _startY = startY;
            MinX = startX;
            MaxX = startX;
            MinY = startY;
            MaxY = startY;
        }

        public float MinX { get; private set; }

        public float MaxX { get; private set; }

        public float MinY { get; private set; }

        public float MaxY { get; private set; }

        public int FirstMoveTick { get; private set; } = -1;

        public int FirstSurfaceChangeTick { get; private set; } = -1;

        public int FirstLinkExecutionTick { get; private set; } = -1;

        public int FirstObjectiveProgressTick { get; private set; } = -1;

        public int PendingRouteTicks { get; private set; }

        public int MaxRouteLinkIndex { get; private set; } = -1;

        public float MaxDisplacement { get; private set; }

        public float ObjectiveImprovement
        {
            get
            {
                if (float.IsPositiveInfinity(_initialObjectiveDistance)
                    || float.IsPositiveInfinity(_bestObjectiveDistance))
                {
                    return 0f;
                }

                return MathF.Max(0f, _initialObjectiveDistance - _bestObjectiveDistance);
            }
        }

        public string LastIssue { get; private set; } = string.Empty;

        public void Sample(float x, float y, int tick, float movementThreshold)
        {
            MinX = MathF.Min(MinX, x);
            MaxX = MathF.Max(MaxX, x);
            MinY = MathF.Min(MinY, y);
            MaxY = MathF.Max(MaxY, y);
            var dx = x - _startX;
            var dy = y - _startY;
            MaxDisplacement = MathF.Max(MaxDisplacement, MathF.Sqrt((dx * dx) + (dy * dy)));
            if (FirstMoveTick >= 0)
            {
                return;
            }

            if ((dx * dx) + (dy * dy) >= movementThreshold * movementThreshold)
            {
                FirstMoveTick = tick;
            }
        }

        public void SampleDiagnostics(
            BotControllerDiagnosticsEntry diagnostic,
            float x,
            float bottomY,
            int tick,
            float objectiveImprovementThreshold)
        {
            if (diagnostic.CurrentPointId >= 0)
            {
                if (_firstSurfaceId < 0)
                {
                    _firstSurfaceId = diagnostic.CurrentPointId;
                }
                else if (FirstSurfaceChangeTick < 0 && diagnostic.CurrentPointId != _firstSurfaceId)
                {
                    FirstSurfaceChangeTick = tick;
                }
            }

            if (diagnostic.NavigationIssueLabel is "route_plan_pending" or "route_field_pending")
            {
                PendingRouteTicks += 1;
            }

            if (!string.IsNullOrWhiteSpace(diagnostic.NavigationIssueLabel))
            {
                LastIssue = diagnostic.NavigationIssueLabel;
            }

            if (TryParseClientPerformanceRouteLinkIndex(diagnostic.MoveDebug ?? string.Empty, out var linkIndex))
            {
                MaxRouteLinkIndex = Math.Max(MaxRouteLinkIndex, linkIndex);
            }

            if (FirstLinkExecutionTick < 0 && IsClientPerformanceTraversalExecution(diagnostic))
            {
                FirstLinkExecutionTick = tick;
            }

            if (diagnostic.FocusKind != BotFocusKind.Objective)
            {
                return;
            }

            var objectiveDx = x - diagnostic.RouteGoalX;
            var objectiveDy = bottomY - diagnostic.RouteGoalY;
            var objectiveDistance = MathF.Sqrt((objectiveDx * objectiveDx) + (objectiveDy * objectiveDy));
            if (float.IsPositiveInfinity(_initialObjectiveDistance))
            {
                _initialObjectiveDistance = objectiveDistance;
                _bestObjectiveDistance = objectiveDistance;
                return;
            }

            _bestObjectiveDistance = MathF.Min(_bestObjectiveDistance, objectiveDistance);
            if (FirstObjectiveProgressTick < 0
                && ObjectiveImprovement >= objectiveImprovementThreshold)
            {
                FirstObjectiveProgressTick = tick;
            }
        }

        public string Format(byte slot)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{slot}:disp={MaxDisplacement:F0},move={FirstMoveTick},surf={FirstSurfaceChangeTick},exec={FirstLinkExecutionTick},goal={ObjectiveImprovement:F0}@{FirstObjectiveProgressTick},link={MaxRouteLinkIndex},pending={PendingRouteTicks},last={SanitizeClientPerformanceToken(LastIssue)}");
        }
    }
}
