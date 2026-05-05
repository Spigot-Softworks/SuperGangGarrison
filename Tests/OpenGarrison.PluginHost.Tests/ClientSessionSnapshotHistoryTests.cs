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
                    IsUbered: false,
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
