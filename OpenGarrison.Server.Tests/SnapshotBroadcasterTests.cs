using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using Xunit;

namespace OpenGarrison.Server.Tests;

public sealed class SnapshotBroadcasterTests
{
    [Fact]
    public void BroadcastSnapshot_AfterCatchUpStep_SendsEachAdvancedFrame()
    {
        var world = new SimulationWorld();
        var config = world.Config;
        var clientsBySlot = new Dictionary<byte, ClientSession>
        {
            [SimulationWorld.LocalPlayerSlot] = new(
                SimulationWorld.LocalPlayerSlot,
                new IPEndPoint(IPAddress.Loopback, 8190),
                "Player",
                TimeSpan.Zero),
        };
        var sentSnapshots = new List<SnapshotMessage>();
        var broadcaster = new SnapshotBroadcaster(
            world,
            config,
            clientsBySlot,
            transientEventReplayTicks: 4,
            (_, snapshot, _) =>
            {
                sentSnapshots.Add(snapshot);
            });
        var simulator = new FixedStepSimulator(world);

        var ticks = simulator.Step(config.FixedDeltaSeconds * 2.1d, broadcaster.BroadcastSnapshot);

        Assert.Equal(2, ticks);
        Assert.Equal(new ulong[] { 1, 2 }, sentSnapshots.Select(snapshot => snapshot.Frame).ToArray());
    }

    [Fact]
    public void BroadcastSnapshot_WithAcknowledgedBaseline_TrimsProjectileSpikeToBudget()
    {
        var world = new SimulationWorld();
        var config = world.Config;
        var client = new ClientSession(
            SimulationWorld.LocalPlayerSlot,
            new IPEndPoint(IPAddress.Loopback, 8190),
            "Player",
            TimeSpan.Zero);
        var clientsBySlot = new Dictionary<byte, ClientSession>
        {
            [SimulationWorld.LocalPlayerSlot] = client,
        };
        var sentSnapshots = new List<SnapshotMessage>();
        var broadcaster = new SnapshotBroadcaster(
            world,
            config,
            clientsBySlot,
            transientEventReplayTicks: 4,
            (_, snapshot, _) =>
            {
                sentSnapshots.Add(snapshot);
            });
        var simulator = new FixedStepSimulator(world);

        Assert.True(world.ApplySnapshot(CreateSnapshot(world, shots:
        [
            new SnapshotShotState(601, (byte)PlayerTeam.Red, 1, 310f, 255f, 4f, 0f, 10),
        ]), localPlayerSlot: SimulationWorld.LocalPlayerSlot));
        simulator.Step(config.FixedDeltaSeconds * 1.1d, broadcaster.BroadcastSnapshot);

        var baselineSnapshot = Assert.Single(sentSnapshots);
        client.AcknowledgeSnapshot(baselineSnapshot.Frame);

        var crowdedShots = Enumerable.Range(0, 160)
            .Select(index => new SnapshotShotState(
                700 + index,
                (byte)(index % 2 == 0 ? PlayerTeam.Red : PlayerTeam.Blue),
                1,
                180f + (index * 4f),
                240f + ((index % 6) * 8f),
                3f + (index % 5),
                (index % 3) - 1f,
                18))
            .ToArray();
        Assert.True(world.ApplySnapshot(CreateSnapshot(world, shots: crowdedShots), localPlayerSlot: SimulationWorld.LocalPlayerSlot));
        simulator.Step(config.FixedDeltaSeconds * 1.1d, broadcaster.BroadcastSnapshot);

        Assert.Collection(
            sentSnapshots,
            snapshot => Assert.Equal(baselineSnapshot.Frame, snapshot.Frame),
            snapshot => Assert.Equal(baselineSnapshot.Frame + 1UL, snapshot.Frame));
        var deltaSnapshot = sentSnapshots[1];
        Assert.True(deltaSnapshot.IsDelta);
        Assert.Equal(baselineSnapshot.Frame, deltaSnapshot.BaselineFrame);
        Assert.True(ProtocolCodec.Serialize(deltaSnapshot).Length <= 1200);
        Assert.NotEmpty(deltaSnapshot.Shots);
        Assert.True(deltaSnapshot.Shots.Count < crowdedShots.Length);
    }

    [Fact]
    public void BroadcastSnapshot_WithAcknowledgedBaseline_ReaddsSoundEventsAfterBudgetTrim()
    {
        var world = new SimulationWorld();
        var config = world.Config;
        var client = new ClientSession(
            SimulationWorld.LocalPlayerSlot,
            new IPEndPoint(IPAddress.Loopback, 8190),
            "Player",
            TimeSpan.Zero);
        var clientsBySlot = new Dictionary<byte, ClientSession>
        {
            [SimulationWorld.LocalPlayerSlot] = client,
        };
        var sentSnapshots = new List<SnapshotMessage>();
        var broadcaster = new SnapshotBroadcaster(
            world,
            config,
            clientsBySlot,
            transientEventReplayTicks: 4,
            (_, snapshot, _) => sentSnapshots.Add(snapshot));
        var simulator = new FixedStepSimulator(world);

        Assert.True(world.ApplySnapshot(CreateSnapshot(world), localPlayerSlot: SimulationWorld.LocalPlayerSlot));
        simulator.Step(config.FixedDeltaSeconds * 1.1d, broadcaster.BroadcastSnapshot);

        var baselineSnapshot = Assert.Single(sentSnapshots);
        client.AcknowledgeSnapshot(baselineSnapshot.Frame);

        var crowdedShots = Enumerable.Range(0, 160)
            .Select(index => new SnapshotShotState(
                900 + index,
                (byte)(index % 2 == 0 ? PlayerTeam.Red : PlayerTeam.Blue),
                1,
                180f + (index * 4f),
                240f + ((index % 6) * 8f),
                3f + (index % 5),
                (index % 3) - 1f,
                18))
            .ToArray();
        var soundEvents = new[]
        {
            new SnapshotSoundEvent("ChaingunSnd", 280f, 220f, 41),
            new SnapshotSoundEvent("FlamethrowerSnd", 296f, 224f, 42),
        };

        Assert.True(world.ApplySnapshot(
            CreateSnapshot(world, shots: crowdedShots, soundEvents: soundEvents),
            localPlayerSlot: SimulationWorld.LocalPlayerSlot));
        simulator.Step(config.FixedDeltaSeconds * 1.1d, broadcaster.BroadcastSnapshot);

        var deltaSnapshot = sentSnapshots[^1];
        Assert.True(deltaSnapshot.IsDelta);
        Assert.Equal(baselineSnapshot.Frame, deltaSnapshot.BaselineFrame);
        Assert.True(ProtocolCodec.Serialize(deltaSnapshot).Length <= 1200);
        Assert.NotEmpty(deltaSnapshot.SoundEvents);
        Assert.Contains(deltaSnapshot.SoundEvents, soundEvent => soundEvent.SoundName == "ChaingunSnd");
    }

    [Fact]
    public void BroadcastSnapshot_WithSmallScene_SendsFullSnapshot()
    {
        var world = new SimulationWorld();
        var config = world.Config;
        var clientsBySlot = new Dictionary<byte, ClientSession>
        {
            [SimulationWorld.LocalPlayerSlot] = new(
                SimulationWorld.LocalPlayerSlot,
                new IPEndPoint(IPAddress.Loopback, 8190),
                "Player",
                TimeSpan.Zero),
        };
        var sentSnapshots = new List<SnapshotMessage>();
        var broadcaster = new SnapshotBroadcaster(
            world,
            config,
            clientsBySlot,
            transientEventReplayTicks: 4,
            (_, snapshot, _) =>
            {
                sentSnapshots.Add(snapshot);
            });
        var simulator = new FixedStepSimulator(world);

        simulator.Step(config.FixedDeltaSeconds * 1.1d, broadcaster.BroadcastSnapshot);

        var snapshot = Assert.Single(sentSnapshots);
        Assert.False(snapshot.IsDelta);
        Assert.Equal(0UL, snapshot.BaselineFrame);
        Assert.True(ProtocolCodec.Serialize(snapshot).Length <= 1200);
    }

    [Fact]
    public void BroadcastSnapshot_WithSentryGibs_IncludesSentryGibState()
    {
        var world = new SimulationWorld();
        var config = world.Config;
        var clientsBySlot = new Dictionary<byte, ClientSession>
        {
            [SimulationWorld.LocalPlayerSlot] = new(
                SimulationWorld.LocalPlayerSlot,
                new IPEndPoint(IPAddress.Loopback, 8190),
                "Player",
                TimeSpan.Zero),
        };
        var sentSnapshots = new List<SnapshotMessage>();
        var broadcaster = new SnapshotBroadcaster(
            world,
            config,
            clientsBySlot,
            transientEventReplayTicks: 4,
            (_, snapshot, _) => sentSnapshots.Add(snapshot));

        Assert.True(world.ApplySnapshot(
            CreateSnapshot(world) with
            {
                SentryGibs =
                [
                    new SnapshotSentryGibState(911, (byte)PlayerTeam.Red, 402f, 310f, 23),
                ],
            },
            localPlayerSlot: SimulationWorld.LocalPlayerSlot));

        var simulator = new FixedStepSimulator(world);

        simulator.Step(config.FixedDeltaSeconds * 1.1d, broadcaster.BroadcastSnapshot);

        var snapshot = Assert.Single(sentSnapshots);
        var sentryGib = Assert.Single(snapshot.SentryGibs);
        Assert.Equal(911, sentryGib.Id);
        Assert.Equal((byte)PlayerTeam.Red, sentryGib.Team);
        Assert.Equal(22, sentryGib.TicksRemaining);
    }

    [Fact]
    public void BroadcastSnapshot_WithSpectatorClient_IncludesOnlySpectatorSnapshotState()
    {
        var world = new SimulationWorld();
        var config = world.Config;
        var spectatorSlot = SimulationWorld.FirstSpectatorSlot;
        var clientsBySlot = new Dictionary<byte, ClientSession>
        {
            [spectatorSlot] = new(
                spectatorSlot,
                new IPEndPoint(IPAddress.Loopback, 8190),
                "Watcher",
                TimeSpan.Zero),
        };
        var sentSnapshots = new List<SnapshotMessage>();
        var broadcaster = new SnapshotBroadcaster(
            world,
            config,
            clientsBySlot,
            transientEventReplayTicks: 4,
            (_, snapshot, _) => sentSnapshots.Add(snapshot));
        var simulator = new FixedStepSimulator(world);

        simulator.Step(config.FixedDeltaSeconds * 1.1d, broadcaster.BroadcastSnapshot);

        var snapshot = Assert.Single(sentSnapshots);
        var spectator = Assert.Single(snapshot.Players);
        Assert.Equal(1, snapshot.SpectatorCount);
        Assert.Equal(spectatorSlot, spectator.Slot);
        Assert.Equal("Watcher", spectator.Name);
        Assert.True(spectator.IsSpectator);
        Assert.False(spectator.IsAlive);
        Assert.DoesNotContain(snapshot.Players, player => player.Slot == SimulationWorld.LocalPlayerSlot);
    }

    [Fact]
    public void BroadcastSnapshot_WhenPlayableClientIsAwaitingJoin_IncludesTheirSnapshotState()
    {
        var world = new SimulationWorld();
        var config = world.Config;
        world.TryPrepareNetworkPlayerJoin(SimulationWorld.LocalPlayerSlot);
        var clientsBySlot = new Dictionary<byte, ClientSession>
        {
            [SimulationWorld.LocalPlayerSlot] = new(
                SimulationWorld.LocalPlayerSlot,
                new IPEndPoint(IPAddress.Loopback, 8190),
                "Player",
                TimeSpan.Zero),
        };
        var sentSnapshots = new List<SnapshotMessage>();
        var broadcaster = new SnapshotBroadcaster(
            world,
            config,
            clientsBySlot,
            transientEventReplayTicks: 4,
            (_, snapshot, _) => sentSnapshots.Add(snapshot));
        var simulator = new FixedStepSimulator(world);

        simulator.Step(config.FixedDeltaSeconds * 1.1d, broadcaster.BroadcastSnapshot);

        var snapshot = Assert.Single(sentSnapshots);
        var player = Assert.Single(snapshot.Players);
        Assert.Equal(SimulationWorld.LocalPlayerSlot, player.Slot);
        Assert.True(player.IsAwaitingJoin);
        Assert.False(player.IsAlive);
        Assert.Equal(0, player.RespawnTicks);
    }

    [Fact]
    public void BroadcastSnapshot_AfterAwaitingJoinClassSelection_SendsSpawnedPlayerState()
    {
        var world = new SimulationWorld();
        var config = world.Config;
        world.DespawnEnemyDummy();
        Assert.True(world.TryPrepareNetworkPlayerJoin(SimulationWorld.LocalPlayerSlot));
        Assert.True(world.TrySetNetworkPlayerTeam(SimulationWorld.LocalPlayerSlot, PlayerTeam.Red));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(SimulationWorld.LocalPlayerSlot, PlayerClass.Scout));

        var clientsBySlot = new Dictionary<byte, ClientSession>
        {
            [SimulationWorld.LocalPlayerSlot] = new(
                SimulationWorld.LocalPlayerSlot,
                new IPEndPoint(IPAddress.Loopback, 8190),
                "Player",
                TimeSpan.Zero),
        };
        var sentSnapshots = new List<SnapshotMessage>();
        var broadcaster = new SnapshotBroadcaster(
            world,
            config,
            clientsBySlot,
            transientEventReplayTicks: 4,
            (_, snapshot, _) => sentSnapshots.Add(snapshot));
        var simulator = new FixedStepSimulator(world);

        var ticks = simulator.Step(config.FixedDeltaSeconds * 1.1d, broadcaster.BroadcastSnapshot);

        Assert.Equal(1, ticks);
        var snapshot = Assert.Single(sentSnapshots);
        var player = Assert.Single(snapshot.Players);
        Assert.False(player.IsAwaitingJoin);
        Assert.True(player.IsAlive);
        Assert.Equal((byte)PlayerClass.Scout, player.ClassId);
        Assert.Equal((byte)PlayerTeam.Red, player.Team);
        Assert.True(player.X > 0f);
        Assert.True(player.Y > 0f);
    }

    private static SnapshotMessage CreateSnapshot(
        SimulationWorld world,
        IReadOnlyList<SnapshotShotState>? shots = null,
        IReadOnlyList<SnapshotSoundEvent>? soundEvents = null)
    {
        return new SnapshotMessage(
            Frame: (ulong)world.Frame,
            TickRate: world.Config.TicksPerSecond,
            LevelName: world.Level.Name,
            MapAreaIndex: (byte)Math.Clamp(world.Level.MapAreaIndex, 1, byte.MaxValue),
            MapAreaCount: (byte)Math.Clamp(world.Level.MapAreaCount, 1, byte.MaxValue),
            GameMode: (byte)world.MatchRules.Mode,
            MatchPhase: (byte)world.MatchState.Phase,
            WinnerTeam: 0,
            TimeRemainingTicks: world.MatchState.TimeRemainingTicks,
            RedCaps: world.RedCaps,
            BlueCaps: world.BlueCaps,
            SpectatorCount: 0,
            LastProcessedInputSequence: 0,
            RedIntel: new SnapshotIntelState((byte)PlayerTeam.Red, world.RedIntel.X, world.RedIntel.Y, world.RedIntel.IsAtBase, world.RedIntel.IsDropped, world.RedIntel.ReturnTicksRemaining),
            BlueIntel: new SnapshotIntelState((byte)PlayerTeam.Blue, world.BlueIntel.X, world.BlueIntel.Y, world.BlueIntel.IsAtBase, world.BlueIntel.IsDropped, world.BlueIntel.ReturnTicksRemaining),
            Players:
            [
                new SnapshotPlayerState(
                    Slot: SimulationWorld.LocalPlayerSlot,
                    PlayerId: world.LocalPlayer.Id,
                    Name: world.LocalPlayer.DisplayName,
                    Team: (byte)world.LocalPlayer.Team,
                    ClassId: (byte)world.LocalPlayer.ClassId,
                    IsAlive: true,
                    IsAwaitingJoin: false,
                    IsSpectator: false,
                    RespawnTicks: 0,
                    X: world.LocalPlayer.X,
                    Y: world.LocalPlayer.Y,
                    HorizontalSpeed: world.LocalPlayer.HorizontalSpeed,
                    VerticalSpeed: world.LocalPlayer.VerticalSpeed,
                    Health: (short)world.LocalPlayer.Health,
                    MaxHealth: (short)world.LocalPlayer.MaxHealth,
                    Ammo: (short)world.LocalPlayer.CurrentShells,
                    MaxAmmo: (short)world.LocalPlayer.MaxShells,
                    Kills: (short)world.LocalPlayer.Kills,
                    Deaths: (short)world.LocalPlayer.Deaths,
                    Caps: (short)world.LocalPlayer.Caps,
                    HealPoints: (short)world.LocalPlayer.HealPoints,
                    ActiveDominationCount: 0,
                    IsDominatingLocalViewer: false,
                    IsDominatedByLocalViewer: false,
                    Metal: world.LocalPlayer.Metal,
                    IsGrounded: world.LocalPlayer.IsGrounded,
                    RemainingAirJumps: world.LocalPlayer.RemainingAirJumps,
                    IsCarryingIntel: world.LocalPlayer.IsCarryingIntel,
                    IntelRechargeTicks: world.LocalPlayer.IntelRechargeTicks,
                    IsSpyCloaked: world.LocalPlayer.IsSpyCloaked,
                    SpyCloakAlpha: world.LocalPlayer.SpyCloakAlpha,
                    IsUbered: world.LocalPlayer.IsUbered,
                    IsHeavyEating: world.LocalPlayer.IsHeavyEating,
                    HeavyEatTicksRemaining: world.LocalPlayer.HeavyEatTicksRemaining,
                    IsSniperScoped: world.LocalPlayer.IsSniperScoped,
                    SniperChargeTicks: world.LocalPlayer.SniperChargeTicks,
                    FacingDirectionX: world.LocalPlayer.FacingDirectionX,
                    AimDirectionDegrees: world.LocalPlayer.AimDirectionDegrees,
                    IsTaunting: world.LocalPlayer.IsTaunting,
                    TauntFrameIndex: world.LocalPlayer.TauntFrameIndex,
                    IsChatBubbleVisible: world.LocalPlayer.IsChatBubbleVisible,
                    ChatBubbleFrameIndex: world.LocalPlayer.ChatBubbleFrameIndex,
                    ChatBubbleAlpha: world.LocalPlayer.ChatBubbleAlpha),
            ],
            CombatTraces: [],
            Sentries: [],
            Shots: shots ?? [],
            Bubbles: [],
            Blades: [],
            Needles: [],
            RevolverShots: [],
            Rockets: [],
            Flames: [],
            Flares: [],
            Mines: [],
            PlayerGibs: [],
            BloodDrops: [],
            DeadBodies: [],
            ControlPointSetupTicksRemaining: world.ControlPointSetupTicksRemaining,
            ControlPoints: [],
            Generators: [],
            LocalDeathCam: null,
            KillFeed: [],
            VisualEvents: [],
            SoundEvents: soundEvents ?? []);
    }
}
