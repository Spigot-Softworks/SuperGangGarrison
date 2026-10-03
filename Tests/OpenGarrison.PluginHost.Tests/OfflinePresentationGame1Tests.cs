using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using OpenGarrison.Client;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class OfflinePresentationGame1Tests
{
    [Fact]
    public void OfflineProjectilesBlendAcrossTheLatestTickBySimulatorPhaseAndCoverGrenades()
    {
        var (game, world) = CreateOfflineGame();
        var simulator = new FixedStepSimulator(world);
        SetField(game, "_simulator", simulator);
        var grenade = SpawnGrenade(world, world.LocalPlayer, 100f, 100f);
        grenade.ApplyNetworkState(
            130f,
            100f,
            0f,
            0f,
            isDestroyed: false,
            explosionDamage: grenade.ExplosionDamage,
            fuseTicksLeft: grenade.FuseTicksLeft);

        // At the start of the tick interval the projectile is drawn at its tick-start position.
        game.UpdateOfflineInterpolatedWorldState();
        Assert.Equal(new Vector2(100f, 100f), game._interpolatedEntityPositions[grenade.Id]);

        // Half a tick later (no new tick yet) it is drawn halfway along that tick.
        simulator.Step(1d / 60d);
        game.UpdateOfflineInterpolatedWorldState();
        Assert.Equal(new Vector2(115f, 100f), game._interpolatedEntityPositions[grenade.Id]);
    }

    private static (Game1 Game, SimulationWorld World) CreateOfflineGame()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var game = (Game1)RuntimeHelpers.GetUninitializedObject(typeof(Game1));
        // RuntimeHelpers skips Game1's field initializers, so the first presentation
        // update needs the state pod used by IsOfflineBotSessionActive.
        SetField(game, "_gameplaySessionState", new Game1.GameplaySessionState());
        var services = new ClientServiceContainer();
        typeof(Game1).GetField("_services", flags)!.SetValue(game, services);
        services.Register(new GameplayManager(game));

        var config = new SimulationConfig();
        SetField(game, "_config", config);
        var world = new SimulationWorld(config);
        SetField(game, "_world", world);
        SetField(game, "_networkClient", new NetworkGameClient());
        SetField(game, "_offlinePresentationController", new OfflinePresentationController());
        SetField(game, "_networkInterpolationClockSeconds", 0d);

        foreach (var collectionName in new[]
        {
            "_interpolatedEntityPositions",
            "_interpolatedIntelPositions",
            "_entityInterpolationTracks",
            "_intelInterpolationTracks",
            "_entitySnapshotHistories",
            "_entitySnapshotHistoryKinds",
            "_retainedRocketPresentationEntities",
            "_retainedFlarePresentationEntities",
            "_retainedProjectilePresentationSourceFrames",
            "_intelSnapshotHistories",
            "_remotePlayerSnapshotHistories",
            "_localProjectileLaunchOriginOffsets",
            "_activeInterpolatedEntityIds",
            "_staleInterpolatedEntityIds",
            "_snapshotStatesByFrame",
            "_snapshotStateFrameOrder",
            "_queuedAuthoritativeSnapshots",
            "_civvieUmbrellaShieldBlockObservationByPlayerId",
        })
        {
            var field = typeof(Game1).GetField(collectionName, flags);
            Assert.NotNull(field);
            field!.SetValue(game, Activator.CreateInstance(field.FieldType));
        }

        return (game, world);
    }

    private static GrenadeProjectileEntity SpawnGrenade(SimulationWorld world, PlayerEntity owner, float x, float y)
    {
        return world.TestSpawnGrenade(owner, x, y, 0f, 0f);
    }

    private static void SetField(Game1 game, string name, object value)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var field = typeof(Game1).GetField(name, flags);
        Assert.NotNull(field);
        field!.SetValue(game, value);
    }
}
