using OpenGarrison.Tools.BrowserAssetBuilder;

if (args.Length is < 1 or > 5)
{
    Console.Error.WriteLine("Usage: OpenGarrison.Tools.BrowserAssetBuilder <output-content-root> [packaged-client-plugin-source-root] [--repo-root=<path>] [--prune-deprecated-gamemaker-metadata] [--manifest-only|--runtime-bundle-only]");
    return 1;
}

var outputContentRoot = Path.GetFullPath(args[0]);
var pruneDeprecatedGameMakerMetadata = args.Any(static arg => string.Equals(arg, "--prune-deprecated-gamemaker-metadata", StringComparison.OrdinalIgnoreCase));
var manifestOnly = args.Any(static arg => string.Equals(arg, "--manifest-only", StringComparison.OrdinalIgnoreCase));
var packagedClientPluginSourceRoot = args
    .Skip(1)
    .FirstOrDefault(static arg => !arg.StartsWith("--", StringComparison.Ordinal));
var context = BrowserAssetBuildContext.Create(
    outputContentRoot,
    args.FirstOrDefault(static arg => arg.StartsWith("--repo-root=", StringComparison.Ordinal))?["--repo-root=".Length..] ?? AppContext.BaseDirectory,
    packagedClientPluginSourceRoot,
    pruneDeprecatedGameMakerMetadata);
if (args.Any(static arg => string.Equals(arg, "--runtime-bundle-only", StringComparison.OrdinalIgnoreCase)))
{
    var bundlePath = BrowserAssetBuildPipeline.WriteRuntimeBundleOnly(context);
    Console.WriteLine($"Browser runtime asset bundle generated from staged content: {bundlePath}");
    return 0;
}
if (manifestOnly)
{
    var manifestPath = BrowserAssetBuildPipeline.WriteGameMakerManifestOnly(context);
    Console.WriteLine($"GameMaker asset manifest generated: {manifestPath}");
    return 0;
}

var report = BrowserAssetBuildPipeline.Run(context);

Console.WriteLine(
    $"Browser asset build completed. Atlases={report.GeneratedAtlasCount} Pages={report.GeneratedAtlasPageCount} " +
    $"Sprites={report.GeneratedSpriteCount} Warnings={report.Warnings.Count} Output={context.BrowserOutputRoot}");
return 0;
