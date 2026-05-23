using OpenGarrison.Core;

namespace OpenGarrison.Server.Plugins;

public enum OpenGarrisonServerBuildableKind : byte
{
    Unknown = 0,
    Sentry = 1,
    Generator = 2,
}

public enum OpenGarrisonServerIntelEventKind : byte
{
    Unknown = 0,
    PickedUp = 1,
    Dropped = 2,
    Returned = 3,
    Captured = 4,
}

public readonly record struct OpenGarrisonServerDamageEvent(
    long Frame,
    int Amount,
    DamageTargetKind TargetKind,
    int TargetEntityId,
    bool WasFatal,
    int AttackerPlayerId,
    string AttackerName,
    PlayerTeam? AttackerTeam,
    int AssistedByPlayerId,
    string AssistedByName,
    PlayerTeam? AssistedByTeam,
    int VictimPlayerId,
    string VictimName,
    PlayerTeam? VictimTeam,
    float WorldX,
    float WorldY,
    DamageEventFlags Flags);

public readonly record struct OpenGarrisonServerDeathEvent(
    long Frame,
    int VictimPlayerId,
    string VictimName,
    PlayerTeam VictimTeam,
    int KillerPlayerId,
    string KillerName,
    PlayerTeam? KillerTeam,
    int AssistedByPlayerId,
    string AssistedByName,
    PlayerTeam? AssistedByTeam,
    string WeaponSpriteName,
    string MessageText);

public readonly record struct OpenGarrisonServerAssistEvent(
    long Frame,
    int AssistantPlayerId,
    string AssistantName,
    PlayerTeam AssistantTeam,
    int KillerPlayerId,
    string KillerName,
    PlayerTeam KillerTeam,
    int VictimPlayerId,
    string VictimName,
    PlayerTeam VictimTeam,
    string WeaponSpriteName);

public readonly record struct OpenGarrisonServerBuildableEvent(
    long Frame,
    OpenGarrisonServerBuildableKind Kind,
    int EntityId,
    int OwnerPlayerId,
    string OwnerName,
    PlayerTeam? Team,
    float WorldX,
    float WorldY);

public readonly record struct OpenGarrisonServerIntelEvent(
    long Frame,
    OpenGarrisonServerIntelEventKind Kind,
    PlayerTeam IntelTeam,
    PlayerTeam ActingTeam,
    int ActingPlayerId,
    string ActingPlayerName,
    float WorldX,
    float WorldY);

public readonly record struct OpenGarrisonServerControlPointStateEvent(
    long Frame,
    int Index,
    PlayerTeam? Team,
    PlayerTeam? CappingTeam,
    int Cappers,
    float Progress,
    bool IsLocked,
    float WorldX,
    float WorldY);

public readonly record struct OpenGarrisonServerPlayerSpawnEvent(
    long Frame,
    byte Slot,
    int PlayerId,
    string PlayerName,
    PlayerTeam Team,
    PlayerClass PlayerClass,
    float WorldX,
    float WorldY,
    bool IsRespawn);

public readonly record struct OpenGarrisonServerPlayerJoinedEvent(
    long Frame,
    byte Slot,
    string PlayerName,
    string EndPoint,
    bool IsAuthorized,
    bool IsSpectator);

public readonly record struct OpenGarrisonServerPlayerLeftEvent(
    long Frame,
    byte Slot,
    string PlayerName,
    string EndPoint,
    string Reason,
    bool WasAuthorized);

public readonly record struct OpenGarrisonServerPlayerRespawnEvent(
    long Frame,
    byte Slot,
    int PlayerId,
    string PlayerName,
    PlayerTeam Team,
    PlayerClass PlayerClass,
    float WorldX,
    float WorldY);

public sealed class OpenGarrisonServerGameplayAbilityInputEvent(
    long frame,
    int playerId,
    PlayerClass classId,
    PlayerTeam team,
    string itemId,
    string behaviorId,
    string abilityCategory,
    string activation,
    string executorId,
    string phase,
    IReadOnlyList<string> tags)
{
    public long Frame { get; } = frame;

    public int PlayerId { get; } = playerId;

    public PlayerClass ClassId { get; } = classId;

    public PlayerTeam Team { get; } = team;

    public string ItemId { get; } = itemId;

    public string BehaviorId { get; } = behaviorId;

    public string AbilityCategory { get; } = abilityCategory;

    public string Activation { get; } = activation;

    public string ExecutorId { get; } = executorId;

    public string Phase { get; } = phase;

    public IReadOnlyList<string> Tags { get; } = tags;

    public bool IsCancelled { get; private set; }

    public void Cancel()
    {
        IsCancelled = true;
    }
}

public readonly record struct OpenGarrisonServerGameplayAbilityUsedEvent(
    long Frame,
    int PlayerId,
    PlayerClass ClassId,
    PlayerTeam Team,
    string ItemId,
    string BehaviorId,
    string AbilityCategory,
    string Activation,
    string ExecutorId,
    string Phase,
    IReadOnlyList<string> Tags,
    bool Handled,
    bool ConsumedInput);

public readonly record struct OpenGarrisonServerGameplayAbilityStateChangedEvent(
    long Frame,
    int PlayerId,
    PlayerClass ClassId,
    PlayerTeam Team,
    string OwnerId,
    string StateKey,
    GameplayReplicatedStateValueKind ValueKind,
    bool HasPreviousValue,
    int PreviousIntValue,
    int CurrentIntValue,
    float PreviousFloatValue,
    float CurrentFloatValue,
    bool PreviousBoolValue,
    bool CurrentBoolValue);

public interface IOpenGarrisonServerSemanticGameplayHooks
{
    void OnDamage(OpenGarrisonServerDamageEvent e) { }

    void OnDeath(OpenGarrisonServerDeathEvent e) { }

    void OnAssist(OpenGarrisonServerAssistEvent e) { }

    void OnBuild(OpenGarrisonServerBuildableEvent e) { }

    void OnDestroy(OpenGarrisonServerBuildableEvent e) { }

    void OnIntelEvent(OpenGarrisonServerIntelEvent e) { }

    void OnControlPointStateChanged(OpenGarrisonServerControlPointStateEvent e) { }

    void OnPlayerJoined(OpenGarrisonServerPlayerJoinedEvent e) { }

    void OnPlayerLeft(OpenGarrisonServerPlayerLeftEvent e) { }

    void OnPlayerSpawned(OpenGarrisonServerPlayerSpawnEvent e) { }

    void OnPlayerRespawned(OpenGarrisonServerPlayerRespawnEvent e) { }

    void OnGameplayAbilityInput(OpenGarrisonServerGameplayAbilityInputEvent e) { }

    void OnGameplayAbilityUsed(OpenGarrisonServerGameplayAbilityUsedEvent e) { }

    void OnGameplayAbilityStateChanged(OpenGarrisonServerGameplayAbilityStateChangedEvent e) { }
}
