using OpenGarrison.Protocol;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class ProtocolCodecTests
{
    [Fact]
    public void MeasureSerializedSizeMatchesSerializedSnapshotPayloadLength()
    {
        var snapshot = new SnapshotMessage(
            Frame: 101,
            TickRate: 60,
            LevelName: "ctf_test",
            MapAreaIndex: 1,
            MapAreaCount: 1,
            GameMode: 1,
            MatchPhase: 1,
            WinnerTeam: 0,
            TimeRemainingTicks: 600,
            RedCaps: 1,
            BlueCaps: 2,
            SpectatorCount: 1,
            LastProcessedInputSequence: 77,
            RedIntel: new SnapshotIntelState(1, 16f, 24f, true, false, 0),
            BlueIntel: new SnapshotIntelState(2, 32f, 48f, false, true, 45),
            Players:
            [
                new SnapshotPlayerState(
                    Slot: 1,
                    PlayerId: 5,
                    Name: "Scout",
                    Team: 1,
                    ClassId: 1,
                    IsAlive: true,
                    IsAwaitingJoin: false,
                    IsSpectator: false,
                    RespawnTicks: 0,
                    X: 100f,
                    Y: 150f,
                    HorizontalSpeed: 2f,
                    VerticalSpeed: -1f,
                    Health: 110,
                    MaxHealth: 125,
                    Ammo: 5,
                    MaxAmmo: 6,
                    Kills: 3,
                    Deaths: 1,
                    Caps: 2,
                    Points: 11.5f,
                    HealPoints: 7,
                    ActiveDominationCount: 1,
                    IsDominatingLocalViewer: false,
                    IsDominatedByLocalViewer: false,
                    Metal: 0f,
                    IsGrounded: true,
                    RemainingAirJumps: 1,
                    IsCarryingIntel: false,
                    IntelRechargeTicks: 0f,
                    IsSpyCloaked: false,
                    SpyCloakAlpha: 1f,
                    IsSpySuperjumping: false,
                    SpySuperjumpHorizontalVelocity: 0f,
                    SpySuperjumpCooldownTicksRemaining: 0,
                    SpyBackstabVisualTicksRemaining: 0,
                    IsUbered: false,
                    IsKritzCritBoosted: false,
                    IsHeavyEating: false,
                    HeavyEatTicksRemaining: 0,
                    IsSniperScoped: false,
                    SniperChargeTicks: 0,
                    FacingDirectionX: 1f,
                    AimDirectionDegrees: 15f,
                    IsTaunting: false,
                    TauntFrameIndex: 0f,
                    IsChatBubbleVisible: true,
                    ChatBubbleFrameIndex: 3,
                    ChatBubbleAlpha: 0.5f,
                    Assists: 2,
                    BadgeMask: 123UL,
                    GameplayModPackId: "stock",
                    GameplayLoadoutId: "default",
                    GameplayPrimaryItemId: "scattergun",
                    GameplaySecondaryItemId: "pistol",
                    GameplayUtilityItemId: "bat",
                    GameplayEquippedSlot: 1,
                    GameplayEquippedItemId: "scattergun",
                    GameplayAcquiredItemId: "scattergun",
                    OwnedGameplayItemIds: ["scattergun", "pistol"],
                    ReplicatedStates:
                    [
                        new SnapshotReplicatedStateEntry("plugin.example", "charge", SnapshotReplicatedStateValueKind.Scalar, IntValue: 0, FloatValue: 0.25f, BoolValue: false),
                    ],
                    PlayerScale: 1f,
                    MedicHealTargetId: 9,
                    IsMedicHealing: true),
            ],
            CombatTraces: [new SnapshotCombatTraceState(0f, 0f, 8f, 8f, 2, true, 1, false)],
            Sentries: Array.Empty<SnapshotSentryState>(),
            Shots: [new SnapshotShotState(8, 1, 5, 100f, 120f, 10f, 0f, 15)],
            Bubbles: Array.Empty<SnapshotShotState>(),
            Blades: Array.Empty<SnapshotShotState>(),
            Needles: Array.Empty<SnapshotShotState>(),
            RevolverShots: Array.Empty<SnapshotShotState>(),
            Rockets: [new SnapshotRocketState(9, 1, 5, 100f, 120f, 96f, 120f, 0.2f, 240f, 20)],
            Flames: Array.Empty<SnapshotFlameState>(),
            Flares: Array.Empty<SnapshotShotState>(),
            Mines: Array.Empty<SnapshotMineState>(),
            DeadBodies: Array.Empty<SnapshotDeadBodyState>(),
            ControlPointSetupTicksRemaining: 0,
            KothUnlockTicksRemaining: 0,
            KothRedTimerTicksRemaining: 0,
            KothBlueTimerTicksRemaining: 0,
            ControlPoints: [new SnapshotControlPointState(0, 1, 0, 0, 120, 1, false)],
            Generators: [new SnapshotGeneratorState(1, 75, 100)],
            LocalDeathCam: null,
            KillFeed: [new SnapshotKillFeedEntry("Scout", 1, "scattergun", "Pyro", 2, "Scout fragged Pyro", 0, 5, 5, 6)],
            VisualEvents: [new SnapshotVisualEvent("spark", 10f, 20f, 45f, 1, 55)],
            DamageEvents: [new SnapshotDamageEvent(45, 5, -1, 1, 6, 10f, 20f, false, 66, 101)],
            SoundEvents: [new SnapshotSoundEvent("rocket_fire", 11f, 21f, 77, 101)],
            IsCustomMap: true,
            MapDownloadUrl: "https://example.invalid/map.zip",
            MapContentHash: "deadbeef",
            MapScale: 1.25f)
        {
            BaselineFrame = 100,
            IsDelta = true,
            PlayerMovementStates =
            [
                new SnapshotPlayerMovementState(
                    1,
                    112f,
                    151f,
                    3f,
                    0f,
                    true,
                    1,
                    1f,
                    22f,
                    1,
                    true,
                    4f,
                    5f,
                    MedicHealTargetId: 9,
                    IsMedicHealing: true)
            ],
            PlayerStatusStates = [new SnapshotPlayerStatusState(1, 95, 125, 4, 6, 15f, false, 0f)],
            PlayerChatBubbleStates = [new SnapshotPlayerChatBubbleState(1, true, 49, 0.75f)],
            RemovedShotIds = [2, 4, 6],
            SentryGibs = [new SnapshotSentryGibState(3, 1, 10f, 12f, 25)],
            RemovedSentryGibIds = [3],
        };

        var payload = ProtocolCodec.Serialize(snapshot);
        var measuredSizePayload = ProtocolCodec.Serialize(snapshot, ProtocolCodec.MeasureSerializedSize(snapshot));

        Assert.Equal(payload.Length, ProtocolCodec.MeasureSerializedSize(snapshot));
        Assert.Equal(payload, measuredSizePayload);
        Assert.True(ProtocolCodec.TryDeserialize(payload, out var roundTripped));
        var roundTrippedSnapshot = Assert.IsType<SnapshotMessage>(roundTripped);
        var playerMovement = Assert.Single(roundTrippedSnapshot.PlayerMovementStates);
        var playerStatus = Assert.Single(roundTrippedSnapshot.PlayerStatusStates);
        var chatBubbleState = Assert.Single(roundTrippedSnapshot.PlayerChatBubbleStates);
        Assert.Equal(112f, playerMovement.X);
        Assert.InRange(playerMovement.AimDirectionDegrees, 21.99f, 22.01f);
        Assert.Equal(9, playerMovement.MedicHealTargetId);
        Assert.True(playerMovement.IsMedicHealing);
        Assert.True(playerMovement.IsTaunting);
        Assert.Equal(5f, playerMovement.BurnIntensity);
        Assert.Equal(95, playerStatus.Health);
        Assert.Equal(49, chatBubbleState.ChatBubbleFrameIndex);
        Assert.InRange(chatBubbleState.ChatBubbleAlpha, 0.74f, 0.76f);
        var player = Assert.Single(roundTrippedSnapshot.Players);
        Assert.Equal(9, player.MedicHealTargetId);
        Assert.True(player.IsMedicHealing);
    }

    [Fact]
    public void SnapshotDeltaMergesMedicBeamStateFromMovementUpdate()
    {
        var baseline = new SnapshotMessage(
            Frame: 50,
            TickRate: 30,
            LevelName: "ctf_test",
            MapAreaIndex: 1,
            MapAreaCount: 1,
            GameMode: 1,
            MatchPhase: 1,
            WinnerTeam: 0,
            TimeRemainingTicks: 0,
            RedCaps: 0,
            BlueCaps: 0,
            SpectatorCount: 0,
            LastProcessedInputSequence: 0,
            RedIntel: new SnapshotIntelState(1, 0f, 0f, true, false, 0),
            BlueIntel: new SnapshotIntelState(2, 0f, 0f, true, false, 0),
            Players:
            [
                new SnapshotPlayerState(
                    Slot: 1,
                    PlayerId: 5,
                    Name: "Medic",
                    Team: 1,
                    ClassId: (byte)OpenGarrison.Core.PlayerClass.Medic,
                    IsAlive: true,
                    IsAwaitingJoin: false,
                    IsSpectator: false,
                    RespawnTicks: 0,
                    X: 50f,
                    Y: 75f,
                    HorizontalSpeed: 0f,
                    VerticalSpeed: 0f,
                    Health: 150,
                    MaxHealth: 150,
                    Ammo: 40,
                    MaxAmmo: 40,
                    Kills: 0,
                    Deaths: 0,
                    Caps: 0,
                    Points: 0f,
                    HealPoints: 0,
                    ActiveDominationCount: 0,
                    IsDominatingLocalViewer: false,
                    IsDominatedByLocalViewer: false,
                    Metal: 0f,
                    IsGrounded: true,
                    RemainingAirJumps: 1,
                    IsCarryingIntel: false,
                    IntelRechargeTicks: 0f,
                    IsSpyCloaked: false,
                    SpyCloakAlpha: 1f,
                    IsSpySuperjumping: false,
                    SpySuperjumpHorizontalVelocity: 0f,
                    SpySuperjumpCooldownTicksRemaining: 0,
                    SpyBackstabVisualTicksRemaining: 0,
                    IsUbered: false,
                    IsKritzCritBoosted: false,
                    IsHeavyEating: false,
                    HeavyEatTicksRemaining: 0,
                    IsSniperScoped: false,
                    SniperChargeTicks: 0,
                    FacingDirectionX: 1f,
                    AimDirectionDegrees: 0f,
                    IsTaunting: false,
                    TauntFrameIndex: 0f,
                    IsChatBubbleVisible: false,
                    ChatBubbleFrameIndex: 0,
                    ChatBubbleAlpha: 0f,
                    IsMedicHealing: false,
                    MedicHealTargetId: -1),
            ],
            CombatTraces: [],
            Sentries: [],
            Shots: [],
            Bubbles: [],
            Blades: [],
            Needles: [],
            RevolverShots: [],
            Rockets: [],
            Flames: [],
            Flares: [],
            Mines: [],
            DeadBodies: [],
            ControlPointSetupTicksRemaining: 0,
            KothUnlockTicksRemaining: 0,
            KothRedTimerTicksRemaining: 0,
            KothBlueTimerTicksRemaining: 0,
            ControlPoints: [],
            Generators: [],
            LocalDeathCam: null,
            KillFeed: [],
            VisualEvents: [],
            DamageEvents: [],
            SoundEvents: []);

        var delta = baseline with
        {
            Frame = 51,
            BaselineFrame = 50,
            IsDelta = true,
            Players = [],
            PlayerMovementStates = [new SnapshotPlayerMovementState(1, 50f, 75f, 0f, 0f, true, 1, 1f, 0f, 0, false, 0f, 0f, MedicHealTargetId: 8, IsMedicHealing: true)],
        };

        var merged = SnapshotDelta.ToFullSnapshot(delta, baseline);
        var player = Assert.Single(merged.Players);
        Assert.Equal(8, player.MedicHealTargetId);
        Assert.True(player.IsMedicHealing);
    }
}
