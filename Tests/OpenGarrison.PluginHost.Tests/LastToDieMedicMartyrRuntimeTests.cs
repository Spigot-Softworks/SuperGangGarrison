using System.Reflection;
using OpenGarrison.Core;
using OpenGarrison.Core.LastToDie;
using OpenGarrison.Server;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class LastToDieMedicMartyrRuntimeTests
{
    [Fact]
    public void LowestSlotValidMartyrOwnsProtectionAndReplacementIsImmediate()
    {
        Assert.True(LastToDieDerivedModifiers.FromPerks(
            [LastToDiePerkIds.Medic.Martyr]).MedicMartyrEnabled);
        var world = CreateWorld(PlayerClass.Heavy);
        var target = world.LocalPlayer;
        var lowerSlotMedic = AddPlayer(world, 2, PlayerClass.Medic, PlayerTeam.Red);
        var higherSlotMedic = AddPlayer(world, 3, PlayerClass.Medic, PlayerTeam.Red);
        target.TeleportTo(10f, 0f);
        lowerSlotMedic.TeleportTo(0f, 0f);
        higherSlotMedic.TeleportTo(20f, 0f);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(2, [LastToDiePerkIds.Medic.Martyr]));
        Assert.True(world.TryConfigureLastToDiePlayerBuild(3, [LastToDiePerkIds.Medic.Martyr]));
        lowerSlotMedic.SetMedicHealingTarget(target);
        higherSlotMedic.SetMedicHealingTarget(target);

        RefreshMedicLinks(world);

        Assert.True(target.LastToDieMedicMartyrProtectedLinkActive);
        Assert.True(lowerSlotMedic.LastToDieMedicMartyrProtectorLinkActive);
        Assert.False(higherSlotMedic.LastToDieMedicMartyrProtectorLinkActive);
        Assert.True(world.TryGetLastToDieMartyrProtector(target, out var protector));
        Assert.Same(lowerSlotMedic, protector);
        Assert.Equal(0.7f, lowerSlotMedic.LastToDieIncomingDamageMultiplier, precision: 5);
        Assert.Equal(1f, higherSlotMedic.LastToDieIncomingDamageMultiplier, precision: 5);

        lowerSlotMedic.TeleportTo(1000f, 0f);
        RefreshMedicLinks(world);

        Assert.True(target.LastToDieMedicMartyrProtectedLinkActive);
        Assert.False(lowerSlotMedic.LastToDieMedicMartyrProtectorLinkActive);
        Assert.True(higherSlotMedic.LastToDieMedicMartyrProtectorLinkActive);
        Assert.True(world.TryGetLastToDieMartyrProtector(target, out protector));
        Assert.Same(higherSlotMedic, protector);
    }

    [Fact]
    public void FatalInstantDamageUsesActualClampedDamageForPluginsEventsAndSpikedVest()
    {
        var world = CreateWorld(PlayerClass.Medic);
        var protector = world.LocalPlayer;
        var target = AddPlayer(world, 2, PlayerClass.Medic, PlayerTeam.Red);
        var attacker = AddPlayer(world, 3, PlayerClass.Heavy, PlayerTeam.Blue);
        protector.TeleportTo(0f, 0f);
        target.TeleportTo(10f, 0f);
        attacker.TeleportTo(20f, 0f);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Medic.Martyr]));
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            2,
            [LastToDiePerkIds.Medic.SpikedVest]));
        protector.SetMedicHealingTarget(target);
        RefreshMedicLinks(world);
        target.ForceSetHealth(10);
        var targetDamageDecisions = new List<WorldDamageDecisionRequest>();
        var deathDecisionCount = 0;
        world.DamageDecisionInterceptor = request =>
        {
            if (request.TargetPlayerId == target.Id)
            {
                targetDamageDecisions.Add(request);
            }

            return WorldDecisionResult.Continue;
        };
        world.DeathDecisionInterceptor = _ =>
        {
            deathDecisionCount += 1;
            return WorldDecisionResult.Continue;
        };

        var resolution = ResolveDamage(
            world,
            target,
            100f,
            attacker,
            PlayerDamageApplicationKind.Instant,
            PlayerDamageTraits.CanApplyOnHitEffects | PlayerDamageTraits.CanReflect);

        Assert.Equal(PlayerDamageDisposition.FatalPrevented, resolution.Disposition);
        Assert.Equal(9, resolution.AppliedHealthDamage);
        Assert.False(resolution.WasFatal);
        Assert.True(resolution.ShouldApplyOnHitEffects);
        Assert.Equal(1, target.Health);
        Assert.Equal(85f, resolution.DamageAfterIncomingModifiers);
        var damageDecision = Assert.Single(targetDamageDecisions);
        Assert.Equal(9, damageDecision.Amount);
        Assert.False(damageDecision.WouldBeFatal);
        Assert.Equal(0, deathDecisionCount);
        Assert.Equal(attacker.MaxHealth - 2, attacker.Health);
        var targetEvent = Assert.Single(
            world.DrainPendingDamageEvents().Where(damageEvent => damageEvent.TargetEntityId == target.Id));
        Assert.Equal(9, targetEvent.Amount);
        Assert.False(targetEvent.WasFatal);

        var attackerHealthAtOneHp = attacker.Health;
        var blockedAtOneHealth = ResolveDamage(
            world,
            target,
            100f,
            attacker,
            PlayerDamageApplicationKind.Instant,
            PlayerDamageTraits.CanApplyOnHitEffects | PlayerDamageTraits.CanReflect);

        Assert.Equal(PlayerDamageDisposition.FatalPrevented, blockedAtOneHealth.Disposition);
        Assert.Equal(0, blockedAtOneHealth.AppliedHealthDamage);
        Assert.False(blockedAtOneHealth.ShouldApplyOnHitEffects);
        Assert.Equal(attackerHealthAtOneHp, attacker.Health);
        Assert.Single(targetDamageDecisions);
        Assert.Empty(world.PendingDamageEvents);
    }

    [Fact]
    public void FatalContinuousAndExecuteDamageStopAtOneWithoutGibbing()
    {
        var world = CreateWorld(PlayerClass.Medic);
        var protector = world.LocalPlayer;
        var target = AddPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Red);
        var attacker = AddPlayer(world, 3, PlayerClass.Sniper, PlayerTeam.Blue);
        protector.TeleportTo(0f, 0f);
        target.TeleportTo(10f, 0f);
        attacker.TeleportTo(20f, 0f);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Medic.Martyr]));
        protector.SetMedicHealingTarget(target);
        RefreshMedicLinks(world);
        target.ForceSetHealth(5);

        var continuous = ResolveDamage(
            world,
            target,
            20f,
            attacker,
            PlayerDamageApplicationKind.Continuous,
            PlayerDamageTraits.Periodic | PlayerDamageTraits.CanApplyOnHitEffects);

        Assert.Equal(PlayerDamageDisposition.FatalPrevented, continuous.Disposition);
        Assert.Equal(4, continuous.AppliedHealthDamage);
        Assert.Equal(1, target.Health);
        Assert.False(continuous.WasFatal);
        _ = world.DrainPendingDamageEvents();

        var continuousAtOne = ResolveDamage(
            world,
            target,
            1f,
            attacker,
            PlayerDamageApplicationKind.Continuous,
            PlayerDamageTraits.Periodic | PlayerDamageTraits.CanApplyOnHitEffects);
        Assert.Equal(PlayerDamageDisposition.FatalPrevented, continuousAtOne.Disposition);
        Assert.Equal(0, continuousAtOne.AppliedHealthDamage);
        Assert.False(continuousAtOne.ShouldApplyOnHitEffects);
        Assert.Empty(world.PendingDamageEvents);

        target.ForceSetHealth(5);
        var execute = world.ResolvePlayerDamage(
            target,
            new PlayerDamageRequest(
                PlayerDamageApplicationKind.Instant,
                Amount: 1f,
                attacker,
                PlayerEntity.SpyDamageRevealAlpha,
                DamageEventFlags.None,
                PlayerDamageTraits.ExecuteAfterDefenses | PlayerDamageTraits.Bullet,
                AllowOsmosisHealOwnedSentries: false,
                new PlayerDamageUmbrellaOptions(AllowBlock: false),
                GibOnFatal: true,
                FatalWeaponSpriteName: "SniperRifle"));

        Assert.Equal(PlayerDamageDisposition.FatalPrevented, execute.Disposition);
        Assert.Equal(4, execute.AppliedHealthDamage);
        Assert.Equal(1, target.Health);
        Assert.True(target.IsAlive);
        Assert.Equal(0, target.GibDeaths);
        Assert.False(execute.WasFatal);
    }

    [Fact]
    public void FatalPreventionBasesVampireRewardOnAppliedDamage()
    {
        var world = CreateWorld(PlayerClass.Medic);
        var protector = world.LocalPlayer;
        var target = AddPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Red);
        var attacker = AddPlayer(world, 3, PlayerClass.Spy, PlayerTeam.Blue);
        protector.TeleportTo(0f, 0f);
        target.TeleportTo(10f, 0f);
        attacker.TeleportTo(20f, 0f);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Medic.Martyr]));
        Assert.True(world.TryConfigureLastToDiePlayerBuild(3, [LastToDiePerkIds.Spy.Vampire]));
        protector.SetMedicHealingTarget(target);
        RefreshMedicLinks(world);
        target.ForceSetHealth(100);
        attacker.ForceSetHealth(1);

        var resolution = ResolveDamage(
            world,
            target,
            200f,
            attacker,
            PlayerDamageApplicationKind.Instant,
            PlayerDamageTraits.CanApplyOnHitEffects);

        Assert.Equal(PlayerDamageDisposition.FatalPrevented, resolution.Disposition);
        Assert.Equal(99, resolution.AppliedHealthDamage);
        Assert.Equal(1, target.Health);
        Assert.Equal(11, attacker.Health);
    }

    [Fact]
    public void MartyrRolePresentationRoundTripsThroughPredictionLegacyAndProtocol64()
    {
        var source = CreateWorld(PlayerClass.Medic);
        source.LocalPlayer.SetLastToDieMedicLinkProjection(
            stimulantDripActive: true,
            agilityDriveActive: true,
            martyrProtectedActive: true,
            martyrProtectorActive: true);

        Assert.Equal((byte)15, source.LocalPlayer.LastToDieMedicLinkState);
        var predictionShadow = new PlayerEntity(90, CharacterClassCatalog.Medic, "Prediction Medic");
        predictionShadow.RestorePredictionState(source.LocalPlayer.CapturePredictionState());
        Assert.True(predictionShadow.LastToDieMedicMartyrProtectedLinkActive);
        Assert.True(predictionShadow.LastToDieMedicMartyrProtectorLinkActive);

        var legacyShadow = new PlayerEntity(91, CharacterClassCatalog.Medic, "Legacy Medic");
        legacyShadow.ReplaceReplicatedStateEntries(source.LocalPlayer.GetReplicatedStateEntries());
        Assert.Equal((byte)15, legacyShadow.LastToDieMedicLinkState);

        var protocolState = Assert.Single(new Protocol64StatePublisher(source).BuildPlayerStateBatch(1).Players);
        Assert.Equal((byte)15, protocolState.LastToDieMedicLinkState);
        var receiver = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        Assert.True(receiver.ApplyProtocol64PlayerState(protocolState));
        Assert.True(receiver.LocalPlayer.LastToDieMedicMartyrProtectedLinkActive);
        Assert.True(receiver.LocalPlayer.LastToDieMedicMartyrProtectorLinkActive);
    }

    private static SimulationWorld CreateWorld(PlayerClass localClass)
    {
        var world = new SimulationWorld(new SimulationConfig
        {
            EnableLocalDummies = false,
            EnableEnemyTrainingDummy = false,
            EnableFriendlySupportDummy = false,
        });
        world.PrepareLocalPlayerJoin();
        Assert.True(world.TrySetNetworkPlayerTeam(
            SimulationWorld.LocalPlayerSlot,
            PlayerTeam.Red,
            respawnLivePlayerImmediately: true));
        world.CompleteLocalPlayerJoin(localClass);
        return world;
    }

    private static PlayerEntity AddPlayer(
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

    private static void RefreshMedicLinks(SimulationWorld world)
    {
        var method = typeof(SimulationWorld).GetMethod(
            "RefreshLastToDieMedicLinkProjections",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        _ = method!.Invoke(world, null);
    }

    private static PlayerDamageResolution ResolveDamage(
        SimulationWorld world,
        PlayerEntity target,
        float damage,
        PlayerEntity attacker,
        PlayerDamageApplicationKind applicationKind,
        PlayerDamageTraits traits)
    {
        return world.ResolvePlayerDamage(
            target,
            new PlayerDamageRequest(
                applicationKind,
                damage,
                attacker,
                PlayerEntity.SpyDamageRevealAlpha,
                DamageEventFlags.None,
                traits,
                AllowOsmosisHealOwnedSentries: false,
                new PlayerDamageUmbrellaOptions(AllowBlock: false)));
    }
}
