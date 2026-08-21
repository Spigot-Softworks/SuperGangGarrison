using OpenGarrison.Protocol;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private void ApplySnapshotEventQueues(SnapshotMessage snapshot)
    {
        ApplySnapshotCombatTraces(snapshot.CombatTraces);
        ApplySnapshotSniperAimIndicators(snapshot.SniperAimIndicators);
        ApplySnapshotSoundEvents(snapshot.SoundEvents);
    }

    private void ApplySnapshotCombatTraces(IReadOnlyList<SnapshotCombatTraceState> combatTraces)
    {
        _combatTraces.Clear();
        for (var traceIndex = 0; traceIndex < combatTraces.Count; traceIndex += 1)
        {
            var trace = combatTraces[traceIndex];
            _combatTraces.Add(new CombatTrace(
                trace.StartX,
                trace.StartY,
                trace.EndX,
                trace.EndY,
                trace.TicksRemaining,
                trace.HitCharacter,
                (PlayerTeam)trace.Team,
                trace.IsSniperTracer,
                trace.IsCritical));
        }
    }

    private void ApplySnapshotSniperAimIndicators(IReadOnlyList<SnapshotSniperAimIndicatorState> indicators)
    {
        _sniperAimIndicators.Clear();
        for (var index = 0; index < indicators.Count; index += 1)
        {
            var indicator = indicators[index];
            _sniperAimIndicators.Add(new SniperAimIndicator(
                indicator.SniperPlayerId,
                indicator.X,
                indicator.Y,
                (PlayerTeam)indicator.Team,
                indicator.Transparency));
        }
    }

    private void ApplySnapshotSoundEvents(IReadOnlyList<SnapshotSoundEvent> soundEvents)
    {
        for (var soundIndex = 0; soundIndex < soundEvents.Count; soundIndex += 1)
        {
            var soundEvent = soundEvents[soundIndex];

            // Snapshot application can run several times before the client
            // presentation pass drains the queue. Do not let a newer snapshot
            // erase an older event that is still waiting for its audio asset or
            // for the presentation pass. The server's transient-event replay
            // window already handles delivery; this queue only needs to retain
            // each authoritative event until the client consumes it.
            if (soundEvent.EventId != 0
                && _pendingSoundEvents.Exists(pending => pending.EventId == soundEvent.EventId))
            {
                continue;
            }

            _pendingSoundEvents.Add(new WorldSoundEvent(
                soundEvent.SoundName,
                soundEvent.X,
                soundEvent.Y,
                soundEvent.EventId,
                soundEvent.SourceFrame,
                soundEvent.SourcePlayerId));
        }
    }
}
