using OpenGarrison.BotAI;
using OpenGarrison.Core;

NavBuildOptions options;
try
{
    options = NavBuildOptions.Parse(args);
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine(ex.Message);
    Console.Error.WriteLine("usage: dotnet run --project BotAI.Tools [--map MapName] [--output Path] [--include-custom]");
    return 1;
}

if (!NavBuildOptions.IsValid(out var validationError))
{
    Console.Error.WriteLine(validationError);
    Console.Error.WriteLine("usage: dotnet run --project BotAI.Tools [--map MapName] [--output Path] [--include-custom]");
    return 1;
}

var sourceContentRoot = ProjectSourceLocator.FindDirectory("Core/Content") ?? ContentRoot.Path;
ContentRoot.Initialize(sourceContentRoot);

var outputDirectory = options.OutputDirectory
    ?? ProjectSourceLocator.FindDirectory("Core/Content/BotNav")
    ?? Path.Combine(sourceContentRoot, "BotNav");
Directory.CreateDirectory(outputDirectory);

var catalog = SimpleLevelFactory.GetAvailableSourceLevels()
    .Where(entry => options.IncludeCustomMaps || !IsCustomMapEntry(entry))
    .Where(entry => options.MapNames.Count == 0 || options.MapNames.Contains(entry.Name))
    .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
    .ToArray();

if (catalog.Length == 0)
{
    Console.Error.WriteLine("No maps matched the requested filters.");
    return 2;
}

var totalAssets = 0;
var invalidAssets = 0;
foreach (var entry in catalog)
{
    var baseLevel = SimpleLevelFactory.CreateImportedLevel(entry.Name);
    if (baseLevel is null)
    {
        Console.Error.WriteLine($"Failed to import map {entry.Name}.");
        continue;
    }

    for (var areaIndex = 1; areaIndex <= baseLevel.MapAreaCount; areaIndex += 1)
    {
        var level = areaIndex == 1 ? baseLevel : SimpleLevelFactory.CreateImportedLevel(entry.Name, areaIndex);
        if (level is null)
        {
            Console.Error.WriteLine($"Failed to import map {entry.Name} area {areaIndex}.");
            continue;
        }

        var fingerprint = BotNavigationLevelFingerprint.Compute(level);
        DeleteLegacyClassAssets(outputDirectory, level.Name, level.MapAreaIndex);
        DeleteLegacyProfileAssets(outputDirectory, level.Name, level.MapAreaIndex);

        var asset = BotNavigationModernPointGraphBuilder.Build(level, fingerprint);
        var validation = BotNavigationAssetValidator.Validate(level, asset);
        BotNavigationAssetStore.SaveShipped(asset, outputDirectory);
        totalAssets += 1;
        invalidAssets += validation.IsStructurallyValid ? 0 : 1;
        var classTokens = string.Join("/", BotNavigationClasses.All.Select(BotNavigationClasses.GetFileToken));
        Console.WriteLine(
            $"built map={entry.Name} area={areaIndex} mesh=modern classes={classTokens} strategy={asset.BuildStrategy} nodes={asset.Nodes.Count} edges={asset.Edges.Count} ms={asset.Stats.BuildMilliseconds:F2} phases=sample:{asset.Stats.SurfaceSamplingMilliseconds:F1},anchors:{asset.Stats.AutoAnchorMilliseconds:F1},hints:{asset.Stats.HintNodeMilliseconds:F1},auto-edges:{asset.Stats.AutomaticEdgeMilliseconds:F1},hint-edges:{asset.Stats.HintEdgeMilliseconds:F1},drops:{asset.Stats.DropEdgeMilliseconds:F1} nav={(validation.IsStructurallyValid ? "ok" : $"invalid:{validation.Issues.Count}")}");
        if (!validation.IsStructurallyValid)
        {
            Console.WriteLine($"  issues: {validation.BuildSummary()}");
        }
    }
}

Console.WriteLine($"done assets={totalAssets} invalid={invalidAssets} output={outputDirectory}");
return 0;

static bool IsCustomMapEntry(SimpleLevelFactory.LevelCatalogEntry entry)
{
    if (!entry.RoomSourcePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    var parentDirectoryName = Path.GetFileName(Path.GetDirectoryName(entry.RoomSourcePath));
    return string.Equals(parentDirectoryName, "Maps", StringComparison.OrdinalIgnoreCase);
}

static void DeleteLegacyClassAssets(string outputDirectory, string levelName, int mapAreaIndex)
{
    foreach (var classId in BotNavigationClasses.All)
    {
        var legacyPath = Path.Combine(outputDirectory, BotNavigationAssetStore.GetAssetFileName(levelName, mapAreaIndex, classId));
        if (File.Exists(legacyPath))
        {
            File.Delete(legacyPath);
        }
    }
}

static void DeleteLegacyProfileAssets(string outputDirectory, string levelName, int mapAreaIndex)
{
    foreach (var profile in BotNavigationProfiles.All)
    {
        var legacyPath = Path.Combine(outputDirectory, BotNavigationAssetStore.GetLegacyAssetFileName(levelName, mapAreaIndex, profile));
        if (File.Exists(legacyPath))
        {
            File.Delete(legacyPath);
        }
    }
}

internal sealed class NavBuildOptions
{
    public HashSet<string> MapNames { get; } = new(StringComparer.OrdinalIgnoreCase);

    public string? OutputDirectory { get; private set; }

    public bool IncludeCustomMaps { get; private set; }

    public static NavBuildOptions Parse(IReadOnlyList<string> args)
    {
        var options = new NavBuildOptions();
        for (var index = 0; index < args.Count; index += 1)
        {
            var arg = args[index];
            if (arg.Equals("--map", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                options.MapNames.Add(args[++index].Trim());
                continue;
            }

            if (arg.Equals("--output", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                options.OutputDirectory = args[++index].Trim();
                continue;
            }

            if (arg.Equals("--include-custom", StringComparison.OrdinalIgnoreCase))
            {
                options.IncludeCustomMaps = true;
            }
        }

        return options;
    }

    public static bool IsValid(out string message)
    {
        message = string.Empty;
        return true;
    }
}
