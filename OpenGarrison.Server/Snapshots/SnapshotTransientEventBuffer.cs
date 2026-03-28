using System.Collections.Generic;
using System.Linq;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using static ServerHelpers;

namespace OpenGarrison.Server;

internal readonly record struct SnapshotTransientEvents(
    SnapshotVisualEvent[] VisualEvents,
    SnapshotDamageEvent[] DamageEvents,
    SnapshotSoundEvent[] SoundEvents);

internal sealed class SnapshotTransientEventBuffer(ulong transientEventReplayTicks)
{
    private readonly List<RetainedSnapshotSoundEvent> _recentSoundEvents = new();
    private readonly List<RetainedSnapshotVisualEvent> _recentVisualEvents = new();
    private readonly List<RetainedSnapshotDamageEvent> _recentDamageEvents = new();
    private ulong _nextTransientEventId = 1;

    public void Reset(IReadOnlyCollection<ClientSession> clients)
    {
        _recentSoundEvents.Clear();
        _recentVisualEvents.Clear();
        _recentDamageEvents.Clear();
        _nextTransientEventId = 1;
        foreach (var client in clients)
        {
            client.ResetSnapshotHistory();
        }
    }

    public SnapshotTransientEvents CaptureCurrentEvents(SimulationWorld world)
    {
        var currentFrame = (ulong)world.Frame;
        AppendRetainedVisualEvents(world.DrainPendingVisualEvents(), currentFrame);
        AppendRetainedSoundEvents(world.DrainPendingSoundEvents(), currentFrame);
        AppendRetainedDamageEvents(world.DrainPendingDamageEvents(), currentFrame);
        _recentVisualEvents.RemoveAll(visualEvent => visualEvent.ExpiresAfterFrame < currentFrame);
        _recentSoundEvents.RemoveAll(soundEvent => soundEvent.ExpiresAfterFrame < currentFrame);
        _recentDamageEvents.RemoveAll(damageEvent => damageEvent.ExpiresAfterFrame < currentFrame);
        return new SnapshotTransientEvents(
            _recentVisualEvents.Select(static visualEvent => visualEvent.Event).ToArray(),
            _recentDamageEvents.Select(static damageEvent => damageEvent.Event).ToArray(),
            _recentSoundEvents.Select(static soundEvent => soundEvent.Event).ToArray());
    }

    private void AppendRetainedSoundEvents(IReadOnlyList<WorldSoundEvent> soundEvents, ulong currentFrame)
    {
        for (var index = 0; index < soundEvents.Count; index += 1)
        {
            _recentSoundEvents.Add(new RetainedSnapshotSoundEvent(
                ToSnapshotSoundEvent(soundEvents[index], _nextTransientEventId++),
                currentFrame + transientEventReplayTicks));
        }
    }

    private void AppendRetainedVisualEvents(IReadOnlyList<WorldVisualEvent> visualEvents, ulong currentFrame)
    {
        for (var index = 0; index < visualEvents.Count; index += 1)
        {
            _recentVisualEvents.Add(new RetainedSnapshotVisualEvent(
                ToSnapshotVisualEvent(visualEvents[index], _nextTransientEventId++),
                currentFrame + transientEventReplayTicks));
        }
    }

    private void AppendRetainedDamageEvents(IReadOnlyList<WorldDamageEvent> damageEvents, ulong currentFrame)
    {
        for (var index = 0; index < damageEvents.Count; index += 1)
        {
            _recentDamageEvents.Add(new RetainedSnapshotDamageEvent(
                ToSnapshotDamageEvent(damageEvents[index], _nextTransientEventId++),
                currentFrame + transientEventReplayTicks));
        }
    }
}
