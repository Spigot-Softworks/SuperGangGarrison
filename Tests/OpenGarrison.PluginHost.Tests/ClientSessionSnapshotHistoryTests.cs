using System.Net;
using OpenGarrison.Protocol;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class ClientSessionSnapshotHistoryTests
{
    [Fact]
    public void SnapshotHistoryIsBoundedWhenNoAcknowledgementsArrive()
    {
        var client = new ClientSession(1, 101, new IPEndPoint(IPAddress.Loopback, 8190), "Tester", TimeSpan.Zero);

        for (ulong frame = 1; frame <= 60; frame += 1)
        {
            client.RememberSnapshotState(CreateSnapshot(frame));
        }

        Assert.Equal(48, client.SnapshotHistoryCount);
        Assert.False(client.TryGetSnapshotState(1, out _));
        Assert.True(client.TryGetSnapshotState(60, out var latestBaseline));
        Assert.Equal((ulong)60, latestBaseline.Frame);
    }

    [Fact]
    public void SnapshotHistoryDropsStaleAcknowledgedBaselineWhenItAgesOut()
    {
        var client = new ClientSession(1, 101, new IPEndPoint(IPAddress.Loopback, 8190), "Tester", TimeSpan.Zero);

        for (ulong frame = 1; frame <= 12; frame += 1)
        {
            client.RememberSnapshotState(CreateSnapshot(frame));
        }

        client.AcknowledgeSnapshot(1);
        Assert.Equal((ulong)1, client.LastAcknowledgedSnapshotFrame);

        for (ulong frame = 13; frame <= 64; frame += 1)
        {
            client.RememberSnapshotState(CreateSnapshot(frame));
        }

        Assert.Equal((ulong)0, client.LastAcknowledgedSnapshotFrame);
        Assert.Equal(48, client.SnapshotHistoryCount);
        Assert.False(client.TryGetSnapshotState(1, out _));
        Assert.True(client.TryGetSnapshotState(64, out var latestBaseline));
        Assert.Equal((ulong)64, latestBaseline.Frame);
    }

    [Fact]
    public void RememberResolvedSnapshotStateRejectsDeltaSnapshots()
    {
        var client = new ClientSession(1, 101, new IPEndPoint(IPAddress.Loopback, 8190), "Tester", TimeSpan.Zero);
        var delta = CreateSnapshot(2) with
        {
            IsDelta = true,
            BaselineFrame = 1,
            Players = Array.Empty<SnapshotPlayerState>(),
        };

        var exception = Assert.Throws<InvalidOperationException>(() => client.RememberResolvedSnapshotState(delta));
        Assert.Contains("non-delta", exception.Message);
    }

    [Fact]
    public void AcknowledgeSnapshotMarksSnapshotTransientEventsDelivered()
    {
        var client = new ClientSession(1, 101, new IPEndPoint(IPAddress.Loopback, 8190), "Tester", TimeSpan.Zero);
        var snapshot = CreateSnapshot(20) with
        {
            VisualEvents =
            [
                new SnapshotVisualEvent("Explosion", 32f, 48f, DirectionDegrees: 0f, Count: 1, EventId: 4002),
            ],
            DamageEvents =
            [
                new SnapshotDamageEvent(
                    Amount: 25,
                    AttackerPlayerId: 10,
                    AssistedByPlayerId: -1,
                    TargetKind: 1,
                    TargetEntityId: 11,
                    X: 32f,
                    Y: 48f,
                    WasFatal: false,
                    EventId: 4003),
            ],
            SoundEvents =
            [
                new SnapshotSoundEvent("ExplosionSnd", 32f, 48f, EventId: 4001, SourceFrame: 20),
            ],
            GibSpawnEvents =
            [
                new SnapshotGibSpawnEvent(
                    "Gib",
                    FrameIndex: 0,
                    X: 32f,
                    Y: 48f,
                    VelocityX: 1f,
                    VelocityY: -1f,
                    RotationSpeedDegrees: 0f,
                    HorizontalFriction: 0.1f,
                    RotationFriction: 0.1f,
                    LifetimeTicks: 30,
                    BloodChance: 0.5f,
                    EventId: 4004),
            ],
            RocketSpawnEvents =
            [
                new SnapshotRocketSpawnEvent(77, 1, 10, 32f, 48f, 32f, 48f, DirectionRadians: 0f, Speed: 200f, TicksRemaining: 40, EventId: 4005),
            ],
        };

        client.RememberResolvedSnapshotState(snapshot);

        Assert.False(client.HasAcknowledgedSoundEvent(4001));
        Assert.False(client.HasAcknowledgedTransientEvent(4002));
        Assert.False(client.HasAcknowledgedTransientEvent(4003));
        Assert.False(client.HasAcknowledgedTransientEvent(4004));
        Assert.False(client.HasAcknowledgedTransientEvent(4005));
        client.AcknowledgeSnapshot(20);
        Assert.True(client.HasAcknowledgedSoundEvent(4001));
        Assert.True(client.HasAcknowledgedTransientEvent(4002));
        Assert.True(client.HasAcknowledgedTransientEvent(4003));
        Assert.True(client.HasAcknowledgedTransientEvent(4004));
        Assert.True(client.HasAcknowledgedTransientEvent(4005));
    }

    private static SnapshotMessage CreateSnapshot(ulong frame)
    {
        return new SnapshotMessage(
            frame,
            TickRate: 60,
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
                    PlayerId: 10,
                    Name: "Tester",
                    Team: 1,
                    ClassId: 1,
                    IsAlive: true,
                    IsAwaitingJoin: false,
                    IsSpectator: false,
                    RespawnTicks: 0,
                    X: 32f,
                    Y: 48f,
                    HorizontalSpeed: 0f,
                    VerticalSpeed: 0f,
                    Health: 125,
                    MaxHealth: 125,
                    Ammo: 6,
                    MaxAmmo: 6,
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
                    IsUsingBinoculars: false,
                    BinocularsFocusX: 0f,
                    BinocularsFocusY: 0f,
                    FacingDirectionX: 1f,
                    AimDirectionDegrees: 0f,
                    IsTaunting: false,
                    IsChatBubbleVisible: false,
                    ChatBubbleFrameIndex: 0,
                    ChatBubbleAlpha: 0f,
                    Assists: 0,
                    BadgeMask: 0UL,
                    GameplayModPackId: "stock",
                    GameplayLoadoutId: "default",
                    GameplayPrimaryItemId: "scattergun",
                    GameplaySecondaryItemId: "pistol",
                    GameplayUtilityItemId: "bat",
                    GameplayEquippedSlot: 1,
                    GameplayEquippedItemId: "scattergun",
                    GameplayAcquiredItemId: "scattergun",
                    OwnedGameplayItemIds: ["scattergun"],
                    ReplicatedStates: Array.Empty<SnapshotReplicatedStateEntry>(),
                    PlayerScale: 1f),
            ],
            CombatTraces: Array.Empty<SnapshotCombatTraceState>(),
            SniperAimIndicators: Array.Empty<SnapshotSniperAimIndicatorState>(),
            Sentries: Array.Empty<SnapshotSentryState>(),
            Shots: [new SnapshotShotState((int)frame, 1, 10, 32f, 48f, 1f, 0f, 10)],
            Bubbles: Array.Empty<SnapshotShotState>(),
            Blades: Array.Empty<SnapshotShotState>(),
            Needles: Array.Empty<SnapshotShotState>(),
            RevolverShots: Array.Empty<SnapshotShotState>(),
            Rockets: Array.Empty<SnapshotRocketState>(),
            Flames: Array.Empty<SnapshotFlameState>(),
            Flares: Array.Empty<SnapshotShotState>(),
            Mines: Array.Empty<SnapshotMineState>(),
            DeadBodies: Array.Empty<SnapshotDeadBodyState>(),
            ControlPointSetupTicksRemaining: 0,
            KothUnlockTicksRemaining: 0,
            KothRedTimerTicksRemaining: 0,
            KothBlueTimerTicksRemaining: 0,
            ControlPoints: Array.Empty<SnapshotControlPointState>(),
            Generators: Array.Empty<SnapshotGeneratorState>(),
            LocalDeathCam: null,
            KillFeed: Array.Empty<SnapshotKillFeedEntry>(),
            VisualEvents: Array.Empty<SnapshotVisualEvent>(),
            DamageEvents: Array.Empty<SnapshotDamageEvent>(),
            SoundEvents: Array.Empty<SnapshotSoundEvent>(),
            IsCustomMap: false,
            MapDownloadUrl: string.Empty,
            MapContentHash: string.Empty,
            MapScale: 1f)
        {
            SentryGibs = Array.Empty<SnapshotSentryGibState>(),
        };
    }
}
