using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenGarrison.Core;

public static class RuntimePaths
{
    private static readonly StringComparison RuntimePathComparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    private static readonly StringComparer RuntimePathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private static readonly object MigrationLock = new();
    private static readonly HashSet<string> MigratedConfigDirectories = new(RuntimePathComparer);
    private static readonly HashSet<string> MigratedUserDataRoots = new(RuntimePathComparer);

    private static readonly string[] KnownUserDataMigrationRelativePaths =
    [
        "client-identity.json",
        "friends.json",
        "hostMapFavourites.txt",
        Path.Combine("CustomBubbles", "custom-bubbles.json"),
        Path.Combine("config", "OpenGarrison.hud.json")
    ];

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

            if (TryGetConfiguredUserDataRoot(out var configuredPath))
            {
                return configuredPath;
            }

            var localAppDataPath = Path.Combine(GetDefaultLocalApplicationDataDirectory(), "OpenGarrison");
            if (TryCreateDirectory(localAppDataPath, out var userDataPath))
            {
                MigrateKnownUserDataFilesIfNeeded(userDataPath);
                return userDataPath;
            }

            var documentsPath = Path.Combine(GetDefaultUserDocumentsDirectory(), "OpenGarrison");
            if (TryCreateDirectory(documentsPath, out userDataPath))
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
            var path = Path.Combine(UserDataRoot, "config");
            if (!OperatingSystem.IsBrowser())
            {
                Directory.CreateDirectory(path);
                MigrateLegacyConfigFilesIfNeeded(path);
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
        if (string.Equals(rootDirectory, candidatePath, RuntimePathComparison))
        {
            return true;
        }

        return candidatePath.StartsWith(EnsureTrailingSeparator(rootDirectory), RuntimePathComparison);
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

    private static bool TryGetConfiguredUserDataRoot(out string configuredPath)
    {
        configuredPath = string.Empty;
        var configuredRoot = Environment.GetEnvironmentVariable("OPENGARRISON_USER_DATA_ROOT");
        return !string.IsNullOrWhiteSpace(configuredRoot)
            && TryCreateDirectory(configuredRoot, out configuredPath);
    }

    private static bool HasConfiguredUserDataRoot()
    {
        return !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENGARRISON_USER_DATA_ROOT"));
    }

    private static void MigrateKnownUserDataFilesIfNeeded(string destinationRoot)
    {
        if (OperatingSystem.IsBrowser() || HasConfiguredUserDataRoot())
        {
            return;
        }

        var normalizedDestination = Path.GetFullPath(destinationRoot);
        lock (MigrationLock)
        {
            if (!MigratedUserDataRoots.Add(normalizedDestination))
            {
                return;
            }

            TryCopyKnownUserDataFiles(Path.Combine(GetDefaultUserDocumentsDirectory(), "OpenGarrison"), normalizedDestination);
            TryCopyKnownUserDataFiles(Path.Combine(ApplicationRoot, "UserData"), normalizedDestination);
        }
    }

    private static void TryCopyKnownUserDataFiles(string sourceRoot, string destinationRoot)
    {
        if (!Directory.Exists(sourceRoot) || SamePath(sourceRoot, destinationRoot))
        {
            return;
        }

        foreach (var relativePath in KnownUserDataMigrationRelativePaths)
        {
            TryCopyFileIfMissing(
                Path.Combine(sourceRoot, relativePath),
                Path.Combine(destinationRoot, relativePath));
        }
    }

    private static void MigrateLegacyConfigFilesIfNeeded(string destinationConfigDirectory)
    {
        if (OperatingSystem.IsBrowser() || HasConfiguredUserDataRoot())
        {
            return;
        }

        var normalizedDestination = Path.GetFullPath(destinationConfigDirectory);
        lock (MigrationLock)
        {
            if (!MigratedConfigDirectories.Add(normalizedDestination))
            {
                return;
            }

            TryCopyDirectoryContentsIfMissing(Path.Combine(ApplicationRoot, "config"), normalizedDestination);
        }
    }

    private static void TryCopyDirectoryContentsIfMissing(string sourceDirectory, string destinationDirectory)
    {
        if (!Directory.Exists(sourceDirectory) || SamePath(sourceDirectory, destinationDirectory))
        {
            return;
        }

        try
        {
            foreach (var sourcePath in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(sourceDirectory, sourcePath);
                if (Path.IsPathRooted(relativePath) || IsEscapingRelativePath(relativePath))
                {
                    continue;
                }

                TryCopyFileIfMissing(sourcePath, Path.Combine(destinationDirectory, relativePath));
            }
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
    }

    private static void TryCopyFileIfMissing(string sourcePath, string destinationPath)
    {
        if (!File.Exists(sourcePath) || SamePath(sourcePath, destinationPath) || File.Exists(destinationPath))
        {
            return;
        }

        try
        {
            var destinationDirectory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            File.Copy(sourcePath, destinationPath);
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
    }

    private static bool SamePath(string firstPath, string secondPath)
    {
        return string.Equals(
            Path.GetFullPath(firstPath),
            Path.GetFullPath(secondPath),
            RuntimePathComparison);
    }

    private static bool IsEscapingRelativePath(string relativePath)
    {
        return relativePath == ".."
            || relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || relativePath.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal);
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
                RuntimePathComparison)))
        {
            return;
        }

        directories.Add(fullPath);
    }
}
