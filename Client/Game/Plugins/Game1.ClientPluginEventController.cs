#nullable enable

using Microsoft.Xna.Framework;
using OpenGarrison.Client.Plugins;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using ClientPluginDamageTargetKind = OpenGarrison.Client.Plugins.DamageTargetKind;
using CoreDamageTargetKind = OpenGarrison.Core.DamageTargetKind;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class ClientPluginEventController
    {
        private readonly Game1 _game;

        public ClientPluginEventController(Game1 game)
        {
            _game = game;
        }

        public void QueueResolvedSnapshotDamageEvents(SnapshotMessage resolvedSnapshot)
        {
            for (var damageIndex = 0; damageIndex < resolvedSnapshot.DamageEvents.Count; damageIndex += 1)
            {
                var damageEvent = resolvedSnapshot.DamageEvents[damageIndex];
                if (!ShouldProcessNetworkEvent(damageEvent.EventId, _game._processedNetworkDamageEventIds, _game._processedNetworkDamageEventOrder))
                {
                    continue;
                }

                _game._pendingNetworkDamageEvents.Add(damageEvent);
            }
        }

        public void DispatchClientSemanticGameplayEvents()
        {
            DispatchPendingDamageEventsToPlugins();
            DispatchClientRoundPhaseEvents();
            DispatchClientLocalPlayerStateEvents();
            DispatchClientObjectiveEvents();
            DispatchClientKillFeedEvents();
        }

        public void DispatchPendingDamageEventsToPlugins()
        {
            DispatchPendingDamageEventsToPluginsCore();
        }

        public void ResetClientPluginGameplayEventState()
        {
            _game._clientPluginPreviousMatchPhase = ToClientRoundPhase(_game._world.MatchState.Phase);
            _game._clientPluginPreviousLocalAlive = _game._world.LocalPlayer.IsAlive;
            _game._clientPluginPreviousLocalHealth = _game._world.LocalPlayer.Health;
            _game._clientPluginPreviousLocalAmmo = _game._world.LocalPlayer.CurrentShells;
            _game._clientPluginPreviousLocalPrimaryCooldownTicks = _game._world.LocalPlayer.PrimaryCooldownTicks;
            _game._clientPluginPreviousLocalCarryingIntel = _game._world.LocalPlayer.IsCarryingIntel;
            _game._clientPluginPreviousLocalBurning = _game._world.LocalPlayer.IsBurning;
            _game._clientPluginPreviousKillFeedCount = _game._world.KillFeed.Count;
            _game._clientPluginPreviousObjectiveStates.Clear();
            for (var index = 0; index < _game._world.ControlPoints.Count; index += 1)
            {
                var point = _game._world.ControlPoints[index];
                _game._clientPluginPreviousObjectiveStates[point.Index] = (
                    ToClientPluginTeam(point.Team),
                    ToClientPluginTeam(point.CappingTeam),
                    point.CapTimeTicks <= 0 ? 0f : Math.Clamp(point.CappingTicks / point.CapTimeTicks, 0f, 1f),
                    point.IsLocked);
            }
        }

        private void DispatchPendingDamageEventsToPluginsCore()
        {
            var localDamageEvents = _game._world.DrainPendingDamageEvents();
            for (var index = 0; index < localDamageEvents.Count; index += 1)
            {
                TryTrackLastToDieDamageDealt(localDamageEvents[index].AttackerPlayerId, localDamageEvents[index].Amount);
            }

            for (var index = 0; index < _game._pendingNetworkDamageEvents.Count; index += 1)
            {
                TryTrackLastToDieDamageDealt(_game._pendingNetworkDamageEvents[index].AttackerPlayerId, _game._pendingNetworkDamageEvents[index].Amount);
            }

            if (_game._clientPluginHost is null)
            {
                _game._pendingNetworkDamageEvents.Clear();
                return;
            }

            var localPlayerId = _game.GetClientPluginLocalPlayerId();
            if (localPlayerId.HasValue)
            {
                if (!_game._networkClient.IsConnected)
                {
                    for (var index = 0; index < localDamageEvents.Count; index += 1)
                    {
                        TryDispatchLocalDamageEvent(localPlayerId.Value, localDamageEvents[index]);
                    }
                }

                for (var index = 0; index < _game._pendingNetworkDamageEvents.Count; index += 1)
                {
                    TryDispatchLocalDamageEvent(localPlayerId.Value, _game._pendingNetworkDamageEvents[index]);
                }
            }

            _game._pendingNetworkDamageEvents.Clear();
        }

        private void TryTrackLastToDieDamageDealt(int attackerPlayerId, int amount)
        {
            var localPlayerId = _game.GetClientPluginLocalPlayerId();
            if (!localPlayerId.HasValue || attackerPlayerId != localPlayerId.Value)
            {
                return;
            }

            _game.RegisterLastToDieLocalDamageDealt(amount);
        }

        private void TryDispatchLocalDamageEvent(int localPlayerId, WorldDamageEvent damageEvent)
        {
            var dealtByLocalPlayer = damageEvent.AttackerPlayerId == localPlayerId;
            var assistedByLocalPlayer = damageEvent.AssistedByPlayerId == localPlayerId;
            var receivedByLocalPlayer = damageEvent.TargetKind == CoreDamageTargetKind.Player && damageEvent.TargetEntityId == localPlayerId;
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
                dealtByLocalPlayer,
                assistedByLocalPlayer,
                receivedByLocalPlayer,
                damageEvent.AttackerPlayerId,
                damageEvent.AssistedByPlayerId,
                (LocalDamageFlags)damageEvent.Flags);
            _game._clientPluginHost?.NotifyLocalDamage(pluginEvent);
            if (dealtByLocalPlayer || assistedByLocalPlayer)
            {
                _game._clientPluginHost?.NotifyHitConfirmed(new ClientHitConfirmedEvent(
                    pluginEvent.Amount,
                    pluginEvent.TargetKind,
                    pluginEvent.TargetEntityId,
                    pluginEvent.TargetWorldPosition,
                    pluginEvent.TargetWasKilled,
                    pluginEvent.AttackerPlayerId,
                    pluginEvent.AssistedByPlayerId,
                    pluginEvent.Flags,
                    (ulong)Math.Max(0, _game._world.Frame)));
            }
        }

        private void TryDispatchLocalDamageEvent(int localPlayerId, SnapshotDamageEvent damageEvent)
        {
            var dealtByLocalPlayer = damageEvent.AttackerPlayerId == localPlayerId;
            var assistedByLocalPlayer = damageEvent.AssistedByPlayerId == localPlayerId;
            var receivedByLocalPlayer = damageEvent.TargetKind == (byte)ClientPluginDamageTargetKind.Player && damageEvent.TargetEntityId == localPlayerId;
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
                dealtByLocalPlayer,
                assistedByLocalPlayer,
                receivedByLocalPlayer,
                damageEvent.AttackerPlayerId,
                damageEvent.AssistedByPlayerId,
                LocalDamageFlags.None);
            _game._clientPluginHost?.NotifyLocalDamage(pluginEvent);
            if (dealtByLocalPlayer || assistedByLocalPlayer)
            {
                _game._clientPluginHost?.NotifyHitConfirmed(new ClientHitConfirmedEvent(
                    pluginEvent.Amount,
                    pluginEvent.TargetKind,
                    pluginEvent.TargetEntityId,
                    pluginEvent.TargetWorldPosition,
                    pluginEvent.TargetWasKilled,
                    pluginEvent.AttackerPlayerId,
                    pluginEvent.AssistedByPlayerId,
                    pluginEvent.Flags,
                    (ulong)Math.Max(0, _game._world.Frame)));
            }
        }

        private void DispatchClientRoundPhaseEvents()
        {
            if (_game._clientPluginHost is null)
            {
                _game._clientPluginPreviousMatchPhase = ToClientRoundPhase(_game._world.MatchState.Phase);
                return;
            }

            var currentPhase = ToClientRoundPhase(_game._world.MatchState.Phase);
            if (currentPhase != _game._clientPluginPreviousMatchPhase)
            {
                _game._clientPluginHost.NotifyRoundPhaseChanged(new ClientRoundPhaseChangedEvent(
                    _game._clientPluginPreviousMatchPhase,
                    currentPhase,
                    (ulong)Math.Max(0, _game._world.Frame)));
                _game._clientPluginPreviousMatchPhase = currentPhase;
            }
        }

        private void DispatchClientLocalPlayerStateEvents()
        {
            var pluginHost = _game._clientPluginHost;
            var localPlayer = _game._world.LocalPlayer;
            if (pluginHost is null || _game._networkClient.IsSpectator)
            {
                _game._clientPluginPreviousLocalAlive = localPlayer.IsAlive;
                _game._clientPluginPreviousLocalHealth = localPlayer.Health;
                _game._clientPluginPreviousLocalAmmo = localPlayer.CurrentShells;
                _game._clientPluginPreviousLocalPrimaryCooldownTicks = localPlayer.PrimaryCooldownTicks;
                _game._clientPluginPreviousLocalCarryingIntel = localPlayer.IsCarryingIntel;
                _game._clientPluginPreviousLocalBurning = localPlayer.IsBurning;
                return;
            }

            if (_game._clientPluginPreviousLocalAlive && !localPlayer.IsAlive)
            {
                var latestEntry = FindLatestKillFeedEntryForVictim(localPlayer.Id);
                pluginHost.NotifyLocalDeath(new ClientLocalDeathEvent(
                    latestEntry?.KillerPlayerId ?? -1,
                    latestEntry?.KillerName ?? string.Empty,
                    ToClientPluginTeam(latestEntry?.KillerTeam),
                    latestEntry?.WeaponSpriteName ?? string.Empty,
                    latestEntry?.MessageText ?? string.Empty,
                    (ulong)Math.Max(0, _game._world.Frame)));
            }

            if (localPlayer.IsAlive)
            {
                var healedAmount = localPlayer.Health - _game._clientPluginPreviousLocalHealth;
                if (healedAmount > 0)
                {
                    pluginHost.NotifyHeal(new ClientHealEvent(
                        healedAmount,
                        localPlayer.Health,
                        localPlayer.MaxHealth,
                        (ulong)Math.Max(0, _game._world.Frame)));
                }

                var firedShot = localPlayer.CurrentShells < _game._clientPluginPreviousLocalAmmo
                    || (_game._clientPluginPreviousLocalPrimaryCooldownTicks <= 0 && localPlayer.PrimaryCooldownTicks > 0);
                if (firedShot)
                {
                    pluginHost.NotifyShotFired(new ClientShotFiredEvent(
                        _game.GetClientPluginLocalPlayerId(),
                        ToClientPluginClass(localPlayer.ClassId),
                        new Vector2(localPlayer.X, localPlayer.Y),
                        (ulong)Math.Max(0, _game._world.Frame)));
                }

                if (!_game._clientPluginPreviousLocalCarryingIntel && localPlayer.IsCarryingIntel)
                {
                    pluginHost.NotifyPickup(new ClientPickupEvent(
                        ClientGameplayPickupKind.Intel,
                        new Vector2(localPlayer.X, localPlayer.Y),
                        (ulong)Math.Max(0, _game._world.Frame)));
                }
            }

            if (!_game._clientPluginPreviousLocalBurning && localPlayer.IsBurning)
            {
                pluginHost.NotifyIgnited(new ClientIgniteEvent(localPlayer.BurnedByPlayerId ?? -1, localPlayer.BurnIntensity, (ulong)Math.Max(0, _game._world.Frame)));
            }
            else if (_game._clientPluginPreviousLocalBurning && !localPlayer.IsBurning)
            {
                pluginHost.NotifyExtinguished(new ClientExtinguishEvent((ulong)Math.Max(0, _game._world.Frame)));
            }

            _game._clientPluginPreviousLocalAlive = localPlayer.IsAlive;
            _game._clientPluginPreviousLocalHealth = localPlayer.Health;
            _game._clientPluginPreviousLocalAmmo = localPlayer.CurrentShells;
            _game._clientPluginPreviousLocalPrimaryCooldownTicks = localPlayer.PrimaryCooldownTicks;
            _game._clientPluginPreviousLocalCarryingIntel = localPlayer.IsCarryingIntel;
            _game._clientPluginPreviousLocalBurning = localPlayer.IsBurning;
        }

        private void DispatchClientObjectiveEvents()
        {
            var pluginHost = _game._clientPluginHost;
            if (pluginHost is null)
            {
                return;
            }

            for (var index = 0; index < _game._world.ControlPoints.Count; index += 1)
            {
                var point = _game._world.ControlPoints[index];
                var currentState = (
                    ToClientPluginTeam(point.Team),
                    ToClientPluginTeam(point.CappingTeam),
                    point.CapTimeTicks <= 0 ? 0f : Math.Clamp(point.CappingTicks / point.CapTimeTicks, 0f, 1f),
                    point.IsLocked);
                var previousState = _game._clientPluginPreviousObjectiveStates.GetValueOrDefault(point.Index, currentState);
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
                        (ulong)Math.Max(0, _game._world.Frame)));
                }

                _game._clientPluginPreviousObjectiveStates[point.Index] = currentState;
            }
        }

        private void DispatchClientKillFeedEvents()
        {
            var pluginHost = _game._clientPluginHost;
            if (pluginHost is null)
            {
                _game._clientPluginPreviousKillFeedCount = _game._world.KillFeed.Count;
                return;
            }

            if (_game._world.KillFeed.Count < _game._clientPluginPreviousKillFeedCount)
            {
                _game._clientPluginPreviousKillFeedCount = 0;
            }

            var localPlayerId = _game.GetClientPluginLocalPlayerId();
            for (var index = _game._clientPluginPreviousKillFeedCount; index < _game._world.KillFeed.Count; index += 1)
            {
                var entry = _game._world.KillFeed[index];
                var killFeedEvent = new ClientKillFeedEvent(
                    entry.KillerPlayerId,
                    entry.KillerName,
                    ToClientPluginTeam(entry.KillerTeam),
                    entry.VictimPlayerId,
                    entry.VictimName,
                    ToClientPluginTeam(entry.VictimTeam),
                    entry.WeaponSpriteName,
                    entry.MessageText,
                    (ulong)Math.Max(0, _game._world.Frame));
                pluginHost.NotifyKillFeed(killFeedEvent);
                if (localPlayerId.HasValue && entry.KillerPlayerId == localPlayerId.Value)
                {
                    pluginHost.NotifyLocalKill(new ClientLocalKillEvent(
                        entry.VictimPlayerId,
                        entry.VictimName,
                        ToClientPluginTeam(entry.VictimTeam),
                        entry.WeaponSpriteName,
                        entry.MessageText,
                        (ulong)Math.Max(0, _game._world.Frame)));
                }
            }

            _game._clientPluginPreviousKillFeedCount = _game._world.KillFeed.Count;
        }

        private KillFeedEntry? FindLatestKillFeedEntryForVictim(int victimPlayerId)
        {
            for (var index = _game._world.KillFeed.Count - 1; index >= 0; index -= 1)
            {
                if (_game._world.KillFeed[index].VictimPlayerId == victimPlayerId)
                {
                    return _game._world.KillFeed[index];
                }
            }

            return null;
        }
    }
}
