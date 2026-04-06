using Microsoft.Xna.Framework;

namespace OpenGarrison.Client.Plugins;

public enum ClientGameplayPickupKind : byte
{
    Unknown = 0,
    Intel = 1,
}

public enum ClientObjectiveEventKind : byte
{
    Unknown = 0,
    Intel = 1,
    ControlPoint = 2,
    Generator = 3,
}

public enum ClientRoundPhase : byte
{
    Unknown = 0,
    Running = 1,
    Ended = 2,
}

public readonly record struct ClientShotFiredEvent(
    int? PlayerId,
    ClientPluginClass PlayerClass,
    Vector2 WorldPosition,
    ulong WorldFrame);

public readonly record struct ClientHitConfirmedEvent(
    int Amount,
    DamageTargetKind TargetKind,
    int TargetEntityId,
    Vector2 TargetWorldPosition,
    bool WasFatal,
    int AttackerPlayerId,
    int AssistedByPlayerId,
    LocalDamageFlags Flags,
    ulong WorldFrame);

public readonly record struct ClientLocalKillEvent(
    int VictimPlayerId,
    string VictimName,
    ClientPluginTeam VictimTeam,
    string WeaponSpriteName,
    string MessageText,
    ulong WorldFrame);

public readonly record struct ClientLocalDeathEvent(
    int KillerPlayerId,
    string KillerName,
    ClientPluginTeam KillerTeam,
    string WeaponSpriteName,
    string MessageText,
    ulong WorldFrame);

public readonly record struct ClientPickupEvent(
    ClientGameplayPickupKind Kind,
    Vector2 WorldPosition,
    ulong WorldFrame);

public readonly record struct ClientHealEvent(
    int Amount,
    int HealthAfter,
    int MaxHealth,
    ulong WorldFrame);

public readonly record struct ClientIgniteEvent(
    int BurnedByPlayerId,
    float BurnIntensity,
    ulong WorldFrame);

public readonly record struct ClientExtinguishEvent(ulong WorldFrame);

public readonly record struct ClientObjectiveStateEvent(
    ClientObjectiveEventKind Kind,
    int ObjectiveId,
    ClientPluginTeam Team,
    ClientPluginTeam CappingTeam,
    float Progress,
    bool IsLocked,
    Vector2 WorldPosition,
    ulong WorldFrame);

public readonly record struct ClientRoundPhaseChangedEvent(
    ClientRoundPhase PreviousPhase,
    ClientRoundPhase CurrentPhase,
    ulong WorldFrame);

public readonly record struct ClientKillFeedEvent(
    int KillerPlayerId,
    string KillerName,
    ClientPluginTeam KillerTeam,
    int VictimPlayerId,
    string VictimName,
    ClientPluginTeam VictimTeam,
    string WeaponSpriteName,
    string MessageText,
    ulong WorldFrame);

public interface IOpenGarrisonClientSemanticGameplayHooks
{
    void OnShotFired(ClientShotFiredEvent e);

    void OnHitConfirmed(ClientHitConfirmedEvent e);

    void OnLocalKill(ClientLocalKillEvent e);

    void OnLocalDeath(ClientLocalDeathEvent e);

    void OnPickup(ClientPickupEvent e);

    void OnHeal(ClientHealEvent e);

    void OnIgnited(ClientIgniteEvent e);

    void OnExtinguished(ClientExtinguishEvent e);

    void OnObjectiveStateChanged(ClientObjectiveStateEvent e);

    void OnRoundPhaseChanged(ClientRoundPhaseChangedEvent e);

    void OnKillFeed(ClientKillFeedEvent e);
}
