using System.Reflection;
using System.Runtime.Loader;
using OpenGarrison.Server.Plugins;

namespace OpenGarrison.Server;

internal static class PluginLoader
{
    public static IReadOnlyList<LoadedPlugin> LoadFromSearchDirectories(
        IEnumerable<PluginSearchDirectory> searchDirectories,
        Func<IOpenGarrisonServerPlugin, string, IOpenGarrisonServerPluginContext> contextFactory,
        Action<string> log)
    {
        var loadedAssemblies = new List<LoadedAssembly>();
        var seenAssemblyPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var searchDirectory in searchDirectories)
        {
            Directory.CreateDirectory(searchDirectory.DirectoryPath);
            foreach (var pluginPath in Directory.EnumerateFiles(searchDirectory.DirectoryPath, "*.dll", searchDirectory.SearchOption)
                         .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                var fullPluginPath = Path.GetFullPath(pluginPath);
                if (!seenAssemblyPaths.Add(fullPluginPath))
                {
                    continue;
                }

                try
                {
                    loadedAssemblies.Add(new LoadedAssembly(
                        AssemblyLoadContext.Default.LoadFromAssemblyPath(fullPluginPath),
                        Path.GetDirectoryName(fullPluginPath) ?? string.Empty));
                }
                catch (Exception ex)
                {
                    log($"[plugin] failed to load assembly \"{pluginPath}\": {ex.Message}");
                }
            }
        }

        return LoadFromLoadedAssemblies(loadedAssemblies, contextFactory, log);
    }

    public static IReadOnlyList<LoadedPlugin> LoadFromAssemblies(
        IEnumerable<Assembly> assemblies,
        Func<IOpenGarrisonServerPlugin, string, IOpenGarrisonServerPluginContext> contextFactory,
        Action<string> log)
    {
        var loadedAssemblies = assemblies.Select(assembly =>
            new LoadedAssembly(assembly, Path.GetDirectoryName(assembly.Location) ?? string.Empty));
        return LoadFromLoadedAssemblies(loadedAssemblies, contextFactory, log);
    }

    private static List<LoadedPlugin> LoadFromLoadedAssemblies(
        IEnumerable<LoadedAssembly> loadedAssemblies,
        Func<IOpenGarrisonServerPlugin, string, IOpenGarrisonServerPluginContext> contextFactory,
        Action<string> log)
    {
        var loadedPlugins = new List<LoadedPlugin>();
        var loadedPluginIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var loadedAssembly in loadedAssemblies)
        {
            var assembly = loadedAssembly.Assembly;
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(type => type is not null).Cast<Type>().ToArray();
            }

            foreach (var type in types
                         .Where(type => typeof(IOpenGarrisonServerPlugin).IsAssignableFrom(type)
                             && type is { IsAbstract: false, IsInterface: false }))
            {
                try
                {
                    if (Activator.CreateInstance(type) is not IOpenGarrisonServerPlugin plugin)
                    {
                        continue;
                    }

                    if (!loadedPluginIds.Add(plugin.Id))
                    {
                        log($"[plugin] skipped duplicate plugin id \"{plugin.Id}\" from \"{assembly.FullName}\"");
                        continue;
                    }

                    var context = contextFactory(plugin, loadedAssembly.PluginDirectory);
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

    internal sealed record PluginSearchDirectory(string DirectoryPath, SearchOption SearchOption);

    private sealed record LoadedAssembly(Assembly Assembly, string PluginDirectory);

    internal sealed record LoadedPlugin(
        IOpenGarrisonServerPlugin Plugin,
        IOpenGarrisonServerPluginContext Context,
        string PluginDirectory);
}
