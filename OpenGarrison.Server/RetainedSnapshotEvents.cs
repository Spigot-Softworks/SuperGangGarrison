using OpenGarrison.Protocol;

sealed record RetainedSnapshotSoundEvent(SnapshotSoundEvent Event, ulong ExpiresAfterFrame);

sealed record RetainedSnapshotVisualEvent(SnapshotVisualEvent Event, ulong ExpiresAfterFrame);

sealed record RetainedSnapshotDamageEvent(SnapshotDamageEvent Event, ulong ExpiresAfterFrame);
