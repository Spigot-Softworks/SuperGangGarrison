#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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
        var pluginStatePath = Path.Combine(pluginConfigRoot, "plugins.json");
        _clientPluginStateView = new ClientPluginStateView(this);
        _clientPluginHost = new ClientPluginHost(_clientPluginStateView, GraphicsDevice, pluginsDirectory, pluginConfigRoot, pluginStatePath, AddConsoleLine);
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

    private ClientBubbleMenuUpdateResult? TryHandleClientPluginBubbleMenuInput(ClientBubbleMenuInputState inputState)
    {
        return _clientPluginHost?.TryHandleBubbleMenuInput(inputState);
    }

    private bool TryDrawClientPluginBubbleMenu(Vector2 cameraTopLeft, ClientBubbleMenuRenderState renderState)
    {
        return _clientPluginHost?.TryDrawBubbleMenu(new GameplayHudCanvas(this, cameraTopLeft), renderState) ?? false;
    }

    private bool HasClientPluginBubbleMenuOverride()
    {
        return _clientPluginHost?.HasLoadedBubbleMenuOverride() ?? false;
    }

    private bool TryDrawClientPluginDeadBody(Vector2 cameraTopLeft, ClientDeadBodyRenderState deadBody)
    {
        return _clientPluginHost?.TryDrawDeadBody(new GameplayHudCanvas(this, cameraTopLeft), deadBody) ?? false;
    }

    private ClientPluginMainMenuBackgroundOverride? GetClientPluginMainMenuBackgroundOverride()
    {
        return _clientPluginHost?.GetMainMenuBackgroundOverride();
    }

    private void NotifyClientPluginsWorldSound(WorldSoundEvent soundEvent)
    {
        _clientPluginHost?.NotifyWorldSound(new ClientWorldSoundEvent(
            soundEvent.SoundName,
            new Vector2(soundEvent.X, soundEvent.Y)));
    }

    private Vector2 GetClientPluginCameraOffset()
    {
        return _clientPluginHost?.GetCameraOffset() ?? Vector2.Zero;
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
        return RoundToSourcePixels(CalculateBaseCameraTopLeft(ViewportWidth, ViewportHeight, mouse.X, mouse.Y, trackLiveCamera: false));
    }

    private Texture2D? GetClientPluginLevelBackgroundTexture()
    {
        var backgroundName = _world.Level.BackgroundAssetName;
        return string.IsNullOrWhiteSpace(backgroundName)
            ? null
            : _runtimeAssets.GetBackground(backgroundName);
    }

    private List<ClientPlayerMarker> GetClientPluginPlayerMarkers()
    {
        var markers = new List<ClientPlayerMarker>();
        if (!_networkClient.IsSpectator && _world.LocalPlayer.IsAlive)
        {
            markers.Add(BuildClientPlayerMarker(_world.LocalPlayer, isLocalPlayer: true));
        }

        foreach (var player in EnumerateRemotePlayersForView())
        {
            if (!player.IsAlive)
            {
                continue;
            }

            markers.Add(BuildClientPlayerMarker(player, isLocalPlayer: false));
        }

        return markers;
    }

    private ClientPlayerMarker BuildClientPlayerMarker(PlayerEntity player, bool isLocalPlayer)
    {
        var renderPosition = GetRenderPosition(player, allowInterpolation: !ReferenceEquals(player, _world.LocalPlayer));
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

    private List<ClientSentryMarker> GetClientPluginSentryMarkers()
    {
        if (_world.Sentries.Count == 0)
        {
            return [];
        }

        var markers = new List<ClientSentryMarker>(_world.Sentries.Count);
        foreach (var sentry in _world.Sentries)
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

    private List<ClientObjectiveMarker> GetClientPluginObjectiveMarkers()
    {
        if (_networkClient.IsSpectator)
        {
            return [];
        }

        var markers = new List<ClientObjectiveMarker>();
        switch (_world.MatchRules.Mode)
        {
            case GameModeKind.CaptureTheFlag:
                AddClientPluginIntelMarkers(markers, _world.LocalPlayer.Team);
                break;
            case GameModeKind.Arena:
            case GameModeKind.ControlPoint:
            case GameModeKind.KingOfTheHill:
            case GameModeKind.DoubleKingOfTheHill:
                foreach (var point in _world.ControlPoints)
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
                foreach (var generator in _world.Generators)
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

    private void AddClientPluginIntelMarkers(List<ClientObjectiveMarker> markers, PlayerTeam localTeam)
    {
        var ownBase = _world.Level.GetIntelBase(localTeam);
        var enemyBase = _world.Level.GetIntelBase(localTeam == PlayerTeam.Red ? PlayerTeam.Blue : PlayerTeam.Red);
        var ownIntel = localTeam == PlayerTeam.Red ? _world.RedIntel : _world.BlueIntel;
        var enemyIntel = localTeam == PlayerTeam.Red ? _world.BlueIntel : _world.RedIntel;

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

        if (_world.LocalPlayer.IsCarryingIntel && ownBase.HasValue)
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

    private static ClientPluginTeam ToClientPluginTeam(PlayerTeam? team)
    {
        return team switch
        {
            PlayerTeam.Red => ClientPluginTeam.Red,
            PlayerTeam.Blue => ClientPluginTeam.Blue,
            _ => ClientPluginTeam.None,
        };
    }

    private static ClientPluginClass ToClientPluginClass(PlayerClass classId)
    {
        return classId switch
        {
            PlayerClass.Scout => ClientPluginClass.Scout,
            PlayerClass.Engineer => ClientPluginClass.Engineer,
            PlayerClass.Pyro => ClientPluginClass.Pyro,
            PlayerClass.Soldier => ClientPluginClass.Soldier,
            PlayerClass.Demoman => ClientPluginClass.Demoman,
            PlayerClass.Heavy => ClientPluginClass.Heavy,
            PlayerClass.Sniper => ClientPluginClass.Sniper,
            PlayerClass.Medic => ClientPluginClass.Medic,
            PlayerClass.Spy => ClientPluginClass.Spy,
            PlayerClass.Quote => ClientPluginClass.Quote,
            _ => ClientPluginClass.Unknown,
        };
    }

    private static ClientDeadBodyAnimationKind ToClientDeadBodyAnimationKind(DeadBodyAnimationKind animationKind)
    {
        return animationKind switch
        {
            DeadBodyAnimationKind.Rifle => ClientDeadBodyAnimationKind.Rifle,
            DeadBodyAnimationKind.Severe => ClientDeadBodyAnimationKind.Severe,
            _ => ClientDeadBodyAnimationKind.Default,
        };
    }

    private sealed class ClientPluginStateView(Game1 game) : IOpenGarrisonClientReadOnlyState
    {
        public bool IsConnected => game._networkClient.IsConnected;

        public bool IsMainMenuOpen => game._mainMenuOpen;

        public bool IsGameplayActive => !game._startupSplashOpen && !game._mainMenuOpen;

        public bool IsGameplayInputBlocked => game.IsGameplayInputBlocked();

        public bool IsSpectator => game._networkClient.IsSpectator;

        public bool IsDeathCamActive => game._killCamEnabled
            && !game._world.LocalPlayer.IsAlive
            && game._world.LocalDeathCam is not null;

        public ulong WorldFrame => (ulong)Math.Max(0, game._world.Frame);

        public int TickRate => game._config.TicksPerSecond;

        public int LocalPingMilliseconds => game._networkClient.EstimatedPingMilliseconds;

        public string LevelName => game._world.Level.Name;

        public float LevelWidth => game._world.Bounds.Width;

        public float LevelHeight => game._world.Bounds.Height;

        public int ViewportWidth => game.ViewportWidth;

        public int ViewportHeight => game.ViewportHeight;

        public int? LocalPlayerId => game.GetClientPluginLocalPlayerId();

        public ClientPluginTeam LocalPlayerTeam => game._networkClient.IsSpectator
            ? ClientPluginTeam.None
            : ToClientPluginTeam(game._world.LocalPlayer.Team);

        public ClientPluginClass LocalPlayerClass => game._networkClient.IsSpectator
            ? ClientPluginClass.Unknown
            : ToClientPluginClass(game._world.LocalPlayer.ClassId);

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
            return game.FindPlayerById(playerId) is { } player
                && game.GetPlayerIsSpyCloaked(player);
        }

        public bool WasKeyPressedThisFrame(Keys key)
        {
            return game._clientPluginKeyboard.IsKeyDown(key)
                && !game._clientPluginPreviousKeyboard.IsKeyDown(key);
        }

        public IReadOnlyList<ClientPlayerMarker> GetPlayerMarkers()
        {
            return game.GetClientPluginPlayerMarkers();
        }

        public IReadOnlyList<ClientSentryMarker> GetSentryMarkers()
        {
            return game.GetClientPluginSentryMarkers();
        }

        public IReadOnlyList<ClientObjectiveMarker> GetObjectiveMarkers()
        {
            return game.GetClientPluginObjectiveMarkers();
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

        public void FillScreenRectangle(Rectangle rectangle, Color color)
        {
            game._spriteBatch.Draw(game._pixel, rectangle, color);
        }

        public void DrawScreenRectangleOutline(Rectangle rectangle, Color color, int thickness = 1)
        {
            var safeThickness = Math.Max(1, thickness);
            game._spriteBatch.Draw(game._pixel, new Rectangle(rectangle.X, rectangle.Y, rectangle.Width, safeThickness), color);
            game._spriteBatch.Draw(game._pixel, new Rectangle(rectangle.X, rectangle.Bottom - safeThickness, rectangle.Width, safeThickness), color);
            game._spriteBatch.Draw(game._pixel, new Rectangle(rectangle.X, rectangle.Y, safeThickness, rectangle.Height), color);
            game._spriteBatch.Draw(game._pixel, new Rectangle(rectangle.Right - safeThickness, rectangle.Y, safeThickness, rectangle.Height), color);
        }

        public void DrawScreenLine(Vector2 start, Vector2 endPoint, Color color, float thickness = 1f)
        {
            var edge = endPoint - start;
            var angle = MathF.Atan2(edge.Y, edge.X);
            var length = edge.Length();
            if (length <= 0.01f)
            {
                return;
            }

            game._spriteBatch.Draw(
                game._pixel,
                start,
                null,
                color,
                angle,
                Vector2.Zero,
                new Vector2(length, thickness),
                SpriteEffects.None,
                0f);
        }

        public bool TryDrawScreenSprite(string spriteName, int frameIndex, Vector2 position, Color tint, Vector2 scale)
        {
            return game.TryDrawScreenSprite(spriteName, frameIndex, position, tint, scale);
        }

        public bool TryDrawWorldSprite(string spriteName, int frameIndex, Vector2 worldPosition, Color tint, float rotation = 0f)
        {
            return game.TryDrawSprite(spriteName, frameIndex, worldPosition.X, worldPosition.Y, cameraTopLeft, tint, rotation);
        }

        public bool TryGetLevelBackgroundTexture(out Texture2D texture)
        {
            texture = game.GetClientPluginLevelBackgroundTexture()!;
            return texture is not null;
        }

        public void DrawScreenTexture(
            Texture2D texture,
            Vector2 position,
            Color tint,
            Vector2 scale,
            Rectangle? sourceRectangle = null,
            float rotation = 0f,
            Vector2? origin = null)
        {
            game._spriteBatch.Draw(
                texture,
                position,
                sourceRectangle,
                tint,
                rotation,
                origin ?? Vector2.Zero,
                scale,
                SpriteEffects.None,
                0f);
        }

        public void DrawWorldTexture(
            Texture2D texture,
            Vector2 worldPosition,
            Color tint,
            Vector2 scale,
            Rectangle? sourceRectangle = null,
            float rotation = 0f,
            Vector2? origin = null)
        {
            DrawScreenTexture(
                texture,
                worldPosition - cameraTopLeft,
                tint,
                scale,
                sourceRectangle,
                rotation,
                origin);
        }
    }
}
