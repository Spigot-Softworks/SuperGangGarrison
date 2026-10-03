using System.Diagnostics;
using System.Globalization;

namespace OpenGarrison.Core;

/// <summary>The log files <see cref="SimulationTrace"/> writes.</summary>
internal enum SimulationTraceLog
{
    /// <summary>Tick phases and projectile/transient sub-phases over their threshold.</summary>
    PhaseSpikes,
    /// <summary>Single players whose tick took longer than the player threshold.</summary>
    PlayerSpikes,
    /// <summary>Per-player timings for a slow player phase.</summary>
    PlayerBreakdowns,
    /// <summary>Sub-phase timings inside one player's advance.</summary>
    PlayerPhases,
}

/// <summary>
/// Opt-in performance tracing for the simulation, enabled with
/// <c>OG_CLIENT_PERF_SIM_TRACE=1</c>. Thresholds come from
/// <c>OG_CLIENT_PERF_SIM_TRACE_THRESHOLD_MS</c> (tick phases, default 25),
/// <c>OG_CLIENT_PERF_SIM_PLAYER_TRACE_THRESHOLD_MS</c> (players and entity
/// sub-phases, default 5) and <c>OG_CLIENT_PERF_SIM_PLAYER_PHASE_TRACE_THRESHOLD_MS</c>
/// (sub-phases of one player, default 10).
/// </summary>
/// <remarks>
/// This is process-wide on purpose, and safe to share between worlds: it reads
/// the environment once, only appends to log files, and nothing it holds feeds
/// back into gameplay, so it cannot affect determinism or replays. One lock
/// serialises every append, so worlds on different threads (an embedded server
/// beside the client) write whole lines, and every caller shares one file per log.
/// </remarks>
internal static class SimulationTrace
{
    private static readonly object Sync = new();
    private static readonly string SessionStamp = DateTime.Now.ToString("yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture);

    internal static bool Enabled { get; } =
        Environment.GetEnvironmentVariable("OG_CLIENT_PERF_SIM_TRACE") is "1" or "true" or "TRUE";

    internal static double PhaseThresholdMilliseconds { get; } =
        ReadThreshold("OG_CLIENT_PERF_SIM_TRACE_THRESHOLD_MS", 25d);

    internal static double PlayerThresholdMilliseconds { get; } =
        ReadThreshold("OG_CLIENT_PERF_SIM_PLAYER_TRACE_THRESHOLD_MS", 5d);

    internal static double PlayerPhaseThresholdMilliseconds { get; } =
        ReadThreshold("OG_CLIENT_PERF_SIM_PLAYER_PHASE_TRACE_THRESHOLD_MS", 10d);

    /// <summary>A start timestamp when tracing is on, otherwise 0 (which <see cref="ElapsedMilliseconds"/> reads as 0).</summary>
    internal static long StartTimestamp() => Enabled ? Stopwatch.GetTimestamp() : 0L;

    internal static double ElapsedMilliseconds(long startTimestamp)
    {
        return startTimestamp == 0L
            ? 0d
            : (Stopwatch.GetTimestamp() - startTimestamp) * 1000d / Stopwatch.Frequency;
    }

    internal static void Append(SimulationTraceLog log, string text)
    {
        if (!Enabled)
        {
            return;
        }

        var path = RuntimePaths.GetLogPath($"{FilePrefix(log)}-{SessionStamp}.log");
        lock (Sync)
        {
            File.AppendAllText(path, text);
        }
    }

    private static string FilePrefix(SimulationTraceLog log) => log switch
    {
        SimulationTraceLog.PhaseSpikes => "simulation-phase-spikes",
        SimulationTraceLog.PlayerSpikes => "simulation-player-spikes",
        SimulationTraceLog.PlayerBreakdowns => "simulation-player-breakdowns",
        SimulationTraceLog.PlayerPhases => "simulation-player-phases",
        _ => throw new ArgumentOutOfRangeException(nameof(log), log, null),
    };

    private static double ReadThreshold(string variable, double fallback)
    {
        var configured = Environment.GetEnvironmentVariable(variable);
        return double.TryParse(configured, NumberStyles.Float, CultureInfo.InvariantCulture, out var threshold)
            ? Math.Max(0d, threshold)
            : fallback;
    }
}
