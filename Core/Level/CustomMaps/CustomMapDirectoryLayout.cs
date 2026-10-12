using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenGarrison.Core;

/// <summary>
/// Maps folders may be sorted into plain grouping folders, as the shipped game is:
/// <c>Maps/Super Gang Garrison/cp/Docking/Docking.json</c>,
/// <c>Maps/Classic/koth/koth_valley/...</c>, <c>Maps/Custom/...</c>. A folder holding a map
/// manifest is a map package; any other folder is a group to look inside. Legacy PNG maps
/// may sit in the root or in any group. Names stay unique by map name, not by path.
/// </summary>
public static class CustomMapDirectoryLayout
{
    public const string SuperGangGarrisonCategory = "Super Gang Garrison";
    public const string ClassicCategory = "Classic";
    public const string CustomCategory = "Custom";

    /// <summary>How many grouping levels are searched below a maps root (category/mode plus slack).</summary>
    public const int MaxGroupDepth = 3;

    /// <summary>The root itself plus every grouping folder under it (never a package's own folder).</summary>
    public static IEnumerable<string> EnumerateGroupDirectories(string mapsRoot)
    {
        if (string.IsNullOrWhiteSpace(mapsRoot) || !Directory.Exists(mapsRoot))
        {
            yield break;
        }

        var pending = new Stack<(string Directory, int Depth)>();
        pending.Push((mapsRoot, 0));
        while (pending.Count > 0)
        {
            var (directory, depth) = pending.Pop();
            yield return directory;
            if (depth >= MaxGroupDepth)
            {
                continue;
            }

            foreach (var child in SafeEnumerateDirectories(directory)
                         .OrderByDescending(static path => path, StringComparer.OrdinalIgnoreCase))
            {
                // Any folder with a manifest-like .json is a package (or a broken one), not a group.
                if (IsSkippedDirectoryName(Path.GetFileName(child)) || SafeEnumerateFiles(child, "*.json").Any())
                {
                    continue;
                }

                pending.Push((child, depth + 1));
            }
        }
    }

    /// <summary>Every map package folder under the root, at any grouping depth, in path order.</summary>
    public static IEnumerable<string> EnumeratePackageDirectories(string mapsRoot)
    {
        foreach (var group in EnumerateGroupDirectories(mapsRoot))
        {
            foreach (var child in SafeEnumerateDirectories(group)
                         .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase))
            {
                if (!IsSkippedDirectoryName(Path.GetFileName(child))
                    && CustomMapPackageImporter.TryFindManifestInDirectory(child, out _))
                {
                    yield return child;
                }
            }
        }
    }

    /// <summary>Every legacy PNG map under the root or its grouping folders, in path order.</summary>
    public static IEnumerable<string> EnumerateLegacyPngMaps(string mapsRoot)
    {
        foreach (var group in EnumerateGroupDirectories(mapsRoot))
        {
            foreach (var file in SafeEnumerateFiles(group, "*.png")
                         .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase))
            {
                yield return file;
            }
        }
    }

    /// <summary>The package folder for a map name anywhere under the root (the name is the folder's).</summary>
    public static bool TryFindPackageManifest(string mapsRoot, string levelName, out string manifestPath)
    {
        manifestPath = string.Empty;
        if (string.IsNullOrWhiteSpace(levelName))
        {
            return false;
        }

        foreach (var group in EnumerateGroupDirectories(mapsRoot))
        {
            var candidate = Path.Combine(group, levelName);
            if (CustomMapPackageImporter.TryFindManifestInDirectory(candidate, out manifestPath)
                && Path.GetFileNameWithoutExtension(manifestPath).Equals(levelName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            foreach (var child in SafeEnumerateDirectories(group))
            {
                if (Path.GetFileName(child).Equals(levelName, StringComparison.OrdinalIgnoreCase)
                    && CustomMapPackageImporter.TryFindManifestInDirectory(child, out manifestPath)
                    && Path.GetFileNameWithoutExtension(manifestPath).Equals(levelName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        manifestPath = string.Empty;
        return false;
    }

    /// <summary>A legacy PNG map by name anywhere under the root.</summary>
    public static bool TryFindLegacyPngMap(string mapsRoot, string levelName, out string mapPath)
    {
        mapPath = string.Empty;
        if (string.IsNullOrWhiteSpace(levelName))
        {
            return false;
        }

        foreach (var group in EnumerateGroupDirectories(mapsRoot))
        {
            var candidate = Path.Combine(group, $"{levelName}.png");
            if (File.Exists(candidate))
            {
                mapPath = candidate;
                return true;
            }

            var match = SafeEnumerateFiles(group, "*.png")
                .FirstOrDefault(path => Path.GetFileNameWithoutExtension(path).Equals(levelName, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                mapPath = match;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The mode folder for a map, from its name prefix: cp, koth, dkoth, ctf, tdm, arena, rj...
    /// (3cp counts as cp); "other" when the name has none.
    /// </summary>
    public static string GetModeFolderName(string mapName)
    {
        if (string.IsNullOrWhiteSpace(mapName))
        {
            return "other";
        }

        var separator = mapName.IndexOf('_');
        if (separator <= 0)
        {
            return "other";
        }

        var prefix = mapName[..separator].Trim().ToLowerInvariant();
        if (prefix.Length > 1 && char.IsDigit(prefix[0]))
        {
            prefix = prefix.TrimStart('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
        }

        return prefix.Length == 0 || prefix.Any(static c => !char.IsLetterOrDigit(c)) ? "other" : prefix;
    }

    /// <summary>Name prefixes that are their own mode folder (game modes and jump/training map styles).</summary>
    private static readonly HashSet<string> KnownModePrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "cp", "cq", "koth", "dkoth", "ctf", "tdm", "arena", "gen", "scr", "vip", "rj", "dj", "jt", "nfa", "rr", "dm",
    };

    /// <summary>The mode folder for a game mode.</summary>
    public static string GetModeFolderName(GameModeKind mode) => mode switch
    {
        GameModeKind.CaptureTheFlag => "ctf",
        GameModeKind.Arena => "arena",
        GameModeKind.ControlPoint => "cp",
        GameModeKind.Generator => "gen",
        GameModeKind.KingOfTheHill => "koth",
        GameModeKind.DoubleKingOfTheHill => "dkoth",
        GameModeKind.Conquest => "cq",
        GameModeKind.TeamDeathmatch => "tdm",
        GameModeKind.Scr => "scr",
        GameModeKind.Vip => "vip",
        _ => "other",
    };

    /// <summary>
    /// The mode folder for a package: its name prefix when that is a known one, otherwise
    /// the mode the map plays (from its entities).
    /// </summary>
    public static string ResolveModeFolderName(string mapName, string? manifestPath)
    {
        var byName = GetModeFolderName(mapName);
        if (KnownModePrefixes.Contains(byName) || string.IsNullOrWhiteSpace(manifestPath))
        {
            return byName;
        }

        var imported = CustomMapPackageImporter.Import(manifestPath);
        return imported is null ? byName : GetModeFolderName(SimpleLevelFactory.DetectMode(imported.Room));
    }

    /// <summary>Hidden and temporary folders (download staging, backups) are never maps or groups.</summary>
    private static bool IsSkippedDirectoryName(string? name) =>
        string.IsNullOrEmpty(name) || name.StartsWith('.');

    private static IEnumerable<string> SafeEnumerateDirectories(string directory)
    {
        try
        {
            return Directory.EnumerateDirectories(directory, "*", SearchOption.TopDirectoryOnly).ToArray();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
    }

    private static IEnumerable<string> SafeEnumerateFiles(string directory, string pattern)
    {
        try
        {
            return Directory.EnumerateFiles(directory, pattern, SearchOption.TopDirectoryOnly).ToArray();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
    }
}
