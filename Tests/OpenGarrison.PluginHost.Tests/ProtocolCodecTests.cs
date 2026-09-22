using OpenGarrison.Core;
using OpenGarrison.Protocol;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class ProtocolCodecTests
{
    private static readonly int[] SnapshotRoundTripRocketPassedFriendlyPlayerIds = [5, 7];
    private static readonly int[] SnapshotRoundTripKillFeedInvolvedPlayerIds = [5, 8];

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
    public void WelcomeMessageRoundTripsLocalPredictionFlag()
    {
        var message = new WelcomeMessage(
            ServerName: "Prediction Server",
            Version: ProtocolVersion.Current,
            TickRate: 60,
            LevelName: "ctf_truefort",
            PlayerSlot: 1,
            MaxPlayerCount: 16,
            LocalPredictionEnabled: true);

        var payload = ProtocolCodec.Serialize(message, ProtocolCompressionSettings.Disabled);

        Assert.True(ProtocolCodec.TryDeserialize(payload, out var roundTripped));
        var welcome = Assert.IsType<WelcomeMessage>(roundTripped);
        Assert.True(welcome.LocalPredictionEnabled);
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
    public void PingMessagesRoundTrip()
    {
        var requestPayload = ProtocolCodec.Serialize(new PingRequestMessage(123), ProtocolCompressionSettings.Disabled);
        Assert.True(ProtocolCodec.TryDeserialize(requestPayload, out var requestRoundTripped));
        var request = Assert.IsType<PingRequestMessage>(requestRoundTripped);
        Assert.Equal(123u, request.Sequence);

        var responsePayload = ProtocolCodec.Serialize(new PingResponseMessage(123), ProtocolCompressionSettings.Disabled);
        Assert.True(ProtocolCodec.TryDeserialize(responsePayload, out var responseRoundTripped));
        var response = Assert.IsType<PingResponseMessage>(responseRoundTripped);
        Assert.Equal(123u, response.Sequence);
    }

    [Fact]
    public void InputStateMessageRoundTripsPingMilliseconds()
    {
        var message = new InputStateMessage(
            Sequence: 55,
            Buttons: InputButtons.Left | InputButtons.FirePrimary | InputButtons.BuildDispenser | InputButtons.ToggleSecondaryWeapon,
            AimRelX: 16f,
            AimRelY: -8f,
            ChatBubbleFrameIndex: 2,
            IsUsingBinoculars: true,
            BinocularsFocusX: 120f,
            BinocularsFocusY: 96f,
            PingMilliseconds: 84);

        var payload = ProtocolCodec.Serialize(message, ProtocolCompressionSettings.Disabled);

        Assert.True(ProtocolCodec.TryDeserialize(payload, out var roundTripped));
        var input = Assert.IsType<InputStateMessage>(roundTripped);
        Assert.Equal(84, input.PingMilliseconds);
        Assert.True(input.Buttons.HasFlag(InputButtons.BuildDispenser));
        Assert.True(input.Buttons.HasFlag(InputButtons.ToggleSecondaryWeapon));
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
            [7],
            [new PlayerServerTitleState(2, "[Owner]", 0x12ABEF, Rainbow: true)]);

        var payload = ProtocolCodec.Serialize(message, ProtocolCompressionSettings.Disabled);

        Assert.True(ProtocolCodec.TryDeserialize(payload, out var roundTripped));
        var update = Assert.IsType<PlayerSocialProfileUpdateMessage>(roundTripped);
        var profile = Assert.Single(update.Profiles);
        Assert.Equal((byte)2, profile.Slot);
        Assert.Equal("Remote Player", profile.DisplayName);
        Assert.Equal("OG2-ABCD-EFGH-JKLM", profile.FriendCode);
        Assert.Contains("MenuBackground1", profile.PlayerCardJson);
        Assert.Equal((byte)7, Assert.Single(update.RemovedSlots));
        var title = Assert.Single(update.Titles!);
        Assert.Equal((byte)2, title.Slot);
        Assert.Equal("[Owner]", title.Text);
        Assert.Equal(0x12ABEFu, title.ColorRgb);
        Assert.True(title.Rainbow);
    }

    [Fact]
    public void PlayerSocialProfileUpdateAcceptsPayloadFromBeforeServerTitles()
    {
        var message = new PlayerSocialProfileUpdateMessage(
            [new PlayerSocialProfileState(2, "Remote Player", "OG2-ABCD-EFGH", "{}")],
            [7]);
        var currentPayload = ProtocolCodec.Serialize(message, ProtocolCompressionSettings.Disabled);
        var legacyPayload = currentPayload[..^1];

        Assert.True(ProtocolCodec.TryDeserialize(legacyPayload, out var roundTripped));
        var update = Assert.IsType<PlayerSocialProfileUpdateMessage>(roundTripped);
        Assert.Single(update.Profiles);
        Assert.Equal((byte)7, Assert.Single(update.RemovedSlots));
        Assert.Empty(update.Titles!);
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
                    IsKritzCritBoosted: true,
                    IsHeavyEating: false,
                    HeavyEatTicksRemaining: 0,
                    IsSniperScoped: false,
                    IsUsingBinoculars: false,
                    BinocularsFocusX: 0f,
                    BinocularsFocusY: 0f,
                    FacingDirectionX: 1f,
                    AimDirectionDegrees: 15f,
                    IsTaunting: false,
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
                    IsMedicHealing: true,
                    PingMilliseconds: 73,
                    LastToDieSpyCloakMeterUnits: 905,
                    LastToDieSpyRogueRampStacks: 4,
                    LastToDieSpyRogueRampTicks: 29,
                    KritzCritBoostProviderPlayerId: 77,
                    KritzCritBoostProviderSlot: 2,
                    KritzCritBoostDamageMultiplier: 3.5f,
                    IsBot: true),
            ],
            CombatTraces: [new SnapshotCombatTraceState(0f, 0f, 8f, 8f, 2, true, 1, false)],
            SniperAimIndicators: [],
            Sentries: Array.Empty<SnapshotSentryState>(),
            Shots:
            [
                new SnapshotShotState(
                    8,
                    1,
                    5,
                    100f,
                    120f,
                    10f,
                    0f,
                    15,
                    IsCritical: true,
                    DamageValue: 9.5f,
                    CriticalDamageMultiplier: 3.5f,
                    PlayerKnockbackImpulse: 1.25f,
                    PlayerKnockbackAirborneVerticalScale: 0.5f,
                    PlayerKnockbackGroundedVerticalScale: 0.25f),
            ],
            Bubbles: Array.Empty<SnapshotShotState>(),
            Blades: Array.Empty<SnapshotShotState>(),
            Needles:
            [
                new SnapshotShotState(
                    Id: 12,
                    Team: 1,
                    OwnerId: 5,
                    X: 100f,
                    Y: 120f,
                    VelocityX: 1f,
                    VelocityY: 0f,
                    TicksRemaining: 90,
                    IsArrow: true,
                    ArrowFakeSpeedMultiplier: 0.75f,
                    IsLanded: true,
                    AppliesLastToDieGuardian: true,
                    PiercesPlayers: true,
                    AppliesLastToDieTranqDarts: true,
                    LastToDiePoisonDamagePerSecond: 14.5f,
                    LastToDieGhostDamageMultiplier: 3f,
                    AppliesLastToDieDecapitator: true,
                    IsLastToDieDecapitatorFullyCharged: true,
                    LastToDieAttachedHeadClassId: (byte)PlayerClass.Heavy,
                    LastToDieAttachedHeadTeam: (byte)PlayerTeam.Blue,
                    AppliesLastToDieExplosiveTip: true,
                    DamageValue: 30f),
                new SnapshotShotState(
                    Id: 14,
                    Team: 1,
                    OwnerId: 5,
                    X: 110f,
                    Y: 120f,
                    VelocityX: 8f,
                    VelocityY: 0f,
                    TicksRemaining: 35,
                    IsMedicHealNeedle: true,
                    LastToDieMedicKritzM2Payload: 0b1111,
                    IsLastToDieMedicJavelinAnchored: true,
                    LastToDieMedicJavelinFuseTicksRemaining: 21,
                    HasLastToDieMedicJavelinExploded: false),
            ],
            RevolverShots:
            [
                new SnapshotShotState(
                    13,
                    1,
                    5,
                    140f,
                    120f,
                    12f,
                    0f,
                    20,
                    IsCritical: true,
                    DamageValue: 11.2f,
                    LastToDieRevolverProfile: 0b1100_0010,
                    AppliesLuckyStrikeStun: true,
                    CriticalDamageMultiplier: 3.5f,
                    PlayerKnockbackImpulse: 2.5f,
                    PlayerKnockbackAirborneVerticalScale: 0.5f,
                    PlayerKnockbackGroundedVerticalScale: 0.25f),
            ],
            Rockets: [new SnapshotRocketState(9, 1, 5, 100f, 120f, 96f, 120f, 0.2f, 240f, 20)],
            Flames: Array.Empty<SnapshotFlameState>(),
            Flares:
            [
                new SnapshotShotState(
                    15,
                    1,
                    5,
                    180f,
                    120f,
                    22f,
                    0f,
                    12,
                    DamageValue: 45f,
                    FlareStyle: (byte)FlareProjectileStyle.DragonRageSlug),
            ],
            Mines: Array.Empty<SnapshotMineState>(),
            DeadBodies:
            [
                new SnapshotDeadBodyState(
                    Id: 44,
                    SourcePlayerId: 6,
                    Team: 2,
                    ClassId: (byte)PlayerClass.Quote,
                    AnimationKind: (byte)DeadBodyAnimationKind.Default,
                    X: 14f,
                    Y: 18f,
                    Width: 16f,
                    Height: 28f,
                    HorizontalSpeed: 0f,
                    VerticalSpeed: 2f,
                    FacingLeft: true,
                    TicksRemaining: 275,
                    GameplayClassId: "plugin.quote-curly.quote"),
            ],
            ControlPointSetupTicksRemaining: 0,
            KothUnlockTicksRemaining: 0,
            KothRedTimerTicksRemaining: 0,
            KothBlueTimerTicksRemaining: 0,
            ControlPoints: [new SnapshotControlPointState(0, 1, 0, 0, 120, 1, false, HasHealingAura: true)],
            Generators: [new SnapshotGeneratorState(1, 75, 100)],
            LocalDeathCam: null,
            KillFeed:
            [
                new SnapshotKillFeedEntry("Scout", 1, "scattergun", "Pyro", 2, "Scout fragged Pyro", 0, 5, 5, 6)
                {
                    AssistName = "Medic", AssistTeam = 1, AssistPlayerId = 8,
                    InvolvedPlayerIds = SnapshotRoundTripKillFeedInvolvedPlayerIds,
                },
            ],
            VisualEvents: [new SnapshotVisualEvent("spark", 10f, 20f, 45f, 1, EventId: 55, SourceFrame: 101)],
            DamageEvents: [new SnapshotDamageEvent(45, 5, -1, 1, 6, 10f, 20f, false, EventId: 66, SourceFrame: 101, Flags: 6)],
            SoundEvents: [new SnapshotSoundEvent("rocket_fire", 11f, 21f, 77, 101)],
            IsCustomMap: true,
            MapDownloadUrl: "https://example.invalid/map.zip",
            MapContentHash: "deadbeef",
            MapScale: 1.25f)
        {
            BaselineFrame = 100,
            IsDelta = true,
            EntityCollectionCompletenessFlags =
                SnapshotEntityCollectionCompletenessFlags.AllProjectiles & ~SnapshotEntityCollectionCompletenessFlags.Rockets,
            CapLimit = 9,
            ScoreboardPlayers =
            [
                new SnapshotPlayerState(
                    Slot: 2,
                    PlayerId: 6,
                    Name: "Hidden Spy",
                    Team: 2,
                    ClassId: 8,
                    IsAlive: true,
                    IsAwaitingJoin: false,
                    IsSpectator: false,
                    RespawnTicks: 0,
                    X: 90f,
                    Y: 95f,
                    HorizontalSpeed: 0f,
                    VerticalSpeed: 0f,
                    Health: 100,
                    MaxHealth: 100,
                    Ammo: 6,
                    MaxAmmo: 6,
                    Kills: 1,
                    Deaths: 0,
                    Caps: 0,
                    Points: 2f,
                    HealPoints: 0,
                    ActiveDominationCount: 0,
                    IsDominatingLocalViewer: false,
                    IsDominatedByLocalViewer: false,
                    Metal: 0f,
                    IsGrounded: true,
                    RemainingAirJumps: 0,
                    IsCarryingIntel: false,
                    IntelRechargeTicks: 0f,
                    IsSpyCloaked: true,
                    SpyCloakAlpha: 0f,
                    IsSpySuperjumping: false,
                    SpySuperjumpHorizontalVelocity: 0f,
                    SpySuperjumpCooldownTicksRemaining: 0,
                    SpyBackstabVisualTicksRemaining: 0,
                    IsUbered: false,
                    IsKritzCritBoosted: true,
                    IsHeavyEating: false,
                    HeavyEatTicksRemaining: 0,
                    IsSniperScoped: false,
                    IsUsingBinoculars: false,
                    BinocularsFocusX: 0f,
                    BinocularsFocusY: 0f,
                    FacingDirectionX: -1f,
                    AimDirectionDegrees: 180f,
                    IsTaunting: false,
                    IsChatBubbleVisible: false,
                    ChatBubbleFrameIndex: 0,
                    ChatBubbleAlpha: 0f),
            ],
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
                    5,
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
                    EventId: 1024,
                    PassedFriendlyPlayerIds: SnapshotRoundTripRocketPassedFriendlyPlayerIds,
                    CriticalDamageMultiplier: 3.5f),
            ],
            SentryGibs = [new SnapshotSentryGibState(3, 1, 10f, 12f, 25)],
            RemovedSentryGibIds = [3],
            HealthPacks =
            [
                new SnapshotHealthPackState(
                    Id: -1,
                    Size: 1,
                    X: 64f,
                    Y: 96f,
                    VelocityX: 0f,
                    VelocityY: 0f,
                    TicksRemaining: 0,
                    SourceSpawnIndex: 0,
                    RespawnTicksRemaining: 120,
                    Active: false),
            ],
            RemovedHealthPackIds = [42],
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
        Assert.Equal(4f, playerMovement.BurnIntensity);
        Assert.Equal(95, playerStatus.Health);
        Assert.Equal(49, chatBubbleState.ChatBubbleFrameIndex);
        Assert.InRange(chatBubbleState.ChatBubbleAlpha, 0.74f, 0.76f);
        var player = Assert.Single(roundTrippedSnapshot.Players);
        Assert.Equal(9, player.MedicHealTargetId);
        Assert.True(player.IsMedicHealing);
        Assert.Equal(73, player.PingMilliseconds);
        Assert.Equal((ushort)905, player.LastToDieSpyCloakMeterUnits);
        Assert.Equal((byte)4, player.LastToDieSpyRogueRampStacks);
        Assert.Equal((ushort)29, player.LastToDieSpyRogueRampTicks);
        Assert.True(player.IsKritzCritBoosted);
        Assert.Equal(77, player.KritzCritBoostProviderPlayerId);
        Assert.Equal(2, player.KritzCritBoostProviderSlot);
        Assert.Equal(3.5f, player.KritzCritBoostDamageMultiplier);
        Assert.True(player.IsBot);
        Assert.True(Assert.Single(roundTrippedSnapshot.ControlPoints).HasHealingAura);
        Assert.Equal("plugin.example.ranger", player.GameplayClassId);
        var deadBody = Assert.Single(roundTrippedSnapshot.DeadBodies);
        Assert.Equal("plugin.quote-curly.quote", deadBody.GameplayClassId);
        Assert.Equal(101UL, Assert.Single(roundTrippedSnapshot.VisualEvents).SourceFrame);
        Assert.Equal(9, roundTrippedSnapshot.CapLimit);
        var scoreboardPlayer = Assert.Single(roundTrippedSnapshot.ScoreboardPlayers);
        Assert.Equal(6, scoreboardPlayer.PlayerId);
        Assert.Equal("Hidden Spy", scoreboardPlayer.Name);
        Assert.False((roundTrippedSnapshot.EntityCollectionCompletenessFlags & SnapshotEntityCollectionCompletenessFlags.Rockets) != 0);
        Assert.True((roundTrippedSnapshot.EntityCollectionCompletenessFlags & SnapshotEntityCollectionCompletenessFlags.Shots) != 0);
        var rocketSpawn = Assert.Single(roundTrippedSnapshot.RocketSpawnEvents);
        Assert.Equal(901, rocketSpawn.Id);
        Assert.Equal(3.5f, rocketSpawn.CriticalDamageMultiplier);
        var bulletShot = Assert.Single(roundTrippedSnapshot.Shots);
        Assert.Equal(3.5f, bulletShot.CriticalDamageMultiplier);
        Assert.Equal(9.5f, bulletShot.DamageValue);
        Assert.Equal(1.25f, bulletShot.PlayerKnockbackImpulse);
        Assert.Equal(0.5f, bulletShot.PlayerKnockbackAirborneVerticalScale);
        Assert.Equal(0.25f, bulletShot.PlayerKnockbackGroundedVerticalScale);
        Assert.Equal(3.5f, Assert.Single(roundTrippedSnapshot.RevolverShots).CriticalDamageMultiplier);
        var landedArrow = Assert.Single(
            roundTrippedSnapshot.Needles,
            static needle => needle.IsArrow);
        Assert.True(landedArrow.IsArrow);
        Assert.True(landedArrow.IsLanded);
        Assert.Equal(0.75f, landedArrow.ArrowFakeSpeedMultiplier);
        Assert.True(landedArrow.AppliesLastToDieGuardian);
        Assert.True(landedArrow.PiercesPlayers);
        Assert.True(landedArrow.AppliesLastToDieTranqDarts);
        Assert.Equal(14.5f, landedArrow.LastToDiePoisonDamagePerSecond);
        Assert.Equal(3f, landedArrow.LastToDieGhostDamageMultiplier);
        Assert.True(landedArrow.AppliesLastToDieDecapitator);
        Assert.True(landedArrow.IsLastToDieDecapitatorFullyCharged);
        Assert.Equal((byte)PlayerClass.Heavy, landedArrow.LastToDieAttachedHeadClassId);
        Assert.Equal((byte)PlayerTeam.Blue, landedArrow.LastToDieAttachedHeadTeam);
        Assert.True(landedArrow.AppliesLastToDieExplosiveTip);
        Assert.Equal(30f, landedArrow.DamageValue);
        var medicKritzM2 = Assert.Single(
            roundTrippedSnapshot.Needles,
            static needle => needle.IsMedicHealNeedle);
        Assert.Equal((byte)0b1111, medicKritzM2.LastToDieMedicKritzM2Payload);
        Assert.True(medicKritzM2.IsLastToDieMedicJavelinAnchored);
        Assert.Equal(21, medicKritzM2.LastToDieMedicJavelinFuseTicksRemaining);
        Assert.False(medicKritzM2.HasLastToDieMedicJavelinExploded);
        var revolverShot = Assert.Single(roundTrippedSnapshot.RevolverShots);
        Assert.Equal(11.2f, revolverShot.DamageValue);
        Assert.Equal(2.5f, revolverShot.PlayerKnockbackImpulse);
        Assert.Equal(0.5f, revolverShot.PlayerKnockbackAirborneVerticalScale);
        Assert.Equal(0.25f, revolverShot.PlayerKnockbackGroundedVerticalScale);
        Assert.Equal(0b1100_0010, revolverShot.LastToDieRevolverProfile);
        Assert.True(revolverShot.IsCritical);
        Assert.True(revolverShot.AppliesLuckyStrikeStun);
        var flare = Assert.Single(roundTrippedSnapshot.Flares);
        Assert.Equal(45f, flare.DamageValue);
        Assert.Equal((byte)FlareProjectileStyle.DragonRageSlug, flare.FlareStyle);
        Assert.True(rocketSpawn.ExplodeImmediately);
        Assert.True(rocketSpawn.IsCritical);
        Assert.Equal(1024UL, rocketSpawn.EventId);
        Assert.Equal(SnapshotRoundTripRocketPassedFriendlyPlayerIds, rocketSpawn.PassedFriendlyPlayerIds);
        var killFeedEntry = Assert.Single(roundTrippedSnapshot.KillFeed);
        Assert.Equal(SnapshotRoundTripKillFeedInvolvedPlayerIds, killFeedEntry.InvolvedPlayerIds);
        Assert.Equal("Medic", killFeedEntry.AssistName);
        Assert.Equal(1, killFeedEntry.AssistTeam);
        Assert.Equal(8, killFeedEntry.AssistPlayerId);
        var healthPack = Assert.Single(roundTrippedSnapshot.HealthPacks);
        Assert.Equal(-1, healthPack.Id);
        Assert.Equal(1, healthPack.Size);
        Assert.Equal(120, healthPack.RespawnTicksRemaining);
        Assert.False(healthPack.Active);
        Assert.Equal([42], roundTrippedSnapshot.RemovedHealthPackIds);
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
                    IsUsingBinoculars: false,
                    BinocularsFocusX: 50f,
                    BinocularsFocusY: 75f,
                    FacingDirectionX: 1f,
                    AimDirectionDegrees: 0f,
                    IsTaunting: false,
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
            PlayerMovementStates = [new SnapshotPlayerMovementState(1, 50f, 75f, 0f, 0f, true, 1, 1f, 0f, 0, false, 0f, 0, MedicHealTargetId: 8, IsMedicHealing: true)],
        };

        var merged = SnapshotDelta.ToFullSnapshot(delta, baseline);
        var player = Assert.Single(merged.Players);
        Assert.Equal(8, player.MedicHealTargetId);
        Assert.True(player.IsMedicHealing);
    }

    [Fact]
    public void GameplayAccountMessagesRoundTrip()
    {
        var attachRequestPayload = ProtocolCodec.Serialize(
            new GameplayAccountAttachRequestMessage(17, "gameplay-token"),
            ProtocolCompressionSettings.Disabled);
        Assert.True(ProtocolCodec.TryDeserialize(attachRequestPayload, out var attachRequestValue));
        var attachRequest = Assert.IsType<GameplayAccountAttachRequestMessage>(attachRequestValue);
        Assert.Equal(17UL, attachRequest.RequestId);
        Assert.Equal("gameplay-token", attachRequest.GameplayToken);

        var attachResultPayload = ProtocolCodec.Serialize(
            new GameplayAccountAttachResultMessage(17, true, string.Empty, "OG2-ABCD-EFGH", "Runner", 1250, 900, 7),
            ProtocolCompressionSettings.Disabled);
        Assert.True(ProtocolCodec.TryDeserialize(attachResultPayload, out var attachResultValue));
        var attachResult = Assert.IsType<GameplayAccountAttachResultMessage>(attachResultValue);
        Assert.Equal(17UL, attachResult.RequestId);
        Assert.True(attachResult.Attached);
        Assert.Equal("OG2-ABCD-EFGH", attachResult.FriendCode);
        Assert.Equal(1250, attachResult.LifetimePoints);
        Assert.Equal(7, attachResult.ProfileRevision);

        var pointsPayload = ProtocolCodec.Serialize(
            new PlayerPointsStateMessage(1500, 1100, 3, 9),
            ProtocolCompressionSettings.Disabled);
        Assert.True(ProtocolCodec.TryDeserialize(pointsPayload, out var pointsValue));
        var points = Assert.IsType<PlayerPointsStateMessage>(pointsValue);
        Assert.Equal(1500, points.LifetimePoints);
        Assert.Equal(1100, points.WalletBalance);
        Assert.Equal(3, points.GlobalRank);
        Assert.Equal(9, points.ProfileRevision);
    }

    [Fact]
    public void NativeVotingMessagesRoundTrip()
    {
        var commandPayload = ProtocolCodec.Serialize(
            new VoteCommandMessage(VoteCommandKind.StartCustom, "plugin.owner:restart", 2, 7, (byte)PlayerTeam.Blue, 41, "argument"),
            ProtocolCompressionSettings.Disabled);
        Assert.True(ProtocolCodec.TryDeserialize(commandPayload, out var commandValue));
        var command = Assert.IsType<VoteCommandMessage>(commandValue);
        Assert.Equal(VoteCommandKind.StartCustom, command.Command);
        Assert.Equal("plugin.owner:restart", command.Target);
        Assert.Equal(2, command.AreaIndex);
        Assert.Equal((byte)7, command.TargetSlot);
        Assert.Equal((byte)PlayerTeam.Blue, command.Team);
        Assert.Equal(41UL, command.VoteId);
        Assert.Equal("argument", command.Argument);

        var statePayload = ProtocolCodec.Serialize(
            new VoteStateMessage(
                42,
                5,
                ServerVoteEventKind.Yes,
                ServerVoteKind.ChangeMapNextRound,
                "next map: ctf_truefort",
                "Starter",
                "Voter",
                2,
                1,
                3,
                5,
                450,
                "Voter voted yes."),
            ProtocolCompressionSettings.Disabled);
        Assert.True(ProtocolCodec.TryDeserialize(statePayload, out var stateValue));
        var state = Assert.IsType<VoteStateMessage>(stateValue);
        Assert.Equal(42UL, state.VoteId);
        Assert.Equal(5U, state.Revision);
        Assert.Equal(ServerVoteKind.ChangeMapNextRound, state.Kind);
        Assert.Equal(2, state.YesVotes);
        Assert.Equal(450, state.RemainingTicks);

        var menuPayload = ProtocolCodec.Serialize(
            new VoteMenuMessage(
                [new VoteMenuMapEntry("ctf_truefort", "Truefort", 2)],
                [new VoteMenuPlayerEntry(4, "Candidate", (byte)PlayerTeam.Red, IsMuted: true)],
                VipVoteAvailable: true,
                VoteActive: false,
                CooldownTicksRemaining: 90,
                ActiveVoteId: 41,
                KickVoteAvailable: true,
                MuteVoteAvailable: true,
                ScrambleVoteAvailable: true,
                RegisteredVotes:
                [
                    new VoteMenuCustomEntry(
                        "plugin.owner:restart",
                        "Restart Round",
                        "Restarts the current round.",
                        (byte)VoteMenuTargetKind.None),
                ]),
            ProtocolCompressionSettings.Disabled);
        Assert.True(ProtocolCodec.TryDeserialize(menuPayload, out var menuValue));
        var menu = Assert.IsType<VoteMenuMessage>(menuValue);
        Assert.True(menu.VipVoteAvailable);
        Assert.Equal(90, menu.CooldownTicksRemaining);
        Assert.Equal(41UL, menu.ActiveVoteId);
        Assert.Equal("ctf_truefort", Assert.Single(menu.Maps).LevelName);
        Assert.Equal((byte)4, Assert.Single(menu.Players).Slot);
        Assert.True(Assert.Single(menu.Players).IsMuted);
        Assert.True(menu.KickVoteAvailable);
        Assert.True(menu.MuteVoteAvailable);
        Assert.True(menu.ScrambleVoteAvailable);
        Assert.Equal("plugin.owner:restart", Assert.Single(menu.CustomVotes).Id);
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
