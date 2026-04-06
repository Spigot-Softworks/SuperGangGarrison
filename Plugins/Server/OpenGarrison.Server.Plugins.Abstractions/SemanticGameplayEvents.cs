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

public interface IOpenGarrisonServerSemanticGameplayHooks
{
    void OnDamage(OpenGarrisonServerDamageEvent e);

    void OnDeath(OpenGarrisonServerDeathEvent e);

    void OnAssist(OpenGarrisonServerAssistEvent e);

    void OnBuild(OpenGarrisonServerBuildableEvent e);

    void OnDestroy(OpenGarrisonServerBuildableEvent e);

    void OnIntelEvent(OpenGarrisonServerIntelEvent e);

    void OnControlPointStateChanged(OpenGarrisonServerControlPointStateEvent e);

    void OnPlayerSpawned(OpenGarrisonServerPlayerSpawnEvent e);
}
