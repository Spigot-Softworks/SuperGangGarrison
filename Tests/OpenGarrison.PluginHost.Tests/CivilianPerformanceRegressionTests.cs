using System.Diagnostics;
using System.Net;
using OpenGarrison.Core;
using OpenGarrison.Core.BotBrain;
using OpenGarrison.Protocol;
using OpenGarrison.Server;
using Xunit;
using Xunit.Abstractions;

namespace OpenGarrison.PluginHost.Tests;

public sealed class CivilianPerformanceRegressionTests
{
    private const int ClientCount = 4;
    private const int TotalPlayerCount = 20;
    private const int WarmupTicks = 30;
    private const int MeasurementTicks = 180;

    private readonly ITestOutputHelper _output;

    public CivilianPerformanceRegressionTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void CivilianRosterSnapshotAndSimulationCostStaysNearBaseline()
    {
        var scoutWalking = MeasureRoster(PlayerClass.Scout, ScenarioInputMode.Walking);
        var civilianIdle = MeasureRoster(PlayerClass.Quote, ScenarioInputMode.Idle);
        var civilianWalking = MeasureRoster(PlayerClass.Quote, ScenarioInputMode.Walking);
        var civilianUmbrella = MeasureRoster(PlayerClass.Quote, ScenarioInputMode.UmbrellaHeld);
        var civilianTaunt = MeasureRoster(PlayerClass.Quote, ScenarioInputMode.Taunt);

        WriteResult(scoutWalking);
        WriteResult(civilianIdle);
        WriteResult(civilianWalking);
        WriteResult(civilianUmbrella);
        WriteResult(civilianTaunt);

        AssertWithinReasonableMultiplier(scoutWalking, civilianIdle, nameof(civilianIdle));
        AssertWithinReasonableMultiplier(scoutWalking, civilianWalking, nameof(civilianWalking));
        AssertWithinReasonableMultiplier(scoutWalking, civilianUmbrella, nameof(civilianUmbrella));
        AssertWithinReasonableMultiplier(scoutWalking, civilianTaunt, nameof(civilianTaunt));
    }

    [Fact]
    public void CivilianHeldUmbrellaRuntimeEventsStaySummaryOnly()
    {
        var world = new SimulationWorld();
        for (byte slot = 1; slot <= TotalPlayerCount; slot += 1)
        {
            JoinNetworkPlayer(world, slot, PlayerClass.Quote, $"Civilian {slot}");
        }

        var events = new List<PublishedEvent>();
        var reporter = new ServerRuntimeEventReporter(
            world,
            () => null,
            (eventName, fields) => events.Add(new PublishedEvent(
                eventName,
                fields.ToDictionary(static field => field.Key, static field => field.Value))),
            new ServerMapMetadataResolver(world));
        reporter.ResetObservedGameplayState();

        var emptyTransientEvents = new SnapshotTransientEvents(
            Array.Empty<SnapshotVisualEvent>(),
            Array.Empty<SnapshotDamageEvent>(),
            Array.Empty<SnapshotSoundEvent>(),
            Array.Empty<SnapshotGibSpawnEvent>(),
            Array.Empty<SnapshotRocketSpawnEvent>());
        var ticks = world.Config.TicksPerSecond * 6;
        for (var tick = 0; tick < ticks; tick += 1)
        {
            ApplyRosterInput(world, ScenarioInputMode.UmbrellaHeld, tick);
            world.AdvanceOneTick();
            reporter.PublishGameplayEvents(emptyTransientEvents);
        }

        Assert.Equal(0, events.Count(static e => e.Name == "gameplay_ability_used"));

        var usedSummary = Assert.Single(events, static e => e.Name == "gameplay_ability_used_summary");
        Assert.True(
            GetIntField(usedSummary, "suppressed_events") >= TotalPlayerCount,
            "held umbrella ability events should be counted in the summary instead of published as detailed events");

        var stateSummary = Assert.Single(events, static e => e.Name == "gameplay_ability_state_summary");
        var suppressedStates = GetIntField(stateSummary, "suppressed_states");
        var detailedStateChanges = events.Count(static e => e.Name == "gameplay_ability_state_changed");
        Assert.True(
            suppressedStates >= TotalPlayerCount,
            "umbrella cooldown meter changes should be counted in the summary instead of published every tick");
        Assert.True(
            detailedStateChanges < suppressedStates,
            $"expected summary-only cooldown changes to dominate detailed state changes, detailed={detailedStateChanges} suppressed={suppressedStates}");
    }

    private static ScenarioResult MeasureRoster(PlayerClass playerClass, ScenarioInputMode inputMode)
    {
        var world = new SimulationWorld();
        var config = new SimulationConfig();
        var clients = new Dictionary<byte, ClientSession>();
        for (byte slot = 1; slot <= ClientCount; slot += 1)
        {
            var client = new ClientSession(
                slot,
                userId: 1000 + slot,
                new IPEndPoint(IPAddress.Loopback, 8200 + slot),
                $"Client {slot}",
                TimeSpan.Zero);
            clients[slot] = client;
            JoinNetworkPlayer(world, slot, playerClass, $"Client {slot}");
        }

        var botManager = new ServerBotManager(
            world,
            config,
            new BotBrainPracticeBotController(),
            slot => !clients.ContainsKey(slot));
        for (byte slot = ClientCount + 1; slot <= TotalPlayerCount; slot += 1)
        {
            var team = GetTeamForSlot(slot);
            Assert.True(botManager.TryAddBot(slot, team, playerClass, $"Bot {slot}"));
        }

        var sentSnapshots = new List<(SnapshotMessage Message, byte[] Payload)>();
        var broadcaster = new SnapshotBroadcaster(
            world,
            config,
            clients,
            maxPlayableClients: 24,
            botManager,
            transientEventReplayTicks: (ulong)(config.TicksPerSecond * 2),
            new ServerMapMetadataResolver(world),
            (_, message, payload) => sentSnapshots.Add((message, payload)));

        for (var tick = 0; tick < WarmupTicks; tick += 1)
        {
            ApplyRosterInput(world, inputMode, tick);
            world.AdvanceOneTick();
            broadcaster.BroadcastSnapshot();
            AcknowledgeSnapshots(clients, sentSnapshots);
            sentSnapshots.Clear();
        }

        var simulationTicks = 0L;
        var maxSimulationTicks = 0L;
        var totalSnapshotMilliseconds = 0d;
        var maxSnapshotMilliseconds = 0d;
        var totalSharedCaptureMilliseconds = 0d;
        var totalPerClientMilliseconds = 0d;
        long totalAllocatedBytes = 0;
        var totalSentPayloadBytes = 0;
        var totalFullPayloadBytes = 0;
        var totalPlayerStatusBytes = 0;
        var totalPlayerExtendedStatusBytes = 0;
        var totalEventBytes = 0;
        var totalVisualEvents = 0;
        var maxVisualEvents = 0;
        var maxSentPayloadBytes = 0;

        for (var tick = 0; tick < MeasurementTicks; tick += 1)
        {
            ApplyRosterInput(world, inputMode, tick + WarmupTicks);
            var simulationStart = Stopwatch.GetTimestamp();
            world.AdvanceOneTick();
            var elapsedSimulationTicks = Stopwatch.GetTimestamp() - simulationStart;
            simulationTicks += elapsedSimulationTicks;
            maxSimulationTicks = Math.Max(maxSimulationTicks, elapsedSimulationTicks);

            broadcaster.BroadcastSnapshot();
            var metrics = broadcaster.Metrics;
            Assert.True(metrics.HasMeasurements);
            Assert.Equal(ClientCount, sentSnapshots.Count);

            totalSnapshotMilliseconds += metrics.TotalMilliseconds;
            maxSnapshotMilliseconds = Math.Max(maxSnapshotMilliseconds, metrics.TotalMilliseconds);
            totalSharedCaptureMilliseconds += metrics.SharedCaptureMilliseconds;
            totalPerClientMilliseconds += metrics.PerClientMilliseconds;
            totalAllocatedBytes += metrics.TotalAllocatedBytes;
            totalSentPayloadBytes += metrics.AverageSentPayloadBytes;
            totalFullPayloadBytes += metrics.AverageFullPayloadBytes;
            totalPlayerStatusBytes += metrics.AverageSnapshotPlayerStatusBytes;
            totalPlayerExtendedStatusBytes += metrics.AverageSnapshotPlayerExtendedStatusBytes;
            totalEventBytes += metrics.AverageSnapshotEventBytes;
            totalVisualEvents += broadcaster.LastCapturedTransientEvents.VisualEvents.Length;
            maxVisualEvents = Math.Max(maxVisualEvents, broadcaster.LastCapturedTransientEvents.VisualEvents.Length);
            maxSentPayloadBytes = Math.Max(maxSentPayloadBytes, metrics.MaxSentPayloadBytes);

            AcknowledgeSnapshots(clients, sentSnapshots);
            sentSnapshots.Clear();
        }

        return new ScenarioResult(
            playerClass,
            inputMode,
            AverageSimulationMilliseconds: TicksToMilliseconds(simulationTicks) / MeasurementTicks,
            MaxSimulationMilliseconds: TicksToMilliseconds(maxSimulationTicks),
            AverageSnapshotMilliseconds: totalSnapshotMilliseconds / MeasurementTicks,
            MaxSnapshotMilliseconds: maxSnapshotMilliseconds,
            AverageSharedCaptureMilliseconds: totalSharedCaptureMilliseconds / MeasurementTicks,
            AveragePerClientMilliseconds: totalPerClientMilliseconds / MeasurementTicks,
            AverageAllocatedBytes: totalAllocatedBytes / MeasurementTicks,
            AverageSentPayloadBytes: totalSentPayloadBytes / MeasurementTicks,
            AverageFullPayloadBytes: totalFullPayloadBytes / MeasurementTicks,
            AveragePlayerStatusBytes: totalPlayerStatusBytes / MeasurementTicks,
            AveragePlayerExtendedStatusBytes: totalPlayerExtendedStatusBytes / MeasurementTicks,
            AverageEventBytes: totalEventBytes / MeasurementTicks,
            AverageVisualEventCount: (double)totalVisualEvents / MeasurementTicks,
            MaxVisualEventCount: maxVisualEvents,
            MaxSentPayloadBytes: maxSentPayloadBytes);
    }

    private static void JoinNetworkPlayer(SimulationWorld world, byte slot, PlayerClass playerClass, string name)
    {
        Assert.True(world.TryPrepareNetworkPlayerJoin(slot));
        Assert.True(world.TrySetNetworkPlayerName(slot, name));
        Assert.True(world.TrySetNetworkPlayerTeam(slot, GetTeamForSlot(slot)));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(slot, playerClass));
    }

    private static void ApplyRosterInput(SimulationWorld world, ScenarioInputMode inputMode, int tick)
    {
        for (byte slot = 1; slot <= TotalPlayerCount; slot += 1)
        {
            if (!world.TryGetNetworkPlayer(slot, out var player))
            {
                continue;
            }

            var moveRight = inputMode is ScenarioInputMode.Walking or ScenarioInputMode.UmbrellaHeld
                && ((slot + tick / 90) & 1) == 0;
            var moveLeft = inputMode is ScenarioInputMode.Walking or ScenarioInputMode.UmbrellaHeld
                && !moveRight;
            var useUmbrella = inputMode == ScenarioInputMode.UmbrellaHeld;
            var taunt = inputMode == ScenarioInputMode.Taunt && tick % 45 == 0;
            var input = new PlayerInputSnapshot(
                Left: moveLeft,
                Right: moveRight,
                Up: false,
                Down: false,
                BuildSentry: false,
                DestroySentry: false,
                Taunt: taunt,
                FirePrimary: false,
                FireSecondary: useUmbrella,
                AimWorldX: player.X + player.FacingDirectionX * 160f,
                AimWorldY: player.Y - 20f,
                DebugKill: false,
                UseAbility: taunt);
            Assert.True(world.TrySetNetworkPlayerInput(slot, input));
        }
    }

    private static void AcknowledgeSnapshots(
        IReadOnlyDictionary<byte, ClientSession> clients,
        IReadOnlyList<(SnapshotMessage Message, byte[] Payload)> sentSnapshots)
    {
        if (sentSnapshots.Count == 0)
        {
            return;
        }

        var frame = sentSnapshots[^1].Message.Frame;
        foreach (var client in clients.Values)
        {
            client.AcknowledgeSnapshot(frame);
        }
    }

    private static PlayerTeam GetTeamForSlot(byte slot)
    {
        return slot % 2 == 0 ? PlayerTeam.Blue : PlayerTeam.Red;
    }

    private void WriteResult(ScenarioResult result)
    {
        _output.WriteLine(
            $"{result.PlayerClass}/{result.InputMode}: sim avg={result.AverageSimulationMilliseconds:F4}ms max={result.MaxSimulationMilliseconds:F4}ms; " +
            $"snapshot avg={result.AverageSnapshotMilliseconds:F4}ms max={result.MaxSnapshotMilliseconds:F4}ms shared={result.AverageSharedCaptureMilliseconds:F4}ms perClient={result.AveragePerClientMilliseconds:F4}ms; " +
            $"alloc={result.AverageAllocatedBytes}B sent={result.AverageSentPayloadBytes}B full={result.AverageFullPayloadBytes}B status={result.AveragePlayerStatusBytes}B ext={result.AveragePlayerExtendedStatusBytes}B events={result.AverageEventBytes}B; " +
            $"visual avg={result.AverageVisualEventCount:F2} max={result.MaxVisualEventCount} maxSent={result.MaxSentPayloadBytes}B");
    }

    private static void AssertWithinReasonableMultiplier(
        ScenarioResult baseline,
        ScenarioResult candidate,
        string candidateName)
    {
        var simulationLimit = Math.Max(2d, baseline.AverageSimulationMilliseconds * 8d);
        var snapshotLimit = Math.Max(8d, baseline.AverageSnapshotMilliseconds * 8d);
        Assert.True(
            candidate.AverageSimulationMilliseconds <= simulationLimit,
            $"{candidateName} simulation avg {candidate.AverageSimulationMilliseconds:F4}ms exceeded baseline {baseline.AverageSimulationMilliseconds:F4}ms limit {simulationLimit:F4}ms.");
        Assert.True(
            candidate.AverageSnapshotMilliseconds <= snapshotLimit,
            $"{candidateName} snapshot avg {candidate.AverageSnapshotMilliseconds:F4}ms exceeded baseline {baseline.AverageSnapshotMilliseconds:F4}ms limit {snapshotLimit:F4}ms.");
    }

    private static double TicksToMilliseconds(long ticks)
    {
        return ticks * 1000d / Stopwatch.Frequency;
    }

    private static int GetIntField(PublishedEvent publishedEvent, string fieldName)
    {
        Assert.True(publishedEvent.Fields.TryGetValue(fieldName, out var value), $"missing event field {fieldName}");
        return Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private enum ScenarioInputMode
    {
        Idle,
        Walking,
        UmbrellaHeld,
        Taunt,
    }

    private readonly record struct ScenarioResult(
        PlayerClass PlayerClass,
        ScenarioInputMode InputMode,
        double AverageSimulationMilliseconds,
        double MaxSimulationMilliseconds,
        double AverageSnapshotMilliseconds,
        double MaxSnapshotMilliseconds,
        double AverageSharedCaptureMilliseconds,
        double AveragePerClientMilliseconds,
        long AverageAllocatedBytes,
        int AverageSentPayloadBytes,
        int AverageFullPayloadBytes,
        int AveragePlayerStatusBytes,
        int AveragePlayerExtendedStatusBytes,
        int AverageEventBytes,
        double AverageVisualEventCount,
        int MaxVisualEventCount,
        int MaxSentPayloadBytes);

    private readonly record struct PublishedEvent(string Name, IReadOnlyDictionary<string, object?> Fields);
}
