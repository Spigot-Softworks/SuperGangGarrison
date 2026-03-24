using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.Core.Tests;

public sealed class RocketProjectileEntityTests
{
    [Fact]
    public void AdvanceOneTick_NormalRocketKnockbackDecaysAtSourceIntervals()
    {
        var rocket = new RocketProjectileEntity(1001, PlayerTeam.Red, 1, 0f, 0f, 0f, 0f);

        AdvanceTicks(rocket, 21, 1f / 30f);
        Assert.Equal(RocketProjectileEntity.ReducedKnockback, rocket.CurrentKnockback);

        AdvanceTicks(rocket, 10, 1f / 30f);
        Assert.Equal(0f, rocket.CurrentKnockback);
    }

    [Fact]
    public void AdvanceOneTick_UsesSourceDecayTimingAcrossTickRates()
    {
        var thirtyTickRocket = new RocketProjectileEntity(1002, PlayerTeam.Red, 1, 0f, 0f, 0f, 0f);
        var sixtyTickRocket = new RocketProjectileEntity(1003, PlayerTeam.Red, 1, 0f, 0f, 0f, 0f);

        AdvanceTicks(thirtyTickRocket, 21, 1f / 30f);
        AdvanceTicks(sixtyTickRocket, 42, 1f / 60f);
        Assert.Equal(thirtyTickRocket.CurrentKnockback, sixtyTickRocket.CurrentKnockback);

        AdvanceTicks(thirtyTickRocket, 10, 1f / 30f);
        AdvanceTicks(sixtyTickRocket, 20, 1f / 60f);
        Assert.Equal(thirtyTickRocket.CurrentKnockback, sixtyTickRocket.CurrentKnockback);
    }

    [Fact]
    public void Reflect_UsesExtendedSourceKnockbackDecaySchedule()
    {
        var rocket = new RocketProjectileEntity(1004, PlayerTeam.Red, 1, 0f, 0f, 0f, 0f);

        AdvanceTicks(rocket, 21, 1f / 30f);
        Assert.Equal(RocketProjectileEntity.ReducedKnockback, rocket.CurrentKnockback);

        rocket.Reflect(2, PlayerTeam.Blue, 0f);
        Assert.Equal(RocketProjectileEntity.InitialKnockback, rocket.CurrentKnockback);

        AdvanceTicks(rocket, 21, 1f / 30f);
        Assert.Equal(RocketProjectileEntity.InitialKnockback, rocket.CurrentKnockback);

        AdvanceTicks(rocket, 20, 1f / 30f);
        Assert.Equal(RocketProjectileEntity.ReducedKnockback, rocket.CurrentKnockback);

        AdvanceTicks(rocket, 40, 1f / 30f);
        Assert.Equal(0f, rocket.CurrentKnockback);
    }

    private static void AdvanceTicks(RocketProjectileEntity rocket, int tickCount, float deltaSeconds)
    {
        for (var tick = 0; tick < tickCount; tick += 1)
        {
            rocket.AdvanceOneTick(deltaSeconds);
        }
    }
}
