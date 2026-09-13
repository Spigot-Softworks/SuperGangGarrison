#nullable enable

using OpenGarrison.Core;
using System.Collections.Generic;
using System.IO;

namespace OpenGarrison.Client;

internal static class HostSetupLaunchPlaylist
{
    private const string LaunchPlaylistFileName = "host-launch-playlist.txt";

    public static string? TryWrite(IReadOnlyList<string> levelNames)
    {
        if (levelNames.Count == 0)
        {
            return null;
        }

        var path = RuntimePaths.GetConfigPath(Path.Combine("host-playlists", Guid.NewGuid().ToString("N"), LaunchPlaylistFileName));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var lines = new List<string>(levelNames.Count + 2)
        {
            "# OpenGarrison host launch playlist",
            "# Generated from Host Setup map list.",
        };
        lines.AddRange(levelNames);
        File.WriteAllLines(path, lines);
        return path;
    }
}
