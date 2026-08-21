using OpenGarrison.Client;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using OpenGarrison.Server;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class GameplayBuffHudAndReplicationTests
{
    [Fact]
    public void BuffCatalogReturnsNoPresentationWhenNoBuffIsActive()
    {
        Assert.Empty(GameplayBuffPresentationCatalog.Collect(false, false, 1f));
    }

    [Fact]
    public void BuffCatalogPresentsKritzTargetStats()
    {
        var presentation = Assert.Single(GameplayBuffPresentationCatalog.Collect(true, false, 1f));

        Assert.Equal(GameplayBuffPresentationCatalog.KritzCritTargetId, presentation.Id);
        Assert.Equal(["Critical Rate: +100%"], presentation.StatLines);
    }

    [Fact]
    public void BuffCatalogPresentsDispenserStats()
    {
        var presentation = Assert.Single(GameplayBuffPresentationCatalog.Collect(false, true, 1.25f));

        Assert.Equal(GameplayBuffPresentationCatalog.DispenserId, presentation.Id);
        Assert.Equal(
            ["Rate of Fire: +25%", "Reload Speed: +25%"],
            presentation.StatLines);
    }

    [Fact]
    public void BuffCatalogCombinesKritzAndDispenserInStableOrder()
    {
        var presentations = GameplayBuffPresentationCatalog.Collect(true, true, 1.25f);

        Assert.Collection(
            presentations,
            kritz => Assert.Equal(GameplayBuffPresentationCatalog.KritzCritTargetId, kritz.Id),
            dispenser => Assert.Equal(GameplayBuffPresentationCatalog.DispenserId, dispenser.Id));
    }

    [Theory]
    [InlineData(1f, "+0%")]
    [InlineData(1.25f, "+25%")]
    [InlineData(1.255f, "+25.5%")]
    [InlineData(1.005f, "+0.5%")]
    public void DispenserPercentageFormattingIsInvariantAndHumanFriendly(float multiplier, string expected)
    {
        Assert.Equal(
            expected,
            GameplayBuffPresentationCatalog.FormatMultiplierBonusPercentage(multiplier));
    }

    [Theory]
    [InlineData(false, false, false, false, false)]
    [InlineData(true, false, false, false, false)]
    [InlineData(true, true, false, false, false)]
    [InlineData(false, false, true, false, false)]
    [InlineData(true, false, false, true, true)]
    public void BuffIconVisibilityRequiresAlivePlayerAndAnyBuff(
        bool alive,
        bool awaitingJoin,
        bool hasLastToDieBonuses,
        bool hasNormalGameplayBuffs,
        bool expected)
    {
        Assert.Equal(
            expected,
            Game1.ShouldPresentLastToDieBuffIcon(
                alive,
                awaitingJoin,
                hasLastToDieBonuses,
                hasNormalGameplayBuffs));
    }

    [Fact]
    public void LegacySnapshotDispenserBuffRoundTripsAppliesAndMergesFromDelta()
    {
        var source = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        source.LocalPlayer.SetDispenserBuffed(true, 1.25f);
        var player = ServerHelpers.ToSnapshotPlayerState(
            source,
            SimulationWorld.LocalPlayerSlot,
            source.LocalPlayer,
            source.LocalPlayer,
            new SnapshotStringCache());
        var snapshot = CreateSnapshot(player);

        var payload = ProtocolCodec.Serialize(snapshot, ProtocolCompressionSettings.Disabled);
        Assert.True(ProtocolCodec.TryDeserialize(payload, out var decodedMessage));
        var decoded = Assert.IsType<SnapshotMessage>(decodedMessage);
        var decodedPlayer = Assert.Single(decoded.Players);
        Assert.True(decodedPlayer.IsDispenserBuffed);
        Assert.Equal(1.25f, decodedPlayer.DispenserAttackReloadSpeedMultiplier);

        var receiver = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        Assert.True(receiver.ApplySnapshot(decoded));
        Assert.True(receiver.LocalPlayer.IsDispenserBuffed);
        Assert.Equal(1.25f, receiver.LocalPlayer.DispenserAttackReloadSpeedMultiplier);

        var baseline = CreateSnapshot(player with
        {
            IsDispenserBuffed = false,
            DispenserAttackReloadSpeedMultiplier = 1f,
        }) with { Frame = 20 };
        var delta = baseline with
        {
            Frame = 21,
            BaselineFrame = 20,
            IsDelta = true,
            Players = [],
            PlayerExtendedStatusStates =
            [
                new SnapshotPlayerExtendedStatusState(
                    SimulationWorld.LocalPlayerSlot,
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
                    IsDispenserBuffed: true,
                    DispenserAttackReloadSpeedMultiplier: 1.25f),
            ],
        };

        var merged = SnapshotDelta.ToFullSnapshot(delta, baseline);
        var mergedPlayer = Assert.Single(merged.Players);
        Assert.True(mergedPlayer.IsDispenserBuffed);
        Assert.Equal(1.25f, mergedPlayer.DispenserAttackReloadSpeedMultiplier);
    }

    [Fact]
    public void Protocol64DispenserBuffRoundTripsAndApplies()
    {
        var source = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        source.LocalPlayer.SetDispenserBuffed(true, 1.25f);
        var published = Assert.Single(
            new Protocol64StatePublisher(source).BuildPlayerStateBatch(12).Players);
        var registry = new Protocol64SchemaRegistry();
        registry.Register(new Protocol64PlayerStateBatchSchema());
        var encoded = Protocol64FrameCodec.Encode(
            registry,
            new Protocol64PlayerStateBatch(1, 12, [published]),
            1,
            1,
            new Protocol64FrameEncodeOptions { Compression = Protocol64Compression.None });
        Assert.True(encoded.Succeeded, encoded.Fault?.Message);
        var decoded = Protocol64FrameCodec.Decode<Protocol64PlayerStateBatch>(encoded.Payload!, registry);
        Assert.True(decoded.Succeeded, decoded.Fault?.Message);
        var decodedPlayer = Assert.Single(decoded.Event!.Players);
        Assert.True(decodedPlayer.IsDispenserBuffed);
        Assert.Equal(1.25f, decodedPlayer.DispenserAttackReloadSpeedMultiplier);

        var receiver = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        Assert.True(receiver.ApplyProtocol64PlayerState(decodedPlayer));
        Assert.True(receiver.LocalPlayer.IsDispenserBuffed);
        Assert.Equal(1.25f, receiver.LocalPlayer.DispenserAttackReloadSpeedMultiplier);
    }

    private static SnapshotMessage CreateSnapshot(SnapshotPlayerState player)
    {
        return new SnapshotMessage(
            Frame: 10,
            TickRate: 30,
            LevelName: "ctf_truefort",
            MapAreaIndex: 1,
            MapAreaCount: 1,
            GameMode: 1,
            MatchPhase: 1,
            WinnerTeam: 0,
            TimeRemainingTicks: 300,
            RedCaps: 0,
            BlueCaps: 0,
            SpectatorCount: 0,
            LastProcessedInputSequence: 0,
            RedIntel: new SnapshotIntelState(1, 0f, 0f, true, false, 0),
            BlueIntel: new SnapshotIntelState(2, 0f, 0f, true, false, 0),
            Players: [player],
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
    }
}
