using System.Diagnostics;
using System.Globalization;

namespace OpenGarrison.Core;

/// <summary>
/// World-level steps the runtime needs that are not owned by either phase.
/// Implemented explicitly by <see cref="SimulationWorld"/> so the runtime never sees the world itself.
/// </summary>
internal interface ISimulationTickHost
{
    long Frame { get; }

    bool ClientPredictionMode { get; }

    /// <summary>Returns true when a pending map change consumed this tick.</summary>
    bool AdvancePendingMapChange();

    void AdvanceAuthoritativeMapLogicRuntime();

    void AdvanceMovingPlatforms();

    void BeginLastToDieStatusEffectsTick();

    void RefreshLastToDieMedicLinkProjections();

    void AdvanceCivilDefenseTurrets();

    void EndLastToDieStatusEffectsTick();

    void TickMapLogicTimersOncePerFrame();

    void CommitLocalInputForTick();

    void AdvanceFrameCounter();
}

/// <summary>Entity-facing slices of a tick: projectiles, transient entities, players, and structures.</summary>
internal interface IEntityTickPhase
{
    void AdvanceProjectileAndTransientEntityPhase();

    void AdvanceRemoteSnapshotPlayerTauntStates();

    void AdvancePlayerSimulationPhase();

    void AdvancePostPlayerEntityPhase();
}

/// <summary>Match-facing slices of a tick: effects, presentation, objectives, and resolution.</summary>
internal interface IMatchTickPhase
{
    void AdvancePrePlayerMatchPhase();

    void AdvancePresentationAndChatPhase();

    void AdvancePostPlayerMatchPhase();
}

/// <summary>
/// Single owner of tick ordering. <see cref="Tick"/> is the only place that decides
/// which phase runs when; the phase interfaces only decide what happens inside a phase.
/// </summary>
internal sealed class SimulationRuntime
{
    private static readonly bool SlowPhaseTracingEnabled =
        Environment.GetEnvironmentVariable("OG_CLIENT_PERF_SIM_TRACE") is "1" or "true" or "TRUE";
    private static readonly double SlowPhaseThresholdMilliseconds = ResolveSlowPhaseThresholdMilliseconds();
    private static readonly string? SlowPhaseTracePath = SlowPhaseTracingEnabled
        ? RuntimePaths.GetLogPath($"simulation-phase-spikes-{DateTime.Now.ToString("yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture)}.log")
        : null;
    private static readonly object SlowPhaseTraceSync = new();

    private readonly ISimulationTickHost _host;
    private readonly IEntityTickPhase _entities;
    private readonly IMatchTickPhase _match;

    public SimulationRuntime(ISimulationTickHost host, IEntityTickPhase entities, IMatchTickPhase match)
    {
        _host = host;
        _entities = entities;
        _match = match;
    }

    public void Tick()
    {
        if (_host.AdvancePendingMapChange())
        {
            _host.AdvanceFrameCounter();
            return;
        }

        if (_host.ClientPredictionMode)
        {
            // Client prediction: projectiles and deterministic map motion only.
            // Server remains authoritative for all players (including local player).
            AdvanceClientPredictionPhase();
        }
        else
        {
            // Full simulation for offline mode or server.
            var phaseStartTimestamp = Stopwatch.GetTimestamp();
            AdvancePrePlayerPhase();
            TraceSlowPhase("pre", phaseStartTimestamp);

            phaseStartTimestamp = Stopwatch.GetTimestamp();
            _entities.AdvancePlayerSimulationPhase();
            TraceSlowPhase("players", phaseStartTimestamp);

            phaseStartTimestamp = Stopwatch.GetTimestamp();
            AdvancePostPlayerPhase();
            TraceSlowPhase("post", phaseStartTimestamp);
        }

        _host.CommitLocalInputForTick();
        _host.AdvanceFrameCounter();
    }

    private void AdvanceClientPredictionPhase()
    {
        _host.AdvanceAuthoritativeMapLogicRuntime();
        _entities.AdvanceProjectileAndTransientEntityPhase();
        _host.AdvanceMovingPlatforms();
        _entities.AdvanceRemoteSnapshotPlayerTauntStates();
    }

    private void AdvancePrePlayerPhase()
    {
        _match.AdvancePrePlayerMatchPhase();
        _host.BeginLastToDieStatusEffectsTick();
        _host.RefreshLastToDieMedicLinkProjections();
        _host.AdvanceCivilDefenseTurrets();
        _entities.AdvanceProjectileAndTransientEntityPhase();
        _match.AdvancePresentationAndChatPhase();
    }

    private void AdvancePostPlayerPhase()
    {
        _entities.AdvancePostPlayerEntityPhase();
        _host.EndLastToDieStatusEffectsTick();
        _host.TickMapLogicTimersOncePerFrame();
        _match.AdvancePostPlayerMatchPhase();
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
            $"{DateTime.Now:O} frame={_host.Frame} phase={phase} elapsedMs={elapsedMilliseconds:0.0}{Environment.NewLine}");
        lock (SlowPhaseTraceSync)
        {
            File.AppendAllText(SlowPhaseTracePath, line);
        }
    }
}
