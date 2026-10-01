#nullable enable

using Microsoft.Xna.Framework;
using System.Diagnostics;
using OpenGarrison.Client.Plugins;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using ClientPluginDamageTargetKind = OpenGarrison.Client.Plugins.DamageTargetKind;
using CoreDamageTargetKind = OpenGarrison.Core.DamageTargetKind;

using static OpenGarrison.Client.Game1;

namespace OpenGarrison.Client;

public sealed class ClientPluginEventController
    {
        private readonly IPluginContext _context;
        private readonly List<OpenGarrison.Protocol.SnapshotDamageEvent> _pendingNetworkDamageEvents = new();
        private readonly Queue<ulong> _processedNetworkDamageEventOrder = new();
        private readonly HashSet<ulong> _processedNetworkDamageEventIds = new();
        private ValueTuple<bool, bool, OpenGarrison.Client.Plugins.ClientPluginTeam, float, float, float> _clientPluginPreviousBlueIntelState;
        private readonly Dictionary<OpenGarrison.Core.PlayerTeam, ValueTuple<int, int, bool>> _clientPluginPreviousGeneratorStates = new();
        private int _clientPluginPreviousKillFeedCount;
        private bool _clientPluginPreviousLocalAlive;
        private int _clientPluginPreviousLocalAmmo;
        private bool _clientPluginPreviousLocalBurning;
        private bool _clientPluginPreviousLocalCarryingIntel;
        private int _clientPluginPreviousLocalPrimaryCooldownTicks;
        private OpenGarrison.Client.Plugins.ClientRoundPhase _clientPluginPreviousMatchPhase;
        private readonly Dictionary<int, ValueTuple<OpenGarrison.Client.Plugins.ClientPluginTeam, OpenGarrison.Client.Plugins.ClientPluginTeam, float, bool>> _clientPluginPreviousObjectiveStates = new();
        private ValueTuple<bool, bool, OpenGarrison.Client.Plugins.ClientPluginTeam, float, float, float> _clientPluginPreviousRedIntelState;

        public ClientPluginEventController(IPluginContext context)
        {
            _context = context;
        }

        internal void QueuePendingNetworkDamageEvent(SnapshotDamageEvent damageEvent)
        {
            _pendingNetworkDamageEvents.Add(damageEvent);
        }

        internal void ClearPendingNetworkDamageEvents()
        {
            _pendingNetworkDamageEvents.Clear();
        }

        internal void ResetProcessedNetworkDamageEventHistory()
        {
            _processedNetworkDamageEventIds.Clear();
            _processedNetworkDamageEventOrder.Clear();
        }

        public void QueueResolvedSnapshotDamageEvents(SnapshotMessage resolvedSnapshot)
        {
            for (var damageIndex = 0; damageIndex < resolvedSnapshot.DamageEvents.Count; damageIndex += 1)
            {
                var damageEvent = resolvedSnapshot.DamageEvents[damageIndex];
                if (!ShouldProcessNetworkEvent(damageEvent.EventId, _processedNetworkDamageEventIds, _processedNetworkDamageEventOrder))
                {
                    continue;
                }

                QueuePendingNetworkDamageEvent(damageEvent);
                SpawnClientDamageVisuals(damageEvent);
                _context.QueueImmediateNetworkDeathPresentation(resolvedSnapshot, damageEvent);
            }
        }

        public void DispatchClientSemanticGameplayEvents()
        {
            var dispatchStartTimestamp = _context.IsClientPerformanceDiagnosticsEnabled() ? Stopwatch.GetTimestamp() : 0L;
            DispatchPendingDamageEventsToPlugins();
            DispatchPendingHealingEventsToPlugins();
            DispatchClientRoundPhaseEvents();
            DispatchClientLocalPlayerStateEvents();
            DispatchClientObjectiveEvents();
            DispatchClientKillFeedEvents();
            if (dispatchStartTimestamp > 0)
            {
                _context.RecordClientPerformanceMetric(ClientPerformanceMetric.PluginEvents, Game1.GetDiagnosticsElapsedMilliseconds(dispatchStartTimestamp));
            }
        }

        public void DispatchPendingDamageEventsToPlugins()
        {
            DispatchPendingDamageEventsToPluginsCore();
        }

        public void ResetClientPluginGameplayEventState()
        {
            _clientPluginPreviousMatchPhase = ToClientRoundPhase(_context._world.MatchState.Phase);
            _clientPluginPreviousLocalAlive = _context._world.LocalPlayer.IsAlive;
            _clientPluginPreviousLocalAmmo = _context._world.LocalPlayer.CurrentShells;
            _clientPluginPreviousLocalPrimaryCooldownTicks = _context._world.LocalPlayer.PrimaryCooldownTicks;
            _clientPluginPreviousLocalCarryingIntel = _context._world.LocalPlayer.IsCarryingIntel;
            _clientPluginPreviousLocalBurning = _context._world.LocalPlayer.IsBurning;
            _clientPluginPreviousKillFeedCount = _context._world.KillFeed.Count;
            _clientPluginPreviousObjectiveStates.Clear();
            _clientPluginPreviousGeneratorStates.Clear();
            for (var index = 0; index < _context._world.ControlPoints.Count; index += 1)
            {
                var point = _context._world.ControlPoints[index];
                _clientPluginPreviousObjectiveStates[point.Index] = (
                    ToClientPluginTeam(point.Team),
                    ToClientPluginTeam(point.CappingTeam),
                    point.CapTimeTicks <= 0 ? 0f : Math.Clamp(point.CappingTicks / point.CapTimeTicks, 0f, 1f),
                    point.IsLocked);
            }

            _clientPluginPreviousRedIntelState = CaptureIntelState(_context._world.RedIntel, PlayerTeam.Red);
            _clientPluginPreviousBlueIntelState = CaptureIntelState(_context._world.BlueIntel, PlayerTeam.Blue);
            for (var index = 0; index < _context._world.Generators.Count; index += 1)
            {
                var generator = _context._world.Generators[index];
                _clientPluginPreviousGeneratorStates[generator.Team] = (
                    generator.Health,
                    generator.MaxHealth,
                    generator.IsDestroyed);
            }
        }

        private void DispatchPendingDamageEventsToPluginsCore()
        {
            var localDamageEvents = _context._world.DrainPendingDamageEvents();
            for (var index = 0; index < localDamageEvents.Count; index += 1)
            {
                var damageEvent = localDamageEvents[index];
                TryTrackLastToDieDamageDealt(damageEvent.AttackerPlayerId, damageEvent.Amount);
                if (!_context._networkClient.IsConnected)
                {
                    _context.ObserveCivvieUmbrellaShieldBlockDamageEvent(damageEvent);
                }

                _context.ObserveEvasionMissDamageEvent(damageEvent);
                _context.ObserveHeavyDashDodgeDamageEvent(damageEvent);
                _context.ObserveDynamicMusicDamageEvent(damageEvent);
                TryTriggerLocalPortraitDamageFeedback(damageEvent);
                TryTriggerLocalDamageVignette(damageEvent);

                if (ShouldSpawnClientBloodFromDamage(damageEvent.TargetKind, damageEvent.Amount))
                {
                    _context._world.SpawnClientBloodFromDamage(damageEvent.X, damageEvent.Y, damageEvent.Amount);
                }
            }

            for (var index = 0; index < _pendingNetworkDamageEvents.Count; index += 1)
            {
                var damageEvent = _pendingNetworkDamageEvents[index];
                TryTrackLastToDieDamageDealt(damageEvent.AttackerPlayerId, damageEvent.Amount);
                _context.ObserveEvasionMissDamageEvent(damageEvent);
                _context.ObserveHeavyDashDodgeDamageEvent(damageEvent);
                _context.ObserveDynamicMusicDamageEvent(damageEvent);
                TryTriggerLocalPortraitDamageFeedback(damageEvent);
                TryTriggerLocalDamageVignette(damageEvent);
            }

            if (_context._clientPluginHost is null)
            {
                _pendingNetworkDamageEvents.Clear();
                return;
            }

            var localPlayerId = _context.GetClientPluginLocalPlayerId();
            if (localPlayerId.HasValue)
            {
                if (!_context._networkClient.IsConnected)
                {
                    for (var index = 0; index < localDamageEvents.Count; index += 1)
                    {
                        TryDispatchLocalDamageEvent(localPlayerId.Value, localDamageEvents[index]);
                    }
                }

                for (var index = 0; index < _pendingNetworkDamageEvents.Count; index += 1)
                {
                    TryDispatchLocalDamageEvent(localPlayerId.Value, _pendingNetworkDamageEvents[index]);
                }
            }

            _pendingNetworkDamageEvents.Clear();
        }

        private bool ShouldSpawnClientBloodFromDamage(CoreDamageTargetKind targetKind, int damageAmount)
        {
            // Squib mode draws its own client squirts from visual events — skip legacy BloodDropEntity.
            return _context.AreBloodVisualsEnabled
                && _context.GameplayRuntimeSettings.BloodRenderMode != 0
                && targetKind == CoreDamageTargetKind.Player
                && damageAmount > 0;
        }

        private void SpawnClientDamageVisuals(SnapshotDamageEvent damageEvent)
        {
            if (ShouldSpawnClientBloodFromDamage((CoreDamageTargetKind)damageEvent.TargetKind, damageEvent.Amount))
            {
                _context._world.SpawnClientBloodFromDamage(damageEvent.X, damageEvent.Y, damageEvent.Amount);
            }
        }

        private void DispatchPendingHealingEventsToPlugins()
        {
            var pluginHost = _context._clientPluginHost;
            var localPlayerId = _context.GetClientPluginLocalPlayerId();
            var healingEvents = _context._world.DrainPendingHealingEvents();
            if (pluginHost is null || !localPlayerId.HasValue)
            {
                return;
            }

            for (var index = 0; index < healingEvents.Count; index += 1)
            {
                var healingEvent = healingEvents[index];
                if (healingEvent.TargetPlayerId != localPlayerId.Value || healingEvent.Amount <= 0)
                {
                    continue;
                }

                pluginHost.NotifyHeal(new ClientHealEvent(
                    healingEvent.Amount,
                    _context._world.LocalPlayer.Health,
                    _context._world.LocalPlayer.MaxHealth,
                    healingEvent.SourceFrame));
            }
        }

        private void TryTrackLastToDieDamageDealt(int attackerPlayerId, int amount)
        {
            var localPlayerId = _context.GetClientPluginLocalPlayerId();
            if (!localPlayerId.HasValue || attackerPlayerId != localPlayerId.Value)
            {
                return;
            }

            _context.RegisterLastToDieLocalDamageDealt(amount);
        }

        private void TryTriggerLocalPortraitDamageFeedback(WorldDamageEvent damageEvent)
        {
            var localPlayerId = _context.GetClientPluginLocalPlayerId();
            if (!localPlayerId.HasValue
                || damageEvent.Amount <= 0
                || damageEvent.TargetKind != CoreDamageTargetKind.Player
                || damageEvent.TargetEntityId != localPlayerId.Value)
            {
                return;
            }

            _context.TriggerLocalHudPortraitDamageFeedback(damageEvent.Amount);
        }

        private void TryTriggerLocalPortraitDamageFeedback(SnapshotDamageEvent damageEvent)
        {
            var localPlayerId = _context.GetClientPluginLocalPlayerId();
            if (!localPlayerId.HasValue
                || damageEvent.Amount <= 0
                || damageEvent.TargetKind != (byte)ClientPluginDamageTargetKind.Player
                || damageEvent.TargetEntityId != localPlayerId.Value)
            {
                return;
            }

            _context.TriggerLocalHudPortraitDamageFeedback(damageEvent.Amount);
        }

        private void TryTriggerLocalDamageVignette(WorldDamageEvent damageEvent)
        {
            var localPlayerId = _context.GetClientPluginLocalPlayerId();
            if (!localPlayerId.HasValue
                || damageEvent.Amount <= 0
                || damageEvent.TargetKind != CoreDamageTargetKind.Player
                || damageEvent.TargetEntityId != localPlayerId.Value)
            {
                return;
            }

            _context.TriggerLocalHudDamageVignette(damageEvent.Amount);
        }

        private void TryTriggerLocalDamageVignette(SnapshotDamageEvent damageEvent)
        {
            var localPlayerId = _context.GetClientPluginLocalPlayerId();
            if (!localPlayerId.HasValue
                || damageEvent.Amount <= 0
                || damageEvent.TargetKind != (byte)ClientPluginDamageTargetKind.Player
                || damageEvent.TargetEntityId != localPlayerId.Value)
            {
                return;
            }

            _context.TriggerLocalHudDamageVignette(damageEvent.Amount);
        }

        private void TryDispatchLocalDamageEvent(int localPlayerId, WorldDamageEvent damageEvent)
        {
            var dealtByLocalPlayer = damageEvent.AttackerPlayerId == localPlayerId;
            var receivedByLocalPlayer = damageEvent.TargetKind == CoreDamageTargetKind.Player && damageEvent.TargetEntityId == localPlayerId;
            var isSelfDamage = damageEvent.TargetKind == CoreDamageTargetKind.Player
                && damageEvent.AttackerPlayerId >= 0
                && damageEvent.AttackerPlayerId == damageEvent.TargetEntityId;
            var assistedByLocalPlayer = !isSelfDamage && damageEvent.AssistedByPlayerId == localPlayerId;
            if (!dealtByLocalPlayer && !assistedByLocalPlayer && !receivedByLocalPlayer)
            {
                return;
            }

            var pluginEvent = new LocalDamageEvent(
                damageEvent.Amount,
                (ClientPluginDamageTargetKind)damageEvent.TargetKind,
                damageEvent.TargetEntityId,
                new Vector2(damageEvent.X, damageEvent.Y),
                damageEvent.WasFatal,
                dealtByLocalPlayer && !isSelfDamage,
                assistedByLocalPlayer,
                receivedByLocalPlayer,
                damageEvent.AttackerPlayerId,
                damageEvent.AssistedByPlayerId,
                (LocalDamageFlags)damageEvent.Flags);
            _context._clientPluginHost?.NotifyLocalDamage(pluginEvent);
            if (ShouldNotifyHitConfirmed(dealtByLocalPlayer, isSelfDamage, assistedByLocalPlayer, pluginEvent.Flags))
            {
                _context._clientPluginHost?.NotifyHitConfirmed(new ClientHitConfirmedEvent(
                    pluginEvent.Amount,
                    pluginEvent.TargetKind,
                    pluginEvent.TargetEntityId,
                    pluginEvent.TargetWorldPosition,
                    pluginEvent.TargetWasKilled,
                    pluginEvent.AttackerPlayerId,
                    pluginEvent.AssistedByPlayerId,
                    pluginEvent.Flags,
                    (ulong)Math.Max(0, _context._world.Frame)));
            }
        }

        private void TryDispatchLocalDamageEvent(int localPlayerId, SnapshotDamageEvent damageEvent)
        {
            var dealtByLocalPlayer = damageEvent.AttackerPlayerId == localPlayerId;
            var receivedByLocalPlayer = damageEvent.TargetKind == (byte)ClientPluginDamageTargetKind.Player && damageEvent.TargetEntityId == localPlayerId;
            var isSelfDamage = damageEvent.TargetKind == (byte)ClientPluginDamageTargetKind.Player
                && damageEvent.AttackerPlayerId >= 0
                && damageEvent.AttackerPlayerId == damageEvent.TargetEntityId;
            var assistedByLocalPlayer = !isSelfDamage && damageEvent.AssistedByPlayerId == localPlayerId;
            if (!dealtByLocalPlayer && !assistedByLocalPlayer && !receivedByLocalPlayer)
            {
                return;
            }

            var pluginEvent = new LocalDamageEvent(
                damageEvent.Amount,
                (ClientPluginDamageTargetKind)damageEvent.TargetKind,
                damageEvent.TargetEntityId,
                new Vector2(damageEvent.X, damageEvent.Y),
                damageEvent.WasFatal,
                dealtByLocalPlayer && !isSelfDamage,
                assistedByLocalPlayer,
                receivedByLocalPlayer,
                damageEvent.AttackerPlayerId,
                damageEvent.AssistedByPlayerId,
                (LocalDamageFlags)damageEvent.Flags);
            _context._clientPluginHost?.NotifyLocalDamage(pluginEvent);
            if (ShouldNotifyHitConfirmed(dealtByLocalPlayer, isSelfDamage, assistedByLocalPlayer, pluginEvent.Flags))
            {
                _context._clientPluginHost?.NotifyHitConfirmed(new ClientHitConfirmedEvent(
                    pluginEvent.Amount,
                    pluginEvent.TargetKind,
                    pluginEvent.TargetEntityId,
                    pluginEvent.TargetWorldPosition,
                    pluginEvent.TargetWasKilled,
                    pluginEvent.AttackerPlayerId,
                    pluginEvent.AssistedByPlayerId,
                    pluginEvent.Flags,
                    (ulong)Math.Max(0, _context._world.Frame)));
            }
        }

        private static bool ShouldNotifyHitConfirmed(
            bool dealtByLocalPlayer,
            bool isSelfDamage,
            bool assistedByLocalPlayer,
            LocalDamageFlags flags)
        {
            if (dealtByLocalPlayer && !isSelfDamage)
            {
                return true;
            }

            return assistedByLocalPlayer && !flags.HasFlag(LocalDamageFlags.AfterburnTick);
        }

        private void DispatchClientRoundPhaseEvents()
        {
            if (_context._clientPluginHost is null)
            {
                _clientPluginPreviousMatchPhase = ToClientRoundPhase(_context._world.MatchState.Phase);
                return;
            }

            var currentPhase = ToClientRoundPhase(_context._world.MatchState.Phase);
            if (currentPhase != _clientPluginPreviousMatchPhase)
            {
                _context._clientPluginHost.NotifyRoundPhaseChanged(new ClientRoundPhaseChangedEvent(
                    _clientPluginPreviousMatchPhase,
                    currentPhase,
                    (ulong)Math.Max(0, _context._world.Frame)));
                _clientPluginPreviousMatchPhase = currentPhase;
            }
        }

        private void DispatchClientLocalPlayerStateEvents()
        {
            var pluginHost = _context._clientPluginHost;
            var localPlayer = _context._world.LocalPlayer;
            if (pluginHost is null || _context.IsLocalSpectatorPresentationActive())
            {
                _clientPluginPreviousLocalAlive = localPlayer.IsAlive;
                _clientPluginPreviousLocalAmmo = localPlayer.CurrentShells;
                _clientPluginPreviousLocalPrimaryCooldownTicks = localPlayer.PrimaryCooldownTicks;
                _clientPluginPreviousLocalCarryingIntel = localPlayer.IsCarryingIntel;
                _clientPluginPreviousLocalBurning = localPlayer.IsBurning;
                return;
            }

            if (_clientPluginPreviousLocalAlive && !localPlayer.IsAlive)
            {
                var latestEntry = FindLatestKillFeedEntryForVictim(localPlayer.Id);
                pluginHost.NotifyLocalDeath(new ClientLocalDeathEvent(
                    latestEntry?.KillerPlayerId ?? -1,
                    latestEntry?.KillerName ?? string.Empty,
                    ToClientPluginTeam(latestEntry?.KillerTeam),
                    latestEntry?.WeaponSpriteName ?? string.Empty,
                    latestEntry?.MessageText ?? string.Empty,
                    (ulong)Math.Max(0, _context._world.Frame)));
            }

            if (localPlayer.IsAlive)
            {
                var firedShot = localPlayer.CurrentShells < _clientPluginPreviousLocalAmmo
                    || (_clientPluginPreviousLocalPrimaryCooldownTicks <= 0 && localPlayer.PrimaryCooldownTicks > 0);
                if (firedShot)
                {
                    pluginHost.NotifyShotFired(new ClientShotFiredEvent(
                        _context.GetClientPluginLocalPlayerId(),
                        ToClientPluginClass(localPlayer.ClassId),
                        new Vector2(localPlayer.X, localPlayer.Y),
                        (ulong)Math.Max(0, _context._world.Frame)));
                }

                if (!_clientPluginPreviousLocalCarryingIntel && localPlayer.IsCarryingIntel)
                {
                    pluginHost.NotifyPickup(new ClientPickupEvent(
                        ClientGameplayPickupKind.Intel,
                        new Vector2(localPlayer.X, localPlayer.Y),
                        (ulong)Math.Max(0, _context._world.Frame)));
                }
            }

            if (!_clientPluginPreviousLocalBurning && localPlayer.IsBurning)
            {
                pluginHost.NotifyIgnited(new ClientIgniteEvent(localPlayer.BurnedByPlayerId ?? -1, localPlayer.BurnIntensity, (ulong)Math.Max(0, _context._world.Frame)));
            }
            else if (_clientPluginPreviousLocalBurning && !localPlayer.IsBurning)
            {
                pluginHost.NotifyExtinguished(new ClientExtinguishEvent((ulong)Math.Max(0, _context._world.Frame)));
            }

            _clientPluginPreviousLocalAlive = localPlayer.IsAlive;
            _clientPluginPreviousLocalAmmo = localPlayer.CurrentShells;
            _clientPluginPreviousLocalPrimaryCooldownTicks = localPlayer.PrimaryCooldownTicks;
            _clientPluginPreviousLocalCarryingIntel = localPlayer.IsCarryingIntel;
            _clientPluginPreviousLocalBurning = localPlayer.IsBurning;
        }

        private void DispatchClientObjectiveEvents()
        {
            var pluginHost = _context._clientPluginHost;
            if (pluginHost is null)
            {
                return;
            }

            for (var index = 0; index < _context._world.ControlPoints.Count; index += 1)
            {
                var point = _context._world.ControlPoints[index];
                var currentState = (
                    ToClientPluginTeam(point.Team),
                    ToClientPluginTeam(point.CappingTeam),
                    point.CapTimeTicks <= 0 ? 0f : Math.Clamp(point.CappingTicks / point.CapTimeTicks, 0f, 1f),
                    point.IsLocked);
                var previousState = _clientPluginPreviousObjectiveStates.GetValueOrDefault(point.Index, currentState);
                if (!Equals(previousState, currentState))
                {
                    pluginHost.NotifyObjectiveStateChanged(new ClientObjectiveStateEvent(
                        ClientObjectiveEventKind.ControlPoint,
                        point.Index,
                        currentState.Item1,
                        currentState.Item2,
                        currentState.Item3,
                        currentState.Item4,
                        new Vector2(point.Marker.CenterX, point.Marker.CenterY),
                        (ulong)Math.Max(0, _context._world.Frame)));
                }

                _clientPluginPreviousObjectiveStates[point.Index] = currentState;
            }

            DispatchIntelStateEvent(pluginHost, _context._world.RedIntel, PlayerTeam.Red, ref _clientPluginPreviousRedIntelState);
            DispatchIntelStateEvent(pluginHost, _context._world.BlueIntel, PlayerTeam.Blue, ref _clientPluginPreviousBlueIntelState);

            for (var index = 0; index < _context._world.Generators.Count; index += 1)
            {
                var generator = _context._world.Generators[index];
                var currentState = (
                    generator.Health,
                    generator.MaxHealth,
                    generator.IsDestroyed);
                var previousState = _clientPluginPreviousGeneratorStates.GetValueOrDefault(generator.Team, currentState);
                if (!Equals(previousState, currentState))
                {
                    pluginHost.NotifyGeneratorStateChanged(new ClientGeneratorStateEvent(
                        ToClientPluginTeam(generator.Team),
                        generator.Health,
                        generator.MaxHealth,
                        generator.IsDestroyed,
                        new Vector2(generator.Marker.CenterX, generator.Marker.CenterY),
                        (ulong)Math.Max(0, _context._world.Frame)));
                }

                _clientPluginPreviousGeneratorStates[generator.Team] = currentState;
            }
        }

        private void DispatchClientKillFeedEvents()
        {
            var pluginHost = _context._clientPluginHost;
            if (pluginHost is null)
            {
                _clientPluginPreviousKillFeedCount = _context._world.KillFeed.Count;
                return;
            }

            if (_context._world.KillFeed.Count < _clientPluginPreviousKillFeedCount)
            {
                _clientPluginPreviousKillFeedCount = 0;
            }

            var localPlayerId = _context.GetClientPluginLocalPlayerId();
            for (var index = _clientPluginPreviousKillFeedCount; index < _context._world.KillFeed.Count; index += 1)
            {
                var entry = _context._world.KillFeed[index];
                var killFeedEvent = new ClientKillFeedEvent(
                    entry.KillerPlayerId,
                    entry.KillerName,
                    ToClientPluginTeam(entry.KillerTeam),
                    entry.VictimPlayerId,
                    entry.VictimName,
                    ToClientPluginTeam(entry.VictimTeam),
                    entry.WeaponSpriteName,
                    entry.MessageText,
                    (ulong)Math.Max(0, _context._world.Frame));
                pluginHost.NotifyKillFeed(killFeedEvent);
                if (localPlayerId.HasValue && entry.KillerPlayerId == localPlayerId.Value)
                {
                    pluginHost.NotifyLocalKill(new ClientLocalKillEvent(
                        entry.VictimPlayerId,
                        entry.VictimName,
                        ToClientPluginTeam(entry.VictimTeam),
                        entry.WeaponSpriteName,
                        entry.MessageText,
                        (ulong)Math.Max(0, _context._world.Frame)));
                }
            }

            _clientPluginPreviousKillFeedCount = _context._world.KillFeed.Count;
        }

        private KillFeedEntry? FindLatestKillFeedEntryForVictim(int victimPlayerId)
        {
            for (var index = _context._world.KillFeed.Count - 1; index >= 0; index -= 1)
            {
                if (_context._world.KillFeed[index].VictimPlayerId == victimPlayerId)
                {
                    return _context._world.KillFeed[index];
                }
            }

            return null;
        }

        private void DispatchIntelStateEvent(
            ClientPluginHost pluginHost,
            TeamIntelligenceState intel,
            PlayerTeam intelTeam,
            ref (bool IsAtBase, bool IsDropped, ClientPluginTeam CarrierTeam, float ReturnProgress, float X, float Y) previousState)
        {
            var currentState = CaptureIntelState(intel, intelTeam);
            if (!Equals(previousState, currentState))
            {
                pluginHost.NotifyIntelStateChanged(new ClientIntelStateEvent(
                    ToClientPluginTeam(intel.Team),
                    currentState.CarrierTeam,
                    currentState.IsAtBase,
                    currentState.IsDropped,
                    currentState.ReturnProgress,
                    new Vector2(intel.X, intel.Y),
                    (ulong)Math.Max(0, _context._world.Frame)));
            }

            previousState = currentState;
        }

        private static (bool IsAtBase, bool IsDropped, ClientPluginTeam CarrierTeam, float ReturnProgress, float X, float Y) CaptureIntelState(
            TeamIntelligenceState intel,
            PlayerTeam intelTeam)
        {
            var carrierTeam = intel.IsCarried
                ? ToClientPluginTeam(intelTeam == PlayerTeam.Red ? PlayerTeam.Blue : PlayerTeam.Red)
                : ClientPluginTeam.None;
            var returnProgress = intel.IsDropped
                ? 1f - Math.Clamp(intel.ReturnTicksRemaining / (float)PlayerEntity.IntelRechargeMaxTicks, 0f, 1f)
                : (intel.IsAtBase ? 1f : 0f);
            return (
                intel.IsAtBase,
                intel.IsDropped,
                carrierTeam,
                returnProgress,
                intel.X,
                intel.Y);
        }
}
