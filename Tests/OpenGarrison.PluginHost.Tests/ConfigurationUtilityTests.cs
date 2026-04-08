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
        var relativePath = Path.Combine("test-config", Guid.NewGuid().ToString("N"), "settings.json");

        var resolvedPath = RuntimePaths.GetConfigPath(relativePath);

        Assert.Equal(
            Path.GetFullPath(Path.Combine(RuntimePaths.ConfigDirectory, relativePath)),
            Path.GetFullPath(resolvedPath));
        Assert.StartsWith(
            EnsureTrailingSeparator(Path.GetFullPath(RuntimePaths.ConfigDirectory)),
            Path.GetFullPath(resolvedPath),
            RuntimePathComparison);
    }

    [Fact]
    public void GetConfigPathRejectsEscapingRelativePaths()
    {
        Assert.Throws<InvalidOperationException>(() => RuntimePaths.GetConfigPath(Path.Combine("..", "escape.json")));
    }

    [Fact]
    public void GetLogPathRejectsRootedPaths()
    {
        var rootedPath = Path.Combine(Path.GetTempPath(), "should-not-be-used.log");

        Assert.Throws<ArgumentException>(() => RuntimePaths.GetLogPath(rootedPath));
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
