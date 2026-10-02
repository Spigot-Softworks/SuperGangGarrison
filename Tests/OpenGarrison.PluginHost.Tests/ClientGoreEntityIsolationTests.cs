using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using OpenGarrison.Client;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class ClientGoreEntityIsolationTests
{
    private const BindingFlags InstanceAll = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SnapshotGibsDamageBloodAndGibTrailsUseUniqueClientLocalIds(bool clientPredictionMode)
    {
        var world = new SimulationWorld { ClientPredictionMode = clientPredictionMode };
        var local = SimulationWorldSnapshotPresentationTests.CreatePlayerState(
            1, 101, "Local", PlayerTeam.Red, PlayerClass.Scout, isAlive: true, gibDeaths: 0);
        var remote = SimulationWorldSnapshotPresentationTests.CreatePlayerState(
            2, 202, "Remote", PlayerTeam.Blue, PlayerClass.Soldier, isAlive: true, gibDeaths: 0);
        var snapshot = SimulationWorldSnapshotPresentationTests.CreateSnapshot(world, 80, local, remote) with
        {
            GibSpawnEvents =
            [
                CreateGibSpawnEvent(1, 64f, 96f),
                CreateGibSpawnEvent(2, 80f, 96f, bloodChance: 0f),
            ],
        };

        Assert.True(world.ApplySnapshot(snapshot, localPlayerSlot: 1));
        var eventGibs = world.PlayerGibs.ToArray();
        Assert.Equal(2, eventGibs.Length);
        Assert.All(eventGibs, gib => Assert.True(gib.Id < 0));
        Assert.Equal(eventGibs.Length, eventGibs.Select(gib => gib.Id).Distinct().Count());

        world.SpawnClientBloodFromDamage(128f, 96f, damageAmount: 12);
        var damageBlood = world.BloodDrops.ToArray();
        Assert.Equal(3, damageBlood.Length);
        Assert.All(damageBlood, blood => Assert.True(blood.Id < 0));

        // This snapshot gib has a small blood-chance divisor and nonzero speed,
        // so it emits a trail drop on the next simulation step.
        world.PlayerRemains.AdvancePlayerGibs();
        var gibTrailBlood = Assert.Single(world.BloodDrops.Except(damageBlood));
        Assert.True(gibTrailBlood.Id < 0);

        var allLocalEffectIds = eventGibs.Select(gib => gib.Id)
            .Concat(damageBlood.Select(blood => blood.Id))
            .Append(gibTrailBlood.Id)
            .ToArray();
        Assert.Equal(allLocalEffectIds.Length, allLocalEffectIds.Distinct().Count());
        foreach (var gib in eventGibs)
        {
            Assert.Same(gib, world.Entities[gib.Id]);
        }
        foreach (var blood in damageBlood.Append(gibTrailBlood))
        {
            Assert.Same(blood, world.Entities[blood.Id]);
        }
    }

    [Theory]
    [InlineData(false, "bullet")]
    [InlineData(false, "rocket")]
    [InlineData(true, "bullet")]
    [InlineData(true, "rocket")]
    public void OnlineDeathGoreCannotReplaceOrExpirePositiveProjectileId203(bool clientPredictionMode, string projectileKind)
    {
        var world = new SimulationWorld { ClientPredictionMode = clientPredictionMode };
        var local = SimulationWorldSnapshotPresentationTests.CreatePlayerState(
            1, 101, "Local", PlayerTeam.Red, PlayerClass.Scout, isAlive: true, gibDeaths: 0);
        var remote = SimulationWorldSnapshotPresentationTests.CreatePlayerState(
            2, 202, "Remote", PlayerTeam.Blue, PlayerClass.Soldier, isAlive: true, gibDeaths: 0);
        var alive = SimulationWorldSnapshotPresentationTests.CreateSnapshot(world, 80, local, remote);
        Assert.True(world.ApplySnapshot(alive, localPlayerSlot: 1));

        var deadRemote = remote with { IsAlive = false, Health = 0, Deaths = 1, GibDeaths = 1 };
        var death = alive with { Frame = 81, Players = [local, deadRemote] };
        Assert.True(world.ApplySnapshot(death, localPlayerSlot: 1));

        var clientGibIds = world.PlayerGibs.Select(gib => gib.Id).ToArray();
        var clientBloodIds = world.BloodDrops.Select(blood => blood.Id).ToArray();
        Assert.NotEmpty(clientGibIds);
        Assert.NotEmpty(clientBloodIds);
        Assert.All(clientGibIds.Concat(clientBloodIds), id => Assert.True(id < 0));

        var projectileSnapshot = projectileKind == "bullet"
            ? death with
            {
                Frame = 82,
                Shots = [new SnapshotShotState(203, (byte)PlayerTeam.Blue, 202, 900f, 400f, 12f, 0f, 20)],
            }
            : death with
            {
                Frame = 82,
                Rockets = [new SnapshotRocketState(203, (byte)PlayerTeam.Blue, 202, 900f, 400f, 890f, 400f, 0f, 10f, 30)],
            };
        Assert.True(world.ApplySnapshot(projectileSnapshot, localPlayerSlot: 1));

        var projectile = projectileKind == "bullet"
            ? (SimulationEntity)Assert.Single(world.Shots)
            : Assert.Single(world.Rockets);
        Assert.Equal(203, projectile.Id);
        Assert.Same(projectile, world.Entities[203]);
        Assert.DoesNotContain(203, clientGibIds);
        Assert.DoesNotContain(203, clientBloodIds);
        Assert.Empty(clientGibIds.Intersect(clientBloodIds));

        // Expiring the local gibs must not remove the authoritative projectile
        // that now occupies its own positive ID in the entity store.
        for (var tick = 0; tick < 251; tick += 1)
        {
            world.PlayerRemains.AdvancePlayerGibs();
        }

        Assert.Empty(world.PlayerGibs);
        Assert.Same(projectile, world.Entities[203]);
        if (projectile is ShotProjectileEntity)
        {
            Assert.Contains(world.Shots, shot => ReferenceEquals(shot, projectile));
        }
        else
        {
            Assert.Contains(world.Rockets, rocket => ReferenceEquals(rocket, projectile));
        }
    }

    [Fact]
    public void OfflinePlayerGibsKeepPositiveSimulationIds()
    {
        var world = new SimulationWorld();

        world.PlayerRemains.SpawnPlayerGibs(world.LocalPlayer);

        Assert.NotEmpty(world.PlayerGibs);
        Assert.All(world.PlayerGibs, gib => Assert.True(gib.Id > 0));
        Assert.All(world.PlayerGibs, gib => Assert.Same(gib, world.Entities[gib.Id]));
    }

    [Theory]
    [InlineData("gib")]
    [InlineData("blood")]
    public void ExpiringStaleGoreDoesNotRemoveEntityNowMappedAtSameId(string kind)
    {
        var world = new SimulationWorld();
        var staleGore = kind == "gib"
            ? (SimulationEntity)new PlayerGibEntity(
                id: -7,
                spriteName: "GibS",
                frameIndex: 0,
                x: 100f,
                y: 100f,
                velocityX: 0f,
                velocityY: 0f,
                rotationSpeedDegrees: 0f,
                horizontalFriction: 0.4f,
                rotationFriction: 0.5f,
                lifetimeTicks: 1,
                bloodChance: 0f)
            : new BloodDropEntity(-7, 100f, 100f, 0f, 0f, lifetimeTicks: 1);
        if (staleGore is PlayerGibEntity gib)
        {
            world.WorldObjects.PlayerGibs.Add(gib);
        }
        else
        {
            world.WorldObjects.BloodDrops.Add((BloodDropEntity)staleGore);
        }
        world.EntityStore.Add(staleGore);
        var replacement = new ShotProjectileEntity( -7, PlayerTeam.Blue, 202, 900f, 400f, 12f, 0f);
        world.EntityStore.Set(replacement.Id, replacement);

        if (kind == "gib")
        {
            world.PlayerRemains.AdvancePlayerGibs();
        }
        else
        {
            world.PlayerRemains.AdvanceBloodDrops();
        }

        Assert.Same(replacement, world.Entities[-7]);
    }

    [Fact]
    public void OnlineGibRenderingUsesItsOwnTickSamplesWhenProjectileHistoryHasSameNegativeId()
    {
        var world = new SimulationWorld();
        var gib = new PlayerGibEntity(
            id: -203,
            spriteName: "GibS",
            frameIndex: 0,
            x: 900f,
            y: 400f,
            velocityX: 2f,
            velocityY: 0f,
            rotationSpeedDegrees: 8f,
            horizontalFriction: 0.4f,
            rotationFriction: 0.5f,
            lifetimeTicks: 250);
        gib.Advance(world.Level, world.Bounds);

        var game = CreateHeadlessGame(world, connected: true);
        var simulator = new FixedStepSimulator(world);
        Assert.Equal(0, simulator.Step(world.Config.FixedDeltaSeconds * 0.5d));
        SetField(typeof(Game1), game, "_simulator", simulator);

        // This is the same projectile interpolation capture path used by online
        // snapshots, deliberately keyed to the gib's otherwise client-local ID.
        var capture = typeof(Game1).GetMethod("CaptureProjectileInterpolationTarget", InstanceAll);
        Assert.NotNull(capture);
        capture!.Invoke(game, [gib.Id, 202, 700f, 300f, Vector2.Zero, 24f, 0d, null]);
        capture.Invoke(game, [gib.Id, 202, 800f, 300f, Vector2.Zero, 24f, 1d, null]);
        var update = typeof(Game1).GetMethod("UpdateInterpolatedEntityPosition", InstanceAll);
        Assert.NotNull(update);
        update!.Invoke(game, [gib.Id, gib.X, gib.Y, 0.5d]);

        var histories = (Dictionary<int, List<Game1.EntitySnapshotSample>>)typeof(Game1)
            .GetField("_entitySnapshotHistories", InstanceAll)!
            .GetValue(game)!;
        Assert.Contains(gib.Id, histories.Keys);

        var drawPosition = Invoke<Vector2>(game, "GetPlayerGibRenderPosition", gib);
        var expectedGibPosition = Vector2.Lerp(
            new Vector2(gib.PreviousX, gib.PreviousY),
            new Vector2(gib.X, gib.Y),
            0.5f);
        Assert.Equal(expectedGibPosition, drawPosition);

        var drawRotation = Invoke<float>(game, "GetPlayerGibRenderRotationDegrees", gib);
        Assert.Equal(
            gib.PreviousRotationDegrees + ((gib.RotationDegrees - gib.PreviousRotationDegrees) * 0.5f),
            drawRotation);
    }

    private static SnapshotGibSpawnEvent CreateGibSpawnEvent(ulong eventId, float x, float y, float bloodChance = 0.001f)
        => new(
            "GibS",
            FrameIndex: 0,
            x,
            y,
            VelocityX: 10f,
            VelocityY: 0f,
            RotationSpeedDegrees: 8f,
            HorizontalFriction: 0.4f,
            RotationFriction: 0.5f,
            LifetimeTicks: 250,
            BloodChance: bloodChance,
            EventId: eventId);

    private static Game1 CreateHeadlessGame(SimulationWorld world, bool connected)
    {
        var game = (Game1)RuntimeHelpers.GetUninitializedObject(typeof(Game1));
        foreach (var field in typeof(Game1).GetFields(InstanceAll))
        {
            if (!field.FieldType.IsGenericType)
            {
                continue;
            }

            var definition = field.FieldType.GetGenericTypeDefinition();
            if (definition == typeof(Dictionary<,>)
                || definition == typeof(List<>)
                || definition == typeof(HashSet<>)
                || definition == typeof(Queue<>))
            {
                field.SetValue(game, Activator.CreateInstance(field.FieldType));
            }
        }

        SetField(typeof(Game1), game, "_world", world);
        SetField(typeof(Game1), game, "_config", world.Config);
        var sessionState = typeof(Game1).GetField("_gameplaySessionState", InstanceAll)!;
        sessionState.SetValue(game, Activator.CreateInstance(sessionState.FieldType));
        var services = new ClientServiceContainer();
        SetField(typeof(Game1), game, "_services", services);
        services.Register(new GameplayManager(game));

        var network = new NetworkGameClient();
        if (connected)
        {
            typeof(NetworkGameClient).GetField("_transport", InstanceAll)!
                .SetValue(network, new FakeTransport());
        }
        SetField(typeof(Game1), game, "_networkClient", network);
        return game;
    }

    private static void SetField(Type type, object instance, string name, object value)
        => type.GetField(name, InstanceAll)!.SetValue(instance, value);

    private static T Invoke<T>(object instance, string methodName, params object[] arguments)
        => (T)typeof(Game1).GetMethod(methodName, InstanceAll)!.Invoke(instance, arguments)!;

    private sealed class FakeTransport : INetworkClientMessageTransport
    {
        public bool HasPendingMessages => false;
        public bool IsLoopbackConnection => false;
        public string RemoteDescription => "gore-regression-test";
        public bool TryReceive(out byte[] payload) { payload = []; return false; }
        public bool TryConsumeDisconnectReason(out string reason) { reason = string.Empty; return false; }
        public void Send(byte[] payload) { }
        public void Dispose() { }
    }
}
