namespace OpenGarrison.Tools.BrowserAssetBuilder;

internal sealed record BrowserAssetBuildContext(
    DirectoryInfo RepoRoot,
    string ContentRoot,
    string OutputContentRoot,
    string BrowserWwwRoot,
    string BrowserOutputRoot,
    string BrowserPluginsRoot,
    string? PackagedClientPluginSourceRoot,
    bool PruneDeprecatedGameMakerMetadata,
    string BrowserAtlasesRoot,
    string BrowserManifestsRoot,
    string BrowserBootstrapRoot)
{
    public static BrowserAssetBuildContext Create(
        string outputContentRoot,
        string startPath,
        string? packagedClientPluginSourceRoot = null,
        bool pruneDeprecatedGameMakerMetadata = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputContentRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(startPath);

        var repoRoot = FindRepoRoot(startPath);
        var contentRoot = Path.Combine(repoRoot.FullName, "Core", "Content");
        var browserWwwRoot = Directory.GetParent(outputContentRoot)?.FullName
            ?? throw new DirectoryNotFoundException($"Could not resolve the browser wwwroot for \"{outputContentRoot}\".");
        var browserOutputRoot = Path.Combine(outputContentRoot, "Browser");
        return new BrowserAssetBuildContext(
            repoRoot,
            contentRoot,
            outputContentRoot,
            browserWwwRoot,
            browserOutputRoot,
            Path.Combine(browserWwwRoot, "Plugins"),
            string.IsNullOrWhiteSpace(packagedClientPluginSourceRoot)
                ? null
                : Path.GetFullPath(packagedClientPluginSourceRoot),
            pruneDeprecatedGameMakerMetadata,
            Path.Combine(browserOutputRoot, "Atlases"),
            Path.Combine(browserOutputRoot, "Manifests"),
            Path.Combine(browserOutputRoot, "Bootstrap"));
    }

    private static DirectoryInfo FindRepoRoot(string startPath)
    {
        for (var current = new DirectoryInfo(Path.GetFullPath(startPath)); current is not null; current = current.Parent)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "Core", "Content")))
            {
                return current;
            }
        }

        throw new DirectoryNotFoundException(
            $"Could not locate the repository root containing Core{Path.DirectorySeparatorChar}Content from \"{startPath}\".");
    }
}
