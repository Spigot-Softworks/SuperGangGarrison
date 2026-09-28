#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Client.Plugins;
using OpenGarrison.Core;
using OpenGarrison.Protocol;

using static OpenGarrison.Client.Game1;

namespace OpenGarrison.Client;

public sealed class ClientPluginUiBridgeController
    {
        private readonly IPluginContext _context;

        public ClientPluginUiBridgeController(IPluginContext context)
        {
            _context = context;
        }

        public void DrawClientPluginHud(Vector2 cameraTopLeft)
        {
            if (_context._clientPluginHost is null)
            {
                return;
            }

            _context._clientPluginHost.NotifyGameplayHudDraw(_context.CreateGameplayHudCanvas(cameraTopLeft));
        }

        public ClientBubbleMenuUpdateResult? TryHandleClientPluginBubbleMenuInput(ClientBubbleMenuInputState inputState)
        {
            return _context._clientPluginHost?.TryHandleBubbleMenuInput(inputState);
        }

        public bool TryDrawClientPluginBubbleMenu(Vector2 cameraTopLeft, ClientBubbleMenuRenderState renderState)
        {
            return _context._clientPluginHost?.TryDrawBubbleMenu(_context.CreateGameplayHudCanvas(cameraTopLeft), renderState) ?? false;
        }

        public bool HasClientPluginBubbleMenuOverride()
        {
            return _context._clientPluginHost?.HasLoadedBubbleMenuOverride() ?? false;
        }

        public bool TryDrawClientPluginDeadBody(Vector2 cameraTopLeft, ClientDeadBodyRenderState deadBody)
        {
            return _context._clientPluginHost?.TryDrawDeadBody(_context.CreateGameplayHudCanvas(cameraTopLeft), deadBody) ?? false;
        }

        public ClientPluginMainMenuBackgroundOverride? GetClientPluginMainMenuBackgroundOverride()
        {
            return _context._clientPluginHost?.GetMainMenuBackgroundOverride();
        }

        public void NotifyClientPluginsWorldSound(WorldSoundEvent soundEvent)
        {
            _context._clientPluginHost?.NotifyWorldSound(new ClientWorldSoundEvent(
                soundEvent.SoundName,
                new Vector2(soundEvent.X, soundEvent.Y)));
        }

        public void NotifyClientPluginsServerMessage(ServerPluginMessage message)
        {
            _context._clientPluginHost?.NotifyServerPluginMessage(new ClientPluginMessageEnvelope(
                message.SourcePluginId,
                message.TargetPluginId,
                message.MessageTypeName,
                message.Payload,
                message.PayloadFormat,
                message.SchemaVersion));
        }

        public Vector2 GetClientPluginCameraOffset()
        {
            return _context._clientPluginHost?.GetCameraOffset() ?? Vector2.Zero;
        }

        public int? GetClientPluginLocalPlayerId()
        {
            if (_context.IsLocalSpectatorPresentationActive())
            {
                return null;
            }

            if (_context._networkClient.IsConnected)
            {
                return _context._localPlayerSnapshotEntityId;
            }

            return _context._world.LocalPlayer.Id;
        }

        public Vector2 GetCurrentClientPluginCameraTopLeft()
        {
            if (_context._startupSplashOpen || _context._mainMenuOpen)
            {
                return Vector2.Zero;
            }

            var mouse = _context.GetFrameMouseState();
            return _context._hasGameplayCameraTopLeft
                ? _context._gameplayCameraTopLeft
                : _context.GetUntrackedCameraTopLeft(_context.ViewportWidth, _context.ViewportHeight, mouse.X, mouse.Y);
        }

        public Texture2D? GetClientPluginLevelBackgroundTexture()
        {
            var backgroundName = _context._world.Level.BackgroundAssetName;
            return string.IsNullOrWhiteSpace(backgroundName)
                ? null
                : _context._runtimeAssets.GetBackground(backgroundName);
        }
}
