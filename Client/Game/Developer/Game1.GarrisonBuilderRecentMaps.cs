#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

/// <summary>
/// The builder's recent maps: the last few maps opened or saved, listed under "Open map..."
/// in the File menu. Kept in a small text file next to the other settings (one path per line,
/// newest first). Recovered drafts are left out; they live in the recovery folder.
/// </summary>
public partial class Game1
{
    private const int GarrisonBuilderRecentMapCount = 3;
    private const int GarrisonBuilderRecentMapLabelLength = 30;
    private const string GarrisonBuilderRecentMapsFileName = "builder-recent-maps.txt";

    private List<string>? _builderRecentMaps;

    private static string GarrisonBuilderRecentMapsPath => Path.Combine(RuntimePaths.ConfigDirectory, GarrisonBuilderRecentMapsFileName);

    private List<string> GetGarrisonBuilderRecentMaps()
    {
        if (_builderRecentMaps is not null)
        {
            return _builderRecentMaps;
        }

        _builderRecentMaps = new List<string>();
        try
        {
            if (File.Exists(GarrisonBuilderRecentMapsPath))
            {
                foreach (var line in File.ReadAllLines(GarrisonBuilderRecentMapsPath))
                {
                    var path = line.Trim();
                    if (path.Length > 0
                        && !_builderRecentMaps.Contains(path, StringComparer.OrdinalIgnoreCase)
                        && _builderRecentMaps.Count < GarrisonBuilderRecentMapCount)
                    {
                        _builderRecentMaps.Add(path);
                    }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // No recent list is not worth bothering anyone about.
        }

        return _builderRecentMaps;
    }

    /// <summary>Moves a map to the top of the recent list (after it was opened or saved).</summary>
    private void RememberGarrisonBuilderRecentMap(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path.Trim().Trim('"'));
            var recovery = Path.GetFullPath(BuilderRecoveryDirectory) + Path.DirectorySeparatorChar;
            if (fullPath.StartsWith(recovery, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return;
        }

        var recent = GetGarrisonBuilderRecentMaps();
        recent.RemoveAll(entry => entry.Equals(fullPath, StringComparison.OrdinalIgnoreCase));
        recent.Insert(0, fullPath);
        if (recent.Count > GarrisonBuilderRecentMapCount)
        {
            recent.RemoveRange(GarrisonBuilderRecentMapCount, recent.Count - GarrisonBuilderRecentMapCount);
        }

        SaveGarrisonBuilderRecentMaps();
    }

    private void SaveGarrisonBuilderRecentMaps()
    {
        try
        {
            Directory.CreateDirectory(RuntimePaths.ConfigDirectory);
            File.WriteAllLines(GarrisonBuilderRecentMapsPath, GetGarrisonBuilderRecentMaps());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AddConsoleLine($"builder: could not save the recent maps list ({ex.Message})");
        }
    }

    private void OpenGarrisonBuilderRecentMap(string path)
    {
        if (!File.Exists(path))
        {
            GetGarrisonBuilderRecentMaps().RemoveAll(entry => entry.Equals(path, StringComparison.OrdinalIgnoreCase));
            SaveGarrisonBuilderRecentMaps();
            _builderStatus = $"{Path.GetFileName(path)} is no longer there; removed from recent maps";
            AddConsoleLine($"builder recent map missing: {path}");
            return;
        }

        OpenGarrisonBuilderMap(path);
    }

    /// <summary>File menu rows for the recent maps, newest first (none when the list is empty).</summary>
    private IEnumerable<(string Label, Action Action)> GetGarrisonBuilderRecentMapMenuItems()
    {
        foreach (var path in GetGarrisonBuilderRecentMaps().ToArray())
        {
            var name = Path.GetFileName(path);
            if (name.Length > GarrisonBuilderRecentMapLabelLength)
            {
                name = name[..(GarrisonBuilderRecentMapLabelLength - 3)] + "...";
            }

            var target = path;
            yield return ($"  Recent: {name}", () => OpenGarrisonBuilderRecentMap(target));
        }
    }
}
