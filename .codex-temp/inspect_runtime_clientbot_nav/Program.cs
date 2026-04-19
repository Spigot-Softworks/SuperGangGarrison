using System.Collections;
using System.Reflection;
using OpenGarrison.BotAI;
using OpenGarrison.Core;

ContentRoot.Initialize(Path.Combine(Environment.CurrentDirectory, "Core", "Content"));
var mapName = args.Length > 0 ? args[0] : "Truefort";
var level = SimpleLevelFactory.CreateImportedLevel(mapName) ?? throw new InvalidOperationException($"failed to load {mapName}");
var resolverType = typeof(ModernPracticeBotController).Assembly.GetType("OpenGarrison.BotAI.OriginalClientBotNavMeshStore")
    ?? throw new MissingMethodException("OriginalClientBotNavMeshStore");
var tryResolveMeshPath = resolverType.GetMethod("TryResolveMeshPath", BindingFlags.Public | BindingFlags.Static)
    ?? throw new MissingMethodException("TryResolveMeshPath");
var loadOriginal = resolverType.GetMethod("Load", BindingFlags.Public | BindingFlags.Static)
    ?? throw new MissingMethodException("Load");
var resolveArgs = new object?[] { level, null };
var resolved = (bool)(tryResolveMeshPath.Invoke(null, resolveArgs) ?? false);
Console.WriteLine($"resolvedOriginal={resolved} path={resolveArgs[1]}");
var controller = new ModernPracticeBotController();
var method = typeof(ModernPracticeBotController).GetMethod("GetOrCreateClientBotNavPoints", BindingFlags.NonPublic | BindingFlags.Instance)
    ?? throw new MissingMethodException("GetOrCreateClientBotNavPoints");
var nav = method.Invoke(controller, [level]) ?? throw new InvalidOperationException("nav loader returned null");
var navType = nav.GetType();
var tryGetPoint = navType.GetMethod("TryGetPoint", BindingFlags.Public | BindingFlags.Instance)
    ?? throw new MissingMethodException("TryGetPoint");
var tryGetOutgoing = navType.GetMethod("TryGetOutgoingConnections", BindingFlags.Public | BindingFlags.Instance)
    ?? throw new MissingMethodException("TryGetOutgoingConnections");
var cacheKey = navType.GetProperty("CacheKey", BindingFlags.Public | BindingFlags.Instance)?.GetValue(nav);
Console.WriteLine($"map={mapName} cacheKey={cacheKey}");

foreach (var id in new[] { 167, 232, 233, 247 })
{
    var pointArgs = new object?[] { id, null };
    if (!(bool)(tryGetPoint.Invoke(nav, pointArgs) ?? false))
    {
        Console.WriteLine($"missing {id}");
        continue;
    }

    var point = pointArgs[1] ?? throw new InvalidOperationException($"missing point payload {id}");
    var pointType = point.GetType();
    Console.WriteLine($"point {id} x={pointType.GetProperty("X")!.GetValue(point)} y={pointType.GetProperty("Y")!.GetValue(point)}");
    var edgeArgs = new object?[] { id, null };
    if ((bool)(tryGetOutgoing.Invoke(nav, edgeArgs) ?? false))
    {
        foreach (var edge in (IEnumerable)(edgeArgs[1] ?? Array.Empty<object>()))
        {
            var edgeType = edge.GetType();
            Console.WriteLine($"  edge to={edgeType.GetProperty("ToNodeId")!.GetValue(edge)}");
        }
    }
}

if (resolved)
{
    Console.WriteLine("direct store load:");
    var directNav = loadOriginal.Invoke(null, [level, resolveArgs[1]]) ?? throw new InvalidOperationException("direct load failed");
    var directCacheKey = navType.GetProperty("CacheKey", BindingFlags.Public | BindingFlags.Instance)?.GetValue(directNav);
    Console.WriteLine($"directCacheKey={directCacheKey}");
    foreach (var id in new[] { 167, 232, 233, 247 })
    {
        var pointArgs = new object?[] { id, null };
        if (!(bool)(tryGetPoint.Invoke(directNav, pointArgs) ?? false))
        {
            Console.WriteLine($"missing direct {id}");
            continue;
        }

        var point = pointArgs[1] ?? throw new InvalidOperationException($"missing direct point payload {id}");
        var pointType = point.GetType();
        Console.WriteLine($"direct point {id} x={pointType.GetProperty("X")!.GetValue(point)} y={pointType.GetProperty("Y")!.GetValue(point)}");
    }
}
