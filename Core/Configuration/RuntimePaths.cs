using System;
using System.IO;

namespace OpenGarrison.Core;

public static class RuntimePaths
{
    public static string ApplicationRoot => AppContext.BaseDirectory;

    public static string AssetsDirectory => Path.Combine(ApplicationRoot, "Assets");

    public static string ConfigDirectory
    {
        get
        {
            var path = Path.Combine(ApplicationRoot, "config");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    public static string MapsDirectory
    {
        get
        {
            var path = Path.Combine(ApplicationRoot, "Maps");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    public static string LogsDirectory
    {
        get
        {
            var path = Path.Combine(ApplicationRoot, "logs");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    public static string GetConfigPath(string fileName)
    {
        return ResolvePathUnderRoot(ConfigDirectory, fileName);
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
}
