using OpenGarrison.Client.Plugins;

namespace OpenGarrison.Client;

internal sealed class ClientPluginHost
{
    private readonly IOpenGarrisonClientReadOnlyState _clientState;
    private readonly Action<string> _log;
    private readonly string _pluginsDirectory;
    private readonly string _pluginConfigRoot;
    private readonly List<ClientPluginLoader.LoadedPlugin> _loadedPlugins = new();

    public ClientPluginHost(
        IOpenGarrisonClientReadOnlyState clientState,
        string pluginsDirectory,
        string pluginConfigRoot,
        Action<string> log)
    {
        _clientState = clientState;
        _pluginsDirectory = pluginsDirectory;
        _pluginConfigRoot = pluginConfigRoot;
        _log = log;
    }

    public IReadOnlyList<string> LoadedPluginIds => _loadedPlugins
        .Select(entry => entry.Plugin.Id)
        .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public void LoadPlugins()
    {
        _loadedPlugins.Clear();
        _loadedPlugins.AddRange(ClientPluginLoader.LoadFromDirectory(_pluginsDirectory, CreateContext, _log));
        foreach (var loadedPlugin in _loadedPlugins)
        {
            _log($"[plugin] loaded {loadedPlugin.Plugin.DisplayName} ({loadedPlugin.Plugin.Id} {loadedPlugin.Plugin.Version})");
        }
    }

    public void LoadPlugins(IEnumerable<System.Reflection.Assembly> assemblies)
    {
        _loadedPlugins.Clear();
        _loadedPlugins.AddRange(ClientPluginLoader.LoadFromAssemblies(assemblies, CreateContext, _log));
        foreach (var loadedPlugin in _loadedPlugins)
        {
            _log($"[plugin] loaded {loadedPlugin.Plugin.DisplayName} ({loadedPlugin.Plugin.Id} {loadedPlugin.Plugin.Version})");
        }
    }

    public void NotifyClientStarting() => Dispatch<IOpenGarrisonClientLifecycleHooks>(hook => hook.OnClientStarting());

    public void NotifyClientStarted() => Dispatch<IOpenGarrisonClientLifecycleHooks>(hook => hook.OnClientStarted());

    public void NotifyClientStopping() => Dispatch<IOpenGarrisonClientLifecycleHooks>(hook => hook.OnClientStopping());

    public void NotifyClientStopped() => Dispatch<IOpenGarrisonClientLifecycleHooks>(hook => hook.OnClientStopped());

    public void NotifyClientFrame(ClientFrameEvent e) => Dispatch<IOpenGarrisonClientUpdateHooks>(hook => hook.OnClientFrame(e));

    public void NotifyGameplayHudDraw(IOpenGarrisonClientHudCanvas canvas) => Dispatch<IOpenGarrisonClientHudHooks>(hook => hook.OnGameplayHudDraw(canvas));

    public void NotifyLocalDamage(LocalDamageEvent e) => Dispatch<IOpenGarrisonClientDamageHooks>(hook => hook.OnLocalDamage(e));

    public IReadOnlyList<ClientPluginOptionsSection> GetOptionsSections()
    {
        var sections = new List<ClientPluginOptionsSection>();
        foreach (var plugin in GetPluginOptionsEntries())
        {
            sections.AddRange(plugin.Sections);
        }

        return sections;
    }

    public IReadOnlyList<ClientPluginOptionsEntry> GetPluginOptionsEntries()
    {
        var entries = new List<ClientPluginOptionsEntry>();
        foreach (var loadedPlugin in _loadedPlugins)
        {
            if (loadedPlugin.Plugin is not IOpenGarrisonClientOptionsHooks hook)
            {
                continue;
            }

            try
            {
                var pluginSections = hook.GetOptionsSections();
                if (pluginSections.Count == 0)
                {
                    continue;
                }

                var sections = new List<ClientPluginOptionsSection>(pluginSections.Count);
                for (var index = 0; index < pluginSections.Count; index += 1)
                {
                    var section = pluginSections[index];
                    sections.Add(string.IsNullOrWhiteSpace(section.Title)
                        ? section with { Title = loadedPlugin.Plugin.DisplayName }
                        : section);
                }

                entries.Add(new ClientPluginOptionsEntry(
                    loadedPlugin.Plugin.Id,
                    loadedPlugin.Plugin.DisplayName,
                    sections));
            }
            catch (Exception ex)
            {
                _log($"[plugin] options query failed for {loadedPlugin.Plugin.Id}: {ex.Message}");
            }
        }

        return entries;
    }

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

    private IOpenGarrisonClientPluginContext CreateContext(IOpenGarrisonClientPlugin plugin, string pluginDirectory)
    {
        var configDirectory = Path.Combine(_pluginConfigRoot, plugin.Id);
        Directory.CreateDirectory(pluginDirectory);
        Directory.CreateDirectory(configDirectory);
        return new ClientPluginContext(
            plugin.Id,
            pluginDirectory,
            configDirectory,
            _clientState,
            _log);
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

internal sealed record ClientPluginOptionsEntry(
    string PluginId,
    string DisplayName,
    IReadOnlyList<ClientPluginOptionsSection> Sections);
