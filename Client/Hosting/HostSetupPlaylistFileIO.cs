#nullable enable

using OpenGarrison.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenGarrison.Client;

internal static class HostSetupPlaylistFileIO
{
    public static IReadOnlyList<string> ReadPlaylistLines(string path)
    {
        return File.ReadAllLines(path)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith('#'))
            .ToArray();
    }

    public static void WritePlaylist(string path, IEnumerable<OpenGarrisonMapRotationEntry> playlistEntries)
    {
        var lines = new List<string>
        {
            "# OpenGarrison playlist",
            "# One map per line. Lines starting with # are ignored.",
        };

        foreach (var entry in playlistEntries)
        {
            var line = string.IsNullOrWhiteSpace(entry.IniKey) ? entry.LevelName : entry.IniKey;
            if (!string.IsNullOrWhiteSpace(line))
            {
                lines.Add(line);
            }
        }

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllLines(path, lines);
    }

    public static bool TryResolvePlaylistLine(string line, IReadOnlyList<OpenGarrisonMapRotationEntry> catalog, out OpenGarrisonMapRotationEntry entry)
    {
        entry = null!;
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        if (OpenGarrisonStockMapCatalog.TryGetDefinition(line, out _))
        {
            entry = catalog.FirstOrDefault(candidate =>
                string.Equals(candidate.IniKey, line, StringComparison.OrdinalIgnoreCase)
                || string.Equals(candidate.LevelName, line, StringComparison.OrdinalIgnoreCase));
            return entry is not null;
        }

        entry = catalog.FirstOrDefault(candidate =>
            string.Equals(candidate.LevelName, line, StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate.IniKey, line, StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate.DisplayName, line, StringComparison.OrdinalIgnoreCase));
        return entry is not null;
    }
}
