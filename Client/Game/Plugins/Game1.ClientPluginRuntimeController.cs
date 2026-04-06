#nullable enable

using System.IO;
using Microsoft.Xna.Framework;
using OpenGarrison.Client.Plugins;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class ClientPluginRuntimeController
    {
        private readonly Game1 _game;

        public ClientPluginRuntimeController(Game1 game)
        {
            _game = game;
        }

        public void InitializeClientPlugins()
        {
            var pluginsDirectory = Path.Combine(RuntimePaths.ApplicationRoot, "Plugins", "Client");
            var pluginConfigRoot = Path.Combine(RuntimePaths.ConfigDirectory, "plugins", "client");
            var pluginStatePath = Path.Combine(pluginConfigRoot, "plugins.json");
            _game._clientPluginStateView = new ClientPluginStateView(_game);
            _game._clientPluginHost = new ClientPluginHost(_game._clientPluginStateView, _game.GraphicsDevice, pluginsDirectory, pluginConfigRoot, pluginStatePath, _game.AddConsoleLine);
            _game._clientPluginHost.LoadPlugins();
            _game.ResetClientPluginGameplayEventState();
            _game._clientPluginHost.NotifyClientStarting();
        }

        public void NotifyClientPluginsStarted()
        {
            _game._clientPluginHost?.NotifyClientStarted();
        }

        public void ShutdownClientPlugins()
        {
            if (_game._clientPluginHost is null)
            {
                return;
            }

            _game._clientPluginHost.NotifyClientStopping();
            _game._clientPluginHost.ShutdownPlugins();
            _game._clientPluginHost.NotifyClientStopped();
            _game._clientPluginHost = null;
            _game._clientPluginStateView = null;
        }

        public void NotifyClientPluginsFrame(GameTime gameTime, int clientTicks)
        {
            _game._clientPluginHost?.NotifyClientFrame(new ClientFrameEvent(
                (float)gameTime.ElapsedGameTime.TotalSeconds,
                clientTicks,
                _game._mainMenuOpen,
                !_game._startupSplashOpen && !_game._mainMenuOpen,
                _game._networkClient.IsConnected,
                _game._networkClient.IsSpectator));
        }
    }
}
