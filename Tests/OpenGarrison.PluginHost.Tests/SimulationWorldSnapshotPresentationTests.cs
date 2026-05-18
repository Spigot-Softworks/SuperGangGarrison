using OpenGarrison.Core;
using OpenGarrison.Protocol;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class SimulationWorldSnapshotPresentationTests
{
    [Fact]
    public void ApplySnapshotSpawnsRemotePlayerGibsWhenGibDeathStateAdvances()
    {
        var world = new SimulationWorld();
        var initialSnapshot = CreateSnapshot(
            world,
            frame: 100,
            localPlayer: CreatePlayerState(1, 101, "Local", PlayerTeam.Red, PlayerClass.Scout, isAlive: true, gibDeaths: 0),
            remotePlayer: CreatePlayerState(2, 202, "Remote", PlayerTeam.Blue, PlayerClass.Soldier, isAlive: true, gibDeaths: 0));
        var deathSnapshot = CreateSnapshot(
            world,
            frame: 101,
            localPlayer: CreatePlayerState(1, 101, "Local", PlayerTeam.Red, PlayerClass.Scout, isAlive: true, gibDeaths: 0),
            remotePlayer: CreatePlayerState(2, 202, "Remote", PlayerTeam.Blue, PlayerClass.Soldier, isAlive: false, gibDeaths: 1));

        Assert.True(world.ApplySnapshot(initialSnapshot, localPlayerSlot: 1));
        Assert.Empty(world.PlayerGibs);

        Assert.True(world.ApplySnapshot(deathSnapshot, localPlayerSlot: 1));

        Assert.NotEmpty(world.PlayerGibs);
        Assert.Contains(world.PendingVisualEvents, visualEvent => visualEvent.EffectName == "GibBlood");
    }

    [Fact]
    public void ApplySnapshotDoesNotSpawnRemotePlayerGibsWhenAlreadyDeadStateAdvances()
    {
        var world = new SimulationWorld();
        var initialSnapshot = CreateSnapshot(
            world,
            frame: 110,
            localPlayer: CreatePlayerState(1, 101, "Local", PlayerTeam.Red, PlayerClass.Scout, isAlive: true, gibDeaths: 0),
            remotePlayer: CreatePlayerState(2, 202, "Remote", PlayerTeam.Blue, PlayerClass.Soldier, isAlive: false, gibDeaths: 0));
        var retainedDeadSnapshot = CreateSnapshot(
            world,
            frame: 111,
            localPlayer: CreatePlayerState(1, 101, "Local", PlayerTeam.Red, PlayerClass.Scout, isAlive: true, gibDeaths: 0),
            remotePlayer: CreatePlayerState(2, 202, "Remote", PlayerTeam.Blue, PlayerClass.Soldier, isAlive: false, gibDeaths: 1));

        Assert.True(world.ApplySnapshot(initialSnapshot, localPlayerSlot: 1));
        Assert.True(world.ApplySnapshot(retainedDeadSnapshot, localPlayerSlot: 1));

        Assert.Empty(world.PlayerGibs);
        Assert.DoesNotContain(world.PendingVisualEvents, visualEvent => visualEvent.EffectName == "GibBlood");
    }

    private static SnapshotMessage CreateSnapshot(
        SimulationWorld world,
        ulong frame,
        SnapshotPlayerState localPlayer,
        SnapshotPlayerState remotePlayer)
    {
        return new SnapshotMessage(
            frame,
            TickRate: 60,
            LevelName: world.Level.Name,
            MapAreaIndex: (byte)world.Level.MapAreaIndex,
            MapAreaCount: (byte)world.Level.MapAreaCount,
            GameMode: (byte)GameModeKind.CaptureTheFlag,
            MatchPhase: 1,
            WinnerTeam: 0,
            TimeRemainingTicks: 0,
            RedCaps: 0,
            BlueCaps: 0,
            SpectatorCount: 0,
            LastProcessedInputSequence: 0,
            RedIntel: new SnapshotIntelState((byte)PlayerTeam.Red, 0f, 0f, true, false, 0),
            BlueIntel: new SnapshotIntelState((byte)PlayerTeam.Blue, 0f, 0f, true, false, 0),
            Players: [localPlayer, remotePlayer],
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

    private static SnapshotPlayerState CreatePlayerState(
        byte slot,
        int playerId,
        string name,
        PlayerTeam team,
        PlayerClass classId,
        bool isAlive,
        short gibDeaths)
    {
        return new SnapshotPlayerState(
            Slot: slot,
            PlayerId: playerId,
            Name: name,
            Team: (byte)team,
            ClassId: (byte)classId,
            IsAlive: isAlive,
            IsAwaitingJoin: false,
            IsSpectator: false,
            RespawnTicks: isAlive ? 0 : 120,
            X: 128f + slot,
            Y: 96f,
            HorizontalSpeed: 0f,
            VerticalSpeed: 0f,
            Health: isAlive ? (short)100 : (short)0,
            MaxHealth: 100,
            Ammo: 6,
            MaxAmmo: 6,
            Kills: 0,
            Deaths: isAlive ? (short)0 : (short)1,
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
            SniperChargeTicks: 0,
            IsUsingBinoculars: false,
            BinocularsFocusX: 0f,
            BinocularsFocusY: 0f,
            FacingDirectionX: 1f,
            AimDirectionDegrees: 0f,
            IsTaunting: false,
            TauntFrameIndex: 0f,
            IsChatBubbleVisible: false,
            ChatBubbleFrameIndex: 0,
            ChatBubbleAlpha: 0f,
            GibDeaths: gibDeaths);
    }
}
