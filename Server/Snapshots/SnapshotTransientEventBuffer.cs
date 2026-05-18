using System;
using System.Collections.Generic;
using System.Linq;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using static ServerHelpers;

namespace OpenGarrison.Server;

internal readonly record struct SnapshotTransientEvents(
    SnapshotVisualEvent[] VisualEvents,
    SnapshotDamageEvent[] DamageEvents,
    SnapshotSoundEvent[] SoundEvents,
    SnapshotGibSpawnEvent[] GibSpawnEvents,
    SnapshotRocketSpawnEvent[] RocketSpawnEvents);

internal sealed class SnapshotTransientEventBuffer(ulong transientEventReplayTicks)
{
    private readonly List<RetainedSnapshotSoundEvent> _recentSoundEvents = new();
    private readonly List<RetainedSnapshotVisualEvent> _recentVisualEvents = new();
    private readonly List<RetainedSnapshotDamageEvent> _recentDamageEvents = new();
    private readonly List<RetainedSnapshotGibSpawnEvent> _recentGibSpawnEvents = new();
    private readonly List<RetainedSnapshotRocketSpawnEvent> _recentRocketSpawnEvents = new();
    private ulong _nextTransientEventId = 1;

    public void Reset(IReadOnlyCollection<ClientSession> clients)
    {
        _recentSoundEvents.Clear();
        _recentVisualEvents.Clear();
        _recentDamageEvents.Clear();
        _recentGibSpawnEvents.Clear();
        _recentRocketSpawnEvents.Clear();
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
        AppendRetainedGibSpawnEvents(world.DrainPendingGibSpawnEvents(), currentFrame);
        AppendRetainedRocketSpawnEvents(world.DrainPendingRocketSpawnEvents(), currentFrame);
        _recentVisualEvents.RemoveAll(visualEvent => visualEvent.ExpiresAfterFrame < currentFrame);
        _recentSoundEvents.RemoveAll(soundEvent => soundEvent.ExpiresAfterFrame < currentFrame);
        _recentDamageEvents.RemoveAll(damageEvent => damageEvent.ExpiresAfterFrame < currentFrame);
        _recentGibSpawnEvents.RemoveAll(gibEvent => gibEvent.ExpiresAfterFrame < currentFrame);
        _recentRocketSpawnEvents.RemoveAll(rocketEvent => rocketEvent.ExpiresAfterFrame < currentFrame);
        return new SnapshotTransientEvents(
            _recentVisualEvents.Select(static visualEvent => visualEvent.Event).ToArray(),
            _recentDamageEvents.Select(static damageEvent => damageEvent.Event).ToArray(),
            _recentSoundEvents.Select(static soundEvent => soundEvent.Event).ToArray(),
            _recentGibSpawnEvents.Select(static gibEvent => gibEvent.Event).ToArray(),
            _recentRocketSpawnEvents.Select(static rocketEvent => rocketEvent.Event).ToArray());
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
            if (IsClientSideDamageVisualEvent(visualEvents[index]))
            {
                continue;
            }

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

    private void AppendRetainedGibSpawnEvents(IReadOnlyList<WorldGibSpawnEvent> gibSpawnEvents, ulong currentFrame)
    {
        for (var index = 0; index < gibSpawnEvents.Count; index += 1)
        {
            _recentGibSpawnEvents.Add(new RetainedSnapshotGibSpawnEvent(
                ToSnapshotGibSpawnEvent(gibSpawnEvents[index], _nextTransientEventId++),
                currentFrame + transientEventReplayTicks));
        }
    }

    private void AppendRetainedRocketSpawnEvents(IReadOnlyList<WorldRocketSpawnEvent> rocketSpawnEvents, ulong currentFrame)
    {
        for (var index = 0; index < rocketSpawnEvents.Count; index += 1)
        {
            _recentRocketSpawnEvents.Add(new RetainedSnapshotRocketSpawnEvent(
                ToSnapshotRocketSpawnEvent(rocketSpawnEvents[index], _nextTransientEventId++),
                currentFrame + transientEventReplayTicks));
        }
    }

    private static bool IsClientSideDamageVisualEvent(WorldVisualEvent visualEvent)
    {
        return string.Equals(visualEvent.EffectName, "Blood", StringComparison.OrdinalIgnoreCase)
            || string.Equals(visualEvent.EffectName, "GibBlood", StringComparison.OrdinalIgnoreCase);
    }

    private static SnapshotGibSpawnEvent ToSnapshotGibSpawnEvent(WorldGibSpawnEvent gibSpawnEvent, ulong eventId)
    {
        return new SnapshotGibSpawnEvent(
            gibSpawnEvent.SpriteName,
            gibSpawnEvent.FrameIndex,
            gibSpawnEvent.X,
            gibSpawnEvent.Y,
            gibSpawnEvent.VelocityX,
            gibSpawnEvent.VelocityY,
            gibSpawnEvent.RotationSpeedDegrees,
            gibSpawnEvent.HorizontalFriction,
            gibSpawnEvent.RotationFriction,
            gibSpawnEvent.LifetimeTicks,
            gibSpawnEvent.BloodChance,
            eventId);
    }

    private static SnapshotRocketSpawnEvent ToSnapshotRocketSpawnEvent(WorldRocketSpawnEvent rocketSpawnEvent, ulong eventId)
    {
        return new SnapshotRocketSpawnEvent(
            rocketSpawnEvent.Id,
            rocketSpawnEvent.Team,
            rocketSpawnEvent.OwnerId,
            rocketSpawnEvent.X,
            rocketSpawnEvent.Y,
            rocketSpawnEvent.PreviousX,
            rocketSpawnEvent.PreviousY,
            rocketSpawnEvent.DirectionRadians,
            rocketSpawnEvent.Speed,
            rocketSpawnEvent.TicksRemaining,
            rocketSpawnEvent.ReducedKnockbackSourceTicksRemaining,
            rocketSpawnEvent.ZeroKnockbackSourceTicksRemaining,
            rocketSpawnEvent.RangeAnchorOwnerId,
            rocketSpawnEvent.LastKnownRangeOriginX,
            rocketSpawnEvent.LastKnownRangeOriginY,
            rocketSpawnEvent.DistanceToTravel,
            rocketSpawnEvent.IsFading,
            rocketSpawnEvent.FadeSourceTicksRemaining,
            rocketSpawnEvent.ExplodeImmediately,
            rocketSpawnEvent.IsCritical,
            eventId);
    }
}
