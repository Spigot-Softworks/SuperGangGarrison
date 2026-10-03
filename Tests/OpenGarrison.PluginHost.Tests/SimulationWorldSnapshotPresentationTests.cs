using System.Reflection;
using OpenGarrison.Core;
using OpenGarrison.Core.LastToDie;
using OpenGarrison.Protocol;
using OpenGarrison.Server;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class SimulationWorldSnapshotPresentationTests
{
    [Fact]
    public void SnapshotProducerAndWorldPreserveBulletDamageKnockbackAndLifetime()
    {
        var world = new SimulationWorld();
        var local = CreatePlayerState(1, 101, "Local", PlayerTeam.Red, PlayerClass.Scout, isAlive: true, gibDeaths: 0);
        var owner = CreatePlayerState(2, 202, "Remote Scout", PlayerTeam.Blue, PlayerClass.Scout, isAlive: true, gibDeaths: 0);
        var source = new ShotProjectileEntity(
            899,
            PlayerTeam.Blue,
            owner.PlayerId,
            128f,
            96f,
            12f,
            0f,
            damagePerHit: 9.5f,
            playerKnockbackImpulse: 1.25f,
            playerKnockbackAirborneVerticalScale: 0.5f,
            playerKnockbackGroundedVerticalScale: 0.25f);
        source.AdvanceOneTick();
        var snapshot = CreateSnapshot(world, frame: 79, localPlayer: local, remotePlayer: owner) with
        {
            Shots = [world.Snapshots.ToSnapshotBulletState(source)],
        };

        Assert.True(world.SnapshotApply.ApplySnapshot(snapshot, localPlayerSlot: 1));

        var recreated = Assert.Single(world.Shots);
        Assert.Equal(source.DamageValue, recreated.DamageValue);
        Assert.Equal(source.PlayerKnockbackPayload, recreated.PlayerKnockbackPayload);
        Assert.Equal(source.TicksRemaining, recreated.TicksRemaining);
    }

    [Fact]
    public void ApplySnapshotReplacesRemotePlayerWhenAVisibleSlotGetsANewPlayerId()
    {
        var world = new SimulationWorld();
        var local = CreatePlayerState(1, 101, "Local", PlayerTeam.Red, PlayerClass.Scout, isAlive: true, gibDeaths: 0);
        var firstRemote = CreatePlayerState(2, 202, "First remote", PlayerTeam.Blue, PlayerClass.Soldier, isAlive: true, gibDeaths: 0);
        var secondRemote = CreatePlayerState(2, 303, "Second remote", PlayerTeam.Blue, PlayerClass.Soldier, isAlive: true, gibDeaths: 0) with
        {
            X = 640f,
            Y = 320f,
        };

        Assert.True(world.SnapshotApply.ApplySnapshot(CreateSnapshot(world, 80, local, firstRemote), localPlayerSlot: 1));
        var firstAppliedPlayer = Assert.Single(world.RemoteSnapshotPlayers);
        Assert.Equal(202, firstAppliedPlayer.Id);

        Assert.True(world.SnapshotApply.ApplySnapshot(CreateSnapshot(world, 81, local, secondRemote), localPlayerSlot: 1));

        var secondAppliedPlayer = Assert.Single(world.RemoteSnapshotPlayers);
        Assert.Equal(303, secondAppliedPlayer.Id);
        Assert.Equal("Second remote", secondAppliedPlayer.DisplayName);
        Assert.Equal(640f, secondAppliedPlayer.X);
        Assert.Equal(320f, secondAppliedPlayer.Y);
        Assert.NotSame(firstAppliedPlayer, secondAppliedPlayer);
        var secondScoreboardPlayer = Assert.Single(world.RemoteSnapshotScoreboardPlayers);
        Assert.Equal(303, secondScoreboardPlayer.Id);
        Assert.Same(secondAppliedPlayer, secondScoreboardPlayer);
    }

    [Fact]
    public void ApplySnapshotRetainsPendingSoundEventsAcrossNewerSnapshots()
    {
        var world = new SimulationWorld();
        var local = CreatePlayerState(1, 101, "Local", PlayerTeam.Red, PlayerClass.Scout, isAlive: true, gibDeaths: 0);
        var first = CreateSnapshot(world, frame: 80, localPlayer: local) with
        {
            SoundEvents =
            [
                new SnapshotSoundEvent("JumpSnd", 128f, 96f, EventId: 7001, SourceFrame: 80, SourcePlayerId: 101),
            ],
        };
        var second = CreateSnapshot(world, frame: 81, localPlayer: local) with
        {
            SoundEvents =
            [
                new SnapshotSoundEvent("RocketSnd", 160f, 96f, EventId: 7002, SourceFrame: 81, SourcePlayerId: 202),
            ],
        };
        var retransmit = CreateSnapshot(world, frame: 82, localPlayer: local) with
        {
            SoundEvents =
            [
                new SnapshotSoundEvent("JumpSnd", 128f, 96f, EventId: 7001, SourceFrame: 80, SourcePlayerId: 101),
            ],
        };

        Assert.True(world.SnapshotApply.ApplySnapshot(first, localPlayerSlot: 1));
        Assert.True(world.SnapshotApply.ApplySnapshot(second, localPlayerSlot: 1));
        Assert.True(world.SnapshotApply.ApplySnapshot(retransmit, localPlayerSlot: 1));

        Assert.Equal(2, world.PendingSoundEvents.Count);
        Assert.Contains(world.PendingSoundEvents, sound => sound.EventId == 7001);
        Assert.Contains(world.PendingSoundEvents, sound => sound.EventId == 7002);
    }

    [Fact]
    public void ApplySnapshotRecreatesRevolverWithItsImmutablePerkPayload()
    {
        var world = new SimulationWorld();
        var profile = LastToDieSpyRevolverProfile.FromPerks(
            new HashSet<LastToDiePerkId>
            {
                LastToDiePerkIds.Spy.Blunderbuss1,
                LastToDiePerkIds.Spy.Blunderbuss2,
                LastToDiePerkIds.Spy.Ricochet,
                LastToDiePerkIds.Spy.LuckyStrike,
            });
        var snapshot = CreateSnapshot(
            world,
            frame: 80,
            localPlayer: CreatePlayerState(1, 101, "Local", PlayerTeam.Red, PlayerClass.Scout, isAlive: true, gibDeaths: 0),
            remotePlayer: CreatePlayerState(2, 202, "Remote Spy", PlayerTeam.Blue, PlayerClass.Spy, isAlive: true, gibDeaths: 0)) with
        {
            RevolverShots =
            [
                new SnapshotShotState(
                    900,
                    (byte)PlayerTeam.Blue,
                    202,
                    128f,
                    96f,
                    12f,
                    0f,
                    20,
                    IsCritical: true,
                    DamageValue: 11.2f,
                    LastToDieRevolverProfile: profile.Encode(),
                    AppliesLuckyStrikeStun: true,
                    PlayerKnockbackImpulse: 2.5f,
                    PlayerKnockbackAirborneVerticalScale: 0.5f,
                    PlayerKnockbackGroundedVerticalScale: 0.25f),
            ],
        };

        Assert.True(world.SnapshotApply.ApplySnapshot(snapshot, localPlayerSlot: 1));

        var shot = Assert.Single(world.RevolverShots);
        Assert.Equal(11.2f, shot.DamageValue);
        Assert.Equal(profile, shot.LastToDieProfile);
        Assert.True(shot.IsCritical);
        Assert.True(shot.AppliesLuckyStrikeStun);
        Assert.Equal(new BulletKnockbackPayload(2.5f, 0.5f, 0.25f), shot.PlayerKnockbackPayload);
    }

    [Fact]
    public void ApplySnapshotReplacesRevolverWhenAuthoritativeImmutablePayloadDiffers()
    {
        var world = new SimulationWorld();
        var local = CreatePlayerState(1, 101, "Local", PlayerTeam.Red, PlayerClass.Scout, isAlive: true, gibDeaths: 0);
        var owner = CreatePlayerState(2, 202, "Remote Spy", PlayerTeam.Blue, PlayerClass.Spy, isAlive: true, gibDeaths: 0);
        var initial = CreateSnapshot(world, frame: 90, localPlayer: local, remotePlayer: owner) with
        {
            RevolverShots =
            [
                new SnapshotShotState(901, (byte)PlayerTeam.Blue, 202, 128f, 96f, 12f, 0f, 20),
            ],
        };
        var profile = LastToDieSpyRevolverProfile.FromPerks(
            new HashSet<LastToDiePerkId>
            {
                LastToDiePerkIds.Spy.Ricochet,
                LastToDiePerkIds.Spy.LuckyStrike,
            });
        var corrected = CreateSnapshot(world, frame: 91, localPlayer: local, remotePlayer: owner) with
        {
            RevolverShots =
            [
                new SnapshotShotState(
                    901,
                    (byte)PlayerTeam.Blue,
                    202,
                    130f,
                    96f,
                    12f,
                    0f,
                    19,
                    DamageValue: 28f,
                    LastToDieRevolverProfile: profile.Encode(),
                    AppliesLuckyStrikeStun: true),
            ],
        };

        Assert.True(world.SnapshotApply.ApplySnapshot(initial, localPlayerSlot: 1));
        var predicted = Assert.Single(world.RevolverShots);
        Assert.True(world.SnapshotApply.ApplySnapshot(corrected, localPlayerSlot: 1));

        var authoritative = Assert.Single(world.RevolverShots);
        Assert.NotSame(predicted, authoritative);
        Assert.Equal(profile, authoritative.LastToDieProfile);
        Assert.True(authoritative.AppliesLuckyStrikeStun);
    }





    [Fact]
    public void ApplySnapshotAddsOnlineKillFeedEntryAfterProtocolRoundTrip()
    {
        var world = new SimulationWorld();
        var localPlayer = CreatePlayerState(1, 101, "Local", PlayerTeam.Red, PlayerClass.Scout, isAlive: true, gibDeaths: 0);
        var remotePlayer = CreatePlayerState(2, 202, "Remote", PlayerTeam.Blue, PlayerClass.Soldier, isAlive: false, gibDeaths: 0);
        var killFeedEntry = new SnapshotKillFeedEntry(
            "Local",
            (byte)PlayerTeam.Red,
            "ScatterKL",
            "Remote",
            (byte)PlayerTeam.Blue,
            EventId: 77)
        {
            InvolvedPlayerIds = [101, 202],
        };
        var snapshot = CreateSnapshot(world, frame: 109, localPlayer, remotePlayer) with
        {
            KillFeed = [killFeedEntry],
        };
        var payload = ProtocolCodec.Serialize(snapshot, ProtocolCompressionSettings.Disabled);

        Assert.True(ProtocolCodec.TryDeserialize(payload, out var roundTripped));
        var roundTrippedSnapshot = Assert.IsType<SnapshotMessage>(roundTripped);

        Assert.True(world.SnapshotApply.ApplySnapshot(roundTrippedSnapshot, localPlayerSlot: 1));

        var entry = Assert.Single(world.KillFeedEntries);
        Assert.Equal(killFeedEntry.EventId, entry.EventId);
        Assert.Equal("Local", entry.KillerName);
        Assert.Equal("Remote", entry.VictimName);
    }


    [Fact]
    public void ApplySnapshotForcesAwaitingJoinLocalPlayerNonRenderable()
    {
        var world = new SimulationWorld();
        var awaitingLocalPlayer = CreatePlayerState(
            1,
            101,
            "Local",
            PlayerTeam.Red,
            PlayerClass.Scout,
            isAlive: true,
            gibDeaths: 0) with
            {
                IsAwaitingJoin = true,
                Health = 125,
                RespawnTicks = 0,
            };
        var snapshot = CreateSnapshot(
            world,
            frame: 112,
            localPlayer: awaitingLocalPlayer);

        Assert.True(world.SnapshotApply.ApplySnapshot(snapshot, localPlayerSlot: 1));

        Assert.True(world.LocalPlayerAwaitingJoin);
        Assert.False(world.LocalPlayer.IsAlive);
        Assert.Equal(0, world.LocalPlayer.Health);
    }

    [Fact]
    public void ApplySnapshotForcesAwaitingJoinRemotePlayerNonRenderable()
    {
        var world = new SimulationWorld();
        var localPlayer = CreatePlayerState(1, 101, "Local", PlayerTeam.Red, PlayerClass.Scout, isAlive: true, gibDeaths: 0);
        var awaitingRemotePlayer = CreatePlayerState(
            2,
            202,
            "Remote",
            PlayerTeam.Blue,
            PlayerClass.Soldier,
            isAlive: true,
            gibDeaths: 0) with
            {
                IsAwaitingJoin = true,
                Health = 200,
                RespawnTicks = 0,
            };
        var snapshot = CreateSnapshot(
            world,
            frame: 113,
            localPlayer: localPlayer,
            remotePlayer: awaitingRemotePlayer);

        Assert.True(world.SnapshotApply.ApplySnapshot(snapshot, localPlayerSlot: 1));

        var remote = Assert.Single(world.RemoteSnapshotPlayers);
        Assert.True(world.IsRemoteSnapshotPlayerAwaitingJoin(remote));
        Assert.False(remote.IsAlive);
        Assert.Equal(0, remote.Health);
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

        Assert.True(world.SnapshotApply.ApplySnapshot(visibleSnapshot, localPlayerSlot: 1));
        Assert.Single(world.RemoteSnapshotPlayers);
        Assert.Single(world.RemoteSnapshotScoreboardPlayers);

        Assert.True(world.SnapshotApply.ApplySnapshot(hiddenSnapshot, localPlayerSlot: 1));

        Assert.Empty(world.RemoteSnapshotPlayers);
        var scoreboardPlayer = Assert.Single(world.RemoteSnapshotScoreboardPlayers);
        Assert.Equal(remoteSpy.PlayerId, scoreboardPlayer.Id);
        Assert.True(world.NetworkPlayers.TryGetPlayerNetworkSlot(scoreboardPlayer, out var slot));
        Assert.Equal(remoteSpy.Slot, slot);
    }

    [Fact]
    public void ApplySnapshotUsesExplicitScoreboardRosterForHiddenEnemySpy()
    {
        var world = new SimulationWorld();
        var localPlayer = CreatePlayerState(1, 101, "Local", PlayerTeam.Red, PlayerClass.Scout, isAlive: true, gibDeaths: 0);
        var remoteSpy = CreatePlayerState(2, 202, "Remote Spy", PlayerTeam.Blue, PlayerClass.Spy, isAlive: true, gibDeaths: 0) with
        {
            X = 64f,
        };
        var visibleSnapshot = CreateSnapshot(world, 122, localPlayer, remoteSpy);
        var hiddenSnapshot = CreateSnapshot(world, 123, localPlayer) with
        {
            ScoreboardPlayers = [localPlayer, remoteSpy],
            RemovedPlayerIds = [remoteSpy.Slot],
        };
        var visibleAgainSnapshot = CreateSnapshot(world, 124, localPlayer, remoteSpy);

        Assert.True(world.SnapshotApply.ApplySnapshot(visibleSnapshot, localPlayerSlot: 1));
        Assert.Single(world.RemoteSnapshotPlayers);
        Assert.Single(world.RemoteSnapshotScoreboardPlayers);

        Assert.True(world.SnapshotApply.ApplySnapshot(hiddenSnapshot, localPlayerSlot: 1));

        Assert.Empty(world.RemoteSnapshotPlayers);
        var hiddenScoreboardPlayer = Assert.Single(world.RemoteSnapshotScoreboardPlayers);
        Assert.Equal(remoteSpy.PlayerId, hiddenScoreboardPlayer.Id);
        Assert.True(world.NetworkPlayers.TryGetPlayerNetworkSlot(hiddenScoreboardPlayer, out var hiddenSlot));
        Assert.Equal(remoteSpy.Slot, hiddenSlot);

        Assert.True(world.SnapshotApply.ApplySnapshot(visibleAgainSnapshot, localPlayerSlot: 1));

        var visibleScoreboardPlayer = Assert.Single(world.RemoteSnapshotScoreboardPlayers);
        Assert.Equal(remoteSpy.PlayerId, visibleScoreboardPlayer.Id);
        Assert.True(world.NetworkPlayers.TryGetPlayerNetworkSlot(visibleScoreboardPlayer, out var visibleSlot));
        Assert.Equal(remoteSpy.Slot, visibleSlot);
    }

    [Fact]
    public void ApplySnapshotUpdatesCapLimitFromServerRules()
    {
        var world = new SimulationWorld();
        var localPlayer = CreatePlayerState(1, 101, "Local", PlayerTeam.Red, PlayerClass.Scout, isAlive: true, gibDeaths: 0);
        var snapshot = CreateSnapshot(world, 127, localPlayer) with
        {
            CapLimit = 9,
        };

        Assert.Equal(5, world.MatchRules.CapLimit);
        Assert.True(world.SnapshotApply.ApplySnapshot(snapshot, localPlayerSlot: 1));

        Assert.Equal(9, world.MatchRules.CapLimit);
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

        Assert.True(world.SnapshotApply.ApplySnapshot(visibleSnapshot, localPlayerSlot: 1));

        Assert.True(world.SnapshotApply.ApplySnapshot(hiddenSnapshot, localPlayerSlot: 1));

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

        Assert.True(world.SnapshotApply.ApplySnapshot(visibleSnapshot, localPlayerSlot: 1));
        Assert.Single(world.RemoteSnapshotScoreboardPlayers);

        Assert.True(world.SnapshotApply.ApplySnapshot(removedSnapshot, localPlayerSlot: 1));

        Assert.Empty(world.RemoteSnapshotPlayers);
        Assert.Empty(world.RemoteSnapshotScoreboardPlayers);
    }





    internal static SnapshotMessage CreateSnapshot(
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

    internal static SnapshotPlayerState CreatePlayerState(
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
            IsUsingBinoculars: false,
            BinocularsFocusX: 0f,
            BinocularsFocusY: 0f,
            FacingDirectionX: 1f,
            AimDirectionDegrees: 0f,
            IsTaunting: false,
            IsChatBubbleVisible: false,
            ChatBubbleFrameIndex: 0,
            ChatBubbleAlpha: 0f,
            GibDeaths: gibDeaths);
    }

    private static void InvokeRegisterBloodEffect(SimulationWorld world, float x, float y, float directionDegrees, int count)
    {
        world.WorldEffects.RegisterBloodEffect(x, y, directionDegrees, count);
    }

    private static void InvokeKillPlayer(SimulationWorld world, PlayerEntity player, bool gibbed)
    {
        world.PlayerDeaths.KillPlayer(player,
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
            -1,
            false);
    }
}
