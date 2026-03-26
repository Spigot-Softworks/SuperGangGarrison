using System.Reflection;
using System.Runtime.Loader;
using OpenGarrison.Client.Plugins;

namespace OpenGarrison.Client;

internal static class ClientPluginLoader
{
    public static IReadOnlyList<LoadedPlugin> LoadFromDirectory(
        string pluginsDirectory,
        Func<IOpenGarrisonClientPlugin, string, IOpenGarrisonClientPluginContext> contextFactory,
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

        return LoadFromAssemblies(assemblies, contextFactory, log);
    }

    public static IReadOnlyList<LoadedPlugin> LoadFromAssemblies(
        IEnumerable<Assembly> assemblies,
        Func<IOpenGarrisonClientPlugin, string, IOpenGarrisonClientPluginContext> contextFactory,
        Action<string> log)
    {
        var loadedPlugins = new List<LoadedPlugin>();
        foreach (var assembly in assemblies)
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

            foreach (var type in types
                         .Where(type => typeof(IOpenGarrisonClientPlugin).IsAssignableFrom(type)
                             && type is { IsAbstract: false, IsInterface: false }))
            {
                try
                {
                    if (Activator.CreateInstance(type) is not IOpenGarrisonClientPlugin plugin)
                    {
                        continue;
                    }

                    var pluginDirectory = Path.GetDirectoryName(assembly.Location) ?? string.Empty;
                    var context = contextFactory(plugin, pluginDirectory);
                    plugin.Initialize(context);
                    loadedPlugins.Add(new LoadedPlugin(plugin, context, pluginDirectory));
                }
                catch (Exception ex)
                {
                    log($"[plugin] failed to initialize \"{type.FullName}\": {ex.Message}");
                }
            }
        }

        return loadedPlugins;
    }

    internal sealed record LoadedPlugin(
        IOpenGarrisonClientPlugin Plugin,
        IOpenGarrisonClientPluginContext Context,
        string PluginDirectory);
}
