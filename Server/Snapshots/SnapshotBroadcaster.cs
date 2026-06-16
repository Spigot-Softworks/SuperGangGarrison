using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using OpenGarrison.Server;
using static ServerHelpers;

sealed class SnapshotBroadcaster
{
    private readonly record struct SentSnapshotMetrics(
        int FullPayloadBytes,
        int SentPayloadBytes,
        int SerializePassCount,
        bool WasBudgeted,
        bool BaselineHit,
        bool BaselineMiss,
        SnapshotCompositionMetrics Composition,
        SnapshotCompositionMetrics CandidateComposition);

    private sealed record SharedSnapshotData(
        ClientSession[] OrderedClients,
        SnapshotMessage Template);

    private readonly record struct ClientSnapshotWorkItem(
        ClientSession Client,
        SnapshotMessage FullSnapshot,
        SnapshotBaselineState? Baseline,
        int TargetPayloadBytes,
        OpenGarrison.Server.SnapshotContributionPlanningContext PlanningContext,
        OpenGarrison.Server.SnapshotBudgetMode BudgetMode);

    private readonly record struct BuiltClientSnapshot(
        ClientSession Client,
        SnapshotMessage SentSnapshot,
        SnapshotMessage ResolvedFullSnapshot,
        byte[] Payload,
        SentSnapshotMetrics Metrics,
        long ElapsedTicks,
        long AllocatedBytes);

    private readonly SimulationWorld _world;
    private readonly SimulationConfig _config;
    private readonly Dictionary<byte, ClientSession> _clientsBySlot;
    private readonly int _maxPlayableClients;
    private readonly ServerBotManager _botManager;
    private readonly Action<ServerTransportPeer, SnapshotMessage, byte[]> _sendSnapshot;
    private readonly OpenGarrison.Server.ServerMapMetadataResolver _mapMetadataResolver;
    private readonly OpenGarrison.Server.SnapshotTransientEventBuffer _transientEventBuffer;
    private readonly OpenGarrison.Server.SnapshotBudgetMode _snapshotBudgetMode;
    private readonly Action<SnapshotMessage>? _recordCanonicalSnapshot;
    private readonly Func<bool>? _shouldRecordCanonicalSnapshot;
    private readonly Action<string> _log;
    private readonly SnapshotStringCache _stringCache = new();
    private readonly Dictionary<byte, ClientStringCacheTracker> _clientCacheTrackers = new();
    private bool _hasBroadcastTiming;
    private long _lastBroadcastTimestamp;
    private ulong _lastBroadcastFrame;

    public SnapshotBroadcaster(
        SimulationWorld world,
        SimulationConfig config,
        Dictionary<byte, ClientSession> clientsBySlot,
        int maxPlayableClients,
        ServerBotManager botManager,
        ulong transientEventReplayTicks,
        OpenGarrison.Server.ServerMapMetadataResolver mapMetadataResolver,
        Action<ServerTransportPeer, SnapshotMessage, byte[]> sendSnapshot,
        OpenGarrison.Server.SnapshotBudgetMode snapshotBudgetMode = OpenGarrison.Server.SnapshotBudgetMode.GameplayCriticalUntrimmed,
        Action<SnapshotMessage>? recordCanonicalSnapshot = null,
        Func<bool>? shouldRecordCanonicalSnapshot = null,
        Action<string>? log = null)
    {
        _world = world;
        _config = config;
        _clientsBySlot = clientsBySlot;
        _maxPlayableClients = maxPlayableClients;
        _botManager = botManager;
        _mapMetadataResolver = mapMetadataResolver;
        _transientEventBuffer = new OpenGarrison.Server.SnapshotTransientEventBuffer(transientEventReplayTicks);
        _snapshotBudgetMode = snapshotBudgetMode;
        _sendSnapshot = sendSnapshot;
        _recordCanonicalSnapshot = recordCanonicalSnapshot;
        _shouldRecordCanonicalSnapshot = shouldRecordCanonicalSnapshot;
        _log = log ?? (_ => { });
    }

    public OpenGarrison.Server.SnapshotTransientEvents LastCapturedTransientEvents { get; private set; } =
        new([], [], [], [], []);

    public SnapshotBroadcastMetrics Metrics { get; private set; }

    public void ResetTransientEvents()
    {
        _transientEventBuffer.Reset(_clientsBySlot.Values);
        LastCapturedTransientEvents = new([], [], [], [], []);
        Metrics = default;
        _hasBroadcastTiming = false;
        _lastBroadcastTimestamp = 0L;
        _lastBroadcastFrame = 0UL;
    }

    public void RemoveClientState(byte slot)
    {
        _clientCacheTrackers.Remove(slot);
    }

    public void MoveClientState(byte oldSlot, byte newSlot)
    {
        if (oldSlot == newSlot)
        {
            return;
        }

        if (_clientCacheTrackers.TryGetValue(oldSlot, out var tracker))
        {
            _clientCacheTrackers.Remove(oldSlot);
            _clientCacheTrackers[newSlot] = tracker;
            return;
        }

        _clientCacheTrackers.Remove(newSlot);
    }

    public void BroadcastSnapshot()
    {
        if (_clientsBySlot.Count == 0)
        {
            LastCapturedTransientEvents = _transientEventBuffer.CaptureCurrentEvents(_world);
            Metrics = default;
            return;
        }

        var totalStartTimestamp = Stopwatch.GetTimestamp();
        var currentFrame = (ulong)_world.Frame;
        var broadcastIntervalMilliseconds = _hasBroadcastTiming
            ? TicksToMilliseconds(totalStartTimestamp - _lastBroadcastTimestamp)
            : 0d;
        var snapshotFrameDelta = _hasBroadcastTiming && currentFrame >= _lastBroadcastFrame
            ? currentFrame - _lastBroadcastFrame
            : 0UL;
        var broadcastTargetIntervalMilliseconds = _config.TicksPerSecond > 0
            ? 1000d / _config.TicksPerSecond
            : 1000d / SimulationConfig.DefaultTicksPerSecond;
        var broadcastIntervalOverrunMilliseconds = _hasBroadcastTiming
            ? Math.Max(0d, broadcastIntervalMilliseconds - broadcastTargetIntervalMilliseconds)
            : 0d;
        _hasBroadcastTiming = true;
        _lastBroadcastTimestamp = totalStartTimestamp;
        _lastBroadcastFrame = currentFrame;
        var totalStartAllocatedBytes = GC.GetAllocatedBytesForCurrentThread();
        var transientEvents = _transientEventBuffer.CaptureCurrentEvents(_world);
        LastCapturedTransientEvents = transientEvents;

        var sharedCaptureStartTimestamp = Stopwatch.GetTimestamp();
        var sharedCaptureStartAllocatedBytes = GC.GetAllocatedBytesForCurrentThread();
        var sharedSnapshot = CaptureSharedSnapshotData(
            transientEvents.VisualEvents,
            transientEvents.DamageEvents,
            transientEvents.SoundEvents,
            transientEvents.GibSpawnEvents,
            transientEvents.RocketSpawnEvents);
        var sharedCaptureTicks = Stopwatch.GetTimestamp() - sharedCaptureStartTimestamp;
        var sharedCaptureAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - sharedCaptureStartAllocatedBytes;
        if (_recordCanonicalSnapshot is not null
            && (_shouldRecordCanonicalSnapshot?.Invoke() ?? true))
        {
            _recordCanonicalSnapshot(CaptureCanonicalSnapshot(sharedSnapshot));
        }

        long perClientTicks = 0;
        long perClientAllocatedBytes = 0;
        var totalTargetPayloadBytes = 0;
        var totalFullPayloadBytes = 0;
        var totalCandidateUncompressedBytes = 0;
        var totalCandidatePayloadBytes = 0;
        var totalSentUncompressedBytes = 0;
        var totalSentPayloadBytes = 0;
        var maxSentPayloadBytes = 0;
        var payloadOverTargetCount = 0;
        var totalSerializePassCount = 0;
        var budgetedClientCount = 0;
        var baselineHitCount = 0;
        var baselineMissCount = 0;
        var totalSnapshotHistoryCount = 0;
        var maxSnapshotHistoryCount = 0;
        var totalCandidateFullPlayerBytes = 0;
        var totalCandidatePlayerMovementBytes = 0;
        var totalCandidatePlayerStatusBytes = 0;
        var totalCandidatePlayerExtendedStatusBytes = 0;
        var totalCandidatePlayerChatBubbleBytes = 0;
        var totalCandidateProjectileBytes = 0;
        var totalCandidateSentryBytes = 0;
        var totalCandidateEventBytes = 0;
        var totalCandidateRemovalBytes = 0;
        var totalCandidateWorldBytes = 0;
        var totalCandidateEnvelopeBytes = 0;
        var totalFullPlayerBytes = 0;
        var totalPlayerMovementBytes = 0;
        var totalPlayerStatusBytes = 0;
        var totalPlayerExtendedStatusBytes = 0;
        var totalPlayerChatBubbleBytes = 0;
        var totalProjectileBytes = 0;
        var totalSentryBytes = 0;
        var totalEventBytes = 0;
        var totalRemovalBytes = 0;
        var totalWorldBytes = 0;
        var totalEnvelopeBytes = 0;
        var workItems = new ClientSnapshotWorkItem[sharedSnapshot.OrderedClients.Length];
        for (var index = 0; index < sharedSnapshot.OrderedClients.Length; index += 1)
        {
            var client = sharedSnapshot.OrderedClients[index];
            var clientStartTimestamp = Stopwatch.GetTimestamp();
            var clientStartAllocatedBytes = GC.GetAllocatedBytesForCurrentThread();
            workItems[index] = CreateClientSnapshotWorkItem(client, sharedSnapshot);
            perClientTicks += Stopwatch.GetTimestamp() - clientStartTimestamp;
            perClientAllocatedBytes += GC.GetAllocatedBytesForCurrentThread() - clientStartAllocatedBytes;
        }

        var builtSnapshots = BuildClientSnapshots(workItems);
        for (var index = 0; index < builtSnapshots.Length; index += 1)
        {
            var builtSnapshot = builtSnapshots[index];
            perClientTicks += builtSnapshot.ElapsedTicks;
            perClientAllocatedBytes += builtSnapshot.AllocatedBytes;

            var commitStartTimestamp = Stopwatch.GetTimestamp();
            var commitStartAllocatedBytes = GC.GetAllocatedBytesForCurrentThread();
            var snapshotSent = false;
            try
            {
                _sendSnapshot(builtSnapshot.Client.Peer, builtSnapshot.SentSnapshot, builtSnapshot.Payload);
                snapshotSent = true;
            }
            catch (Exception ex)
            {
                _log($"[server] failed to send snapshot to {builtSnapshot.Client.RemoteDescription}: {ex.Message}");
            }

            if (snapshotSent)
            {
                builtSnapshot.Client.RememberResolvedSnapshotState(builtSnapshot.ResolvedFullSnapshot);
            }

            perClientTicks += Stopwatch.GetTimestamp() - commitStartTimestamp;
            perClientAllocatedBytes += GC.GetAllocatedBytesForCurrentThread() - commitStartAllocatedBytes;

            var clientMetrics = builtSnapshot.Metrics;
            totalTargetPayloadBytes += clientMetrics.Composition.TargetPayloadBytes;
            totalFullPayloadBytes += clientMetrics.FullPayloadBytes;
            totalCandidateUncompressedBytes += clientMetrics.CandidateComposition.FinalUncompressedBytes;
            totalCandidatePayloadBytes += clientMetrics.CandidateComposition.FinalPayloadBytes;
            totalSentUncompressedBytes += clientMetrics.Composition.FinalUncompressedBytes;
            totalSentPayloadBytes += clientMetrics.SentPayloadBytes;
            maxSentPayloadBytes = Math.Max(maxSentPayloadBytes, clientMetrics.SentPayloadBytes);
            if (clientMetrics.Composition.PayloadOverTarget)
            {
                payloadOverTargetCount += 1;
            }

            totalSerializePassCount += clientMetrics.SerializePassCount;
            if (clientMetrics.WasBudgeted)
            {
                budgetedClientCount += 1;
            }

            if (clientMetrics.BaselineHit)
            {
                baselineHitCount += 1;
            }

            if (clientMetrics.BaselineMiss)
            {
                baselineMissCount += 1;
            }

            var snapshotHistoryCount = builtSnapshot.Client.SnapshotHistoryCount;
            totalSnapshotHistoryCount += snapshotHistoryCount;
            maxSnapshotHistoryCount = Math.Max(maxSnapshotHistoryCount, snapshotHistoryCount);
            totalCandidateFullPlayerBytes += clientMetrics.CandidateComposition.FullPlayerBytes;
            totalCandidatePlayerMovementBytes += clientMetrics.CandidateComposition.PlayerMovementBytes;
            totalCandidatePlayerStatusBytes += clientMetrics.CandidateComposition.PlayerStatusBytes;
            totalCandidatePlayerExtendedStatusBytes += clientMetrics.CandidateComposition.PlayerExtendedStatusBytes;
            totalCandidatePlayerChatBubbleBytes += clientMetrics.CandidateComposition.PlayerChatBubbleBytes;
            totalCandidateProjectileBytes += clientMetrics.CandidateComposition.ProjectileBytes;
            totalCandidateSentryBytes += clientMetrics.CandidateComposition.SentryBytes;
            totalCandidateEventBytes += clientMetrics.CandidateComposition.EventBytes;
            totalCandidateRemovalBytes += clientMetrics.CandidateComposition.RemovalBytes;
            totalCandidateWorldBytes += clientMetrics.CandidateComposition.WorldBytes;
            totalCandidateEnvelopeBytes += clientMetrics.CandidateComposition.EnvelopeAndOtherBytes;
            totalFullPlayerBytes += clientMetrics.Composition.FullPlayerBytes;
            totalPlayerMovementBytes += clientMetrics.Composition.PlayerMovementBytes;
            totalPlayerStatusBytes += clientMetrics.Composition.PlayerStatusBytes;
            totalPlayerExtendedStatusBytes += clientMetrics.Composition.PlayerExtendedStatusBytes;
            totalPlayerChatBubbleBytes += clientMetrics.Composition.PlayerChatBubbleBytes;
            totalProjectileBytes += clientMetrics.Composition.ProjectileBytes;
            totalSentryBytes += clientMetrics.Composition.SentryBytes;
            totalEventBytes += clientMetrics.Composition.EventBytes;
            totalRemovalBytes += clientMetrics.Composition.RemovalBytes;
            totalWorldBytes += clientMetrics.Composition.WorldBytes;
            totalEnvelopeBytes += clientMetrics.Composition.EnvelopeAndOtherBytes;
        }

        var totalTicks = Stopwatch.GetTimestamp() - totalStartTimestamp;
        var totalAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - totalStartAllocatedBytes;
        var clientCount = sharedSnapshot.OrderedClients.Length;
        Metrics = new SnapshotBroadcastMetrics(
            HasMeasurements: true,
            Frame: currentFrame,
            ClientCount: clientCount,
            SharedCaptureMilliseconds: TicksToMilliseconds(sharedCaptureTicks),
            PerClientMilliseconds: TicksToMilliseconds(perClientTicks),
            TotalMilliseconds: TicksToMilliseconds(totalTicks),
            BroadcastIntervalMilliseconds: broadcastIntervalMilliseconds,
            BroadcastTargetIntervalMilliseconds: broadcastTargetIntervalMilliseconds,
            BroadcastIntervalOverrunMilliseconds: broadcastIntervalOverrunMilliseconds,
            SnapshotFrameDelta: snapshotFrameDelta,
            SharedCaptureAllocatedBytes: sharedCaptureAllocatedBytes,
            PerClientAllocatedBytes: perClientAllocatedBytes,
            TotalAllocatedBytes: totalAllocatedBytes,
            AverageTargetPayloadBytes: clientCount == 0 ? 0 : totalTargetPayloadBytes / clientCount,
            AverageFullPayloadBytes: clientCount == 0 ? 0 : totalFullPayloadBytes / clientCount,
            AverageCandidateUncompressedBytes: clientCount == 0 ? 0 : totalCandidateUncompressedBytes / clientCount,
            AverageCandidatePayloadBytes: clientCount == 0 ? 0 : totalCandidatePayloadBytes / clientCount,
            AverageSentUncompressedBytes: clientCount == 0 ? 0 : totalSentUncompressedBytes / clientCount,
            AverageSentPayloadBytes: clientCount == 0 ? 0 : totalSentPayloadBytes / clientCount,
            MaxSentPayloadBytes: maxSentPayloadBytes,
            PayloadOverTargetCount: payloadOverTargetCount,
            AverageSerializePasses: clientCount == 0 ? 0d : (double)totalSerializePassCount / clientCount,
            BudgetedClientCount: budgetedClientCount,
            BaselineHitCount: baselineHitCount,
            BaselineMissCount: baselineMissCount,
            AverageSnapshotHistoryCount: clientCount == 0 ? 0d : (double)totalSnapshotHistoryCount / clientCount,
            MaxSnapshotHistoryCount: maxSnapshotHistoryCount,
            AverageSnapshotCandidateFullPlayerBytes: clientCount == 0 ? 0 : totalCandidateFullPlayerBytes / clientCount,
            AverageSnapshotCandidatePlayerMovementBytes: clientCount == 0 ? 0 : totalCandidatePlayerMovementBytes / clientCount,
            AverageSnapshotCandidatePlayerStatusBytes: clientCount == 0 ? 0 : totalCandidatePlayerStatusBytes / clientCount,
            AverageSnapshotCandidatePlayerExtendedStatusBytes: clientCount == 0 ? 0 : totalCandidatePlayerExtendedStatusBytes / clientCount,
            AverageSnapshotCandidatePlayerChatBubbleBytes: clientCount == 0 ? 0 : totalCandidatePlayerChatBubbleBytes / clientCount,
            AverageSnapshotCandidateProjectileBytes: clientCount == 0 ? 0 : totalCandidateProjectileBytes / clientCount,
            AverageSnapshotCandidateSentryBytes: clientCount == 0 ? 0 : totalCandidateSentryBytes / clientCount,
            AverageSnapshotCandidateEventBytes: clientCount == 0 ? 0 : totalCandidateEventBytes / clientCount,
            AverageSnapshotCandidateRemovalBytes: clientCount == 0 ? 0 : totalCandidateRemovalBytes / clientCount,
            AverageSnapshotCandidateWorldBytes: clientCount == 0 ? 0 : totalCandidateWorldBytes / clientCount,
            AverageSnapshotCandidateEnvelopeBytes: clientCount == 0 ? 0 : totalCandidateEnvelopeBytes / clientCount,
            AverageSnapshotFullPlayerBytes: clientCount == 0 ? 0 : totalFullPlayerBytes / clientCount,
            AverageSnapshotPlayerMovementBytes: clientCount == 0 ? 0 : totalPlayerMovementBytes / clientCount,
            AverageSnapshotPlayerStatusBytes: clientCount == 0 ? 0 : totalPlayerStatusBytes / clientCount,
            AverageSnapshotPlayerExtendedStatusBytes: clientCount == 0 ? 0 : totalPlayerExtendedStatusBytes / clientCount,
            AverageSnapshotPlayerChatBubbleBytes: clientCount == 0 ? 0 : totalPlayerChatBubbleBytes / clientCount,
            AverageSnapshotProjectileBytes: clientCount == 0 ? 0 : totalProjectileBytes / clientCount,
            AverageSnapshotSentryBytes: clientCount == 0 ? 0 : totalSentryBytes / clientCount,
            AverageSnapshotEventBytes: clientCount == 0 ? 0 : totalEventBytes / clientCount,
            AverageSnapshotRemovalBytes: clientCount == 0 ? 0 : totalRemovalBytes / clientCount,
            AverageSnapshotWorldBytes: clientCount == 0 ? 0 : totalWorldBytes / clientCount,
            AverageSnapshotEnvelopeBytes: clientCount == 0 ? 0 : totalEnvelopeBytes / clientCount);
    }

    private ClientSnapshotWorkItem CreateClientSnapshotWorkItem(ClientSession client, SharedSnapshotData sharedSnapshot)
    {
        var fullSnapshot = CaptureFullSnapshot(client, sharedSnapshot);
        return new ClientSnapshotWorkItem(
            client,
            fullSnapshot,
            TryGetBaselineSnapshot(client, fullSnapshot),
            GetTargetSnapshotPayloadBytes(client),
            OpenGarrison.Server.SnapshotContributionPlanner.CreatePlanningContext(client, fullSnapshot, _world),
            _snapshotBudgetMode);
    }

    private static BuiltClientSnapshot[] BuildClientSnapshots(ClientSnapshotWorkItem[] workItems)
    {
        if (workItems.Length == 0)
        {
            return Array.Empty<BuiltClientSnapshot>();
        }

        var builtSnapshots = new BuiltClientSnapshot[workItems.Length];
        var workerCount = Math.Min(
            workItems.Length,
            Math.Max(1, Environment.ProcessorCount));
        if (workerCount <= 1)
        {
            for (var index = 0; index < workItems.Length; index += 1)
            {
                builtSnapshots[index] = BuildClientSnapshot(workItems[index]);
            }

            return builtSnapshots;
        }

        Parallel.For(
            0,
            workItems.Length,
            new ParallelOptions { MaxDegreeOfParallelism = workerCount },
            index => builtSnapshots[index] = BuildClientSnapshot(workItems[index]));
        return builtSnapshots;
    }

    private static BuiltClientSnapshot BuildClientSnapshot(ClientSnapshotWorkItem workItem)
    {
        var startTimestamp = Stopwatch.GetTimestamp();
        var startAllocatedBytes = GC.GetAllocatedBytesForCurrentThread();
        var fullSnapshotPayloadBytes = ProtocolCodec.MeasureSerializedSize(workItem.FullSnapshot);

        // A client with no baseline cannot safely render compact movement-only player updates for
        // peers it has never seen. Always send a true full snapshot until the client ACKs one.
        if (workItem.Baseline is null)
        {
            return BuildFullClientSnapshot(
                workItem,
                fullSnapshotPayloadBytes,
                startTimestamp,
                startAllocatedBytes);
        }

        var contributions = OpenGarrison.Server.SnapshotContributionPlanner.BuildContributions(
            workItem.PlanningContext,
            workItem.FullSnapshot,
            workItem.Baseline);
        var snapshot = workItem.BudgetMode == OpenGarrison.Server.SnapshotBudgetMode.GameplayCriticalUntrimmed
            ? OpenGarrison.Server.SnapshotDeltaBudgeter.BuildUntrimmedSnapshotWithEmergencyReduction(
                workItem.FullSnapshot,
                workItem.Baseline,
                contributions,
                workItem.TargetPayloadBytes)
            : OpenGarrison.Server.SnapshotDeltaBudgeter.BuildBudgetedSnapshotWithMetrics(
                workItem.FullSnapshot,
                workItem.Baseline,
                contributions,
                workItem.TargetPayloadBytes);
        var resolvedFullSnapshot = SnapshotDelta.ToFullSnapshot(snapshot.Message, workItem.Baseline);
        return new BuiltClientSnapshot(
            workItem.Client,
            snapshot.Message,
            resolvedFullSnapshot,
            snapshot.Payload,
            new SentSnapshotMetrics(
                FullPayloadBytes: fullSnapshotPayloadBytes,
                SentPayloadBytes: snapshot.Payload.Length,
                SerializePassCount: snapshot.SerializePassCount,
                WasBudgeted: workItem.BudgetMode == OpenGarrison.Server.SnapshotBudgetMode.Balanced
                    || snapshot.ReductionApplied,
                BaselineHit: workItem.Baseline is not null,
                BaselineMiss: workItem.Baseline is null,
                Composition: snapshot.Composition,
                CandidateComposition: snapshot.CandidateComposition),
            Stopwatch.GetTimestamp() - startTimestamp,
            GC.GetAllocatedBytesForCurrentThread() - startAllocatedBytes);
    }

    private static BuiltClientSnapshot BuildFullClientSnapshot(
        ClientSnapshotWorkItem workItem,
        int fullSnapshotPayloadBytes,
        long startTimestamp,
        long startAllocatedBytes)
    {
        var payload = ProtocolCodec.Serialize(workItem.FullSnapshot, ServerProtocolCompression.Settings);
        var composition = SnapshotDeltaBudgeter.BuildCompositionMetrics(
            workItem.FullSnapshot,
            workItem.TargetPayloadBytes,
            fullSnapshotPayloadBytes,
            payload.Length);
        return new BuiltClientSnapshot(
            workItem.Client,
            workItem.FullSnapshot,
            workItem.FullSnapshot,
            payload,
            new SentSnapshotMetrics(
                FullPayloadBytes: fullSnapshotPayloadBytes,
                SentPayloadBytes: payload.Length,
                SerializePassCount: 1,
                WasBudgeted: false,
                BaselineHit: false,
                BaselineMiss: false,
                Composition: composition,
                CandidateComposition: composition),
            Stopwatch.GetTimestamp() - startTimestamp,
            GC.GetAllocatedBytesForCurrentThread() - startAllocatedBytes);
    }

    private SharedSnapshotData CaptureSharedSnapshotData(
        SnapshotVisualEvent[] visualEvents,
        SnapshotDamageEvent[] damageEvents,
        SnapshotSoundEvent[] soundEvents,
        SnapshotGibSpawnEvent[] gibSpawnEvents,
        SnapshotRocketSpawnEvent[] rocketSpawnEvents)
    {
        var orderedClients = _clientsBySlot.Values.ToArray();
        Array.Sort(orderedClients, static (left, right) => left.Slot.CompareTo(right.Slot));

        var spectatorCount = 0;
        foreach (var client in orderedClients)
        {
            if (IsSpectatorSlot(client.Slot))
            {
                spectatorCount += 1;
                continue;
            }

            if (SimulationWorld.IsPlayableNetworkPlayerSlot(client.Slot)
                && _world.IsNetworkPlayerAwaitingJoin(client.Slot))
            {
                spectatorCount += 1;
            }
        }

        var mapAreaIndex = (byte)Math.Clamp(_world.Level.MapAreaIndex, 1, byte.MaxValue);
        var mapAreaCount = (byte)Math.Clamp(_world.Level.MapAreaCount, 1, byte.MaxValue);
        var mapMetadata = _mapMetadataResolver.GetCurrentMapMetadata();
        var template = new SnapshotMessage(
            (ulong)_world.Frame,
            _config.TicksPerSecond,
            _world.Level.Name,
            mapAreaIndex,
            mapAreaCount,
            (byte)_world.MatchRules.Mode,
            (byte)_world.MatchState.Phase,
            _world.MatchState.WinnerTeam.HasValue ? (byte)_world.MatchState.WinnerTeam.Value : (byte)0,
            _world.MatchState.TimeRemainingTicks,
            _world.RedCaps,
            _world.BlueCaps,
            spectatorCount,
            LastProcessedInputSequence: 0,
            ToSnapshotIntelState(_world.RedIntel),
            ToSnapshotIntelState(_world.BlueIntel),
            Players: Array.Empty<SnapshotPlayerState>(),
            ConvertNetworkCombatTracesToArray(_world.CombatTraces),
            ConvertToArray(_world.SniperAimIndicators, static indicator => ToSnapshotSniperAimIndicatorState(indicator)),
            ConvertToArray(_world.Sentries, static sentry => ToSnapshotSentryState(sentry)),
            ConvertToArray(_world.Shots, static shot => ToSnapshotBulletState(shot)),
            ConvertToArray(_world.Bubbles, static bubble => ToSnapshotBubbleState(bubble)),
            ConvertToArray(_world.Blades, static blade => ToSnapshotBladeState(blade)),
            ConvertToArray(_world.Needles, static needle => ToSnapshotNeedleState(needle)),
            ConvertToArray(_world.RevolverShots, static shot => ToSnapshotRevolverState(shot)),
            ConvertToArray(_world.Rockets, static rocket => ToSnapshotRocketState(rocket)),
            ConvertToArray(_world.Flames, static flame => ToSnapshotFlameState(flame)),
            ConvertToArray(_world.Flares, static flare => ToSnapshotFlareState(flare)),
            ConvertToArray(_world.Mines, static mine => ToSnapshotMineState(mine)),
            ConvertToArray(_world.DeadBodies, static body => ToSnapshotDeadBodyState(body)),
            _world.ControlPointSetupTicksRemaining,
            _world.KothUnlockTicksRemaining,
            _world.KothRedTimerTicksRemaining,
            _world.KothBlueTimerTicksRemaining,
            ConvertToArray(_world.ControlPoints, static point => ToSnapshotControlPointState(point)),
            ConvertToArray(_world.Generators, static generator => ToSnapshotGeneratorState(generator)),
            LocalDeathCam: null,
            ConvertToArray(_world.KillFeed, static entry => ToSnapshotKillFeedEntry(entry)),
            visualEvents,
            damageEvents,
            soundEvents,
            StringCacheUpdates: null, // TODO: Implement string cache
            mapMetadata.IsCustomMap,
            mapMetadata.MapDownloadUrl,
            mapMetadata.MapContentHash,
            _world.Level.MapScale)
        {
            TimeLimitTicks = _world.MatchRules.TimeLimitTicks,
            ArenaUnlockTicksRemaining = _world.ArenaUnlockTicksRemaining,
            ArenaPointTeam = _world.ArenaPointTeam.HasValue ? (byte)_world.ArenaPointTeam.Value : (byte)0,
            ArenaCappingTeam = _world.ArenaCappingTeam.HasValue ? (byte)_world.ArenaCappingTeam.Value : (byte)0,
            ArenaCappingTicks = _world.ArenaCappingTicks,
            ArenaCappers = _world.ArenaCappers,
            ArenaRedConsecutiveWins = _world.ArenaRedConsecutiveWins,
            ArenaBlueConsecutiveWins = _world.ArenaBlueConsecutiveWins,
            CompetitiveReadyUpPhase = (byte)_world.CompetitiveReadyUpPhase,
            CompetitiveReadyUpTicksRemaining = _world.CompetitiveReadyUpTicksRemaining,
            SentryGibs = ConvertToArray(_world.SentryGibs, static sentryGib => ToSnapshotSentryGibState(sentryGib)),
            JumpPads = ConvertToArray(_world.JumpPads, static jumpPad => ToSnapshotJumpPadState(jumpPad)),
            JumpPadGibs = ConvertToArray(_world.JumpPadGibs, static jumpPadGib => ToSnapshotJumpPadGibState(jumpPadGib)),
            Grenades = ConvertToArray(_world.Grenades, static grenade => ToSnapshotGrenadeState(grenade)),
            GibSpawnEvents = gibSpawnEvents,
            RocketSpawnEvents = rocketSpawnEvents,
        };

        return new SharedSnapshotData(orderedClients, template);
    }

    private SnapshotMessage CaptureFullSnapshot(ClientSession client, SharedSnapshotData sharedSnapshot)
    {
        PlayerEntity? viewer = null;
        if (SimulationWorld.IsPlayableNetworkPlayerSlot(client.Slot)
            && !_world.IsNetworkPlayerAwaitingJoin(client.Slot)
            && _world.TryGetNetworkPlayer(client.Slot, out var viewerPlayer))
        {
            viewer = viewerPlayer;
        }

        // Start with capacity for clients + bots
        var players = new List<SnapshotPlayerState>(sharedSnapshot.OrderedClients.Length + _botManager.BotSlots.Count);
        var removedPlayerIds = new List<int>();

        // Add human client players
        foreach (var entry in sharedSnapshot.OrderedClients)
        {
            if (IsSpectatorSlot(entry.Slot))
            {
                players.Add(CreateSpectatorSnapshotPlayerState(entry));
                continue;
            }

            if (!_world.TryGetNetworkPlayer(entry.Slot, out var player))
            {
                continue;
            }

            if (ShouldHideSpyFromViewer(player, viewer))
            {
                removedPlayerIds.Add(entry.Slot);
                continue;
            }

            players.Add(ToSnapshotPlayerState(_world, entry.Slot, player, viewer, _stringCache, entry.PingMilliseconds));
        }

        // Add server bot players
        foreach (var (botSlot, botState) in _botManager.BotSlots)
        {
            if (!_world.TryGetNetworkPlayer(botSlot, out var botPlayer))
            {
                continue;
            }

            if (ShouldHideSpyFromViewer(botPlayer, viewer))
            {
                removedPlayerIds.Add(botSlot);
                continue;
            }

            players.Add(ToSnapshotPlayerState(_world, botSlot, botPlayer, viewer, _stringCache));
        }

        // Build string cache updates for this client
        var cacheTracker = GetOrCreateCacheTracker(client.Slot);
        var referencedStrings = CollectReferencedCachedStrings(players);
        var stringCacheUpdates = cacheTracker.BuildCacheUpdatesForSnapshot(referencedStrings);

        return sharedSnapshot.Template with
        {
            LastProcessedInputSequence = client.LastProcessedInputSequence,
            Players = players.ToArray(),
            RemovedPlayerIds = removedPlayerIds.Count == 0 ? Array.Empty<int>() : removedPlayerIds.ToArray(),
            LocalDeathCam = ToSnapshotDeathCamState(_world.GetNetworkPlayerDeathCam(client.Slot)),
            StringCacheUpdates = stringCacheUpdates,
            VisualEvents = FilterAcknowledgedTransientEvents(
                sharedSnapshot.Template.VisualEvents,
                client,
                static visualEvent => visualEvent.EventId),
            DamageEvents = FilterAcknowledgedTransientEvents(
                sharedSnapshot.Template.DamageEvents,
                client,
                static damageEvent => damageEvent.EventId),
            SoundEvents = FilterSoundEventsForClient(sharedSnapshot.Template.SoundEvents, client, viewer),
            GibSpawnEvents = FilterAcknowledgedTransientEvents(
                sharedSnapshot.Template.GibSpawnEvents,
                client,
                static gibEvent => gibEvent.EventId),
            RocketSpawnEvents = FilterRedundantRocketSpawnEvents(
                FilterAcknowledgedTransientEvents(
                    sharedSnapshot.Template.RocketSpawnEvents,
                    client,
                    static rocketEvent => rocketEvent.EventId),
                sharedSnapshot.Template.Rockets),
        };
    }

    internal static IReadOnlyList<SnapshotSoundEvent> FilterSoundEventsForClient(
        IReadOnlyList<SnapshotSoundEvent> soundEvents,
        ClientSession client,
        PlayerEntity? viewer)
    {
        if (soundEvents.Count == 0)
        {
            return Array.Empty<SnapshotSoundEvent>();
        }

        List<SnapshotSoundEvent>? filtered = null;
        for (var index = 0; index < soundEvents.Count; index += 1)
        {
            var soundEvent = soundEvents[index];
            if (client.HasAcknowledgedSoundEvent(soundEvent.EventId)
                || IsLocalManagedRapidFireSound(soundEvent, viewer))
            {
                filtered ??= CopySoundEvents(soundEvents, index);
                continue;
            }

            filtered?.Add(soundEvent);
        }

        return filtered is null
            ? soundEvents
            : filtered.Count == 0
                ? Array.Empty<SnapshotSoundEvent>()
                : filtered.ToArray();
    }

    private static IReadOnlyList<TEvent> FilterAcknowledgedTransientEvents<TEvent>(
        IReadOnlyList<TEvent> events,
        ClientSession client,
        Func<TEvent, ulong> eventIdSelector)
    {
        if (events.Count == 0)
        {
            return Array.Empty<TEvent>();
        }

        List<TEvent>? filtered = null;
        for (var index = 0; index < events.Count; index += 1)
        {
            var transientEvent = events[index];
            if (client.HasAcknowledgedTransientEvent(eventIdSelector(transientEvent)))
            {
                filtered ??= CopyEvents(events, index);
                continue;
            }

            filtered?.Add(transientEvent);
        }

        return filtered is null
            ? events
            : filtered.Count == 0
                ? Array.Empty<TEvent>()
                : filtered.ToArray();
    }

    internal static IReadOnlyList<SnapshotRocketSpawnEvent> FilterRedundantRocketSpawnEvents(
        IReadOnlyList<SnapshotRocketSpawnEvent> rocketSpawnEvents,
        IReadOnlyList<SnapshotRocketState> rocketStates)
    {
        if (rocketSpawnEvents.Count == 0)
        {
            return Array.Empty<SnapshotRocketSpawnEvent>();
        }

        if (rocketStates.Count == 0)
        {
            return rocketSpawnEvents;
        }

        HashSet<int>? rocketStateIds = null;
        List<SnapshotRocketSpawnEvent>? filtered = null;
        for (var index = 0; index < rocketSpawnEvents.Count; index += 1)
        {
            var rocketSpawnEvent = rocketSpawnEvents[index];
            if (!rocketSpawnEvent.ExplodeImmediately)
            {
                rocketStateIds ??= BuildRocketStateIdSet(rocketStates);
                if (rocketStateIds.Contains(rocketSpawnEvent.Id))
                {
                    filtered ??= CopyEvents(rocketSpawnEvents, index);
                    continue;
                }
            }

            filtered?.Add(rocketSpawnEvent);
        }

        return filtered is null
            ? rocketSpawnEvents
            : filtered.Count == 0
                ? Array.Empty<SnapshotRocketSpawnEvent>()
                : filtered.ToArray();
    }

    private static HashSet<int> BuildRocketStateIdSet(IReadOnlyList<SnapshotRocketState> rocketStates)
    {
        var ids = new HashSet<int>();
        for (var index = 0; index < rocketStates.Count; index += 1)
        {
            ids.Add(rocketStates[index].Id);
        }

        return ids;
    }

    private static List<TEvent> CopyEvents<TEvent>(IReadOnlyList<TEvent> events, int count)
    {
        var copied = new List<TEvent>(events.Count);
        for (var index = 0; index < count; index += 1)
        {
            copied.Add(events[index]);
        }

        return copied;
    }

    private static List<SnapshotSoundEvent> CopySoundEvents(IReadOnlyList<SnapshotSoundEvent> soundEvents, int count)
    {
        var copied = new List<SnapshotSoundEvent>(soundEvents.Count);
        for (var index = 0; index < count; index += 1)
        {
            copied.Add(soundEvents[index]);
        }

        return copied;
    }

    private static bool IsLocalManagedRapidFireSound(SnapshotSoundEvent soundEvent, PlayerEntity? viewer)
    {
        return viewer is not null
            && soundEvent.SourcePlayerId == viewer.Id
            && IsManagedRapidFireSound(soundEvent.SoundName);
    }

    private static bool IsManagedRapidFireSound(string soundName)
    {
        return string.Equals(soundName, "ChaingunSnd", StringComparison.OrdinalIgnoreCase)
            || string.Equals(soundName, "FlamethrowerSnd", StringComparison.OrdinalIgnoreCase)
            || string.Equals(soundName, "MedigunSnd", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldHideSpyFromViewer(PlayerEntity player, PlayerEntity? viewer)
    {
        if (viewer is null)
        {
            return false;
        }

        if (player.Team == viewer.Team)
        {
            return false;
        }

        if (player.ClassId != PlayerClass.Spy)
        {
            return false;
        }

        if (!viewer.IsAlive)
        {
            return false;
        }

        if (player.IsSpyBackstabAnimating)
        {
            return IsSpyHiddenFromViewer(player, viewer);
        }

        if (player.IsSpyVisibleToEnemies)
        {
            return false;
        }

        return IsSpyHiddenFromViewer(player, viewer);
    }

    private static bool IsSpyHiddenFromViewer(PlayerEntity spy, PlayerEntity viewer)
    {
        var viewerFacingSign = IsFacingLeftByAim(viewer) ? -1 : 1;
        return Math.Sign(spy.X - viewer.X) == -viewerFacingSign;
    }

    private static bool IsFacingLeftByAim(PlayerEntity player)
    {
        var radians = MathF.PI * player.AimDirectionDegrees / 180f;
        return MathF.Cos(radians) < 0f;
    }

    private static SnapshotPlayerState CreateSpectatorSnapshotPlayerState(ClientSession client)
    {
        return new SnapshotPlayerState(
            Slot: client.Slot,
            PlayerId: -(int)client.Slot,
            Name: client.Name,
            Team: 0,
            ClassId: 0,
            IsAlive: false,
            IsAwaitingJoin: false,
            IsSpectator: true,
            RespawnTicks: 0,
            PingMilliseconds: client.PingMilliseconds,
            X: 0f,
            Y: 0f,
            HorizontalSpeed: 0f,
            VerticalSpeed: 0f,
            Health: 0,
            MaxHealth: 0,
            Ammo: 0,
            MaxAmmo: 0,
            Kills: 0,
            Deaths: 0,
            Caps: 0,
            Points: 0f,
            HealPoints: 0,
            ActiveDominationCount: 0,
            IsDominatingLocalViewer: false,
            IsDominatedByLocalViewer: false,
            Metal: 0f,
            IsGrounded: false,
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
            IsTypingChatMessage: false,
            Assists: 0,
            BadgeMask: client.BadgeMask,
            GameplayModPackId: string.Empty,
            GameplayLoadoutId: string.Empty,
            GameplayPrimaryItemId: string.Empty,
            GameplaySecondaryItemId: string.Empty,
            GameplayUtilityItemId: string.Empty,
            GameplayEquippedSlot: 0,
            GameplayEquippedItemId: string.Empty,
            GameplayAcquiredItemId: string.Empty,
            OwnedGameplayItemIds: Array.Empty<string>(),
            PlayerScale: 1f);
    }

    private ClientStringCacheTracker GetOrCreateCacheTracker(byte slot)
    {
        if (!_clientCacheTrackers.TryGetValue(slot, out var tracker))
        {
            tracker = new ClientStringCacheTracker(_stringCache);
            _clientCacheTrackers[slot] = tracker;
        }

        return tracker;
    }

    private static List<(string value, ushort cacheId)> CollectReferencedCachedStrings(List<SnapshotPlayerState> players)
    {
        var referenced = new List<(string, ushort)>(players.Count * 8);

        foreach (var player in players)
        {
            if (player.GameplayModPackCacheId != 0)
                referenced.Add((player.GameplayModPackId, player.GameplayModPackCacheId));
            if (player.GameplayLoadoutCacheId != 0)
                referenced.Add((player.GameplayLoadoutId, player.GameplayLoadoutCacheId));
            if (player.GameplayPrimaryItemCacheId != 0)
                referenced.Add((player.GameplayPrimaryItemId, player.GameplayPrimaryItemCacheId));
            if (player.GameplaySecondaryItemCacheId != 0)
                referenced.Add((player.GameplaySecondaryItemId, player.GameplaySecondaryItemCacheId));
            if (player.GameplayUtilityItemCacheId != 0)
                referenced.Add((player.GameplayUtilityItemId, player.GameplayUtilityItemCacheId));
            if (player.GameplayEquippedItemCacheId != 0)
                referenced.Add((player.GameplayEquippedItemId, player.GameplayEquippedItemCacheId));
            if (player.GameplayAcquiredItemCacheId != 0)
                referenced.Add((player.GameplayAcquiredItemId, player.GameplayAcquiredItemCacheId));
            if (player.GameplayClassCacheId != 0)
                referenced.Add((player.GameplayClassId, player.GameplayClassCacheId));
        }

        return referenced;
    }

    private static SnapshotBaselineState? TryGetBaselineSnapshot(ClientSession client, SnapshotMessage fullSnapshot)
    {
        if (client.LastAcknowledgedSnapshotFrame == 0
            || !client.TryGetSnapshotState(client.LastAcknowledgedSnapshotFrame, out var baseline))
        {
            return null;
        }

        return string.Equals(baseline.LevelName, fullSnapshot.LevelName, StringComparison.OrdinalIgnoreCase)
            && baseline.MapAreaIndex == fullSnapshot.MapAreaIndex
            && baseline.MapAreaCount == fullSnapshot.MapAreaCount
            && MathF.Abs(baseline.MapScale - fullSnapshot.MapScale) <= 0.0001f
            ? baseline
            : null;
    }

    private static TTarget[] ConvertToArray<TSource, TTarget>(IEnumerable<TSource> source, Func<TSource, TTarget> selector)
    {
        if (source is ICollection<TSource> collection)
        {
            if (collection.Count == 0)
            {
                return Array.Empty<TTarget>();
            }

            var result = new TTarget[collection.Count];
            var index = 0;
            foreach (var item in collection)
            {
                result[index] = selector(item);
                index += 1;
            }

            return result;
        }

        if (source is IReadOnlyCollection<TSource> readOnlyCollection)
        {
            if (readOnlyCollection.Count == 0)
            {
                return Array.Empty<TTarget>();
            }

            var result = new TTarget[readOnlyCollection.Count];
            var index = 0;
            foreach (var item in source)
            {
                result[index] = selector(item);
                index += 1;
            }

            return result;
        }

        var list = new List<TTarget>();
        foreach (var item in source)
        {
            list.Add(selector(item));
        }

        return list.Count == 0 ? Array.Empty<TTarget>() : [.. list];
    }

    internal static SnapshotCombatTraceState[] ConvertNetworkCombatTracesToArray(IEnumerable<CombatTrace> combatTraces)
    {
        var traces = new List<SnapshotCombatTraceState>();
        foreach (var trace in combatTraces)
        {
            if (!trace.IsSniperTracer)
            {
                continue;
            }

            traces.Add(ToSnapshotCombatTraceState(trace));
        }

        return traces.Count == 0 ? Array.Empty<SnapshotCombatTraceState>() : [.. traces];
    }

    private static double TicksToMilliseconds(long ticks)
    {
        return ticks * 1000d / Stopwatch.Frequency;
    }

    private static int GetTargetSnapshotPayloadBytes(ClientSession client)
    {
        return client.Peer.Kind == ServerTransportKind.WebSocket
            ? OpenGarrison.Server.SnapshotDeltaBudgeter.ReliableStreamTargetSnapshotPayloadBytes
            : client.IsLoopbackConnection
                ? OpenGarrison.Server.SnapshotDeltaBudgeter.LoopbackTargetSnapshotPayloadBytes
                : OpenGarrison.Server.SnapshotDeltaBudgeter.TargetSnapshotPayloadBytes;
    }

    public WelcomeMessage CreateCanonicalDemoWelcomeMessage(string serverName)
    {
        var mapMetadata = _mapMetadataResolver.GetCurrentMapMetadata();
        return new WelcomeMessage(
            serverName,
            ProtocolVersion.Current,
            _config.TicksPerSecond,
            _world.Level.Name,
            SimulationWorld.FirstSpectatorSlot,
            _maxPlayableClients,
            mapMetadata.IsCustomMap,
            mapMetadata.MapDownloadUrl,
            mapMetadata.MapContentHash,
            _world.Level.MapScale);
    }

    public SnapshotMessage CaptureCanonicalDemoSnapshot()
    {
        var sharedSnapshot = CaptureSharedSnapshotData(
            Array.Empty<SnapshotVisualEvent>(),
            Array.Empty<SnapshotDamageEvent>(),
            Array.Empty<SnapshotSoundEvent>(),
            Array.Empty<SnapshotGibSpawnEvent>(),
            Array.Empty<SnapshotRocketSpawnEvent>());
        return CaptureCanonicalSnapshot(sharedSnapshot);
    }

    private SnapshotMessage CaptureCanonicalSnapshot(SharedSnapshotData sharedSnapshot)
    {
        var players = new List<SnapshotPlayerState>(sharedSnapshot.OrderedClients.Length + _botManager.BotSlots.Count);

        foreach (var entry in sharedSnapshot.OrderedClients)
        {
            if (IsSpectatorSlot(entry.Slot))
            {
                players.Add(CreateSpectatorSnapshotPlayerState(entry));
                continue;
            }

            if (_world.TryGetNetworkPlayer(entry.Slot, out var player))
            {
                players.Add(ToSnapshotPlayerState(_world, entry.Slot, player, viewer: null, _stringCache, entry.PingMilliseconds));
            }
        }

        foreach (var (botSlot, _) in _botManager.BotSlots)
        {
            if (_world.TryGetNetworkPlayer(botSlot, out var botPlayer))
            {
                players.Add(ToSnapshotPlayerState(_world, botSlot, botPlayer, viewer: null, _stringCache));
            }
        }

        return sharedSnapshot.Template with
        {
            LastProcessedInputSequence = 0,
            Players = players.ToArray(),
            RemovedPlayerIds = Array.Empty<int>(),
            LocalDeathCam = null,
        };
    }

}
