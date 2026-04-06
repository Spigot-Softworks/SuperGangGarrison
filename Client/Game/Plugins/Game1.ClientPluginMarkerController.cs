#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using OpenGarrison.Client.Plugins;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class ClientPluginMarkerController
    {
        private readonly Game1 _game;

        public ClientPluginMarkerController(Game1 game)
        {
            _game = game;
        }

        public List<ClientPlayerMarker> GetClientPluginPlayerMarkers()
        {
            var markers = new List<ClientPlayerMarker>();
            if (!_game._networkClient.IsSpectator && _game._world.LocalPlayer.IsAlive)
            {
                markers.Add(BuildClientPlayerMarker(_game._world.LocalPlayer, isLocalPlayer: true));
            }

            foreach (var player in _game.EnumerateRemotePlayersForView())
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
            if (_game._world.Sentries.Count == 0)
            {
                return [];
            }

            var markers = new List<ClientSentryMarker>(_game._world.Sentries.Count);
            foreach (var sentry in _game._world.Sentries)
            {
                markers.Add(new ClientSentryMarker(
                    sentry.Id,
                    sentry.OwnerPlayerId,
                    ToClientPluginTeam(sentry.Team),
                    new Vector2(sentry.X, sentry.Y),
                    sentry.Health,
                    SentryEntity.MaxHealth));
            }

            return markers;
        }

        public List<ClientObjectiveMarker> GetClientPluginObjectiveMarkers()
        {
            if (_game._networkClient.IsSpectator)
            {
                return [];
            }

            var markers = new List<ClientObjectiveMarker>();
            switch (_game._world.MatchRules.Mode)
            {
                case GameModeKind.CaptureTheFlag:
                    AddClientPluginIntelMarkers(markers, _game._world.LocalPlayer.Team);
                    break;
                case GameModeKind.Arena:
                case GameModeKind.ControlPoint:
                case GameModeKind.KingOfTheHill:
                case GameModeKind.DoubleKingOfTheHill:
                    foreach (var point in _game._world.ControlPoints)
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
                    foreach (var generator in _game._world.Generators)
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
            var renderPosition = _game.GetRenderPosition(player, allowInterpolation: !ReferenceEquals(player, _game._world.LocalPlayer));
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
            var ownBase = _game._world.Level.GetIntelBase(localTeam);
            var enemyBase = _game._world.Level.GetIntelBase(localTeam == PlayerTeam.Red ? PlayerTeam.Blue : PlayerTeam.Red);
            var ownIntel = localTeam == PlayerTeam.Red ? _game._world.RedIntel : _game._world.BlueIntel;
            var enemyIntel = localTeam == PlayerTeam.Red ? _game._world.BlueIntel : _game._world.RedIntel;

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

            if (_game._world.LocalPlayer.IsCarryingIntel && ownBase.HasValue)
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
}
