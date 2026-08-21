using OpenGarrison.Core;
using OpenGarrison.Core.LastToDie;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class LastToDieStatusRuntimeTests
{
    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    public void BleedDealsExactAttributedDamageAcrossSimulationRates(int ticksPerSecond)
    {
        var world = CreateWorld(ticksPerSecond);
        var target = AddNetworkPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Blue);
        target.ForceSetHealth(target.MaxHealth);
        var durationTicks = ticksPerSecond * 4;

        Assert.True(world.TryApplyLastToDieStatusEffect(
            target.Id,
            world.LocalPlayer.Id,
            LastToDieStatusEffectSpec.Bleed(
                LastToDieStatusEffectIds.SpyBlunderbussBleed,
                durationTicks,
                damagePerSecond: 5f)));

        Advance(world, durationTicks);

        Assert.Equal(target.MaxHealth - 20, target.Health);
        Assert.Empty(world.GetLastToDieStatusEffects(target.Id));
        var bleedEvents = world.PendingDamageEvents
            .Where(damageEvent => damageEvent.TargetEntityId == target.Id
                && damageEvent.Flags.HasFlag(DamageEventFlags.StatusTick))
            .ToArray();
        Assert.Equal(20, bleedEvents.Sum(damageEvent => damageEvent.Amount));
        Assert.All(bleedEvents, damageEvent => Assert.Equal(world.LocalPlayer.Id, damageEvent.AttackerPlayerId));
    }

    [Fact]
    public void IndependentDotChannelsKeepSeparateFractionalAccumulators()
    {
        var world = CreateWorld();
        var target = AddNetworkPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Blue);
        var durationTicks = world.Config.TicksPerSecond;
        target.ForceSetHealth(target.MaxHealth);

        Assert.True(world.TryApplyLastToDieStatusEffect(
            target.Id,
            world.LocalPlayer.Id,
            LastToDieStatusEffectSpec.Bleed(
                LastToDieStatusEffectIds.MedicExsanguinationBleed,
                durationTicks,
                damagePerSecond: 2f)));
        Assert.True(world.TryApplyLastToDieStatusEffect(
            target.Id,
            world.LocalPlayer.Id,
            LastToDieStatusEffectSpec.Poison(
                LastToDieStatusEffectIds.SniperTranqPoison,
                durationTicks,
                damagePerSecond: 9f)));

        Advance(world, durationTicks);

        Assert.Equal(target.MaxHealth - 11, target.Health);
    }

    [Fact]
    public void OverlappingSlowChannelsRestoreTheWeakerEffectThenBaseTuning()
    {
        var world = CreateWorld();
        var target = AddNetworkPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Blue);
        Assert.True(world.TrySetNetworkPlayerMovementSpeedScale(2, 1.25f));
        var baseMaxRunSpeed = target.ClassDefinition.MaxRunSpeed * 1.25f;

        Assert.True(world.TryApplyLastToDieStatusEffect(
            target.Id,
            world.LocalPlayer.Id,
            LastToDieStatusEffectSpec.Slow(
                LastToDieStatusEffectIds.SpyRubberBulletsSlow,
                durationTicks: 15,
                movementSpeedMultiplier: 0.6f)));
        Assert.True(world.TryApplyLastToDieStatusEffect(
            target.Id,
            world.LocalPlayer.Id,
            LastToDieStatusEffectSpec.Slow(
                LastToDieStatusEffectIds.MedicExsanguinationSlow,
                durationTicks: 30,
                movementSpeedMultiplier: 0.8f)));

        Assert.Equal(0.6f, target.LastToDieStatusMovementSpeedMultiplier);
        Assert.Equal(baseMaxRunSpeed * 0.6f, target.MaxRunSpeed, precision: 3);

        Advance(world, 15);

        Assert.Equal(0.8f, target.LastToDieStatusMovementSpeedMultiplier);
        Assert.Equal(baseMaxRunSpeed * 0.8f, target.MaxRunSpeed, precision: 3);

        Advance(world, 15);

        Assert.Equal(1f, target.LastToDieStatusMovementSpeedMultiplier);
        Assert.Equal(baseMaxRunSpeed, target.MaxRunSpeed, precision: 3);
        Assert.Equal(1.25f, world.GetNetworkPlayerMovementSpeedScale(2));
    }

    [Fact]
    public void ShorterStunRefreshCannotTruncateAnExistingStun()
    {
        var world = CreateWorld();
        var target = AddNetworkPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Blue);

        Assert.True(world.TryApplyLastToDieStatusEffect(
            target.Id,
            world.LocalPlayer.Id,
            LastToDieStatusEffectSpec.Stun(
                LastToDieStatusEffectIds.SpyLuckyStrikeStun,
                durationTicks: 30)));
        Advance(world, 10);
        Assert.True(world.TryApplyLastToDieStatusEffect(
            target.Id,
            world.LocalPlayer.Id,
            LastToDieStatusEffectSpec.Stun(
                LastToDieStatusEffectIds.SpyLuckyStrikeStun,
                durationTicks: 5)));

        Advance(world, 19);
        Assert.True(target.IsServerStunned);
        Assert.Single(world.GetLastToDieStatusEffects(target.Id));

        Advance(world, 1);

        Assert.False(target.IsServerStunned);
        Assert.Empty(world.GetLastToDieStatusEffects(target.Id));
    }

    [Fact]
    public void StatusFatalDamageCreditsTheSourceAndClearsEveryTargetEffect()
    {
        var world = CreateWorld();
        var target = AddNetworkPlayer(world, 2, PlayerClass.Scout, PlayerTeam.Blue);
        target.ForceSetHealth(3);
        var killsBefore = world.LocalPlayer.Kills;

        Assert.True(world.TryApplyLastToDieStatusEffect(
            target.Id,
            world.LocalPlayer.Id,
            LastToDieStatusEffectSpec.Bleed(
                LastToDieStatusEffectIds.SpyBlunderbussBleed,
                world.Config.TicksPerSecond,
                damagePerSecond: world.Config.TicksPerSecond * target.MaxHealth)));
        Assert.True(world.TryApplyLastToDieStatusEffect(
            target.Id,
            world.LocalPlayer.Id,
            LastToDieStatusEffectSpec.Slow(
                LastToDieStatusEffectIds.SpyRubberBulletsSlow,
                world.Config.TicksPerSecond,
                movementSpeedMultiplier: 0.6f)));

        Advance(world, 1);

        Assert.Equal(killsBefore + 1, world.LocalPlayer.Kills);
        Assert.Empty(world.GetLastToDieStatusEffects(target.Id));
        Assert.Equal(1f, target.LastToDieStatusMovementSpeedMultiplier);
    }

    [Fact]
    public void ReleasingASourceRemovesItsEffectsWithoutTouchingOtherSources()
    {
        var world = CreateWorld();
        var target = AddNetworkPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Blue);
        var teammate = AddNetworkPlayer(world, 3, PlayerClass.Medic, PlayerTeam.Red);
        var durationTicks = world.Config.TicksPerSecond * 3;

        Assert.True(world.TryApplyLastToDieStatusEffect(
            target.Id,
            world.LocalPlayer.Id,
            LastToDieStatusEffectSpec.Slow(
                LastToDieStatusEffectIds.SpyRubberBulletsSlow,
                durationTicks,
                movementSpeedMultiplier: 0.6f)));
        Assert.True(world.TryApplyLastToDieStatusEffect(
            target.Id,
            teammate.Id,
            LastToDieStatusEffectSpec.Slow(
                LastToDieStatusEffectIds.MedicExsanguinationSlow,
                durationTicks,
                movementSpeedMultiplier: 0.8f)));

        Assert.True(world.TryReleaseNetworkPlayerSlot(3));

        var remaining = Assert.Single(world.GetLastToDieStatusEffects(target.Id));
        Assert.Equal(world.LocalPlayer.Id, remaining.SourcePlayerId);
        Assert.Equal(0.6f, target.LastToDieStatusMovementSpeedMultiplier);
    }

    [Fact]
    public void MovementStatusReplicatedStateHydratesPredictionState()
    {
        var world = CreateWorld();
        var target = AddNetworkPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Blue);
        Assert.True(world.TryApplyLastToDieStatusEffect(
            target.Id,
            world.LocalPlayer.Id,
            LastToDieStatusEffectSpec.Slow(
                LastToDieStatusEffectIds.SpyRubberBulletsSlow,
                world.Config.TicksPerSecond,
                movementSpeedMultiplier: 0.6f)));
        var predictionState = target.CapturePredictionState();
        var clone = new PlayerEntity(5000, CharacterClassCatalog.Heavy, "Clone");

        clone.RestorePredictionState(predictionState);

        Assert.Equal(0.6f, clone.LastToDieStatusMovementSpeedMultiplier);
        Assert.Contains(
            clone.GetReplicatedStateEntries(),
            entry => entry.OwnerId == PlayerEntity.LastToDieStatusReplicatedStateOwnerId
                && entry.Key == PlayerEntity.LastToDieStatusMovementSpeedMultiplierReplicatedStateKey);
    }

    private static SimulationWorld CreateWorld(int ticksPerSecond = SimulationConfig.DefaultTicksPerSecond)
    {
        var world = new SimulationWorld(new SimulationConfig
        {
            TicksPerSecond = ticksPerSecond,
            EnableLocalDummies = false,
            EnableEnemyTrainingDummy = false,
            EnableFriendlySupportDummy = false,
        });
        world.PrepareLocalPlayerJoin();
        world.CompleteLocalPlayerJoin(PlayerClass.Spy);
        return world;
    }

    private static PlayerEntity AddNetworkPlayer(
        SimulationWorld world,
        byte slot,
        PlayerClass playerClass,
        PlayerTeam team)
    {
        Assert.True(world.TryPrepareNetworkPlayerJoin(slot));
        Assert.True(world.TrySetNetworkPlayerTeam(slot, team));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(slot, playerClass));
        Assert.True(world.TryGetNetworkPlayer(slot, out var player));
        return player;
    }

    private static void Advance(SimulationWorld world, int ticks)
    {
        for (var tick = 0; tick < ticks; tick += 1)
        {
            world.AdvanceOneTick();
        }
    }
}
