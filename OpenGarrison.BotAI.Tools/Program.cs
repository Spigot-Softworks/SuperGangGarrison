using OpenGarrison.BotAI;
using OpenGarrison.Core;

var options = NavBuildOptions.Parse(args);
if (!options.IsValid(out var validationError))
{
    Console.Error.WriteLine(validationError);
    Console.Error.WriteLine("usage: dotnet run --project OpenGarrison.BotAI.Tools [--map MapName] [--profile light|standard|heavy|all] [--output Path] [--include-custom]");
    return 1;
}

var sourceContentRoot = ProjectSourceLocator.FindDirectory("OpenGarrison.Core/Content") ?? ContentRoot.Path;
ContentRoot.Initialize(sourceContentRoot);

var outputDirectory = options.OutputDirectory
    ?? ProjectSourceLocator.FindDirectory("OpenGarrison.Core/Content/BotNav")
    ?? Path.Combine(sourceContentRoot, "BotNav");
Directory.CreateDirectory(outputDirectory);

var catalog = SimpleLevelFactory.GetAvailableSourceLevels()
    .Where(entry => options.IncludeCustomMaps || !entry.RoomSourcePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
    .Where(entry => options.MapNames.Count == 0 || options.MapNames.Contains(entry.Name))
    .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
    .ToArray();

if (catalog.Length == 0)
{
    Console.Error.WriteLine("No maps matched the requested filters.");
    return 2;
}

var totalAssets = 0;
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
        foreach (var profile in options.Profiles)
        {
            var asset = BotNavigationAssetBuilder.Build(level, profile, fingerprint);
            BotNavigationAssetStore.SaveShipped(asset, outputDirectory);
            totalAssets += 1;
            Console.WriteLine(
                $"built map={entry.Name} area={areaIndex} profile={BotNavigationProfiles.GetFileToken(profile)} nodes={asset.Nodes.Count} edges={asset.Edges.Count} ms={asset.Stats.BuildMilliseconds:F2}");
        }
    }
}

Console.WriteLine($"done assets={totalAssets} output={outputDirectory}");
return 0;

internal sealed class NavBuildOptions
{
    public HashSet<string> MapNames { get; } = new(StringComparer.OrdinalIgnoreCase);

    public List<BotNavigationProfile> Profiles { get; } = new(BotNavigationProfiles.All);

    public string? OutputDirectory { get; private set; }

    public bool IncludeCustomMaps { get; private set; }

    public static NavBuildOptions Parse(IReadOnlyList<string> args)
    {
        var options = new NavBuildOptions();
        var explicitProfiles = false;

        for (var index = 0; index < args.Count; index += 1)
        {
            var arg = args[index];
            if (arg.Equals("--map", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                options.MapNames.Add(args[++index].Trim());
                continue;
            }

            if (arg.Equals("--profile", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                if (!explicitProfiles)
                {
                    options.Profiles.Clear();
                    explicitProfiles = true;
                }

                foreach (var profile in ParseProfiles(args[++index]))
                {
                    if (!options.Profiles.Contains(profile))
                    {
                        options.Profiles.Add(profile);
                    }
                }

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

    public bool IsValid(out string message)
    {
        if (Profiles.Count == 0)
        {
            message = "At least one navigation profile must be selected.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private static IEnumerable<BotNavigationProfile> ParseProfiles(string value)
    {
        foreach (var token in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (token.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var profile in BotNavigationProfiles.All)
                {
                    yield return profile;
                }

                yield break;
            }

            if (token.Equals("light", StringComparison.OrdinalIgnoreCase))
            {
                yield return BotNavigationProfile.Light;
                continue;
            }

            if (token.Equals("heavy", StringComparison.OrdinalIgnoreCase))
            {
                yield return BotNavigationProfile.Heavy;
                continue;
            }

            yield return BotNavigationProfile.Standard;
        }
    }
}
