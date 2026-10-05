using System.Reflection;
using OpenGarrison.PluginHost;
using OpenGarrison.Server.Plugins;

namespace OpenGarrison.Server;

internal static class PluginLoader
{
    /// <summary>
    /// Loads the Lua server plugins found under the search directories. Lua is the
    /// only plugin runtime: a manifest that declares another runtime is skipped with
    /// a log line, and plugin assemblies on disk are never loaded.
    /// </summary>
    public static IReadOnlyList<LoadedPlugin> LoadFromSearchDirectories(
        IEnumerable<PluginSearchDirectory> searchDirectories,
        Func<IOpenGarrisonServerPlugin, OpenGarrisonPluginManifest, string, IOpenGarrisonServerPluginContext> contextFactory,
        Action<string> log,
        Action<string>? initializationFailed = null)
    {
        var pluginCandidates = new List<PluginLoadCandidate>();
        var candidatePluginIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var luaCandidate in EnumerateLuaPluginCandidates(searchDirectories, log))
        {
            if (!candidatePluginIds.Add(luaCandidate.Manifest.Id))
            {
                log($"[plugin] skipped duplicate plugin id \"{luaCandidate.Manifest.Id}\" from Lua manifest \"{luaCandidate.ManifestPath}\"");
                continue;
            }

            pluginCandidates.Add(new PluginLoadCandidate(
                luaCandidate.Manifest,
                luaCandidate.PluginDirectory,
                luaCandidate.ManifestPath,
                () => new LuaServerPlugin(luaCandidate.Manifest, luaCandidate.PluginDirectory)));
        }

        return LoadPlannedCandidates(pluginCandidates, contextFactory, log, initializationFailed);
    }

    /// <summary>
    /// Loads server plugins compiled into assemblies that are already loaded. Only the
    /// host and its tests use this; it never reads plugin assemblies from disk.
    /// </summary>
    public static IReadOnlyList<LoadedPlugin> LoadFromAssemblies(
        IEnumerable<Assembly> assemblies,
        Func<IOpenGarrisonServerPlugin, OpenGarrisonPluginManifest, string, IOpenGarrisonServerPluginContext> contextFactory,
        Action<string> log,
        Action<string>? initializationFailed = null)
    {
        var loadedAssemblies = assemblies.Select(assembly =>
            new LoadedAssembly(
                assembly,
                Path.GetDirectoryName(assembly.Location) ?? string.Empty,
                Manifest: null));
        var pluginCandidates = CreatePluginLoadCandidatesFromLoadedAssemblies(loadedAssemblies, log);
        return LoadPlannedCandidates(pluginCandidates, contextFactory, log, initializationFailed);
    }

    private static List<PluginLoadCandidate> CreatePluginLoadCandidatesFromLoadedAssemblies(
        IEnumerable<LoadedAssembly> loadedAssemblies,
        Action<string> log)
    {
        var pluginCandidates = new List<PluginLoadCandidate>();
        var loadedPluginIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var loadedAssembly in loadedAssemblies)
        {
            foreach (var type in GetPluginTypes(loadedAssembly.Assembly))
            {
                try
                {
                    if (Activator.CreateInstance(type) is not IOpenGarrisonServerPlugin plugin)
                    {
                        continue;
                    }

                    var manifest = loadedAssembly.Manifest ?? OpenGarrisonPluginManifest.CreateClr(
                        plugin.Id,
                        plugin.DisplayName,
                        plugin.Version,
                        OpenGarrisonPluginType.Server,
                        Path.GetFileName(loadedAssembly.Assembly.Location),
                        type.FullName);

                    if (!ValidateManifestAgainstPlugin(manifest, plugin.Id, plugin.DisplayName, plugin.Version, type.FullName, log))
                    {
                        continue;
                    }

                    if (!loadedPluginIds.Add(plugin.Id))
                    {
                        log($"[plugin] skipped duplicate plugin id \"{plugin.Id}\" from \"{loadedAssembly.Assembly.FullName}\"");
                        continue;
                    }

                    pluginCandidates.Add(new PluginLoadCandidate(
                        manifest,
                        loadedAssembly.PluginDirectory,
                        loadedAssembly.Assembly.FullName ?? loadedAssembly.Assembly.Location,
                        () => plugin));
                }
                catch (Exception ex)
                {
                    log($"[plugin] failed to inspect \"{type.FullName}\": {ex.Message}");
                }
            }
        }

        return pluginCandidates;
    }

    private static List<LoadedPlugin> LoadPlannedCandidates(
        IEnumerable<PluginLoadCandidate> pluginCandidates,
        Func<IOpenGarrisonServerPlugin, OpenGarrisonPluginManifest, string, IOpenGarrisonServerPluginContext> contextFactory,
        Action<string> log,
        Action<string>? initializationFailed)
    {
        var plannedCandidates = OpenGarrisonPluginManifestPlanner.PlanLoadOrder(
            pluginCandidates,
            static candidate => candidate.Manifest);
        foreach (var warning in plannedCandidates.Warnings)
        {
            log(warning);
        }

        var loadedPlugins = new List<LoadedPlugin>();
        foreach (var candidate in plannedCandidates.Plugins)
        {
            try
            {
                var plugin = candidate.CreatePlugin();
                var context = contextFactory(plugin, candidate.Manifest, candidate.PluginDirectory);
                plugin.Initialize(context);
                loadedPlugins.Add(new LoadedPlugin(plugin, context, candidate.PluginDirectory));
            }
            catch (Exception ex)
            {
                initializationFailed?.Invoke(candidate.Manifest.Id);
                log($"[plugin] failed to initialize \"{candidate.Manifest.Id}\" from \"{candidate.SourceDescription}\": {ex.Message}");
            }
        }

        return loadedPlugins;
    }

    private static IEnumerable<LuaPluginCandidate> EnumerateLuaPluginCandidates(
        IEnumerable<PluginSearchDirectory> searchDirectories,
        Action<string> log)
    {
        var seenManifestPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var searchDirectory in searchDirectories)
        {
            Directory.CreateDirectory(searchDirectory.DirectoryPath);
            foreach (var manifestPath in Directory.EnumerateFiles(searchDirectory.DirectoryPath, OpenGarrisonPluginManifestLoader.DefaultManifestFileName, searchDirectory.SearchOption)
                         .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                var fullManifestPath = Path.GetFullPath(manifestPath);
                if (!seenManifestPaths.Add(fullManifestPath))
                {
                    continue;
                }

                var pluginDirectory = Path.GetDirectoryName(fullManifestPath) ?? string.Empty;
                if (!OpenGarrisonPluginManifestLoader.TryLoadFromPath(fullManifestPath, out var manifest, out var error))
                {
                    log($"[plugin] failed to read manifest \"{fullManifestPath}\": {error}");
                    continue;
                }

                if (manifest.Type != OpenGarrisonPluginType.Server)
                {
                    continue;
                }

                if (manifest.Runtime != OpenGarrisonPluginRuntimeKind.Lua)
                {
                    log($"[plugin] skipped \"{fullManifestPath}\": only Lua plugins are supported (manifest runtime is {manifest.Runtime}).");
                    continue;
                }

                if (!OpenGarrisonPluginManifestLoader.TryValidateHostApiCompatibility(manifest, OpenGarrisonPluginHostApi.CreateServerDefault(), out error))
                {
                    log($"[plugin] incompatible Lua manifest \"{fullManifestPath}\": {error}");
                    continue;
                }

                if (!OpenGarrisonPluginManifestLoader.TryResolveEntryPointPath(manifest, pluginDirectory, out var entryPointPath, out error))
                {
                    log($"[plugin] invalid Lua manifest \"{fullManifestPath}\": {error}");
                    continue;
                }

                if (!File.Exists(entryPointPath))
                {
                    log($"[plugin] Lua manifest entry point \"{entryPointPath}\" was not found.");
                    continue;
                }

                yield return new LuaPluginCandidate(fullManifestPath, pluginDirectory, manifest);
            }
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

        return types.Where(type => typeof(IOpenGarrisonServerPlugin).IsAssignableFrom(type)
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

        if (manifest.Type != OpenGarrisonPluginType.Server)
        {
            log($"[plugin] manifest for \"{pluginTypeName}\" declared incompatible type {manifest.Type}.");
            return false;
        }

        if (!OpenGarrisonPluginManifestLoader.TryValidateHostApiCompatibility(manifest, OpenGarrisonPluginHostApi.CreateServerDefault(), out var compatibilityError))
        {
            log($"[plugin] manifest for \"{pluginTypeName}\" declared incompatible host API: {compatibilityError}");
            return false;
        }

        return true;
    }

    internal sealed record PluginSearchDirectory(string DirectoryPath, SearchOption SearchOption);

    internal sealed record LoadedPlugin(
        IOpenGarrisonServerPlugin Plugin,
        IOpenGarrisonServerPluginContext Context,
        string PluginDirectory);

    private sealed record LoadedAssembly(
        Assembly Assembly,
        string PluginDirectory,
        OpenGarrisonPluginManifest? Manifest);

    private sealed record PluginLoadCandidate(
        OpenGarrisonPluginManifest Manifest,
        string PluginDirectory,
        string SourceDescription,
        Func<IOpenGarrisonServerPlugin> CreatePlugin);

    private sealed record LuaPluginCandidate(
        string ManifestPath,
        string PluginDirectory,
        OpenGarrisonPluginManifest Manifest);
}
