using OpenGarrison.Protocol;

namespace OpenGarrison.Core;

/// <summary>
/// Owns the per-slot state for network players: who occupies each slot, their pending
/// input and team choice, respawn timers, and server-applied tuning overrides.
/// Slot 1 is the local player; <c>PlayersBySlot</c> holds every additional slot.
/// </summary>
internal sealed class NetworkPlayerRegistry
{
    // Occupancy and lookup
    public Dictionary<byte, PlayerEntity> PlayersBySlot { get; } = new();
    public Dictionary<int, PlayerEntity> ActivePlayersById { get; } = new();
    public Dictionary<int, byte> SlotsByPlayerId { get; } = new();

    // Enabled slots stay ordered so hot-path player enumeration keeps slot order
    // without scanning every possible network slot.
    public SortedSet<byte> EnabledAdditionalSlots { get; } = new();

    // Class, team, and join state
    public Dictionary<byte, CharacterClassDefinition> ClassDefinitions { get; } = new();
    public Dictionary<byte, PlayerTeam> Teams { get; } = new();
    public HashSet<byte> PendingTeamSelections { get; } = new();
    public Dictionary<byte, bool> AwaitingJoin { get; } = new();
    public HashSet<byte> BotSlots { get; } = new();

    // Input
    public Dictionary<byte, PlayerInputSnapshot> Inputs { get; } = new();
    public Dictionary<byte, PlayerInputSnapshot> PreviousInputs { get; } = new();
    public Dictionary<byte, (InputButtons Buttons, bool ExplicitOnly)> ForcedPressedButtons { get; } = new();

    // Respawn and spawn behavior
    public Dictionary<byte, int> RespawnTicks { get; } = new();
    public HashSet<byte> AutomaticRespawnSuppressedSlots { get; } = new();
    public Dictionary<byte, SpawnPoint> SpawnOverrides { get; } = new();
    public HashSet<byte> MapSpawnClassBehaviorBypassSlots { get; } = new();
    public Dictionary<byte, LocalDeathCamState> DeathCams { get; } = new();

    // Server-applied tuning overrides
    public Dictionary<byte, float> MovementSpeedScaleOverrides { get; } = new();
    public Dictionary<byte, float> LastToDieEnemyDamageScaleOverrides { get; } = new();
    public Dictionary<byte, float> GravityScaleOverrides { get; } = new();
    public Dictionary<byte, int> MaxHealthOverrides { get; } = new();

    // Reported connection quality
    public Dictionary<byte, int> PingMillisecondsBySlot { get; } = new();
}

/// <summary>
/// Owns the client-side mirrors of remote players built from authoritative snapshots,
/// including the bookkeeping used to detect players that have left.
/// </summary>
internal sealed class RemoteSnapshotPlayerRegistry
{
    public List<PlayerEntity> Players { get; } = new();
    public List<PlayerEntity> ScoreboardPlayers { get; } = new();
    public Dictionary<byte, PlayerEntity> PlayersBySlot { get; } = new();
    public Dictionary<byte, PlayerEntity> ScoreboardPlayersBySlot { get; } = new();

    public HashSet<byte> AwaitingJoinSlots { get; } = new();
    public HashSet<int> AwaitingJoinPlayerIds { get; } = new();

    // Per-snapshot scratch used to find slots that were not refreshed.
    public HashSet<byte> SeenSlots { get; } = new();
    public List<byte> StaleSlots { get; } = new();
}
