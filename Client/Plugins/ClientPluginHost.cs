using System.Globalization;
using System.IO;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Client.Plugins;
using OpenGarrison.Core;
using OpenGarrison.PluginHost;
using OpenGarrison.Protocol;

namespace OpenGarrison.Client;

internal sealed class ClientPluginHost
{
    private const string GameplayHudTraceFileName = "client-plugin-gameplay-hud-trace.log";
    private readonly IOpenGarrisonClientReadOnlyState _clientState;
    private readonly GraphicsDevice _graphicsDevice;
    private readonly Action<string> _log;
    private readonly string _pluginsDirectory;
    private readonly string _pluginConfigRoot;
    private readonly ClientPluginStateStore _stateStore;
    private readonly OpenGarrisonPluginHostApi _hostApi = OpenGarrisonPluginHostApi.CreateClientDefault();
    private readonly List<ClientPluginLoader.DiscoveredPlugin> _discoveredPlugins = new();
    private readonly List<LoadedPluginEntry> _loadedPlugins = new();
    private readonly Dictionary<string, List<RegisteredHotkey>> _registeredHotkeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<RegisteredMenuEntry>> _registeredMenuEntries = new(StringComparer.OrdinalIgnoreCase);
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

    public void NotifyGameplayHudDraw(IOpenGarrisonClientHudCanvas canvas)
    {
        var orderedEntries = _loadedPlugins
            .OrderBy(entry => entry.LoadedPlugin.Plugin is IOpenGarrisonClientHudOrderHooks orderedHook ? orderedHook.GameplayHudOrder : 0)
            .ThenBy(entry => entry.DiscoveredPlugin.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        WriteGameplayHudTrace($"begin count={orderedEntries.Length}");
        for (var index = 0; index < orderedEntries.Length; index += 1)
        {
            var entry = orderedEntries[index];
            WriteGameplayHudTrace(FormattableString.Invariant($"before index={index} plugin={entry.DiscoveredPlugin.PluginId}"));
            DispatchHook<IOpenGarrisonClientHudHooks>(entry, hook => hook.OnGameplayHudDraw(canvas), "runtime");
            WriteGameplayHudTrace(FormattableString.Invariant($"after index={index} plugin={entry.DiscoveredPlugin.PluginId}"));
        }

        WriteGameplayHudTrace("end");
    }

    public void NotifyLocalDamage(LocalDamageEvent e) => Dispatch<IOpenGarrisonClientDamageHooks>(hook => hook.OnLocalDamage(e));

    public void NotifyWorldSound(ClientWorldSoundEvent e) => Dispatch<IOpenGarrisonClientSoundHooks>(hook => hook.OnWorldSound(e));

    public void NotifyServerPluginMessage(ClientPluginMessageEnvelope e) => Dispatch<IOpenGarrisonClientPluginMessageHooks>(hook => hook.OnServerPluginMessage(e));

    public void NotifyShotFired(ClientShotFiredEvent e) => Dispatch<IOpenGarrisonClientSemanticGameplayHooks>(hook => hook.OnShotFired(e));

    public void NotifyHitConfirmed(ClientHitConfirmedEvent e) => Dispatch<IOpenGarrisonClientSemanticGameplayHooks>(hook => hook.OnHitConfirmed(e));

    public void NotifyLocalKill(ClientLocalKillEvent e) => Dispatch<IOpenGarrisonClientSemanticGameplayHooks>(hook => hook.OnLocalKill(e));

    public void NotifyLocalDeath(ClientLocalDeathEvent e) => Dispatch<IOpenGarrisonClientSemanticGameplayHooks>(hook => hook.OnLocalDeath(e));

    public void NotifyPickup(ClientPickupEvent e) => Dispatch<IOpenGarrisonClientSemanticGameplayHooks>(hook => hook.OnPickup(e));

    public void NotifyHeal(ClientHealEvent e) => Dispatch<IOpenGarrisonClientSemanticGameplayHooks>(hook => hook.OnHeal(e));

    public void NotifyIgnited(ClientIgniteEvent e) => Dispatch<IOpenGarrisonClientSemanticGameplayHooks>(hook => hook.OnIgnited(e));

    public void NotifyExtinguished(ClientExtinguishEvent e) => Dispatch<IOpenGarrisonClientSemanticGameplayHooks>(hook => hook.OnExtinguished(e));

    public void NotifyObjectiveStateChanged(ClientObjectiveStateEvent e) => Dispatch<IOpenGarrisonClientSemanticGameplayHooks>(hook => hook.OnObjectiveStateChanged(e));

    public void NotifyIntelStateChanged(ClientIntelStateEvent e) => Dispatch<IOpenGarrisonClientSemanticGameplayHooks>(hook => hook.OnIntelStateChanged(e));

    public void NotifyGeneratorStateChanged(ClientGeneratorStateEvent e) => Dispatch<IOpenGarrisonClientSemanticGameplayHooks>(hook => hook.OnGeneratorStateChanged(e));

    public void NotifyRoundPhaseChanged(ClientRoundPhaseChangedEvent e) => Dispatch<IOpenGarrisonClientSemanticGameplayHooks>(hook => hook.OnRoundPhaseChanged(e));

    public void NotifyKillFeed(ClientKillFeedEvent e) => Dispatch<IOpenGarrisonClientSemanticGameplayHooks>(hook => hook.OnKillFeed(e));

    public void NotifyScoreboardDraw(IOpenGarrisonClientScoreboardCanvas canvas, ClientScoreboardRenderState state)
    {
        var orderedEntries = _loadedPlugins
            .OrderBy(entry => GetScoreboardLocation(entry.LoadedPlugin.Plugin))
            .ThenBy(entry => GetScoreboardOrder(entry.LoadedPlugin.Plugin))
            .ThenBy(entry => entry.DiscoveredPlugin.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        for (var index = 0; index < orderedEntries.Length; index += 1)
        {
            var entry = orderedEntries[index];
            DispatchHook<IOpenGarrisonClientScoreboardHooks>(entry, hook => hook.OnScoreboardDraw(canvas, state), "runtime");
            DispatchHook<IOpenGarrisonClientScoreboardLegacyHooks>(entry, hook => hook.OnScoreboardDraw(canvas, state), "runtime");
        }
    }

    public IReadOnlyList<ClientPluginMenuEntry> GetMenuEntries(ClientPluginMenuLocation location)
    {
        var entries = new List<ClientPluginMenuEntry>();
        foreach (var pluginEntries in _registeredMenuEntries.Values)
        {
            for (var index = 0; index < pluginEntries.Count; index += 1)
            {
                var entry = pluginEntries[index];
                if (entry.Location != location)
                {
                    continue;
                }

                entries.Add(new ClientPluginMenuEntry(entry.PluginId, entry.MenuEntryId, entry.Label, entry.Activate, entry.Order));
            }
        }

        entries.Sort((left, right) =>
        {
            var orderComparison = left.Order.CompareTo(right.Order);
            return orderComparison != 0
                ? orderComparison
                : string.Compare(left.Label, right.Label, StringComparison.OrdinalIgnoreCase);
        });
        return entries;
    }

    public Vector2 GetCameraOffset()
    {
        var offset = Vector2.Zero;
        for (var index = 0; index < _loadedPlugins.Count; index += 1)
        {
            var entry = _loadedPlugins[index];
            if (entry.LoadedPlugin.Plugin is not IOpenGarrisonClientCameraHooks hook)
            {
                continue;
            }

            try
            {
                offset += hook.GetCameraOffset();
            }
            catch (Exception ex)
            {
                _log($"[plugin] camera hook failed for {entry.DiscoveredPlugin.PluginId}: {ex.Message}");
            }
        }

        return offset;
    }

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

            DisposePluginResources(_loadedPlugins[index]);
        }

        _registeredHotkeys.Clear();
        _registeredMenuEntries.Clear();
        _loadedPlugins.Clear();
    }

    private void LoadDiscoveredPlugins(IReadOnlyList<ClientPluginLoader.DiscoveredPlugin> discoveredPlugins)
    {
        ResetLoadedPluginsForDiscovery();
        _discoveredPlugins.Clear();
        _registeredHotkeys.Clear();
        _registeredMenuEntries.Clear();
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

            DisposePluginResources(_loadedPlugins[index]);
        }

        _loadedPlugins.Clear();
    }

    private List<ClientPluginOptionsSection> GetPluginOptionsSections(
        ClientPluginLoader.DiscoveredPlugin discoveredPlugin,
        LoadedPluginEntry? loadedPlugin)
    {
        if (loadedPlugin?.LoadedPlugin.Plugin is not IOpenGarrisonClientOptionsHooks hook)
        {
            return GetRegisteredHotkeySections(discoveredPlugin.PluginId);
        }

        try
        {
            var pluginSections = hook.GetOptionsSections();
            var sections = new List<ClientPluginOptionsSection>(pluginSections.Count + 1);
            for (var sectionIndex = 0; sectionIndex < pluginSections.Count; sectionIndex += 1)
            {
                var section = pluginSections[sectionIndex];
                sections.Add(string.IsNullOrWhiteSpace(section.Title)
                    ? section with { Title = discoveredPlugin.DisplayName }
                    : section);
            }

            sections.AddRange(GetRegisteredHotkeySections(discoveredPlugin.PluginId));
            return sections;
        }
        catch (Exception ex)
        {
            _log($"[plugin] options query failed for {discoveredPlugin.PluginId}: {ex.Message}");
            return GetRegisteredHotkeySections(discoveredPlugin.PluginId);
        }
    }

    private List<ClientPluginOptionsSection> GetRegisteredHotkeySections(string pluginId)
    {
        if (!_registeredHotkeys.TryGetValue(pluginId, out var hotkeys) || hotkeys.Count == 0)
        {
            return [];
        }

        var items = new List<ClientPluginOptionItem>(hotkeys.Count);
        for (var index = 0; index < hotkeys.Count; index += 1)
        {
            var hotkeyId = hotkeys[index].HotkeyId;
            var displayName = hotkeys[index].DisplayName;
            items.Add(new ClientPluginKeyOptionItem(
                displayName,
                () => GetRegisteredHotkey(pluginId, hotkeyId)?.CurrentKey ?? Keys.None,
                value => SetRegisteredHotkey(pluginId, hotkeyId, value)));
        }

        return
        [
            new ClientPluginOptionsSection("Hotkeys", items),
        ];
    }

    private RegisteredHotkey? GetRegisteredHotkey(string pluginId, string hotkeyId)
    {
        if (!_registeredHotkeys.TryGetValue(pluginId, out var hotkeys))
        {
            return null;
        }

        for (var index = 0; index < hotkeys.Count; index += 1)
        {
            if (string.Equals(hotkeys[index].HotkeyId, hotkeyId, StringComparison.OrdinalIgnoreCase))
            {
                return hotkeys[index];
            }
        }

        return null;
    }

    private void SetRegisteredHotkey(string pluginId, string hotkeyId, Keys value)
    {
        if (!_registeredHotkeys.TryGetValue(pluginId, out var hotkeys))
        {
            return;
        }

        for (var index = 0; index < hotkeys.Count; index += 1)
        {
            if (!string.Equals(hotkeys[index].HotkeyId, hotkeyId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            hotkeys[index] = hotkeys[index] with { CurrentKey = value };
            _stateStore.SetPluginHotkey(pluginId, hotkeyId, value);
            return;
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
                DispatchHook<IOpenGarrisonClientLifecycleHooks>(entry, hook => hook.OnClientStopped(), "stopped");
            }

            try
            {
                entry.LoadedPlugin.Plugin.Shutdown();
            }
            catch (Exception ex)
            {
                _log($"[plugin] shutdown failed for {entry.DiscoveredPlugin.PluginId}: {ex.Message}");
            }

            DisposePluginResources(entry);
            _registeredHotkeys.Remove(entry.DiscoveredPlugin.PluginId);
            _registeredMenuEntries.Remove(entry.DiscoveredPlugin.PluginId);
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

    private IOpenGarrisonClientPluginContext CreateContext(IOpenGarrisonClientPlugin plugin, OpenGarrisonPluginManifest manifest, string pluginDirectory)
    {
        var configDirectory = Path.Combine(_pluginConfigRoot, plugin.Id);
        Directory.CreateDirectory(pluginDirectory);
        Directory.CreateDirectory(configDirectory);
        var assetRegistry = new ClientPluginAssetRegistry(plugin.Id, pluginDirectory, _graphicsDevice);
        return new ClientPluginContext(
            plugin.Id,
            pluginDirectory,
            configDirectory,
            manifest,
            _hostApi,
            _graphicsDevice,
            _clientState,
            assetRegistry,
            (hotkeyId, displayName, defaultKey) => RegisterHotkey(plugin.Id, hotkeyId, displayName, defaultKey),
            hotkeyId => WasHotkeyPressed(plugin.Id, hotkeyId),
            (menuEntryId, label, location, activate, order) => RegisterMenuEntry(plugin.Id, menuEntryId, label, location, activate, order),
            (text, durationTicks, playSound) => ShowNotice(plugin.Id, text, durationTicks, playSound),
            (targetPluginId, messageType, payload, payloadFormat, schemaVersion) => SendMessageToServer(plugin.Id, targetPluginId, messageType, payload, payloadFormat, schemaVersion),
            _log);
    }

    private Keys RegisterHotkey(string pluginId, string hotkeyId, string displayName, Keys defaultKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentException.ThrowIfNullOrWhiteSpace(hotkeyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        if (!_registeredHotkeys.TryGetValue(pluginId, out var hotkeys))
        {
            hotkeys = new List<RegisteredHotkey>();
            _registeredHotkeys[pluginId] = hotkeys;
        }

        var resolvedKey = _stateStore.GetPluginHotkey(pluginId, hotkeyId, defaultKey);
        for (var index = 0; index < hotkeys.Count; index += 1)
        {
            if (!string.Equals(hotkeys[index].HotkeyId, hotkeyId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            hotkeys[index] = hotkeys[index] with
            {
                DisplayName = displayName,
                DefaultKey = defaultKey,
                CurrentKey = resolvedKey,
            };
            return resolvedKey;
        }

        hotkeys.Add(new RegisteredHotkey(hotkeyId, displayName, defaultKey, resolvedKey));
        hotkeys.Sort((left, right) => string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase));
        return resolvedKey;
    }

    private bool WasHotkeyPressed(string pluginId, string hotkeyId)
    {
        var registeredHotkey = GetRegisteredHotkey(pluginId, hotkeyId);
        return registeredHotkey is not null && _clientState.WasKeyPressedThisFrame(registeredHotkey.CurrentKey);
    }

    private void RegisterMenuEntry(string pluginId, string menuEntryId, string label, ClientPluginMenuLocation location, Action activate, int order)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentException.ThrowIfNullOrWhiteSpace(menuEntryId);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        if (!_registeredMenuEntries.TryGetValue(pluginId, out var menuEntries))
        {
            menuEntries = new List<RegisteredMenuEntry>();
            _registeredMenuEntries[pluginId] = menuEntries;
        }

        for (var index = 0; index < menuEntries.Count; index += 1)
        {
            if (!string.Equals(menuEntries[index].MenuEntryId, menuEntryId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            menuEntries[index] = menuEntries[index] with
            {
                Label = label,
                Location = location,
                Activate = activate,
                Order = order,
            };
            return;
        }

        menuEntries.Add(new RegisteredMenuEntry(pluginId, menuEntryId, label, location, activate, order));
    }

    private void ShowNotice(string pluginId, string text, int durationTicks, bool playSound)
    {
        if (_clientState is not ClientPluginUiSink sink)
        {
            _log($"[plugin] client plugin notice surface unavailable for {pluginId}.");
            return;
        }

        try
        {
            sink.EnqueuePluginNotice(text, durationTicks, playSound);
        }
        catch (Exception ex)
        {
            _log($"[plugin] notice enqueue failed for {pluginId}: {ex.Message}");
        }
    }

    private void SendMessageToServer(
        string sourcePluginId,
        string targetPluginId,
        string messageType,
        string payload,
        PluginMessagePayloadFormat payloadFormat,
        ushort schemaVersion)
    {
        if (_clientState is not ClientPluginMessageSink sink)
        {
            _log($"[plugin] client plugin messaging unavailable for {sourcePluginId}.");
            return;
        }

        try
        {
            if (!TryNormalizePluginMessage(targetPluginId, messageType, payload, out var normalizedTargetPluginId, out var normalizedMessageType, out var normalizedPayload))
            {
                return;
            }

            sink.SendPluginMessage(sourcePluginId, normalizedTargetPluginId, normalizedMessageType, normalizedPayload, payloadFormat, schemaVersion);
        }
        catch (Exception ex)
        {
            _log($"[plugin] message send failed for {sourcePluginId}: {ex.Message}");
        }
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

    private static void DisposePluginResources(LoadedPluginEntry entry)
    {
        if (entry.LoadedPlugin.Context is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private static ClientScoreboardPanelLocation GetScoreboardLocation(IOpenGarrisonClientPlugin plugin)
    {
        return plugin switch
        {
            IOpenGarrisonClientScoreboardHooks scoreboardHooks => scoreboardHooks.ScoreboardPanelLocation,
            _ => ClientScoreboardPanelLocation.Footer,
        };
    }

    private static int GetScoreboardOrder(IOpenGarrisonClientPlugin plugin)
    {
        return plugin switch
        {
            IOpenGarrisonClientScoreboardHooks scoreboardHooks => scoreboardHooks.ScoreboardPanelOrder,
            _ => 0,
        };
    }

    private static bool TryNormalizePluginMessage(
        string targetPluginId,
        string messageType,
        string? payload,
        out string normalizedTargetPluginId,
        out string normalizedMessageType,
        out string normalizedPayload)
    {
        normalizedTargetPluginId = targetPluginId?.Trim() ?? string.Empty;
        normalizedMessageType = messageType?.Trim() ?? string.Empty;
        normalizedPayload = payload ?? string.Empty;
        return normalizedTargetPluginId.Length > 0 && normalizedMessageType.Length > 0;
    }

    private static void WriteGameplayHudTrace(string message)
    {
        try
        {
            var timestamp = DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture);
            File.AppendAllText(RuntimePaths.GetLogPath(GameplayHudTraceFileName), $"[{timestamp}] {message}{Environment.NewLine}");
        }
        catch
        {
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

    private sealed record RegisteredHotkey(
        string HotkeyId,
        string DisplayName,
        Keys DefaultKey,
        Keys CurrentKey);

    private sealed record RegisteredMenuEntry(
        string PluginId,
        string MenuEntryId,
        string Label,
        ClientPluginMenuLocation Location,
        Action Activate,
        int Order);
}

internal interface ClientPluginMessageSink
{
    void SendPluginMessage(string sourcePluginId, string targetPluginId, string messageType, string payload, PluginMessagePayloadFormat payloadFormat, ushort schemaVersion);
}

internal interface ClientPluginUiSink
{
    void EnqueuePluginNotice(string text, int durationTicks, bool playSound);
}

internal sealed record ClientPluginMenuEntry(
    string PluginId,
    string MenuEntryId,
    string Label,
    Action Activate,
    int Order);

internal sealed record ClientPluginOptionsEntry(
    string PluginId,
    string DisplayName,
    Version Version,
    bool IsEnabled,
    bool IsLoaded,
    IReadOnlyList<ClientPluginOptionsSection> Sections);
