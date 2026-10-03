using System.Reflection;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class SimulationPerformanceLineOfSightTests
{
    [Fact]
    public void PlayerAndSentryLineOfSightMatchFullSolidScansOnSparseMaps()
    {
        var solids = CreateSparseSolids();
        var world = CreateWorld(solids);
        var attacker = AddNetworkPlayer(world, 2, PlayerTeam.Red, 0f, 0f);
        var target = AddNetworkPlayer(world, 3, PlayerTeam.Blue, 100f, 100f);
        var random = new Random(0x5EED);

        for (var index = 0; index < 160; index += 1)
        {
            var originX = random.NextSingle() * 8_000f;
            var originY = random.NextSingle() * 8_000f;
            var targetX = random.NextSingle() * 8_000f;
            var targetY = random.NextSingle() * 8_000f;
            attacker.TeleportTo(originX, originY);
            target.TeleportTo(targetX, targetY);
            var sentry = new SentryEntity(10_000 + index, attacker.Id, PlayerTeam.Red, originX, originY, 1f);

            Assert.Equal(
                HasFullScanLineOfSight(solids, originX, originY, target.X, target.Y - (target.Height / 4f)),
                world.CombatTestHasLineOfSight(attacker, target));
            Assert.Equal(
                HasFullScanLineOfSight(solids, originX, originY, targetX, targetY),
                HasSentryLineOfSight(world, sentry, target));
        }
    }

    [Fact]
    public void SolidSpatialIndexReturnsOnlyCandidatesAlongTheQueriedRay()
    {
        var world = CreateWorld(CreateSparseSolids());
        var resolver = world.GeometryResolver;
        var candidateMethod = resolver.GetType().GetMethod(
            "GetPotentialSolidRaycastCandidates",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(candidateMethod);

        var candidates = (IReadOnlyList<LevelSolid>)candidateMethod!.Invoke(
            resolver,
            [new RectangleHitbox(0f, 120f, 8_192f, 136f)])!;

        Assert.InRange(candidates.Count, 1, world.Level.Solids.Count / 8);
    }

    private static IReadOnlyList<LevelSolid> CreateSparseSolids()
    {
        var solids = new List<LevelSolid>();
        for (var y = 0; y < 32; y += 1)
        {
            for (var x = 0; x < 32; x += 1)
            {
                solids.Add(new LevelSolid((x * 256f) + 96f, (y * 256f) + 96f, 32f, 32f));
            }
        }

        return solids;
    }

    private static SimulationWorld CreateWorld(IReadOnlyList<LevelSolid> solids)
    {
        var world = new SimulationWorld(new SimulationConfig
        {
            EnableLocalDummies = false,
            EnableEnemyTrainingDummy = false,
            EnableFriendlySupportDummy = false,
        });
        world.CombatTestSetLevel(new SimpleLevel(
            name: "simulation_performance_los",
            mode: GameModeKind.CaptureTheFlag,
            bounds: new WorldBounds(8_192f, 8_192f),
            mapScale: 1f,
            backgroundAssetName: null,
            mapAreaIndex: 1,
            mapAreaCount: 1,
            localSpawn: new SpawnPoint(32f, 32f),
            redSpawns: [new SpawnPoint(32f, 32f)],
            blueSpawns: [new SpawnPoint(8_000f, 8_000f)],
            intelBases: [],
            roomObjects: [],
            floorY: 8_192f,
            solids: solids,
            importedFromSource: false));
        return world;
    }

    private static PlayerEntity AddNetworkPlayer(
        SimulationWorld world,
        byte slot,
        PlayerTeam team,
        float x,
        float y)
    {
        Assert.True(world.NetworkPlayerRules.TryPrepareNetworkPlayerJoin(slot));
        Assert.True(world.NetworkPlayerRules.TrySetNetworkPlayerTeam(slot, team));
        Assert.True(world.NetworkPlayerRules.TryApplyNetworkPlayerClassSelection(slot, PlayerClass.Scout));

        Assert.True(world.NetworkPlayerRules.TryGetNetworkPlayer(slot, out var player));
        player.TeleportTo(x, y);
        player.RestoreMovementProbeState(isGrounded: true, remainingAirJumps: null, facingDirectionX: 1f);
        return player;
    }

    private static bool HasSentryLineOfSight(SimulationWorld world, SentryEntity sentry, PlayerEntity target)
    {
        return world.GeometryResolver.HasSentryLineOfSight(sentry, target);
    }

    private static bool HasFullScanLineOfSight(
        IReadOnlyList<LevelSolid> solids,
        float originX,
        float originY,
        float targetX,
        float targetY)
    {
        var deltaX = targetX - originX;
        var deltaY = targetY - originY;
        var distance = MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
        if (distance <= 0.0001f)
        {
            return true;
        }

        var directionX = deltaX / distance;
        var directionY = deltaY / distance;
        foreach (var solid in solids)
        {
            if (RayIntersectsRectangle(
                    originX,
                    originY,
                    directionX,
                    directionY,
                    solid.Left,
                    solid.Top,
                    solid.Right,
                    solid.Bottom,
                    distance))
            {
                return false;
            }
        }

        return true;
    }

    private static bool RayIntersectsRectangle(
        float originX,
        float originY,
        float directionX,
        float directionY,
        float left,
        float top,
        float right,
        float bottom,
        float maxDistance)
    {
        const float epsilon = 0.0001f;
        var tMin = float.NegativeInfinity;
        var tMax = float.PositiveInfinity;

        if (MathF.Abs(directionX) < epsilon)
        {
            if (originX < left || originX > right)
            {
                return false;
            }
        }
        else
        {
            var first = (left - originX) / directionX;
            var second = (right - originX) / directionX;
            tMin = MathF.Max(tMin, MathF.Min(first, second));
            tMax = MathF.Min(tMax, MathF.Max(first, second));
        }

        if (MathF.Abs(directionY) < epsilon)
        {
            if (originY < top || originY > bottom)
            {
                return false;
            }
        }
        else
        {
            var first = (top - originY) / directionY;
            var second = (bottom - originY) / directionY;
            tMin = MathF.Max(tMin, MathF.Min(first, second));
            tMax = MathF.Min(tMax, MathF.Max(first, second));
        }

        if (tMax < 0f || tMin > tMax)
        {
            return false;
        }

        return tMin < 0f || tMin <= maxDistance;
    }
}
