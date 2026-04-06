using System.Reflection;
using System.Runtime.Loader;
using OpenGarrison.Client.Plugins;
using OpenGarrison.PluginHost;

namespace OpenGarrison.Client;

internal static class ClientPluginLoader
{
    public static IReadOnlyList<DiscoveredPlugin> DiscoverFromDirectory(
        string pluginsDirectory,
        Action<string> log)
    {
        Directory.CreateDirectory(pluginsDirectory);
        var loadedAssemblies = new List<LoadedAssembly>();
        foreach (var candidate in EnumerateAssemblyCandidates(pluginsDirectory, log))
        {
            try
            {
                loadedAssemblies.Add(new LoadedAssembly(
                    AssemblyLoadContext.Default.LoadFromAssemblyPath(candidate.AssemblyPath),
                    candidate.PluginDirectory,
                    candidate.Manifest));
            }
            catch (Exception ex)
            {
                log($"[plugin] failed to load assembly \"{candidate.AssemblyPath}\": {ex.Message}");
            }
        }

        var discoveredPlugins = DiscoverFromLoadedAssemblies(loadedAssemblies, log).ToList();
        var discoveredPluginIds = new HashSet<string>(discoveredPlugins.Select(plugin => plugin.PluginId), StringComparer.OrdinalIgnoreCase);
        foreach (var luaCandidate in EnumerateLuaPluginCandidates(pluginsDirectory, log))
        {
            if (!discoveredPluginIds.Add(luaCandidate.Manifest.Id))
            {
                log($"[plugin] duplicate client plugin id \"{luaCandidate.Manifest.Id}\" from Lua manifest \"{luaCandidate.ManifestPath}\" ignored.");
                continue;
            }

            discoveredPlugins.Add(new DiscoveredPlugin(
                luaCandidate.Manifest.Id,
                luaCandidate.Manifest.DisplayName,
                Version.TryParse(luaCandidate.Manifest.Version, out var version) ? version : new Version(1, 0, 0, 0),
                typeof(LuaClientPlugin),
                luaCandidate.PluginDirectory,
                luaCandidate.Manifest));
        }

        return discoveredPlugins
            .OrderBy(plugin => plugin.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(plugin => plugin.PluginId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static IReadOnlyList<DiscoveredPlugin> DiscoverFromAssemblies(
        IEnumerable<Assembly> assemblies,
        Action<string> log)
    {
        var loadedAssemblies = assemblies.Select(assembly =>
            new LoadedAssembly(
                assembly,
                Path.GetDirectoryName(assembly.Location) ?? string.Empty,
                Manifest: null));
        return DiscoverFromLoadedAssemblies(loadedAssemblies, log);
    }

    public static LoadedPlugin? TryLoadDiscoveredPlugin(
        DiscoveredPlugin discoveredPlugin,
        Func<IOpenGarrisonClientPlugin, OpenGarrisonPluginManifest, string, IOpenGarrisonClientPluginContext> contextFactory,
        Action<string> log)
    {
        try
        {
            var pluginInstance = discoveredPlugin.Manifest.Runtime == OpenGarrisonPluginRuntimeKind.Lua
                ? new LuaClientPlugin(discoveredPlugin.Manifest, discoveredPlugin.PluginDirectory)
                : Activator.CreateInstance(discoveredPlugin.PluginType);
            if (pluginInstance is not IOpenGarrisonClientPlugin plugin)
            {
                return null;
            }

            if (!ValidateManifestAgainstPlugin(discoveredPlugin.Manifest, plugin.Id, plugin.DisplayName, plugin.Version, discoveredPlugin.PluginType.FullName, log))
            {
                return null;
            }

            var context = contextFactory(plugin, discoveredPlugin.Manifest, discoveredPlugin.PluginDirectory);
            plugin.Initialize(context);
            return new LoadedPlugin(plugin, context, discoveredPlugin.PluginDirectory);
        }
        catch (Exception ex)
        {
            log($"[plugin] failed to initialize \"{discoveredPlugin.PluginType.FullName}\": {ex.Message}");
            return null;
        }
    }

    private static DiscoveredPlugin[] DiscoverFromLoadedAssemblies(
        IEnumerable<LoadedAssembly> loadedAssemblies,
        Action<string> log)
    {
        var discoveredPlugins = new List<DiscoveredPlugin>();
        var discoveredPluginIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var loadedAssembly in loadedAssemblies)
        {
            foreach (var type in GetPluginTypes(loadedAssembly.Assembly))
            {
                try
                {
                    if (Activator.CreateInstance(type) is not IOpenGarrisonClientPlugin plugin)
                    {
                        continue;
                    }

                    var manifest = loadedAssembly.Manifest ?? OpenGarrisonPluginManifest.CreateClr(
                        plugin.Id,
                        plugin.DisplayName,
                        plugin.Version,
                        OpenGarrisonPluginType.Client,
                        Path.GetFileName(loadedAssembly.Assembly.Location),
                        type.FullName);

                    if (!ValidateManifestAgainstPlugin(manifest, plugin.Id, plugin.DisplayName, plugin.Version, type.FullName, log))
                    {
                        continue;
                    }

                    if (!discoveredPluginIds.Add(plugin.Id))
                    {
                        log($"[plugin] duplicate client plugin id \"{plugin.Id}\" from \"{type.FullName}\" ignored.");
                        continue;
                    }

                    discoveredPlugins.Add(new DiscoveredPlugin(
                        plugin.Id,
                        plugin.DisplayName,
                        plugin.Version,
                        type,
                        loadedAssembly.PluginDirectory,
                        manifest));
                }
                catch (Exception ex)
                {
                    log($"[plugin] failed to inspect \"{type.FullName}\": {ex.Message}");
                }
            }
        }

        return discoveredPlugins
            .OrderBy(plugin => plugin.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(plugin => plugin.PluginId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IEnumerable<AssemblyCandidate> EnumerateAssemblyCandidates(string pluginsDirectory, Action<string> log)
    {
        var coveredDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var manifestPath in Directory.EnumerateFiles(pluginsDirectory, OpenGarrisonPluginManifestLoader.DefaultManifestFileName, SearchOption.AllDirectories)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var pluginDirectory = Path.GetDirectoryName(manifestPath) ?? string.Empty;
            coveredDirectories.Add(Path.GetFullPath(pluginDirectory));

            if (!OpenGarrisonPluginManifestLoader.TryLoadFromPath(manifestPath, out var manifest, out var error))
            {
                log($"[plugin] failed to read manifest \"{manifestPath}\": {error}");
                continue;
            }

            if (manifest.Type != OpenGarrisonPluginType.Client)
            {
                log($"[plugin] skipped manifest \"{manifestPath}\" because it targets {manifest.Type} plugins.");
                continue;
            }

            if (manifest.Runtime != OpenGarrisonPluginRuntimeKind.Clr)
            {
                continue;
            }

            if (!OpenGarrisonPluginManifestLoader.TryResolveEntryPointPath(manifest, pluginDirectory, out var entryPointPath, out error))
            {
                log($"[plugin] invalid manifest \"{manifestPath}\": {error}");
                continue;
            }

            if (!File.Exists(entryPointPath))
            {
                log($"[plugin] manifest entry point \"{entryPointPath}\" was not found.");
                continue;
            }

            yield return new AssemblyCandidate(entryPointPath, pluginDirectory, manifest);
        }

        foreach (var pluginPath in Directory.EnumerateFiles(pluginsDirectory, "*.dll", SearchOption.AllDirectories)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var pluginDirectory = Path.GetDirectoryName(pluginPath) ?? string.Empty;
            if (IsCoveredByManifest(pluginDirectory, coveredDirectories))
            {
                continue;
            }

            yield return new AssemblyCandidate(Path.GetFullPath(pluginPath), pluginDirectory, Manifest: null);
        }
    }

    private static IEnumerable<LuaPluginCandidate> EnumerateLuaPluginCandidates(string pluginsDirectory, Action<string> log)
    {
        foreach (var manifestPath in Directory.EnumerateFiles(pluginsDirectory, OpenGarrisonPluginManifestLoader.DefaultManifestFileName, SearchOption.AllDirectories)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var pluginDirectory = Path.GetDirectoryName(manifestPath) ?? string.Empty;
            if (!OpenGarrisonPluginManifestLoader.TryLoadFromPath(manifestPath, out var manifest, out var error))
            {
                log($"[plugin] failed to read manifest \"{manifestPath}\": {error}");
                continue;
            }

            if (manifest.Type != OpenGarrisonPluginType.Client || manifest.Runtime != OpenGarrisonPluginRuntimeKind.Lua)
            {
                continue;
            }

            if (!OpenGarrisonPluginManifestLoader.TryResolveEntryPointPath(manifest, pluginDirectory, out var entryPointPath, out error))
            {
                log($"[plugin] invalid Lua manifest \"{manifestPath}\": {error}");
                continue;
            }

            if (!File.Exists(entryPointPath))
            {
                log($"[plugin] Lua manifest entry point \"{entryPointPath}\" was not found.");
                continue;
            }

            yield return new LuaPluginCandidate(Path.GetFullPath(manifestPath), pluginDirectory, manifest);
        }
    }

    private static IEnumerable<Type> GetPluginTypes(Assembly assembly)
    {
        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types.Where(type => type is not null).Cast<Type>().ToArray();
        }

        return types.Where(type => typeof(IOpenGarrisonClientPlugin).IsAssignableFrom(type)
            && type is { IsAbstract: false, IsInterface: false });
    }

    private static bool ValidateManifestAgainstPlugin(
        OpenGarrisonPluginManifest manifest,
        string pluginId,
        string displayName,
        Version version,
        string? pluginTypeName,
        Action<string> log)
    {
        if (!string.Equals(manifest.Id, pluginId, StringComparison.Ordinal))
        {
            log($"[plugin] manifest id \"{manifest.Id}\" did not match runtime id \"{pluginId}\" for \"{pluginTypeName}\".");
            return false;
        }

        if (!string.Equals(manifest.DisplayName, displayName, StringComparison.Ordinal))
        {
            log($"[plugin] manifest display name \"{manifest.DisplayName}\" did not match runtime display name \"{displayName}\" for \"{pluginTypeName}\".");
            return false;
        }

        if (!Version.TryParse(manifest.Version, out var manifestVersion) || manifestVersion != version)
        {
            log($"[plugin] manifest version \"{manifest.Version}\" did not match runtime version \"{version}\" for \"{pluginTypeName}\".");
            return false;
        }

        if (manifest.Type != OpenGarrisonPluginType.Client)
        {
            log($"[plugin] manifest for \"{pluginTypeName}\" declared incompatible type {manifest.Type}.");
            return false;
        }

        return true;
    }

    private static bool IsCoveredByManifest(string pluginDirectory, HashSet<string> coveredDirectories)
    {
        var fullPluginDirectory = Path.GetFullPath(pluginDirectory);
        return coveredDirectories.Any(coveredDirectory =>
            string.Equals(coveredDirectory, fullPluginDirectory, StringComparison.OrdinalIgnoreCase)
            || fullPluginDirectory.StartsWith(coveredDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
    }

    internal sealed record DiscoveredPlugin(
        string PluginId,
        string DisplayName,
        Version Version,
        Type PluginType,
        string PluginDirectory,
        OpenGarrisonPluginManifest Manifest);

    internal sealed record LoadedPlugin(
        IOpenGarrisonClientPlugin Plugin,
        IOpenGarrisonClientPluginContext Context,
        string PluginDirectory);

    private sealed record AssemblyCandidate(
        string AssemblyPath,
        string PluginDirectory,
        OpenGarrisonPluginManifest? Manifest);

    private sealed record LoadedAssembly(
        Assembly Assembly,
        string PluginDirectory,
        OpenGarrisonPluginManifest? Manifest);

    private sealed record LuaPluginCandidate(
        string ManifestPath,
        string PluginDirectory,
        OpenGarrisonPluginManifest Manifest);
}
