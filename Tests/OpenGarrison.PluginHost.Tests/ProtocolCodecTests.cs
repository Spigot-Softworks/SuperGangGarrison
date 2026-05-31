using OpenGarrison.Protocol;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class ProtocolCodecTests
{
    [Fact]
    public void HelloMessageRoundTripsConnectionIntent()
    {
        var message = new HelloMessage(
            "Watcher",
            ProtocolVersion.Current,
            BadgeMask: 42,
            FriendCode: "OG2-ABCD",
            PlayerCardJson: "{}",
            Intent: ConnectionIntent.Watch);

        var payload = ProtocolCodec.Serialize(message, ProtocolCompressionSettings.Disabled);

        Assert.True(ProtocolCodec.TryDeserialize(payload, out var roundTripped));
        var hello = Assert.IsType<HelloMessage>(roundTripped);
        Assert.Equal("Watcher", hello.Name);
        Assert.Equal(ConnectionIntent.Watch, hello.Intent);
    }

    [Fact]
    public void ServerDetailsMessagesRoundTrip()
    {
        var requestPayload = ProtocolCodec.Serialize(new ServerDetailsRequestMessage(), ProtocolCompressionSettings.Disabled);
        Assert.True(ProtocolCodec.TryDeserialize(requestPayload, out var requestRoundTripped));
        Assert.IsType<ServerDetailsRequestMessage>(requestRoundTripped);

        var response = new ServerDetailsResponseMessage(
            "Test Server",
            "ctf_test",
            GameMode: 1,
            PlayerCount: 1,
            MaxPlayerCount: 12,
            SpectatorCount: 2,
            RedScore: 3,
            BlueScore: 4,
            TimeRemainingTicks: 900,
            TimeLimitTicks: 1800,
            TickRate: 30,
            [
                new ServerDetailsRosterEntry(
                    Slot: 1,
                    Name: "Runner",
                    Team: 1,
                    ClassId: 1,
                    IsSpectator: false,
                    IsAlive: true,
                    IsAwaitingJoin: false,
                    Health: 110,
                    MaxHealth: 125,
                    Kills: 5,
                    Deaths: 2,
                    Assists: 1,
                    Caps: 3,
                    Points: 12.5f),
            ]);

        var responsePayload = ProtocolCodec.Serialize(response, ProtocolCompressionSettings.Disabled);

        Assert.True(ProtocolCodec.TryDeserialize(responsePayload, out var responseRoundTripped));
        var details = Assert.IsType<ServerDetailsResponseMessage>(responseRoundTripped);
        Assert.Equal("Test Server", details.ServerName);
        Assert.Equal(3, details.RedScore);
        var rosterEntry = Assert.Single(details.Roster);
        Assert.Equal("Runner", rosterEntry.Name);
        Assert.Equal(12.5f, rosterEntry.Points);
    }

    [Fact]
    public void ChatRelayMessageRoundTripsSenderSlot()
    {
        var message = new ChatRelayMessage(
            Team: 1,
            PlayerName: "Medic",
            Text: "incoming",
            TeamOnly: true,
            PlayerSlot: 7);

        var payload = ProtocolCodec.Serialize(message, ProtocolCompressionSettings.Disabled);

        Assert.True(ProtocolCodec.TryDeserialize(payload, out var roundTripped));
        var chatRelay = Assert.IsType<ChatRelayMessage>(roundTripped);
        Assert.Equal((byte)1, chatRelay.Team);
        Assert.Equal("Medic", chatRelay.PlayerName);
        Assert.Equal("incoming", chatRelay.Text);
        Assert.True(chatRelay.TeamOnly);
        Assert.Equal((byte)7, chatRelay.PlayerSlot);
    }

    [Fact]
    public void PlayerSocialProfileUpdateRoundTrips()
    {
        var message = new PlayerSocialProfileUpdateMessage(
            [
                new PlayerSocialProfileState(
                    2,
                    "Remote Player",
                    "OG2-ABCD-EFGH-JKLM",
                    "{\"background\":\"MenuBackground1.png\",\"class\":\"Spy\"}"),
            ],
            [7]);

        var payload = ProtocolCodec.Serialize(message, ProtocolCompressionSettings.Disabled);

        Assert.True(ProtocolCodec.TryDeserialize(payload, out var roundTripped));
        var update = Assert.IsType<PlayerSocialProfileUpdateMessage>(roundTripped);
        var profile = Assert.Single(update.Profiles);
        Assert.Equal((byte)2, profile.Slot);
        Assert.Equal("Remote Player", profile.DisplayName);
        Assert.Equal("OG2-ABCD-EFGH-JKLM", profile.FriendCode);
        Assert.Contains("MenuBackground1", profile.PlayerCardJson);
        Assert.Equal((byte)7, Assert.Single(update.RemovedSlots));
    }

    [Fact]
    public void CustomBubbleUploadRoundTripsRgba64Payload()
    {
        var pixels = CreateCustomBubblePixels();
        var message = new CustomBubbleUploadMessage(Slot: 2, Revision: 42, pixels);

        var payload = ProtocolCodec.Serialize(message, ProtocolCompressionSettings.Disabled);

        Assert.True(ProtocolCodec.TryDeserialize(payload, out var roundTripped));
        var upload = Assert.IsType<CustomBubbleUploadMessage>(roundTripped);
        Assert.Equal((byte)2, upload.Slot);
        Assert.Equal(42u, upload.Revision);
        Assert.Equal(pixels, upload.Rgba64Pixels);
    }

    [Fact]
    public void CustomBubbleStateAndClearRoundTrip()
    {
        var pixels = CreateCustomBubblePixels();
        var statePayload = ProtocolCodec.Serialize(
            new CustomBubbleStateMessage(PlayerSlot: 7, Slot: 1, Revision: 9, pixels),
            ProtocolCompressionSettings.Disabled);

        Assert.True(ProtocolCodec.TryDeserialize(statePayload, out var stateRoundTripped));
        var state = Assert.IsType<CustomBubbleStateMessage>(stateRoundTripped);
        Assert.Equal((byte)7, state.PlayerSlot);
        Assert.Equal((byte)1, state.Slot);
        Assert.Equal(9u, state.Revision);
        Assert.Equal(pixels, state.Rgba64Pixels);

        var clearPayload = ProtocolCodec.Serialize(new CustomBubbleClearMessage(7), ProtocolCompressionSettings.Disabled);
        Assert.True(ProtocolCodec.TryDeserialize(clearPayload, out var clearRoundTripped));
        var clear = Assert.IsType<CustomBubbleClearMessage>(clearRoundTripped);
        Assert.Equal((byte)7, clear.PlayerSlot);
    }

    [Fact]
    public void CustomBubbleUploadRejectsWrongPayloadLength()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ProtocolCodec.Serialize(
                new CustomBubbleUploadMessage(0, 1, new byte[ProtocolCodec.CustomBubbleRgba64PayloadBytes - 1]),
                ProtocolCompressionSettings.Disabled));
    }

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
                    IsUsingBinoculars: false,
                    BinocularsFocusX: 0f,
                    BinocularsFocusY: 0f,
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
                    GameplayClassId: "plugin.example.ranger",
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
            SniperAimIndicators: [],
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
            DamageEvents: [new SnapshotDamageEvent(45, 5, -1, 1, 6, 10f, 20f, false, EventId: 66, SourceFrame: 101, Flags: 6)],
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
            RocketSpawnEvents =
            [
                new SnapshotRocketSpawnEvent(
                    901,
                    1,
                    5,
                    100f,
                    120f,
                    96f,
                    120f,
                    0.2f,
                    240f,
                    20,
                    ExplodeImmediately: true,
                    IsCritical: true,
                    EventId: 1024),
            ],
            SentryGibs = [new SnapshotSentryGibState(3, 1, 10f, 12f, 25)],
            RemovedSentryGibIds = [3],
        };

        var measuredSize = ProtocolCodec.MeasureSerializedSize(snapshot);
        var payload = ProtocolCodec.Serialize(snapshot, ProtocolCompressionSettings.Disabled);
        var measuredSizePayload = ProtocolCodec.Serialize(snapshot, measuredSize, ProtocolCompressionSettings.Disabled);
        var defaultPayload = ProtocolCodec.Serialize(snapshot, ProtocolCompressionSettings.Default);
        var measuredDefaultPayload = ProtocolCodec.Serialize(snapshot, measuredSize, ProtocolCompressionSettings.Default);

        Assert.Equal(payload.Length - 1, measuredSize);
        Assert.Equal((byte)0, payload[0]);
        Assert.Equal(payload, measuredSizePayload);
        Assert.Equal(defaultPayload, measuredDefaultPayload);
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
        Assert.Equal("plugin.example.ranger", player.GameplayClassId);
        var rocketSpawn = Assert.Single(roundTrippedSnapshot.RocketSpawnEvents);
        Assert.Equal(901, rocketSpawn.Id);
        Assert.True(rocketSpawn.ExplodeImmediately);
        Assert.True(rocketSpawn.IsCritical);
        Assert.Equal(1024UL, rocketSpawn.EventId);
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
                    IsUsingBinoculars: false,
                    BinocularsFocusX: 50f,
                    BinocularsFocusY: 75f,
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
            SniperAimIndicators: [],
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

    private static byte[] CreateCustomBubblePixels()
    {
        var pixels = new byte[ProtocolCodec.CustomBubbleRgba64PayloadBytes];
        for (var index = 0; index < pixels.Length; index += 1)
        {
            pixels[index] = (byte)(index % byte.MaxValue);
        }

        return pixels;
    }
}
