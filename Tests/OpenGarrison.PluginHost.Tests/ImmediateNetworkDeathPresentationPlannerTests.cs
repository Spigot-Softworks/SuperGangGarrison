using System;
using System.Reflection;
using OpenGarrison.Client;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class ImmediateNetworkDeathPresentationPlannerTests
{
    [Fact]
    public void TryCreateReturnsTemporaryDeadBodyForFatalPlayerDamageWithoutAuthoritativeCorpse()
    {
        var plannerMethod = GetPlannerMethod();
        var targetPlayer = CreateDeadPlayer(playerId: 7, PlayerTeam.Blue, PlayerClass.Soldier, x: 100f, y: 200f, facingDirectionX: 1f);
        var snapshot = CreateSnapshot(Array.Empty<SnapshotDeadBodyState>());
        var damageEvent = new SnapshotDamageEvent(
            Amount: 90,
            AttackerPlayerId: 4,
            AssistedByPlayerId: -1,
            TargetKind: (byte)DamageTargetKind.Player,
            TargetEntityId: 7,
            X: 100f,
            Y: 200f,
            WasFatal: true,
            EventId: 11,
            SourceFrame: 22);

        var result = plannerMethod.Invoke(null, [snapshot, damageEvent, targetPlayer, 90]);

        Assert.NotNull(result);
        Assert.Equal(7, GetPresentationValue<int>(result!, "SourcePlayerId"));
        Assert.Equal(PlayerClass.Soldier, GetPresentationValue<PlayerClass>(result!, "ClassId"));
        Assert.Equal(PlayerTeam.Blue, GetPresentationValue<PlayerTeam>(result!, "Team"));
        Assert.Equal(100f, GetPresentationValue<float>(result!, "X"));
        Assert.Equal(200f, GetPresentationValue<float>(result!, "Y"));
        Assert.False(GetPresentationValue<bool>(result!, "FacingLeft"));
        Assert.Equal(90, GetPresentationValue<int>(result!, "TicksRemaining"));
    }

    [Fact]
    public void TryCreateSkipsTemporaryDeadBodyWhenAuthoritativeCorpseAlreadyExists()
    {
        var plannerMethod = GetPlannerMethod();
        var targetPlayer = CreateDeadPlayer(playerId: 7, PlayerTeam.Red, PlayerClass.Medic, x: 144f, y: 88f, facingDirectionX: -1f);
        var snapshot = CreateSnapshot(
        [
            new SnapshotDeadBodyState(
                Id: 301,
                SourcePlayerId: 7,
                Team: (byte)PlayerTeam.Red,
                ClassId: (byte)PlayerClass.Medic,
                AnimationKind: (byte)DeadBodyAnimationKind.Default,
                X: 144f,
                Y: 88f,
                Width: targetPlayer.Width,
                Height: targetPlayer.Height,
                HorizontalSpeed: 0f,
                VerticalSpeed: 0f,
                FacingLeft: true,
                TicksRemaining: 300),
        ]);
        var damageEvent = new SnapshotDamageEvent(
            Amount: 120,
            AttackerPlayerId: 2,
            AssistedByPlayerId: -1,
            TargetKind: (byte)DamageTargetKind.Player,
            TargetEntityId: 7,
            X: 144f,
            Y: 88f,
            WasFatal: true,
            EventId: 19,
            SourceFrame: 31);

        var result = plannerMethod.Invoke(null, [snapshot, damageEvent, targetPlayer, 90]);

        Assert.Null(result);
    }

    private static MethodInfo GetPlannerMethod()
    {
        var plannerType = typeof(Game1).Assembly.GetType("OpenGarrison.Client.ImmediateNetworkDeathPresentationPlanner");
        Assert.NotNull(plannerType);
        var method = plannerType!.GetMethod("TryCreate", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return method!;
    }

    private static T GetPresentationValue<T>(object value, string propertyName)
    {
        var property = value.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(property);
        return Assert.IsType<T>(property!.GetValue(value));
    }

    private static PlayerEntity CreateDeadPlayer(int playerId, PlayerTeam team, PlayerClass classId, float x, float y, float facingDirectionX)
    {
        var player = new PlayerEntity(playerId, CharacterClassCatalog.GetDefinition(classId), "Target");
        player.ApplyNetworkState(
            team: team,
            classDefinition: CharacterClassCatalog.GetDefinition(classId),
            isAlive: false,
            x: x,
            y: y,
            horizontalSpeed: 0f,
            verticalSpeed: 0f,
            health: 0,
            currentShells: 0,
            kills: 0,
            deaths: 0,
            caps: 0,
            points: 0f,
            healPoints: 0,
            activeDominationCount: 0,
            isDominatingLocalViewer: false,
            isDominatedByLocalViewer: false,
            metal: 0f,
            isGrounded: true,
            remainingAirJumps: 0,
            isCarryingIntel: false,
            intelRechargeTicks: 0f,
            isSpyCloaked: false,
            spyCloakAlpha: 1f,
            isSpySuperjumping: false,
            spySuperjumpHorizontalVelocity: 0f,
            spySuperjumpCooldownTicksRemaining: 0,
            spyBackstabVisualTicksRemaining: 0,
            isUbered: false,
            isKritzCritBoosted: false,
            isHeavyEating: false,
            heavyEatTicksRemaining: 0,
            isSniperScoped: false,
            sniperChargeTicks: 0,
            facingDirectionX: facingDirectionX,
            aimDirectionDegrees: 0f,
            aimWorldX: x + facingDirectionX,
            aimWorldY: y,
            isTaunting: false,
            tauntFrameIndex: 0f,
            isChatBubbleVisible: false,
            chatBubbleFrameIndex: 0,
            chatBubbleAlpha: 0f);
        return player;
    }

    private static SnapshotMessage CreateSnapshot(SnapshotDeadBodyState[] deadBodies)
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
            TimeRemainingTicks: 0,
            RedCaps: 0,
            BlueCaps: 0,
            SpectatorCount: 0,
            LastProcessedInputSequence: 0,
            RedIntel: new SnapshotIntelState((byte)PlayerTeam.Red, 0f, 0f, true, false, 0),
            BlueIntel: new SnapshotIntelState((byte)PlayerTeam.Blue, 0f, 0f, true, false, 0),
            Players: Array.Empty<SnapshotPlayerState>(),
            CombatTraces: Array.Empty<SnapshotCombatTraceState>(),
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
            DeadBodies: deadBodies,
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
            SoundEvents: Array.Empty<SnapshotSoundEvent>())
        {
            PlayerGibs = Array.Empty<SnapshotPlayerGibState>(),
        };
    }
}
