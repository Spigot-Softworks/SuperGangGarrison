using System.Collections;
using System.Reflection;
using OpenGarrison.BotAI;
using OpenGarrison.Core;

ContentRoot.Initialize(Path.Combine(Environment.CurrentDirectory, "Core", "Content"));

var mapName = args.Length > 0 ? args[0] : "Corinth";
var goalId = args.Length > 1 ? int.Parse(args[1]) : 18;
var nodeIds = args.Length > 2
    ? args.Skip(2).Select(int.Parse).ToArray()
    : [81, 96, 105, 31, 30];

var level = SimpleLevelFactory.CreateImportedLevel(mapName) ?? throw new InvalidOperationException($"failed to load {mapName}");
var controller = new ModernPracticeBotController();
var getNav = typeof(ModernPracticeBotController).GetMethod("GetOrCreateClientBotNavPoints", BindingFlags.NonPublic | BindingFlags.Instance)
    ?? throw new MissingMethodException("GetOrCreateClientBotNavPoints");
var nav = getNav.Invoke(controller, [level]) ?? throw new InvalidOperationException("nav loader returned null");
var navType = nav.GetType();

var tryGetPoint = navType.GetMethod("TryGetPoint", BindingFlags.Public | BindingFlags.Instance)
    ?? throw new MissingMethodException("TryGetPoint");
var tryGetOutgoing = navType.GetMethod("TryGetOutgoingConnections", BindingFlags.Public | BindingFlags.Instance)
    ?? throw new MissingMethodException("TryGetOutgoingConnections");
var isReverseBlocked = navType.GetMethod("IsReverseBlocked", BindingFlags.Public | BindingFlags.Instance)
    ?? throw new MissingMethodException("IsReverseBlocked");
var getGoalWeights = navType.GetMethod("GetGoalWeights", BindingFlags.Public | BindingFlags.Instance)
    ?? throw new MissingMethodException("GetGoalWeights");
var cacheKey = navType.GetProperty("CacheKey", BindingFlags.Public | BindingFlags.Instance)?.GetValue(nav);

var baseWeights = (int[]?)getGoalWeights.Invoke(nav, [goalId, 130, null]);
var expandedWeights = (int[]?)getGoalWeights.Invoke(nav, [goalId, 10_000, null]);

Console.WriteLine($"map={mapName} cacheKey={cacheKey} goal={goalId}");
Console.WriteLine($"weights130={(baseWeights is null ? "null" : baseWeights.Length)} weightsExpanded={(expandedWeights is null ? "null" : expandedWeights.Length)}");

foreach (var nodeId in nodeIds)
{
    var pointArgs = new object?[] { nodeId, null };
    if (!(bool)(tryGetPoint.Invoke(nav, pointArgs) ?? false))
    {
        Console.WriteLine($"node {nodeId}: missing");
        continue;
    }

    var point = pointArgs[1] ?? throw new InvalidOperationException($"missing point payload {nodeId}");
    var pointType = point.GetType();
    var x = pointType.GetProperty("X")!.GetValue(point);
    var y = pointType.GetProperty("Y")!.GetValue(point);
    var weight130 = baseWeights is null || nodeId >= baseWeights.Length ? -1 : baseWeights[nodeId];
    var weightExpanded = expandedWeights is null || nodeId >= expandedWeights.Length ? -1 : expandedWeights[nodeId];
    Console.WriteLine($"node {nodeId} pos=({x},{y}) w130={weight130} wAll={weightExpanded}");

    var outgoingArgs = new object?[] { nodeId, null };
    if (!(bool)(tryGetOutgoing.Invoke(nav, outgoingArgs) ?? false))
    {
        Console.WriteLine("  outgoing: none");
        continue;
    }

    foreach (var edge in (IEnumerable)(outgoingArgs[1] ?? Array.Empty<object>()))
    {
        var edgeType = edge.GetType();
        var toNodeId = (int)(edgeType.GetProperty("ToNodeId")!.GetValue(edge) ?? -1);
        var targetArgs = new object?[] { toNodeId, null };
        _ = (bool)(tryGetPoint.Invoke(nav, targetArgs) ?? false);
        var target = targetArgs[1]!;
        var targetType = target.GetType();
        var toX = targetType.GetProperty("X")!.GetValue(target);
        var toY = targetType.GetProperty("Y")!.GetValue(target);
        var toWeight130 = baseWeights is null || toNodeId >= baseWeights.Length ? -1 : baseWeights[toNodeId];
        var toWeightExpanded = expandedWeights is null || toNodeId >= expandedWeights.Length ? -1 : expandedWeights[toNodeId];
        var reverseBlocked = (bool)(isReverseBlocked.Invoke(nav, [toNodeId, nodeId]) ?? false);
        Console.WriteLine($"  -> {toNodeId} pos=({toX},{toY}) w130={toWeight130} wAll={toWeightExpanded} reverseBlockedFromCurrent={reverseBlocked}");
    }
}
