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
        var contributions = OpenGarrison.Server.SnapshotContributionPlanner.BuildContributions(client, fullSnapshot, baseline, _world);
        var snapshot = OpenGarrison.Server.SnapshotDeltaBudgeter.BuildBudgetedSnapshot(fullSnapshot, baseline, contributions);
        return new SerializedSnapshot(snapshot.Message, snapshot.Payload);
    }
}
