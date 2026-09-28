#nullable enable

using System.IO;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using OpenGarrison.Client.Plugins;
using OpenGarrison.Core;

using static OpenGarrison.Client.Game1;

namespace OpenGarrison.Client;

public sealed class ClientPluginRuntimeController
    {
        private readonly IPluginContext _context;

        public ClientPluginRuntimeController(IPluginContext context)
        {
            _context = context;
        }

        public void InitializeClientPlugins()
        {
            var pluginsDirectory = Path.Combine(RuntimePaths.ApplicationRoot, "Plugins", "Client");
            var pluginConfigRoot = Path.Combine(RuntimePaths.ConfigDirectory, "plugins", "client");
            var pluginStatePath = Path.Combine(pluginConfigRoot, "plugins.json");
            if (!OperatingSystem.IsBrowser())
            {
                if (!PackagedClientPluginBootstrapper.TryPrepareRuntimePlugins(pluginsDirectory, out var packagedPluginError))
                {
                    _context.AddConsoleLine(packagedPluginError);
                }

                _context._clientPluginStateView = _context.CreateClientPluginStateView();
                _context._clientPluginHost = _context.CreateClientPluginHost(pluginsDirectory, pluginConfigRoot, pluginStatePath);
                _context._clientPluginHost.LoadPlugins();
                _context.ResetClientPluginGameplayEventState();
                _context._clientPluginHost.NotifyClientStarting();
                return;
            }

            _context._clientPluginStateView = _context.CreateClientPluginStateView();
            _context._clientPluginHost = _context.CreateClientPluginHost(pluginsDirectory, pluginConfigRoot, pluginStatePath);
            _context._clientPluginHost.LoadPlugins();
            _context.ResetClientPluginGameplayEventState();
            _context._clientPluginHost.NotifyClientStarting();
        }

        public void NotifyClientPluginsStarted()
        {
            _context._clientPluginHost?.NotifyClientStarted();
        }

        public void ShutdownClientPlugins()
        {
            if (_context._clientPluginHost is null)
            {
                return;
            }

            _context._clientPluginHost.NotifyClientStopping();
            _context._clientPluginHost.NotifyClientStopped();
            _context._clientPluginHost.ShutdownPlugins();
            _context._clientPluginHost = null;
            _context._clientPluginStateView = null;
        }

        public void NotifyClientPluginsFrame(GameTime gameTime, int clientTicks)
        {
            var notifyStartTimestamp = _context.IsClientPerformanceDiagnosticsEnabled() ? Stopwatch.GetTimestamp() : 0L;
            _context._clientPluginHost?.NotifyClientFrame(new ClientFrameEvent(
                (float)gameTime.ElapsedGameTime.TotalSeconds,
                clientTicks,
                _context._mainMenuOpen,
                !_context._startupSplashOpen && !_context._mainMenuOpen,
                _context._networkClient.IsConnected,
                _context.IsLocalSpectatorPresentationActive()));
            if (notifyStartTimestamp > 0)
            {
                _context.RecordClientPerformanceMetric(ClientPerformanceMetric.PluginFrame, Game1.GetDiagnosticsElapsedMilliseconds(notifyStartTimestamp));
            }
        }
}
