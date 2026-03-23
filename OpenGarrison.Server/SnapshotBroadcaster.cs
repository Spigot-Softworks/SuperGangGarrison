using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using static ServerHelpers;

sealed class SnapshotBroadcaster
{
    private const int TargetSnapshotPayloadBytes = 1200;
    private readonly record struct SerializedSnapshot(SnapshotMessage Message, byte[] Payload);

    private readonly SimulationWorld _world;
    private readonly SimulationConfig _config;
    private readonly Dictionary<byte, ClientSession> _clientsBySlot;
    private readonly Action<IPEndPoint, SnapshotMessage, byte[]> _sendSnapshot;
    private readonly ulong _transientEventReplayTicks;
    private readonly List<RetainedSnapshotSoundEvent> _recentSoundEvents = new();
    private readonly List<RetainedSnapshotVisualEvent> _recentVisualEvents = new();
    private string _cachedMapMetadataLevelName = string.Empty;
    private bool _cachedIsCustomMap;
    private string _cachedMapDownloadUrl = string.Empty;
    private string _cachedMapContentHash = string.Empty;
    private ulong _nextTransientEventId = 1;

    public SnapshotBroadcaster(
        SimulationWorld world,
        SimulationConfig config,
        Dictionary<byte, ClientSession> clientsBySlot,
        ulong transientEventReplayTicks,
        Action<IPEndPoint, SnapshotMessage, byte[]> sendSnapshot)
    {
        _world = world;
        _config = config;
        _clientsBySlot = clientsBySlot;
        _transientEventReplayTicks = transientEventReplayTicks;
        _sendSnapshot = sendSnapshot;
    }

    public void ResetTransientEvents()
    {
        _recentSoundEvents.Clear();
        _recentVisualEvents.Clear();
        _nextTransientEventId = 1;
        foreach (var client in _clientsBySlot.Values)
        {
            client.ResetSnapshotHistory();
        }
    }

    public void BroadcastSnapshot()
    {
        if (_clientsBySlot.Count == 0)
        {
            return;
        }

        var currentFrame = (ulong)_world.Frame;
        AppendRetainedVisualEvents(_world.DrainPendingVisualEvents(), currentFrame);
        AppendRetainedSoundEvents(_world.DrainPendingSoundEvents(), currentFrame);
        _recentVisualEvents.RemoveAll(visualEvent => visualEvent.ExpiresAfterFrame < currentFrame);
        _recentSoundEvents.RemoveAll(soundEvent => soundEvent.ExpiresAfterFrame < currentFrame);
        var visualEvents = _recentVisualEvents.Select(visualEvent => visualEvent.Event).ToArray();
        var soundEvents = _recentSoundEvents.Select(soundEvent => soundEvent.Event).ToArray();
        foreach (var client in _clientsBySlot.Values)
        {
            SendSnapshot(client, visualEvents, soundEvents);
        }
    }

    private void AppendRetainedSoundEvents(IReadOnlyList<WorldSoundEvent> soundEvents, ulong currentFrame)
    {
        for (var index = 0; index < soundEvents.Count; index += 1)
        {
            _recentSoundEvents.Add(new RetainedSnapshotSoundEvent(
                ToSnapshotSoundEvent(soundEvents[index], _nextTransientEventId++),
                currentFrame + _transientEventReplayTicks));
        }
    }

    private void AppendRetainedVisualEvents(IReadOnlyList<WorldVisualEvent> visualEvents, ulong currentFrame)
    {
        for (var index = 0; index < visualEvents.Count; index += 1)
        {
            _recentVisualEvents.Add(new RetainedSnapshotVisualEvent(
                ToSnapshotVisualEvent(visualEvents[index], _nextTransientEventId++),
                currentFrame + _transientEventReplayTicks));
        }
    }

    private void SendSnapshot(ClientSession client, SnapshotVisualEvent[] visualEvents, SnapshotSoundEvent[] soundEvents)
    {
        var fullSnapshot = CaptureFullSnapshot(client, visualEvents, soundEvents);
        var fullSnapshotPayload = ProtocolCodec.Serialize(fullSnapshot);
        if (fullSnapshotPayload.Length <= TargetSnapshotPayloadBytes)
        {
            _sendSnapshot(client.EndPoint, fullSnapshot, fullSnapshotPayload);
            client.RememberSnapshotState(fullSnapshot);
            return;
        }

        var baseline = TryGetBaselineSnapshot(client, fullSnapshot);
        var snapshot = BuildBudgetedSnapshot(client, fullSnapshot, baseline);
        _sendSnapshot(client.EndPoint, snapshot.Message, snapshot.Payload);
        client.RememberSnapshotState(SnapshotDelta.ToFullSnapshot(snapshot.Message, baseline));
    }

    private SnapshotMessage CaptureFullSnapshot(
        ClientSession client,
        SnapshotVisualEvent[] visualEvents,
        SnapshotSoundEvent[] soundEvents)
    {
        var players = _world
            .EnumerateReplicatedNetworkPlayers()
            .Select(entry => ToSnapshotPlayerState(_world, entry.Slot, entry.Player))
            .ToList();

        var spectatorCount = _clientsBySlot.Keys.Count(IsSpectatorSlot);
        var mapAreaIndex = (byte)Math.Clamp(_world.Level.MapAreaIndex, 1, byte.MaxValue);
        var mapAreaCount = (byte)Math.Clamp(_world.Level.MapAreaCount, 1, byte.MaxValue);
        var mapMetadata = GetCurrentMapMetadata();
        return new SnapshotMessage(
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
            client.LastProcessedInputSequence,
            ToSnapshotIntelState(_world.RedIntel),
            ToSnapshotIntelState(_world.BlueIntel),
            players,
            _world.CombatTraces.Select(ToSnapshotCombatTraceState).ToArray(),
            _world.Sentries.Select(ToSnapshotSentryState).ToArray(),
            _world.Shots.Select(ToSnapshotBulletState).ToArray(),
            _world.Bubbles.Select(ToSnapshotBubbleState).ToArray(),
            _world.Blades.Select(ToSnapshotBladeState).ToArray(),
            _world.Needles.Select(ToSnapshotNeedleState).ToArray(),
            _world.RevolverShots.Select(ToSnapshotRevolverState).ToArray(),
            _world.Rockets.Select(ToSnapshotRocketState).ToArray(),
            _world.Flames.Select(ToSnapshotFlameState).ToArray(),
            _world.Mines.Select(ToSnapshotMineState).ToArray(),
            _world.PlayerGibs.Select(ToSnapshotPlayerGibState).ToArray(),
            _world.BloodDrops.Select(ToSnapshotBloodDropState).ToArray(),
            _world.DeadBodies.Select(ToSnapshotDeadBodyState).ToArray(),
            _world.ControlPointSetupTicksRemaining,
            _world.ControlPoints.Select(ToSnapshotControlPointState).ToArray(),
            _world.Generators.Select(ToSnapshotGeneratorState).ToArray(),
            ToSnapshotDeathCamState(_world.GetNetworkPlayerDeathCam(client.Slot)),
            _world.KillFeed.Select(ToSnapshotKillFeedEntry).ToArray(),
            visualEvents,
            soundEvents,
            mapMetadata.IsCustomMap,
            mapMetadata.MapDownloadUrl,
            mapMetadata.MapContentHash);
    }

    private (bool IsCustomMap, string MapDownloadUrl, string MapContentHash) GetCurrentMapMetadata()
    {
        var levelName = _world.Level.Name;
        if (string.Equals(_cachedMapMetadataLevelName, levelName, StringComparison.OrdinalIgnoreCase))
        {
            return (_cachedIsCustomMap, _cachedMapDownloadUrl, _cachedMapContentHash);
        }

        _cachedMapMetadataLevelName = levelName;
        if (CustomMapDescriptorResolver.TryResolve(levelName, out var descriptor))
        {
            _cachedIsCustomMap = true;
            _cachedMapDownloadUrl = descriptor.SourceUrl;
            _cachedMapContentHash = descriptor.ContentHash;
        }
        else
        {
            _cachedIsCustomMap = false;
            _cachedMapDownloadUrl = string.Empty;
            _cachedMapContentHash = string.Empty;
        }

        return (_cachedIsCustomMap, _cachedMapDownloadUrl, _cachedMapContentHash);
    }

    private SnapshotMessage? TryGetBaselineSnapshot(ClientSession client, SnapshotMessage fullSnapshot)
    {
        if (client.LastAcknowledgedSnapshotFrame == 0
            || !client.TryGetSnapshotState(client.LastAcknowledgedSnapshotFrame, out var baseline))
        {
            return null;
        }

        return string.Equals(baseline.LevelName, fullSnapshot.LevelName, StringComparison.OrdinalIgnoreCase)
            && baseline.MapAreaIndex == fullSnapshot.MapAreaIndex
            && baseline.MapAreaCount == fullSnapshot.MapAreaCount
            ? baseline
            : null;
    }

    private SerializedSnapshot BuildBudgetedSnapshot(ClientSession client, SnapshotMessage fullSnapshot, SnapshotMessage? baseline)
    {
        var builder = new SnapshotBuilder(fullSnapshot, baseline?.Frame ?? 0);
        var snapshot = builder.Build();
        var payload = ProtocolCodec.Serialize(snapshot);
        var payloadSize = payload.Length;

        if (payloadSize > TargetSnapshotPayloadBytes)
        {
            TrimAuxiliaryCollections(builder);
            snapshot = builder.Build();
            payload = ProtocolCodec.Serialize(snapshot);
            payloadSize = payload.Length;
            if (payloadSize > TargetSnapshotPayloadBytes)
            {
                return new SerializedSnapshot(snapshot, payload);
            }
        }

        var contributions = BuildTransientContributions(client, fullSnapshot, baseline);
        foreach (var contribution in contributions.OrderByDescending(static entry => entry.Priority).ThenBy(static entry => entry.DistanceSquared))
        {
            var trialBuilder = builder.Clone();
            contribution.Apply(trialBuilder);
            var trialSnapshot = trialBuilder.Build();
            var trialPayload = ProtocolCodec.Serialize(trialSnapshot);
            if (trialPayload.Length > TargetSnapshotPayloadBytes)
            {
                continue;
            }

            builder = trialBuilder;
            snapshot = trialSnapshot;
            payload = trialPayload;
        }

        return new SerializedSnapshot(snapshot, payload);
    }

    private static void TrimAuxiliaryCollections(SnapshotBuilder builder)
    {
        builder.KillFeed.Clear();
        builder.CombatTraces.Clear();
        builder.VisualEvents.Clear();
        builder.SoundEvents.Clear();
    }

    private List<SnapshotContribution> BuildTransientContributions(
        ClientSession client,
        SnapshotMessage fullSnapshot,
        SnapshotMessage? baseline)
    {
        var focus = GetClientFocusPoint(client);
        var contributions = new List<SnapshotContribution>();

        AddEntityDelta(
            contributions,
            fullSnapshot.Sentries,
            baseline?.Sentries,
            priority: 1200,
            focus,
            static state => state.Id,
            static state => state.X,
            static state => state.Y,
            static (builder, state) => builder.Sentries.Add(state),
            static (builder, id) => builder.RemovedSentryIds.Add(id));
        AddEntityDelta(
            contributions,
            fullSnapshot.Rockets,
            baseline?.Rockets,
            priority: 1120,
            focus,
            static state => state.Id,
            static state => state.X,
            static state => state.Y,
            static (builder, state) => builder.Rockets.Add(state),
            static (builder, id) => builder.RemovedRocketIds.Add(id));
        AddEntityDelta(
            contributions,
            fullSnapshot.Flames,
            baseline?.Flames,
            priority: 1110,
            focus,
            static state => state.Id,
            static state => state.X,
            static state => state.Y,
            static (builder, state) => builder.Flames.Add(state),
            static (builder, id) => builder.RemovedFlameIds.Add(id));
        AddEntityDelta(
            contributions,
            fullSnapshot.Mines,
            baseline?.Mines,
            priority: 1100,
            focus,
            static state => state.Id,
            static state => state.X,
            static state => state.Y,
            static (builder, state) => builder.Mines.Add(state),
            static (builder, id) => builder.RemovedMineIds.Add(id));
        AddEntityDelta(
            contributions,
            fullSnapshot.Shots,
            baseline?.Shots,
            priority: 1080,
            focus,
            static state => state.Id,
            static state => state.X,
            static state => state.Y,
            static (builder, state) => builder.Shots.Add(state),
            static (builder, id) => builder.RemovedShotIds.Add(id));
        AddEntityDelta(
            contributions,
            fullSnapshot.Needles,
            baseline?.Needles,
            priority: 1070,
            focus,
            static state => state.Id,
            static state => state.X,
            static state => state.Y,
            static (builder, state) => builder.Needles.Add(state),
            static (builder, id) => builder.RemovedNeedleIds.Add(id));
        AddEntityDelta(
            contributions,
            fullSnapshot.RevolverShots,
            baseline?.RevolverShots,
            priority: 1060,
            focus,
            static state => state.Id,
            static state => state.X,
            static state => state.Y,
            static (builder, state) => builder.RevolverShots.Add(state),
            static (builder, id) => builder.RemovedRevolverShotIds.Add(id));
        AddEntityDelta(
            contributions,
            fullSnapshot.Bubbles,
            baseline?.Bubbles,
            priority: 1050,
            focus,
            static state => state.Id,
            static state => state.X,
            static state => state.Y,
            static (builder, state) => builder.Bubbles.Add(state),
            static (builder, id) => builder.RemovedBubbleIds.Add(id));
        AddEntityDelta(
            contributions,
            fullSnapshot.Blades,
            baseline?.Blades,
            priority: 1040,
            focus,
            static state => state.Id,
            static state => state.X,
            static state => state.Y,
            static (builder, state) => builder.Blades.Add(state),
            static (builder, id) => builder.RemovedBladeIds.Add(id));
        AddEntityDelta(
            contributions,
            fullSnapshot.DeadBodies,
            baseline?.DeadBodies,
            priority: 440,
            focus,
            static state => state.Id,
            static state => state.X,
            static state => state.Y,
            static (builder, state) => builder.DeadBodies.Add(state),
            static (builder, id) => builder.RemovedDeadBodyIds.Add(id));
        AddEntityDelta(
            contributions,
            fullSnapshot.PlayerGibs,
            baseline?.PlayerGibs,
            priority: 320,
            focus,
            static state => state.Id,
            static state => state.X,
            static state => state.Y,
            static (builder, state) => builder.PlayerGibs.Add(state),
            static (builder, id) => builder.RemovedPlayerGibIds.Add(id));
        AddEntityDelta(
            contributions,
            fullSnapshot.BloodDrops,
            baseline?.BloodDrops,
            priority: 240,
            focus,
            static state => state.Id,
            static state => state.X,
            static state => state.Y,
            static (builder, state) => builder.BloodDrops.Add(state),
            static (builder, id) => builder.RemovedBloodDropIds.Add(id));

        return contributions;
    }

    private (float X, float Y) GetClientFocusPoint(ClientSession client)
    {
        if (SimulationWorld.IsPlayableNetworkPlayerSlot(client.Slot)
            && _world.TryGetNetworkPlayer(client.Slot, out var player)
            && player.IsAlive)
        {
            return (player.X, player.Y);
        }

        var deathCam = _world.GetNetworkPlayerDeathCam(client.Slot);
        if (deathCam is not null)
        {
            return (deathCam.FocusX, deathCam.FocusY);
        }

        return (_world.Bounds.Width / 2f, _world.Bounds.Height / 2f);
    }

    private static void AddEntityDelta<T>(
        List<SnapshotContribution> contributions,
        IReadOnlyList<T> currentStates,
        IReadOnlyList<T>? baselineStates,
        int priority,
        (float X, float Y) focus,
        Func<T, int> idSelector,
        Func<T, float> xSelector,
        Func<T, float> ySelector,
        Action<SnapshotBuilder, T> addState,
        Action<SnapshotBuilder, int> addRemovedId)
    {
        var delta = DiffEntities(currentStates, baselineStates, idSelector);
        for (var index = 0; index < delta.RemovedIds.Count; index += 1)
        {
            var removedId = delta.RemovedIds[index];
            contributions.Add(new SnapshotContribution(
                priority + 100,
                DistanceSquared(focus.X, focus.Y, focus.X, focus.Y),
                builder => addRemovedId(builder, removedId)));
        }

        for (var index = 0; index < delta.UpdatedStates.Count; index += 1)
        {
            var state = delta.UpdatedStates[index];
            contributions.Add(new SnapshotContribution(
                priority,
                DistanceSquared(focus.X, focus.Y, xSelector(state), ySelector(state)),
                builder => addState(builder, state)));
        }
    }

    private static EntityDelta<T> DiffEntities<T>(
        IReadOnlyList<T> currentStates,
        IReadOnlyList<T>? baselineStates,
        Func<T, int> idSelector)
    {
        var updatedStates = new List<T>(currentStates.Count);
        if (baselineStates is null || baselineStates.Count == 0)
        {
            updatedStates.AddRange(currentStates);
            return new EntityDelta<T>(updatedStates, []);
        }

        var currentById = new Dictionary<int, T>(currentStates.Count);
        for (var index = 0; index < currentStates.Count; index += 1)
        {
            var state = currentStates[index];
            currentById[idSelector(state)] = state;
        }

        var baselineById = new Dictionary<int, T>(baselineStates.Count);
        for (var index = 0; index < baselineStates.Count; index += 1)
        {
            var state = baselineStates[index];
            baselineById[idSelector(state)] = state;
        }

        var removedIds = new List<int>();
        foreach (var baselineState in baselineStates)
        {
            var id = idSelector(baselineState);
            if (!currentById.ContainsKey(id))
            {
                removedIds.Add(id);
            }
        }

        for (var index = 0; index < currentStates.Count; index += 1)
        {
            var state = currentStates[index];
            var id = idSelector(state);
            if (!baselineById.TryGetValue(id, out var baselineState)
                || !EqualityComparer<T>.Default.Equals(state, baselineState))
            {
                updatedStates.Add(state);
            }
        }

        return new EntityDelta<T>(updatedStates, removedIds);
    }

    private static float DistanceSquared(float x1, float y1, float x2, float y2)
    {
        var deltaX = x2 - x1;
        var deltaY = y2 - y1;
        return (deltaX * deltaX) + (deltaY * deltaY);
    }

    private sealed record EntityDelta<T>(List<T> UpdatedStates, List<int> RemovedIds);

    private sealed record SnapshotContribution(int Priority, float DistanceSquared, Action<SnapshotBuilder> Apply);

    private sealed class SnapshotBuilder
    {
        private readonly SnapshotMessage _template;

        public SnapshotBuilder(SnapshotMessage template, ulong baselineFrame)
        {
            _template = template;
            BaselineFrame = baselineFrame;
            CombatTraces = new List<SnapshotCombatTraceState>(template.CombatTraces);
            KillFeed = new List<SnapshotKillFeedEntry>(template.KillFeed);
            VisualEvents = new List<SnapshotVisualEvent>(template.VisualEvents);
            SoundEvents = new List<SnapshotSoundEvent>(template.SoundEvents);
        }

        private SnapshotBuilder(SnapshotBuilder other)
        {
            _template = other._template;
            BaselineFrame = other.BaselineFrame;
            CombatTraces = new List<SnapshotCombatTraceState>(other.CombatTraces);
            KillFeed = new List<SnapshotKillFeedEntry>(other.KillFeed);
            VisualEvents = new List<SnapshotVisualEvent>(other.VisualEvents);
            SoundEvents = new List<SnapshotSoundEvent>(other.SoundEvents);
            Sentries = new List<SnapshotSentryState>(other.Sentries);
            Shots = new List<SnapshotShotState>(other.Shots);
            Bubbles = new List<SnapshotShotState>(other.Bubbles);
            Blades = new List<SnapshotShotState>(other.Blades);
            Needles = new List<SnapshotShotState>(other.Needles);
            RevolverShots = new List<SnapshotShotState>(other.RevolverShots);
            Rockets = new List<SnapshotRocketState>(other.Rockets);
            Flames = new List<SnapshotFlameState>(other.Flames);
            Mines = new List<SnapshotMineState>(other.Mines);
            PlayerGibs = new List<SnapshotPlayerGibState>(other.PlayerGibs);
            BloodDrops = new List<SnapshotBloodDropState>(other.BloodDrops);
            DeadBodies = new List<SnapshotDeadBodyState>(other.DeadBodies);
            RemovedSentryIds = new List<int>(other.RemovedSentryIds);
            RemovedShotIds = new List<int>(other.RemovedShotIds);
            RemovedBubbleIds = new List<int>(other.RemovedBubbleIds);
            RemovedBladeIds = new List<int>(other.RemovedBladeIds);
            RemovedNeedleIds = new List<int>(other.RemovedNeedleIds);
            RemovedRevolverShotIds = new List<int>(other.RemovedRevolverShotIds);
            RemovedRocketIds = new List<int>(other.RemovedRocketIds);
            RemovedFlameIds = new List<int>(other.RemovedFlameIds);
            RemovedMineIds = new List<int>(other.RemovedMineIds);
            RemovedPlayerGibIds = new List<int>(other.RemovedPlayerGibIds);
            RemovedBloodDropIds = new List<int>(other.RemovedBloodDropIds);
            RemovedDeadBodyIds = new List<int>(other.RemovedDeadBodyIds);
        }

        public ulong BaselineFrame { get; }
        public List<SnapshotCombatTraceState> CombatTraces { get; }
        public List<SnapshotKillFeedEntry> KillFeed { get; }
        public List<SnapshotVisualEvent> VisualEvents { get; }
        public List<SnapshotSoundEvent> SoundEvents { get; }
        public List<SnapshotSentryState> Sentries { get; } = new();
        public List<SnapshotShotState> Shots { get; } = new();
        public List<SnapshotShotState> Bubbles { get; } = new();
        public List<SnapshotShotState> Blades { get; } = new();
        public List<SnapshotShotState> Needles { get; } = new();
        public List<SnapshotShotState> RevolverShots { get; } = new();
        public List<SnapshotRocketState> Rockets { get; } = new();
        public List<SnapshotFlameState> Flames { get; } = new();
        public List<SnapshotMineState> Mines { get; } = new();
        public List<SnapshotPlayerGibState> PlayerGibs { get; } = new();
        public List<SnapshotBloodDropState> BloodDrops { get; } = new();
        public List<SnapshotDeadBodyState> DeadBodies { get; } = new();
        public List<int> RemovedSentryIds { get; } = new();
        public List<int> RemovedShotIds { get; } = new();
        public List<int> RemovedBubbleIds { get; } = new();
        public List<int> RemovedBladeIds { get; } = new();
        public List<int> RemovedNeedleIds { get; } = new();
        public List<int> RemovedRevolverShotIds { get; } = new();
        public List<int> RemovedRocketIds { get; } = new();
        public List<int> RemovedFlameIds { get; } = new();
        public List<int> RemovedMineIds { get; } = new();
        public List<int> RemovedPlayerGibIds { get; } = new();
        public List<int> RemovedBloodDropIds { get; } = new();
        public List<int> RemovedDeadBodyIds { get; } = new();

        public SnapshotBuilder Clone()
        {
            return new SnapshotBuilder(this);
        }

        public SnapshotMessage Build()
        {
            return _template with
            {
                BaselineFrame = BaselineFrame,
                IsDelta = true,
                CombatTraces = CombatTraces.ToArray(),
                Sentries = Sentries.ToArray(),
                Shots = Shots.ToArray(),
                Bubbles = Bubbles.ToArray(),
                Blades = Blades.ToArray(),
                Needles = Needles.ToArray(),
                RevolverShots = RevolverShots.ToArray(),
                Rockets = Rockets.ToArray(),
                Flames = Flames.ToArray(),
                Mines = Mines.ToArray(),
                PlayerGibs = PlayerGibs.ToArray(),
                BloodDrops = BloodDrops.ToArray(),
                DeadBodies = DeadBodies.ToArray(),
                KillFeed = KillFeed.ToArray(),
                VisualEvents = VisualEvents.ToArray(),
                SoundEvents = SoundEvents.ToArray(),
                RemovedSentryIds = RemovedSentryIds.ToArray(),
                RemovedShotIds = RemovedShotIds.ToArray(),
                RemovedBubbleIds = RemovedBubbleIds.ToArray(),
                RemovedBladeIds = RemovedBladeIds.ToArray(),
                RemovedNeedleIds = RemovedNeedleIds.ToArray(),
                RemovedRevolverShotIds = RemovedRevolverShotIds.ToArray(),
                RemovedRocketIds = RemovedRocketIds.ToArray(),
                RemovedFlameIds = RemovedFlameIds.ToArray(),
                RemovedMineIds = RemovedMineIds.ToArray(),
                RemovedPlayerGibIds = RemovedPlayerGibIds.ToArray(),
                RemovedBloodDropIds = RemovedBloodDropIds.ToArray(),
                RemovedDeadBodyIds = RemovedDeadBodyIds.ToArray(),
            };
        }
    }
}
