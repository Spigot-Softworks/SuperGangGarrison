using System;
using System.Collections.Generic;
using System.IO;

namespace OpenGarrison.Core;

public static class ProjectSourceLocator
{
    public static string? FindFile(string relativePathFromRepoRoot)
    {
        return FindPath(relativePathFromRepoRoot, static candidate => File.Exists(candidate));
    }

    public static string? FindDirectory(string relativePathFromRepoRoot)
    {
        return FindPath(relativePathFromRepoRoot, static candidate => Directory.Exists(candidate));
    }

    private static string? FindPath(string relativePathFromRepoRoot, Func<string, bool> exists)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePathFromRepoRoot);
        ArgumentNullException.ThrowIfNull(exists);

        foreach (var candidate in EnumerateSearchRoots())
        {
            var directory = new DirectoryInfo(candidate);
            while (directory is not null)
            {
                var fullPath = Path.Combine(directory.FullName, relativePathFromRepoRoot);
                if (exists(fullPath))
                {
                    return fullPath;
                }

                directory = directory.Parent;
            }
        }

        return null;
    }

    private static string[] EnumerateSearchRoots()
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var baseDirectory = AppContext.BaseDirectory;
        var comparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        var uniqueRoots = new HashSet<string>(comparer);
        var candidates =
            new[]
            {
                currentDirectory,
                baseDirectory,
                Path.Combine(currentDirectory, "Assets"),
                Path.Combine(baseDirectory, "Assets"),
            };
        var roots = new List<string>(candidates.Length);
        for (var index = 0; index < candidates.Length; index += 1)
        {
            var candidate = candidates[index];
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            var fullPath = Path.GetFullPath(candidate);
            if (uniqueRoots.Add(fullPath))
            {
                roots.Add(fullPath);
            }
        }

        return roots.ToArray();
    }
}
