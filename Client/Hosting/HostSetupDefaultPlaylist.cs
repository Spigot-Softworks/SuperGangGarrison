#nullable enable

using OpenGarrison.Core;
using System.IO;

namespace OpenGarrison.Client;

internal static class HostSetupDefaultPlaylist
{
    public const string FileName = "DefaultPlaylist.txt";

    public static string GetBundledPath()
    {
        return Path.Combine(RuntimePaths.ApplicationRoot, "Content", FileName);
    }

    public static bool TryGetBundledPath(out string path)
    {
        path = GetBundledPath();
        return File.Exists(path);
    }
}
