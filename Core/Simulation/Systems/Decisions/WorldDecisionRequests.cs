namespace OpenGarrison.Core;

public readonly record struct WorldDecisionResult(
    bool IsCancelled,
    string Reason = "")
{
    public static WorldDecisionResult Continue { get; } = new(false);

    public static WorldDecisionResult Cancel(string reason = "") => new(true, reason);
}

public readonly record struct WorldSpawnDecisionRequest(
    long Frame,
    byte Slot,
    int PlayerId,
    string PlayerName,
    PlayerTeam Team,
    PlayerClass PlayerClass,
    float WorldX,
    float WorldY,
    bool IsRespawn);

public readonly record struct WorldDamageDecisionRequest(
    long Frame,
    DamageTargetKind TargetKind,
    int TargetEntityId,
    int TargetPlayerId,
    PlayerTeam? TargetTeam,
    int AttackerPlayerId,
    PlayerTeam? AttackerTeam,
    int Amount,
    bool WouldBeFatal,
    float WorldX,
    float WorldY);

public readonly record struct WorldDeathDecisionRequest(
    long Frame,
    byte Slot,
    int VictimPlayerId,
    string VictimName,
    PlayerTeam VictimTeam,
    PlayerClass VictimClass,
    int KillerPlayerId,
    string KillerName,
    PlayerTeam? KillerTeam,
    string WeaponSpriteName,
    bool Gibbed);

public enum WorldPickupKind : byte
{
    HealthPack = 0,
    DroppedWeapon = 1,
    Intelligence = 2,
}

public readonly record struct WorldPickupDecisionRequest(
    long Frame,
    WorldPickupKind Kind,
    byte Slot,
    int PlayerId,
    string PlayerName,
    PlayerTeam Team,
    int PickupEntityId,
    string PickupValue,
    float WorldX,
    float WorldY);

public readonly record struct WorldScoreDecisionRequest(
    long Frame,
    PlayerTeam Team,
    int Delta,
    int RedCaps,
    int BlueCaps,
    int ActorPlayerId,
    string Reason);

public readonly record struct WorldRoundEndDecisionRequest(
    long Frame,
    GameModeKind GameMode,
    PlayerTeam? WinnerTeam,
    int RedCaps,
    int BlueCaps,
    string Reason);
