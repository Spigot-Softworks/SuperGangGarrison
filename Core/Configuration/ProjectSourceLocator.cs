using System;
using System.IO;

namespace OpenGarrison.Core;

public static class ProjectSourceLocator
{
    public static string? FindFile(string relativePathFromRepoRoot)
    {
        foreach (var candidate in EnumerateSearchRoots())
        {
            var directory = new DirectoryInfo(candidate);
            while (directory is not null)
            {
                var fullPath = Path.Combine(directory.FullName, relativePathFromRepoRoot);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }

                directory = directory.Parent;
            }
        }

        return null;
    }

    public static string? FindDirectory(string relativePathFromRepoRoot)
    {
        foreach (var candidate in EnumerateSearchRoots())
        {
            var directory = new DirectoryInfo(candidate);
            while (directory is not null)
            {
                var fullPath = Path.Combine(directory.FullName, relativePathFromRepoRoot);
                if (Directory.Exists(fullPath))
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
        return
        [
            currentDirectory,
            baseDirectory,
            Path.Combine(currentDirectory, "Assets"),
            Path.Combine(baseDirectory, "Assets"),
        ];
    }
}
