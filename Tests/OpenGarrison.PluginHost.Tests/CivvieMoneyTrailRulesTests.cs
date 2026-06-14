using OpenGarrison.Core;
using OpenGarrison.Protocol;
using System.Reflection;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class CivvieMoneyTrailRulesTests
{
    [Fact]
    public void DeterministicSpawnChanceIsStableForSameFrameAndPlayer()
    {
        var first = CivvieMoneyTrailRules.ShouldEmitDeterministicSourceTickChance(
            frame: 120,
            playerId: 7,
            ticksPerSecond: 30,
            CivvieMoneyTrailRules.TrailSpawnChance);
        var second = CivvieMoneyTrailRules.ShouldEmitDeterministicSourceTickChance(
            frame: 120,
            playerId: 7,
            ticksPerSecond: 30,
            CivvieMoneyTrailRules.TrailSpawnChance);

        Assert.Equal(first, second);
    }

    [Fact]
    public void DeterministicVerticalOffsetIsStableForSameFrameAndPlayer()
    {
        var first = CivvieMoneyTrailRules.GetDeterministicVerticalOffset(frame: 64, playerId: 3);
        var second = CivvieMoneyTrailRules.GetDeterministicVerticalOffset(frame: 64, playerId: 3);

        Assert.Equal(first, second);
        Assert.InRange(first, 0f, CivvieMoneyTrailRules.TrailVerticalJitterSpan);
    }

    [Fact]
    public void IndependentTrackersProduceMatchingSpawnForSameFrame()
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false, TicksPerSecond = 30 });
        Assert.True(world.TryPrepareNetworkPlayerJoin(slot: 1));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(slot: 1, PlayerClass.Quote));
        Assert.True(world.TryGetNetworkPlayer(slot: 1, out var civilian));

        ulong frame = 0;
        for (var candidate = 0UL; candidate < 4096; candidate += 1)
        {
            if (CivvieMoneyTrailRules.ShouldEmitDeterministicSourceTickChance(
                    candidate,
                    civilian.Id,
                    ticksPerSecond: 30,
                    CivvieMoneyTrailRules.TrailSpawnChance))
            {
                frame = candidate;
                break;
            }
        }

        Assert.NotEqual(0UL, frame);

        for (var tick = 0; tick < 30; tick += 1)
        {
            Assert.True(world.TrySetNetworkPlayerInput(
                slot: 1,
                new PlayerInputSnapshot(
                    Left: false,
                    Right: true,
                    Up: false,
                    Down: false,
                    BuildSentry: false,
                    DestroySentry: false,
                    Taunt: false,
                    FirePrimary: false,
                    FireSecondary: false,
                    AimWorldX: civilian.X + 160f,
                    AimWorldY: civilian.Y - 20f,
                    DebugKill: false,
                    UseAbility: false)));
            world.AdvanceOneTick();
        }

        Assert.True(MathF.Abs(civilian.HorizontalSpeed) >= CivvieMoneyTrailRules.TrailMoveSpeedThreshold);

        var firstTracker = new CivvieMoneyTrailTracker();
        var secondTracker = new CivvieMoneyTrailTracker();
        firstTracker.TryRegisterTrail(frame, ticksPerSecond: 30, civilian);
        secondTracker.TryRegisterTrail(frame, ticksPerSecond: 30, civilian);

        var firstSpawn = Assert.Single(firstTracker.DrainPendingSpawns());
        var secondSpawn = Assert.Single(secondTracker.DrainPendingSpawns());

        Assert.Equal(firstSpawn, secondSpawn);

        var trailDirection = -MathF.Sign(civilian.HorizontalSpeed);
        var expectedX = civilian.X + (trailDirection * CivvieMoneyTrailRules.TrailHorizontalOffset);
        var expectedY = civilian.Y
            - CivvieMoneyTrailRules.TrailVerticalBaseOffset
            + CivvieMoneyTrailRules.GetDeterministicVerticalOffset(frame, civilian.Id);

        Assert.Equal(expectedX, firstSpawn.X, precision: 3);
        Assert.Equal(expectedY, firstSpawn.Y, precision: 3);
    }

    [Fact]
    public void TauntDoesNotBlockMoneyTrailWhilePogoing()
    {
        var player = CreateStandingCivilian();
        Assert.True(player.TryToggleCivviePogo());
        SetPlayerTaunting(player, isTaunting: true);

        var frame = FindDeterministicSpawnFrame(player.Id);
        var tracker = new CivvieMoneyTrailTracker();
        tracker.TryRegisterTrail(frame, ticksPerSecond: 30, player);

        Assert.NotEmpty(tracker.DrainPendingSpawns());
    }

    [Fact]
    public void PogoTrickBurstSpawnIsDeterministic()
    {
        var first = CivvieMoneyTrailRules.CreatePogoTrickBurstSpawn(
            frame: 500,
            playerId: 3,
            particleIndex: 2,
            centerX: 128f,
            centerY: 96f);
        var second = CivvieMoneyTrailRules.CreatePogoTrickBurstSpawn(
            frame: 500,
            playerId: 3,
            particleIndex: 2,
            centerX: 128f,
            centerY: 96f);

        Assert.Equal(first, second);
        Assert.InRange(
            MathF.Sqrt((first.VelocityX * first.VelocityX) + (first.VelocityY * first.VelocityY)),
            CivvieMoneyTrailRules.PogoTrickBurstSpeedMin * 0.9f,
            CivvieMoneyTrailRules.PogoTrickBurstSpeedMin + CivvieMoneyTrailRules.PogoTrickBurstSpeedSpan + 0.1f);
    }

    [Fact]
    public void PogoRegistersMoneyTrailWithoutHorizontalSpeed()
    {
        var player = CreateStandingCivilian();
        Assert.True(player.TryToggleCivviePogo());
        Assert.Equal(0f, player.HorizontalSpeed);

        var frame = FindDeterministicSpawnFrame(player.Id);
        var tracker = new CivvieMoneyTrailTracker();
        tracker.TryRegisterTrail(frame, ticksPerSecond: 30, player);

        var spawn = Assert.Single(tracker.DrainPendingSpawns());
        var expectedX = player.X + (player.FacingDirectionX * CivvieMoneyTrailRules.TrailHorizontalOffset);
        Assert.Equal(expectedX, spawn.X, precision: 3);
    }

    private static PlayerEntity CreateStandingCivilian()
    {
        var player = new PlayerEntity(7, CharacterClassCatalog.Civilian, "Test");
        player.Spawn(PlayerTeam.Red, 128f, 128f);
        player.TeleportTo(128f, 128f);
        return player;
    }

    private static void SetPlayerTaunting(PlayerEntity player, bool isTaunting)
    {
        typeof(PlayerEntity).GetProperty(
            nameof(PlayerEntity.IsTaunting),
            BindingFlags.Instance | BindingFlags.Public)!.SetValue(player, isTaunting);
    }

    private static ulong FindDeterministicSpawnFrame(int playerId)
    {
        for (var candidate = 0UL; candidate < 4096; candidate += 1)
        {
            if (CivvieMoneyTrailRules.ShouldEmitDeterministicSourceTickChance(
                    candidate,
                    playerId,
                    ticksPerSecond: 30,
                    CivvieMoneyTrailRules.TrailSpawnChance))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Expected a deterministic spawn frame within the search window.");
    }
}
