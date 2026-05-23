namespace OpenGarrison.PluginHost;

public static class OpenGarrisonPluginPathContainment
{
    public static string ResolveContainedPath(string rootDirectory, string relativePath, string errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        var fullRootDirectory = Path.GetFullPath(rootDirectory);
        var combinedPath = Path.GetFullPath(Path.Combine(fullRootDirectory, relativePath));
        if (!IsPathContained(fullRootDirectory, combinedPath))
        {
            throw new InvalidOperationException(errorMessage);
        }

        return combinedPath;
    }

    public static bool TryResolveContainedPath(
        string rootDirectory,
        string relativePath,
        out string containedPath,
        out string error)
    {
        containedPath = string.Empty;
        error = string.Empty;

        try
        {
            containedPath = ResolveContainedPath(
                rootDirectory,
                relativePath,
                $"Path escapes root directory: {relativePath}");
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static bool IsPathContained(string rootDirectory, string candidatePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(candidatePath);

        var fullRootDirectory = Path.GetFullPath(rootDirectory);
        var fullCandidatePath = Path.GetFullPath(candidatePath);
        var relativePath = Path.GetRelativePath(fullRootDirectory, fullCandidatePath);
        return relativePath == "."
            || (!Path.IsPathRooted(relativePath)
                && !relativePath.Equals("..", StringComparison.Ordinal)
                && !relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && !relativePath.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal));
    }
}
