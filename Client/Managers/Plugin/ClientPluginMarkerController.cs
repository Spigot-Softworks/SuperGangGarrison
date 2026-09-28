#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using OpenGarrison.Client.Plugins;
using OpenGarrison.Core;

using static OpenGarrison.Client.Game1;

namespace OpenGarrison.Client;

public sealed class ClientPluginMarkerController
    {
        private readonly IPluginContext _context;

        public ClientPluginMarkerController(IPluginContext context)
        {
            _context = context;
        }

        public List<ClientPlayerMarker> GetClientPluginPlayerMarkers()
        {
            var markers = new List<ClientPlayerMarker>();
            if (!_context.IsLocalSpectatorPresentationActive() && _context._world.LocalPlayer.IsAlive)
            {
                markers.Add(BuildClientPlayerMarker(_context._world.LocalPlayer, isLocalPlayer: true));
            }

            foreach (var player in _context.EnumerateRemotePlayersForView())
            {
                if (!player.IsAlive)
                {
                    continue;
                }

                markers.Add(BuildClientPlayerMarker(player, isLocalPlayer: false));
            }

            return markers;
        }

        public List<ClientSentryMarker> GetClientPluginSentryMarkers()
        {
            if (_context._world.Sentries.Count == 0)
            {
                return [];
            }

            var markers = new List<ClientSentryMarker>(_context._world.Sentries.Count);
            foreach (var sentry in _context._world.Sentries)
            {
                markers.Add(new ClientSentryMarker(
                    sentry.Id,
                    sentry.OwnerPlayerId,
                    ToClientPluginTeam(sentry.Team),
                    new Vector2(sentry.X, sentry.Y),
                    sentry.Health,
                    sentry.MaxHealth));
            }

            return markers;
        }

        public List<ClientObjectiveMarker> GetClientPluginObjectiveMarkers()
        {
            if (_context.IsLocalSpectatorPresentationActive())
            {
                return [];
            }

            var markers = new List<ClientObjectiveMarker>();
            switch (_context._world.MatchRules.Mode)
            {
                case GameModeKind.CaptureTheFlag:
                    AddClientPluginIntelMarkers(markers, _context._world.LocalPlayer.Team);
                    break;
                case GameModeKind.Arena:
                case GameModeKind.ControlPoint:
                case GameModeKind.Vip:
                case GameModeKind.KingOfTheHill:
                case GameModeKind.DoubleKingOfTheHill:
                    foreach (var point in _context._world.ControlPoints)
                    {
                        var progress = point.CapTimeTicks <= 0
                            ? 0f
                            : Math.Clamp(point.CappingTicks / point.CapTimeTicks, 0f, 1f);
                        markers.Add(new ClientObjectiveMarker(
                            ClientObjectiveMarkerKind.ControlPoint,
                            ToClientPluginTeam(point.Team),
                            new Vector2(point.Marker.CenterX, point.Marker.CenterY),
                            progress,
                            point.IsLocked));
                    }
                    break;
                case GameModeKind.Generator:
                    foreach (var generator in _context._world.Generators)
                    {
                        markers.Add(new ClientObjectiveMarker(
                            ClientObjectiveMarkerKind.Generator,
                            ToClientPluginTeam(generator.Team),
                            new Vector2(generator.Marker.CenterX, generator.Marker.CenterY),
                            1f - (generator.Health / (float)Math.Max(1, generator.MaxHealth)),
                            false));
                    }
                    break;
            }

            return markers;
        }

        private ClientPlayerMarker BuildClientPlayerMarker(PlayerEntity player, bool isLocalPlayer)
        {
            var renderPosition = _context.GetRenderPosition(player);
            return new ClientPlayerMarker(
                player.Id,
                player.DisplayName,
                ToClientPluginTeam(player.Team),
                ToClientPluginClass(player.ClassId),
                renderPosition,
                player.Health,
                player.MaxHealth,
                player.IsAlive,
                player.IsCarryingIntel,
                isLocalPlayer);
        }

        private void AddClientPluginIntelMarkers(List<ClientObjectiveMarker> markers, PlayerTeam localTeam)
        {
            var ownBase = _context._world.Level.GetIntelBase(localTeam);
            var enemyBase = _context._world.Level.GetIntelBase(localTeam == PlayerTeam.Red ? PlayerTeam.Blue : PlayerTeam.Red);
            var ownIntel = localTeam == PlayerTeam.Red ? _context._world.RedIntel : _context._world.BlueIntel;
            var enemyIntel = localTeam == PlayerTeam.Red ? _context._world.BlueIntel : _context._world.RedIntel;

            var defendPosition = ownBase.HasValue
                ? new Vector2(ownBase.Value.X, ownBase.Value.Y)
                : new Vector2(ownIntel.X, ownIntel.Y);
            if (!ownIntel.IsAtBase)
            {
                defendPosition = new Vector2(ownIntel.X, ownIntel.Y);
            }

            var attackPosition = enemyBase.HasValue
                ? new Vector2(enemyBase.Value.X, enemyBase.Value.Y)
                : new Vector2(enemyIntel.X, enemyIntel.Y);
            if (!enemyIntel.IsAtBase)
            {
                attackPosition = new Vector2(enemyIntel.X, enemyIntel.Y);
            }

            if (_context._world.LocalPlayer.IsCarryingIntel && ownBase.HasValue)
            {
                attackPosition = new Vector2(ownBase.Value.X, ownBase.Value.Y);
            }

            markers.Add(new ClientObjectiveMarker(
                ClientObjectiveMarkerKind.Defend,
                ToClientPluginTeam(localTeam),
                defendPosition,
                0f,
                false));
            markers.Add(new ClientObjectiveMarker(
                ClientObjectiveMarkerKind.Attack,
                ToClientPluginTeam(localTeam == PlayerTeam.Red ? PlayerTeam.Blue : PlayerTeam.Red),
                attackPosition,
                0f,
                false));
        }
}
