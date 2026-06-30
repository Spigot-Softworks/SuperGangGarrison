using System.Net;
using System.Globalization;
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
    public void BuildBudgetedSnapshotKeepsPayloadUnderTargetAndPreservesHighPriorityContributions()
    {
        var baseline = CreateSnapshot(100);
        var current = CreateSnapshot(101);
        var contributions = new List<SnapshotDeltaBudgeter.Contribution>
        {
            new(
                Priority: 2000,
                DistanceSquared: 0f,
                EstimatedBytes: 88,
                Apply: static builder => builder.Rockets.Add(new SnapshotRocketState(
                    Id: 1,
                    Team: 1,
                    OwnerId: 7,
                    X: 128f,
                    Y: 64f,
                    PreviousX: 120f,
                    PreviousY: 60f,
                    DirectionRadians: 0.5f,
                    Speed: 240f,
                    TicksRemaining: 40))),
        };

        for (var index = 0; index < 120; index += 1)
        {
            var soundIndex = index;
            contributions.Add(new SnapshotDeltaBudgeter.Contribution(
                Priority: 1000 - index,
                DistanceSquared: index,
                EstimatedBytes: 48,
                Apply: builder => builder.SoundEvents.Add(new SnapshotSoundEvent(
                    $"low-priority-sound-event-{soundIndex:D3}-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                    X: soundIndex,
                    Y: soundIndex,
                    EventId: (ulong)soundIndex,
                    SourceFrame: 101))));
        }

        var result = SnapshotDeltaBudgeter.BuildBudgetedSnapshot(current, baseline, contributions);
        var merged = SnapshotDelta.ToFullSnapshot(result.Message, baseline);

        Assert.True(result.Payload.Length <= SnapshotDeltaBudgeter.TargetSnapshotPayloadBytes);
        Assert.True(result.Message.IsDelta);
        Assert.Equal(baseline.Frame, result.Message.BaselineFrame);
        Assert.Single(result.Message.Rockets);
        Assert.True(result.Message.SoundEvents.Count < 120);
        Assert.Single(merged.Rockets);
    }

    [Fact]
    public void BuildUntrimmedSnapshotEmergencyReductionDoesNotClearAllSoundEventsForKillFeed()
    {
        var baseline = CreateSnapshot(106);
        var killFeedEntry = new SnapshotKillFeedEntry(
            "Scout",
            1,
            "ScatterKL",
            "Pyro",
            2,
            "Scout fragged Pyro",
            0,
            5,
            501,
            502,
            EventId: 90)
        {
            InvolvedPlayerIds = [501, 502],
        };
        var current = CreateSnapshot(107) with
        {
            KillFeed = [killFeedEntry],
            SoundEvents =
            [
                new SnapshotSoundEvent("rocket_fire", 10f, 20f, EventId: 701, SourceFrame: 107),
                new SnapshotSoundEvent("damage_tick", 12f, 22f, EventId: 702, SourceFrame: 107),
            ],
        };
        var contributions = SnapshotContributionPlanner.BuildContributions(
            new SnapshotContributionPlanningContext(
                ViewerSlot: 1,
                FocusX: 0f,
                FocusY: 0f,
                Frame: 107,
                ClientAuthoritativePlayerId: 501),
            current,
            baseline);

        var result = SnapshotDeltaBudgeter.BuildUntrimmedSnapshotWithEmergencyReduction(
            current,
            baseline,
            contributions,
            targetPayloadBytes: SnapshotDeltaBudgeter.TargetSnapshotPayloadBytes);

        Assert.NotEmpty(result.Message.SoundEvents);
    }

    [Fact]
    public void BuildUntrimmedSnapshotDoesNotReduceAtNormalMtuPressure()
    {
        var baseline = CreateSnapshot(108);
        var soundEvents = Enumerable.Range(0, 160)
            .Select(index => new SnapshotSoundEvent(
                $"bot-spam-{index:D3}-{CreateDeterministicNoise(index, 48)}",
                X: index * 4f,
                Y: index * 2f,
                EventId: (ulong)(800 + index),
                SourceFrame: 109))
            .ToArray();
        var current = CreateSnapshot(109) with
        {
            SoundEvents = soundEvents,
        };
        var contributions = SnapshotContributionPlanner.BuildContributions(
            new SnapshotContributionPlanningContext(
                ViewerSlot: 1,
                FocusX: 0f,
                FocusY: 0f,
                Frame: 109,
                ClientAuthoritativePlayerId: 501),
            current,
            baseline);

        var result = SnapshotDeltaBudgeter.BuildUntrimmedSnapshotWithEmergencyReduction(
            current,
            baseline,
            contributions,
            targetPayloadBytes: SnapshotDeltaBudgeter.GameplayCriticalEmergencyPayloadBytes);

        Assert.True(result.Payload.Length > SnapshotDeltaBudgeter.TargetSnapshotPayloadBytes);
        Assert.True(result.Payload.Length <= SnapshotDeltaBudgeter.GameplayCriticalEmergencyPayloadBytes);
        Assert.False(result.ReductionApplied);
        Assert.Equal(soundEvents.Length, result.Message.SoundEvents.Count);

        static string CreateDeterministicNoise(int seed, int length)
        {
            var chars = new char[length];
            var value = unchecked((uint)(seed * 747796405 + 2891336453));
            for (var index = 0; index < chars.Length; index += 1)
            {
                value = unchecked(value * 1664525 + 1013904223);
                chars[index] = (char)('a' + (value % 26));
            }

            return new string(chars);
        }
    }

    [Fact]
    public void BuildContributionsTreatsNewRocketsAsRequiredProjectileSpawns()
    {
        var baseline = CreateSnapshot(110);
        var current = CreateSnapshot(111) with
        {
            Rockets =
            [
                new SnapshotRocketState(
                    Id: 42,
                    Team: 1,
                    OwnerId: 7,
                    X: 128f,
                    Y: 64f,
                    PreviousX: 120f,
                    PreviousY: 60f,
                    DirectionRadians: 0.5f,
                    Speed: 240f,
                    TicksRemaining: 40),
            ],
        };
        var client = new ClientSession(
            1,
            userId: 101,
            new IPEndPoint(IPAddress.Loopback, 8190),
            "Tester",
            TimeSpan.Zero);
        var contributions = SnapshotContributionPlanner.BuildContributions(
            client,
            current,
            baseline,
            new SimulationWorld());

        Assert.Contains(
            contributions,
            static contribution => contribution.Kind == SnapshotDeltaBudgeter.ContributionKind.ProjectileSpawn);
    }

    [Fact]
    public void BuildContributionsSkipsRocketSpawnEventsAlreadyCoveredByRocketState()
    {
        var rocket = new SnapshotRocketState(
            Id: 42,
            Team: 1,
            OwnerId: 7,
            X: 128f,
            Y: 64f,
            PreviousX: 120f,
            PreviousY: 60f,
            DirectionRadians: 0.5f,
            Speed: 240f,
            TicksRemaining: 40);
        var redundantSpawnEvent = new SnapshotRocketSpawnEvent(
            rocket.Id,
            rocket.Team,
            rocket.OwnerId,
            rocket.X,
            rocket.Y,
            rocket.PreviousX,
            rocket.PreviousY,
            rocket.DirectionRadians,
            rocket.Speed,
            rocket.TicksRemaining,
            EventId: 401);
        var immediateExplosionEvent = redundantSpawnEvent with
        {
            ExplodeImmediately = true,
            EventId = 402,
        };
        var replayedSpawnEvent = redundantSpawnEvent with
        {
            Id = 99,
            EventId = 403,
        };
        var baseline = CreateSnapshot(120);
        var current = CreateSnapshot(121) with
        {
            Rockets = [rocket],
            RocketSpawnEvents = [redundantSpawnEvent, immediateExplosionEvent, replayedSpawnEvent],
        };
        var client = new ClientSession(
            1,
            userId: 101,
            new IPEndPoint(IPAddress.Loopback, 8190),
            "Tester",
            TimeSpan.Zero);
        var contributions = SnapshotContributionPlanner.BuildContributions(
            client,
            current,
            baseline,
            new SimulationWorld());

        var result = SnapshotDeltaBudgeter.BuildBudgetedSnapshot(current, baseline, contributions, targetPayloadBytes: 64 * 1024);

        Assert.Single(result.Message.Rockets);
        Assert.DoesNotContain(result.Message.RocketSpawnEvents, rocketEvent => rocketEvent.EventId == redundantSpawnEvent.EventId);
        Assert.Contains(result.Message.RocketSpawnEvents, rocketEvent => rocketEvent.EventId == immediateExplosionEvent.EventId);
        Assert.Contains(result.Message.RocketSpawnEvents, rocketEvent => rocketEvent.EventId == replayedSpawnEvent.EventId);
    }

    [Fact]
    public void FilterRedundantRocketSpawnEventsKeepsImmediateExplosions()
    {
        var rocket = new SnapshotRocketState(
            Id: 42,
            Team: 1,
            OwnerId: 7,
            X: 128f,
            Y: 64f,
            PreviousX: 120f,
            PreviousY: 60f,
            DirectionRadians: 0.5f,
            Speed: 240f,
            TicksRemaining: 40);
        var redundantSpawnEvent = new SnapshotRocketSpawnEvent(
            rocket.Id,
            rocket.Team,
            rocket.OwnerId,
            rocket.X,
            rocket.Y,
            rocket.PreviousX,
            rocket.PreviousY,
            rocket.DirectionRadians,
            rocket.Speed,
            rocket.TicksRemaining,
            EventId: 411);
        var immediateExplosionEvent = redundantSpawnEvent with
        {
            ExplodeImmediately = true,
            EventId = 412,
        };
        var filtered = SnapshotBroadcaster.FilterRedundantRocketSpawnEvents(
            [redundantSpawnEvent, immediateExplosionEvent],
            [rocket]);

        Assert.DoesNotContain(filtered, rocketEvent => rocketEvent.EventId == redundantSpawnEvent.EventId);
        Assert.Contains(filtered, rocketEvent => rocketEvent.EventId == immediateExplosionEvent.EventId);
    }

    [Fact]
    public void BuildContributionsTreatsKnownShotMotionAsBudgetBackfill()
    {
        var knownShot = new SnapshotShotState(
            Id: 501,
            Team: 1,
            OwnerId: 77,
            X: 128f,
            Y: 96f,
            VelocityX: 12f,
            VelocityY: 0f,
            TicksRemaining: 40);
        var removedShot = knownShot with { Id = 502 };
        var movedKnownShot = knownShot with
        {
            X = knownShot.X + knownShot.VelocityX,
            TicksRemaining = knownShot.TicksRemaining - 1,
        };
        var newShot = knownShot with { Id = 503, X = 256f };
        var baseline = CreateSnapshot(112) with
        {
            Players = [CreatePlayerState(1, knownShot.OwnerId, "Owner")],
            Shots = [knownShot, removedShot],
        };
        var current = CreateSnapshot(113) with
        {
            Players = [CreatePlayerState(1, knownShot.OwnerId, "Owner")],
            Shots = [movedKnownShot, newShot],
        };
        var client = new ClientSession(
            1,
            userId: 101,
            new IPEndPoint(IPAddress.Loopback, 8190),
            "Tester",
            TimeSpan.Zero);
        var contributions = SnapshotContributionPlanner.BuildContributions(
            client,
            current,
            baseline,
            new SimulationWorld());

        var result = SnapshotDeltaBudgeter.BuildBudgetedSnapshot(current, baseline, contributions, targetPayloadBytes: 64 * 1024);

        Assert.Contains(result.Message.Shots, shot => shot.Id == movedKnownShot.Id);
        Assert.Contains(result.Message.Shots, shot => shot.Id == newShot.Id);
        Assert.Equal([removedShot.Id], result.Message.RemovedShotIds);
    }

    [Fact]
    public void BuildUntrimmedSnapshotKeepsProjectileMotionSkippedByEstimateWhenPayloadFits()
    {
        var knownShot = new SnapshotShotState(
            Id: 510,
            Team: 1,
            OwnerId: 77,
            X: 128f,
            Y: 96f,
            VelocityX: 12f,
            VelocityY: 0f,
            TicksRemaining: 40);
        var movedKnownShot = knownShot with
        {
            X = knownShot.X + knownShot.VelocityX,
            TicksRemaining = knownShot.TicksRemaining - 1,
        };
        var baseline = CreateSnapshot(114) with
        {
            Shots = [knownShot],
        };
        var current = CreateSnapshot(115) with
        {
            Shots = [movedKnownShot],
        };
        var contributions = new List<SnapshotDeltaBudgeter.Contribution>
        {
            new(
                Priority: 1000,
                DistanceSquared: 0f,
                EstimatedBytes: SnapshotDeltaBudgeter.TargetSnapshotPayloadBytes + 1,
                Apply: builder => builder.Shots.Add(movedKnownShot),
                Kind: SnapshotDeltaBudgeter.ContributionKind.ProjectileMotionUpdate),
        };

        var balanced = SnapshotDeltaBudgeter.BuildBudgetedSnapshot(
            current,
            baseline,
            contributions,
            SnapshotDeltaBudgeter.TargetSnapshotPayloadBytes);
        var untrimmed = SnapshotDeltaBudgeter.BuildUntrimmedSnapshotWithEmergencyReduction(
            current,
            baseline,
            contributions,
            SnapshotDeltaBudgeter.TargetSnapshotPayloadBytes);

        Assert.DoesNotContain(balanced.Message.Shots, shot => shot.Id == movedKnownShot.Id);
        Assert.Contains(untrimmed.Message.Shots, shot => shot.Id == movedKnownShot.Id);
        Assert.False(untrimmed.ReductionApplied);
        Assert.True(untrimmed.Payload.Length <= SnapshotDeltaBudgeter.TargetSnapshotPayloadBytes);
    }

    [Fact]
    public void BuildUntrimmedSnapshotReducesOptionalDataBeforeDroppingCriticalData()
    {
        var player = CreatePlayerState(1, 611, "Critical Player") with
        {
            X = 192f,
            Y = 128f,
            HorizontalSpeed = 8f,
            AimDirectionDegrees = 25f,
        };
        var rocket = new SnapshotRocketState(
            Id: 612,
            Team: 1,
            OwnerId: player.PlayerId,
            X: 192f,
            Y: 128f,
            PreviousX: 184f,
            PreviousY: 128f,
            DirectionRadians: 0f,
            Speed: 240f,
            TicksRemaining: 40);
        var baseline = CreateSnapshot(610) with
        {
            Players = [player with { X = 184f, HorizontalSpeed = 0f }],
        };
        var current = CreateSnapshot(611) with
        {
            Players = [player],
            Rockets = [rocket],
        };
        var criticalOnlyDelta = current with
        {
            IsDelta = true,
            BaselineFrame = baseline.Frame,
            PlayerMovementStates = [CreateMovementState(player)],
            PlayerChatBubbleStates = [new SnapshotPlayerChatBubbleState(player.Slot, true, 7, 0.9f)],
            Rockets = [rocket],
        };
        var visualEvents = Enumerable.Range(0, 80)
            .Select(static index => new SnapshotVisualEvent(
                $"optional-visual-{index:D2}-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                128f + index,
                96f,
                index,
                1,
                EventId: (ulong)(1000 + index)))
            .ToArray();
        var targetPayloadBytes = MeasurePayloadLength(criticalOnlyDelta) + 128;
        var contributions = new List<SnapshotDeltaBudgeter.Contribution>
        {
            new(
                Priority: 2000,
                DistanceSquared: 0f,
                EstimatedBytes: 32,
                Apply: builder => builder.PlayerMovementStates.Add(CreateMovementState(player)),
                Kind: SnapshotDeltaBudgeter.ContributionKind.PlayerMovementUpdate),
            new(
                Priority: 1900,
                DistanceSquared: 0f,
                EstimatedBytes: 88,
                Apply: builder => builder.Rockets.Add(rocket),
                Kind: SnapshotDeltaBudgeter.ContributionKind.ProjectileSpawn),
            new(
                Priority: 1800,
                DistanceSquared: 0f,
                EstimatedBytes: 10,
                Apply: builder => builder.PlayerChatBubbleStates.Add(new SnapshotPlayerChatBubbleState(player.Slot, true, 7, 0.9f)),
                Kind: SnapshotDeltaBudgeter.ContributionKind.PlayerChatBubbleUpdate),
            new(
                Priority: 500,
                DistanceSquared: 0f,
                EstimatedBytes: 4096,
                Apply: builder =>
                {
                    for (var index = 0; index < visualEvents.Length; index += 1)
                    {
                        builder.GibSpawnEvents.Add(new SnapshotGibSpawnEvent(
                            visualEvents[index].EffectName,
                            FrameIndex: 0,
                            visualEvents[index].X,
                            visualEvents[index].Y,
                            VelocityX: 0f,
                            VelocityY: 0f,
                            RotationSpeedDegrees: 0f,
                            HorizontalFriction: 0.9f,
                            RotationFriction: 0.9f,
                            LifetimeTicks: 30,
                            BloodChance: 0f,
                            EventId: visualEvents[index].EventId));
                    }
                }),
        };

        var result = SnapshotDeltaBudgeter.BuildUntrimmedSnapshotWithEmergencyReduction(
            current,
            baseline,
            contributions,
            targetPayloadBytes);

        Assert.True(result.ReductionApplied);
        Assert.True(result.CandidateComposition.FinalPayloadBytes > targetPayloadBytes);
        Assert.True(result.Payload.Length <= targetPayloadBytes);
        Assert.Single(result.Message.PlayerMovementStates);
        Assert.Single(result.Message.PlayerChatBubbleStates);
        Assert.Single(result.Message.Rockets);
        Assert.Empty(result.Message.GibSpawnEvents);
        Assert.True(result.CandidateComposition.EventBytes > result.Composition.EventBytes);
    }

    [Fact]
    public void BuildContributionsSendsRemoteProjectileMotionAsStateUpdate()
    {
        var localShot = new SnapshotShotState(
            Id: 501,
            Team: 1,
            OwnerId: 77,
            X: 128f,
            Y: 96f,
            VelocityX: 12f,
            VelocityY: 0f,
            TicksRemaining: 40);
        var remoteShot = localShot with { Id = 502, OwnerId = 88, X = 160f };
        var baseline = CreateSnapshot(112) with
        {
            Players = [CreatePlayerState(1, localShot.OwnerId, "Owner")],
            Shots = [localShot, remoteShot],
        };
        var current = CreateSnapshot(113) with
        {
            Players = [CreatePlayerState(1, localShot.OwnerId, "Owner")],
            Shots =
            [
                localShot with { X = localShot.X + localShot.VelocityX, TicksRemaining = localShot.TicksRemaining - 1 },
                remoteShot with { X = remoteShot.X + remoteShot.VelocityX, TicksRemaining = remoteShot.TicksRemaining - 1 },
            ],
        };
        var client = new ClientSession(
            1,
            userId: 101,
            new IPEndPoint(IPAddress.Loopback, 8190),
            "Tester",
            TimeSpan.Zero);

        var contributions = SnapshotContributionPlanner.BuildContributions(
            client,
            current,
            baseline,
            new SimulationWorld());

        Assert.Contains(contributions, contribution => contribution.Kind == SnapshotDeltaBudgeter.ContributionKind.ProjectileMotionUpdate);
        Assert.Contains(contributions, contribution => contribution.Kind == SnapshotDeltaBudgeter.ContributionKind.EntityStateUpdate);
    }

    [Fact]
    public void BuildContributionsTreatsKnownFlameMotionAsBudgetBackfill()
    {
        var knownFlame = new SnapshotFlameState(
            Id: 521,
            Team: 1,
            OwnerId: 77,
            X: 128f,
            Y: 96f,
            PreviousX: 120f,
            PreviousY: 94f,
            VelocityX: 8f,
            VelocityY: 1f,
            TicksRemaining: 15,
            AttachedPlayerId: -1,
            AttachedOffsetX: 0f,
            AttachedOffsetY: 0f);
        var removedFlame = knownFlame with { Id = 522 };
        var movedKnownFlame = knownFlame with
        {
            X = knownFlame.X + knownFlame.VelocityX,
            Y = knownFlame.Y + knownFlame.VelocityY,
            PreviousX = knownFlame.X,
            PreviousY = knownFlame.Y,
            TicksRemaining = knownFlame.TicksRemaining - 1,
        };
        var newFlame = knownFlame with { Id = 523, X = 256f };
        var baseline = CreateSnapshot(116) with
        {
            Players = [CreatePlayerState(1, knownFlame.OwnerId, "Owner")],
            Flames = [knownFlame, removedFlame],
        };
        var current = CreateSnapshot(117) with
        {
            Players = [CreatePlayerState(1, knownFlame.OwnerId, "Owner")],
            Flames = [movedKnownFlame, newFlame],
        };
        var client = new ClientSession(
            1,
            userId: 101,
            new IPEndPoint(IPAddress.Loopback, 8190),
            "Tester",
            TimeSpan.Zero);
        var contributions = SnapshotContributionPlanner.BuildContributions(
            client,
            current,
            baseline,
            new SimulationWorld());

        var result = SnapshotDeltaBudgeter.BuildBudgetedSnapshot(current, baseline, contributions, targetPayloadBytes: 64 * 1024);

        Assert.Contains(result.Message.Flames, flame => flame.Id == movedKnownFlame.Id);
        Assert.Contains(result.Message.Flames, flame => flame.Id == newFlame.Id);
        Assert.Equal([removedFlame.Id], result.Message.RemovedFlameIds);
    }

    [Fact]
    public void ApplySnapshotDoesNotRewindLocallySimulatedShotFromStaleBaselineState()
    {
        var world = new SimulationWorld
        {
            ClientPredictionMode = true,
        };
        var initialShot = new SnapshotShotState(
            Id: 601,
            Team: 1,
            OwnerId: 77,
            X: 128f,
            Y: 96f,
            VelocityX: 12f,
            VelocityY: 0f,
            TicksRemaining: 40);
        var initial = CreateSnapshot(114) with
        {
            LevelName = world.Level.Name,
            MapAreaIndex = (byte)world.Level.MapAreaIndex,
            MapAreaCount = (byte)world.Level.MapAreaCount,
            Players = [CreatePlayerState(1, 701, "Viewer")],
            Shots = [initialShot],
        };
        var staleResolvedSnapshot = CreateSnapshot(115) with
        {
            LevelName = world.Level.Name,
            MapAreaIndex = (byte)world.Level.MapAreaIndex,
            MapAreaCount = (byte)world.Level.MapAreaCount,
            Players = [CreatePlayerState(1, 701, "Viewer")],
            Shots = [initialShot],
        };

        Assert.True(world.ApplySnapshot(initial));
        var localShot = Assert.Single(world.Shots);
        localShot.ApplyNetworkState(
            initialShot.X + initialShot.VelocityX,
            initialShot.Y,
            initialShot.VelocityX,
            initialShot.VelocityY,
            initialShot.TicksRemaining - 1);

        Assert.True(world.ApplySnapshot(staleResolvedSnapshot));

        var retainedShot = Assert.Single(world.Shots);
        Assert.Equal(initialShot.X + initialShot.VelocityX, retainedShot.X);
        Assert.Equal(initialShot.TicksRemaining - 1, retainedShot.TicksRemaining);
    }

    [Fact]
    public void ApplySnapshotDoesNotRewindLocallySimulatedFlameFromStaleBaselineState()
    {
        var world = new SimulationWorld
        {
            ClientPredictionMode = true,
        };
        var initialFlame = new SnapshotFlameState(
            Id: 621,
            Team: 1,
            OwnerId: 77,
            X: 128f,
            Y: 96f,
            PreviousX: 120f,
            PreviousY: 94f,
            VelocityX: 8f,
            VelocityY: 1f,
            TicksRemaining: 15,
            AttachedPlayerId: -1,
            AttachedOffsetX: 0f,
            AttachedOffsetY: 0f);
        var initial = CreateSnapshot(118) with
        {
            LevelName = world.Level.Name,
            MapAreaIndex = (byte)world.Level.MapAreaIndex,
            MapAreaCount = (byte)world.Level.MapAreaCount,
            Players = [CreatePlayerState(1, 701, "Viewer")],
            Flames = [initialFlame],
        };
        var staleResolvedSnapshot = CreateSnapshot(119) with
        {
            LevelName = world.Level.Name,
            MapAreaIndex = (byte)world.Level.MapAreaIndex,
            MapAreaCount = (byte)world.Level.MapAreaCount,
            Players = [CreatePlayerState(1, 701, "Viewer")],
            Flames = [initialFlame],
        };

        Assert.True(world.ApplySnapshot(initial));
        var localFlame = Assert.Single(world.Flames);
        localFlame.ApplyNetworkState(
            initialFlame.X + initialFlame.VelocityX,
            initialFlame.Y + initialFlame.VelocityY,
            initialFlame.X,
            initialFlame.Y,
            initialFlame.VelocityX,
            initialFlame.VelocityY,
            initialFlame.TicksRemaining - 1,
            attachedPlayerId: null,
            initialFlame.AttachedOffsetX,
            initialFlame.AttachedOffsetY);

        Assert.True(world.ApplySnapshot(staleResolvedSnapshot));

        var retainedFlame = Assert.Single(world.Flames);
        Assert.Equal(initialFlame.X + initialFlame.VelocityX, retainedFlame.X);
        Assert.Equal(initialFlame.Y + initialFlame.VelocityY, retainedFlame.Y);
        Assert.Equal(initialFlame.TicksRemaining - 1, retainedFlame.TicksRemaining);
    }

    [Fact]
    public void ApplySnapshotDoesNotRewindLocallySimulatedRocketFromStaleBaselineState()
    {
        var world = new SimulationWorld
        {
            ClientPredictionMode = true,
        };
        var initialRocket = new SnapshotRocketState(
            Id: 631,
            Team: 1,
            OwnerId: 77,
            X: 128f,
            Y: 96f,
            PreviousX: 120f,
            PreviousY: 96f,
            DirectionRadians: 0f,
            Speed: 8f,
            TicksRemaining: 40);
        var initial = CreateSnapshot(120) with
        {
            LevelName = world.Level.Name,
            MapAreaIndex = (byte)world.Level.MapAreaIndex,
            MapAreaCount = (byte)world.Level.MapAreaCount,
            Players = [CreatePlayerState(1, 701, "Viewer")],
            Rockets = [initialRocket],
        };
        var staleResolvedSnapshot = CreateSnapshot(121) with
        {
            LevelName = world.Level.Name,
            MapAreaIndex = (byte)world.Level.MapAreaIndex,
            MapAreaCount = (byte)world.Level.MapAreaCount,
            Players = [CreatePlayerState(1, 701, "Viewer")],
            Rockets = [initialRocket],
        };

        Assert.True(world.ApplySnapshot(initial));
        var localRocket = Assert.Single(world.Rockets);
        localRocket.ApplyNetworkState(
            initialRocket.X + initialRocket.Speed,
            initialRocket.Y,
            initialRocket.X,
            initialRocket.Y,
            initialRocket.DirectionRadians,
            initialRocket.Speed,
            initialRocket.TicksRemaining - 1,
            initialRocket.ReducedKnockbackSourceTicksRemaining,
            initialRocket.ZeroKnockbackSourceTicksRemaining,
            initialRocket.RangeAnchorOwnerId,
            initialRocket.LastKnownRangeOriginX,
            initialRocket.LastKnownRangeOriginY,
            initialRocket.DistanceToTravel,
            initialRocket.IsFading,
            initialRocket.FadeSourceTicksRemaining,
            initialRocket.PassedFriendlyPlayerIds);

        Assert.True(world.ApplySnapshot(staleResolvedSnapshot));

        var retainedRocket = Assert.Single(world.Rockets);
        Assert.Equal(initialRocket.X + initialRocket.Speed, retainedRocket.X);
        Assert.Equal(initialRocket.TicksRemaining - 1, retainedRocket.TicksRemaining);
    }

    [Fact]
    public void FilterSoundEventsForClientDropsLocalManagedRapidFireSoundsOnly()
    {
        var client = new ClientSession(
            1,
            userId: 101,
            new IPEndPoint(IPAddress.Loopback, 8190),
            "Tester",
            TimeSpan.Zero);
        var viewer = new PlayerEntity(701, CharacterClassCatalog.Heavy, "Viewer");
        var events = new[]
        {
            new SnapshotSoundEvent("ChaingunSnd", 128f, 96f, EventId: 1, SourceFrame: 200, SourcePlayerId: viewer.Id),
            new SnapshotSoundEvent("ShotgunSnd", 128f, 96f, EventId: 2, SourceFrame: 200, SourcePlayerId: viewer.Id),
            new SnapshotSoundEvent("ChaingunSnd", 128f, 96f, EventId: 3, SourceFrame: 200, SourcePlayerId: 702),
        };

        var filtered = SnapshotBroadcaster.FilterSoundEventsForClient(events, client, viewer);

        Assert.DoesNotContain(filtered, soundEvent => soundEvent.EventId == 1);
        Assert.Contains(filtered, soundEvent => soundEvent.EventId == 2);
        Assert.Contains(filtered, soundEvent => soundEvent.EventId == 3);
    }

    [Fact]
    public void BuildBudgetedSnapshotMarksDroppedProjectileCollectionsIncomplete()
    {
        var baseline = CreateSnapshot(120);
        var current = CreateSnapshot(121);
        var shotStates = Enumerable.Range(0, 220)
            .Select(index => new SnapshotShotState(
                Id: 1000 + index,
                Team: 1,
                OwnerId: 700 + index,
                X: 128f + index,
                Y: 64f,
                VelocityX: 12f,
                VelocityY: 0f,
                TicksRemaining: 20))
            .ToArray();
        var contributions = shotStates
            .Select(shot => new SnapshotDeltaBudgeter.Contribution(
                Priority: 500,
                DistanceSquared: shot.Id,
                EstimatedBytes: 1,
                Apply: builder => builder.Shots.Add(shot)))
            .ToArray();

        var result = SnapshotDeltaBudgeter.BuildBudgetedSnapshot(
            current,
            baseline,
            contributions,
            targetPayloadBytes: 900);

        Assert.True(result.Payload.Length <= 900);
        Assert.Empty(result.Message.Shots);
        Assert.False((result.Message.EntityCollectionCompletenessFlags & SnapshotEntityCollectionCompletenessFlags.Shots) != 0);
        Assert.True((result.Message.EntityCollectionCompletenessFlags & SnapshotEntityCollectionCompletenessFlags.Rockets) != 0);
    }

    [Fact]
    public void BuildContributionsTreatsGameplayClassAndCloakChangesAsRosterCritical()
    {
        var localPlayer = CreatePlayerState(1, 1301, "Viewer");
        var baselineCustomClassPlayer = CreatePlayerState(2, 1302, "Custom Class") with
        {
            GameplayClassId = "class.scout",
        };
        var currentCustomClassPlayer = baselineCustomClassPlayer with
        {
            GameplayClassId = "class.soldier",
        };
        var baselineSpy = CreatePlayerState(3, 1303, "Remote Spy") with
        {
            ClassId = (byte)PlayerClass.Spy,
            IsSpyCloaked = false,
            SpyCloakAlpha = 1f,
        };
        var currentSpy = baselineSpy with
        {
            IsSpyCloaked = true,
            SpyCloakAlpha = 0.25f,
        };
        var baseline = CreateSnapshot(1300) with
        {
            Players = [localPlayer, baselineCustomClassPlayer, baselineSpy],
        };
        var current = CreateSnapshot(1301) with
        {
            Players = [localPlayer, currentCustomClassPlayer, currentSpy],
        };
        var client = new ClientSession(
            1,
            userId: 101,
            new IPEndPoint(IPAddress.Loopback, 8190),
            "Viewer",
            TimeSpan.Zero);
        var contributions = SnapshotContributionPlanner.BuildContributions(
            client,
            current,
            baseline,
            new SimulationWorld());
        var builder = new SnapshotDeltaBudgeter.Builder(current, baseline.Frame, seedFromTemplateCollections: false);

        foreach (var contribution in contributions.Where(static entry => entry.Kind == SnapshotDeltaBudgeter.ContributionKind.PlayerRosterUpdate))
        {
            contribution.Apply(builder);
        }

        var rosterPlayers = builder.Build().Players;
        Assert.Contains(rosterPlayers, player => player.Slot == 2 && player.GameplayClassId == "class.soldier");
        Assert.Contains(rosterPlayers, player => player.Slot == 3 && player.IsSpyCloaked);
    }

    [Fact]
    public void BuildBudgetedSnapshotWithReliableStreamBudgetPreservesPlayers()
    {
        var players = Enumerable.Range(0, 12)
            .Select(index => CreatePlayerState((byte)(index + 1), 500 + index, $"Bot Player {index:D2}"))
            .ToArray();
        var baseline = CreateSnapshot(200) with
        {
            Players = players,
        };
        var current = CreateSnapshot(201) with
        {
            Players = players,
        };

        var result = SnapshotDeltaBudgeter.BuildBudgetedSnapshot(
            current,
            baseline,
            [],
            SnapshotDeltaBudgeter.ReliableStreamTargetSnapshotPayloadBytes);

        Assert.True(result.Payload.Length <= SnapshotDeltaBudgeter.ReliableStreamTargetSnapshotPayloadBytes);
        Assert.Empty(result.Message.Players);
        var merged = SnapshotDelta.ToFullSnapshot(result.Message, baseline);
        Assert.Equal(players.Length, merged.Players.Count);
        Assert.Contains(merged.Players, player => player.Name == "Bot Player 00");
    }

    [Fact]
    public void BuildBudgetedSnapshotPreservesCloakedSpyStateWhenReducingPlayerStateAggressively()
    {
        var spy = CreatePlayerState(1, 900, "Cloaked Spy") with
        {
            ClassId = (byte)PlayerClass.Spy,
            IsSpyCloaked = true,
            SpyCloakAlpha = 0f,
        };

        var method = typeof(SnapshotDeltaBudgeter).GetMethod(
            "ReducePlayerStateAggressivelyForBudget",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var reduced = (SnapshotPlayerState)method.Invoke(null, new object[] { spy })!;
        Assert.True(reduced.IsSpyCloaked);
        Assert.Equal(0f, reduced.SpyCloakAlpha);
    }

    [Fact]
    public void BuildBudgetedSnapshotReplicatesKnownPlayerMovementAsCompactDeltas()
    {
        var baselinePlayers = Enumerable.Range(0, 20)
            .Select(index => CreatePlayerState((byte)(index + 1), 500 + index, $"Roster Player {index:D2}"))
            .ToArray();
        var currentPlayers = baselinePlayers
            .Select(player => player with { X = player.X + 8f, AimDirectionDegrees = player.AimDirectionDegrees + 15f })
            .ToArray();
        var baseline = CreateSnapshot(300) with
        {
            Players = baselinePlayers,
        };
        var current = CreateSnapshot(301) with
        {
            Players = currentPlayers,
        };
        var client = new ClientSession(
            1,
            userId: 101,
            new IPEndPoint(IPAddress.Loopback, 8190),
            "Tester",
            TimeSpan.Zero);
        var world = new SimulationWorld();
        var contributions = SnapshotContributionPlanner.BuildContributions(client, current, baseline, world);

        var result = SnapshotDeltaBudgeter.BuildBudgetedSnapshot(current, baseline, contributions);
        var merged = SnapshotDelta.ToFullSnapshot(result.Message, baseline);

        Assert.True(result.Payload.Length <= SnapshotDeltaBudgeter.TargetSnapshotPayloadBytes);
        Assert.Empty(result.Message.Players);
        Assert.Equal(currentPlayers.Length, result.Message.PlayerMovementStates.Count);
        Assert.Equal(currentPlayers.Length, merged.Players.Count);
        var localPlayer = Assert.Single(merged.Players, player => player.Slot == 1);
        Assert.Equal(currentPlayers[0].X, localPlayer.X);
        Assert.InRange(
            localPlayer.AimDirectionDegrees,
            currentPlayers[0].AimDirectionDegrees - 0.01f,
            currentPlayers[0].AimDirectionDegrees + 0.01f);
    }

    [Fact]
    public void BuildBudgetedSnapshotKeepsBotMovementFromStarvingProjectiles()
    {
        var baselinePlayers = Enumerable.Range(0, 12)
            .Select(index => CreatePlayerState((byte)(index + 1), 900 + index, $"Moving Player {index:D2}"))
            .ToArray();
        var currentPlayers = baselinePlayers
            .Select(player => player with
            {
                X = player.X + 12f,
                HorizontalSpeed = 6f,
                AimDirectionDegrees = player.AimDirectionDegrees + 20f,
            })
            .ToArray();
        var baseline = CreateSnapshot(320) with
        {
            Players = baselinePlayers,
        };
        var current = CreateSnapshot(321) with
        {
            Players = currentPlayers,
            Shots = Enumerable.Range(0, 8)
                .Select(index => new SnapshotShotState(
                    Id: 1000 + index,
                    Team: 1,
                    OwnerId: 900 + index,
                    X: 128f + index,
                    Y: 64f + index,
                    VelocityX: 16f,
                    VelocityY: 0f,
                    TicksRemaining: 24))
                .ToArray(),
            Rockets = Enumerable.Range(0, 3)
                .Select(index => new SnapshotRocketState(
                    Id: 2000 + index,
                    Team: 1,
                    OwnerId: 900 + index,
                    X: 200f + index,
                    Y: 90f + index,
                    PreviousX: 196f + index,
                    PreviousY: 90f + index,
                    DirectionRadians: 0f,
                    Speed: 240f,
                    TicksRemaining: 30))
                .ToArray(),
        };
        var client = new ClientSession(
            1,
            userId: 101,
            new IPEndPoint(IPAddress.Loopback, 8190),
            "Tester",
            TimeSpan.Zero);
        var world = new SimulationWorld();
        var contributions = SnapshotContributionPlanner.BuildContributions(client, current, baseline, world);

        var result = SnapshotDeltaBudgeter.BuildBudgetedSnapshot(current, baseline, contributions);
        var merged = SnapshotDelta.ToFullSnapshot(result.Message, baseline);

        Assert.True(result.Payload.Length <= SnapshotDeltaBudgeter.TargetSnapshotPayloadBytes);
        Assert.Equal(currentPlayers.Length, result.Message.PlayerMovementStates.Count);
        Assert.Equal(current.Shots.Count, result.Message.Shots.Count);
        Assert.Equal(current.Rockets.Count, result.Message.Rockets.Count);
        Assert.All(currentPlayers, expected =>
        {
            var actual = Assert.Single(merged.Players, player => player.Slot == expected.Slot);
            Assert.Equal(expected.X, actual.X);
            Assert.Equal(expected.HorizontalSpeed, actual.HorizontalSpeed);
        });
    }

    [Fact]
    public void BuildBudgetedSnapshotDoesNotClearAllMovementWhenEstimatesUnderrun()
    {
        var baselinePlayers = Enumerable.Range(0, 16)
            .Select(index => CreatePlayerState((byte)(index + 1), 1200 + index, $"Moving Player {index:D2}"))
            .ToArray();
        var currentPlayers = baselinePlayers
            .Select(player => player with
            {
                X = player.X + 18f,
                HorizontalSpeed = 7f,
                AimDirectionDegrees = player.AimDirectionDegrees + 12f,
            })
            .ToArray();
        var baseline = CreateSnapshot(340) with
        {
            Players = baselinePlayers,
        };
        var current = CreateSnapshot(341) with
        {
            Players = currentPlayers,
        };
        var contributions = new List<SnapshotDeltaBudgeter.Contribution>
        {
            new(
                Priority: 2000,
                DistanceSquared: 0f,
                EstimatedBytes: 1,
                Apply: builder => builder.PlayerMovementStates.Add(CreateMovementState(currentPlayers[0])),
                Kind: SnapshotDeltaBudgeter.ContributionKind.LocalPlayerUpdate),
        };
        for (var index = 1; index < currentPlayers.Length; index += 1)
        {
            var player = currentPlayers[index];
            contributions.Add(new SnapshotDeltaBudgeter.Contribution(
                Priority: 1800 - index,
                DistanceSquared: index,
                EstimatedBytes: 1,
                Apply: builder => builder.PlayerMovementStates.Add(CreateMovementState(player)),
                Kind: SnapshotDeltaBudgeter.ContributionKind.PlayerMovementUpdate));
        }

        var result = SnapshotDeltaBudgeter.BuildBudgetedSnapshot(
            current,
            baseline,
            contributions,
            targetPayloadBytes: 260);

        Assert.True(result.Payload.Length <= 260);
        Assert.NotEmpty(result.Message.PlayerMovementStates);
        Assert.Contains(result.Message.PlayerMovementStates, player => player.Slot == currentPlayers[0].Slot);
    }

    [Fact]
    public void BuildBudgetedSnapshotPreservesFarTransientEventsWhenUnderBudget()
    {
        var world = new SimulationWorld();
        var nearX = world.Bounds.Width / 2f;
        var nearY = world.Bounds.Height / 2f;
        var baseline = CreateSnapshot(600);
        var current = CreateSnapshot(601) with
        {
            SoundEvents =
            [
                new SnapshotSoundEvent("near-sound", nearX, nearY, EventId: 1, SourceFrame: 601),
                new SnapshotSoundEvent("far-sound", 5000f, 5000f, EventId: 2, SourceFrame: 601),
            ],
            VisualEvents =
            [
                new SnapshotVisualEvent("near-visual", nearX, nearY, 0f, 1, EventId: 3),
                new SnapshotVisualEvent("far-visual", 5000f, 5000f, 0f, 1, EventId: 4),
            ],
            DamageEvents =
            [
                new SnapshotDamageEvent(25, 1, -1, 1, 10, nearX, nearY, false, EventId: 5, SourceFrame: 601),
                new SnapshotDamageEvent(25, 1, -1, 1, 10, 5000f, 5000f, false, EventId: 6, SourceFrame: 601),
            ],
            RocketSpawnEvents =
            [
                new SnapshotRocketSpawnEvent(
                    701,
                    1,
                    5,
                    nearX,
                    nearY,
                    nearX - 4f,
                    nearY,
                    0f,
                    240f,
                    20,
                    EventId: 7),
                new SnapshotRocketSpawnEvent(
                    702,
                    1,
                    5,
                    5000f,
                    5000f,
                    4996f,
                    5000f,
                    0f,
                    240f,
                    20,
                    EventId: 8),
            ],
            GibSpawnEvents =
            [
                new SnapshotGibSpawnEvent("near-gib", 0, nearX, nearY, 1f, -2f, 15f, 0.4f, 0.5f, 120, 1f, EventId: 9),
                new SnapshotGibSpawnEvent("far-gib", 0, 5000f, 5000f, 1f, -2f, 15f, 0.4f, 0.5f, 120, 1f, EventId: 10),
            ],
        };
        var client = new ClientSession(
            1,
            userId: 101,
            new IPEndPoint(IPAddress.Loopback, 8190),
            "Tester",
            TimeSpan.Zero);
        var contributions = SnapshotContributionPlanner.BuildContributions(client, current, baseline, world);

        var result = SnapshotDeltaBudgeter.BuildBudgetedSnapshot(current, baseline, contributions, targetPayloadBytes: 64 * 1024);

        Assert.Equal(2, result.Message.SoundEvents.Count);
        Assert.Equal(2, result.Message.VisualEvents.Count);
        Assert.Equal(2, result.Message.DamageEvents.Count);
        Assert.Equal(2, result.Message.RocketSpawnEvents.Count);
        Assert.Equal(2, result.Message.GibSpawnEvents.Count);
        Assert.Contains(result.Message.SoundEvents, soundEvent => soundEvent.SoundName == "far-sound");
        Assert.Contains(result.Message.VisualEvents, visualEvent => visualEvent.EffectName == "far-visual");
        Assert.Contains(result.Message.DamageEvents, damageEvent => damageEvent.EventId == 6);
        Assert.Contains(result.Message.RocketSpawnEvents, rocketEvent => rocketEvent.EventId == 8);
        Assert.Contains(result.Message.GibSpawnEvents, gibEvent => gibEvent.EventId == 10);
    }

    [Fact]
    public void BuildContributionsTreatsDamageEventsAsRequiredTransientFeedback()
    {
        var baseline = CreateSnapshot(608);
        var current = CreateSnapshot(609) with
        {
            DamageEvents =
            [
                new SnapshotDamageEvent(
                    Amount: 18,
                    TargetKind: 1,
                    TargetEntityId: 77,
                    AttackerPlayerId: 88,
                    AssistedByPlayerId: -1,
                    X: 128f,
                    Y: 96f,
                    WasFatal: false,
                    EventId: 66,
                    SourceFrame: 609),
            ],
        };
        var client = new ClientSession(
            1,
            userId: 101,
            new IPEndPoint(IPAddress.Loopback, 8190),
            "Tester",
            TimeSpan.Zero);

        var contributions = SnapshotContributionPlanner.BuildContributions(client, current, baseline, new SimulationWorld());

        Assert.Contains(
            contributions,
            static contribution => contribution.Kind == SnapshotDeltaBudgeter.ContributionKind.TransientDamageEvent);
    }

    [Fact]
    public void BuildContributionsTreatsSoundEventsAsReliableOldestFirstTransientEvents()
    {
        var baseline = CreateSnapshot(610);
        var current = CreateSnapshot(611) with
        {
            SoundEvents =
            [
                new SnapshotSoundEvent("older-sound", 128f, 96f, EventId: 41, SourceFrame: 610),
                new SnapshotSoundEvent("newer-sound", 128f, 96f, EventId: 42, SourceFrame: 611),
            ],
        };
        var client = new ClientSession(
            1,
            userId: 101,
            new IPEndPoint(IPAddress.Loopback, 8190),
            "Tester",
            TimeSpan.Zero);
        var world = new SimulationWorld();

        var soundContributions = SnapshotContributionPlanner.BuildContributions(client, current, baseline, world)
            .Where(static contribution => contribution.Kind == SnapshotDeltaBudgeter.ContributionKind.TransientSoundEvent)
            .OrderByDescending(static contribution => contribution.Priority)
            .ToArray();
        var builder = new SnapshotDeltaBudgeter.Builder(current, baseline.Frame, seedFromTemplateCollections: false);
        foreach (var contribution in soundContributions)
        {
            contribution.Apply(builder);
        }

        Assert.Equal(2, soundContributions.Length);
        Assert.True(soundContributions[0].Priority > soundContributions[1].Priority);
        Assert.Equal(["older-sound", "newer-sound"], builder.SoundEvents.Select(static sound => sound.SoundName).ToArray());
    }

    [Fact]
    public void TransientEventBufferRetainsGibSpawnEventsForReplay()
    {
        var world = new SimulationWorld();
        var pendingGibSpawnEvents = GetPendingGibSpawnEvents(world);
        pendingGibSpawnEvents.Add(new WorldGibSpawnEvent(
            "HeadS",
            1,
            128f,
            64f,
            3f,
            -5f,
            45f,
            0.4f,
            0.5f,
            180,
            1.25f));
        var buffer = new SnapshotTransientEventBuffer(transientEventReplayTicks: 3);

        var events = buffer.CaptureCurrentEvents(world);

        var gibEvent = Assert.Single(events.GibSpawnEvents);
        Assert.Equal("HeadS", gibEvent.SpriteName);
        Assert.NotEqual<ulong>(0, gibEvent.EventId);
        Assert.Empty(world.DrainPendingGibSpawnEvents());
    }

    [Fact]
    public void TransientEventBufferSkipsManagedRapidFireLoopSounds()
    {
        var world = new SimulationWorld();
        var heavy = AddNetworkPlayer(world, 2, PlayerClass.Heavy);
        var pyro = AddNetworkPlayer(world, 3, PlayerClass.Pyro);
        var medic = AddNetworkPlayer(world, 4, PlayerClass.Medic);
        var healTarget = AddNetworkPlayer(world, 5, PlayerClass.Scout);
        var acquiredSoldier = AddNetworkPlayer(world, 6, PlayerClass.Soldier);
        Assert.True(heavy.TryFirePrimaryWeapon(ignoreAmmoCost: true));
        pyro.CommitPyroPrimaryWeaponShot(ignoreAmmoCost: true);
        medic.SetMedicHealingTarget(healTarget);
        Assert.True(world.TryGrantNetworkPlayerGameplayItem(6, "weapon.minigun"));
        Assert.True(world.TrySetNetworkPlayerGameplayAcquiredItem(6, "weapon.minigun"));
        Assert.True(world.TrySetNetworkPlayerGameplayEquippedSlot(6, GameplayEquipmentSlot.Secondary));
        Assert.True(acquiredSoldier.TryFireAcquiredWeapon());
        var pendingSoundEvents = GetPendingSoundEvents(world);
        pendingSoundEvents.Add(new WorldSoundEvent("ChaingunSnd", 128f, 96f, SourcePlayerId: heavy.Id));
        pendingSoundEvents.Add(new WorldSoundEvent("FlamethrowerSnd", 128f, 96f, SourcePlayerId: pyro.Id));
        pendingSoundEvents.Add(new WorldSoundEvent("MedigunSnd", 128f, 96f, SourcePlayerId: medic.Id));
        pendingSoundEvents.Add(new WorldSoundEvent("ShotgunSnd", 128f, 96f, SourcePlayerId: healTarget.Id));
        pendingSoundEvents.Add(new WorldSoundEvent("ChaingunSnd", 128f, 96f));
        pendingSoundEvents.Add(new WorldSoundEvent("ChaingunSnd", 128f, 96f, SourcePlayerId: acquiredSoldier.Id));
        var buffer = new SnapshotTransientEventBuffer(transientEventReplayTicks: 3);

        var events = buffer.CaptureCurrentEvents(world);

        Assert.Equal(3, events.SoundEvents.Length);
        Assert.DoesNotContain(events.SoundEvents, soundEvent => soundEvent.SourcePlayerId == heavy.Id);
        Assert.DoesNotContain(events.SoundEvents, soundEvent => soundEvent.SourcePlayerId == pyro.Id);
        Assert.DoesNotContain(events.SoundEvents, soundEvent => soundEvent.SourcePlayerId == medic.Id);
        Assert.Contains(events.SoundEvents, soundEvent => soundEvent.SoundName == "ShotgunSnd");
        Assert.Contains(events.SoundEvents, soundEvent => soundEvent.SoundName == "ChaingunSnd" && soundEvent.SourcePlayerId < 0);
        Assert.Contains(events.SoundEvents, soundEvent => soundEvent.SoundName == "ChaingunSnd" && soundEvent.SourcePlayerId == acquiredSoldier.Id);
        Assert.All(events.SoundEvents, soundEvent => Assert.NotEqual<ulong>(0, soundEvent.EventId));
        Assert.Empty(world.DrainPendingSoundEvents());
    }

    [Fact]
    public void BuildBudgetedSnapshotThrottlesCosmeticEntityMotionUpdates()
    {
        var baseline = CreateSnapshot(620) with
        {
            SentryGibs =
            [
                new SnapshotSentryGibState(1, 1, 100f, 100f, 30),
                new SnapshotSentryGibState(8, 1, 120f, 100f, 30),
            ],
        };
        var current = baseline with
        {
            Frame = 621,
            SentryGibs =
            [
                new SnapshotSentryGibState(1, 1, 101f, 100f, 29),
                new SnapshotSentryGibState(8, 1, 121f, 100f, 29),
            ],
        };
        var client = new ClientSession(
            1,
            userId: 101,
            new IPEndPoint(IPAddress.Loopback, 8190),
            "Tester",
            TimeSpan.Zero);
        var world = new SimulationWorld();
        var contributions = SnapshotContributionPlanner.BuildContributions(client, current, baseline, world);

        var result = SnapshotDeltaBudgeter.BuildBudgetedSnapshot(current, baseline, contributions, targetPayloadBytes: 64 * 1024);

        var sentryGib = Assert.Single(result.Message.SentryGibs);
        Assert.Equal(8, sentryGib.Id);
        Assert.Empty(result.Message.PlayerGibs);
    }

    [Fact]
    public void BuildBudgetedSnapshotForcesNewPlayerRosterEntryBeforeKnownMovement()
    {
        var knownPlayer = CreatePlayerState(1, 601, "Known Player");
        var movedKnownPlayer = knownPlayer with
        {
            X = knownPlayer.X + 24f,
            AimDirectionDegrees = knownPlayer.AimDirectionDegrees + 10f,
        };
        var addedPlayer = CreatePlayerState(2, 602, "Added Bot");
        var baseline = CreateSnapshot(600) with
        {
            Players = [knownPlayer],
        };
        var current = CreateSnapshot(601) with
        {
            Players = [movedKnownPlayer, addedPlayer],
        };
        var contributions = new List<SnapshotDeltaBudgeter.Contribution>
        {
            new(
                Priority: 2000,
                DistanceSquared: 0f,
                EstimatedBytes: 32,
                Apply: builder => builder.PlayerMovementStates.Add(CreateMovementState(movedKnownPlayer)),
                Kind: SnapshotDeltaBudgeter.ContributionKind.PlayerMovementUpdate),
            new(
                Priority: 1900,
                DistanceSquared: 1f,
                EstimatedBytes: 4096,
                Apply: builder => builder.Players.Add(addedPlayer),
                Kind: SnapshotDeltaBudgeter.ContributionKind.PlayerFirstAppearance),
        };

        var result = SnapshotDeltaBudgeter.BuildBudgetedSnapshot(current, baseline, contributions);
        var merged = SnapshotDelta.ToFullSnapshot(result.Message, baseline);

        Assert.True(result.Payload.Length <= SnapshotDeltaBudgeter.TargetSnapshotPayloadBytes);
        Assert.Contains(result.Message.Players, player => player.Slot == addedPlayer.Slot);
        Assert.Contains(merged.Players, player => player.Slot == knownPlayer.Slot);
        Assert.Contains(merged.Players, player => player.Slot == addedPlayer.Slot);
    }

    [Fact]
    public void BuildBudgetedSnapshotForcesRosterCorrectionWhenKnownSlotChangesClass()
    {
        var staleBot = CreatePlayerState(2, 701, "Old Occupant") with
        {
            ClassId = (byte)PlayerClass.Heavy,
        };
        var correctedBot = staleBot with
        {
            PlayerId = 702,
            Name = "Soldier Bot",
            ClassId = (byte)PlayerClass.Soldier,
            X = staleBot.X + 64f,
        };
        var baseline = CreateSnapshot(700) with
        {
            Players = [staleBot],
        };
        var current = CreateSnapshot(701) with
        {
            Players = [correctedBot],
        };
        var client = new ClientSession(
            1,
            userId: 101,
            new IPEndPoint(IPAddress.Loopback, 8190),
            "Tester",
            TimeSpan.Zero);
        var world = new SimulationWorld();
        var contributions = SnapshotContributionPlanner.BuildContributions(client, current, baseline, world);
        var inflatedContributions = contributions
            .Select(contribution => contribution.Kind == SnapshotDeltaBudgeter.ContributionKind.PlayerRosterUpdate
                ? contribution with { EstimatedBytes = 4096 }
                : contribution)
            .ToArray();

        var result = SnapshotDeltaBudgeter.BuildBudgetedSnapshot(current, baseline, inflatedContributions);
        var merged = SnapshotDelta.ToFullSnapshot(result.Message, baseline);

        Assert.True(result.Payload.Length <= SnapshotDeltaBudgeter.TargetSnapshotPayloadBytes);
        var deltaPlayer = Assert.Single(result.Message.Players);
        Assert.Equal((byte)PlayerClass.Soldier, deltaPlayer.ClassId);
        var mergedPlayer = Assert.Single(merged.Players);
        Assert.Equal(correctedBot.PlayerId, mergedPlayer.PlayerId);
        Assert.Equal(correctedBot.Name, mergedPlayer.Name);
        Assert.Equal((byte)PlayerClass.Soldier, mergedPlayer.ClassId);
        Assert.Equal(correctedBot.X, mergedPlayer.X);
    }

    [Fact]
    public void BuildBudgetedSnapshotForcesRemotePlayerMovementWhenLocalCorrectionWouldCrowdItOut()
    {
        var localPlayer = CreatePlayerState(1, 801, "Local Player") with
        {
            ClassId = (byte)PlayerClass.Soldier,
        };
        var remoteBot = CreatePlayerState(2, 802, "Remote Bot") with
        {
            ClassId = (byte)PlayerClass.Soldier,
        };
        var movedLocalPlayer = localPlayer with
        {
            X = localPlayer.X + 4f,
        };
        var movedRemoteBot = remoteBot with
        {
            X = remoteBot.X + 96f,
            HorizontalSpeed = 8f,
        };
        var baseline = CreateSnapshot(800) with
        {
            Players = [localPlayer, remoteBot],
        };
        var current = CreateSnapshot(801) with
        {
            Players = [movedLocalPlayer, movedRemoteBot],
        };
        var contributions = new List<SnapshotDeltaBudgeter.Contribution>
        {
            new(
                Priority: 2000,
                DistanceSquared: 0f,
                EstimatedBytes: 32,
                Apply: builder => builder.PlayerMovementStates.Add(CreateMovementState(movedLocalPlayer)),
                Kind: SnapshotDeltaBudgeter.ContributionKind.LocalPlayerUpdate),
            new(
                Priority: 1500,
                DistanceSquared: 1f,
                EstimatedBytes: 32,
                Apply: builder => builder.PlayerMovementStates.Add(CreateMovementState(movedRemoteBot)),
                Kind: SnapshotDeltaBudgeter.ContributionKind.PlayerMovementUpdate),
        };

        var result = SnapshotDeltaBudgeter.BuildBudgetedSnapshot(current, baseline, contributions);
        var merged = SnapshotDelta.ToFullSnapshot(result.Message, baseline);

        Assert.True(result.Payload.Length <= SnapshotDeltaBudgeter.TargetSnapshotPayloadBytes);
        Assert.Contains(result.Message.PlayerMovementStates, player => player.Slot == remoteBot.Slot);
        var mergedBot = Assert.Single(merged.Players, player => player.Slot == remoteBot.Slot);
        Assert.Equal(movedRemoteBot.X, mergedBot.X);
        Assert.Equal(movedRemoteBot.HorizontalSpeed, mergedBot.HorizontalSpeed);
    }

    [Fact]
    public void BuildBudgetedSnapshotForcesLocalPlayerCorrectionWhenRemoteUpdatesWouldCrowdItOut()
    {
        var localPlayer = CreatePlayerState(1, 851, "Local Player") with
        {
            ClassId = (byte)PlayerClass.Soldier,
        };
        var remoteBot = CreatePlayerState(2, 852, "Remote Bot") with
        {
            ClassId = (byte)PlayerClass.Soldier,
        };
        var correctedLocalPlayer = localPlayer with
        {
            X = localPlayer.X + 128f,
            HorizontalSpeed = 12f,
        };
        var movedRemoteBot = remoteBot with
        {
            X = remoteBot.X + 24f,
        };
        var baseline = CreateSnapshot(850) with
        {
            Players = [localPlayer, remoteBot],
        };
        var current = CreateSnapshot(851) with
        {
            Players = [correctedLocalPlayer, movedRemoteBot],
        };
        var contributions = new List<SnapshotDeltaBudgeter.Contribution>
        {
            new(
                Priority: 2500,
                DistanceSquared: 0f,
                EstimatedBytes: 32,
                Apply: builder => builder.PlayerMovementStates.Add(CreateMovementState(movedRemoteBot)),
                Kind: SnapshotDeltaBudgeter.ContributionKind.PlayerMovementUpdate),
            new(
                Priority: 1000,
                DistanceSquared: 1f,
                EstimatedBytes: 32,
                Apply: builder => builder.PlayerMovementStates.Add(CreateMovementState(correctedLocalPlayer)),
                Kind: SnapshotDeltaBudgeter.ContributionKind.LocalPlayerUpdate),
        };

        var result = SnapshotDeltaBudgeter.BuildBudgetedSnapshot(current, baseline, contributions);
        var merged = SnapshotDelta.ToFullSnapshot(result.Message, baseline);

        Assert.True(result.Payload.Length <= SnapshotDeltaBudgeter.TargetSnapshotPayloadBytes);
        Assert.Contains(result.Message.PlayerMovementStates, player => player.Slot == localPlayer.Slot);
        var mergedLocalPlayer = Assert.Single(merged.Players, player => player.Slot == localPlayer.Slot);
        Assert.Equal(correctedLocalPlayer.X, mergedLocalPlayer.X);
        Assert.Equal(correctedLocalPlayer.HorizontalSpeed, mergedLocalPlayer.HorizontalSpeed);
    }

    [Fact]
    public void BuildBudgetedSnapshotPreservesLocalPlayerStatusWhenFullDetailIsCrowdedOut()
    {
        var baselineLocalPlayer = CreatePlayerState(1, 951, "Local Player") with
        {
            Health = 125,
            Ammo = 6,
            Metal = 0f,
        };
        var remoteBot = CreatePlayerState(2, 952, "Remote Bot");
        var currentLocalPlayer = baselineLocalPlayer with
        {
            Health = 48,
            Ammo = 2,
            Metal = 75f,
        };
        var baseline = CreateSnapshot(950) with
        {
            Players = [baselineLocalPlayer, remoteBot],
        };
        var current = CreateSnapshot(951) with
        {
            Players = [currentLocalPlayer, remoteBot],
        };
        var client = new ClientSession(
            1,
            userId: 101,
            new IPEndPoint(IPAddress.Loopback, 8190),
            "Tester",
            TimeSpan.Zero);
        var world = new SimulationWorld();
        var contributions = SnapshotContributionPlanner.BuildContributions(client, current, baseline, world);

        var result = SnapshotDeltaBudgeter.BuildBudgetedSnapshot(current, baseline, contributions, targetPayloadBytes: 320);
        var merged = SnapshotDelta.ToFullSnapshot(result.Message, baseline);

        Assert.True(result.Payload.Length <= 320);
        var statusState = Assert.Single(result.Message.PlayerStatusStates);
        Assert.Equal(1, statusState.Slot);
        Assert.Equal(48, statusState.Health);
        var mergedLocalPlayer = Assert.Single(merged.Players, player => player.Slot == 1);
        Assert.Equal(48, mergedLocalPlayer.Health);
        Assert.Equal(2, mergedLocalPlayer.Ammo);
        Assert.Equal(75f, mergedLocalPlayer.Metal);
    }

    [Fact]
    public void BuildBudgetedSnapshotPreservesHeavyDashRuntimeStateWhenFullDetailIsCrowdedOut()
    {
        var baselineHeavy = CreatePlayerState(1, 951, "Local Heavy") with
        {
            ClassId = (byte)PlayerClass.Heavy,
            ReplicatedStates =
            [
                CreateCoreAbilityState(
                    GameplayAbilityReplicatedState.HeavyDashCooldownTicksKey,
                    SnapshotReplicatedStateValueKind.Whole),
                CreateCoreAbilityState(
                    GameplayAbilityReplicatedState.HeavyDashActiveKey,
                    SnapshotReplicatedStateValueKind.Toggle),
                CreateCoreAbilityState(
                    GameplayAbilityReplicatedState.HeavyDashVisibleKey,
                    SnapshotReplicatedStateValueKind.Toggle),
                CreateCoreAbilityState(
                    GameplayAbilityReplicatedState.HeavyDashTrailAlphaKey,
                    SnapshotReplicatedStateValueKind.Scalar),
            ],
        };
        var remoteBot = CreatePlayerState(2, 952, "Remote Bot");
        var currentHeavy = baselineHeavy with
        {
            ReplicatedStates =
            [
                CreateCoreAbilityState(
                    GameplayAbilityReplicatedState.HeavyDashCooldownTicksKey,
                    SnapshotReplicatedStateValueKind.Whole,
                    intValue: 360),
                CreateCoreAbilityState(
                    GameplayAbilityReplicatedState.HeavyDashActiveKey,
                    SnapshotReplicatedStateValueKind.Toggle,
                    boolValue: true),
                CreateCoreAbilityState(
                    GameplayAbilityReplicatedState.HeavyDashVisibleKey,
                    SnapshotReplicatedStateValueKind.Toggle,
                    boolValue: true),
                CreateCoreAbilityState(
                    GameplayAbilityReplicatedState.HeavyDashTrailAlphaKey,
                    SnapshotReplicatedStateValueKind.Scalar,
                    floatValue: 0.4f),
            ],
        };
        var baseline = CreateSnapshot(952) with
        {
            Players = [baselineHeavy, remoteBot],
        };
        var current = CreateSnapshot(953) with
        {
            Players = [currentHeavy, remoteBot],
        };
        var client = new ClientSession(
            1,
            userId: 101,
            new IPEndPoint(IPAddress.Loopback, 8190),
            "Tester",
            TimeSpan.Zero);
        var world = new SimulationWorld();
        var contributions = SnapshotContributionPlanner.BuildContributions(client, current, baseline, world);

        var result = SnapshotDeltaBudgeter.BuildBudgetedSnapshot(current, baseline, contributions, targetPayloadBytes: 260);
        var merged = SnapshotDelta.ToFullSnapshot(result.Message, baseline);

        Assert.True(result.Payload.Length <= 260);
        Assert.Empty(result.Message.Players);
        var statusState = Assert.Single(result.Message.PlayerStatusStates);
        Assert.Equal(1, statusState.Slot);
        Assert.Contains(statusState.SecondaryAmmoStates ?? [], IsHeavyDashCooldownState);
        Assert.Contains(statusState.SecondaryAmmoStates ?? [], IsHeavyDashActiveState);
        Assert.Contains(statusState.SecondaryAmmoStates ?? [], IsHeavyDashVisibleState);
        Assert.Contains(statusState.SecondaryAmmoStates ?? [], IsHeavyDashTrailAlphaState);

        var mergedHeavy = Assert.Single(merged.Players, player => player.Slot == 1);
        Assert.Contains(mergedHeavy.ReplicatedStates ?? [], IsHeavyDashCooldownState);
        Assert.Contains(mergedHeavy.ReplicatedStates ?? [], IsHeavyDashActiveState);
        Assert.Contains(mergedHeavy.ReplicatedStates ?? [], IsHeavyDashVisibleState);
        Assert.Contains(mergedHeavy.ReplicatedStates ?? [], IsHeavyDashTrailAlphaState);
    }

    [Fact]
    public void BuildBudgetedSnapshotForcesAllLocalPlayerUpdates()
    {
        var localPlayer = CreatePlayerState(1, 861, "Local Player") with
        {
            ClassId = (byte)PlayerClass.Soldier,
        };
        var correctedLocalPlayer = localPlayer with
        {
            X = localPlayer.X + 10f,
            Health = (short)Math.Max(1, localPlayer.Health - 5),
        };
        var baseline = CreateSnapshot(860) with
        {
            Players = [localPlayer],
        };
        var current = CreateSnapshot(861) with
        {
            Players = [correctedLocalPlayer],
        };
        var contributions = new List<SnapshotDeltaBudgeter.Contribution>
        {
            new(
                Priority: 1800,
                DistanceSquared: 0f,
                EstimatedBytes: 32,
                Apply: builder => builder.PlayerMovementStates.Add(CreateMovementState(correctedLocalPlayer)),
                Kind: SnapshotDeltaBudgeter.ContributionKind.LocalPlayerUpdate),
            new(
                Priority: 950,
                DistanceSquared: 0f,
                EstimatedBytes: 180,
                Apply: builder => builder.Players.Add(correctedLocalPlayer),
                Kind: SnapshotDeltaBudgeter.ContributionKind.LocalPlayerUpdate),
        };

        var result = SnapshotDeltaBudgeter.BuildBudgetedSnapshot(current, baseline, contributions);
        var merged = SnapshotDelta.ToFullSnapshot(result.Message, baseline);

        Assert.True(result.Payload.Length <= SnapshotDeltaBudgeter.TargetSnapshotPayloadBytes);
        Assert.Contains(result.Message.PlayerMovementStates, player => player.Slot == localPlayer.Slot);
        Assert.Contains(result.Message.Players, player => player.Slot == localPlayer.Slot);
        var mergedLocalPlayer = Assert.Single(merged.Players, player => player.Slot == localPlayer.Slot);
        Assert.Equal(correctedLocalPlayer.Health, mergedLocalPlayer.Health);
    }

    [Fact]
    public void BuildContributionsKeepsLocalHealthUpdateUnderBudgetPressure()
    {
        var localPlayer = CreatePlayerState(1, 871, "Local Player") with
        {
            ClassId = (byte)PlayerClass.Soldier,
        };
        var remoteBot = CreatePlayerState(2, 872, "Remote Bot");
        var baseline = CreateSnapshot(870) with
        {
            Players = [localPlayer, remoteBot],
        };
        var current = CreateSnapshot(871) with
        {
            Players =
            [
                localPlayer with { Health = (short)75 },
                remoteBot with { X = remoteBot.X + 24f },
            ],
            SoundEvents = Enumerable.Range(0, 80)
                .Select(index => new SnapshotSoundEvent(
                    $"pressure-sound-{index:D3}",
                    X: index,
                    Y: index,
                    EventId: (ulong)index,
                    SourceFrame: 871))
                .ToArray(),
        };
        var client = new ClientSession(
            1,
            userId: 101,
            new IPEndPoint(IPAddress.Loopback, 8190),
            "Tester",
            TimeSpan.Zero);
        var world = new SimulationWorld();
        var contributions = SnapshotContributionPlanner.BuildContributions(client, current, baseline, world);

        var result = SnapshotDeltaBudgeter.BuildBudgetedSnapshot(current, baseline, contributions, targetPayloadBytes: 900);
        var merged = SnapshotDelta.ToFullSnapshot(result.Message, baseline);

        Assert.True(result.Payload.Length <= 900);
        var mergedLocalPlayer = Assert.Single(merged.Players, player => player.Slot == 1);
        Assert.Equal(75, mergedLocalPlayer.Health);
    }

    [Fact]
    public void BuildBudgetedSnapshotDoesNotDropSoundsBeforeLowerPriorityVisualsWhenReducingOverage()
    {
        var baseline = CreateSnapshot(880);
        var current = CreateSnapshot(881);
        var soundEvent = new SnapshotSoundEvent("critical-reminder-sound", 128f, 96f, EventId: 1, SourceFrame: 881);
        var visualEvents = Enumerable.Range(0, 80)
            .Select(static index =>
            {
                var value = unchecked((uint)(index * 2654435761u));
                return new SnapshotVisualEvent(
                    $"visual-{value.ToString("X8", CultureInfo.InvariantCulture)}",
                    128f + index,
                    96f - index,
                    index,
                    1,
                    EventId: (ulong)(index + 2));
            })
            .ToArray();
        var soundOnlyDelta = current with
        {
            IsDelta = true,
            BaselineFrame = baseline.Frame,
            SoundEvents = [soundEvent],
        };
        var targetPayloadBytes = ProtocolCodec.Serialize(
            soundOnlyDelta,
            ProtocolCodec.MeasureSerializedSize(soundOnlyDelta),
            ServerProtocolCompression.Settings).Length + 256;
        var contributions = new List<SnapshotDeltaBudgeter.Contribution>
        {
            new(
                Priority: 850,
                DistanceSquared: 0f,
                EstimatedBytes: 1,
                Apply: builder => builder.SoundEvents.Add(soundEvent)),
            new(
                Priority: 840,
                DistanceSquared: 0f,
                EstimatedBytes: 1,
                Apply: builder =>
                {
                    for (var index = 0; index < visualEvents.Length; index += 1)
                    {
                        builder.VisualEvents.Add(visualEvents[index]);
                    }
                }),
        };

        var result = SnapshotDeltaBudgeter.BuildBudgetedSnapshot(current, baseline, contributions, targetPayloadBytes);

        Assert.True(result.Payload.Length <= targetPayloadBytes);
        Assert.Single(result.Message.SoundEvents);
        Assert.Empty(result.Message.VisualEvents);
    }

    [Fact]
    public void BuildBudgetedSnapshotPreservesPlayerChatBubbleWhenFullDetailIsCrowdedOut()
    {
        var baselinePlayer = CreatePlayerState(1, 981, "Bubbly");
        var currentPlayer = baselinePlayer with
        {
            IsChatBubbleVisible = true,
            ChatBubbleFrameIndex = 49,
            ChatBubbleAlpha = 0.85f,
        };
        var baseline = CreateSnapshot(980) with
        {
            Players = [baselinePlayer],
        };
        var current = CreateSnapshot(981) with
        {
            Players = [currentPlayer],
        };
        var client = new ClientSession(
            2,
            userId: 101,
            new IPEndPoint(IPAddress.Loopback, 8190),
            "Viewer",
            TimeSpan.Zero);
        var world = new SimulationWorld();
        var contributions = SnapshotContributionPlanner.BuildContributions(client, current, baseline, world);

        var result = SnapshotDeltaBudgeter.BuildBudgetedSnapshot(current, baseline, contributions, targetPayloadBytes: 320);
        var merged = SnapshotDelta.ToFullSnapshot(result.Message, baseline);

        Assert.True(result.Payload.Length <= 320);
        Assert.Empty(result.Message.Players);
        var bubbleState = Assert.Single(result.Message.PlayerChatBubbleStates);
        Assert.Equal(1, bubbleState.Slot);
        Assert.True(bubbleState.IsChatBubbleVisible);
        Assert.Equal(49, bubbleState.ChatBubbleFrameIndex);
        var mergedPlayer = Assert.Single(merged.Players);
        Assert.True(mergedPlayer.IsChatBubbleVisible);
        Assert.Equal(49, mergedPlayer.ChatBubbleFrameIndex);
        Assert.InRange(mergedPlayer.ChatBubbleAlpha, 0.84f, 0.86f);
    }

    [Fact]
    public void BuildBudgetedSnapshotPreservesRemoteStatusViaCompactStateWithoutFullPlayerPayload()
    {
        var localPlayer = CreatePlayerState(1, 991, "Viewer");
        var baselineRemotePlayer = CreatePlayerState(2, 992, "Target") with
        {
            Health = 125,
            Ammo = 6,
            Metal = 0f,
            IsCarryingIntel = false,
        };
        var currentRemotePlayer = baselineRemotePlayer with
        {
            Health = 58,
            Ammo = 2,
            Metal = 40f,
            IsCarryingIntel = true,
            IntelRechargeTicks = 12f,
        };
        var baseline = CreateSnapshot(990) with
        {
            Players = [localPlayer, baselineRemotePlayer],
        };
        var current = CreateSnapshot(991) with
        {
            Players = [localPlayer, currentRemotePlayer],
        };
        var client = new ClientSession(
            1,
            userId: 101,
            new IPEndPoint(IPAddress.Loopback, 8190),
            "Viewer",
            TimeSpan.Zero);
        var world = new SimulationWorld();
        var contributions = SnapshotContributionPlanner.BuildContributions(client, current, baseline, world);

        var result = SnapshotDeltaBudgeter.BuildBudgetedSnapshot(current, baseline, contributions, targetPayloadBytes: 320);
        var merged = SnapshotDelta.ToFullSnapshot(result.Message, baseline);

        Assert.True(result.Payload.Length <= 320);
        Assert.Empty(result.Message.Players);
        var statusState = Assert.Single(result.Message.PlayerStatusStates, status => status.Slot == 2);
        Assert.Equal(58, statusState.Health);
        Assert.Equal(2, statusState.Ammo);
        Assert.Equal(40f, statusState.Metal);
        Assert.True(statusState.IsCarryingIntel);
        var mergedRemotePlayer = Assert.Single(merged.Players, player => player.Slot == 2);
        Assert.Equal(58, mergedRemotePlayer.Health);
        Assert.Equal(2, mergedRemotePlayer.Ammo);
        Assert.Equal(40f, mergedRemotePlayer.Metal);
        Assert.True(mergedRemotePlayer.IsCarryingIntel);
    }

    [Fact]
    public void SnapshotDeltaStatusUpdateClearsRemovedSecondaryWeaponRuntimeState()
    {
        var baselineSoldier = CreatePlayerState(2, 1092, "Remote Soldier") with
        {
            ClassId = (byte)PlayerClass.Soldier,
            ReplicatedStates =
            [
                new SnapshotReplicatedStateEntry(
                    "core.player",
                    "soldier_shotgun_equipped",
                    SnapshotReplicatedStateValueKind.Toggle,
                    BoolValue: true),
                new SnapshotReplicatedStateEntry(
                    "core.player",
                    "soldier_shotgun_ammo",
                    SnapshotReplicatedStateValueKind.Whole,
                    IntValue: 2),
                new SnapshotReplicatedStateEntry(
                    "core.player",
                    "soldier_shotgun_reload_ticks",
                    SnapshotReplicatedStateValueKind.Whole,
                    IntValue: 18),
                new SnapshotReplicatedStateEntry(
                    "core.player",
                    "soldier_shotgun_cooldown_ticks",
                    SnapshotReplicatedStateValueKind.Whole,
                    IntValue: 6),
            ],
        };
        var baseline = CreateSnapshot(1090) with
        {
            Players = [baselineSoldier],
        };
        var delta = CreateSnapshot(1091) with
        {
            IsDelta = true,
            BaselineFrame = baseline.Frame,
            PlayerStatusStates =
            [
                new SnapshotPlayerStatusState(
                    baselineSoldier.Slot,
                    baselineSoldier.Health,
                    baselineSoldier.MaxHealth,
                    baselineSoldier.Ammo,
                    baselineSoldier.MaxAmmo,
                    baselineSoldier.Metal,
                    baselineSoldier.IsCarryingIntel,
                    baselineSoldier.IntelRechargeTicks,
                    SecondaryAmmoStates: []),
            ],
        };

        var merged = SnapshotDelta.ToFullSnapshot(delta, baseline);

        var mergedSoldier = Assert.Single(merged.Players);
        var states = mergedSoldier.ReplicatedStates ?? [];
        Assert.Contains(states, state => state.Key == "soldier_shotgun_equipped" && state.BoolValue);
        Assert.DoesNotContain(states, state => state.Key == "soldier_shotgun_ammo");
        Assert.DoesNotContain(states, state => state.Key == "soldier_shotgun_reload_ticks");
        Assert.DoesNotContain(states, state => state.Key == "soldier_shotgun_cooldown_ticks");
    }

    [Fact]
    public void BuildContributionsDoesNotSendFullPlayerForAimAndBinocularDriftBetweenDetailWindows()
    {
        var localPlayer = CreatePlayerState(1, 1121, "Viewer");
        var baselineRemotePlayer = CreatePlayerState(2, 1122, "Remote") with
        {
            AimWorldX = 128f,
            AimWorldY = 96f,
            IsUsingBinoculars = false,
            BinocularsFocusX = 128f,
            BinocularsFocusY = 96f,
        };
        var currentRemotePlayer = baselineRemotePlayer with
        {
            AimWorldX = 4096f,
            AimWorldY = 2048f,
            IsUsingBinoculars = true,
            BinocularsFocusX = 3600f,
            BinocularsFocusY = 1800f,
        };
        var baseline = CreateSnapshot(1120) with
        {
            Players = [localPlayer, baselineRemotePlayer],
        };
        var current = CreateSnapshot(1121) with
        {
            Players = [localPlayer, currentRemotePlayer],
        };
        var client = new ClientSession(
            1,
            userId: 101,
            new IPEndPoint(IPAddress.Loopback, 8190),
            "Viewer",
            TimeSpan.Zero);
        var world = new SimulationWorld();
        typeof(SimulationWorld)
            .GetProperty(nameof(SimulationWorld.Frame))!
            .SetValue(world, 1121L);

        var contributions = SnapshotContributionPlanner.BuildContributions(client, current, baseline, world);
        var result = SnapshotDeltaBudgeter.BuildBudgetedSnapshot(current, baseline, contributions, targetPayloadBytes: 320);

        Assert.Empty(result.Message.Players);
        Assert.Empty(result.Message.PlayerStatusStates);
        Assert.Empty(result.Message.PlayerExtendedStatusStates);
    }

    [Fact]
    public void BuildContributionsSendsRuntimeReplicatedStateThroughCompactStatusWithoutFullPlayer()
    {
        var localPlayer = CreatePlayerState(1, 1131, "Viewer");
        var baselineRemotePlayer = CreatePlayerState(2, 1132, "Remote Soldier") with
        {
            ClassId = (byte)PlayerClass.Soldier,
            ReplicatedStates =
            [
                new SnapshotReplicatedStateEntry(
                    "core.player",
                    "soldier_shotgun_cooldown_ticks",
                    SnapshotReplicatedStateValueKind.Whole,
                    IntValue: 6),
            ],
        };
        var currentRemotePlayer = baselineRemotePlayer with
        {
            ReplicatedStates =
            [
                new SnapshotReplicatedStateEntry(
                    "core.player",
                    "soldier_shotgun_cooldown_ticks",
                    SnapshotReplicatedStateValueKind.Whole,
                    IntValue: 5),
            ],
        };
        var baseline = CreateSnapshot(1130) with
        {
            Players = [localPlayer, baselineRemotePlayer],
        };
        var current = CreateSnapshot(1131) with
        {
            Players = [localPlayer, currentRemotePlayer],
        };
        var client = new ClientSession(
            1,
            userId: 101,
            new IPEndPoint(IPAddress.Loopback, 8190),
            "Viewer",
            TimeSpan.Zero);
        var world = new SimulationWorld();
        typeof(SimulationWorld)
            .GetProperty(nameof(SimulationWorld.Frame))!
            .SetValue(world, 1131L);

        var contributions = SnapshotContributionPlanner.BuildContributions(client, current, baseline, world);
        var result = SnapshotDeltaBudgeter.BuildBudgetedSnapshot(current, baseline, contributions, targetPayloadBytes: 512);

        Assert.Empty(result.Message.Players);
        var status = Assert.Single(result.Message.PlayerStatusStates, state => state.Slot == 2);
        var replicatedState = Assert.Single(
            status.SecondaryAmmoStates ?? Array.Empty<SnapshotReplicatedStateEntry>(),
            state => state.Key == "soldier_shotgun_cooldown_ticks");
        Assert.Equal(5, replicatedState.IntValue);
    }

    [Fact]
    public void BuildContributionsDefersLowFrequencyPlayerDetailBetweenRefreshWindows()
    {
        var localPlayer = CreatePlayerState(1, 1001, "Viewer");
        var baselineRemotePlayer = CreatePlayerState(2, 1002, "ScoreboardBot");
        var currentRemotePlayer = baselineRemotePlayer with
        {
            Kills = 3,
            Deaths = 1,
            Points = 2f,
            HealPoints = 5,
            Assists = 2,
        };
        var baseline = CreateSnapshot(1000) with
        {
            Players = [localPlayer, baselineRemotePlayer],
        };
        var current = CreateSnapshot(1001) with
        {
            Players = [localPlayer, currentRemotePlayer],
        };
        var client = new ClientSession(
            1,
            userId: 101,
            new IPEndPoint(IPAddress.Loopback, 8190),
            "Viewer",
            TimeSpan.Zero);
        var world = new SimulationWorld();
        typeof(SimulationWorld)
            .GetProperty(nameof(SimulationWorld.Frame))!
            .SetValue(world, 1018L);

        var contributions = SnapshotContributionPlanner.BuildContributions(client, current, baseline, world);

        var result = SnapshotDeltaBudgeter.BuildBudgetedSnapshot(current, baseline, contributions, targetPayloadBytes: 320);
        Assert.Empty(result.Message.Players);
        Assert.Empty(result.Message.PlayerStatusStates);
        Assert.Empty(result.Message.PlayerChatBubbleStates);
    }

    [Fact]
    public void BuildContributionsDefersBadgeAndOwnedItemProfileChangesBetweenRefreshWindows()
    {
        var baselineLocalPlayer = CreatePlayerState(1, 1051, "Viewer") with
        {
            BadgeMask = 1,
            OwnedGameplayItemIds = ["inventory.base"],
        };
        var remotePlayer = CreatePlayerState(2, 1052, "Remote");
        var currentLocalPlayer = baselineLocalPlayer with
        {
            BadgeMask = 3,
            OwnedGameplayItemIds = ["inventory.base", "inventory.unlock"],
        };
        var baseline = CreateSnapshot(1050) with
        {
            Players = [baselineLocalPlayer, remotePlayer],
        };
        var current = CreateSnapshot(1051) with
        {
            Players = [currentLocalPlayer, remotePlayer],
        };
        var client = new ClientSession(
            1,
            userId: 101,
            new IPEndPoint(IPAddress.Loopback, 8190),
            "Viewer",
            TimeSpan.Zero);
        var world = new SimulationWorld();
        typeof(SimulationWorld)
            .GetProperty(nameof(SimulationWorld.Frame))!
            .SetValue(world, 1068L);

        var contributions = SnapshotContributionPlanner.BuildContributions(client, current, baseline, world);
        var result = SnapshotDeltaBudgeter.BuildBudgetedSnapshot(current, baseline, contributions, targetPayloadBytes: 320);

        Assert.Empty(result.Message.Players);
        Assert.Empty(result.Message.PlayerStatusStates);
        Assert.Empty(result.Message.PlayerExtendedStatusStates);
        Assert.Empty(result.Message.PlayerChatBubbleStates);
    }

    [Fact]
    public void BuildBudgetedSnapshotPreservesRemoteSpyCloakTransitionAsRosterCriticalState()
    {
        var localPlayer = CreatePlayerState(1, 1101, "Viewer");
        var baselineRemoteSpy = CreatePlayerState(2, 1102, "Remote Spy") with
        {
            ClassId = (byte)PlayerClass.Spy,
            IsSpyCloaked = false,
            SpyCloakAlpha = 1f,
            SpyBackstabVisualTicksRemaining = 0,
            IsUbered = false,
        };
        var currentRemoteSpy = baselineRemoteSpy with
        {
            IsSpyCloaked = true,
            SpyCloakAlpha = 0.35f,
            SpyBackstabVisualTicksRemaining = 24,
            IsUbered = true,
        };
        var baseline = CreateSnapshot(1100) with
        {
            Players = [localPlayer, baselineRemoteSpy],
        };
        var current = CreateSnapshot(1101) with
        {
            Players = [localPlayer, currentRemoteSpy],
        };
        var client = new ClientSession(
            1,
            userId: 101,
            new IPEndPoint(IPAddress.Loopback, 8190),
            "Viewer",
            TimeSpan.Zero);
        var world = new SimulationWorld();
        var contributions = SnapshotContributionPlanner.BuildContributions(client, current, baseline, world);

        var result = SnapshotDeltaBudgeter.BuildBudgetedSnapshot(current, baseline, contributions, targetPayloadBytes: 320);
        var merged = SnapshotDelta.ToFullSnapshot(result.Message, baseline);

        Assert.True(result.Payload.Length <= 320);
        var rosterPlayer = Assert.Single(result.Message.Players, player => player.Slot == 2);
        Assert.True(rosterPlayer.IsSpyCloaked);
        Assert.InRange(rosterPlayer.SpyCloakAlpha, 0.34f, 0.36f);
        Assert.Equal(24, rosterPlayer.SpyBackstabVisualTicksRemaining);
        Assert.True(rosterPlayer.IsUbered);
        var mergedRemoteSpy = Assert.Single(merged.Players, player => player.Slot == 2);
        Assert.True(mergedRemoteSpy.IsSpyCloaked);
        Assert.InRange(mergedRemoteSpy.SpyCloakAlpha, 0.34f, 0.36f);
        Assert.Equal(24, mergedRemoteSpy.SpyBackstabVisualTicksRemaining);
        Assert.True(mergedRemoteSpy.IsUbered);
    }

    [Fact]
    public void BuildBudgetedSnapshotPreservesLocalMedicUberViaCompactExtendedStatus()
    {
        var baselineLocalMedic = CreatePlayerState(1, 1201, "Local Medic") with
        {
            ClassId = (byte)PlayerClass.Medic,
            MedicUberCharge = 0f,
            IsMedicUberReady = false,
        };
        var remoteBot = CreatePlayerState(2, 1202, "Remote Bot");
        var currentLocalMedic = baselineLocalMedic with
        {
            MedicUberCharge = 1999.75f,
            IsMedicUberReady = true,
        };
        var baseline = CreateSnapshot(1200) with
        {
            Players = [baselineLocalMedic, remoteBot],
        };
        var current = CreateSnapshot(1201) with
        {
            Players = [currentLocalMedic, remoteBot],
        };
        var client = new ClientSession(
            1,
            userId: 101,
            new IPEndPoint(IPAddress.Loopback, 8190),
            "Local Medic",
            TimeSpan.Zero);
        var world = new SimulationWorld();
        var contributions = SnapshotContributionPlanner.BuildContributions(client, current, baseline, world);

        var result = SnapshotDeltaBudgeter.BuildBudgetedSnapshot(current, baseline, contributions, targetPayloadBytes: 320);
        var merged = SnapshotDelta.ToFullSnapshot(result.Message, baseline);

        Assert.True(result.Payload.Length <= 320);
        Assert.Empty(result.Message.Players);
        var extendedState = Assert.Single(result.Message.PlayerExtendedStatusStates, status => status.Slot == 1);
        Assert.InRange(extendedState.MedicUberCharge, 1999.5f, 2000f);
        Assert.True(extendedState.IsMedicUberReady);
        var mergedLocalMedic = Assert.Single(merged.Players, player => player.Slot == 1);
        Assert.InRange(mergedLocalMedic.MedicUberCharge, 1999.5f, 2000f);
        Assert.True(mergedLocalMedic.IsMedicUberReady);
    }

    [Fact]
    public void BuildBudgetedSnapshotReductionPreservesGameplayEquipmentForRemoteWeaponPresentation()
    {
        var baselineSoldier = CreatePlayerState(2, 902, "Remote Soldier") with
        {
            ClassId = (byte)PlayerClass.Soldier,
            GameplayModPackId = "stock.gg2",
            GameplayLoadoutId = "soldier.stock",
            GameplayPrimaryItemId = "weapon.rocketlauncher",
            GameplaySecondaryItemId = "weapon.soldier-shotgun",
            GameplayUtilityItemId = "ability.soldier-utility",
            GameplayEquippedSlot = (byte)GameplayEquipmentSlot.Primary,
            GameplayEquippedItemId = "weapon.rocketlauncher",
        };
        var currentSoldier = baselineSoldier with
        {
            Ammo = 5,
            AimDirectionDegrees = 35f,
            GameplayEquippedSlot = (byte)GameplayEquipmentSlot.Secondary,
            GameplayEquippedItemId = "weapon.soldier-shotgun",
            OwnedGameplayItemIds =
            [
                "inventory.remote-soldier-primary",
                "inventory.remote-soldier-secondary",
                "inventory.remote-soldier-utility",
            ],
            ReplicatedStates =
            [
                new SnapshotReplicatedStateEntry(
                    "core.player",
                    "soldier_shotgun_equipped",
                    SnapshotReplicatedStateValueKind.Toggle,
                    BoolValue: true),
            ],
        };
        var baseline = CreateSnapshot(900) with
        {
            Players = [baselineSoldier],
        };
        var current = CreateSnapshot(901);
        var fullDelta = current with
        {
            IsDelta = true,
            BaselineFrame = baseline.Frame,
            Players = [currentSoldier],
        };
        var contributions = new List<SnapshotDeltaBudgeter.Contribution>
        {
            new(
                Priority: 1900,
                DistanceSquared: 1f,
                EstimatedBytes: ProtocolCodec.MeasureSerializedSize(fullDelta),
                Apply: builder => builder.Players.Add(currentSoldier),
                Kind: SnapshotDeltaBudgeter.ContributionKind.PlayerRosterUpdate),
        };
        var targetPayloadBytes = ProtocolCodec.MeasureSerializedSize(fullDelta) - 16;

        var result = SnapshotDeltaBudgeter.BuildBudgetedSnapshot(
            current,
            baseline,
            contributions,
            targetPayloadBytes);
        var merged = SnapshotDelta.ToFullSnapshot(result.Message, baseline);

        Assert.True(result.Payload.Length <= targetPayloadBytes);
        var deltaPlayer = Assert.Single(result.Message.Players);
        Assert.Equal("weapon.soldier-shotgun", deltaPlayer.GameplayEquippedItemId);
        Assert.Equal((byte)GameplayEquipmentSlot.Secondary, deltaPlayer.GameplayEquippedSlot);
        Assert.Equal("weapon.soldier-shotgun", deltaPlayer.GameplaySecondaryItemId);
        Assert.Contains(
            deltaPlayer.ReplicatedStates ?? [],
            state => state.OwnerId == "core.player"
                && state.Key == "soldier_shotgun_equipped"
                && state.BoolValue);
        var mergedSoldier = Assert.Single(merged.Players);
        Assert.Equal("weapon.soldier-shotgun", mergedSoldier.GameplayEquippedItemId);
        Assert.Equal((byte)GameplayEquipmentSlot.Secondary, mergedSoldier.GameplayEquippedSlot);
    }

    [Fact]
    public void BuildBudgetedSnapshotReductionPreservesPluginAbilityCooldownHudState()
    {
        var baselineHeavy = CreatePlayerState(2, 912, "Remote Heavy") with
        {
            ClassId = (byte)PlayerClass.Heavy,
            GameplayModPackId = "stock.gg2",
            GameplayLoadoutId = "heavy.stock",
            GameplayPrimaryItemId = "weapon.minigun",
            GameplaySecondaryItemId = "ability.heavy-sandvich",
            GameplayUtilityItemId = "ability.sample-force-dash",
            GameplayEquippedItemId = "weapon.minigun",
        };
        var currentHeavy = baselineHeavy with
        {
            OwnedGameplayItemIds =
            [
                "inventory.remote-heavy-primary",
                "inventory.remote-heavy-secondary",
                "inventory.remote-heavy-utility",
                "inventory.remote-heavy-cosmetic",
            ],
            ReplicatedStates =
            [
                new SnapshotReplicatedStateEntry(
                    "sample.server.lua-gameplay-ability",
                    "sample_force_dash_cooldown",
                    SnapshotReplicatedStateValueKind.Whole,
                    IntValue: 42),
                new SnapshotReplicatedStateEntry(
                    "sample.server.lua-gameplay-ability",
                    "debug_score",
                    SnapshotReplicatedStateValueKind.Whole,
                    IntValue: 99),
            ],
        };
        var method = typeof(SnapshotDeltaBudgeter).GetMethod(
            "ReducePlayerStateForBudget",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var deltaPlayer = (SnapshotPlayerState)method.Invoke(null, new object[] { currentHeavy })!;
        Assert.Contains(
            deltaPlayer.ReplicatedStates ?? [],
            state => state.OwnerId == "sample.server.lua-gameplay-ability"
                && state.Key == "sample_force_dash_cooldown"
                && state.IntValue == 42);
        Assert.DoesNotContain(
            deltaPlayer.ReplicatedStates ?? [],
            state => state.OwnerId == "sample.server.lua-gameplay-ability"
                && state.Key == "debug_score");
    }

    [Fact]
    public void BuildBudgetedFirstSnapshotKeepsPlayableLocalPlayerWithinUdpBudget()
    {
        var currentPlayers = Enumerable.Range(0, 20)
            .Select(index => CreatePlayerState((byte)(index + 1), 700 + index, $"Initial Player {index:D2}"))
            .ToArray();
        var current = CreateSnapshot(350) with
        {
            Players = currentPlayers,
        };
        var client = new ClientSession(
            1,
            userId: 101,
            new IPEndPoint(IPAddress.Loopback, 8190),
            "Tester",
            TimeSpan.Zero);
        var world = new SimulationWorld();
        var contributions = SnapshotContributionPlanner.BuildContributions(client, current, baseline: null, world);

        var result = SnapshotDeltaBudgeter.BuildBudgetedSnapshot(current, baseline: null, contributions);
        var merged = SnapshotDelta.ToFullSnapshot(result.Message);

        Assert.True(result.Payload.Length <= SnapshotDeltaBudgeter.TargetSnapshotPayloadBytes);
        Assert.True(result.Message.IsDelta);
        Assert.Equal((ulong)0, result.Message.BaselineFrame);
        Assert.Contains(merged.Players, player => player.Slot == 1);
    }

    [Fact]
    public void BuildContributionsMarksNewDeadBodiesAsRequiredEntityAppearances()
    {
        var deadBody = new SnapshotDeadBodyState(
            Id: 701,
            SourcePlayerId: 202,
            Team: (byte)PlayerTeam.Blue,
            ClassId: (byte)PlayerClass.Soldier,
            AnimationKind: (byte)DeadBodyAnimationKind.Default,
            X: 128f,
            Y: 96f,
            Width: 24f,
            Height: 48f,
            HorizontalSpeed: 0f,
            VerticalSpeed: 0f,
            FacingLeft: false,
            TicksRemaining: 90);
        var current = CreateSnapshot(360) with
        {
            DeadBodies = [deadBody],
        };
        var baseline = SnapshotBaselineState.FromSnapshot(CreateSnapshot(359));
        var client = new ClientSession(
            1,
            userId: 101,
            new IPEndPoint(IPAddress.Loopback, 8190),
            "Tester",
            TimeSpan.Zero);
        var world = new SimulationWorld();

        var contributions = SnapshotContributionPlanner.BuildContributions(client, current, baseline, world);

        var contribution = Assert.Single(
            contributions,
            static entry => entry.Kind == SnapshotDeltaBudgeter.ContributionKind.EntityFirstAppearance);
        var builder = new SnapshotDeltaBudgeter.Builder(current, baseline.Frame, seedFromTemplateCollections: false);
        contribution.Apply(builder);
        var snapshot = builder.Build();
        var includedDeadBody = Assert.Single(snapshot.DeadBodies);
        Assert.Equal(deadBody.Id, includedDeadBody.Id);
    }

    [Fact]
    public void BuildContributionsMarksKnownDeadBodyUpdatesAsRequiredEntityStateUpdates()
    {
        var baselineDeadBody = new SnapshotDeadBodyState(
            Id: 702,
            SourcePlayerId: 203,
            Team: (byte)PlayerTeam.Red,
            ClassId: (byte)PlayerClass.Heavy,
            AnimationKind: (byte)DeadBodyAnimationKind.Default,
            X: 128f,
            Y: 96f,
            Width: 28f,
            Height: 50f,
            HorizontalSpeed: 0f,
            VerticalSpeed: 0f,
            FacingLeft: false,
            TicksRemaining: 300);
        var updatedDeadBody = baselineDeadBody with
        {
            Y = 112f,
            VerticalSpeed = 4f,
            TicksRemaining = 284,
        };
        var baseline = SnapshotBaselineState.FromSnapshot(CreateSnapshot(369) with
        {
            DeadBodies = [baselineDeadBody],
        });
        var current = CreateSnapshot(370) with
        {
            DeadBodies = [updatedDeadBody],
        };
        var client = new ClientSession(
            1,
            userId: 101,
            new IPEndPoint(IPAddress.Loopback, 8190),
            "Tester",
            TimeSpan.Zero);
        var world = new SimulationWorld();

        var contributions = SnapshotContributionPlanner.BuildContributions(client, current, baseline, world);

        var contribution = Assert.Single(
            contributions,
            static entry => entry.Kind == SnapshotDeltaBudgeter.ContributionKind.EntityStateUpdate);
        var builder = new SnapshotDeltaBudgeter.Builder(current, baseline.Frame, seedFromTemplateCollections: false);
        contribution.Apply(builder);
        var snapshot = builder.Build();
        var includedDeadBody = Assert.Single(snapshot.DeadBodies);
        Assert.Equal(updatedDeadBody.Id, includedDeadBody.Id);
        Assert.Equal(updatedDeadBody.Y, includedDeadBody.Y);
        Assert.Equal(updatedDeadBody.TicksRemaining, includedDeadBody.TicksRemaining);
    }

    [Fact]
    public void BuildBudgetedSnapshotPrioritizesUndeliveredPlayersOverKnownMovementUpdates()
    {
        var knownPlayers = Enumerable.Range(0, 4)
            .Select(index => CreatePlayerState((byte)(index + 1), 800 + index, $"Known Player {index:D2}") with
            {
                X = 16f + index,
                Y = 16f + index,
            })
            .ToArray();
        var newPlayers = Enumerable.Range(0, 8)
            .Select(index => CreatePlayerState((byte)(index + 5), 900 + index, $"New Player {index:D2}") with
            {
                X = 4000f + index,
                Y = 4000f + index,
            })
            .ToArray();
        var baseline = SnapshotBaselineState.FromSnapshot(CreateSnapshot(500) with
        {
            Players = knownPlayers,
        });
        var client = new ClientSession(
            1,
            userId: 101,
            new IPEndPoint(IPAddress.Loopback, 8190),
            "Tester",
            TimeSpan.Zero);
        var world = new SimulationWorld();

        for (var frameOffset = 1; frameOffset <= 8; frameOffset += 1)
        {
            var movedKnownPlayers = knownPlayers
                .Select(player => player with
                {
                    X = player.X + frameOffset,
                    AimDirectionDegrees = player.AimDirectionDegrees + frameOffset,
                })
                .ToArray();
            var currentPlayers = movedKnownPlayers.Concat(newPlayers).ToArray();
            var current = CreateSnapshot((ulong)(500 + frameOffset)) with
            {
                Players = currentPlayers,
            };
            var contributions = SnapshotContributionPlanner.BuildContributions(client, current, baseline, world);

            var result = SnapshotDeltaBudgeter.BuildBudgetedSnapshot(
                current,
                baseline,
                contributions,
                targetPayloadBytes: 4 * 1024);
            var merged = SnapshotDelta.ToFullSnapshot(result.Message, baseline);
            baseline = SnapshotBaselineState.FromSnapshot(merged);
        }

        var deliveredSlots = baseline.Players.Select(player => player.Slot).ToHashSet();
        for (var slot = 1; slot <= 12; slot += 1)
        {
            Assert.Contains((byte)slot, deliveredSlots);
        }
    }

    [Fact]
    public void SnapshotDeltaMergesPlayerUpdatesAndRemovals()
    {
        var baselinePlayers = new[]
        {
            CreatePlayerState(1, 501, "Alpha"),
            CreatePlayerState(2, 502, "Bravo"),
        };
        var current = CreateSnapshot(401) with
        {
            IsDelta = true,
            BaselineFrame = 400,
            Players = [baselinePlayers[0] with { X = baselinePlayers[0].X + 32f }],
            RemovedPlayerIds = [2],
        };
        var baseline = CreateSnapshot(400) with
        {
            Players = baselinePlayers,
        };

        var merged = SnapshotDelta.ToFullSnapshot(current, baseline);

        var player = Assert.Single(merged.Players);
        Assert.Equal(1, player.Slot);
        Assert.Equal(baselinePlayers[0].X + 32f, player.X);
        Assert.Empty(merged.RemovedPlayerIds);
    }

    [Fact]
    public void SnapshotDeltaPreservesProjectileRemovalsAfterMerge()
    {
        var shot = new SnapshotShotState(
            Id: 701,
            Team: 1,
            OwnerId: 501,
            X: 128f,
            Y: 64f,
            VelocityX: 16f,
            VelocityY: 0f,
            TicksRemaining: 18);
        var rocket = new SnapshotRocketState(
            Id: 702,
            Team: 1,
            OwnerId: 501,
            X: 160f,
            Y: 64f,
            PreviousX: 152f,
            PreviousY: 64f,
            DirectionRadians: 0f,
            Speed: 240f,
            TicksRemaining: 40);
        var completeness =
            SnapshotEntityCollectionCompletenessFlags.Shots | SnapshotEntityCollectionCompletenessFlags.Rockets;
        var baseline = CreateSnapshot(410) with
        {
            Shots = [shot],
            Rockets = [rocket],
        };
        var delta = CreateSnapshot(411) with
        {
            IsDelta = true,
            BaselineFrame = baseline.Frame,
            RemovedShotIds = [shot.Id],
            RemovedRocketIds = [rocket.Id],
            EntityCollectionCompletenessFlags = completeness,
        };

        var merged = SnapshotDelta.ToFullSnapshot(delta, baseline);

        Assert.Empty(merged.Shots);
        Assert.Empty(merged.Rockets);
        Assert.Equal(new[] { shot.Id }, merged.RemovedShotIds);
        Assert.Equal(new[] { rocket.Id }, merged.RemovedRocketIds);
        Assert.Equal(completeness, merged.EntityCollectionCompletenessFlags);
    }

    [Fact]
    public void SnapshotDeltaMergesPlayerMovementDeltasIntoBaselinePlayers()
    {
        var baselinePlayers = new[]
        {
            CreatePlayerState(1, 551, "Alpha"),
            CreatePlayerState(2, 552, "Bravo"),
        };
        var movedPlayer = baselinePlayers[1] with
        {
            X = baselinePlayers[1].X + 48f,
            HorizontalSpeed = 10f,
            AimDirectionDegrees = 35f,
        };
        var current = CreateSnapshot(451) with
        {
            IsDelta = true,
            BaselineFrame = 450,
            PlayerMovementStates = [CreateMovementState(movedPlayer)],
        };
        var baseline = CreateSnapshot(450) with
        {
            Players = baselinePlayers,
        };

        var merged = SnapshotDelta.ToFullSnapshot(current, baseline);

        Assert.Empty(merged.PlayerMovementStates);
        var unchanged = Assert.Single(merged.Players, player => player.Slot == 1);
        Assert.Equal(baselinePlayers[0].X, unchanged.X);
        var moved = Assert.Single(merged.Players, player => player.Slot == 2);
        Assert.Equal(movedPlayer.X, moved.X);
        Assert.Equal(movedPlayer.HorizontalSpeed, moved.HorizontalSpeed);
        Assert.Equal(movedPlayer.AimDirectionDegrees, moved.AimDirectionDegrees);
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

    private static int MeasurePayloadLength(SnapshotMessage snapshot)
    {
        return ProtocolCodec.Serialize(
            snapshot,
            ProtocolCodec.MeasureSerializedSize(snapshot),
            ServerProtocolCompression.Settings).Length;
    }

    private static List<WorldGibSpawnEvent> GetPendingGibSpawnEvents(SimulationWorld world)
    {
        var field = typeof(SimulationWorld).GetField("_pendingGibSpawnEvents", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<List<WorldGibSpawnEvent>>(field!.GetValue(world));
    }

    private static List<WorldSoundEvent> GetPendingSoundEvents(SimulationWorld world)
    {
        var field = typeof(SimulationWorld).GetField("_pendingSoundEvents", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<List<WorldSoundEvent>>(field!.GetValue(world));
    }

    private static PlayerEntity AddNetworkPlayer(SimulationWorld world, byte slot, PlayerClass playerClass, PlayerTeam team = PlayerTeam.Red)
    {
        Assert.True(world.TryPrepareNetworkPlayerJoin(slot));
        Assert.True(world.TrySetNetworkPlayerTeam(slot, team));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(slot, playerClass));
        Assert.True(world.TryGetNetworkPlayer(slot, out var player));
        return player;
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

    private static SnapshotPlayerMovementState CreateMovementState(SnapshotPlayerState player)
    {
        return new SnapshotPlayerMovementState(
            player.Slot,
            player.X,
            player.Y,
            player.HorizontalSpeed,
            player.VerticalSpeed,
            player.IsGrounded,
            player.RemainingAirJumps,
            player.FacingDirectionX,
            player.AimDirectionDegrees,
            player.MovementState,
            player.IsTaunting,
            player.BurnIntensity);
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

    private static bool IsHeavyDashCooldownState(SnapshotReplicatedStateEntry state)
    {
        return state.OwnerId == GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId
            && state.Key == GameplayAbilityReplicatedState.HeavyDashCooldownTicksKey
            && state.Kind == SnapshotReplicatedStateValueKind.Whole
            && state.IntValue == 360;
    }

    private static bool IsHeavyDashActiveState(SnapshotReplicatedStateEntry state)
    {
        return state.OwnerId == GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId
            && state.Key == GameplayAbilityReplicatedState.HeavyDashActiveKey
            && state.Kind == SnapshotReplicatedStateValueKind.Toggle
            && state.BoolValue;
    }

    private static bool IsHeavyDashVisibleState(SnapshotReplicatedStateEntry state)
    {
        return state.OwnerId == GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId
            && state.Key == GameplayAbilityReplicatedState.HeavyDashVisibleKey
            && state.Kind == SnapshotReplicatedStateValueKind.Toggle
            && state.BoolValue;
    }

    private static bool IsHeavyDashTrailAlphaState(SnapshotReplicatedStateEntry state)
    {
        return state.OwnerId == GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId
            && state.Key == GameplayAbilityReplicatedState.HeavyDashTrailAlphaKey
            && state.Kind == SnapshotReplicatedStateValueKind.Scalar
            && MathF.Abs(state.FloatValue - 0.4f) <= 0.0001f;
    }
}
