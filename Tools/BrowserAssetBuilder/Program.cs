using OpenGarrison.Tools.BrowserAssetBuilder;

if (args.Length is < 1 or > 4)
{
    Console.Error.WriteLine("Usage: OpenGarrison.Tools.BrowserAssetBuilder <output-content-root> [packaged-client-plugin-source-root] [--prune-deprecated-gamemaker-metadata] [--manifest-only]");
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
    AppContext.BaseDirectory,
    packagedClientPluginSourceRoot,
    pruneDeprecatedGameMakerMetadata);
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
