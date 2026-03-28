#nullable enable

using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Client.Plugins;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using ClientPluginDamageTargetKind = OpenGarrison.Client.Plugins.DamageTargetKind;
using CoreDamageTargetKind = OpenGarrison.Core.DamageTargetKind;

namespace OpenGarrison.Client;

public partial class Game1
{
    private readonly List<SnapshotDamageEvent> _pendingNetworkDamageEvents = new();
    private readonly HashSet<ulong> _processedNetworkDamageEventIds = new();
    private readonly Queue<ulong> _processedNetworkDamageEventOrder = new();
    private ClientPluginHost? _clientPluginHost;
    private ClientPluginStateView? _clientPluginStateView;

    private void InitializeClientPlugins()
    {
        var pluginsDirectory = Path.Combine(RuntimePaths.ApplicationRoot, "Plugins", "Client");
        var pluginConfigRoot = Path.Combine(RuntimePaths.ConfigDirectory, "plugins", "client");
        _clientPluginStateView = new ClientPluginStateView(this);
        _clientPluginHost = new ClientPluginHost(_clientPluginStateView, pluginsDirectory, pluginConfigRoot, AddConsoleLine);
        _clientPluginHost.LoadPlugins();
        _clientPluginHost.NotifyClientStarting();
    }

    private void NotifyClientPluginsStarted()
    {
        _clientPluginHost?.NotifyClientStarted();
    }

    private void ShutdownClientPlugins()
    {
        if (_clientPluginHost is null)
        {
            return;
        }

        _clientPluginHost.NotifyClientStopping();
        _clientPluginHost.ShutdownPlugins();
        _clientPluginHost.NotifyClientStopped();
        _clientPluginHost = null;
        _clientPluginStateView = null;
    }

    private void NotifyClientPluginsFrame(GameTime gameTime, int clientTicks)
    {
        _clientPluginHost?.NotifyClientFrame(new ClientFrameEvent(
            (float)gameTime.ElapsedGameTime.TotalSeconds,
            clientTicks,
            _mainMenuOpen,
            !_startupSplashOpen && !_mainMenuOpen,
            _networkClient.IsConnected,
            _networkClient.IsSpectator));
    }

    private void QueueResolvedSnapshotDamageEvents(SnapshotMessage resolvedSnapshot)
    {
        for (var damageIndex = 0; damageIndex < resolvedSnapshot.DamageEvents.Count; damageIndex += 1)
        {
            var damageEvent = resolvedSnapshot.DamageEvents[damageIndex];
            if (!ShouldProcessNetworkEvent(damageEvent.EventId, _processedNetworkDamageEventIds, _processedNetworkDamageEventOrder))
            {
                continue;
            }

            _pendingNetworkDamageEvents.Add(damageEvent);
        }
    }

    private void DispatchPendingDamageEventsToPlugins()
    {
        var localDamageEvents = _world.DrainPendingDamageEvents();
        if (_clientPluginHost is null)
        {
            _pendingNetworkDamageEvents.Clear();
            return;
        }

        var localPlayerId = GetClientPluginLocalPlayerId();
        if (localPlayerId.HasValue)
        {
            if (!_networkClient.IsConnected)
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

    private void TryDispatchLocalDamageEvent(int localPlayerId, WorldDamageEvent damageEvent)
    {
        var dealtByLocalPlayer = damageEvent.AttackerPlayerId == localPlayerId;
        var assistedByLocalPlayer = damageEvent.AssistedByPlayerId == localPlayerId;
        var receivedByLocalPlayer = damageEvent.TargetKind == CoreDamageTargetKind.Player
            && damageEvent.TargetEntityId == localPlayerId;
        if (!dealtByLocalPlayer && !assistedByLocalPlayer && !receivedByLocalPlayer)
        {
            return;
        }

        _clientPluginHost?.NotifyLocalDamage(new LocalDamageEvent(
            damageEvent.Amount,
            (ClientPluginDamageTargetKind)damageEvent.TargetKind,
            damageEvent.TargetEntityId,
            new Vector2(damageEvent.X, damageEvent.Y),
            damageEvent.WasFatal,
            dealtByLocalPlayer,
            assistedByLocalPlayer,
            receivedByLocalPlayer,
            damageEvent.AttackerPlayerId,
            damageEvent.AssistedByPlayerId));
    }

    private void TryDispatchLocalDamageEvent(int localPlayerId, SnapshotDamageEvent damageEvent)
    {
        var dealtByLocalPlayer = damageEvent.AttackerPlayerId == localPlayerId;
        var assistedByLocalPlayer = damageEvent.AssistedByPlayerId == localPlayerId;
        var receivedByLocalPlayer = damageEvent.TargetKind == (byte)ClientPluginDamageTargetKind.Player
            && damageEvent.TargetEntityId == localPlayerId;
        if (!dealtByLocalPlayer && !assistedByLocalPlayer && !receivedByLocalPlayer)
        {
            return;
        }

        _clientPluginHost?.NotifyLocalDamage(new LocalDamageEvent(
            damageEvent.Amount,
            (ClientPluginDamageTargetKind)damageEvent.TargetKind,
            damageEvent.TargetEntityId,
            new Vector2(damageEvent.X, damageEvent.Y),
            damageEvent.WasFatal,
            dealtByLocalPlayer,
            assistedByLocalPlayer,
            receivedByLocalPlayer,
            damageEvent.AttackerPlayerId,
            damageEvent.AssistedByPlayerId));
    }

    private void DrawClientPluginHud(Vector2 cameraTopLeft)
    {
        if (_clientPluginHost is null)
        {
            return;
        }

        _clientPluginHost.NotifyGameplayHudDraw(new GameplayHudCanvas(this, cameraTopLeft));
    }

    private int? GetClientPluginLocalPlayerId()
    {
        if (_networkClient.IsSpectator)
        {
            return null;
        }

        if (_networkClient.IsConnected)
        {
            return _localPlayerSnapshotEntityId;
        }

        return _world.LocalPlayer.Id;
    }

    private Vector2 GetCurrentClientPluginCameraTopLeft()
    {
        if (_startupSplashOpen || _mainMenuOpen)
        {
            return Vector2.Zero;
        }

        var mouse = GetScaledMouseState(GetConstrainedMouseState(Mouse.GetState()));
        return GetCameraTopLeft(ViewportWidth, ViewportHeight, mouse.X, mouse.Y);
    }

    private sealed class ClientPluginStateView(Game1 game) : IOpenGarrisonClientReadOnlyState
    {
        public bool IsConnected => game._networkClient.IsConnected;

        public bool IsMainMenuOpen => game._mainMenuOpen;

        public bool IsGameplayActive => !game._startupSplashOpen && !game._mainMenuOpen;

        public bool IsSpectator => game._networkClient.IsSpectator;

        public bool IsDeathCamActive => game._killCamEnabled
            && !game._world.LocalPlayer.IsAlive
            && game._world.LocalDeathCam is not null;

        public ulong WorldFrame => (ulong)Math.Max(0, game._world.Frame);

        public int TickRate => game._config.TicksPerSecond;

        public string LevelName => game._world.Level.Name;

        public int ViewportWidth => game.ViewportWidth;

        public int ViewportHeight => game.ViewportHeight;

        public int? LocalPlayerId => game.GetClientPluginLocalPlayerId();

        public Vector2 CameraTopLeft => game.GetCurrentClientPluginCameraTopLeft();

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
    }

    private sealed class GameplayHudCanvas(Game1 game, Vector2 cameraTopLeft) : IOpenGarrisonClientHudCanvas
    {
        public int ViewportWidth => game.ViewportWidth;

        public int ViewportHeight => game.ViewportHeight;

        public Vector2 CameraTopLeft => cameraTopLeft;

        public Vector2 WorldToScreen(Vector2 worldPosition)
        {
            return worldPosition - cameraTopLeft;
        }

        public float MeasureBitmapTextWidth(string text, float scale)
        {
            return game.MeasureBitmapFontWidth(text, scale);
        }

        public float MeasureBitmapTextHeight(float scale)
        {
            return game.MeasureBitmapFontHeight(scale);
        }

        public void DrawBitmapText(string text, Vector2 position, Color color, float scale = 1f)
        {
            game.DrawBitmapFontText(text, position, color, scale);
        }

        public void DrawBitmapTextCentered(string text, Vector2 position, Color color, float scale = 1f)
        {
            game.DrawHudTextCentered(text, position, color, scale);
        }

        public bool TryDrawScreenSprite(string spriteName, int frameIndex, Vector2 position, Color tint, Vector2 scale)
        {
            return game.TryDrawScreenSprite(spriteName, frameIndex, position, tint, scale);
        }

        public bool TryDrawWorldSprite(string spriteName, int frameIndex, Vector2 worldPosition, Color tint, float rotation = 0f)
        {
            return game.TryDrawSprite(spriteName, frameIndex, worldPosition.X, worldPosition.Y, cameraTopLeft, tint, rotation);
        }
    }
}
