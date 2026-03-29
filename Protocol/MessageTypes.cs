using System;
using System.Collections.Generic;

namespace OpenGarrison.Protocol;

public enum MessageType : byte
{
    Hello = 1,
    Welcome = 2,
    InputState = 3,
    Snapshot = 4,
    ControlCommand = 5,
    ControlAck = 6,
    ConnectionDenied = 7,
    SessionSlotChanged = 8,
    ServerStatusRequest = 9,
    ServerStatusResponse = 10,
    PasswordRequest = 11,
    PasswordSubmit = 12,
    PasswordResult = 13,
    AutoBalanceNotice = 14,
    ChatSubmit = 15,
    ChatRelay = 16,
    SnapshotAck = 17,
}

public enum ControlCommandKind : byte
{
    SelectTeam = 1,
    SelectClass = 2,
    Spectate = 3,
}

[Flags]
public enum InputButtons : ushort
{
    None = 0,
    Left = 1 << 0,
    Right = 1 << 1,
    Up = 1 << 2,
    Down = 1 << 3,
    BuildSentry = 1 << 4,
    Taunt = 1 << 5,
    FirePrimary = 1 << 6,
    FireSecondary = 1 << 7,
    DebugKill = 1 << 8,
    DestroySentry = 1 << 9,
    DropIntel = 1 << 10,
}

public interface IProtocolMessage
{
    MessageType Type { get; }
}

public sealed record HelloMessage(string Name, int Version) : IProtocolMessage
{
    public MessageType Type => MessageType.Hello;
}

public sealed record WelcomeMessage(
    string ServerName,
    int Version,
    int TickRate,
    string LevelName,
    byte PlayerSlot,
    bool IsCustomMap = false,
    string MapDownloadUrl = "",
    string MapContentHash = "") : IProtocolMessage
{
    public MessageType Type => MessageType.Welcome;
}

public sealed record ConnectionDeniedMessage(string Reason) : IProtocolMessage
{
    public MessageType Type => MessageType.ConnectionDenied;
}

public sealed record PasswordRequestMessage : IProtocolMessage
{
    public MessageType Type => MessageType.PasswordRequest;
}

public sealed record PasswordSubmitMessage(string Password) : IProtocolMessage
{
    public MessageType Type => MessageType.PasswordSubmit;
}

public sealed record PasswordResultMessage(bool Accepted, string Reason) : IProtocolMessage
{
    public MessageType Type => MessageType.PasswordResult;
}

public sealed record ChatSubmitMessage(string Text, bool TeamOnly = false) : IProtocolMessage
{
    public MessageType Type => MessageType.ChatSubmit;
}

public sealed record ChatRelayMessage(
    byte Team,
    string PlayerName,
    string Text,
    bool TeamOnly = false) : IProtocolMessage
{
    public MessageType Type => MessageType.ChatRelay;
}

public enum AutoBalanceNoticeKind : byte
{
    Pending = 1,
    Applied = 2,
}

public sealed record AutoBalanceNoticeMessage(
    AutoBalanceNoticeKind Kind,
    string PlayerName,
    byte FromTeam,
    byte ToTeam,
    int DelaySeconds) : IProtocolMessage
{
    public MessageType Type => MessageType.AutoBalanceNotice;
}

public sealed record SessionSlotChangedMessage(byte PlayerSlot) : IProtocolMessage
{
    public MessageType Type => MessageType.SessionSlotChanged;
}

public sealed record ServerStatusRequestMessage : IProtocolMessage
{
    public MessageType Type => MessageType.ServerStatusRequest;
}

public sealed record ServerStatusResponseMessage(
    string ServerName,
    string LevelName,
    byte GameMode,
    int PlayerCount,
    int MaxPlayerCount,
    int SpectatorCount) : IProtocolMessage
{
    public MessageType Type => MessageType.ServerStatusResponse;
}

public sealed record InputStateMessage(
    uint Sequence,
    InputButtons Buttons,
    float AimWorldX,
    float AimWorldY,
    int ChatBubbleFrameIndex) : IProtocolMessage
{
    public MessageType Type => MessageType.InputState;
}

public sealed record ControlCommandMessage(
    uint Sequence,
    ControlCommandKind Kind,
    byte Value) : IProtocolMessage
{
    public MessageType Type => MessageType.ControlCommand;
}

public sealed record ControlAckMessage(
    uint Sequence,
    ControlCommandKind Kind,
    bool Accepted) : IProtocolMessage
{
    public MessageType Type => MessageType.ControlAck;
}

public sealed record SnapshotAckMessage(ulong Frame) : IProtocolMessage
{
    public MessageType Type => MessageType.SnapshotAck;
}

public sealed record SnapshotPlayerState(
    byte Slot,
    int PlayerId,
    string Name,
    byte Team,
    byte ClassId,
    bool IsAlive,
    bool IsAwaitingJoin,
    bool IsSpectator,
    int RespawnTicks,
    float X,
    float Y,
    float HorizontalSpeed,
    float VerticalSpeed,
    short Health,
    short MaxHealth,
    short Ammo,
    short MaxAmmo,
    short Kills,
    short Deaths,
    short Caps,
    short HealPoints,
    short ActiveDominationCount,
    bool IsDominatingLocalViewer,
    bool IsDominatedByLocalViewer,
    float Metal,
    bool IsGrounded,
    int RemainingAirJumps,
    bool IsCarryingIntel,
    float IntelRechargeTicks,
    bool IsSpyCloaked,
    float SpyCloakAlpha,
    bool IsUbered,
    bool IsHeavyEating,
    int HeavyEatTicksRemaining,
    bool IsSniperScoped,
    int SniperChargeTicks,
    float FacingDirectionX,
    float AimDirectionDegrees,
    bool IsTaunting,
    float TauntFrameIndex,
    bool IsChatBubbleVisible,
    int ChatBubbleFrameIndex,
    float ChatBubbleAlpha,
    float BurnIntensity = 0f,
    float BurnDurationSourceTicks = 0f,
    float BurnDecayDelaySourceTicksRemaining = 0f,
    float BurnIntensityDecayPerSourceTick = 0f,
    int BurnedByPlayerId = -1,
    byte MovementState = 0,
    int PrimaryCooldownTicks = 0,
    int ReloadTicksUntilNextShell = 0,
    int MedicNeedleCooldownTicks = 0,
    int MedicNeedleRefillTicks = 0,
    int PyroAirblastCooldownTicks = 0,
    int PyroFlareCooldownTicks = 0,
    int PyroPrimaryFuelScaled = 0,
    bool IsPyroPrimaryRefilling = false,
    int PyroFlameLoopTicksRemaining = 0,
    bool PyroPrimaryRequiresReleaseAfterEmpty = false,
    int HeavyEatCooldownTicksRemaining = 0);

public sealed record SnapshotIntelState(
    byte Team,
    float X,
    float Y,
    bool IsAtBase,
    bool IsDropped,
    int ReturnTicksRemaining);

public sealed record SnapshotSentryState(
    int Id,
    int OwnerPlayerId,
    byte Team,
    float X,
    float Y,
    int Health,
    bool IsBuilt,
    float FacingDirectionX,
    float DesiredFacingDirectionX,
    float AimDirectionDegrees,
    int ReloadTicksRemaining,
    int AlertTicksRemaining,
    int ShotTraceTicksRemaining,
    bool HasLanded,
    bool HasActiveTarget,
    int CurrentTargetPlayerId,
    float LastShotTargetX,
    float LastShotTargetY);

public sealed record SnapshotShotState(
    int Id,
    byte Team,
    int OwnerId,
    float X,
    float Y,
    float VelocityX,
    float VelocityY,
    int TicksRemaining);

public sealed record SnapshotRocketState(
    int Id,
    byte Team,
    int OwnerId,
    float X,
    float Y,
    float PreviousX,
    float PreviousY,
    float DirectionRadians,
    float Speed,
    int TicksRemaining,
    float ReducedKnockbackSourceTicksRemaining = 20f,
    float ZeroKnockbackSourceTicksRemaining = 30f,
    int RangeAnchorOwnerId = -1,
    float LastKnownRangeOriginX = 0f,
    float LastKnownRangeOriginY = 0f,
    float DistanceToTravel = 800f,
    bool IsFading = false,
    float FadeSourceTicksRemaining = 0f,
    IReadOnlyList<int>? PassedFriendlyPlayerIds = null);

public sealed record SnapshotFlameState(
    int Id,
    byte Team,
    int OwnerId,
    float X,
    float Y,
    float PreviousX,
    float PreviousY,
    float VelocityX,
    float VelocityY,
    int TicksRemaining,
    int AttachedPlayerId,
    float AttachedOffsetX,
    float AttachedOffsetY);

public sealed record SnapshotMineState(
    int Id,
    byte Team,
    int OwnerId,
    float X,
    float Y,
    float VelocityX,
    float VelocityY,
    bool IsStickied,
    bool IsDestroyed,
    float ExplosionDamage);

public sealed record SnapshotControlPointState(
    byte Index,
    byte Team,
    byte CappingTeam,
    ushort CappingTicks,
    ushort CapTimeTicks,
    byte Cappers,
    bool IsLocked);

public sealed record SnapshotGeneratorState(
    byte Team,
    short Health,
    short MaxHealth);

public sealed record SnapshotDeadBodyState(
    int Id,
    byte Team,
    byte ClassId,
    float X,
    float Y,
    float Width,
    float Height,
    float HorizontalSpeed,
    float VerticalSpeed,
    bool FacingLeft,
    int TicksRemaining);

public sealed record SnapshotSentryGibState(
    int Id,
    byte Team,
    float X,
    float Y,
    int TicksRemaining);

public sealed record SnapshotPlayerGibState(
    int Id,
    string SpriteName,
    int FrameIndex,
    float X,
    float Y,
    float VelocityX,
    float VelocityY,
    float RotationDegrees,
    float RotationSpeedDegrees,
    int TicksRemaining,
    float BloodChance);

public sealed record SnapshotBloodDropState(
    int Id,
    float X,
    float Y,
    float VelocityX,
    float VelocityY,
    bool IsStuck,
    int TicksRemaining,
    float Scale);

public sealed record SnapshotDeathCamState(
    float FocusX,
    float FocusY,
    string KillMessage,
    string KillerName,
    byte KillerTeam,
    int Health,
    int MaxHealth,
    int RemainingTicks,
    int InitialTicks = 0);

public sealed record SnapshotCombatTraceState(
    float StartX,
    float StartY,
    float EndX,
    float EndY,
    int TicksRemaining,
    bool HitCharacter,
    byte Team,
    bool IsSniperTracer);

public sealed record SnapshotSoundEvent(
    string SoundName,
    float X,
    float Y,
    ulong EventId = 0,
    ulong SourceFrame = 0);

public sealed record SnapshotVisualEvent(
    string EffectName,
    float X,
    float Y,
    float DirectionDegrees,
    int Count,
    ulong EventId = 0);

public sealed record SnapshotDamageEvent(
    int Amount,
    int AttackerPlayerId,
    int AssistedByPlayerId,
    byte TargetKind,
    int TargetEntityId,
    float X,
    float Y,
    bool WasFatal,
    ulong EventId = 0,
    ulong SourceFrame = 0);

public sealed record SnapshotKillFeedEntry(
    string KillerName,
    byte KillerTeam,
    string WeaponSpriteName,
    string VictimName,
    byte VictimTeam,
    string MessageText = "",
    int KillerPlayerId = -1,
    int VictimPlayerId = -1,
    KillFeedSpecialType SpecialType = KillFeedSpecialType.None,
    ulong EventId = 0);

public sealed record SnapshotMessage(
    ulong Frame,
    int TickRate,
    string LevelName,
    byte MapAreaIndex,
    byte MapAreaCount,
    byte GameMode,
    byte MatchPhase,
    byte WinnerTeam,
    int TimeRemainingTicks,
    int RedCaps,
    int BlueCaps,
    int SpectatorCount,
    uint LastProcessedInputSequence,
    SnapshotIntelState RedIntel,
    SnapshotIntelState BlueIntel,
    IReadOnlyList<SnapshotPlayerState> Players,
    IReadOnlyList<SnapshotCombatTraceState> CombatTraces,
    IReadOnlyList<SnapshotSentryState> Sentries,
    IReadOnlyList<SnapshotShotState> Shots,
    IReadOnlyList<SnapshotShotState> Bubbles,
    IReadOnlyList<SnapshotShotState> Blades,
    IReadOnlyList<SnapshotShotState> Needles,
    IReadOnlyList<SnapshotShotState> RevolverShots,
    IReadOnlyList<SnapshotRocketState> Rockets,
    IReadOnlyList<SnapshotFlameState> Flames,
    IReadOnlyList<SnapshotShotState> Flares,
    IReadOnlyList<SnapshotMineState> Mines,
    IReadOnlyList<SnapshotPlayerGibState> PlayerGibs,
    IReadOnlyList<SnapshotBloodDropState> BloodDrops,
    IReadOnlyList<SnapshotDeadBodyState> DeadBodies,
    int ControlPointSetupTicksRemaining,
    IReadOnlyList<SnapshotControlPointState> ControlPoints,
    IReadOnlyList<SnapshotGeneratorState> Generators,
    SnapshotDeathCamState? LocalDeathCam,
    IReadOnlyList<SnapshotKillFeedEntry> KillFeed,
    IReadOnlyList<SnapshotVisualEvent> VisualEvents,
    IReadOnlyList<SnapshotDamageEvent> DamageEvents,
    IReadOnlyList<SnapshotSoundEvent> SoundEvents,
    bool IsCustomMap = false,
    string MapDownloadUrl = "",
    string MapContentHash = "") : IProtocolMessage
{
    public ulong BaselineFrame { get; init; }
    public bool IsDelta { get; init; }
    public IReadOnlyList<int> RemovedSentryIds { get; init; } = Array.Empty<int>();
    public IReadOnlyList<int> RemovedShotIds { get; init; } = Array.Empty<int>();
    public IReadOnlyList<int> RemovedBubbleIds { get; init; } = Array.Empty<int>();
    public IReadOnlyList<int> RemovedBladeIds { get; init; } = Array.Empty<int>();
    public IReadOnlyList<int> RemovedNeedleIds { get; init; } = Array.Empty<int>();
    public IReadOnlyList<int> RemovedRevolverShotIds { get; init; } = Array.Empty<int>();
    public IReadOnlyList<int> RemovedRocketIds { get; init; } = Array.Empty<int>();
    public IReadOnlyList<int> RemovedFlameIds { get; init; } = Array.Empty<int>();
    public IReadOnlyList<int> RemovedFlareIds { get; init; } = Array.Empty<int>();
    public IReadOnlyList<int> RemovedMineIds { get; init; } = Array.Empty<int>();
    public IReadOnlyList<int> RemovedPlayerGibIds { get; init; } = Array.Empty<int>();
    public IReadOnlyList<int> RemovedBloodDropIds { get; init; } = Array.Empty<int>();
    public IReadOnlyList<int> RemovedDeadBodyIds { get; init; } = Array.Empty<int>();
    public IReadOnlyList<int> RemovedSentryGibIds { get; init; } = Array.Empty<int>();
    public IReadOnlyList<SnapshotSentryGibState> SentryGibs { get; init; } = Array.Empty<SnapshotSentryGibState>();

    public MessageType Type => MessageType.Snapshot;
}

