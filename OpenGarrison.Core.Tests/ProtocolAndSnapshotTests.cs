using OpenGarrison.Core;
using OpenGarrison.Protocol;
using Xunit;
using System.IO;
using System.Text;

namespace OpenGarrison.Core.Tests;

public sealed class ProtocolAndSnapshotTests
{
    [Fact]
    public void ProtocolCodec_RoundTripsConnectionDeniedMessage()
    {
        var message = new ConnectionDeniedMessage("Server is full.");

        var payload = ProtocolCodec.Serialize(message);
        var success = ProtocolCodec.TryDeserialize(payload, out var decoded);

        Assert.True(success);
        var decodedMessage = Assert.IsType<ConnectionDeniedMessage>(decoded);
        Assert.Equal(message.Reason, decodedMessage.Reason);
    }

    [Fact]
    public void ProtocolCodec_RoundTripsSessionSlotChangedMessage()
    {
        var message = new SessionSlotChangedMessage(SimulationWorld.FirstSpectatorSlot);

        var payload = ProtocolCodec.Serialize(message);
        var success = ProtocolCodec.TryDeserialize(payload, out var decoded);

        Assert.True(success);
        var decodedMessage = Assert.IsType<SessionSlotChangedMessage>(decoded);
        Assert.Equal(message.PlayerSlot, decodedMessage.PlayerSlot);
    }

    [Fact]
    public void ProtocolCodec_RoundTripsSnapshotAckMessage()
    {
        var message = new SnapshotAckMessage(42UL);

        var payload = ProtocolCodec.Serialize(message);
        var success = ProtocolCodec.TryDeserialize(payload, out var decoded);

        Assert.True(success);
        var decodedMessage = Assert.IsType<SnapshotAckMessage>(decoded);
        Assert.Equal(message.Frame, decodedMessage.Frame);
    }

    [Fact]
    public void ProtocolCodec_RoundTripsServerStatusResponseMessage()
    {
        var message = new ServerStatusResponseMessage("OpenGarrison CSharp UDP Server", "Truefort", (byte)GameModeKind.CaptureTheFlag, 2, 2, 1);

        var payload = ProtocolCodec.Serialize(message);
        var success = ProtocolCodec.TryDeserialize(payload, out var decoded);

        Assert.True(success);
        var decodedMessage = Assert.IsType<ServerStatusResponseMessage>(decoded);
        Assert.Equal(message.ServerName, decodedMessage.ServerName);
        Assert.Equal(message.LevelName, decodedMessage.LevelName);
        Assert.Equal(message.GameMode, decodedMessage.GameMode);
        Assert.Equal(message.PlayerCount, decodedMessage.PlayerCount);
        Assert.Equal(message.MaxPlayerCount, decodedMessage.MaxPlayerCount);
        Assert.Equal(message.SpectatorCount, decodedMessage.SpectatorCount);
    }

    [Fact]
    public void ProtocolCodec_RoundTripsSnapshotMessage()
    {
        var snapshot = CreateSnapshot();

        var payload = ProtocolCodec.Serialize(snapshot);
        var success = ProtocolCodec.TryDeserialize(payload, out var decoded);

        Assert.True(success);
        var decodedSnapshot = Assert.IsType<SnapshotMessage>(decoded);
        Assert.Equal(snapshot.Frame, decodedSnapshot.Frame);
        Assert.Equal(snapshot.LevelName, decodedSnapshot.LevelName);
        Assert.Equal(snapshot.MapAreaIndex, decodedSnapshot.MapAreaIndex);
        Assert.Equal(snapshot.MapAreaCount, decodedSnapshot.MapAreaCount);
        Assert.Equal(snapshot.LastProcessedInputSequence, decodedSnapshot.LastProcessedInputSequence);
        Assert.Equal(snapshot.SpectatorCount, decodedSnapshot.SpectatorCount);
        Assert.Equal(snapshot.Players.Count, decodedSnapshot.Players.Count);
        Assert.Equal(snapshot.VisualEvents.Count, decodedSnapshot.VisualEvents.Count);
        Assert.Equal(snapshot.SoundEvents.Count, decodedSnapshot.SoundEvents.Count);
        Assert.Equal(snapshot.KillFeed.Count, decodedSnapshot.KillFeed.Count);
        Assert.Equal(snapshot.Players[0].Name, decodedSnapshot.Players[0].Name);
        Assert.Equal(snapshot.Players[1].ClassId, decodedSnapshot.Players[1].ClassId);
        Assert.Equal(snapshot.VisualEvents[0].EffectName, decodedSnapshot.VisualEvents[0].EffectName);
        Assert.Equal(snapshot.VisualEvents[0].X, decodedSnapshot.VisualEvents[0].X);
        Assert.Equal(snapshot.VisualEvents[0].EventId, decodedSnapshot.VisualEvents[0].EventId);
        Assert.Equal(snapshot.SoundEvents[0].EventId, decodedSnapshot.SoundEvents[0].EventId);
        Assert.NotNull(decodedSnapshot.LocalDeathCam);
        Assert.Equal(snapshot.LocalDeathCam!.InitialTicks, decodedSnapshot.LocalDeathCam!.InitialTicks);
        Assert.Equal(snapshot.KillFeed[1].MessageText, decodedSnapshot.KillFeed[1].MessageText);
        Assert.True(decodedSnapshot.CombatTraces[0].IsSniperTracer);
        Assert.Equal(snapshot.ControlPoints.Count, decodedSnapshot.ControlPoints.Count);
        Assert.Equal(snapshot.Generators.Count, decodedSnapshot.Generators.Count);
    }

    [Fact]
    public void ProtocolCodec_RoundTripsDeltaSnapshotMessage()
    {
        var snapshot = CreateSnapshot() with
        {
            BaselineFrame = 41UL,
            IsDelta = true,
            Sentries =
            [
                new SnapshotSentryState(501, 1, (byte)PlayerTeam.Red, 300f, 250f, 60, false, 1f, 1f, 15f, 2, 1, 1, true, true, 2, 330f, 255f),
            ],
            RemovedShotIds = [601, 602],
        };

        var payload = ProtocolCodec.Serialize(snapshot);
        var success = ProtocolCodec.TryDeserialize(payload, out var decoded);

        Assert.True(success);
        var decodedSnapshot = Assert.IsType<SnapshotMessage>(decoded);
        Assert.True(decodedSnapshot.IsDelta);
        Assert.Equal(41UL, decodedSnapshot.BaselineFrame);
        Assert.Single(decodedSnapshot.Sentries);
        Assert.Equal(new[] { 601, 602 }, decodedSnapshot.RemovedShotIds);
    }

    [Fact]
    public void ProtocolCodec_RoundTripsSnapshotPlayerBurnState()
    {
        var snapshot = CreateSnapshot() with
        {
            Players =
            [
                CreateSnapshot().Players[0] with
                {
                    BurnIntensity = 4.5f,
                    BurnDurationSourceTicks = 72f,
                    BurnDecayDelaySourceTicksRemaining = 24f,
                    BurnIntensityDecayPerSourceTick = 0.05f,
                    BurnedByPlayerId = 2,
                    MovementState = (byte)LegacyMovementState.RocketJuggle,
                },
                CreateSnapshot().Players[1],
            ],
        };

        var payload = ProtocolCodec.Serialize(snapshot);
        var success = ProtocolCodec.TryDeserialize(payload, out var decoded);

        Assert.True(success);
        var decodedSnapshot = Assert.IsType<SnapshotMessage>(decoded);
        var player = decodedSnapshot.Players[0];
        Assert.Equal(4.5f, player.BurnIntensity);
        Assert.Equal(72f, player.BurnDurationSourceTicks);
        Assert.Equal(24f, player.BurnDecayDelaySourceTicksRemaining);
        Assert.Equal(0.05f, player.BurnIntensityDecayPerSourceTick);
        Assert.Equal(2, player.BurnedByPlayerId);
        Assert.Equal((byte)LegacyMovementState.RocketJuggle, player.MovementState);
    }

    [Fact]
    public void ProtocolCodec_RoundTripsSnapshotRocketDecayState()
    {
        var snapshot = CreateSnapshot() with
        {
            Rockets =
            [
                new SnapshotRocketState(604, (byte)PlayerTeam.Red, 1, 340f, 280f, 338f, 279f, 0.5f, 13f, 20, 4f, 11f, 1, 120f, 220f, 700f, true, 6f, new[] { 3, 5 }),
            ],
        };

        var payload = ProtocolCodec.Serialize(snapshot);
        var success = ProtocolCodec.TryDeserialize(payload, out var decoded);

        Assert.True(success);
        var decodedSnapshot = Assert.IsType<SnapshotMessage>(decoded);
        var rocket = Assert.Single(decodedSnapshot.Rockets);
        Assert.Equal(4f, rocket.ReducedKnockbackSourceTicksRemaining);
        Assert.Equal(11f, rocket.ZeroKnockbackSourceTicksRemaining);
        Assert.Equal(1, rocket.RangeAnchorOwnerId);
        Assert.Equal(120f, rocket.LastKnownRangeOriginX);
        Assert.Equal(220f, rocket.LastKnownRangeOriginY);
        Assert.Equal(700f, rocket.DistanceToTravel);
        Assert.True(rocket.IsFading);
        Assert.Equal(6f, rocket.FadeSourceTicksRemaining);
        Assert.Equal(new[] { 3, 5 }, rocket.PassedFriendlyPlayerIds);
    }

    [Fact]
    public void ProtocolCodec_TryDeserialize_RejectsOversizedHelloName()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        var oversizedName = new string('A', 81);
        var oversizedBytes = Encoding.UTF8.GetBytes(oversizedName);
        writer.Write((byte)MessageType.Hello);
        writer.Write((ushort)oversizedBytes.Length);
        writer.Write(oversizedBytes);
        writer.Write(ProtocolVersion.Current);
        writer.Flush();

        var success = ProtocolCodec.TryDeserialize(stream.ToArray(), out var decoded);

        Assert.False(success);
        Assert.Null(decoded);
    }

    [Fact]
    public void ProtocolCodec_Serialize_RejectsOversizedPassword()
    {
        var oversizedPassword = new string('p', 65);

        Assert.Throws<InvalidOperationException>(() => ProtocolCodec.Serialize(new PasswordSubmitMessage(oversizedPassword)));
    }

    [Fact]
    public void ProtocolCodec_Serialize_RejectsOversizedSnapshotPlayerName()
    {
        var snapshot = CreateSnapshot() with
        {
            Players =
            [
                CreateSnapshot().Players[0] with { Name = new string('N', 81) },
                CreateSnapshot().Players[1],
            ],
        };

        Assert.Throws<InvalidOperationException>(() => ProtocolCodec.Serialize(snapshot));
    }

    [Fact]
    public void ProtocolCodec_TryDeserialize_RejectsTrailingPayloadData()
    {
        var payload = ProtocolCodec.Serialize(new ConnectionDeniedMessage("Server is full."));
        var trailingPayload = new byte[payload.Length + 1];
        Array.Copy(payload, trailingPayload, payload.Length);
        trailingPayload[^1] = 0x7F;

        var success = ProtocolCodec.TryDeserialize(trailingPayload, out var decoded);

        Assert.False(success);
        Assert.Null(decoded);
    }

    [Fact]
    public void ProtocolCodec_TryDeserialize_RejectsInvalidUtf8()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write((byte)MessageType.ConnectionDenied);
        writer.Write((ushort)2);
        writer.Write(new byte[] { 0xC3, 0x28 });
        writer.Flush();

        var success = ProtocolCodec.TryDeserialize(stream.ToArray(), out var decoded);

        Assert.False(success);
        Assert.Null(decoded);
    }

    [Fact]
    public void ApplySnapshot_UpdatesWorldStateUsingLocalPlayerSlot()
    {
        var world = new SimulationWorld();
        var snapshot = CreateSnapshot() with
        {
            LevelName = world.Level.Name,
            MapAreaIndex = (byte)world.Level.MapAreaIndex,
            MapAreaCount = (byte)world.Level.MapAreaCount,
        };

        var applied = world.ApplySnapshot(snapshot, localPlayerSlot: 2);

        Assert.True(applied);
        Assert.Equal(snapshot.LevelName, world.Level.Name);
        Assert.Equal(GameModeKind.CaptureTheFlag, world.MatchRules.Mode);
        Assert.Equal(MatchPhase.Running, world.MatchState.Phase);
        Assert.Equal(PlayerTeam.Blue, world.LocalPlayer.Team);
        Assert.Equal(PlayerClass.Sniper, world.LocalPlayer.ClassId);
        Assert.Equal("RemoteBlue", world.LocalPlayer.DisplayName);
        Assert.True(world.LocalPlayer.IsSniperScoped);
        Assert.Equal(48, world.LocalPlayer.SniperChargeTicks);
        Assert.False(world.EnemyPlayerEnabled);
        Assert.Single(world.RemoteSnapshotPlayers);
        Assert.Equal(PlayerTeam.Red, world.RemoteSnapshotPlayers[0].Team);
        Assert.Equal(PlayerClass.Soldier, world.RemoteSnapshotPlayers[0].ClassId);
        Assert.Equal("RemoteRed", world.RemoteSnapshotPlayers[0].DisplayName);
        Assert.Equal(2, world.RedCaps);
        Assert.Equal(1, world.BlueCaps);
        Assert.Single(world.CombatTraces);
        Assert.True(world.CombatTraces[0].IsSniperTracer);
        Assert.Single(world.PendingSoundEvents);
        Assert.Equal(701UL, world.PendingSoundEvents[0].EventId);
        Assert.Equal(2, world.KillFeed.Count);
        Assert.Equal("RemoteBlue bid farewell, cruel world!", world.KillFeed[1].MessageText);
    }

    [Fact]
    public void ApplySnapshot_RestoresLocalPlayerAfterburnState()
    {
        var world = new SimulationWorld();
        var snapshot = CreateSnapshot() with
        {
            LevelName = world.Level.Name,
            MapAreaIndex = (byte)world.Level.MapAreaIndex,
            MapAreaCount = (byte)world.Level.MapAreaCount,
            Players =
            [
                CreateSnapshot().Players[0] with
                {
                    BurnIntensity = 3.5f,
                    BurnDurationSourceTicks = 40f,
                    BurnDecayDelaySourceTicksRemaining = 15f,
                    BurnIntensityDecayPerSourceTick = 0.02f,
                    BurnedByPlayerId = 2,
                    MovementState = (byte)LegacyMovementState.ExplosionRecovery,
                },
                CreateSnapshot().Players[1],
            ],
        };

        var applied = world.ApplySnapshot(snapshot, localPlayerSlot: 1);

        Assert.True(applied);
        Assert.Equal(3.5f, world.LocalPlayer.BurnIntensity);
        Assert.Equal(40f, world.LocalPlayer.BurnDurationSourceTicks);
        Assert.Equal(15f, world.LocalPlayer.BurnDecayDelaySourceTicksRemaining);
        Assert.Equal(0.02f, world.LocalPlayer.BurnIntensityDecayPerSourceTick);
        Assert.Equal(2, world.LocalPlayer.BurnedByPlayerId);
        Assert.Equal(LegacyMovementState.ExplosionRecovery, world.LocalPlayer.MovementState);
    }

    [Fact]
    public void ApplySnapshot_RetainsAdditionalRemotePlayers()
    {
        var world = new SimulationWorld();
        var snapshot = CreateSnapshot() with
        {
            Players =
            [
                CreateSnapshot().Players[0],
                CreateSnapshot().Players[1],
                new SnapshotPlayerState(
                    Slot: 4,
                    PlayerId: 44,
                    Name: "RemoteExtra",
                    Team: (byte)PlayerTeam.Red,
                    ClassId: (byte)PlayerClass.Sniper,
                    IsAlive: true,
                    IsAwaitingJoin: false,
                    IsSpectator: false,
                    RespawnTicks: 0,
                    X: 520f,
                    Y: 240f,
                    HorizontalSpeed: 0f,
                    VerticalSpeed: 0f,
                    Health: 125,
                    MaxHealth: 125,
                    Ammo: 25,
                    MaxAmmo: 25,
                    Kills: 7,
                    Deaths: 1,
                    Caps: 0,
                    HealPoints: 0,
                    ActiveDominationCount: 0,
                    IsDominatingLocalViewer: false,
                    IsDominatedByLocalViewer: false,
                    Metal: 100f,
                    IsGrounded: true,
                    RemainingAirJumps: 0,
                    IsCarryingIntel: false,
                    IsSpyCloaked: false,
                    SpyCloakAlpha: 1f,
                    IsUbered: false,
                    IsHeavyEating: false,
                    HeavyEatTicksRemaining: 0,
                    IsSniperScoped: false,
                    SniperChargeTicks: 0,
                    FacingDirectionX: -1f,
                    AimDirectionDegrees: 180f,
                    IsTaunting: false,
                    TauntFrameIndex: 0f,
                    IsChatBubbleVisible: false,
                    ChatBubbleFrameIndex: 0,
                    ChatBubbleAlpha: 0f),
            ],
        };

        var applied = world.ApplySnapshot(snapshot, localPlayerSlot: 1);

        Assert.True(applied);
        Assert.Equal(2, world.RemoteSnapshotPlayers.Count);
        Assert.Equal(2, world.RemoteSnapshotPlayers[0].Id);
        Assert.Equal("RemoteBlue", world.RemoteSnapshotPlayers[0].DisplayName);
        Assert.Equal(44, world.RemoteSnapshotPlayers[1].Id);
        Assert.Equal("RemoteExtra", world.RemoteSnapshotPlayers[1].DisplayName);
        Assert.Equal(PlayerClass.Sniper, world.RemoteSnapshotPlayers[1].ClassId);
    }

    [Fact]
    public void ApplySnapshot_DisablesFriendlyDummy()
    {
        var world = new SimulationWorld();
        world.SpawnFriendlyDummy();
        Assert.True(world.FriendlyDummyEnabled);

        var snapshot = CreateSnapshot() with
        {
            Players =
            [
                CreateSnapshot().Players[0],
                CreateSnapshot().Players[1],
                new SnapshotPlayerState(
                    Slot: 3,
                    PlayerId: 33,
                    Name: "RemoteSlotThree",
                    Team: (byte)PlayerTeam.Red,
                    ClassId: (byte)PlayerClass.Scout,
                    IsAlive: true,
                    IsAwaitingJoin: false,
                    IsSpectator: false,
                    RespawnTicks: 0,
                    X: 520f,
                    Y: 240f,
                    HorizontalSpeed: 0f,
                    VerticalSpeed: 0f,
                    Health: 125,
                    MaxHealth: 125,
                    Ammo: 6,
                    MaxAmmo: 6,
                    Kills: 2,
                    Deaths: 1,
                    Caps: 0,
                    HealPoints: 0,
                    ActiveDominationCount: 0,
                    IsDominatingLocalViewer: false,
                    IsDominatedByLocalViewer: false,
                    Metal: 100f,
                    IsGrounded: true,
                    RemainingAirJumps: 0,
                    IsCarryingIntel: false,
                    IsSpyCloaked: false,
                    SpyCloakAlpha: 1f,
                    IsUbered: false,
                    IsHeavyEating: false,
                    HeavyEatTicksRemaining: 0,
                    IsSniperScoped: false,
                    SniperChargeTicks: 0,
                    FacingDirectionX: -1f,
                    AimDirectionDegrees: 180f,
                    IsTaunting: false,
                    TauntFrameIndex: 0f,
                    IsChatBubbleVisible: false,
                    ChatBubbleFrameIndex: 0,
                    ChatBubbleAlpha: 0f),
            ],
        };

        var applied = world.ApplySnapshot(snapshot, localPlayerSlot: 1);

        Assert.True(applied);
        Assert.False(world.FriendlyDummyEnabled);
        Assert.False(world.FriendlyDummy.IsAlive);
    }

    [Fact]
    public void ApplySnapshot_AllowsSpectatorSlotsWithoutOwnedPlayer()
    {
        var world = new SimulationWorld();
        var snapshot = CreateSnapshot() with
        {
            SpectatorCount = 3,
        };

        var applied = world.ApplySnapshot(snapshot, localPlayerSlot: SimulationWorld.FirstSpectatorSlot);

        Assert.True(applied);
        Assert.True(world.LocalPlayerAwaitingJoin);
        Assert.False(world.LocalPlayer.IsAlive);
        Assert.Equal(3, world.SpectatorCount);
        Assert.False(world.EnemyPlayerEnabled);
        Assert.Equal(2, world.RemoteSnapshotPlayers.Count);
        Assert.Equal("RemoteRed", world.RemoteSnapshotPlayers[0].DisplayName);
        Assert.Equal("RemoteBlue", world.RemoteSnapshotPlayers[1].DisplayName);
    }

    [Fact]
    public void ApplySnapshot_RemovesStaleRemotePlayersBetweenSnapshots()
    {
        var world = new SimulationWorld();

        Assert.True(world.ApplySnapshot(CreateSnapshot(), localPlayerSlot: 1));
        Assert.Single(world.RemoteSnapshotPlayers);

        var reducedSnapshot = CreateSnapshot() with
        {
            Players =
            [
                CreateSnapshot().Players[0],
            ],
        };

        Assert.True(world.ApplySnapshot(reducedSnapshot, localPlayerSlot: 1));
        Assert.Empty(world.RemoteSnapshotPlayers);
    }

    [Fact]
    public void ApplySnapshot_PopulatesTransientEntityCollections()
    {
        var world = new SimulationWorld();
        var snapshot = CreateSnapshot() with
        {
            LevelName = world.Level.Name,
            MapAreaIndex = (byte)world.Level.MapAreaIndex,
            MapAreaCount = (byte)world.Level.MapAreaCount,
            Sentries =
            [
                new SnapshotSentryState(501, 1, (byte)PlayerTeam.Red, 300f, 250f, 60, false, 1f, 1f, 15f, 2, 1, 1, true, true, 2, 330f, 255f),
            ],
            Shots =
            [
                new SnapshotShotState(601, (byte)PlayerTeam.Red, 1, 310f, 255f, 4f, 0f, 10),
            ],
            Needles =
            [
                new SnapshotShotState(602, (byte)PlayerTeam.Blue, 2, 320f, 265f, -3f, 1f, 8),
            ],
            RevolverShots =
            [
                new SnapshotShotState(603, (byte)PlayerTeam.Blue, 2, 330f, 275f, -5f, 0f, 6),
            ],
            Rockets =
            [
                new SnapshotRocketState(604, (byte)PlayerTeam.Red, 1, 340f, 280f, 338f, 279f, 0.5f, 13f, 20),
            ],
            Flames =
            [
                new SnapshotFlameState(605, (byte)PlayerTeam.Blue, 2, 350f, 285f, 349f, 284f, 1.5f, -0.5f, 5, -1, 0f, 0f),
            ],
            Mines =
            [
                new SnapshotMineState(606, (byte)PlayerTeam.Red, 1, 360f, 290f, 0f, 0f, true, false, 75f),
            ],
            PlayerGibs =
            [
                new SnapshotPlayerGibState(607, "ScoutGibS", 0, 370f, 295f, 1f, 2f, 30f, 4f, 12, 0.2f),
            ],
            BloodDrops =
            [
                new SnapshotBloodDropState(608, 380f, 300f, 0.5f, 1.5f, false, 11, 1.6f),
            ],
            DeadBodies =
            [
                new SnapshotDeadBodyState(609, (byte)PlayerTeam.Red, (byte)PlayerClass.Soldier, 390f, 305f, 24f, 36f, 0f, 1f, false, 25),
            ],
        };

        var applied = world.ApplySnapshot(snapshot, localPlayerSlot: 1);

        Assert.True(applied);
        Assert.Single(world.Sentries);
        Assert.Single(world.Shots);
        Assert.Single(world.Needles);
        Assert.Single(world.RevolverShots);
        Assert.Single(world.Rockets);
        Assert.Single(world.Flames);
        Assert.Single(world.Mines);
        Assert.Single(world.PlayerGibs);
        Assert.Single(world.BloodDrops);
        Assert.Single(world.DeadBodies);
        Assert.Equal(1.6f, world.BloodDrops[0].Scale);
    }

    [Fact]
    public void ApplySnapshot_ReusesTransientEntitiesWhenIdentityIsStable()
    {
        var world = new SimulationWorld();
        var initialSnapshot = CreateSnapshot() with
        {
            LevelName = world.Level.Name,
            MapAreaIndex = (byte)world.Level.MapAreaIndex,
            MapAreaCount = (byte)world.Level.MapAreaCount,
            Shots =
            [
                new SnapshotShotState(601, (byte)PlayerTeam.Red, 1, 310f, 255f, 4f, 0f, 10),
            ],
            Rockets =
            [
                new SnapshotRocketState(604, (byte)PlayerTeam.Red, 1, 340f, 280f, 338f, 279f, 0.5f, 13f, 20),
            ],
            DeadBodies =
            [
                new SnapshotDeadBodyState(609, (byte)PlayerTeam.Red, (byte)PlayerClass.Soldier, 390f, 305f, 24f, 36f, 0f, 1f, false, 25),
            ],
        };

        Assert.True(world.ApplySnapshot(initialSnapshot, localPlayerSlot: 1));
        var initialShot = Assert.Single(world.Shots);
        var initialRocket = Assert.Single(world.Rockets);
        var initialDeadBody = Assert.Single(world.DeadBodies);

        var updatedSnapshot = initialSnapshot with
        {
            Frame = initialSnapshot.Frame + 1UL,
            Shots =
            [
                new SnapshotShotState(601, (byte)PlayerTeam.Red, 1, 324f, 257f, 5f, 0.5f, 8),
            ],
            Rockets =
            [
                new SnapshotRocketState(604, (byte)PlayerTeam.Red, 1, 356f, 288f, 352f, 286f, 0.65f, 11f, 17),
            ],
            DeadBodies =
            [
                new SnapshotDeadBodyState(609, (byte)PlayerTeam.Red, (byte)PlayerClass.Soldier, 402f, 311f, 24f, 36f, 1.5f, -0.5f, false, 22),
            ],
        };

        Assert.True(world.ApplySnapshot(updatedSnapshot, localPlayerSlot: 1));
        var updatedShot = Assert.Single(world.Shots);
        var updatedRocket = Assert.Single(world.Rockets);
        var updatedDeadBody = Assert.Single(world.DeadBodies);

        Assert.Same(initialShot, updatedShot);
        Assert.Equal(324f, updatedShot.X);
        Assert.Equal(257f, updatedShot.Y);
        Assert.Equal(8, updatedShot.TicksRemaining);

        Assert.Same(initialRocket, updatedRocket);
        Assert.Equal(356f, updatedRocket.X);
        Assert.Equal(288f, updatedRocket.Y);
        Assert.Equal(17, updatedRocket.TicksRemaining);

        Assert.Same(initialDeadBody, updatedDeadBody);
        Assert.Equal(402f, updatedDeadBody.X);
        Assert.Equal(311f, updatedDeadBody.Y);
        Assert.Equal(22, updatedDeadBody.TicksRemaining);
    }

    [Fact]
    public void ApplySnapshot_RocketsRestoreKnockbackDecayState()
    {
        var world = new SimulationWorld();
        var snapshot = CreateSnapshot() with
        {
            LevelName = world.Level.Name,
            MapAreaIndex = (byte)world.Level.MapAreaIndex,
            MapAreaCount = (byte)world.Level.MapAreaCount,
            Rockets =
            [
                new SnapshotRocketState(604, (byte)PlayerTeam.Red, 1, 340f, 280f, 338f, 279f, 0.5f, 13f, 20, 1f, 2f),
            ],
        };

        Assert.True(world.ApplySnapshot(snapshot, localPlayerSlot: 1));
        var rocket = Assert.Single(world.Rockets);
        Assert.Equal(RocketProjectileEntity.InitialKnockback, rocket.CurrentKnockback);

        world.AdvanceOneTick();
        Assert.Equal(RocketProjectileEntity.ReducedKnockback, rocket.CurrentKnockback);

        world.AdvanceOneTick();
        Assert.Equal(0f, rocket.CurrentKnockback);
    }

    [Fact]
    public void ApplySnapshot_RocketsRestoreTravelFadeState()
    {
        var world = new SimulationWorld();
        var snapshot = CreateSnapshot() with
        {
            LevelName = world.Level.Name,
            MapAreaIndex = (byte)world.Level.MapAreaIndex,
            MapAreaCount = (byte)world.Level.MapAreaCount,
            Rockets =
            [
                new SnapshotRocketState(604, (byte)PlayerTeam.Red, 1, 340f, 280f, 340f, 280f, 0f, 0f, 20, 4f, 11f, 1, 150f, 260f, 700f, true, 2f, new[] { 44 }),
            ],
        };

        Assert.True(world.ApplySnapshot(snapshot, localPlayerSlot: 1));
        var rocket = Assert.Single(world.Rockets);
        Assert.True(rocket.IsFading);
        Assert.Equal(700f, rocket.DistanceToTravel);
        Assert.Equal(new[] { 44 }, rocket.PassedFriendlyPlayerIds);

        world.AdvanceOneTick();
        Assert.Single(world.Rockets);

        world.AdvanceOneTick();
        Assert.Empty(world.Rockets);
    }

    [Fact]
    public void ApplySnapshot_RecreatesTransientEntitiesWhenIdentityChangesForExistingId()
    {
        var world = new SimulationWorld();
        var initialSnapshot = CreateSnapshot() with
        {
            LevelName = world.Level.Name,
            MapAreaIndex = (byte)world.Level.MapAreaIndex,
            MapAreaCount = (byte)world.Level.MapAreaCount,
            Rockets =
            [
                new SnapshotRocketState(604, (byte)PlayerTeam.Red, 1, 340f, 280f, 338f, 279f, 0.5f, 13f, 20),
            ],
            DeadBodies =
            [
                new SnapshotDeadBodyState(609, (byte)PlayerTeam.Red, (byte)PlayerClass.Soldier, 390f, 305f, 24f, 36f, 0f, 1f, false, 25),
            ],
        };

        Assert.True(world.ApplySnapshot(initialSnapshot, localPlayerSlot: 1));
        var initialRocket = Assert.Single(world.Rockets);
        var initialDeadBody = Assert.Single(world.DeadBodies);

        var changedIdentitySnapshot = initialSnapshot with
        {
            Frame = initialSnapshot.Frame + 1UL,
            Rockets =
            [
                new SnapshotRocketState(604, (byte)PlayerTeam.Blue, 2, 352f, 288f, 348f, 286f, 1.1f, 9f, 16),
            ],
            DeadBodies =
            [
                new SnapshotDeadBodyState(609, (byte)PlayerTeam.Blue, (byte)PlayerClass.Sniper, 404f, 314f, 20f, 32f, 0f, 0f, true, 18),
            ],
        };

        Assert.True(world.ApplySnapshot(changedIdentitySnapshot, localPlayerSlot: 1));

        Assert.NotSame(initialRocket, Assert.Single(world.Rockets));
        Assert.NotSame(initialDeadBody, Assert.Single(world.DeadBodies));
    }

    [Fact]
    public void ApplySnapshot_UpdatesGeneratorState()
    {
        var world = new SimulationWorld();
        var snapshot = CreateSnapshot() with
        {
            LevelName = "Destroy",
            GameMode = (byte)GameModeKind.Generator,
            Generators =
            [
                new SnapshotGeneratorState((byte)PlayerTeam.Red, 3200, 4000),
                new SnapshotGeneratorState((byte)PlayerTeam.Blue, 900, 4000),
            ],
        };

        var applied = world.ApplySnapshot(snapshot, localPlayerSlot: 1);

        Assert.True(applied);
        Assert.Equal(GameModeKind.Generator, world.MatchRules.Mode);
        Assert.Equal(2, world.Generators.Count);
        Assert.Equal(3200, world.GetGenerator(PlayerTeam.Red)!.Health);
        Assert.Equal(900, world.GetGenerator(PlayerTeam.Blue)!.Health);
    }

    [Fact]
    public void SnapshotDelta_ToFullSnapshot_MergesTransientEntitiesAgainstBaseline()
    {
        var baseline = CreateSnapshot() with
        {
            Frame = 10UL,
            Sentries =
            [
                new SnapshotSentryState(501, 1, (byte)PlayerTeam.Red, 300f, 250f, 60, false, 1f, 1f, 15f, 2, 1, 1, true, true, 2, 330f, 255f),
            ],
            Shots =
            [
                new SnapshotShotState(601, (byte)PlayerTeam.Red, 1, 310f, 255f, 4f, 0f, 10),
            ],
            BloodDrops =
            [
                new SnapshotBloodDropState(701, 380f, 300f, 0.5f, 1.5f, false, 11, 1.6f),
            ],
        };
        var delta = CreateSnapshot() with
        {
            Frame = 11UL,
            BaselineFrame = 10UL,
            IsDelta = true,
            Sentries =
            [
                new SnapshotSentryState(501, 1, (byte)PlayerTeam.Red, 304f, 252f, 55, true, 1f, 1f, 18f, 1, 0, 0, true, false, -1, 0f, 0f),
                new SnapshotSentryState(502, 1, (byte)PlayerTeam.Red, 350f, 265f, 80, true, 1f, 1f, 0f, 0, 0, 0, true, false, -1, 0f, 0f),
            ],
            Shots = [],
            RemovedShotIds = [601],
            BloodDrops = [],
            RemovedBloodDropIds = [701],
            DeadBodies =
            [
                new SnapshotDeadBodyState(801, (byte)PlayerTeam.Blue, (byte)PlayerClass.Scout, 390f, 305f, 24f, 36f, 0f, 1f, false, 25),
            ],
        };

        var merged = SnapshotDelta.ToFullSnapshot(delta, baseline);

        Assert.False(merged.IsDelta);
        Assert.Equal(0UL, merged.BaselineFrame);
        Assert.Equal(2, merged.Sentries.Count);
        Assert.Empty(merged.Shots);
        Assert.Empty(merged.BloodDrops);
        Assert.Single(merged.DeadBodies);
        Assert.Equal(502, merged.Sentries[1].Id);
        Assert.Equal(801, merged.DeadBodies[0].Id);
    }

    private static SnapshotMessage CreateSnapshot()
    {
        return new SnapshotMessage(
            Frame: 42UL,
            TickRate: 30,
            LevelName: "Waterway",
            MapAreaIndex: 1,
            MapAreaCount: 1,
            GameMode: (byte)GameModeKind.CaptureTheFlag,
            MatchPhase: (byte)MatchPhase.Running,
            WinnerTeam: 0,
            TimeRemainingTicks: 1200,
            RedCaps: 2,
            BlueCaps: 1,
            SpectatorCount: 0,
            LastProcessedInputSequence: 99,
            RedIntel: new SnapshotIntelState((byte)PlayerTeam.Red, 100f, 200f, true, false, 0),
            BlueIntel: new SnapshotIntelState((byte)PlayerTeam.Blue, 700f, 220f, false, true, 80),
            Players:
            [
                new SnapshotPlayerState(
                    Slot: 1,
                    PlayerId: 1,
                    Name: "RemoteRed",
                    Team: (byte)PlayerTeam.Red,
                    ClassId: (byte)PlayerClass.Soldier,
                    IsAlive: true,
                    IsAwaitingJoin: false,
                    IsSpectator: false,
                    RespawnTicks: 0,
                    X: 150f,
                    Y: 260f,
                    HorizontalSpeed: 1f,
                    VerticalSpeed: 2f,
                    Health: 175,
                    MaxHealth: 175,
                    Ammo: 3,
                    MaxAmmo: 4,
                    Kills: 4,
                    Deaths: 2,
                    Caps: 1,
                    HealPoints: 0,
                    ActiveDominationCount: 0,
                    IsDominatingLocalViewer: false,
                    IsDominatedByLocalViewer: false,
                    Metal: 100f,
                    IsGrounded: true,
                    RemainingAirJumps: 0,
                    IsCarryingIntel: false,
                    IsSpyCloaked: false,
                    SpyCloakAlpha: 1f,
                    IsUbered: false,
                    IsHeavyEating: false,
                    HeavyEatTicksRemaining: 0,
                    IsSniperScoped: false,
                    SniperChargeTicks: 0,
                    FacingDirectionX: 1f,
                    AimDirectionDegrees: 20f,
                    IsTaunting: false,
                    TauntFrameIndex: 0f,
                    IsChatBubbleVisible: false,
                    ChatBubbleFrameIndex: 0,
                    ChatBubbleAlpha: 0f),
                new SnapshotPlayerState(
                    Slot: 2,
                    PlayerId: 2,
                    Name: "RemoteBlue",
                    Team: (byte)PlayerTeam.Blue,
                    ClassId: (byte)PlayerClass.Sniper,
                    IsAlive: true,
                    IsAwaitingJoin: false,
                    IsSpectator: false,
                    RespawnTicks: 15,
                    X: 450f,
                    Y: 280f,
                    HorizontalSpeed: -1f,
                    VerticalSpeed: 0f,
                    Health: 100,
                    MaxHealth: 120,
                    Ammo: 24,
                    MaxAmmo: 25,
                    Kills: 1,
                    Deaths: 3,
                    Caps: 0,
                    HealPoints: 75,
                    ActiveDominationCount: 0,
                    IsDominatingLocalViewer: false,
                    IsDominatedByLocalViewer: false,
                    Metal: 100f,
                    IsGrounded: false,
                    RemainingAirJumps: 0,
                    IsCarryingIntel: true,
                    IsSpyCloaked: false,
                    SpyCloakAlpha: 1f,
                    IsUbered: true,
                    IsHeavyEating: false,
                    HeavyEatTicksRemaining: 0,
                    IsSniperScoped: true,
                    SniperChargeTicks: 48,
                    FacingDirectionX: -1f,
                    AimDirectionDegrees: 180f,
                    IsTaunting: false,
                    TauntFrameIndex: 0f,
                    IsChatBubbleVisible: true,
                    ChatBubbleFrameIndex: 3,
                    ChatBubbleAlpha: 1f),
            ],
            CombatTraces:
            [
                new SnapshotCombatTraceState(150f, 260f, 450f, 280f, 2, true, (byte)PlayerTeam.Red, true),
            ],
            Sentries: [],
            Shots: [],
            Bubbles: [],
            Blades: [],
            Needles: [],
            RevolverShots: [],
            Rockets: [],
            Flames: [],
            Mines: [],
            PlayerGibs: [],
            BloodDrops: [],
            DeadBodies: [],
            ControlPointSetupTicksRemaining: 0,
            ControlPoints:
            [
                new SnapshotControlPointState(1, (byte)PlayerTeam.Red, 0, 0, 180, 0, false),
            ],
            Generators:
            [
                new SnapshotGeneratorState((byte)PlayerTeam.Red, 4000, 4000),
                new SnapshotGeneratorState((byte)PlayerTeam.Blue, 2800, 4000),
            ],
            LocalDeathCam: new SnapshotDeathCamState(
                FocusX: 450f,
                FocusY: 280f,
                KillMessage: "You were killed by",
                KillerName: "RemoteBlue",
                KillerTeam: (byte)PlayerTeam.Blue,
                Health: 75,
                MaxHealth: 120,
                RemainingTicks: 90,
                InitialTicks: 150),
            KillFeed:
            [
                new SnapshotKillFeedEntry(
                    KillerName: "RemoteRed",
                    KillerTeam: (byte)PlayerTeam.Red,
                    WeaponSpriteName: "RocketlauncherS",
                    VictimName: "RemoteBlue",
                    VictimTeam: (byte)PlayerTeam.Blue),
                new SnapshotKillFeedEntry(
                    KillerName: string.Empty,
                    KillerTeam: (byte)PlayerTeam.Blue,
                    WeaponSpriteName: "DeadS",
                    VictimName: "RemoteBlue",
                    VictimTeam: (byte)PlayerTeam.Blue,
                    MessageText: "RemoteBlue bid farewell, cruel world!"),
            ],
            VisualEvents:
            [
                new SnapshotVisualEvent("Explosion", 460f, 300f, 0f, 1, 700UL),
            ],
            SoundEvents:
            [
                new SnapshotSoundEvent("IntelGetSnd", 450f, 280f, 701UL, 699UL),
            ]);
    }
}
