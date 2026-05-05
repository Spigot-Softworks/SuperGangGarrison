#nullable enable

using Microsoft.Xna.Framework;
using OpenGarrison.Core;
using System;
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
                TryStartLastToDieRun();
            }
            else
            {
                TryStartPracticeFromSetup();
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
            ApplyDirectClassSelection(PlayerClass.Scout);
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
            LogClientPerformanceLine("event=client_perf_test_measurement_started");
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
            LogClientPerformanceLine(
                $"event=client_perf_test_measurement_started mode=lasttodie survivor={_lastToDieRun.SurvivorKind} round={_lastToDieRun.StageNumber} enemyBots={_lastToDieRun.EnemyBotCount}");
        }
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
        LogClientPerformanceLine(
            $"event=client_perf_started test={ClientPerformanceTestEnabled} mode={mode} map={map} friendlyBots={friendlyBots} enemyBots={enemyBots} warmupSeconds={warmupSeconds} measureSeconds={measureSeconds}");
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
        var pass = averageUpdateFps >= 60d
            && averageDrawFps >= 60d
            && minimumUpdateFps >= 60d
            && minimumDrawFps >= 60d;
        LogClientPerformanceLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"event=client_perf_test_result pass={pass} avgUpdateFps={averageUpdateFps:F1} avgDrawFps={averageDrawFps:F1} minUpdateFps={minimumUpdateFps:F1} minDrawFps={minimumDrawFps:F1} seconds={_clientPerformanceTestMeasuredSeconds:F1}"));

        if (GetClientPerformanceEnvironmentFlag(ClientPerformanceAutoExitEnvironmentVariable))
        {
            Exit();
        }
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
}
