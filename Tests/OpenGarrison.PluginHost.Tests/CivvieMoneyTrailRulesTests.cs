using OpenGarrison.Core;
using OpenGarrison.Protocol;
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
        for (var candidate = 0UL; candidate < 512; candidate += 1)
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
}
