using OpenGarrison.Tools.BrowserAssetBuilder;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: OpenGarrison.Tools.BrowserAssetBuilder <output-content-root>");
    return 1;
}

var outputContentRoot = Path.GetFullPath(args[0]);
var context = BrowserAssetBuildContext.Create(outputContentRoot, AppContext.BaseDirectory);
var report = BrowserAssetBuildPipeline.Run(context);

Console.WriteLine(
    $"Browser asset build completed. Atlases={report.GeneratedAtlasCount} Pages={report.GeneratedAtlasPageCount} " +
    $"Sprites={report.GeneratedSpriteCount} Warnings={report.Warnings.Count} Output={context.BrowserOutputRoot}");
return 0;
