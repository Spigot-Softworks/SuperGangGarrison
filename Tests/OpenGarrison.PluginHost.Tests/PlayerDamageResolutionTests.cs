using System.Reflection;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class PlayerDamageResolutionTests
{
    [Fact]
    public void TypedResolutionReportsAppliedCriticalDamage()
    {
        var world = CreateWorld(PlayerClass.Spy);
        var target = AddNetworkPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Blue);
        var healthBefore = target.Health;

        var resolution = world.ResolvePlayerDamage(
            target,
            new PlayerDamageRequest(
                PlayerDamageApplicationKind.Instant,
                Amount: 20f,
                world.LocalPlayer,
                PlayerEntity.SpyDamageRevealAlpha,
                DamageEventFlags.None,
                PlayerDamageTraits.CanEvade
                    | PlayerDamageTraits.CanApplyOnHitEffects
                    | PlayerDamageTraits.Critical
                    | PlayerDamageTraits.Bullet,
                AllowOsmosisHealOwnedSentries: true,
                new PlayerDamageUmbrellaOptions(AllowBlock: true)));

        Assert.Equal(PlayerDamageDisposition.Applied, resolution.Disposition);
        Assert.Equal(20, resolution.AppliedHealthDamage);
        Assert.Equal(healthBefore, resolution.HealthBefore);
        Assert.Equal(healthBefore - 20, resolution.HealthAfter);
        Assert.True(resolution.ShouldApplyOnHitEffects);
        Assert.True(resolution.EventFlags.HasFlag(DamageEventFlags.Critical));
        Assert.True(Assert.Single(world.PendingDamageEvents).Flags.HasFlag(DamageEventFlags.Critical));
    }

    [Fact]
    public void ContinuousResolutionPreservesFractionalAccumulation()
    {
        var world = CreateWorld(PlayerClass.Spy);
        var target = AddNetworkPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Blue);
        var healthBefore = target.Health;
        var request = new PlayerDamageRequest(
            PlayerDamageApplicationKind.Continuous,
            Amount: 0.4f,
            world.LocalPlayer,
            PlayerEntity.SpyDamageRevealAlpha,
            DamageEventFlags.None,
            PlayerDamageTraits.None,
            AllowOsmosisHealOwnedSentries: true,
            new PlayerDamageUmbrellaOptions(AllowBlock: false));

        var first = world.ResolvePlayerDamage(target, request);
        var second = world.ResolvePlayerDamage(target, request);
        var third = world.ResolvePlayerDamage(target, request);

        Assert.Equal(PlayerDamageDisposition.Accumulated, first.Disposition);
        Assert.Equal(PlayerDamageDisposition.Accumulated, second.Disposition);
        Assert.Equal(PlayerDamageDisposition.Applied, third.Disposition);
        Assert.Equal(1, third.AppliedHealthDamage);
        Assert.Equal(healthBefore - 1, target.Health);
    }

    [Fact]
    public void InvulnerableResolutionCannotApplyOnHitEffects()
    {
        var world = CreateWorld(PlayerClass.Spy);
        var target = AddNetworkPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Blue);
        target.RefreshUber();

        var resolution = world.ResolvePlayerDamage(
            target,
            new PlayerDamageRequest(
                PlayerDamageApplicationKind.Instant,
                Amount: 20f,
                world.LocalPlayer,
                PlayerEntity.SpyDamageRevealAlpha,
                DamageEventFlags.None,
                PlayerDamageTraits.CanApplyOnHitEffects | PlayerDamageTraits.Bullet,
                AllowOsmosisHealOwnedSentries: true,
                new PlayerDamageUmbrellaOptions(AllowBlock: true)));

        Assert.Equal(PlayerDamageDisposition.Invulnerable, resolution.Disposition);
        Assert.Equal(0, resolution.AppliedHealthDamage);
        Assert.False(resolution.ShouldApplyOnHitEffects);
    }

    [Fact]
    public void LegacyDamageReflectionContractStillHasOneElevenParameterMethod()
    {
        var methods = typeof(SimulationWorld)
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(static method => method.Name == "ApplyPlayerDamage")
            .ToArray();

        var method = Assert.Single(methods);
        Assert.Equal(11, method.GetParameters().Length);
        Assert.Equal(typeof(bool), method.ReturnType);
    }

    private static SimulationWorld CreateWorld(PlayerClass localClass)
    {
        var world = new SimulationWorld();
        world.PrepareLocalPlayerJoin();
        world.CompleteLocalPlayerJoin(localClass);
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
}
