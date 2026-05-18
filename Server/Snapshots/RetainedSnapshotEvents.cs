using OpenGarrison.Protocol;

sealed record RetainedSnapshotSoundEvent(SnapshotSoundEvent Event, ulong ExpiresAfterFrame);

sealed record RetainedSnapshotVisualEvent(SnapshotVisualEvent Event, ulong ExpiresAfterFrame);

sealed record RetainedSnapshotDamageEvent(SnapshotDamageEvent Event, ulong ExpiresAfterFrame);

sealed record RetainedSnapshotGibSpawnEvent(SnapshotGibSpawnEvent Event, ulong ExpiresAfterFrame);

sealed record RetainedSnapshotRocketSpawnEvent(SnapshotRocketSpawnEvent Event, ulong ExpiresAfterFrame);
