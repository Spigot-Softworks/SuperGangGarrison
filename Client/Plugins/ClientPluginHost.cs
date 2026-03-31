using System.Reflection;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Client.Plugins;

namespace OpenGarrison.Client;

internal sealed class ClientPluginHost
{
    private readonly IOpenGarrisonClientReadOnlyState _clientState;
    private readonly GraphicsDevice _graphicsDevice;
    private readonly Action<string> _log;
    private readonly string _pluginsDirectory;
    private readonly string _pluginConfigRoot;
    private readonly ClientPluginStateStore _stateStore;
    private readonly List<ClientPluginLoader.DiscoveredPlugin> _discoveredPlugins = new();
    private readonly List<LoadedPluginEntry> _loadedPlugins = new();
    private ClientPluginLifecyclePhase _lifecyclePhase;

    public ClientPluginHost(
        IOpenGarrisonClientReadOnlyState clientState,
        GraphicsDevice graphicsDevice,
        string pluginsDirectory,
        string pluginConfigRoot,
        string pluginStatePath,
        Action<string> log)
    {
        _clientState = clientState;
        _graphicsDevice = graphicsDevice;
        _pluginsDirectory = pluginsDirectory;
        _pluginConfigRoot = pluginConfigRoot;
        _stateStore = new ClientPluginStateStore(pluginStatePath, log);
        _log = log;
    }

    public IReadOnlyList<string> LoadedPluginIds => _loadedPlugins
        .Select(entry => entry.DiscoveredPlugin.PluginId)
        .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public void LoadPlugins()
    {
        LoadDiscoveredPlugins(ClientPluginLoader.DiscoverFromDirectory(_pluginsDirectory, _log));
    }

    public void LoadPlugins(IEnumerable<Assembly> assemblies)
    {
        LoadDiscoveredPlugins(ClientPluginLoader.DiscoverFromAssemblies(assemblies, _log));
    }

    public bool IsPluginEnabled(string pluginId)
    {
        return _stateStore.IsPluginEnabled(pluginId);
    }

    public bool SetPluginEnabled(string pluginId, bool enabled)
    {
        var discoveredPlugin = FindDiscoveredPlugin(pluginId);
        if (discoveredPlugin is null)
        {
            _log($"[plugin] unknown client plugin id \"{pluginId}\".");
            return false;
        }

        _stateStore.SetPluginEnabled(pluginId, enabled);
        return enabled
            ? TryLoadPlugin(discoveredPlugin, catchUpLifecycle: true)
            : UnloadPlugin(pluginId, notifyLifecycle: true);
    }

    public void NotifyClientStarting()
    {
        _lifecyclePhase = ClientPluginLifecyclePhase.Starting;
        Dispatch<IOpenGarrisonClientLifecycleHooks>(hook => hook.OnClientStarting());
    }

    public void NotifyClientStarted()
    {
        _lifecyclePhase = ClientPluginLifecyclePhase.Started;
        Dispatch<IOpenGarrisonClientLifecycleHooks>(hook => hook.OnClientStarted());
    }

    public void NotifyClientStopping()
    {
        _lifecyclePhase = ClientPluginLifecyclePhase.Stopping;
        Dispatch<IOpenGarrisonClientLifecycleHooks>(hook => hook.OnClientStopping());
    }

    public void NotifyClientStopped()
    {
        Dispatch<IOpenGarrisonClientLifecycleHooks>(hook => hook.OnClientStopped());
        _lifecyclePhase = ClientPluginLifecyclePhase.Stopped;
    }

    public void NotifyClientFrame(ClientFrameEvent e) => Dispatch<IOpenGarrisonClientUpdateHooks>(hook => hook.OnClientFrame(e));

    public void NotifyGameplayHudDraw(IOpenGarrisonClientHudCanvas canvas) => Dispatch<IOpenGarrisonClientHudHooks>(hook => hook.OnGameplayHudDraw(canvas));

    public void NotifyLocalDamage(LocalDamageEvent e) => Dispatch<IOpenGarrisonClientDamageHooks>(hook => hook.OnLocalDamage(e));

    public ClientBubbleMenuUpdateResult? TryHandleBubbleMenuInput(ClientBubbleMenuInputState inputState)
    {
        for (var index = 0; index < _loadedPlugins.Count; index += 1)
        {
            var entry = _loadedPlugins[index];
            if (entry.LoadedPlugin.Plugin is not IOpenGarrisonClientBubbleMenuHooks hook)
            {
                continue;
            }

            try
            {
                var result = hook.TryHandleBubbleMenuInput(inputState);
                if (result is not null)
                {
                    return result;
                }
            }
            catch (Exception ex)
            {
                _log($"[plugin] bubble-menu hook failed for {entry.DiscoveredPlugin.PluginId}: {ex.Message}");
            }
        }

        return null;
    }

    public bool TryDrawBubbleMenu(IOpenGarrisonClientHudCanvas canvas, ClientBubbleMenuRenderState renderState)
    {
        for (var index = 0; index < _loadedPlugins.Count; index += 1)
        {
            var entry = _loadedPlugins[index];
            if (entry.LoadedPlugin.Plugin is not IOpenGarrisonClientBubbleMenuHooks hook)
            {
                continue;
            }

            try
            {
                if (hook.TryDrawBubbleMenu(canvas, renderState))
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                _log($"[plugin] bubble-menu draw failed for {entry.DiscoveredPlugin.PluginId}: {ex.Message}");
            }
        }

        return false;
    }

    public bool HasLoadedBubbleMenuOverride()
    {
        for (var index = 0; index < _loadedPlugins.Count; index += 1)
        {
            if (_loadedPlugins[index].LoadedPlugin.Plugin is IOpenGarrisonClientBubbleMenuHooks)
            {
                return true;
            }
        }

        return false;
    }

    public bool TryDrawDeadBody(IOpenGarrisonClientHudCanvas canvas, ClientDeadBodyRenderState deadBody)
    {
        for (var index = 0; index < _loadedPlugins.Count; index += 1)
        {
            var entry = _loadedPlugins[index];
            if (entry.LoadedPlugin.Plugin is not IOpenGarrisonClientDeadBodyHooks hook)
            {
                continue;
            }

            try
            {
                if (hook.TryDrawDeadBody(canvas, deadBody))
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                _log($"[plugin] dead-body draw failed for {entry.DiscoveredPlugin.PluginId}: {ex.Message}");
            }
        }

        return false;
    }

    public ClientPluginMainMenuBackgroundOverride? GetMainMenuBackgroundOverride()
    {
        for (var index = 0; index < _loadedPlugins.Count; index += 1)
        {
            var entry = _loadedPlugins[index];
            if (entry.LoadedPlugin.Plugin is not IOpenGarrisonClientMainMenuHooks hook)
            {
                continue;
            }

            try
            {
                var backgroundOverride = hook.GetMainMenuBackgroundOverride();
                if (backgroundOverride is not null)
                {
                    return backgroundOverride;
                }
            }
            catch (Exception ex)
            {
                _log($"[plugin] main-menu hook failed for {entry.DiscoveredPlugin.PluginId}: {ex.Message}");
            }
        }

        return null;
    }

    public IReadOnlyList<ClientPluginOptionsSection> GetOptionsSections()
    {
        var sections = new List<ClientPluginOptionsSection>();
        foreach (var entry in GetPluginOptionsEntries())
        {
            sections.AddRange(entry.Sections);
        }

        return sections;
    }

    public IReadOnlyList<ClientPluginOptionsEntry> GetPluginOptionsEntries()
    {
        var entries = new List<ClientPluginOptionsEntry>(_discoveredPlugins.Count);
        for (var index = 0; index < _discoveredPlugins.Count; index += 1)
        {
            var discoveredPlugin = _discoveredPlugins[index];
            var isEnabled = _stateStore.IsPluginEnabled(discoveredPlugin.PluginId);
            var loadedPlugin = FindLoadedPlugin(discoveredPlugin.PluginId);
            var sections = GetPluginOptionsSections(discoveredPlugin, loadedPlugin);
            entries.Add(new ClientPluginOptionsEntry(
                discoveredPlugin.PluginId,
                discoveredPlugin.DisplayName,
                discoveredPlugin.Version,
                isEnabled,
                loadedPlugin is not null,
                sections));
        }

        return entries;
    }

    public void ShutdownPlugins()
    {
        for (var index = _loadedPlugins.Count - 1; index >= 0; index -= 1)
        {
            try
            {
                _loadedPlugins[index].LoadedPlugin.Plugin.Shutdown();
            }
            catch (Exception ex)
            {
                _log($"[plugin] shutdown failed for {_loadedPlugins[index].DiscoveredPlugin.PluginId}: {ex.Message}");
            }
        }
    }

    private void LoadDiscoveredPlugins(IReadOnlyList<ClientPluginLoader.DiscoveredPlugin> discoveredPlugins)
    {
        ResetLoadedPluginsForDiscovery();
        _discoveredPlugins.Clear();
        _discoveredPlugins.AddRange(discoveredPlugins);

        for (var index = 0; index < _discoveredPlugins.Count; index += 1)
        {
            var discoveredPlugin = _discoveredPlugins[index];
            if (!_stateStore.IsPluginEnabled(discoveredPlugin.PluginId))
            {
                continue;
            }

            TryLoadPlugin(discoveredPlugin, catchUpLifecycle: false);
        }
    }

    private void ResetLoadedPluginsForDiscovery()
    {
        for (var index = _loadedPlugins.Count - 1; index >= 0; index -= 1)
        {
            try
            {
                _loadedPlugins[index].LoadedPlugin.Plugin.Shutdown();
            }
            catch (Exception ex)
            {
                _log($"[plugin] shutdown failed for {_loadedPlugins[index].DiscoveredPlugin.PluginId}: {ex.Message}");
            }
        }

        _loadedPlugins.Clear();
    }

    private List<ClientPluginOptionsSection> GetPluginOptionsSections(
        ClientPluginLoader.DiscoveredPlugin discoveredPlugin,
        LoadedPluginEntry? loadedPlugin)
    {
        if (loadedPlugin?.LoadedPlugin.Plugin is not IOpenGarrisonClientOptionsHooks hook)
        {
            return [];
        }

        try
        {
            var pluginSections = hook.GetOptionsSections();
            if (pluginSections.Count == 0)
            {
                return [];
            }

            var sections = new List<ClientPluginOptionsSection>(pluginSections.Count);
            for (var sectionIndex = 0; sectionIndex < pluginSections.Count; sectionIndex += 1)
            {
                var section = pluginSections[sectionIndex];
                sections.Add(string.IsNullOrWhiteSpace(section.Title)
                    ? section with { Title = discoveredPlugin.DisplayName }
                    : section);
            }

            return sections;
        }
        catch (Exception ex)
        {
            _log($"[plugin] options query failed for {discoveredPlugin.PluginId}: {ex.Message}");
            return [];
        }
    }

    private ClientPluginLoader.DiscoveredPlugin? FindDiscoveredPlugin(string pluginId)
    {
        for (var index = 0; index < _discoveredPlugins.Count; index += 1)
        {
            var discoveredPlugin = _discoveredPlugins[index];
            if (string.Equals(discoveredPlugin.PluginId, pluginId, StringComparison.OrdinalIgnoreCase))
            {
                return discoveredPlugin;
            }
        }

        return null;
    }

    private LoadedPluginEntry? FindLoadedPlugin(string pluginId)
    {
        for (var index = 0; index < _loadedPlugins.Count; index += 1)
        {
            if (string.Equals(_loadedPlugins[index].DiscoveredPlugin.PluginId, pluginId, StringComparison.OrdinalIgnoreCase))
            {
                return _loadedPlugins[index];
            }
        }

        return null;
    }

    private bool TryLoadPlugin(ClientPluginLoader.DiscoveredPlugin discoveredPlugin, bool catchUpLifecycle)
    {
        if (FindLoadedPlugin(discoveredPlugin.PluginId) is not null)
        {
            return true;
        }

        var loadedPlugin = ClientPluginLoader.TryLoadDiscoveredPlugin(discoveredPlugin, CreateContext, _log);
        if (loadedPlugin is null)
        {
            return false;
        }

        var entry = new LoadedPluginEntry(discoveredPlugin, loadedPlugin);
        _loadedPlugins.Add(entry);
        _log($"[plugin] loaded {discoveredPlugin.DisplayName} ({discoveredPlugin.PluginId} {discoveredPlugin.Version})");
        if (catchUpLifecycle)
        {
            CatchUpPluginLifecycle(entry);
        }

        return true;
    }

    private bool UnloadPlugin(string pluginId, bool notifyLifecycle)
    {
        for (var index = _loadedPlugins.Count - 1; index >= 0; index -= 1)
        {
            var entry = _loadedPlugins[index];
            if (!string.Equals(entry.DiscoveredPlugin.PluginId, pluginId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (notifyLifecycle)
            {
                DispatchHook<IOpenGarrisonClientLifecycleHooks>(entry, hook => hook.OnClientStopping(), "stopping");
            }

            try
            {
                entry.LoadedPlugin.Plugin.Shutdown();
            }
            catch (Exception ex)
            {
                _log($"[plugin] shutdown failed for {entry.DiscoveredPlugin.PluginId}: {ex.Message}");
            }

            if (notifyLifecycle)
            {
                DispatchHook<IOpenGarrisonClientLifecycleHooks>(entry, hook => hook.OnClientStopped(), "stopped");
            }

            _loadedPlugins.RemoveAt(index);
            _log($"[plugin] unloaded {entry.DiscoveredPlugin.DisplayName} ({entry.DiscoveredPlugin.PluginId})");
            return true;
        }

        return true;
    }

    private void CatchUpPluginLifecycle(LoadedPluginEntry entry)
    {
        switch (_lifecyclePhase)
        {
            case ClientPluginLifecyclePhase.Starting:
                DispatchHook<IOpenGarrisonClientLifecycleHooks>(entry, hook => hook.OnClientStarting(), "starting");
                break;
            case ClientPluginLifecyclePhase.Started:
                DispatchHook<IOpenGarrisonClientLifecycleHooks>(entry, hook => hook.OnClientStarting(), "starting");
                DispatchHook<IOpenGarrisonClientLifecycleHooks>(entry, hook => hook.OnClientStarted(), "started");
                break;
        }
    }

    private IOpenGarrisonClientPluginContext CreateContext(IOpenGarrisonClientPlugin plugin, string pluginDirectory)
    {
        var configDirectory = Path.Combine(_pluginConfigRoot, plugin.Id);
        Directory.CreateDirectory(pluginDirectory);
        Directory.CreateDirectory(configDirectory);
        return new ClientPluginContext(
            plugin.Id,
            pluginDirectory,
            configDirectory,
            _graphicsDevice,
            _clientState,
            _log);
    }

    private void Dispatch<THook>(Action<THook> callback) where THook : class
    {
        for (var index = 0; index < _loadedPlugins.Count; index += 1)
        {
            DispatchHook(_loadedPlugins[index], callback, "runtime");
        }
    }

    private void DispatchHook<THook>(LoadedPluginEntry entry, Action<THook> callback, string hookStage) where THook : class
    {
        if (entry.LoadedPlugin.Plugin is not THook hook)
        {
            return;
        }

        try
        {
            callback(hook);
        }
        catch (Exception ex)
        {
            _log($"[plugin] {hookStage} hook failed for {entry.DiscoveredPlugin.PluginId}: {ex.Message}");
        }
    }

    private enum ClientPluginLifecyclePhase
    {
        Created,
        Starting,
        Started,
        Stopping,
        Stopped,
    }

    private sealed record LoadedPluginEntry(
        ClientPluginLoader.DiscoveredPlugin DiscoveredPlugin,
        ClientPluginLoader.LoadedPlugin LoadedPlugin);
}

internal sealed record ClientPluginOptionsEntry(
    string PluginId,
    string DisplayName,
    Version Version,
    bool IsEnabled,
    bool IsLoaded,
    IReadOnlyList<ClientPluginOptionsSection> Sections);
