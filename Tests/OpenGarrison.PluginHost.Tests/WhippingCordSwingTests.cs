using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

[Collection(ContentRootTestGroup.Name)]
public sealed class WhippingCordSwingTests
{
    [Fact]
    public void HeldStrikeLatchesToTerrainAndReleasePullsTowardAnchor()
    {
        var world = CreateWorld(new LevelSolid(348f, 420f, 24f, 160f));
        var engineer = world.LocalPlayer;
        Assert.True(engineer.TrySelectGameplayPrimaryItem(WhippingCordCatalog.ItemId));
        Assert.NotNull(MeleeHitboxMaskCatalog.GetOrLoad(WhippingCordCatalog.MeleeHitboxSpriteName));

        var heldInput = default(PlayerInputSnapshot) with
        {
            FirePrimary = true,
            AimWorldX = 650f,
            AimWorldY = 500f,
        };
        world.SetLocalInput(heldInput);
        for (var tick = 0; tick < 3; tick += 1)
        {
            world.AdvanceOneTick();
        }

        Assert.True(engineer.IsWhippingCordLatched);
        Assert.InRange(engineer.WhippingCordAnchorX, 347f, 349f);
        var anchorX = engineer.WhippingCordAnchorX;
        var anchorY = engineer.WhippingCordAnchorY;
        var ropeLength = engineer.WhippingCordRopeLength;
        world.SetLocalInput(heldInput with { AimWorldX = 150f, AimWorldY = 750f });
        for (var tick = 0; tick < 5; tick += 1)
        {
            world.AdvanceOneTick();
            Assert.True(engineer.IsWhippingCordLatched);
            Assert.Equal(anchorX, engineer.WhippingCordAnchorX);
            Assert.Equal(anchorY, engineer.WhippingCordAnchorY);
            Assert.True(Distance(engineer.X, engineer.Y, anchorX, anchorY)
                <= ropeLength + 0.25f);
        }

        engineer.ApplyVelocityImpulse(-90f, 30f);
        var speedBeforeRelease = engineer.HorizontalSpeed;
        world.SetLocalInput(heldInput with { FirePrimary = false });
        world.AdvanceOneTick();

        Assert.False(engineer.IsWhippingCordLatched);
        Assert.True(engineer.IsWhippingCordBackswingActive);
        Assert.True(engineer.HorizontalSpeed > speedBeforeRelease);
        Assert.True(engineer.X > 300f);
    }

    [Fact]
    public void ReleasingBeforeTheStrikeDoesNotLatch()
    {
        var world = CreateWorld(new LevelSolid(348f, 420f, 24f, 160f));
        var engineer = world.LocalPlayer;
        Assert.True(engineer.TrySelectGameplayPrimaryItem(WhippingCordCatalog.ItemId));
        var input = default(PlayerInputSnapshot) with
        {
            FirePrimary = true,
            AimWorldX = 650f,
            AimWorldY = 500f,
        };
        world.SetLocalInput(input);
        world.AdvanceOneTick();
        world.SetLocalInput(input with { FirePrimary = false });
        for (var tick = 0; tick < 4; tick += 1)
        {
            world.AdvanceOneTick();
        }

        Assert.False(engineer.IsWhippingCordLatched);
    }

    [Fact]
    public void TerrainReleaseAddsPullToExistingMomentum()
    {
        var world = CreateWorld();
        var engineer = world.LocalPlayer;
        Assert.True(engineer.TrySelectGameplayPrimaryItem(WhippingCordCatalog.ItemId));
        engineer.LatchWhippingCord(engineer.X + 48f, engineer.Y - 36f, 60f);
        engineer.ApplyVelocityImpulse(-90f, 45f);

        Assert.True(engineer.ReleaseWhippingCordWithPull());
        Assert.False(engineer.IsWhippingCordLatched);
        Assert.True(engineer.IsWhippingCordBackswingActive);
        Assert.InRange(engineer.HorizontalSpeed, 101f, 103f);
        Assert.InRange(engineer.VerticalSpeed, -145f, -143f);
        Assert.False(engineer.ReleaseWhippingCordWithPull());
    }

    [Fact]
    public void TerrainReleaseAddsSmallLiftEvenWhenAnchorIsLevel()
    {
        var world = CreateWorld();
        var engineer = world.LocalPlayer;
        Assert.True(engineer.TrySelectGameplayPrimaryItem(WhippingCordCatalog.ItemId));
        engineer.LatchWhippingCord(engineer.X + 48f, engineer.Y, 48f);

        Assert.True(engineer.ReleaseWhippingCordWithPull());
        Assert.Equal(WhippingCordCatalog.TerrainReleasePullSpeedPerTick
            * LegacyMovementModel.SourceTicksPerSecond, engineer.HorizontalSpeed, 1);
        Assert.Equal(-WhippingCordCatalog.TerrainReleaseUpwardImpulsePerTick
            * LegacyMovementModel.SourceTicksPerSecond, engineer.VerticalSpeed, 1);
    }

    [Fact]
    public void NetworkedReleaseStartsBackswingWithoutRepeatingThePull()
    {
        var world = CreateWorld();
        var engineer = world.LocalPlayer;
        Assert.True(engineer.TrySelectGameplayPrimaryItem(WhippingCordCatalog.ItemId));
        engineer.LatchWhippingCord(engineer.X + 48f, engineer.Y - 36f, 60f);
        engineer.ApplyVelocityImpulse(102f, -99f);

        engineer.HydrateWhippingCordLatch(false, 0f, 0f, 0f);

        Assert.False(engineer.IsWhippingCordLatched);
        Assert.True(engineer.IsWhippingCordBackswingActive);
        Assert.Equal(102f, engineer.HorizontalSpeed);
        Assert.Equal(-99f, engineer.VerticalSpeed);
    }

    [Fact]
    public void BackswingPullsOnlyOneEnemyTowardEngineer()
    {
        var world = CreateWorld();
        var engineer = world.LocalPlayer;
        Assert.True(engineer.TrySelectGameplayPrimaryItem(WhippingCordCatalog.ItemId));
        var firstEnemy = AddEnemy(world, 2, 340f, 500f);
        var secondEnemy = AddEnemy(world, 3, 360f, 500f);
        var firstHealthBeforeStrike = firstEnemy.Health;
        var input = default(PlayerInputSnapshot) with
        {
            FirePrimary = true,
            AimWorldX = 650f,
            AimWorldY = 500f,
        };
        world.SetLocalInput(input);
        world.AdvanceOneTick();
        world.AdvanceOneTick();
        Assert.True(firstEnemy.Health < firstHealthBeforeStrike);
        var firstSpeedAfterStrike = firstEnemy.HorizontalSpeed;
        for (var tick = 0; tick < 3; tick += 1)
        {
            world.AdvanceOneTick();
            Assert.Equal(firstEnemy.Id, engineer.PendingWhippingCordBackswingTargetId);
        }

        // The whip has started returning before the caught enemy moves.
        Assert.True(firstEnemy.HorizontalSpeed > firstSpeedAfterStrike - 50f);
        var speedBeforePull = firstEnemy.HorizontalSpeed;
        var secondSpeedBeforePull = secondEnemy.HorizontalSpeed;
        world.AdvanceOneTick();
        Assert.True(firstEnemy.HorizontalSpeed < speedBeforePull);
        Assert.Equal(secondSpeedBeforePull, secondEnemy.HorizontalSpeed, 1);
        var firstSpeedAfterPull = firstEnemy.HorizontalSpeed;
        world.AdvanceOneTick();
        Assert.True(firstEnemy.HorizontalSpeed >= firstSpeedAfterPull - 20f);
    }

    private static SimulationWorld CreateWorld(params LevelSolid[] additionalSolids)
    {
        var world = new SimulationWorld();
        var redSpawn = new SpawnPoint(300f, 500f);
        var blueSpawn = new SpawnPoint(1700f, 500f);
        world.CombatTestSetLevel(new SimpleLevel(
            "whipping-cord-swing", GameModeKind.TeamDeathmatch,
            new WorldBounds(2048f, 2048f), 1f, null, 1, 1, redSpawn,
            [redSpawn], [blueSpawn], [], [], floorY: 1024f,
            [new LevelSolid(0f, 1024f, 2048f, 1024f), .. additionalSolids],
            importedFromSource: false));
        world.PrepareLocalPlayerJoin();
        world.SetLocalPlayerTeam(PlayerTeam.Red);
        world.CompleteLocalPlayerJoin(PlayerClass.Engineer);
        world.LocalPlayer.SetSpawnRoomState(false);
        world.SetLocalInput(default);
        world.SetLocalPreviousInput(default);
        return world;
    }

    private static PlayerEntity AddEnemy(SimulationWorld world, byte slot, float x, float y)
    {
        Assert.True(world.TryPrepareNetworkPlayerJoin(slot));
        Assert.True(world.TrySetNetworkPlayerTeam(slot, PlayerTeam.Blue));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(slot, PlayerClass.Scout));
        Assert.True(world.TryGetNetworkPlayer(slot, out var enemy));
        enemy.TeleportTo(x, y);
        return enemy;
    }

    private static float Distance(float x1, float y1, float x2, float y2)
        => MathF.Sqrt((x1 - x2) * (x1 - x2) + (y1 - y2) * (y1 - y2));
}
