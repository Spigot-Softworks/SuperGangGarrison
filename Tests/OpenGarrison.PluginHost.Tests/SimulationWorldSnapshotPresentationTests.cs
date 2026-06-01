using System.Reflection;
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
    public void TryPresentNetworkGibDeathSuppressesLaterSnapshotDuplicate()
    {
        var world = new SimulationWorld();
        var initialSnapshot = CreateSnapshot(
            world,
            frame: 105,
            localPlayer: CreatePlayerState(1, 101, "Local", PlayerTeam.Red, PlayerClass.Scout, isAlive: true, gibDeaths: 0),
            remotePlayer: CreatePlayerState(2, 202, "Remote", PlayerTeam.Blue, PlayerClass.Soldier, isAlive: true, gibDeaths: 0));
        var deathSnapshot = CreateSnapshot(
            world,
            frame: 106,
            localPlayer: CreatePlayerState(1, 101, "Local", PlayerTeam.Red, PlayerClass.Scout, isAlive: true, gibDeaths: 0),
            remotePlayer: CreatePlayerState(2, 202, "Remote", PlayerTeam.Blue, PlayerClass.Soldier, isAlive: false, gibDeaths: 1));

        Assert.True(world.ApplySnapshot(initialSnapshot, localPlayerSlot: 1));
        Assert.True(world.TryPresentNetworkGibDeath(202, gibDeaths: 1));
        var immediateGibCount = world.PlayerGibs.Count;

        Assert.True(world.ApplySnapshot(deathSnapshot, localPlayerSlot: 1));

        Assert.Equal(immediateGibCount, world.PlayerGibs.Count);
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

    [Fact]
    public void ApplySnapshotDoesNotSpawnRemotePlayerGibsForNormalDeathAfterEarlierGibDeath()
    {
        var world = new SimulationWorld();
        var aliveAfterEarlierGibSnapshot = CreateSnapshot(
            world,
            frame: 115,
            localPlayer: CreatePlayerState(1, 101, "Local", PlayerTeam.Red, PlayerClass.Scout, isAlive: true, gibDeaths: 0),
            remotePlayer: CreatePlayerState(2, 202, "Remote", PlayerTeam.Blue, PlayerClass.Soldier, isAlive: true, gibDeaths: 1));
        var normalDeathSnapshot = CreateSnapshot(
            world,
            frame: 116,
            localPlayer: CreatePlayerState(1, 101, "Local", PlayerTeam.Red, PlayerClass.Scout, isAlive: true, gibDeaths: 0),
            remotePlayer: CreatePlayerState(2, 202, "Remote", PlayerTeam.Blue, PlayerClass.Soldier, isAlive: false, gibDeaths: 1));

        Assert.True(world.ApplySnapshot(aliveAfterEarlierGibSnapshot, localPlayerSlot: 1));
        Assert.Empty(world.PlayerGibs);

        Assert.True(world.ApplySnapshot(normalDeathSnapshot, localPlayerSlot: 1));

        Assert.Empty(world.PlayerGibs);
        Assert.DoesNotContain(world.PendingVisualEvents, visualEvent => visualEvent.EffectName == "GibBlood");
    }

    [Fact]
    public void ApplySnapshotRetainsMissingEnemySpyForScoreboard()
    {
        var world = new SimulationWorld();
        var localPlayer = CreatePlayerState(1, 101, "Local", PlayerTeam.Red, PlayerClass.Scout, isAlive: true, gibDeaths: 0);
        var remoteSpy = CreatePlayerState(2, 202, "Remote Spy", PlayerTeam.Blue, PlayerClass.Spy, isAlive: true, gibDeaths: 0) with
        {
            X = 64f,
        };
        var visibleSnapshot = CreateSnapshot(world, 120, localPlayer, remoteSpy);
        var hiddenSnapshot = CreateSnapshot(world, 121, localPlayer);

        Assert.True(world.ApplySnapshot(visibleSnapshot, localPlayerSlot: 1));
        Assert.Single(world.RemoteSnapshotPlayers);
        Assert.Single(world.RemoteSnapshotScoreboardPlayers);

        Assert.True(world.ApplySnapshot(hiddenSnapshot, localPlayerSlot: 1));

        Assert.Empty(world.RemoteSnapshotPlayers);
        var scoreboardPlayer = Assert.Single(world.RemoteSnapshotScoreboardPlayers);
        Assert.Equal(remoteSpy.PlayerId, scoreboardPlayer.Id);
        Assert.True(world.TryGetPlayerNetworkSlot(scoreboardPlayer, out var slot));
        Assert.Equal(remoteSpy.Slot, slot);
    }

    [Fact]
    public void ApplySnapshotRetainsMissingBackstabAnimatingEnemySpyForScoreboard()
    {
        var world = new SimulationWorld();
        var localPlayer = CreatePlayerState(1, 101, "Local", PlayerTeam.Red, PlayerClass.Scout, isAlive: true, gibDeaths: 0);
        var remoteSpy = CreatePlayerState(2, 202, "Remote Spy", PlayerTeam.Blue, PlayerClass.Spy, isAlive: true, gibDeaths: 0) with
        {
            X = 64f,
            IsSpyCloaked = true,
            SpyCloakAlpha = 0f,
            SpyBackstabVisualTicksRemaining = 24,
        };
        var visibleSnapshot = CreateSnapshot(world, 125, localPlayer, remoteSpy);
        var hiddenSnapshot = CreateSnapshot(world, 126, localPlayer);

        Assert.True(world.ApplySnapshot(visibleSnapshot, localPlayerSlot: 1));

        Assert.True(world.ApplySnapshot(hiddenSnapshot, localPlayerSlot: 1));

        Assert.Empty(world.RemoteSnapshotPlayers);
        var scoreboardPlayer = Assert.Single(world.RemoteSnapshotScoreboardPlayers);
        Assert.Equal(remoteSpy.PlayerId, scoreboardPlayer.Id);
    }

    [Fact]
    public void ApplySnapshotRemovesMissingNonSpyFromScoreboard()
    {
        var world = new SimulationWorld();
        var localPlayer = CreatePlayerState(1, 101, "Local", PlayerTeam.Red, PlayerClass.Scout, isAlive: true, gibDeaths: 0);
        var remoteSoldier = CreatePlayerState(2, 202, "Remote Soldier", PlayerTeam.Blue, PlayerClass.Soldier, isAlive: true, gibDeaths: 0);
        var visibleSnapshot = CreateSnapshot(world, 130, localPlayer, remoteSoldier);
        var removedSnapshot = CreateSnapshot(world, 131, localPlayer);

        Assert.True(world.ApplySnapshot(visibleSnapshot, localPlayerSlot: 1));
        Assert.Single(world.RemoteSnapshotScoreboardPlayers);

        Assert.True(world.ApplySnapshot(removedSnapshot, localPlayerSlot: 1));

        Assert.Empty(world.RemoteSnapshotPlayers);
        Assert.Empty(world.RemoteSnapshotScoreboardPlayers);
    }

    [Fact]
    public void LocalGoreEffectsDisabledSuppressesPlayerGibsAndBloodDrops()
    {
        var world = new SimulationWorld { LocalGoreEffectsEnabled = false };
        world.CompleteLocalPlayerJoin(PlayerClass.Soldier);

        InvokeRegisterBloodEffect(world, world.LocalPlayer.X, world.LocalPlayer.Y, directionDegrees: 0f, count: 3);

        Assert.Empty(world.BloodDrops);
        Assert.DoesNotContain(world.PendingVisualEvents, static visualEvent => visualEvent.EffectName == "Blood");

        InvokeKillPlayer(world, world.LocalPlayer, gibbed: true);

        Assert.Equal(1, world.LocalPlayer.GibDeaths);
        Assert.Empty(world.PlayerGibs);
        Assert.Empty(world.BloodDrops);
        Assert.DoesNotContain(world.PendingVisualEvents, static visualEvent => visualEvent.EffectName == "GibBlood");
    }

    [Fact]
    public void GetNetworkPlayerDeathCamRefreshesTrackedKillerFocus()
    {
        var world = new SimulationWorld();
        world.CompleteLocalPlayerJoin(PlayerClass.Scout);
        Assert.True(world.TryPrepareNetworkPlayerJoin(2));
        Assert.True(world.TrySetNetworkPlayerTeam(2, PlayerTeam.Blue));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(2, PlayerClass.Soldier));
        Assert.True(world.TryGetNetworkPlayer(2, out var killer));
        killer.TeleportTo(128f, 96f);

        var killMethod = typeof(SimulationWorld).GetMethod("KillPlayer", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(killMethod);
        _ = killMethod!.Invoke(
            world,
            [
                world.LocalPlayer,
                false,
                killer,
                "RocketKL",
                DeadBodyAnimationKind.Default,
                null,
                null,
                null,
                true,
                true,
                false,
                true,
            ]);

        killer.TeleportTo(320f, 192f);

        var deathCam = world.GetNetworkPlayerDeathCam(SimulationWorld.LocalPlayerSlot);
        Assert.NotNull(deathCam);
        Assert.Equal(killer.X, deathCam!.FocusX);
        Assert.Equal(killer.Y, deathCam.FocusY);
    }

    private static SnapshotMessage CreateSnapshot(
        SimulationWorld world,
        ulong frame,
        SnapshotPlayerState localPlayer,
        SnapshotPlayerState? remotePlayer = null)
    {
        var players = remotePlayer is null
            ? new[] { localPlayer }
            : new[] { localPlayer, remotePlayer };
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
            Players: players,
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

    private static void InvokeRegisterBloodEffect(SimulationWorld world, float x, float y, float directionDegrees, int count)
    {
        var method = typeof(SimulationWorld).GetMethod("RegisterBloodEffect", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        _ = method!.Invoke(world, [x, y, directionDegrees, count]);
    }

    private static void InvokeKillPlayer(SimulationWorld world, PlayerEntity player, bool gibbed)
    {
        var method = typeof(SimulationWorld).GetMethod("KillPlayer", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        _ = method!.Invoke(
            world,
            [
                player,
                gibbed,
                null,
                "ExplodeKL",
                DeadBodyAnimationKind.Default,
                null,
                null,
                null,
                true,
                true,
                false,
                true,
            ]);
    }
}
