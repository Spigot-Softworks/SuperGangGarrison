namespace OpenGarrison.Protocol;

public sealed record SnapshotDamageableZoneState(ushort Index, float Health, uint Revision, int StateAgeTicks);
