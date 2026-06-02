#nullable enable

using OpenGarrison.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenGarrison.Client;

internal static class HostSetupMapFavouritesStore
{
    private const string FavouritesFileName = "hostMapFavourites.txt";

    public static HashSet<string> Load()
    {
        var path = GetFavouritesPath();
        if (!File.Exists(path))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var favourites = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in File.ReadAllLines(path))
        {
            var trimmed = line.Trim();
            if (!string.IsNullOrWhiteSpace(trimmed) && !trimmed.StartsWith('#'))
            {
                favourites.Add(trimmed);
            }
        }

        return favourites;
    }

    public static void Save(IEnumerable<string> favouriteLevelNames)
    {
        var path = GetFavouritesPath();
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var lines = favouriteLevelNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        File.WriteAllLines(path, lines);
    }

    private static string GetFavouritesPath()
    {
        return RuntimePaths.GetUserDataPath(FavouritesFileName);
    }
}
