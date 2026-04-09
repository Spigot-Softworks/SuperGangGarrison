#nullable enable

using System.IO;

namespace OpenGarrison.Client;

internal static class PackagedClientPluginBootstrapper
{
    private static readonly string PackagedClientPluginsRelativePath = Path.Combine("Plugins", "Packaged", "Client");

    public static bool TryPrepareRuntimePlugins(string runtimePluginsDestination, out string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimePluginsDestination);

        error = string.Empty;
        var packagedPluginsSource = FindPackagedClientPluginsSource();
        if (string.IsNullOrWhiteSpace(packagedPluginsSource) || !Directory.Exists(packagedPluginsSource))
        {
            return true;
        }

        return TryMirrorPackagedPlugins(packagedPluginsSource, runtimePluginsDestination, out error);
    }

    internal static bool TryMirrorPackagedPlugins(
        string packagedPluginsSource,
        string runtimePluginsDestination,
        out string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagedPluginsSource);
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimePluginsDestination);

        error = string.Empty;
        try
        {
            Directory.CreateDirectory(runtimePluginsDestination);
            foreach (var pluginDirectory in Directory.GetDirectories(packagedPluginsSource))
            {
                var pluginFolderName = Path.GetFileName(pluginDirectory);
                if (string.IsNullOrWhiteSpace(pluginFolderName))
                {
                    continue;
                }

                var pluginDestination = Path.Combine(runtimePluginsDestination, pluginFolderName);
                if (Directory.Exists(pluginDestination))
                {
                    Directory.Delete(pluginDestination, recursive: true);
                }

                CopyDirectory(pluginDirectory, pluginDestination);
            }

            return true;
        }
        catch (Exception ex)
        {
            error = $"Failed to mirror packaged client plugins: {ex.Message}";
            return false;
        }
    }

    private static string? FindPackagedClientPluginsSource()
    {
        foreach (var root in EnumerateProbeRoots())
        {
            var candidate = Path.Combine(root, PackagedClientPluginsRelativePath);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateProbeRoots()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var probe in EnumerateDirectLaunchDirectories())
        {
            var directory = new DirectoryInfo(probe);
            while (directory is not null)
            {
                if (seen.Add(directory.FullName))
                {
                    yield return directory.FullName;
                }

                directory = directory.Parent;
            }
        }
    }

    private static IEnumerable<string> EnumerateDirectLaunchDirectories()
    {
        yield return AppContext.BaseDirectory;
        yield return Directory.GetCurrentDirectory();
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (var filePath in Directory.GetFiles(sourceDirectory))
        {
            var destinationPath = Path.Combine(destinationDirectory, Path.GetFileName(filePath));
            File.Copy(filePath, destinationPath, overwrite: true);
        }

        foreach (var directoryPath in Directory.GetDirectories(sourceDirectory))
        {
            var destinationPath = Path.Combine(destinationDirectory, Path.GetFileName(directoryPath));
            CopyDirectory(directoryPath, destinationPath);
        }
    }
}
