using System.Net;
using System.Reflection;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;
using OpenGarrison.Protocol;
using OpenGarrison.Server;
using Xunit;
using Xunit.Abstractions;

namespace OpenGarrison.PluginHost.Tests;

public sealed class SnapshotDeltaBudgeterTests
{
    private readonly ITestOutputHelper _output;

    public SnapshotDeltaBudgeterTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void SpecialAbilitiesToggleSurvivesACrowdedSnapshotAndCompactStatusMerge()
    {
        var enabled = CreateCoreAbilityState(GameplayAbilityReplicatedState.SpecialAbilitiesEnabledKey,
            SnapshotReplicatedStateValueKind.Toggle, boolValue: true);
        var player = CreatePlayerState(1, 951, "Local Player") with { ReplicatedStates = [enabled] };
        var remote = CreatePlayerState(2, 952, "Remote Bot");
        var baseline = CreateSnapshot(950) with { Players = [player, remote] };
        var current = CreateSnapshot(951) with
        {
            Players = [player with { ReplicatedStates = [enabled with { BoolValue = false }] }, remote],
        };
        var client = new ClientSession(1, userId: 101, new IPEndPoint(IPAddress.Loopback, 8190), "Tester", TimeSpan.Zero);
        var contributions = SnapshotContributionPlanner.BuildContributions(client, current, baseline, new SimulationWorld());
        var result = SnapshotDeltaBudgeter.BuildBudgetedSnapshot(current, baseline, contributions, targetPayloadBytes: 260);
        Assert.True(result.Payload.Length <= 260);
        Assert.Empty(result.Message.Players);
        Assert.True(result.Message.PlayerStatusStates.Count > 0,
            $"No status update: {contributions.Count} contributions; {result.Payload.Length} bytes; kinds={string.Join(',', contributions.Select(c => c.Kind))}");
        var merged = SnapshotDelta.ToFullSnapshot(result.Message, baseline);
        var local = Assert.Single(merged.Players, player => player.Slot == 1);
        Assert.False(Assert.Single(local.ReplicatedStates!, entry => entry.Key == enabled.Key).BoolValue);
    }

    [Fact]
    public void AggressiveSnapshotReductionPreservesConstructorMetal()
    {
        var player = CreatePlayerState(1, 900, "Constructor") with
        { ClassId = (byte)PlayerClass.Engineer, Metal = 100f };
        var method = typeof(SnapshotDeltaBudgeter).GetMethod(
            "ReducePlayerStateAggressivelyForBudget", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        var reduced = (SnapshotPlayerState)method.Invoke(null, new object[] { player })!;
        Assert.Equal(100f, reduced.Metal);
    }

    [Fact]
    public void ScoreboardDeltaRoundTripsExactRosterOrderIdentityAndCanonicalPlayerRecords()
    {
        var unchanged = CreatePlayerState(1, 501, "Hidden Spy") with
        {
            IsSpyCloaked = true,
            SpyCloakAlpha = 0.125f,
            IsSpectator = true,
            GameplayModPackId = "community.pack",
            GameplayPrimaryItemId = "weapon.custom",
            OwnedGameplayItemIds = ["weapon.custom", "ability.custom"],
            ReplicatedStates = [new SnapshotReplicatedStateEntry("test.mod", "visible", SnapshotReplicatedStateValueKind.Toggle, BoolValue: true)],
            IsDominatingLocalViewer = true,
        };
        var changed = CreatePlayerState(2, 502, "Spectator") with
        {
            IsSpectator = true,
            PingMilliseconds = 88,
            Kills = 3,
            AimWorldX = 18.25f,
            KritzCritBoostDamageMultiplier = 2.5f,
        };
        var removed = CreatePlayerState(3, 503, "Leaving Player");
        var baseline = CreateSnapshot(20) with { ScoreboardPlayers = [unchanged, changed, removed] };
        var current = CreateSnapshot(21) with
        {
            ScoreboardPlayers =
            [
                CreatePlayerState(2, 900, "Reused Slot") with
                {
                    Team = 2,
                    IsAlive = false,
                    ExperimentalCryoSlowTicksRemaining = 13,
                    ExperimentalCryoFreezeTicksRemaining = 7,
                    ExperimentalCryoExposureFraction = 0.63f,
                    ExperimentalGhostTrailAlpha = 0.41f,
                    RageCharge = 63.2f,
                    IsRageReady = true,
                    KritzCritBoostProviderPlayerId = 501,
                    GameplayPrimaryItemId = "w.x",
                },
                unchanged,
                CreatePlayerState(4, 901, "New Slot") with { GameplayClassId = "c" },
            ],
        };

        var result = SnapshotDeltaBudgeter.BuildUntrimmedSnapshot(current, baseline, Array.Empty<SnapshotDeltaBudgeter.Contribution>());
        Assert.True(result.Message.HasScoreboardDelta);
        Assert.Empty(result.Message.ScoreboardPlayers);
        Assert.Equal(new byte[] { 2, 1, 4 }, result.Message.ScoreboardPlayerOrder);
        Assert.Equal(2, result.Message.ScoreboardPlayerPatches.Count);

        Assert.True(ProtocolCodec.TryDeserialize(result.Payload, out var decodedMessage));
        var decoded = Assert.IsType<SnapshotMessage>(decodedMessage);
        Assert.True(decoded.HasScoreboardDelta);
        var resolved = SnapshotDelta.ToFullSnapshot(decoded, baseline);
        var expected = RoundTripSnapshot(current).ScoreboardPlayers;
        Assert.Equal(SerializeScoreboardOnly(expected), SerializeScoreboardOnly(resolved.ScoreboardPlayers));
        Assert.Equal(new[] { 900, 501, 901 }, resolved.ScoreboardPlayers.Select(player => player.PlayerId));
        Assert.True(resolved.ScoreboardPlayers[1].IsSpyCloaked);
        Assert.True(resolved.ScoreboardPlayers[1].IsSpectator);
        Assert.True(resolved.ScoreboardPlayers[1].IsDominatingLocalViewer);
        Assert.Equal(new[] { "weapon.custom", "ability.custom" }, resolved.ScoreboardPlayers[1].OwnedGameplayItemIds);

        var compatibilityCurrent = RoundTripSnapshot(current);
        var legacyFullRosterDelta = current with
        {
            BaselineFrame = baseline.Frame,
            IsDelta = true,
            ScoreboardPlayers = compatibilityCurrent.ScoreboardPlayers,
        };
        var legacyCompressedBytes = ProtocolCodec.Serialize(legacyFullRosterDelta, ProtocolCompressionSettings.Default).Length;
        _output.WriteLine("Scoreboard-only sample (3 players; identity replacement, removal, addition, late ability changes): compact={0} bytes, full roster={1} bytes", result.Payload.Length, legacyCompressedBytes);
        Assert.True(result.Payload.Length < legacyCompressedBytes,
            $"Expected compact roster delta ({result.Payload.Length} bytes) below complete scoreboard ({legacyCompressedBytes} bytes).");
    }

    [Fact]
    public void ScoreboardDeltaRepresentsExplicitEmptyRosterAndReorderingWithoutPatches()
    {
        var first = CreatePlayerState(1, 601, "One");
        var second = CreatePlayerState(2, 602, "Two");
        var baseline = CreateSnapshot(30) with { ScoreboardPlayers = [first, second] };
        var reordered = CreateSnapshot(31) with { ScoreboardPlayers = [second, first] };
        var reorderDelta = SnapshotDeltaBudgeter.BuildUntrimmedSnapshot(reordered, baseline, Array.Empty<SnapshotDeltaBudgeter.Contribution>());
        Assert.Empty(reorderDelta.Message.ScoreboardPlayerPatches);
        Assert.Equal(new byte[] { 2, 1 }, SnapshotDelta.ToFullSnapshot(reorderDelta.Message, baseline).ScoreboardPlayers.Select(player => player.Slot));

        var empty = CreateSnapshot(32);
        var emptyDelta = SnapshotDeltaBudgeter.BuildUntrimmedSnapshot(empty, baseline, Array.Empty<SnapshotDeltaBudgeter.Contribution>());
        Assert.True(emptyDelta.Message.HasScoreboardDelta);
        Assert.Empty(emptyDelta.Message.ScoreboardPlayerOrder);
        Assert.Empty(SnapshotDelta.ToFullSnapshot(emptyDelta.Message, baseline).ScoreboardPlayers);
        Assert.True(ProtocolCodec.TryDeserialize(emptyDelta.Payload, out var decodedMessage));
        Assert.Empty(SnapshotDelta.ToFullSnapshot(Assert.IsType<SnapshotMessage>(decodedMessage), baseline).ScoreboardPlayers);
    }

    [Fact]
    public void ScoreboardDeltaRejectsMismatchedDecodedIdentitySlotAndOverflowMetadata()
    {
        var baselinePlayer = CreatePlayerState(1, 701, "Original");
        var baseline = CreateSnapshot(40) with { ScoreboardPlayers = [baselinePlayer] };
        var currentPlayer = baselinePlayer with { Name = "Changed" };
        var built = SnapshotDeltaBudgeter.BuildUntrimmedSnapshot(
            CreateSnapshot(41) with { ScoreboardPlayers = [currentPlayer] }, baseline,
            Array.Empty<SnapshotDeltaBudgeter.Contribution>());
        var patch = Assert.Single(built.Message.ScoreboardPlayerPatches);

        var wrongIdentity = patch with
        {
            BaseLength = 0,
            PrefixLength = 0,
            SuffixLength = 0,
            PlayerId = 702,
            ReplacementBytes = SerializeCanonicalSnapshotPlayerForTest(currentPlayer),
        };
        Assert.Throws<InvalidOperationException>(() => SnapshotDelta.ToFullSnapshot(
            built.Message with { ScoreboardPlayerPatches = [wrongIdentity] }, baseline));

        var wrongSlot = patch with
        {
            BaseLength = 0,
            PrefixLength = 0,
            SuffixLength = 0,
            ReplacementBytes = SerializeCanonicalSnapshotPlayerForTest(currentPlayer with { Slot = 2 }),
        };
        Assert.Throws<InvalidOperationException>(() => SnapshotDelta.ToFullSnapshot(
            built.Message with { ScoreboardPlayerPatches = [wrongSlot] }, baseline));

        var overflow = built.Message with
        {
            ScoreboardPlayerPatches = [patch with { PrefixLength = int.MaxValue }],
        };
        var malformedPayload = ProtocolCodec.Serialize(overflow, ProtocolCompressionSettings.Disabled);
        Assert.False(ProtocolCodec.TryDeserialize(malformedPayload, out _));

        var malformedUtf8Bytes = SerializeCanonicalSnapshotPlayerForTest(currentPlayer);
        malformedUtf8Bytes[7] = 0xff;
        var malformedUtf8Patch = patch with
        {
            BaseLength = 0,
            PrefixLength = 0,
            SuffixLength = 0,
            ReplacementBytes = malformedUtf8Bytes,
        };
        Assert.Throws<InvalidOperationException>(() => SnapshotDelta.ToFullSnapshot(
            built.Message with { ScoreboardPlayerPatches = [malformedUtf8Patch] }, baseline));
    }

    [Fact]
    public void OversizedButLegalScoreboardRecordsFallBackToCompleteRoster()
    {
        var largeId = new string('i', 96);
        var largePlayer = CreatePlayerState(1, 751, "Large state") with
        {
            OwnedGameplayItemIds = Enumerable.Repeat(largeId, byte.MaxValue).ToArray(),
            ReplicatedStates = Enumerable.Range(0, byte.MaxValue)
                .Select(index => new SnapshotReplicatedStateEntry(
                    new string((char)('a' + (index % 26)), 80),
                    largeId,
                    SnapshotReplicatedStateValueKind.Toggle,
                    BoolValue: true))
                .ToArray(),
        };
        Assert.True(SerializeCanonicalSnapshotPlayerForTest(largePlayer).Length > 64 * 1024);

        var built = SnapshotDeltaBudgeter.BuildUntrimmedSnapshot(
            CreateSnapshot(42) with { ScoreboardPlayers = [largePlayer] },
            baseline: null,
            contributions: Array.Empty<SnapshotDeltaBudgeter.Contribution>());

        Assert.False(built.Message.HasScoreboardDelta);
        Assert.Equal(new[] { largePlayer.PlayerId }, built.Message.ScoreboardPlayers.Select(player => player.PlayerId));
        Assert.True(ProtocolCodec.TryDeserialize(built.Payload, out var decoded));
        Assert.Equal(largePlayer.PlayerId, Assert.IsType<SnapshotMessage>(decoded).ScoreboardPlayers.Single().PlayerId);
    }

    [Fact]
    public void ScoreboardDeltaKeepsTwentyFourPlayerMovementAndLateStatusChangesCompact()
    {
        var baselinePlayers = Enumerable.Range(1, 24)
            .Select(slot => CreatePlayerState((byte)slot, 800 + slot, $"Player {slot:00}"))
            .ToArray();
        var currentPlayers = baselinePlayers.Select((player, index) => player with
        {
            X = player.X + 0.25f + index * 0.03125f,
            Y = player.Y - 0.125f,
            Health = index >= 16 ? (short)(player.Health - 17) : player.Health,
            PingMilliseconds = index >= 16 ? 42 + index : player.PingMilliseconds,
            ExperimentalCryoSlowTicksRemaining = index >= 16 ? index : player.ExperimentalCryoSlowTicksRemaining,
            KritzCritBoostDamageMultiplier = index >= 16 ? 1.5f : player.KritzCritBoostDamageMultiplier,
        }).ToArray();
        var baseline = CreateSnapshot(50) with { ScoreboardPlayers = baselinePlayers };
        var current = CreateSnapshot(51) with { ScoreboardPlayers = currentPlayers };
        var built = SnapshotDeltaBudgeter.BuildUntrimmedSnapshot(
            current, baseline, Array.Empty<SnapshotDeltaBudgeter.Contribution>());
        var movement = currentPlayers.Select(player => new SnapshotPlayerMovementState(
            player.Slot, player.X, player.Y, player.HorizontalSpeed, player.VerticalSpeed,
            player.IsGrounded, player.RemainingAirJumps, player.FacingDirectionX,
            player.AimDirectionDegrees, player.MovementState, player.IsTaunting,
            player.BurnIntensity)).ToArray();
        var lateStatus = currentPlayers.Skip(16).Select(player => new SnapshotPlayerStatusState(
            player.Slot, player.Health, player.MaxHealth, player.Ammo, player.MaxAmmo,
            player.Metal, player.IsCarryingIntel, player.IntelRechargeTicks)).ToArray();
        var compactDelta = built.Message with
        {
            PlayerMovementStates = movement,
            PlayerStatusStates = lateStatus,
        };
        var compactBytes = ProtocolCodec.Serialize(compactDelta, ProtocolCompressionSettings.Default);
        var decoded = Assert.IsType<SnapshotMessage>(Decode(compactBytes));
        var merged = SnapshotDelta.ToFullSnapshot(decoded, baseline);
        Assert.Equal(SerializeScoreboardOnly(RoundTripSnapshot(current).ScoreboardPlayers),
            SerializeScoreboardOnly(merged.ScoreboardPlayers));

        var fullRoster = current with
        {
            BaselineFrame = baseline.Frame,
            IsDelta = true,
            ScoreboardPlayers = currentPlayers,
        };
        var fullRosterWithMovement = fullRoster with
        {
            PlayerMovementStates = movement,
            PlayerStatusStates = lateStatus,
        };
        var fullRosterBytes = ProtocolCodec.Serialize(fullRosterWithMovement, ProtocolCompressionSettings.Default).Length;
        _output.WriteLine("Scoreboard LZ4 sample (24 players; movement plus eight late status changes): compact={0} bytes, full roster={1} bytes",
            compactBytes.Length, fullRosterBytes);
        Assert.True(compactBytes.Length < fullRosterBytes,
            $"Expected compact 24-player roster ({compactBytes.Length} bytes) below full roster ({fullRosterBytes} bytes).");
    }

    private static IProtocolMessage? Decode(byte[] payload)
    {
        Assert.True(ProtocolCodec.TryDeserialize(payload, out var message));
        return message;
    }

    private static SnapshotMessage RoundTripSnapshot(SnapshotMessage snapshot)
    {
        var payload = ProtocolCodec.Serialize(snapshot, ProtocolCompressionSettings.Disabled);
        Assert.True(ProtocolCodec.TryDeserialize(payload, out var decoded));
        return Assert.IsType<SnapshotMessage>(decoded);
    }

    private static byte[] SerializeScoreboardOnly(IReadOnlyList<SnapshotPlayerState> players)
    {
        return ProtocolCodec.Serialize(CreateSnapshot(40) with { ScoreboardPlayers = players }, ProtocolCompressionSettings.Disabled);
    }

    private static byte[] SerializeCanonicalSnapshotPlayerForTest(SnapshotPlayerState player)
    {
        var method = typeof(ProtocolCodec).GetMethod(
            "SerializeCanonicalSnapshotPlayer",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsType<byte[]>(method.Invoke(null, [player]));
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
            RedIntel: new SnapshotIntelState(0, 0f, 0f, true, false, 0),
            BlueIntel: new SnapshotIntelState(0, 0f, 0f, true, false, 0),
            Players: Array.Empty<SnapshotPlayerState>(),
            CombatTraces: Array.Empty<SnapshotCombatTraceState>(),
            SniperAimIndicators: Array.Empty<SnapshotSniperAimIndicatorState>(),
            Sentries: Array.Empty<SnapshotSentryState>(),
            Shots: Array.Empty<SnapshotShotState>(),
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
            MapContentHash: string.Empty)
        {
            SentryGibs = Array.Empty<SnapshotSentryGibState>(),
        };
    }

    private static SnapshotPlayerState CreatePlayerState(byte slot, int playerId, string name)
    {
        return new SnapshotPlayerState(
            Slot: slot,
            PlayerId: playerId,
            Name: name,
            Team: 1,
            ClassId: (byte)PlayerClass.Scout,
            IsAlive: true,
            IsAwaitingJoin: false,
            IsSpectator: false,
            RespawnTicks: 0,
            X: 64f + slot,
            Y: 96f + slot,
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
            RemainingAirJumps: 0,
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
            ChatBubbleAlpha: 0f);
    }

    private static SnapshotReplicatedStateEntry CreateCoreAbilityState(
        string key,
        SnapshotReplicatedStateValueKind kind,
        int intValue = 0,
        float floatValue = 0f,
        bool boolValue = false)
    {
        return new SnapshotReplicatedStateEntry(
            GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId,
            key,
            kind,
            intValue,
            floatValue,
            boolValue);
    }
}
