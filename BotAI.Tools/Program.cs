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
    Console.Error.WriteLine("usage: dotnet run --project BotAI.Tools [--map MapName] [--output Path] [--include-custom] [--audit-reachability] [--audit-shipped] [--repair-shipped] [--audit-capture-routes]");
    return 1;
}

if (!NavBuildOptions.IsValid(out var validationError))
{
    Console.Error.WriteLine(validationError);
    Console.Error.WriteLine("usage: dotnet run --project BotAI.Tools [--map MapName] [--output Path] [--include-custom] [--audit-reachability] [--audit-shipped] [--repair-shipped] [--audit-capture-routes]");
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
var reachabilityIssues = 0;
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
        if (options.AuditShipped)
        {
            totalAssets += 1;
            if (!BotNavigationAssetStore.TryLoadModernShippedAsset(level, out var shippedAsset, out var shippedPath, out var shippedMessage, out var shippedValidation))
            {
                invalidAssets += 1;
                Console.WriteLine($"shipped map={entry.Name} area={areaIndex} nav=missing path={shippedPath} message={shippedMessage}");
                continue;
            }

            var repairAddedEdges = 0;
            if (options.RepairShipped)
            {
                var repair = BotNavigationModernGraphRepairer.AddMissingClientBotEdges(level, shippedAsset!);
                shippedAsset = repair.Asset;
                repairAddedEdges = repair.AddedEdges;
                if (repairAddedEdges > 0)
                {
                    BotNavigationAssetStore.SaveShipped(shippedAsset, outputDirectory);
                    shippedValidation = BotNavigationAssetValidator.Validate(level, shippedAsset);
                }
            }

            var reachabilityAudit = options.AuditReachability
                ? BotNavigationAssetValidator.AuditAttackReachability(level, shippedAsset!)
                : BotNavigationValidationResult.Valid;
            if (options.AuditCaptureRoutes)
            {
                AuditCaptureRoutes(level, shippedAsset!);
            }
            invalidAssets += shippedValidation.IsStructurallyValid ? 0 : 1;
            reachabilityIssues += reachabilityAudit.Issues.Count;
            Console.WriteLine(
                $"shipped map={entry.Name} area={areaIndex} strategy={shippedAsset!.BuildStrategy} nodes={shippedAsset.Nodes.Count} edges={shippedAsset.Edges.Count} repairedEdges={repairAddedEdges} nav={(shippedValidation.IsStructurallyValid ? "ok" : $"invalid:{shippedValidation.Issues.Count}")} reachability={(reachabilityAudit.IsStructurallyValid ? "ok" : $"issues:{reachabilityAudit.Issues.Count}")} path={shippedPath}");
            if (!shippedValidation.IsStructurallyValid)
            {
                Console.WriteLine($"  issues: {shippedValidation.BuildSummary()}");
            }

            if (!reachabilityAudit.IsStructurallyValid)
            {
                foreach (var issue in reachabilityAudit.Issues)
                {
                    Console.WriteLine($"  reachability: {issue.Message}");
                }
            }

            continue;
        }

        DeleteLegacyClassAssets(outputDirectory, level.Name, level.MapAreaIndex);
        DeleteLegacyProfileAssets(outputDirectory, level.Name, level.MapAreaIndex);

        var asset = BotNavigationModernPointGraphBuilder.Build(level, fingerprint);
        var validation = BotNavigationAssetValidator.Validate(level, asset);
        var generatedReachabilityAudit = options.AuditReachability
            ? BotNavigationAssetValidator.AuditAttackReachability(level, asset)
            : BotNavigationValidationResult.Valid;
        if (options.AuditCaptureRoutes)
        {
            AuditCaptureRoutes(level, asset);
        }
        BotNavigationAssetStore.SaveShipped(asset, outputDirectory);
        totalAssets += 1;
        invalidAssets += validation.IsStructurallyValid ? 0 : 1;
        reachabilityIssues += generatedReachabilityAudit.Issues.Count;
        var classTokens = string.Join("/", BotNavigationClasses.All.Select(BotNavigationClasses.GetFileToken));
        Console.WriteLine(
            $"built map={entry.Name} area={areaIndex} mesh=modern classes={classTokens} strategy={asset.BuildStrategy} nodes={asset.Nodes.Count} edges={asset.Edges.Count} ms={asset.Stats.BuildMilliseconds:F2} phases=sample:{asset.Stats.SurfaceSamplingMilliseconds:F1},anchors:{asset.Stats.AutoAnchorMilliseconds:F1},hints:{asset.Stats.HintNodeMilliseconds:F1},auto-edges:{asset.Stats.AutomaticEdgeMilliseconds:F1},hint-edges:{asset.Stats.HintEdgeMilliseconds:F1},drops:{asset.Stats.DropEdgeMilliseconds:F1} nav={(validation.IsStructurallyValid ? "ok" : $"invalid:{validation.Issues.Count}")} reachability={(generatedReachabilityAudit.IsStructurallyValid ? "ok" : $"issues:{generatedReachabilityAudit.Issues.Count}")}");
        if (!validation.IsStructurallyValid)
        {
            Console.WriteLine($"  issues: {validation.BuildSummary()}");
        }

        if (!generatedReachabilityAudit.IsStructurallyValid)
        {
            foreach (var issue in generatedReachabilityAudit.Issues)
            {
                Console.WriteLine($"  reachability: {issue.Message}");
            }
        }
    }
}

Console.WriteLine($"done assets={totalAssets} invalid={invalidAssets} reachabilityIssues={reachabilityIssues} output={outputDirectory}");
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

static void AuditCaptureRoutes(SimpleLevel level, BotNavigationAsset asset)
{
    var captureZones = level.GetRoomObjects(RoomObjectType.CaptureZone).ToArray();
    var controlPoints = level.GetRoomObjects(RoomObjectType.ControlPoint).ToArray();
    if (captureZones.Length == 0 && controlPoints.Length == 0)
    {
        return;
    }

    var outgoing = asset.Edges
        .GroupBy(static edge => edge.FromNodeId)
        .ToDictionary(static group => group.Key, static group => group.Select(static edge => edge.ToNodeId).Distinct().ToArray());
    Console.WriteLine($"  capture markers cp={controlPoints.Length} zones={captureZones.Length}");
    for (var pointIndex = 0; pointIndex < controlPoints.Length; pointIndex += 1)
    {
        var point = controlPoints[pointIndex];
        Console.WriteLine($"  cp[{pointIndex}] {point.SourceName} center=({point.CenterX:F0},{point.CenterY:F0}) bounds=({point.Left:F0},{point.Top:F0},{point.Right:F0},{point.Bottom:F0})");
    }

    for (var zoneIndex = 0; zoneIndex < captureZones.Length; zoneIndex += 1)
    {
        var zone = captureZones[zoneIndex];
        var nearest = FindNearestNode(asset.Nodes, zone.CenterX, zone.CenterY, requireGroundSupport: false, maxDistance: 0f);
        var nearestGround = FindNearestNode(asset.Nodes, zone.CenterX, zone.CenterY, requireGroundSupport: true, maxDistance: 220f);
        var redRoute = FindShortestRouteLength(asset.Nodes, outgoing, level.RedSpawns, nearest?.Id);
        var blueRoute = FindShortestRouteLength(asset.Nodes, outgoing, level.BlueSpawns, nearest?.Id);
        Console.WriteLine(
            $"  zone[{zoneIndex}] center=({zone.CenterX:F0},{zone.CenterY:F0}) node={FormatNode(nearest)} ground={FormatNode(nearestGround)} redRoute={FormatRoute(redRoute)} blueRoute={FormatRoute(blueRoute)}");
    }
}

static BotNavigationNode? FindNearestNode(
    IReadOnlyList<BotNavigationNode> nodes,
    float x,
    float y,
    bool requireGroundSupport,
    float maxDistance)
{
    var bestDistanceSquared = maxDistance <= 0f ? float.PositiveInfinity : maxDistance * maxDistance;
    BotNavigationNode? best = null;
    for (var index = 0; index < nodes.Count; index += 1)
    {
        var node = nodes[index];
        if (requireGroundSupport && !node.RequiresGroundSupport)
        {
            continue;
        }

        var dx = node.X - x;
        var dy = node.Y - y;
        var distanceSquared = (dx * dx) + (dy * dy);
        if (distanceSquared > bestDistanceSquared)
        {
            continue;
        }

        bestDistanceSquared = distanceSquared;
        best = node;
    }

    return best;
}

static int? FindShortestRouteLength(
    IReadOnlyList<BotNavigationNode> nodes,
    IReadOnlyDictionary<int, int[]> outgoing,
    IReadOnlyList<SpawnPoint> spawns,
    int? goalNodeId)
{
    if (!goalNodeId.HasValue)
    {
        return null;
    }

    var best = int.MaxValue;
    for (var spawnIndex = 0; spawnIndex < spawns.Count; spawnIndex += 1)
    {
        var spawn = spawns[spawnIndex];
        var start = FindNearestNode(nodes, spawn.X, spawn.Y, requireGroundSupport: true, maxDistance: 220f);
        if (start is null)
        {
            continue;
        }

        var routeLength = FindRouteLength(outgoing, start.Id, goalNodeId.Value);
        if (routeLength.HasValue)
        {
            best = Math.Min(best, routeLength.Value);
        }
    }

    return best == int.MaxValue ? null : best;
}

static int? FindRouteLength(IReadOnlyDictionary<int, int[]> outgoing, int startNodeId, int goalNodeId)
{
    if (startNodeId == goalNodeId)
    {
        return 0;
    }

    var visited = new HashSet<int> { startNodeId };
    var queue = new Queue<(int NodeId, int Distance)>();
    queue.Enqueue((startNodeId, 0));
    while (queue.Count > 0)
    {
        var current = queue.Dequeue();
        if (!outgoing.TryGetValue(current.NodeId, out var neighbors))
        {
            continue;
        }

        for (var index = 0; index < neighbors.Length; index += 1)
        {
            var neighbor = neighbors[index];
            if (!visited.Add(neighbor))
            {
                continue;
            }

            var distance = current.Distance + 1;
            if (neighbor == goalNodeId)
            {
                return distance;
            }

            queue.Enqueue((neighbor, distance));
        }
    }

    return null;
}

static string FormatNode(BotNavigationNode? node)
{
    return node is not null
        ? $"{node.Id}@({node.X:F0},{node.Y:F0})"
        : "miss";
}

static string FormatRoute(int? routeLength)
{
    return routeLength.HasValue ? routeLength.Value.ToString() : "miss";
}

internal sealed class NavBuildOptions
{
    public HashSet<string> MapNames { get; } = new(StringComparer.OrdinalIgnoreCase);

    public string? OutputDirectory { get; private set; }

    public bool IncludeCustomMaps { get; private set; }

    public bool AuditReachability { get; private set; }

    public bool AuditShipped { get; private set; }

    public bool RepairShipped { get; private set; }

    public bool AuditCaptureRoutes { get; private set; }

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
                continue;
            }

            if (arg.Equals("--audit-reachability", StringComparison.OrdinalIgnoreCase))
            {
                options.AuditReachability = true;
                continue;
            }

            if (arg.Equals("--audit-shipped", StringComparison.OrdinalIgnoreCase))
            {
                options.AuditShipped = true;
                continue;
            }

            if (arg.Equals("--repair-shipped", StringComparison.OrdinalIgnoreCase))
            {
                options.AuditShipped = true;
                options.RepairShipped = true;
                continue;
            }

            if (arg.Equals("--audit-capture-routes", StringComparison.OrdinalIgnoreCase))
            {
                options.AuditCaptureRoutes = true;
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
