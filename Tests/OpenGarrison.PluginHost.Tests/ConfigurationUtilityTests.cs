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
}
