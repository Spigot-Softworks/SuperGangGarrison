using System.Reflection;
using System.Runtime.Loader;
using OpenGarrison.PluginHost;
using OpenGarrison.Server.Plugins;

namespace OpenGarrison.Server;

internal static class PluginLoader
{
    public static IReadOnlyList<LoadedPlugin> LoadFromSearchDirectories(
        IEnumerable<PluginSearchDirectory> searchDirectories,
        Func<IOpenGarrisonServerPlugin, OpenGarrisonPluginManifest, string, IOpenGarrisonServerPluginContext> contextFactory,
        Action<string> log)
    {
        var loadedAssemblies = new List<LoadedAssembly>();
        var seenAssemblyPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var searchDirectory in searchDirectories)
        {
            Directory.CreateDirectory(searchDirectory.DirectoryPath);
            foreach (var candidate in EnumerateAssemblyCandidates(searchDirectory, log))
            {
                if (!seenAssemblyPaths.Add(candidate.AssemblyPath))
                {
                    continue;
                }

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
        }

        var loadedPlugins = LoadFromLoadedAssemblies(loadedAssemblies, contextFactory, log);
        var loadedPluginIds = new HashSet<string>(loadedPlugins.Select(entry => entry.Plugin.Id), StringComparer.OrdinalIgnoreCase);
        foreach (var luaCandidate in EnumerateLuaPluginCandidates(searchDirectories, log))
        {
            try
            {
                if (!loadedPluginIds.Add(luaCandidate.Manifest.Id))
                {
                    log($"[plugin] skipped duplicate plugin id \"{luaCandidate.Manifest.Id}\" from Lua manifest \"{luaCandidate.ManifestPath}\"");
                    continue;
                }

                var plugin = new LuaServerPlugin(luaCandidate.Manifest, luaCandidate.PluginDirectory);
                var context = contextFactory(plugin, luaCandidate.Manifest, luaCandidate.PluginDirectory);
                plugin.Initialize(context);
                loadedPlugins.Add(new LoadedPlugin(plugin, context, luaCandidate.PluginDirectory));
            }
            catch (Exception ex)
            {
                log($"[plugin] failed to initialize Lua plugin \"{luaCandidate.Manifest.Id}\": {ex.Message}");
            }
        }

        return loadedPlugins;
    }

    public static IReadOnlyList<LoadedPlugin> LoadFromAssemblies(
        IEnumerable<Assembly> assemblies,
        Func<IOpenGarrisonServerPlugin, OpenGarrisonPluginManifest, string, IOpenGarrisonServerPluginContext> contextFactory,
        Action<string> log)
    {
        var loadedAssemblies = assemblies.Select(assembly =>
            new LoadedAssembly(
                assembly,
                Path.GetDirectoryName(assembly.Location) ?? string.Empty,
                Manifest: null));
        return LoadFromLoadedAssemblies(loadedAssemblies, contextFactory, log);
    }

    private static List<LoadedPlugin> LoadFromLoadedAssemblies(
        IEnumerable<LoadedAssembly> loadedAssemblies,
        Func<IOpenGarrisonServerPlugin, OpenGarrisonPluginManifest, string, IOpenGarrisonServerPluginContext> contextFactory,
        Action<string> log)
    {
        var loadedPlugins = new List<LoadedPlugin>();
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

                    var context = contextFactory(plugin, manifest, loadedAssembly.PluginDirectory);
                    plugin.Initialize(context);
                    loadedPlugins.Add(new LoadedPlugin(plugin, context, loadedAssembly.PluginDirectory));
                }
                catch (Exception ex)
                {
                    log($"[plugin] failed to initialize \"{type.FullName}\": {ex.Message}");
                }
            }
        }

        return loadedPlugins;
    }

    private static IEnumerable<AssemblyCandidate> EnumerateAssemblyCandidates(PluginSearchDirectory searchDirectory, Action<string> log)
    {
        var coveredDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var manifestPath in Directory.EnumerateFiles(searchDirectory.DirectoryPath, OpenGarrisonPluginManifestLoader.DefaultManifestFileName, searchDirectory.SearchOption)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var pluginDirectory = Path.GetDirectoryName(manifestPath) ?? string.Empty;
            coveredDirectories.Add(Path.GetFullPath(pluginDirectory));

            if (!OpenGarrisonPluginManifestLoader.TryLoadFromPath(manifestPath, out var manifest, out var error))
            {
                log($"[plugin] failed to read manifest \"{manifestPath}\": {error}");
                continue;
            }

            if (manifest.Type != OpenGarrisonPluginType.Server)
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

        foreach (var pluginPath in Directory.EnumerateFiles(searchDirectory.DirectoryPath, "*.dll", searchDirectory.SearchOption)
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

                if (manifest.Type != OpenGarrisonPluginType.Server || manifest.Runtime != OpenGarrisonPluginRuntimeKind.Lua)
                {
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

        return true;
    }

    private static bool IsCoveredByManifest(string pluginDirectory, HashSet<string> coveredDirectories)
    {
        var fullPluginDirectory = Path.GetFullPath(pluginDirectory);
        return coveredDirectories.Any(coveredDirectory =>
            string.Equals(coveredDirectory, fullPluginDirectory, StringComparison.OrdinalIgnoreCase)
            || fullPluginDirectory.StartsWith(coveredDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
    }

    internal sealed record PluginSearchDirectory(string DirectoryPath, SearchOption SearchOption);

    internal sealed record LoadedPlugin(
        IOpenGarrisonServerPlugin Plugin,
        IOpenGarrisonServerPluginContext Context,
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
