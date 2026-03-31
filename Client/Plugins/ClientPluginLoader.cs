using System.Reflection;
using System.Runtime.Loader;
using OpenGarrison.Client.Plugins;

namespace OpenGarrison.Client;

internal static class ClientPluginLoader
{
    public static IReadOnlyList<DiscoveredPlugin> DiscoverFromDirectory(
        string pluginsDirectory,
        Action<string> log)
    {
        Directory.CreateDirectory(pluginsDirectory);
        var assemblies = new List<Assembly>();
        foreach (var pluginPath in Directory.EnumerateFiles(pluginsDirectory, "*.dll", SearchOption.AllDirectories)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                assemblies.Add(AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(pluginPath)));
            }
            catch (Exception ex)
            {
                log($"[plugin] failed to load assembly \"{pluginPath}\": {ex.Message}");
            }
        }

        return DiscoverFromAssemblies(assemblies, log);
    }

    public static IReadOnlyList<DiscoveredPlugin> DiscoverFromAssemblies(
        IEnumerable<Assembly> assemblies,
        Action<string> log)
    {
        var discoveredPlugins = new List<DiscoveredPlugin>();
        var discoveredPluginIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var assembly in assemblies)
        {
            foreach (var type in GetPluginTypes(assembly))
            {
                try
                {
                    if (Activator.CreateInstance(type) is not IOpenGarrisonClientPlugin plugin)
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
                        Path.GetDirectoryName(assembly.Location) ?? string.Empty));
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

    public static LoadedPlugin? TryLoadDiscoveredPlugin(
        DiscoveredPlugin discoveredPlugin,
        Func<IOpenGarrisonClientPlugin, string, IOpenGarrisonClientPluginContext> contextFactory,
        Action<string> log)
    {
        try
        {
            if (Activator.CreateInstance(discoveredPlugin.PluginType) is not IOpenGarrisonClientPlugin plugin)
            {
                return null;
            }

            var context = contextFactory(plugin, discoveredPlugin.PluginDirectory);
            plugin.Initialize(context);
            return new LoadedPlugin(plugin, context, discoveredPlugin.PluginDirectory);
        }
        catch (Exception ex)
        {
            log($"[plugin] failed to initialize \"{discoveredPlugin.PluginType.FullName}\": {ex.Message}");
            return null;
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

    internal sealed record DiscoveredPlugin(
        string PluginId,
        string DisplayName,
        Version Version,
        Type PluginType,
        string PluginDirectory);

    internal sealed record LoadedPlugin(
        IOpenGarrisonClientPlugin Plugin,
        IOpenGarrisonClientPluginContext Context,
        string PluginDirectory);
}
