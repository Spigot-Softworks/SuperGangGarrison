using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using OpenGarrison.Server;
using static ServerHelpers;

sealed class SnapshotBroadcaster
{
    private readonly record struct SerializedSnapshot(SnapshotMessage Message, byte[] Payload);

    private readonly SimulationWorld _world;
    private readonly SimulationConfig _config;
    private readonly Dictionary<byte, ClientSession> _clientsBySlot;
    private readonly Action<IPEndPoint, SnapshotMessage, byte[]> _sendSnapshot;
    private readonly OpenGarrison.Server.ServerMapMetadataResolver _mapMetadataResolver;
    private readonly OpenGarrison.Server.SnapshotTransientEventBuffer _transientEventBuffer;

    public SnapshotBroadcaster(
        SimulationWorld world,
        SimulationConfig config,
        Dictionary<byte, ClientSession> clientsBySlot,
        ulong transientEventReplayTicks,
        OpenGarrison.Server.ServerMapMetadataResolver mapMetadataResolver,
        Action<IPEndPoint, SnapshotMessage, byte[]> sendSnapshot)
    {
        _world = world;
        _config = config;
        _clientsBySlot = clientsBySlot;
        _mapMetadataResolver = mapMetadataResolver;
        _transientEventBuffer = new OpenGarrison.Server.SnapshotTransientEventBuffer(transientEventReplayTicks);
        _sendSnapshot = sendSnapshot;
    }

    public void ResetTransientEvents()
    {
        _transientEventBuffer.Reset(_clientsBySlot.Values);
    }

    public void BroadcastSnapshot()
    {
        if (_clientsBySlot.Count == 0)
        {
            return;
        }

        var transientEvents = _transientEventBuffer.CaptureCurrentEvents(_world);
        foreach (var client in _clientsBySlot.Values)
        {
            SendSnapshot(client, transientEvents.VisualEvents, transientEvents.DamageEvents, transientEvents.SoundEvents);
        }
    }

    private void SendSnapshot(
        ClientSession client,
        SnapshotVisualEvent[] visualEvents,
        SnapshotDamageEvent[] damageEvents,
        SnapshotSoundEvent[] soundEvents)
    {
        var fullSnapshot = CaptureFullSnapshot(client, visualEvents, damageEvents, soundEvents);
        var fullSnapshotPayload = ProtocolCodec.Serialize(fullSnapshot);
        if (fullSnapshotPayload.Length <= OpenGarrison.Server.SnapshotDeltaBudgeter.TargetSnapshotPayloadBytes)
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
        SnapshotDamageEvent[] damageEvents,
        SnapshotSoundEvent[] soundEvents)
    {
        PlayerEntity? viewer = null;
        if (SimulationWorld.IsPlayableNetworkPlayerSlot(client.Slot)
            && !_world.IsNetworkPlayerAwaitingJoin(client.Slot)
            && _world.TryGetNetworkPlayer(client.Slot, out var viewerPlayer))
        {
            viewer = viewerPlayer;
        }

        var players = new List<SnapshotPlayerState>(_clientsBySlot.Count);
        foreach (var entry in _clientsBySlot.OrderBy(static entry => entry.Key))
        {
            if (IsSpectatorSlot(entry.Key))
            {
                players.Add(CreateSpectatorSnapshotPlayerState(entry.Value));
                continue;
            }

            if (_world.TryGetNetworkPlayer(entry.Key, out var player))
            {
                players.Add(ToSnapshotPlayerState(_world, entry.Key, player, viewer));
            }
        }

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
            _world.Flares.Select(ToSnapshotFlareState).ToArray(),
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
            damageEvents,
            soundEvents,
            mapMetadata.IsCustomMap,
            mapMetadata.MapDownloadUrl,
            mapMetadata.MapContentHash)
        {
            SentryGibs = _world.SentryGibs.Select(ToSnapshotSentryGibState).ToArray(),
        };
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
            IsUbered: false,
            IsHeavyEating: false,
            HeavyEatTicksRemaining: 0,
            IsSniperScoped: false,
            SniperChargeTicks: 0,
            FacingDirectionX: 1f,
            AimDirectionDegrees: 0f,
            IsTaunting: false,
            TauntFrameIndex: 0f,
            IsChatBubbleVisible: false,
            ChatBubbleFrameIndex: 0,
            ChatBubbleAlpha: 0f);
    }

    private (bool IsCustomMap, string MapDownloadUrl, string MapContentHash) GetCurrentMapMetadata()
    {
        return _mapMetadataResolver.GetCurrentMapMetadata();
    }

    private static SnapshotMessage? TryGetBaselineSnapshot(ClientSession client, SnapshotMessage fullSnapshot)
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
        var contributions = BuildTransientContributions(client, fullSnapshot, baseline);
        var snapshot = OpenGarrison.Server.SnapshotDeltaBudgeter.BuildBudgetedSnapshot(fullSnapshot, baseline, contributions);
        return new SerializedSnapshot(snapshot.Message, snapshot.Payload);
    }

    private List<OpenGarrison.Server.SnapshotDeltaBudgeter.Contribution> BuildTransientContributions(
        ClientSession client,
        SnapshotMessage fullSnapshot,
        SnapshotMessage? baseline)
    {
        var focus = GetClientFocusPoint(client);
        var contributions = new List<OpenGarrison.Server.SnapshotDeltaBudgeter.Contribution>();

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
            fullSnapshot.Flares,
            baseline?.Flares,
            priority: 1105,
            focus,
            static state => state.Id,
            static state => state.X,
            static state => state.Y,
            static (builder, state) => builder.Flares.Add(state),
            static (builder, id) => builder.RemovedFlareIds.Add(id));
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
            fullSnapshot.SentryGibs,
            baseline?.SentryGibs,
            priority: 360,
            focus,
            static state => state.Id,
            static state => state.X,
            static state => state.Y,
            static (builder, state) => builder.SentryGibs.Add(state),
            static (builder, id) => builder.RemovedSentryGibIds.Add(id));
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
        AddPointEventContributions(
            contributions,
            fullSnapshot.SoundEvents,
            priority: 1300,
            focus,
            static state => state.X,
            static state => state.Y,
            static (builder, state) => builder.SoundEvents.Add(state));
        AddPointEventContributions(
            contributions,
            fullSnapshot.VisualEvents,
            priority: 1290,
            focus,
            static state => state.X,
            static state => state.Y,
            static (builder, state) => builder.VisualEvents.Add(state));
        AddPointEventContributions(
            contributions,
            fullSnapshot.DamageEvents,
            priority: 1285,
            focus,
            static state => state.X,
            static state => state.Y,
            static (builder, state) => builder.DamageEvents.Add(state));
        AddOrderedContributions(
            contributions,
            fullSnapshot.KillFeed,
            priority: 1180,
            static (builder, entry) => builder.KillFeed.Add(entry));

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
        List<OpenGarrison.Server.SnapshotDeltaBudgeter.Contribution> contributions,
        IReadOnlyList<T> currentStates,
        IReadOnlyList<T>? baselineStates,
        int priority,
        (float X, float Y) focus,
        Func<T, int> idSelector,
        Func<T, float> xSelector,
        Func<T, float> ySelector,
        Action<OpenGarrison.Server.SnapshotDeltaBudgeter.Builder, T> addState,
        Action<OpenGarrison.Server.SnapshotDeltaBudgeter.Builder, int> addRemovedId)
    {
        var delta = DiffEntities(currentStates, baselineStates, idSelector);
        for (var index = 0; index < delta.RemovedIds.Count; index += 1)
        {
            var removedId = delta.RemovedIds[index];
            contributions.Add(new OpenGarrison.Server.SnapshotDeltaBudgeter.Contribution(
                priority + 100,
                DistanceSquared(focus.X, focus.Y, focus.X, focus.Y),
                builder => addRemovedId(builder, removedId)));
        }

        for (var index = 0; index < delta.UpdatedStates.Count; index += 1)
        {
            var state = delta.UpdatedStates[index];
            contributions.Add(new OpenGarrison.Server.SnapshotDeltaBudgeter.Contribution(
                priority,
                DistanceSquared(focus.X, focus.Y, xSelector(state), ySelector(state)),
                builder => addState(builder, state)));
        }
    }

    private static void AddPointEventContributions<T>(
        List<OpenGarrison.Server.SnapshotDeltaBudgeter.Contribution> contributions,
        IReadOnlyList<T> states,
        int priority,
        (float X, float Y) focus,
        Func<T, float> xSelector,
        Func<T, float> ySelector,
        Action<OpenGarrison.Server.SnapshotDeltaBudgeter.Builder, T> addState)
    {
        for (var index = states.Count - 1; index >= 0; index -= 1)
        {
            var state = states[index];
            contributions.Add(new OpenGarrison.Server.SnapshotDeltaBudgeter.Contribution(
                priority - ((states.Count - 1) - index),
                DistanceSquared(focus.X, focus.Y, xSelector(state), ySelector(state)),
                builder => addState(builder, state)));
        }
    }

    private static void AddOrderedContributions<T>(
        List<OpenGarrison.Server.SnapshotDeltaBudgeter.Contribution> contributions,
        IReadOnlyList<T> states,
        int priority,
        Action<OpenGarrison.Server.SnapshotDeltaBudgeter.Builder, T> addState)
    {
        for (var index = states.Count - 1; index >= 0; index -= 1)
        {
            var state = states[index];
            contributions.Add(new OpenGarrison.Server.SnapshotDeltaBudgeter.Contribution(
                priority - ((states.Count - 1) - index),
                0f,
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
}
