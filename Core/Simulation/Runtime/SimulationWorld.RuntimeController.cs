using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private sealed class RuntimeController
    {
        private static readonly bool SlowPhaseTracingEnabled =
            Environment.GetEnvironmentVariable("OG_CLIENT_PERF_SIM_TRACE") is "1" or "true" or "TRUE";
        private static readonly double SlowPhaseThresholdMilliseconds = ResolveSlowPhaseThresholdMilliseconds();
        private static readonly string? SlowPhaseTracePath = SlowPhaseTracingEnabled
            ? RuntimePaths.GetLogPath($"simulation-phase-spikes-{DateTime.Now.ToString("yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture)}.log")
            : null;
        private static readonly object SlowPhaseTraceSync = new();
        private readonly SimulationWorld _world;
        private readonly RuntimePhaseController _phaseController;

        public RuntimeController(SimulationWorld world)
        {
            _world = world;
            _phaseController = new RuntimePhaseController(world);
        }

        public void AdvanceOneTick()
        {
            if (_world.AdvancePendingMapChange())
            {
                _world.Frame += 1;
                return;
            }

            if (_world.ClientPredictionMode)
            {
                // Client prediction: simulate projectiles and local player only
                _phaseController.AdvanceClientPredictionPhase();
            }
            else
            {
                // Full simulation for offline mode or server
                var phaseStartTimestamp = Stopwatch.GetTimestamp();
                _phaseController.AdvancePrePlayerSimulationPhase();
                TraceSlowPhase("pre", phaseStartTimestamp);

                phaseStartTimestamp = Stopwatch.GetTimestamp();
                _phaseController.AdvancePlayerSimulationPhase();
                TraceSlowPhase("players", phaseStartTimestamp);

                phaseStartTimestamp = Stopwatch.GetTimestamp();
                _phaseController.AdvancePostPlayerSimulationPhase();
                TraceSlowPhase("post", phaseStartTimestamp);
            }
            _world._previousLocalInput = _world._localInput;
            _world.Frame += 1;
        }

        public void AdvanceLegacyMatchState()
        {
            _phaseController.AdvanceLegacyMatchState();
        }

        public void AdvanceLegacyControlPointMatchState()
        {
            _phaseController.AdvanceLegacyControlPointMatchState();
        }

        public void AdvanceLegacyKothMatchState()
        {
            _phaseController.AdvanceLegacyKothMatchState();
        }

        public void AdvanceLegacyGeneratorMatchState()
        {
            _phaseController.AdvanceLegacyGeneratorMatchState();
        }

        public void AdvanceLegacyCaptureTheFlagState()
        {
            _phaseController.AdvanceLegacyCaptureTheFlagState();
        }

        public void AdvanceLegacyScrState()
        {
            _phaseController.AdvanceLegacyScrState();
        }

        public void AdvanceLegacyArenaState()
        {
            _phaseController.AdvanceLegacyArenaState();
        }

        private static double ResolveSlowPhaseThresholdMilliseconds()
        {
            var configured = Environment.GetEnvironmentVariable("OG_CLIENT_PERF_SIM_TRACE_THRESHOLD_MS");
            return double.TryParse(configured, NumberStyles.Float, CultureInfo.InvariantCulture, out var threshold)
                ? Math.Max(0d, threshold)
                : 25d;
        }

        private void TraceSlowPhase(string phase, long startTimestamp)
        {
            if (!SlowPhaseTracingEnabled || string.IsNullOrWhiteSpace(SlowPhaseTracePath))
            {
                return;
            }

            var elapsedMilliseconds = (Stopwatch.GetTimestamp() - startTimestamp) * 1000d / Stopwatch.Frequency;
            if (elapsedMilliseconds < SlowPhaseThresholdMilliseconds)
            {
                return;
            }

            var line = string.Create(
                CultureInfo.InvariantCulture,
                $"{DateTime.Now:O} frame={_world.Frame} phase={phase} elapsedMs={elapsedMilliseconds:0.0}{Environment.NewLine}");
            lock (SlowPhaseTraceSync)
            {
                File.AppendAllText(SlowPhaseTracePath, line);
            }
        }

    }
}
