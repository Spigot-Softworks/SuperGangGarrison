#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Client.Plugins;
using OpenGarrison.Core;
using OpenGarrison.Protocol;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class ClientPluginUiBridgeController
    {
        private readonly Game1 _game;

        public ClientPluginUiBridgeController(Game1 game)
        {
            _game = game;
        }

        public void DrawClientPluginHud(Vector2 cameraTopLeft)
        {
            if (_game._clientPluginHost is null)
            {
                return;
            }

            _game._clientPluginHost.NotifyGameplayHudDraw(new GameplayHudCanvas(_game, cameraTopLeft));
        }

        public ClientBubbleMenuUpdateResult? TryHandleClientPluginBubbleMenuInput(ClientBubbleMenuInputState inputState)
        {
            return _game._clientPluginHost?.TryHandleBubbleMenuInput(inputState);
        }

        public bool TryDrawClientPluginBubbleMenu(Vector2 cameraTopLeft, ClientBubbleMenuRenderState renderState)
        {
            return _game._clientPluginHost?.TryDrawBubbleMenu(new GameplayHudCanvas(_game, cameraTopLeft), renderState) ?? false;
        }

        public bool HasClientPluginBubbleMenuOverride()
        {
            return _game._clientPluginHost?.HasLoadedBubbleMenuOverride() ?? false;
        }

        public bool TryDrawClientPluginDeadBody(Vector2 cameraTopLeft, ClientDeadBodyRenderState deadBody)
        {
            return _game._clientPluginHost?.TryDrawDeadBody(new GameplayHudCanvas(_game, cameraTopLeft), deadBody) ?? false;
        }

        public ClientPluginMainMenuBackgroundOverride? GetClientPluginMainMenuBackgroundOverride()
        {
            return _game._clientPluginHost?.GetMainMenuBackgroundOverride();
        }

        public void NotifyClientPluginsWorldSound(WorldSoundEvent soundEvent)
        {
            _game._clientPluginHost?.NotifyWorldSound(new ClientWorldSoundEvent(
                soundEvent.SoundName,
                new Vector2(soundEvent.X, soundEvent.Y)));
        }

        public void NotifyClientPluginsServerMessage(ServerPluginMessage message)
        {
            _game._clientPluginHost?.NotifyServerPluginMessage(new ClientPluginMessageEnvelope(
                message.SourcePluginId,
                message.TargetPluginId,
                message.MessageTypeName,
                message.Payload,
                message.PayloadFormat,
                message.SchemaVersion));
        }

        public Vector2 GetClientPluginCameraOffset()
        {
            return _game._clientPluginHost?.GetCameraOffset() ?? Vector2.Zero;
        }

        public int? GetClientPluginLocalPlayerId()
        {
            if (_game._networkClient.IsSpectator)
            {
                return null;
            }

            if (_game._networkClient.IsConnected)
            {
                return _game._localPlayerSnapshotEntityId;
            }

            return _game._world.LocalPlayer.Id;
        }

        public Vector2 GetCurrentClientPluginCameraTopLeft()
        {
            if (_game._startupSplashOpen || _game._mainMenuOpen)
            {
                return Vector2.Zero;
            }

            var mouse = _game.GetScaledMouseState(_game.GetConstrainedMouseState(Mouse.GetState()));
            return RoundToSourcePixels(_game.CalculateBaseCameraTopLeft(_game.ViewportWidth, _game.ViewportHeight, mouse.X, mouse.Y, trackLiveCamera: false));
        }

        public Texture2D? GetClientPluginLevelBackgroundTexture()
        {
            var backgroundName = _game._world.Level.BackgroundAssetName;
            return string.IsNullOrWhiteSpace(backgroundName)
                ? null
                : _game._runtimeAssets.GetBackground(backgroundName);
        }
    }
}
