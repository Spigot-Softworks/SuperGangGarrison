using OpenGarrison.Core;

if (args.Length is not 3 and not 4)
{
    Console.Error.WriteLine("Usage: OpenGarrison.Tools.MapPackageBuilder <source-maps-directory> <stock-maps-directory> <destination-maps-directory> [--drop-unconverted-legacy-pngs]");
    return 2;
}

var dropUnconvertedLegacyPngs = args.Length == 4
    && args[3].Equals("--drop-unconverted-legacy-pngs", StringComparison.OrdinalIgnoreCase);
if (args.Length == 4 && !dropUnconvertedLegacyPngs)
{
    Console.Error.WriteLine($"Unknown option: {args[3]}");
    return 2;
}

var result = CustomMapPackageDirectoryPublisher.Publish(
    args[0],
    args[1],
    args[2],
    dropUnconvertedLegacyPngs: dropUnconvertedLegacyPngs);
Console.WriteLine(
    $"Published Maps: copied {result.CopiedTopLevelLegacyPngCount} legacy PNG(s), " +
    $"converted {result.ConvertedLegacyPngCount}, kept {result.KeptLegacyPngCount}, " +
    $"dropped {result.DroppedLegacyPngCount}, " +
    $"copied {result.CopiedStockPackageCount} stock package template(s).");
return 0;
