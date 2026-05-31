using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenGarrison.Core;

public static class RuntimePaths
{
    public static string ApplicationRoot => OperatingSystem.IsBrowser()
        ? "."
        : AppContext.BaseDirectory;

    public static string UserDataRoot
    {
        get
        {
            if (OperatingSystem.IsBrowser())
            {
                return ".";
            }

            var configuredRoot = Environment.GetEnvironmentVariable("OPENGARRISON_USER_DATA_ROOT");
            if (!string.IsNullOrWhiteSpace(configuredRoot)
                && TryCreateDirectory(configuredRoot, out var configuredPath))
            {
                return configuredPath;
            }

            var documentsPath = Path.Combine(GetDefaultUserDocumentsDirectory(), "OpenGarrison");
            if (TryCreateDirectory(documentsPath, out var userDataPath))
            {
                return userDataPath;
            }

            var localAppDataPath = Path.Combine(GetDefaultLocalApplicationDataDirectory(), "OpenGarrison");
            if (TryCreateDirectory(localAppDataPath, out userDataPath))
            {
                return userDataPath;
            }

            var applicationFallbackPath = Path.Combine(ApplicationRoot, "UserData");
            Directory.CreateDirectory(applicationFallbackPath);
            return applicationFallbackPath;
        }
    }

    public static string AssetsDirectory => Path.Combine(ApplicationRoot, "Assets");

    public static string ConfigDirectory
    {
        get
        {
            var path = Path.Combine(ApplicationRoot, "config");
            if (!OperatingSystem.IsBrowser())
            {
                Directory.CreateDirectory(path);
            }

            return path;
        }
    }

    public static string MapsDirectory
    {
        get
        {
            var configuredMapsDirectory = Environment.GetEnvironmentVariable("OPENGARRISON_MAPS_DIR");
            var path = !string.IsNullOrWhiteSpace(configuredMapsDirectory)
                ? configuredMapsDirectory
                : ApplicationMapsDirectory;
            if (!OperatingSystem.IsBrowser())
            {
                Directory.CreateDirectory(path);
            }

            return path;
        }
    }

    public static string ApplicationMapsDirectory => Path.Combine(ApplicationRoot, "Maps");

    public static string LegacyApplicationMapsDirectory => ApplicationMapsDirectory;

    public static string LegacyUserMapsDirectory => Path.Combine(GetDefaultUserDocumentsDirectory(), "OpenGarrison", "Maps");

    public static IReadOnlyList<string> MapSearchDirectories
    {
        get
        {
            if (OperatingSystem.IsBrowser())
            {
                return Array.Empty<string>();
            }

            var directories = new List<string>();
            AddUniqueDirectory(directories, MapsDirectory);
            AddUniqueDirectory(directories, ApplicationMapsDirectory);
            AddUniqueDirectory(directories, LegacyUserMapsDirectory);
            return directories;
        }
    }

    public static string LogsDirectory
    {
        get
        {
            var path = Path.Combine(ApplicationRoot, "logs");
            if (!OperatingSystem.IsBrowser())
            {
                Directory.CreateDirectory(path);
            }

            return path;
        }
    }

    public static string ReplaysDirectory
    {
        get
        {
            var path = Path.Combine(ConfigDirectory, "replays");
            if (!OperatingSystem.IsBrowser())
            {
                Directory.CreateDirectory(path);
            }

            return path;
        }
    }

    public static string GetConfigPath(string fileName)
    {
        return ResolvePathUnderRoot(ConfigDirectory, fileName);
    }

    public static string GetUserDataPath(string fileName)
    {
        return ResolvePathUnderRoot(UserDataRoot, fileName);
    }

    public static string GetLogPath(string fileName)
    {
        return ResolvePathUnderRoot(LogsDirectory, fileName);
    }

    private static string ResolvePathUnderRoot(string rootDirectory, string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        if (Path.IsPathRooted(relativePath))
        {
            throw new ArgumentException("Path must be relative to the runtime root.", nameof(relativePath));
        }

        var normalizedRoot = Path.GetFullPath(rootDirectory);
        var resolvedPath = Path.GetFullPath(Path.Combine(normalizedRoot, relativePath));
        if (!IsPathUnderRoot(normalizedRoot, resolvedPath))
        {
            throw new InvalidOperationException("Path escapes the runtime root directory.");
        }

        return resolvedPath;
    }

    private static bool IsPathUnderRoot(string rootDirectory, string candidatePath)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (string.Equals(rootDirectory, candidatePath, comparison))
        {
            return true;
        }

        return candidatePath.StartsWith(EnsureTrailingSeparator(rootDirectory), comparison);
    }

    private static string EnsureTrailingSeparator(string path)
    {
        if (path.EndsWith(Path.DirectorySeparatorChar)
            || path.EndsWith(Path.AltDirectorySeparatorChar))
        {
            return path;
        }

        return path + Path.DirectorySeparatorChar;
    }

    private static string GetDefaultUserDocumentsDirectory()
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return string.IsNullOrWhiteSpace(documents)
            ? ApplicationRoot
            : documents;
    }

    private static string GetDefaultLocalApplicationDataDirectory()
    {
        var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return string.IsNullOrWhiteSpace(localApplicationData)
            ? ApplicationRoot
            : localApplicationData;
    }

    private static bool TryCreateDirectory(string path, out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            fullPath = Path.GetFullPath(path);
            Directory.CreateDirectory(fullPath);
            return Directory.Exists(fullPath);
        }
        catch (ArgumentException)
        {
        }
        catch (IOException)
        {
        }
        catch (NotSupportedException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        fullPath = string.Empty;
        return false;
    }

    private static void AddUniqueDirectory(List<string> directories, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var fullPath = Path.GetFullPath(path);
        if (directories.Any(existing => string.Equals(
                Path.GetFullPath(existing),
                fullPath,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)))
        {
            return;
        }

        directories.Add(fullPath);
    }
}
