using System.Net;
using System.Reflection;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;
using OpenGarrison.Protocol;
using OpenGarrison.Server;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class SnapshotDeltaBudgeterTests
{
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
