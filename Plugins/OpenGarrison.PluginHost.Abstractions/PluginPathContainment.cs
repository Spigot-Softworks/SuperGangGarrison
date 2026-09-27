namespace OpenGarrison.PluginHost;

/// <summary>
/// Helpers for keeping plugin file paths contained within a root directory.
/// </summary>
public static class OpenGarrisonPluginPathContainment
{
    /// <summary>
    /// Resolves a relative path inside a root directory, throwing when it escapes the root.
    /// </summary>
    /// <param name="rootDirectory">The root directory.</param>
    /// <param name="relativePath">The relative path.</param>
    /// <param name="errorMessage">The exception message when the path escapes the root.</param>
    /// <returns>The fully resolved contained path.</returns>
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


    /// <summary>
    /// Checks whether a candidate path is contained within a root directory.
    /// </summary>
    /// <param name="rootDirectory">The root directory.</param>
    /// <param name="candidatePath">The candidate path.</param>
    /// <returns>True when the candidate path is inside the root directory; otherwise false.</returns>
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
