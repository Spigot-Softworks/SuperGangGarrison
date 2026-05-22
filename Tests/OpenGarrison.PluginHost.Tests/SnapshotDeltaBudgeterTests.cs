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
    public void BuildBudgetedSnapshotPreservesRemoteExtendedStatusViaCompactStateWithoutFullPlayerPayload()
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
        Assert.Empty(result.Message.Players);
        var extendedState = Assert.Single(result.Message.PlayerExtendedStatusStates, status => status.Slot == 2);
        Assert.True(extendedState.IsSpyCloaked);
        Assert.InRange(extendedState.SpyCloakAlpha, 0.34f, 0.36f);
        Assert.Equal(24, extendedState.SpyBackstabVisualTicksRemaining);
        Assert.True(extendedState.IsUbered);
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

    private static List<WorldGibSpawnEvent> GetPendingGibSpawnEvents(SimulationWorld world)
    {
        var field = typeof(SimulationWorld).GetField("_pendingGibSpawnEvents", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<List<WorldGibSpawnEvent>>(field!.GetValue(world));
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
            player.TauntFrameIndex,
            player.BurnIntensity);
    }
}
