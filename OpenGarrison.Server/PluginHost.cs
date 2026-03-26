using OpenGarrison.Server.Plugins;

namespace OpenGarrison.Server;

internal sealed class PluginHost
{
    private readonly PluginCommandRegistry _commandRegistry;
    private readonly IOpenGarrisonServerReadOnlyState _serverState;
    private readonly IOpenGarrisonServerAdminOperations _adminOperations;
    private readonly Action<string> _log;
    private readonly string _pluginsRootDirectory;
    private readonly string _pluginConfigRoot;
    private readonly string _mapsDirectory;
    private readonly List<PluginLoader.LoadedPlugin> _loadedPlugins = new();

    public PluginHost(
        PluginCommandRegistry commandRegistry,
        IOpenGarrisonServerReadOnlyState serverState,
        IOpenGarrisonServerAdminOperations adminOperations,
        string pluginsDirectory,
        string pluginConfigRoot,
        string mapsDirectory,
        Action<string> log)
    {
        _commandRegistry = commandRegistry;
        _serverState = serverState;
        _adminOperations = adminOperations;
        _pluginsRootDirectory = pluginsDirectory;
        _pluginConfigRoot = pluginConfigRoot;
        _mapsDirectory = mapsDirectory;
        _log = log;
    }

    public IReadOnlyList<string> LoadedPluginIds => _loadedPlugins
        .Select(entry => entry.Plugin.Id)
        .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public void LoadPlugins()
    {
        _loadedPlugins.Clear();
        _loadedPlugins.AddRange(PluginLoader.LoadFromSearchDirectories(BuildPluginSearchDirectories(), CreateContext, _log));
        foreach (var loadedPlugin in _loadedPlugins)
        {
            _log($"[plugin] loaded {loadedPlugin.Plugin.DisplayName} ({loadedPlugin.Plugin.Id} {loadedPlugin.Plugin.Version})");
        }
    }

    public void LoadPlugins(IEnumerable<System.Reflection.Assembly> assemblies)
    {
        _loadedPlugins.Clear();
        _loadedPlugins.AddRange(PluginLoader.LoadFromAssemblies(assemblies, CreateContext, _log));
        foreach (var loadedPlugin in _loadedPlugins)
        {
            _log($"[plugin] loaded {loadedPlugin.Plugin.DisplayName} ({loadedPlugin.Plugin.Id} {loadedPlugin.Plugin.Version})");
        }
    }

    public void NotifyServerStarting() => Dispatch<IOpenGarrisonServerLifecycleHooks>(hook => hook.OnServerStarting());

    public void NotifyServerStarted() => Dispatch<IOpenGarrisonServerLifecycleHooks>(hook => hook.OnServerStarted());

    public void NotifyServerStopping() => Dispatch<IOpenGarrisonServerLifecycleHooks>(hook => hook.OnServerStopping());

    public void NotifyServerStopped() => Dispatch<IOpenGarrisonServerLifecycleHooks>(hook => hook.OnServerStopped());

    public void NotifyServerHeartbeat(TimeSpan uptime) => Dispatch<IOpenGarrisonServerUpdateHooks>(hook => hook.OnServerHeartbeat(uptime));

    public void NotifyHelloReceived(HelloReceivedEvent e) => Dispatch<IOpenGarrisonServerClientHooks>(hook => hook.OnHelloReceived(e));

    public void NotifyClientConnected(ClientConnectedEvent e) => Dispatch<IOpenGarrisonServerClientHooks>(hook => hook.OnClientConnected(e));

    public void NotifyClientDisconnected(ClientDisconnectedEvent e) => Dispatch<IOpenGarrisonServerClientHooks>(hook => hook.OnClientDisconnected(e));

    public void NotifyPasswordAccepted(PasswordAcceptedEvent e) => Dispatch<IOpenGarrisonServerClientHooks>(hook => hook.OnPasswordAccepted(e));

    public void NotifyPlayerTeamChanged(PlayerTeamChangedEvent e) => Dispatch<IOpenGarrisonServerClientHooks>(hook => hook.OnPlayerTeamChanged(e));

    public void NotifyPlayerClassChanged(PlayerClassChangedEvent e) => Dispatch<IOpenGarrisonServerClientHooks>(hook => hook.OnPlayerClassChanged(e));

    public void NotifyChatReceived(ChatReceivedEvent e) => Dispatch<IOpenGarrisonServerChatHooks>(hook => hook.OnChatReceived(e));

    public bool TryHandleChatMessage(ChatReceivedEvent e)
    {
        var context = new OpenGarrisonServerChatMessageContext(_serverState, _adminOperations);
        foreach (var loadedPlugin in _loadedPlugins)
        {
            if (loadedPlugin.Plugin is not IOpenGarrisonServerChatCommandHooks hook)
            {
                continue;
            }

            try
            {
                if (hook.TryHandleChatMessage(context, e))
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                _log($"[plugin] chat hook failed for {loadedPlugin.Plugin.Id}: {ex.Message}");
            }
        }

        return false;
    }

    public void NotifyMapChanging(MapChangingEvent e) => Dispatch<IOpenGarrisonServerMapHooks>(hook => hook.OnMapChanging(e));

    public void NotifyMapChanged(MapChangedEvent e) => Dispatch<IOpenGarrisonServerMapHooks>(hook => hook.OnMapChanged(e));

    public void NotifyScoreChanged(ScoreChangedEvent e) => Dispatch<IOpenGarrisonServerGameplayHooks>(hook => hook.OnScoreChanged(e));

    public void NotifyRoundEnded(RoundEndedEvent e) => Dispatch<IOpenGarrisonServerGameplayHooks>(hook => hook.OnRoundEnded(e));

    public void NotifyKillFeedEntry(KillFeedEvent e) => Dispatch<IOpenGarrisonServerGameplayHooks>(hook => hook.OnKillFeedEntry(e));

    public void ShutdownPlugins()
    {
        for (var index = _loadedPlugins.Count - 1; index >= 0; index -= 1)
        {
            try
            {
                _loadedPlugins[index].Plugin.Shutdown();
            }
            catch (Exception ex)
            {
                _log($"[plugin] shutdown failed for {_loadedPlugins[index].Plugin.Id}: {ex.Message}");
            }
        }
    }

    private IEnumerable<PluginLoader.PluginSearchDirectory> BuildPluginSearchDirectories()
    {
        var scopedServerPluginsDirectory = Path.Combine(_pluginsRootDirectory, "Server");
        yield return new PluginLoader.PluginSearchDirectory(scopedServerPluginsDirectory, SearchOption.AllDirectories);

        if (LegacyServerPluginsExist())
        {
            _log("[plugin] discovered legacy server plugins under Plugins root; prefer Plugins/Server/<PluginFolder>/ for new installs.");
            yield return new PluginLoader.PluginSearchDirectory(_pluginsRootDirectory, SearchOption.TopDirectoryOnly);
        }
    }

    private bool LegacyServerPluginsExist()
    {
        if (!Directory.Exists(_pluginsRootDirectory))
        {
            return false;
        }

        return Directory.EnumerateFiles(_pluginsRootDirectory, "*.dll", SearchOption.TopDirectoryOnly).Any();
    }

    private IOpenGarrisonServerPluginContext CreateContext(IOpenGarrisonServerPlugin plugin, string pluginDirectory)
    {
        pluginDirectory = ResolvePluginDirectory(plugin, pluginDirectory);
        var configDirectory = ResolveConfigDirectory(plugin.Id);
        Directory.CreateDirectory(pluginDirectory);
        Directory.CreateDirectory(configDirectory);
        Directory.CreateDirectory(_mapsDirectory);
        return new ServerPluginContext(
            plugin.Id,
            pluginDirectory,
            configDirectory,
            _mapsDirectory,
            _serverState,
            _adminOperations,
            _commandRegistry,
            _log);
    }

    private string ResolvePluginDirectory(IOpenGarrisonServerPlugin plugin, string pluginDirectory)
    {
        if (!string.IsNullOrWhiteSpace(pluginDirectory))
        {
            return pluginDirectory;
        }

        return Path.Combine(_pluginsRootDirectory, "Server", plugin.Id);
    }

    private string ResolveConfigDirectory(string pluginId)
    {
        var scopedConfigDirectory = Path.Combine(_pluginConfigRoot, "server", pluginId);
        var legacyConfigDirectory = Path.Combine(_pluginConfigRoot, pluginId);
        return Directory.Exists(legacyConfigDirectory) && !Directory.Exists(scopedConfigDirectory)
            ? legacyConfigDirectory
            : scopedConfigDirectory;
    }

    private void Dispatch<THook>(Action<THook> callback) where THook : class
    {
        foreach (var loadedPlugin in _loadedPlugins)
        {
            if (loadedPlugin.Plugin is not THook hook)
            {
                continue;
            }

            try
            {
                callback(hook);
            }
            catch (Exception ex)
            {
                _log($"[plugin] hook failed for {loadedPlugin.Plugin.Id}: {ex.Message}");
            }
        }
    }
}
