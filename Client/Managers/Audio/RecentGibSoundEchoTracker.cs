#nullable enable

using System;
using System.Collections.Generic;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

internal sealed class RecentGibSoundEchoTracker
{
    private readonly List<RecentGibSoundEvent> _events = new();

    internal void Advance()
    {
        for (var index = _events.Count - 1; index >= 0; index -= 1)
        {
            var recent = _events[index];
            recent.TicksRemaining -= 1;
            if (recent.TicksRemaining <= 0)
            {
                _events.RemoveAt(index);
            }
        }
    }

    internal bool ShouldSuppress(WorldSoundEvent soundEvent)
    {
        if (!IsGibSound(soundEvent))
        {
            return false;
        }

        var isNetworkEvent = soundEvent.EventId != 0;
        for (var index = 0; index < _events.Count; index += 1)
        {
            var recent = _events[index];
            if (recent.IsNetworkEvent == isNetworkEvent)
            {
                continue;
            }

            var deltaX = soundEvent.X - recent.X;
            var deltaY = soundEvent.Y - recent.Y;
            if ((deltaX * deltaX) + (deltaY * deltaY) <= Game1.RecentGibSoundEchoDistanceSquared)
            {
                return true;
            }
        }

        return false;
    }

    internal void RecordPlayback(WorldSoundEvent soundEvent, bool playbackSucceeded)
    {
        if (!playbackSucceeded || !IsGibSound(soundEvent))
        {
            return;
        }

        while (_events.Count >= Game1.RecentGibSoundEchoLimit)
        {
            _events.RemoveAt(0);
        }

        _events.Add(new RecentGibSoundEvent(
            soundEvent.X,
            soundEvent.Y,
            soundEvent.EventId != 0,
            Game1.RecentGibSoundEchoLifetimeTicks));
    }

    internal void Reset() => _events.Clear();

    private static bool IsGibSound(WorldSoundEvent soundEvent)
    {
        return string.Equals(soundEvent.SoundName, "Gibbing", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class RecentGibSoundEvent(float x, float y, bool isNetworkEvent, int ticksRemaining)
    {
        public float X { get; } = x;
        public float Y { get; } = y;
        public bool IsNetworkEvent { get; } = isNetworkEvent;
        public int TicksRemaining { get; set; } = ticksRemaining;
    }
}
