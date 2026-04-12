namespace OpenGarrison.ClientShared;

public static class BrowserClientPluginCompatibility
{
    private const string MoreAnimationsPluginId = "moreanimations";
    private const string MoreAnimationsPackagedDirectoryName = "Lua.MoreAnimations";

    public static bool IsBrowserDisabledPluginId(string pluginId) =>
        string.Equals(pluginId, MoreAnimationsPluginId, StringComparison.OrdinalIgnoreCase);

    public static bool IsBrowserDisabledPackagedClientPluginPath(string packagedClientPluginsRoot, string sourcePath)
    {
        var relativePath = Path.GetRelativePath(packagedClientPluginsRoot, sourcePath);
        if (relativePath.Length == 0 || relativePath.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relativePath))
        {
            return false;
        }

        var separatorIndex = relativePath.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]);
        var pluginDirectoryName = separatorIndex >= 0 ? relativePath[..separatorIndex] : relativePath;
        return string.Equals(pluginDirectoryName, MoreAnimationsPackagedDirectoryName, StringComparison.OrdinalIgnoreCase);
    }
}
