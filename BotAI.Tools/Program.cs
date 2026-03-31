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
    Console.Error.WriteLine("usage: dotnet run --project BotAI.Tools [--map MapName] [--profile light|standard|heavy|all] [--class scout|soldier|...|all] [--output Path] [--include-custom]");
    return 1;
}

if (!options.IsValid(out var validationError))
{
    Console.Error.WriteLine(validationError);
    Console.Error.WriteLine("usage: dotnet run --project BotAI.Tools [--map MapName] [--profile light|standard|heavy|all] [--class scout|soldier|...|all] [--output Path] [--include-custom]");
    return 1;
}

var sourceContentRoot = ProjectSourceLocator.FindDirectory("Core/Content") ?? ContentRoot.Path;
ContentRoot.Initialize(sourceContentRoot);

var outputDirectory = options.OutputDirectory
    ?? ProjectSourceLocator.FindDirectory("Core/Content/BotNav")
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
        foreach (var classId in options.ResolveClasses())
        {
            var asset = BotNavigationAssetBuilder.Build(level, classId, fingerprint);
            var validation = BotNavigationAssetValidator.Validate(level, asset);
            BotNavigationAssetStore.SaveShipped(asset, outputDirectory);
            totalAssets += 1;
            invalidAssets += validation.IsStructurallyValid ? 0 : 1;
            Console.WriteLine(
                $"built map={entry.Name} area={areaIndex} class={BotNavigationClasses.GetFileToken(classId)} profile={BotNavigationProfiles.GetFileToken(asset.Profile)} strategy={asset.BuildStrategy} nodes={asset.Nodes.Count} edges={asset.Edges.Count} ms={asset.Stats.BuildMilliseconds:F2} phases=sample:{asset.Stats.SurfaceSamplingMilliseconds:F1},anchors:{asset.Stats.AutoAnchorMilliseconds:F1},hints:{asset.Stats.HintNodeMilliseconds:F1},auto-edges:{asset.Stats.AutomaticEdgeMilliseconds:F1},hint-edges:{asset.Stats.HintEdgeMilliseconds:F1},drops:{asset.Stats.DropEdgeMilliseconds:F1} nav={(validation.IsStructurallyValid ? "ok" : $"invalid:{validation.Issues.Count}")}");
            if (!validation.IsStructurallyValid)
            {
                Console.WriteLine($"  issues: {validation.BuildSummary()}");
            }
        }
    }
}

Console.WriteLine($"done assets={totalAssets} invalid={invalidAssets} output={outputDirectory}");
return 0;

internal sealed class NavBuildOptions
{
    public HashSet<string> MapNames { get; } = new(StringComparer.OrdinalIgnoreCase);

    public List<BotNavigationProfile> Profiles { get; } = new(BotNavigationProfiles.All);

    public HashSet<PlayerClass> Classes { get; } = new();

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

            if (arg.Equals("--class", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                foreach (var classId in ParseClasses(args[++index]))
                {
                    options.Classes.Add(classId);
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

    public IReadOnlyList<PlayerClass> ResolveClasses()
    {
        IEnumerable<PlayerClass> requestedClasses = Classes.Count > 0
            ? Classes
            : BotNavigationClasses.All;

        return requestedClasses
            .Where(classId => Profiles.Contains(BotNavigationProfiles.GetProfileForClass(classId)))
            .Distinct()
            .ToArray();
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

    private static IEnumerable<PlayerClass> ParseClasses(string value)
    {
        foreach (var token in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (token.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var classId in BotNavigationClasses.All)
                {
                    yield return classId;
                }

                yield break;
            }

            yield return token.ToLowerInvariant() switch
            {
                "scout" => PlayerClass.Scout,
                "engineer" => PlayerClass.Engineer,
                "pyro" => PlayerClass.Pyro,
                "soldier" => PlayerClass.Soldier,
                "demoman" => PlayerClass.Demoman,
                "demo" => PlayerClass.Demoman,
                "heavy" => PlayerClass.Heavy,
                "sniper" => PlayerClass.Sniper,
                "medic" => PlayerClass.Medic,
                "spy" => PlayerClass.Spy,
                "quote" => PlayerClass.Quote,
                _ => throw new ArgumentException($"Unknown class token '{token}'."),
            };
        }
    }
}
