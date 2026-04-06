#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Client.Plugins;
using OpenGarrison.Protocol;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class ClientPluginStateView(Game1 game) : IOpenGarrisonClientReadOnlyState, ClientPluginMessageSink, ClientPluginUiSink
    {
        public bool IsConnected => game._networkClient.IsConnected;
        public bool IsMainMenuOpen => game._mainMenuOpen;
        public bool IsGameplayActive => !game._startupSplashOpen && !game._mainMenuOpen;
        public bool IsGameplayInputBlocked => game.IsGameplayInputBlocked();
        public bool IsSpectator => game._networkClient.IsSpectator;
        public bool IsDeathCamActive => game._killCamEnabled && !game._world.LocalPlayer.IsAlive && game._world.LocalDeathCam is not null;
        public ulong WorldFrame => (ulong)Math.Max(0, game._world.Frame);
        public int TickRate => game._config.TicksPerSecond;
        public int LocalPingMilliseconds => game._networkClient.EstimatedPingMilliseconds;
        public string LevelName => game._world.Level.Name;
        public float LevelWidth => game._world.Bounds.Width;
        public float LevelHeight => game._world.Bounds.Height;
        public int ViewportWidth => game.ViewportWidth;
        public int ViewportHeight => game.ViewportHeight;
        public int? LocalPlayerId => game.GetClientPluginLocalPlayerId();
        public ClientPluginTeam LocalPlayerTeam => game._networkClient.IsSpectator ? ClientPluginTeam.None : ToClientPluginTeam(game._world.LocalPlayer.Team);
        public ClientPluginClass LocalPlayerClass => game._networkClient.IsSpectator ? ClientPluginClass.Unknown : ToClientPluginClass(game._world.LocalPlayer.ClassId);
        public bool IsLocalPlayerAlive => !game._networkClient.IsSpectator && game._world.LocalPlayer.IsAlive;
        public bool IsLocalPlayerScoped => !game._networkClient.IsSpectator && game._world.LocalPlayer.IsSniperScoped;
        public bool IsLocalPlayerHealing => !game._networkClient.IsSpectator && game._world.LocalPlayer.IsMedicHealing;
        public Vector2 CameraTopLeft => game.GetCurrentClientPluginCameraTopLeft();

        public bool TryGetLocalPlayerHealth(out int health, out int maxHealth)
        {
            if (game._networkClient.IsSpectator)
            {
                health = default;
                maxHealth = default;
                return false;
            }

            health = game._world.LocalPlayer.Health;
            maxHealth = game._world.LocalPlayer.MaxHealth;
            return true;
        }

        public bool TryGetLocalPlayerWorldPosition(out Vector2 position)
        {
            if (game._networkClient.IsSpectator)
            {
                position = default;
                return false;
            }

            position = game.GetRenderPosition(game._world.LocalPlayer, allowInterpolation: false);
            return true;
        }

        public bool TryGetPlayerWorldPosition(int playerId, out Vector2 position)
        {
            if (game.FindPlayerById(playerId) is { } player)
            {
                position = game.GetRenderPosition(player, allowInterpolation: !ReferenceEquals(player, game._world.LocalPlayer));
                return true;
            }

            position = default;
            return false;
        }

        public bool IsPlayerVisibleToLocalViewer(int playerId)
        {
            if (game.FindPlayerById(playerId) is not { } player)
            {
                return false;
            }

            return game.GetPlayerVisibilityAlpha(player) > 0f;
        }

        public bool IsPlayerCloaked(int playerId)
        {
            return game.FindPlayerById(playerId) is { } player && game.GetPlayerIsSpyCloaked(player);
        }

        public bool TryGetPlayerReplicatedStateInt(int playerId, string ownerPluginId, string stateKey, out int value)
        {
            if (game.FindPlayerById(playerId) is { } player)
            {
                return player.TryGetReplicatedStateInt(ownerPluginId, stateKey, out value);
            }

            value = default;
            return false;
        }

        public bool TryGetPlayerReplicatedStateFloat(int playerId, string ownerPluginId, string stateKey, out float value)
        {
            if (game.FindPlayerById(playerId) is { } player)
            {
                return player.TryGetReplicatedStateFloat(ownerPluginId, stateKey, out value);
            }

            value = default;
            return false;
        }

        public bool TryGetPlayerReplicatedStateBool(int playerId, string ownerPluginId, string stateKey, out bool value)
        {
            if (game.FindPlayerById(playerId) is { } player)
            {
                return player.TryGetReplicatedStateBool(ownerPluginId, stateKey, out value);
            }

            value = default;
            return false;
        }

        public bool WasKeyPressedThisFrame(Keys key)
        {
            return game._clientPluginKeyboard.IsKeyDown(key) && !game._clientPluginPreviousKeyboard.IsKeyDown(key);
        }

        public IReadOnlyList<ClientPlayerMarker> GetPlayerMarkers() => game.GetClientPluginPlayerMarkers();
        public IReadOnlyList<ClientSentryMarker> GetSentryMarkers() => game.GetClientPluginSentryMarkers();
        public IReadOnlyList<ClientObjectiveMarker> GetObjectiveMarkers() => game.GetClientPluginObjectiveMarkers();

        public void SendPluginMessage(string sourcePluginId, string targetPluginId, string messageType, string payload, PluginMessagePayloadFormat payloadFormat, ushort schemaVersion)
        {
            game._networkClient.SendPluginMessage(sourcePluginId, targetPluginId, messageType, payload, payloadFormat, schemaVersion);
        }

        public void EnqueuePluginNotice(string text, int durationTicks, bool playSound)
        {
            game.QueuePluginNotice(text, durationTicks, playSound);
        }
    }
}
