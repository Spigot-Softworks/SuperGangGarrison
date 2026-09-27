using System;
using System.IO;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class ConfigurationUtilityTests
{
    [Fact]
    public void GetConfigPathReturnsPathUnderConfigDirectoryForRelativeSubpath()
    {
        var previous = Environment.GetEnvironmentVariable("OPENGARRISON_USER_DATA_ROOT");
        var root = Path.Combine(Path.GetTempPath(), "opengarrison-config-tests", Guid.NewGuid().ToString("N"));
        var relativePath = Path.Combine("test-config", Guid.NewGuid().ToString("N"), "settings.json");

        try
        {
            Environment.SetEnvironmentVariable("OPENGARRISON_USER_DATA_ROOT", root);

            var resolvedPath = RuntimePaths.GetConfigPath(relativePath);
            var expectedConfigDirectory = Path.Combine(root, "config");

            Assert.Equal(
                Path.GetFullPath(Path.Combine(expectedConfigDirectory, relativePath)),
                Path.GetFullPath(resolvedPath));
            Assert.StartsWith(
                EnsureTrailingSeparator(Path.GetFullPath(expectedConfigDirectory)),
                Path.GetFullPath(resolvedPath),
                RuntimePathComparison);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENGARRISON_USER_DATA_ROOT", previous);
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void GetConfigPathRejectsEscapingRelativePaths()
    {
        var previous = Environment.GetEnvironmentVariable("OPENGARRISON_USER_DATA_ROOT");
        var root = Path.Combine(Path.GetTempPath(), "opengarrison-config-tests", Guid.NewGuid().ToString("N"));

        try
        {
            Environment.SetEnvironmentVariable("OPENGARRISON_USER_DATA_ROOT", root);

            Assert.Throws<InvalidOperationException>(() => RuntimePaths.GetConfigPath(Path.Combine("..", "escape.json")));
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENGARRISON_USER_DATA_ROOT", previous);
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void GetLogPathRejectsRootedPaths()
    {
        var rootedPath = Path.Combine(Path.GetTempPath(), "should-not-be-used.log");

        Assert.Throws<ArgumentException>(() => RuntimePaths.GetLogPath(rootedPath));
    }

    [Fact]
    public void UserDataRootUsesConfiguredWritableDirectory()
    {
        var previous = Environment.GetEnvironmentVariable("OPENGARRISON_USER_DATA_ROOT");
        var root = Path.Combine(Path.GetTempPath(), "opengarrison-userdata-tests", Guid.NewGuid().ToString("N"));

        try
        {
            Environment.SetEnvironmentVariable("OPENGARRISON_USER_DATA_ROOT", root);

            var resolved = RuntimePaths.UserDataRoot;

            Assert.Equal(Path.GetFullPath(root), Path.GetFullPath(resolved));
            Assert.True(Directory.Exists(resolved));
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENGARRISON_USER_DATA_ROOT", previous);
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void ApplyUserDataRootArgumentSetsEnvironmentAndRemovesArgument()
    {
        var previous = Environment.GetEnvironmentVariable("OPENGARRISON_USER_DATA_ROOT");
        var root = Path.Combine(Path.GetTempPath(), "opengarrison-userdata-tests", Guid.NewGuid().ToString("N"));

        try
        {
            Environment.SetEnvironmentVariable("OPENGARRISON_USER_DATA_ROOT", null);

            var filtered = RuntimePaths.ApplyUserDataRootArgument(["--foo", "--user-data-root", root, "--bar"]);

            Assert.Equal(["--foo", "--bar"], filtered);
            Assert.Equal(root, Environment.GetEnvironmentVariable("OPENGARRISON_USER_DATA_ROOT"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENGARRISON_USER_DATA_ROOT", previous);
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void UserDataRootFallsBackWhenConfiguredPathIsNotDirectory()
    {
        var previous = Environment.GetEnvironmentVariable("OPENGARRISON_USER_DATA_ROOT");
        var root = Path.Combine(Path.GetTempPath(), "opengarrison-userdata-tests", Guid.NewGuid().ToString("N"));
        var filePath = Path.Combine(root, "not-a-directory");

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(filePath, "not a directory");
            Environment.SetEnvironmentVariable("OPENGARRISON_USER_DATA_ROOT", filePath);

            var resolved = RuntimePaths.UserDataRoot;

            Assert.NotEqual(Path.GetFullPath(filePath), Path.GetFullPath(resolved));
            Assert.True(Directory.Exists(resolved));
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENGARRISON_USER_DATA_ROOT", previous);
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void FindFileLocatesContentUnderAssetsProbeRoot()
    {
        var relativePath = Path.Combine("locator-tests", Guid.NewGuid().ToString("N"), "marker.txt");
        var fullPath = Path.Combine(AppContext.BaseDirectory, "Assets", relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, "marker");

        try
        {
            var resolvedPath = ProjectSourceLocator.FindFile(relativePath);

            Assert.NotNull(resolvedPath);
            Assert.Equal(Path.GetFullPath(fullPath), Path.GetFullPath(resolvedPath!));
        }
        finally
        {
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
    }

    [Fact]
    public void FindDirectoryLocatesDirectoryUnderAssetsProbeRoot()
    {
        var relativePath = Path.Combine("locator-tests", Guid.NewGuid().ToString("N"), "content");
        var fullPath = Path.Combine(AppContext.BaseDirectory, "Assets", relativePath);
        Directory.CreateDirectory(fullPath);

        try
        {
            var resolvedPath = ProjectSourceLocator.FindDirectory(relativePath);

            Assert.NotNull(resolvedPath);
            Assert.Equal(Path.GetFullPath(fullPath), Path.GetFullPath(resolvedPath!));
        }
        finally
        {
            if (Directory.Exists(fullPath))
            {
                Directory.Delete(fullPath, recursive: true);
            }
        }
    }

    private static StringComparison RuntimePathComparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    private static string EnsureTrailingSeparator(string path)
    {
        if (path.EndsWith(Path.DirectorySeparatorChar)
            || path.EndsWith(Path.AltDirectorySeparatorChar))
        {
            return path;
        }

        return path + Path.DirectorySeparatorChar;
    }

    [Fact]
    public void LegacyPreferencesMigrationMigratesClientSettings()
    {
        var previous = Environment.GetEnvironmentVariable("OPENGARRISON_USER_DATA_ROOT");
        var root = Path.Combine(Path.GetTempPath(), "opengarrison-migration-tests", Guid.NewGuid().ToString("N"));

        try
        {
            Environment.SetEnvironmentVariable("OPENGARRISON_USER_DATA_ROOT", root);

            var legacyPath = RuntimePaths.GetConfigPath("client.settings.json");
            Directory.CreateDirectory(Path.GetDirectoryName(legacyPath)!);
            File.WriteAllText(legacyPath, """{"PlayerName": "MigratedPlayer", "Fullscreen": true}""");

            var destinationPath = Path.Combine(root, "migrated.ini");
            Assert.True(OpenGarrisonLegacyPreferencesMigration.TryMigrate(destinationPath));

            var migrated = OpenGarrisonPreferencesDocument.Load(destinationPath);
            Assert.Equal("MigratedPlayer", migrated.PlayerName);
            Assert.True(migrated.Fullscreen);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENGARRISON_USER_DATA_ROOT", previous);
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void LegacyPreferencesMigrationReturnsFalseForMalformedJson()
    {
        var previous = Environment.GetEnvironmentVariable("OPENGARRISON_USER_DATA_ROOT");
        var root = Path.Combine(Path.GetTempPath(), "opengarrison-migration-tests", Guid.NewGuid().ToString("N"));

        try
        {
            Environment.SetEnvironmentVariable("OPENGARRISON_USER_DATA_ROOT", root);

            var legacyPath = RuntimePaths.GetConfigPath("client.settings.json");
            Directory.CreateDirectory(Path.GetDirectoryName(legacyPath)!);
            File.WriteAllText(legacyPath, "{ not valid json");

            var destinationPath = Path.Combine(root, "migrated.ini");
            Assert.False(OpenGarrisonLegacyPreferencesMigration.TryMigrate(destinationPath));
            Assert.False(File.Exists(destinationPath));
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENGARRISON_USER_DATA_ROOT", previous);
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void LegacyPreferencesMigrationDoesNotOverwriteExistingDestination()
    {
        var previous = Environment.GetEnvironmentVariable("OPENGARRISON_USER_DATA_ROOT");
        var root = Path.Combine(Path.GetTempPath(), "opengarrison-migration-tests", Guid.NewGuid().ToString("N"));

        try
        {
            Environment.SetEnvironmentVariable("OPENGARRISON_USER_DATA_ROOT", root);

            var legacyPath = RuntimePaths.GetConfigPath("client.settings.json");
            Directory.CreateDirectory(Path.GetDirectoryName(legacyPath)!);
            File.WriteAllText(legacyPath, """{"PlayerName": "MigratedPlayer"}""");

            var destinationPath = Path.Combine(root, "existing.ini");
            Directory.CreateDirectory(root);
            File.WriteAllText(destinationPath, "existing");

            Assert.False(OpenGarrisonLegacyPreferencesMigration.TryMigrate(destinationPath));
            Assert.Equal("existing", File.ReadAllText(destinationPath));
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENGARRISON_USER_DATA_ROOT", previous);
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void LegacyPreferencesMigrationReturnsFalseWhenNothingToMigrate()
    {
        var previous = Environment.GetEnvironmentVariable("OPENGARRISON_USER_DATA_ROOT");
        var root = Path.Combine(Path.GetTempPath(), "opengarrison-migration-tests", Guid.NewGuid().ToString("N"));

        try
        {
            Environment.SetEnvironmentVariable("OPENGARRISON_USER_DATA_ROOT", root);

            var destinationPath = Path.Combine(root, "migrated.ini");
            Assert.False(OpenGarrisonLegacyPreferencesMigration.TryMigrate(destinationPath));
            Assert.False(File.Exists(destinationPath));
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENGARRISON_USER_DATA_ROOT", previous);
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
