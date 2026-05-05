using OpenGarrison.Tools.BrowserAssetBuilder;

if (args.Length is < 1 or > 2)
{
    Console.Error.WriteLine("Usage: OpenGarrison.Tools.BrowserAssetBuilder <output-content-root> [packaged-client-plugin-source-root]");
    return 1;
}

var outputContentRoot = Path.GetFullPath(args[0]);
var packagedClientPluginSourceRoot = args.Length == 2 ? args[1] : null;
var context = BrowserAssetBuildContext.Create(outputContentRoot, AppContext.BaseDirectory, packagedClientPluginSourceRoot);
var report = BrowserAssetBuildPipeline.Run(context);

Console.WriteLine(
    $"Browser asset build completed. Atlases={report.GeneratedAtlasCount} Pages={report.GeneratedAtlasPageCount} " +
    $"Sprites={report.GeneratedSpriteCount} Warnings={report.Warnings.Count} Output={context.BrowserOutputRoot}");
return 0;
