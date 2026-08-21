using System.Reflection;
using OpenGarrison.Core;
using OpenGarrison.Core.LastToDie;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class LastToDieMedicPerkRuntimeTests
{
    [Fact]
    public void HealingEconomyPerksAggregateIntoTypedModifiers()
    {
        var modifiers = LastToDieDerivedModifiers.FromPerks(
        [
            LastToDiePerkIds.Medic.TraumaSurgeon,
            LastToDiePerkIds.Medic.Overcharged,
            LastToDiePerkIds.Medic.Homeostasis,
        ]);

        Assert.True(modifiers.MedicTraumaSurgeonEnabled);
        Assert.Equal(
            LastToDieDerivedModifiers.MedicOverchargedUberChargeGainMultiplier,
            modifiers.MedicUberChargeGainMultiplier);
        Assert.Equal(
            LastToDieDerivedModifiers.MedicHomeostasisHealingShare,
            modifiers.MedicHomeostasisHealingFraction);
    }

    [Fact]
    public void DefensivePerksAggregateIntoTypedModifiers()
    {
        var modifiers = LastToDieDerivedModifiers.FromPerks(
        [
            LastToDiePerkIds.Medic.CombatMedic,
            LastToDiePerkIds.Medic.FieldCommander,
            LastToDiePerkIds.Medic.Stoic,
            LastToDiePerkIds.Medic.SpikedVest,
            LastToDiePerkIds.Medic.IronWill,
            LastToDiePerkIds.Medic.VitalityTrinket,
            LastToDiePerkIds.Medic.Exsanguination,
        ]);

        Assert.True(modifiers.MedicCombatMedicEnabled);
        Assert.True(modifiers.MedicFieldCommanderEnabled);
        Assert.True(modifiers.MedicStoicEnabled);
        Assert.True(modifiers.MedicSpikedVestEnabled);
        Assert.True(modifiers.MedicIronWillEnabled);
        Assert.True(modifiers.MedicExsanguinationEnabled);
        Assert.Equal(75, modifiers.MaximumHealthBonus);
    }

    [Fact]
    public void MedicLinkPerksAggregateAndProjectTheirCompleteEffectsToAValidLink()
    {
        var modifiers = LastToDieDerivedModifiers.FromPerks(
        [
            LastToDiePerkIds.Medic.StimulantDrip,
            LastToDiePerkIds.Medic.AgilityDrive,
        ]);
        Assert.True(modifiers.MedicStimulantDripEnabled);
        Assert.True(modifiers.MedicAgilityDriveEnabled);

        var world = CreateMedicWorld();
        var medic = world.LocalPlayer;
        var target = AddHeavyTeammate(world, slot: 2);
        var enemy = AddHeavyPlayer(world, slot: 3, PlayerTeam.Blue);
        medic.TeleportTo(0f, 0f);
        target.TeleportTo(10f, 0f);
        enemy.TeleportTo(20f, 0f);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Medic.StimulantDrip, LastToDiePerkIds.Medic.AgilityDrive]));
        Assert.True(world.TryConfigureLastToDiePlayerBuild(2, []));
        var targetBaseRunPower = target.RunPower;
        var medicBaseRunPower = medic.RunPower;
        medic.SetMedicHealingTarget(target);

        InvokeRefreshMedicLinkProjections(world);

        Assert.True(target.LastToDieMedicStimulantDripLinkActive);
        Assert.True(target.LastToDieMedicAgilityDriveLinkActive);
        Assert.False(medic.LastToDieMedicStimulantDripLinkActive);
        Assert.True(medic.LastToDieMedicAgilityDriveLinkActive);
        Assert.Equal(targetBaseRunPower * 1.25f, target.RunPower, precision: 4);
        Assert.Equal(medicBaseRunPower * 1.25f, medic.RunPower, precision: 4);
        Assert.Equal(0.25f, InvokeLastToDieEvasionChance(world, target), precision: 5);
        Assert.Equal(0.25f, InvokeLastToDieEvasionChance(world, medic), precision: 5);
        Assert.Equal(0.8f, target.LastToDieIncomingDamageMultiplier, precision: 5);

        var outgoing = ResolveDamage(world, enemy, 100f, target);
        Assert.Equal(120f, outgoing.DamageAfterOutgoingModifiers, precision: 4);
        Assert.True(target.TryGetReplicatedStateInt(
            PlayerEntity.LastToDieMedicLinkReplicatedStateOwnerId,
            PlayerEntity.LastToDieMedicLinkReplicatedStateKey,
            out var encoded));
        Assert.Equal(3, encoded);

        var predictionShadow = new PlayerEntity(90, CharacterClassCatalog.Heavy, "Prediction target");
        predictionShadow.RestorePredictionState(target.CapturePredictionState());
        Assert.True(predictionShadow.LastToDieMedicStimulantDripLinkActive);
        Assert.True(predictionShadow.LastToDieMedicAgilityDriveLinkActive);

        var snapshotShadow = new PlayerEntity(91, CharacterClassCatalog.Heavy, "Snapshot target");
        snapshotShadow.ReplaceReplicatedStateEntries(target.GetReplicatedStateEntries());
        Assert.Equal((byte)3, snapshotShadow.LastToDieMedicLinkState);
    }

    [Fact]
    public void StimulantDripRescalesActiveTimersOnceAndMultipleLinksDoNotStack()
    {
        var world = CreateWorld(PlayerClass.Heavy);
        var target = world.LocalPlayer;
        var firstMedic = AddNetworkPlayer(world, slot: 2, PlayerClass.Medic, PlayerTeam.Red);
        var secondMedic = AddNetworkPlayer(world, slot: 3, PlayerClass.Medic, PlayerTeam.Red);
        target.TeleportTo(10f, 0f);
        firstMedic.TeleportTo(0f, 0f);
        secondMedic.TeleportTo(20f, 0f);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(SimulationWorld.LocalPlayerSlot, []));
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            2,
            [LastToDiePerkIds.Medic.StimulantDrip]));
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            3,
            [LastToDiePerkIds.Medic.StimulantDrip]));
        SetPlayerTimer(target, nameof(PlayerEntity.PrimaryCooldownTicks), 12);
        SetPlayerTimer(target, nameof(PlayerEntity.ReloadTicksUntilNextShell), 12);
        firstMedic.SetMedicHealingTarget(target);
        secondMedic.SetMedicHealingTarget(target);

        InvokeRefreshMedicLinkProjections(world);

        Assert.Equal(10, target.PrimaryCooldownTicks);
        Assert.Equal(10, target.ReloadTicksUntilNextShell);
        Assert.Equal(10, InvokeExperimentalReloadMultiplier(target, 12));

        firstMedic.ClearMedicHealingTarget();
        InvokeRefreshMedicLinkProjections(world);
        Assert.True(target.LastToDieMedicStimulantDripLinkActive);
        Assert.Equal(10, target.PrimaryCooldownTicks);
        Assert.Equal(10, target.ReloadTicksUntilNextShell);

        secondMedic.ClearMedicHealingTarget();
        InvokeRefreshMedicLinkProjections(world);
        Assert.False(target.LastToDieMedicStimulantDripLinkActive);
        Assert.Equal(12, target.PrimaryCooldownTicks);
        Assert.Equal(12, target.ReloadTicksUntilNextShell);
        Assert.Equal(12, InvokeExperimentalReloadMultiplier(target, 12));
    }

    [Fact]
    public void ModifiedSpringAcceleratesOnlyMedicM2AndComposesWithStimulantDrip()
    {
        var springWorld = CreateMedicWorld();
        var springMedic = springWorld.LocalPlayer;
        Assert.True(springWorld.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Medic.ModifiedSpring]));

        Assert.True(springMedic.TryFireMedicNeedle(fireCooldownTicks: 18, refillTicks: 60));
        Assert.Equal(9, springMedic.MedicNeedleCooldownTicks);
        Assert.Equal(30, springMedic.MedicNeedleRefillTicks);

        springMedic.EquipExperimentalOffhandWeapon();
        Assert.True(springMedic.TryFireMedicKritzHealNeedle(fireCooldownTicks: 18, refillTicks: 60));
        Assert.Equal(9, springMedic.ExperimentalOffhandCooldownTicks);
        Assert.Equal(30, springMedic.ExperimentalOffhandReloadTicksUntilNextShell);

        var composedWorld = CreateMedicWorld();
        var composedMedic = composedWorld.LocalPlayer;
        var stimulantMedic = AddNetworkPlayer(
            composedWorld,
            slot: 2,
            PlayerClass.Medic,
            PlayerTeam.Red);
        composedMedic.TeleportTo(10f, 0f);
        stimulantMedic.TeleportTo(0f, 0f);
        Assert.True(composedWorld.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Medic.ModifiedSpring]));
        Assert.True(composedWorld.TryConfigureLastToDiePlayerBuild(
            2,
            [LastToDiePerkIds.Medic.StimulantDrip]));
        stimulantMedic.SetMedicHealingTarget(composedMedic);
        InvokeRefreshMedicLinkProjections(composedWorld);

        Assert.True(composedMedic.TryFireMedicNeedle(fireCooldownTicks: 24, refillTicks: 120));
        Assert.Equal(10, composedMedic.MedicNeedleCooldownTicks);
        Assert.Equal(50, composedMedic.MedicNeedleRefillTicks);
    }

    [Fact]
    public void ModifiedSpringRescalesActiveM2TimersWithoutTouchingPrimaryBeamTimers()
    {
        var world = CreateMedicWorld();
        var medic = world.LocalPlayer;
        SetPlayerTimer(medic, nameof(PlayerEntity.PrimaryCooldownTicks), 12);
        SetPlayerTimer(medic, nameof(PlayerEntity.MedicNeedleCooldownTicks), 12);
        SetPlayerTimer(medic, nameof(PlayerEntity.MedicNeedleRefillTicks), 20);
        SetPlayerTimer(medic, nameof(PlayerEntity.ExperimentalOffhandCooldownTicks), 12);
        SetPlayerTimer(medic, nameof(PlayerEntity.ExperimentalOffhandReloadTicksUntilNextShell), 20);

        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Medic.ModifiedSpring]));

        Assert.Equal(12, medic.PrimaryCooldownTicks);
        Assert.Equal(6, medic.MedicNeedleCooldownTicks);
        Assert.Equal(10, medic.MedicNeedleRefillTicks);
        Assert.Equal(6, medic.ExperimentalOffhandCooldownTicks);
        Assert.Equal(10, medic.ExperimentalOffhandReloadTicksUntilNextShell);

        Assert.True(world.TryConfigureLastToDiePlayerBuild(SimulationWorld.LocalPlayerSlot, []));
        Assert.Equal(12, medic.PrimaryCooldownTicks);
        Assert.Equal(12, medic.MedicNeedleCooldownTicks);
        Assert.Equal(20, medic.MedicNeedleRefillTicks);
        Assert.Equal(12, medic.ExperimentalOffhandCooldownTicks);
        Assert.Equal(20, medic.ExperimentalOffhandReloadTicksUntilNextShell);
    }

    [Fact]
    public void SupportRelayRestoresEquippedAmmoPoolsWithoutClearingWeaponTimers()
    {
        var primary = CreateWorld(PlayerClass.Heavy).LocalPlayer;
        primary.ForceSetAmmo(primary.MaxShells - 7);
        SetPlayerTimer(primary, nameof(PlayerEntity.PrimaryCooldownTicks), 13);
        SetPlayerTimer(primary, nameof(PlayerEntity.ReloadTicksUntilNextShell), 17);
        Assert.True(primary.TryRestoreLastToDieSupportRelayAmmo());
        Assert.Equal(primary.MaxShells - 5, primary.CurrentShells);
        Assert.Equal(13, primary.PrimaryCooldownTicks);
        Assert.Equal(17, primary.ReloadTicksUntilNextShell);

        var offhand = CreateMedicWorld().LocalPlayer;
        offhand.EquipExperimentalOffhandWeapon();
        SetPlayerTimer(offhand, nameof(PlayerEntity.ExperimentalOffhandCurrentShells), 1);
        SetPlayerTimer(offhand, nameof(PlayerEntity.ExperimentalOffhandCooldownTicks), 8);
        SetPlayerTimer(offhand, nameof(PlayerEntity.ExperimentalOffhandReloadTicksUntilNextShell), 19);
        var primaryAmmoBeforeOffhandRelay = offhand.CurrentShells;
        Assert.True(offhand.TryRestoreLastToDieSupportRelayAmmo());
        Assert.Equal(2, offhand.ExperimentalOffhandCurrentShells);
        Assert.Equal(primaryAmmoBeforeOffhandRelay, offhand.CurrentShells);
        Assert.Equal(8, offhand.ExperimentalOffhandCooldownTicks);
        Assert.Equal(19, offhand.ExperimentalOffhandReloadTicksUntilNextShell);

        var acquired = CreateWorld(PlayerClass.Soldier).LocalPlayer;
        var acquiredItemId = CharacterClassCatalog.RuntimeRegistry
            .GetPrimaryItem(PlayerClass.Medic)
            .Id;
        Assert.True(acquired.TryGrantGameplayItem(acquiredItemId));
        acquired.SetAcquiredWeapon(PlayerClass.Medic);
        acquired.EquipAcquiredWeapon();
        SetPlayerTimer(acquired, nameof(PlayerEntity.AcquiredWeaponCurrentShells), 31);
        SetPlayerTimer(acquired, nameof(PlayerEntity.AcquiredWeaponCooldownTicks), 11);
        SetPlayerTimer(acquired, nameof(PlayerEntity.AcquiredWeaponReloadTicksUntilNextShell), 23);
        Assert.True(acquired.TryRestoreLastToDieSupportRelayAmmo());
        Assert.Equal(33, acquired.AcquiredWeaponCurrentShells);
        Assert.Equal(11, acquired.AcquiredWeaponCooldownTicks);
        Assert.Equal(23, acquired.AcquiredWeaponReloadTicksUntilNextShell);

        var pyro = CreateWorld(PlayerClass.Pyro).LocalPlayer;
        pyro.ForceSetAmmo(pyro.MaxShells - 7);
        SetPlayerTimer(pyro, nameof(PlayerEntity.PrimaryCooldownTicks), 7);
        SetPlayerTimer(pyro, nameof(PlayerEntity.ReloadTicksUntilNextShell), 29);
        Assert.True(pyro.TryRestoreLastToDieSupportRelayAmmo());
        Assert.Equal((pyro.MaxShells - 5) * PlayerEntity.PyroPrimaryFuelScale, pyro.PyroPrimaryFuelScaled);
        Assert.Equal(7, pyro.PrimaryCooldownTicks);
        Assert.Equal(29, pyro.ReloadTicksUntilNextShell);
    }

    [Fact]
    public void SupportRelayIsAcquisitionTriggeredAndUsesPairScopedFiveSecondCooldown()
    {
        var world = CreateMedicWorld();
        var medic = world.LocalPlayer;
        var firstTarget = AddHeavyTeammate(world, slot: 2);
        var secondTarget = AddHeavyTeammate(world, slot: 3);
        medic.TeleportTo(0f, 0f);
        firstTarget.TeleportTo(10f, 0f);
        secondTarget.TeleportTo(20f, 0f);
        firstTarget.ForceSetAmmo(firstTarget.MaxShells - 9);
        secondTarget.ForceSetAmmo(secondTarget.MaxShells - 4);
        SetPlayerTimer(firstTarget, nameof(PlayerEntity.PrimaryCooldownTicks), 7);
        SetPlayerTimer(firstTarget, nameof(PlayerEntity.ReloadTicksUntilNextShell), 13);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Medic.SupportRelay]));

        medic.SetMedicHealingTarget(firstTarget);
        InvokeRefreshMedicLinkProjections(world);
        Assert.Equal(firstTarget.MaxShells - 7, firstTarget.CurrentShells);
        Assert.Equal(7, firstTarget.PrimaryCooldownTicks);
        Assert.Equal(13, firstTarget.ReloadTicksUntilNextShell);

        InvokeRefreshMedicLinkProjections(world);
        Assert.Equal(firstTarget.MaxShells - 7, firstTarget.CurrentShells);
        SetPlayerTimer(firstTarget, nameof(PlayerEntity.CurrentShells), firstTarget.MaxShells - 12);
        medic.ClearMedicHealingTarget();
        InvokeRefreshMedicLinkProjections(world);
        medic.SetMedicHealingTarget(firstTarget);
        InvokeRefreshMedicLinkProjections(world);
        Assert.Equal(firstTarget.MaxShells - 12, firstTarget.CurrentShells);

        medic.ClearMedicHealingTarget();
        InvokeRefreshMedicLinkProjections(world);
        medic.SetMedicHealingTarget(secondTarget);
        InvokeRefreshMedicLinkProjections(world);
        Assert.Equal(secondTarget.MaxShells - 3, secondTarget.CurrentShells);

        SetWorldFrame(
            world,
            LastToDieDerivedModifiers.MedicSupportRelayCooldownSeconds
                * world.Config.TicksPerSecond);
        medic.ClearMedicHealingTarget();
        InvokeRefreshMedicLinkProjections(world);
        medic.SetMedicHealingTarget(firstTarget);
        InvokeRefreshMedicLinkProjections(world);
        Assert.Equal(firstTarget.MaxShells - 9, firstTarget.CurrentShells);
    }

    [Fact]
    public void SupportRelayFullAmmoDoesNotConsumeCooldownAndKritzImpactUsesTheSamePairGate()
    {
        var world = CreateMedicWorld();
        var medic = world.LocalPlayer;
        var target = AddHeavyTeammate(world, slot: 2);
        medic.TeleportTo(0f, 0f);
        target.TeleportTo(10f, 0f);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Medic.SupportRelay]));

        medic.SetMedicHealingTarget(target);
        InvokeRefreshMedicLinkProjections(world);
        medic.ClearMedicHealingTarget();
        InvokeRefreshMedicLinkProjections(world);
        SetPlayerTimer(target, nameof(PlayerEntity.CurrentShells), target.MaxShells - 6);
        medic.SetMedicHealingTarget(target);
        InvokeRefreshMedicLinkProjections(world);
        Assert.Equal(target.MaxShells - 4, target.CurrentShells);

        SetPlayerTimer(target, nameof(PlayerEntity.CurrentShells), target.MaxShells - 10);
        SetWorldFrame(
            world,
            LastToDieDerivedModifiers.MedicSupportRelayCooldownSeconds
                * world.Config.TicksPerSecond);
        var needle = new MedicHealNeedleProjectileEntity(
            100,
            medic.Team,
            medic.Id,
            medic.X,
            medic.Y,
            1f,
            0f);
        InvokeMedicHealNeedleTeammateHit(world, medic, target, needle);
        Assert.Equal(target.MaxShells - 8, target.CurrentShells);

        SetPlayerTimer(target, nameof(PlayerEntity.CurrentShells), target.MaxShells - 15);
        InvokeMedicHealNeedleTeammateHit(world, medic, target, needle);
        Assert.Equal(target.MaxShells - 15, target.CurrentShells);
    }

    [Fact]
    public void TraumaSurgeonScalesFromOneToOneHundredFiftyPercentUsingPreHealHealth()
    {
        var traumaWorld = CreateMedicWorld();
        var traumaTarget = AddHeavyTeammate(traumaWorld, slot: 2);
        Assert.True(traumaWorld.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Medic.TraumaSurgeon]));

        traumaTarget.ForceSetHealth(traumaTarget.MaxHealth);
        Assert.Equal(1f, InvokeMedicHealingMultiplier(traumaWorld, traumaWorld.LocalPlayer, traumaTarget));

        traumaTarget.ForceSetHealth(traumaTarget.MaxHealth / 10);
        Assert.Equal(1.5f, InvokeMedicHealingMultiplier(traumaWorld, traumaWorld.LocalPlayer, traumaTarget));

        traumaTarget.ForceSetHealth((int)(traumaTarget.MaxHealth * 0.55f));
        Assert.Equal(1.25f, InvokeMedicHealingMultiplier(traumaWorld, traumaWorld.LocalPlayer, traumaTarget), precision: 5);
    }

    [Fact]
    public void OverchargedDoublesDamagedTargetUberGain()
    {
        var world = CreateMedicWorld();
        var target = AddHeavyTeammate(world, slot: 2);
        target.ForceSetHealth(target.MaxHealth - 20);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Medic.Overcharged]));

        InvokeApplyMedicHealing(world, world.LocalPlayer, target);

        Assert.Equal(5f, world.LocalPlayer.MedicUberCharge);
    }

    [Fact]
    public void HomeostasisUsesActualTargetHealingAndPreservesFractionalRemainder()
    {
        var world = CreateMedicWorld();
        var medic = world.LocalPlayer;
        var target = AddHeavyTeammate(world, slot: 2);
        medic.ForceSetHealth(medic.MaxHealth - 40);
        target.ForceSetHealth(target.MaxHealth / 10);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Medic.Homeostasis]));

        var medicHealth = medic.Health;
        for (var tick = 0; tick < 20; tick += 1)
        {
            InvokeApplyMedicHealing(world, medic, target);
        }

        Assert.Equal(20, target.Health - (target.MaxHealth / 10));
        Assert.Equal(7, medic.Health - medicHealth);

        target.ForceSetHealth(target.MaxHealth);
        var healthAtFullTarget = medic.Health;
        for (var tick = 0; tick < 10; tick += 1)
        {
            InvokeApplyMedicHealing(world, medic, target);
        }

        Assert.Equal(healthAtFullTarget, medic.Health);
    }

    [Fact]
    public void CombatMedicActivatesStrictlyBelowHalfHealthForOutgoingAndIncomingDamage()
    {
        var world = CreateMedicWorld();
        var medic = world.LocalPlayer;
        var enemy = AddHeavyPlayer(world, slot: 2, PlayerTeam.Blue);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Medic.CombatMedic]));

        medic.ForceSetHealth(medic.MaxHealth / 2);
        var exactHalfOutgoing = ResolveDamage(world, enemy, 100f, medic);
        Assert.Equal(100f, exactHalfOutgoing.DamageAfterOutgoingModifiers);

        enemy.ForceSetHealth(enemy.MaxHealth);
        medic.ForceSetHealth((medic.MaxHealth / 2) - 1);
        var lowHealthOutgoing = ResolveDamage(world, enemy, 100f, medic);
        Assert.Equal(130f, lowHealthOutgoing.DamageAfterOutgoingModifiers);

        medic.ForceSetHealth(medic.MaxHealth / 2);
        var exactHalfIncoming = ResolveDamage(world, medic, 50f, enemy);
        Assert.Equal(50, exactHalfIncoming.AppliedHealthDamage);

        medic.ForceSetHealth((medic.MaxHealth / 2) - 1);
        var lowHealthIncoming = ResolveDamage(world, medic, 50f, enemy);
        Assert.Equal(35, lowHealthIncoming.AppliedHealthDamage);
    }

    [Fact]
    public void CombatMedicAndSpikedVestResistanceStackMultiplicatively()
    {
        var world = CreateMedicWorld();
        var medic = world.LocalPlayer;
        var enemy = AddHeavyPlayer(world, slot: 2, PlayerTeam.Blue);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Medic.CombatMedic, LastToDiePerkIds.Medic.SpikedVest]));
        medic.ForceSetHealth((medic.MaxHealth / 2) - 1);

        var resolution = ResolveDamage(world, medic, 50f, enemy);

        Assert.Equal(30, resolution.AppliedHealthDamage);
        Assert.Equal(30f, resolution.DamageAfterIncomingModifiers);
    }

    [Fact]
    public void StoicScalesFromZeroToFiftyPercentUsingHeldUbercharge()
    {
        var world = CreateMedicWorld();
        var medic = world.LocalPlayer;
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Medic.Stoic]));

        Assert.Equal(0f, InvokeLastToDieEvasionChance(world, medic));
        medic.AddMedicUberCharge(PlayerEntity.MedicUberMaxCharge / 2f);
        Assert.Equal(0.25f, InvokeLastToDieEvasionChance(world, medic), precision: 5);
        medic.AddMedicUberCharge(PlayerEntity.MedicUberMaxCharge / 2f);
        Assert.Equal(0.5f, InvokeLastToDieEvasionChance(world, medic), precision: 5);
    }

    [Fact]
    public void SpikedVestReflectsThirtyPercentOfPostResistanceHealthDamageWithRemainder()
    {
        var world = CreateMedicWorld();
        var medic = world.LocalPlayer;
        var enemy = AddHeavyPlayer(world, slot: 2, PlayerTeam.Blue);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Medic.SpikedVest]));

        var first = ResolveDamage(
            world,
            medic,
            100f,
            enemy,
            PlayerDamageTraits.CanReflect);
        Assert.Equal(85, first.AppliedHealthDamage);
        Assert.Equal(enemy.MaxHealth - 25, enemy.Health);

        medic.ForceSetHealth(medic.MaxHealth);
        var second = ResolveDamage(
            world,
            medic,
            100f,
            enemy,
            PlayerDamageTraits.CanReflect);
        Assert.Equal(85, second.AppliedHealthDamage);
        Assert.Equal(enemy.MaxHealth - 51, enemy.Health);
    }

    [Fact]
    public void SpikedVestReflectionCompletesAttackerDeathLifecycle()
    {
        var world = CreateMedicWorld();
        var medic = world.LocalPlayer;
        var enemy = AddHeavyPlayer(world, slot: 2, PlayerTeam.Blue);
        enemy.ForceSetHealth(10);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Medic.SpikedVest]));

        _ = ResolveDamage(world, medic, 100f, enemy, PlayerDamageTraits.CanReflect);

        Assert.False(enemy.IsAlive);
        Assert.Equal(1, medic.Kills);
    }

    [Fact]
    public void SpikedVestDoesNotReflectPeriodicDamage()
    {
        var world = CreateMedicWorld();
        var medic = world.LocalPlayer;
        var enemy = AddHeavyPlayer(world, slot: 2, PlayerTeam.Blue);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Medic.SpikedVest]));

        _ = ResolveDamage(
            world,
            medic,
            10f,
            enemy,
            PlayerDamageTraits.CanReflect | PlayerDamageTraits.Periodic);

        Assert.Equal(enemy.MaxHealth, enemy.Health);
    }

    [Fact]
    public void IronWillAppliesExactTwoPointFiveTimesPassiveRegenerationAndPredictsItsRemainder()
    {
        var world = CreateMedicWorld();
        var medic = world.LocalPlayer;
        medic.ForceSetHealth(10);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Medic.IronWill]));

        AdvanceSourceTicks(medic, PlayerEntity.MedicPassiveRegenIntervalSourceTicks);
        Assert.Equal(17, medic.Health);

        var predictionState = medic.CapturePredictionState();
        var predictionShadow = new PlayerEntity(99, CharacterClassCatalog.Medic, "Prediction Medic");
        predictionShadow.RestorePredictionState(predictionState);
        var fullHealthResetShadow = new PlayerEntity(100, CharacterClassCatalog.Medic, "Reset Medic");
        fullHealthResetShadow.RestorePredictionState(predictionState);
        AdvanceSourceTicks(medic, PlayerEntity.MedicPassiveRegenIntervalSourceTicks);
        AdvanceSourceTicks(predictionShadow, PlayerEntity.MedicPassiveRegenIntervalSourceTicks);

        Assert.Equal(25, medic.Health);
        Assert.Equal(medic.Health, predictionShadow.Health);

        fullHealthResetShadow.ForceSetHealth(fullHealthResetShadow.MaxHealth);
        AdvanceSourceTicks(fullHealthResetShadow, 1);
        fullHealthResetShadow.ForceSetHealth(10);
        AdvanceSourceTicks(
            fullHealthResetShadow,
            PlayerEntity.MedicPassiveRegenIntervalSourceTicks - 1);
        Assert.Equal(17, fullHealthResetShadow.Health);
    }

    [Fact]
    public void VitalityAcquisitionPreservesMissingHealthAndIsIdempotent()
    {
        var world = CreateMedicWorld();
        var medic = world.LocalPlayer;
        medic.ForceSetHealth(medic.MaxHealth - 50);

        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Medic.VitalityTrinket]));
        Assert.Equal(CharacterClassCatalog.Medic.MaxHealth + 75, medic.MaxHealth);
        Assert.Equal(medic.MaxHealth - 50, medic.Health);

        var healthAfterAcquisition = medic.Health;
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Medic.VitalityTrinket]));
        Assert.Equal(healthAfterAcquisition, medic.Health);
    }

    [Fact]
    public void FieldCommanderAllowsOnlyItsUberedMedicOwnerToCapture()
    {
        var world = CreateMedicWorld();
        var medic = world.LocalPlayer;
        medic.FillMedicUberCharge();
        Assert.True(medic.TryStartMedicUber());
        Assert.False(world.CanPlayerCaptureControlPointsWhileUbered(medic));

        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Medic.FieldCommander]));

        Assert.True(world.CanPlayerCaptureControlPointsWhileUbered(medic));
    }

    [Fact]
    public void FieldCommanderAllowsUberedMedicToAdvanceNeutralControlPointCapture()
    {
        var withoutPerk = CreateFieldCommanderCaptureWorld(fieldCommanderEnabled: false);
        var blockedPoint = Assert.Single(withoutPerk.ControlPoints);
        withoutPerk.LocalPlayer.FillMedicUberCharge();
        Assert.True(withoutPerk.LocalPlayer.TryStartMedicUber());
        withoutPerk.AdvanceOneTick();

        Assert.Equal(0f, blockedPoint.CappingTicks);
        Assert.Null(blockedPoint.CappingTeam);

        var withPerk = CreateFieldCommanderCaptureWorld(fieldCommanderEnabled: true);
        var advancingPoint = Assert.Single(withPerk.ControlPoints);
        withPerk.LocalPlayer.FillMedicUberCharge();
        Assert.True(withPerk.LocalPlayer.TryStartMedicUber());
        withPerk.AdvanceOneTick();

        Assert.True(advancingPoint.CappingTicks > 0f);
        Assert.Equal(PlayerTeam.Red, advancingPoint.CappingTeam);
        Assert.True(advancingPoint.RedCappers > 0);
    }

    [Fact]
    public void KritzHealNeedleHealsTeammateExactlyOnce()
    {
        var world = CreateMedicWorld();
        var target = AddHeavyTeammate(world, slot: 2);
        target.ForceSetHealth(target.MaxHealth - 60);
        var needle = new MedicHealNeedleProjectileEntity(
            100,
            PlayerTeam.Red,
            world.LocalPlayer.Id,
            0f,
            0f,
            0f,
            0f);

        InvokeMedicHealNeedleTeammateHit(world, world.LocalPlayer, target, needle);

        Assert.Equal(target.MaxHealth - 30, target.Health);
    }

    [Fact]
    public void ExsanguinationUsesOnlyALiveValidatedMedigunLink()
    {
        var world = CreateMedicWorld();
        var medic = world.LocalPlayer;
        var teammate = AddHeavyTeammate(world, slot: 2);
        var linkedEnemy = AddHeavyPlayer(world, slot: 3, PlayerTeam.Blue);
        var unlinkedEnemy = AddHeavyPlayer(world, slot: 4, PlayerTeam.Blue);
        medic.TeleportTo(0f, 0f);
        teammate.TeleportTo(10f, 0f);
        linkedEnemy.TeleportTo(20f, 0f);
        unlinkedEnemy.TeleportTo(30f, 0f);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Medic.Exsanguination]));
        medic.SetMedicHealingTarget(teammate);

        _ = ResolveDamage(
            world,
            linkedEnemy,
            10f,
            teammate,
            PlayerDamageTraits.CanApplyOnHitEffects);

        var linkedStatuses = world.GetLastToDieStatusEffects(linkedEnemy.Id);
        Assert.Contains(
            linkedStatuses,
            status => status.Id == LastToDieStatusEffectIds.MedicExsanguinationBleed
                && status.DamagePerSecond == 2f
                && status.RemainingTicks == world.Config.TicksPerSecond * 3);
        Assert.Contains(
            linkedStatuses,
            status => status.Id == LastToDieStatusEffectIds.MedicExsanguinationSlow
                && status.MovementSpeedMultiplier == 0.8f
                && status.RemainingTicks == world.Config.TicksPerSecond * 3);

        medic.ClearMedicHealingTarget();
        _ = ResolveDamage(
            world,
            unlinkedEnemy,
            10f,
            teammate,
            PlayerDamageTraits.CanApplyOnHitEffects);
        Assert.Empty(world.GetLastToDieStatusEffects(unlinkedEnemy.Id));

        medic.SetMedicHealingTarget(linkedEnemy);
        _ = ResolveDamage(
            world,
            unlinkedEnemy,
            10f,
            medic,
            PlayerDamageTraits.CanApplyOnHitEffects);
        Assert.Empty(world.GetLastToDieStatusEffects(unlinkedEnemy.Id));

        teammate.TeleportTo(400f, 0f);
        medic.SetMedicHealingTarget(teammate);
        _ = ResolveDamage(
            world,
            unlinkedEnemy,
            10f,
            medic,
            PlayerDamageTraits.CanApplyOnHitEffects);
        Assert.Empty(world.GetLastToDieStatusEffects(unlinkedEnemy.Id));

        teammate.TeleportTo(10f, 0f);
        teammate.ForceSetHealth(0);
        medic.SetMedicHealingTarget(teammate);
        _ = ResolveDamage(
            world,
            unlinkedEnemy,
            10f,
            medic,
            PlayerDamageTraits.CanApplyOnHitEffects);
        Assert.Empty(world.GetLastToDieStatusEffects(unlinkedEnemy.Id));

        teammate.ForceSetHealth(teammate.MaxHealth);
        medic.ForceSetHealth(0);
        medic.SetMedicHealingTarget(teammate);
        _ = ResolveDamage(
            world,
            unlinkedEnemy,
            10f,
            teammate,
            PlayerDamageTraits.CanApplyOnHitEffects);
        Assert.Empty(world.GetLastToDieStatusEffects(unlinkedEnemy.Id));
    }

    [Fact]
    public void ExsanguinationRejectsAMedigunLinkBlockedBySolidGeometry()
    {
        var world = CreateMedicLineOfSightWorld();
        var medic = world.LocalPlayer;
        var teammate = AddHeavyTeammate(world, slot: 2);
        var enemy = AddHeavyPlayer(world, slot: 3, PlayerTeam.Blue);
        medic.TeleportTo(100f, 100f);
        teammate.TeleportTo(300f, 100f);
        enemy.TeleportTo(350f, 100f);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Medic.Exsanguination]));
        medic.SetMedicHealingTarget(teammate);

        _ = ResolveDamage(
            world,
            enemy,
            10f,
            teammate,
            PlayerDamageTraits.CanApplyOnHitEffects);

        Assert.Empty(world.GetLastToDieStatusEffects(enemy.Id));
    }

    [Fact]
    public void ExsanguinationAppliesWhenTheLinkedMedicDealsDamage()
    {
        var world = CreateMedicWorld();
        var medic = world.LocalPlayer;
        var teammate = AddHeavyTeammate(world, slot: 2);
        var enemy = AddHeavyPlayer(world, slot: 3, PlayerTeam.Blue);
        medic.TeleportTo(0f, 0f);
        teammate.TeleportTo(10f, 0f);
        enemy.TeleportTo(20f, 0f);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Medic.Exsanguination]));
        medic.SetMedicHealingTarget(teammate);

        _ = ResolveDamage(
            world,
            enemy,
            10f,
            medic,
            PlayerDamageTraits.CanApplyOnHitEffects);

        var statuses = world.GetLastToDieStatusEffects(enemy.Id);
        Assert.Contains(statuses, status => status.Id == LastToDieStatusEffectIds.MedicExsanguinationBleed);
        Assert.Contains(statuses, status => status.Id == LastToDieStatusEffectIds.MedicExsanguinationSlow);
        Assert.Equal(-1, Assert.Single(world.PendingDamageEvents).AssistedByPlayerId);
    }

    [Fact]
    public void ExsanguinationRefreshUsesTheDeterministicHitTimeMedicAfterDisconnect()
    {
        var world = CreateWorld(PlayerClass.Heavy);
        var attacker = world.LocalPlayer;
        var lowerSlotMedic = AddNetworkPlayer(world, slot: 2, PlayerClass.Medic, PlayerTeam.Red);
        var higherSlotMedic = AddNetworkPlayer(world, slot: 3, PlayerClass.Medic, PlayerTeam.Red);
        var enemy = AddHeavyPlayer(world, slot: 4, PlayerTeam.Blue);
        attacker.TeleportTo(10f, 0f);
        lowerSlotMedic.TeleportTo(0f, 0f);
        higherSlotMedic.TeleportTo(20f, 0f);
        enemy.TeleportTo(30f, 0f);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            3,
            [LastToDiePerkIds.Medic.Exsanguination]));
        lowerSlotMedic.SetMedicHealingTarget(attacker);
        higherSlotMedic.SetMedicHealingTarget(attacker);

        _ = ResolveDamage(
            world,
            enemy,
            1f,
            attacker,
            PlayerDamageTraits.CanApplyOnHitEffects);
        var firstDamageEvent = Assert.Single(world.DrainPendingDamageEvents());
        Assert.Equal(higherSlotMedic.Id, firstDamageEvent.AssistedByPlayerId);

        for (var tick = 0; tick < 10; tick += 1)
        {
            world.AdvanceOneTick();
        }

        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            2,
            [LastToDiePerkIds.Medic.Exsanguination]));
        attacker.TeleportTo(10f, 0f);
        lowerSlotMedic.TeleportTo(0f, 0f);
        higherSlotMedic.TeleportTo(20f, 0f);
        enemy.TeleportTo(30f, 0f);
        lowerSlotMedic.SetMedicHealingTarget(attacker);
        higherSlotMedic.SetMedicHealingTarget(attacker);
        Assert.Same(lowerSlotMedic, InvokeResolveExsanguinationMedic(world, attacker));
        _ = ResolveDamage(
            world,
            enemy,
            1f,
            attacker,
            PlayerDamageTraits.CanApplyOnHitEffects);
        var refreshedDamageEvent = Assert.Single(world.DrainPendingDamageEvents());
        Assert.Equal(lowerSlotMedic.Id, refreshedDamageEvent.AssistedByPlayerId);
        var refreshedStatuses = world.GetLastToDieStatusEffects(enemy.Id);
        Assert.Equal(2, refreshedStatuses.Count);
        Assert.All(
            refreshedStatuses,
            status => Assert.Equal(world.Config.TicksPerSecond * 3, status.RemainingTicks));

        var disconnectedMedicId = lowerSlotMedic.Id;
        Assert.True(world.TryReleaseNetworkPlayerSlot(2));
        for (var tick = 0; tick < 20; tick += 1)
        {
            world.AdvanceOneTick();
        }

        var statusTicks = world.PendingDamageEvents
            .Where(damageEvent => damageEvent.Flags.HasFlag(DamageEventFlags.StatusTick))
            .ToArray();
        Assert.NotEmpty(statusTicks);
        Assert.All(statusTicks, statusTick =>
        {
            Assert.Equal(disconnectedMedicId, statusTick.AssistedByPlayerId);
            Assert.NotEqual(higherSlotMedic.Id, statusTick.AssistedByPlayerId);
        });
    }

    [Fact]
    public void ExsanguinationBleedKeepsMedicAssistAfterLinkBreakWithoutRecursing()
    {
        var world = CreateMedicWorld();
        var medic = world.LocalPlayer;
        var teammate = AddHeavyTeammate(world, slot: 2);
        var enemy = AddHeavyPlayer(world, slot: 3, PlayerTeam.Blue);
        medic.TeleportTo(0f, 0f);
        teammate.TeleportTo(10f, 0f);
        enemy.TeleportTo(20f, 0f);
        enemy.ForceSetHealth(6);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Medic.Exsanguination]));
        medic.SetMedicHealingTarget(teammate);

        _ = ResolveDamage(
            world,
            enemy,
            1f,
            teammate,
            PlayerDamageTraits.CanApplyOnHitEffects);
        var directDamageEvent = Assert.Single(world.DrainPendingDamageEvents());
        Assert.Equal(medic.Id, directDamageEvent.AssistedByPlayerId);
        world.ForceKillLocalPlayer();
        for (var tick = 0; tick < (world.Config.TicksPerSecond * 3) + 1; tick += 1)
        {
            world.AdvanceOneTick();
        }

        Assert.False(enemy.IsAlive);
        Assert.Equal(1, teammate.Kills);
        Assert.Equal(1, medic.Assists);
        var statusTickEvents = world.PendingDamageEvents
            .Where(damageEvent => damageEvent.Flags.HasFlag(DamageEventFlags.StatusTick))
            .ToArray();
        Assert.NotEmpty(statusTickEvents);
        Assert.All(
            statusTickEvents,
            damageEvent => Assert.Equal(medic.Id, damageEvent.AssistedByPlayerId));
        Assert.Empty(world.GetLastToDieStatusEffects(enemy.Id));
    }

    [Fact]
    public void ExsanguinationBleedKeepsPinnedMedicScoreCreditAfterTeamChange()
    {
        var world = CreateMedicWorld();
        var medic = world.LocalPlayer;
        var teammate = AddHeavyTeammate(world, slot: 2);
        var enemy = AddHeavyPlayer(world, slot: 3, PlayerTeam.Blue);
        medic.TeleportTo(0f, 0f);
        teammate.TeleportTo(10f, 0f);
        enemy.TeleportTo(20f, 0f);
        enemy.ForceSetHealth(3);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Medic.Exsanguination]));
        medic.SetMedicHealingTarget(teammate);

        _ = ResolveDamage(
            world,
            enemy,
            1f,
            teammate,
            PlayerDamageTraits.CanApplyOnHitEffects);
        _ = world.DrainPendingDamageEvents();
        Assert.True(world.TrySetNetworkPlayerTeam(
            SimulationWorld.LocalPlayerSlot,
            PlayerTeam.Blue,
            respawnLivePlayerImmediately: true));
        for (var tick = 0; tick < (world.Config.TicksPerSecond * 3) + 1; tick += 1)
        {
            world.AdvanceOneTick();
        }

        Assert.False(enemy.IsAlive);
        Assert.Equal(1, teammate.Kills);
        Assert.Equal(1, medic.Assists);
        Assert.All(
            world.PendingDamageEvents.Where(damageEvent => damageEvent.Flags.HasFlag(DamageEventFlags.StatusTick)),
            damageEvent => Assert.Equal(medic.Id, damageEvent.AssistedByPlayerId));
    }

    private static SimulationWorld CreateMedicWorld()
    {
        return CreateWorld(PlayerClass.Medic);
    }

    private static SimulationWorld CreateWorld(PlayerClass localPlayerClass)
    {
        var world = new SimulationWorld();
        world.PrepareLocalPlayerJoin();
        world.CompleteLocalPlayerJoin(localPlayerClass);
        return world;
    }

    private static SimulationWorld CreateMedicLineOfSightWorld()
    {
        var world = new SimulationWorld(new SimulationConfig
        {
            EnableLocalDummies = false,
            EnableEnemyTrainingDummy = false,
            EnableFriendlySupportDummy = false,
        });
        world.CombatTestSetLevel(new SimpleLevel(
            name: "ltd_medic_link_los_test",
            mode: GameModeKind.CaptureTheFlag,
            bounds: new WorldBounds(500f, 400f),
            mapScale: 1f,
            backgroundAssetName: null,
            mapAreaIndex: 1,
            mapAreaCount: 1,
            localSpawn: new SpawnPoint(100f, 100f),
            redSpawns: [new SpawnPoint(100f, 100f)],
            blueSpawns: [new SpawnPoint(350f, 100f)],
            intelBases: [],
            roomObjects: [],
            floorY: 400f,
            solids: [new LevelSolid(190f, 0f, 20f, 350f)],
            importedFromSource: false));
        world.PrepareLocalPlayerJoin();
        world.CompleteLocalPlayerJoin(PlayerClass.Medic);
        return world;
    }

    private static SimulationWorld CreateFieldCommanderCaptureWorld(bool fieldCommanderEnabled)
    {
        const float pointX = 320f;
        const float pointY = 240f;
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        world.CombatTestSetLevel(new SimpleLevel(
            name: "ltd_field_commander_capture_test",
            mode: GameModeKind.ControlPoint,
            bounds: new WorldBounds(640f, 480f),
            mapScale: 1f,
            backgroundAssetName: null,
            mapAreaIndex: 1,
            mapAreaCount: 1,
            localSpawn: new SpawnPoint(64f, 64f),
            redSpawns: [new SpawnPoint(64f, 64f)],
            blueSpawns: [new SpawnPoint(576f, 64f)],
            intelBases: [],
            roomObjects:
            [
                new RoomObjectMarker(
                    RoomObjectType.ControlPoint,
                    pointX - 21f,
                    pointY - 21f,
                    42f,
                    42f,
                    "ControlPointNeutralS",
                    SourceName: "ControlPoint1"),
                new RoomObjectMarker(
                    RoomObjectType.CaptureZone,
                    pointX - 100f,
                    pointY - 90f,
                    200f,
                    180f,
                    string.Empty,
                    SourceName: "CaptureZone"),
            ],
            floorY: 320f,
            solids: [new LevelSolid(0f, 320f, 640f, 160f)],
            importedFromSource: false));
        world.PrepareLocalPlayerJoin();
        Assert.True(world.TrySetNetworkPlayerTeam(
            SimulationWorld.LocalPlayerSlot,
            PlayerTeam.Red,
            respawnLivePlayerImmediately: true));
        world.CompleteLocalPlayerJoin(PlayerClass.Medic);
        if (fieldCommanderEnabled)
        {
            Assert.True(world.TryConfigureLastToDiePlayerBuild(
                SimulationWorld.LocalPlayerSlot,
                [LastToDiePerkIds.Medic.FieldCommander]));
        }

        world.LocalPlayer.TeleportTo(pointX, pointY);
        return world;
    }

    private static PlayerEntity AddHeavyTeammate(SimulationWorld world, byte slot)
    {
        return AddHeavyPlayer(world, slot, PlayerTeam.Red);
    }

    private static PlayerEntity AddHeavyPlayer(
        SimulationWorld world,
        byte slot,
        PlayerTeam team)
    {
        return AddNetworkPlayer(world, slot, PlayerClass.Heavy, team);
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

    private static void InvokeApplyMedicHealing(
        SimulationWorld world,
        PlayerEntity medic,
        PlayerEntity target)
    {
        var method = typeof(SimulationWorld).GetMethod(
            "ApplyMedicHealing",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        _ = method!.Invoke(world, [medic, target]);
    }

    private static float InvokeMedicHealingMultiplier(
        SimulationWorld world,
        PlayerEntity medic,
        PlayerEntity target)
    {
        var method = typeof(SimulationWorld).GetMethod(
            "GetLastToDieMedicHealingMultiplier",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return (float)method!.Invoke(world, [medic, target])!;
    }

    private static float InvokeLastToDieEvasionChance(
        SimulationWorld world,
        PlayerEntity target)
    {
        var method = typeof(SimulationWorld).GetMethod(
            "GetLastToDieEvasionChance",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return (float)method!.Invoke(world, [target])!;
    }

    private static void InvokeRefreshMedicLinkProjections(SimulationWorld world)
    {
        var method = typeof(SimulationWorld).GetMethod(
            "RefreshLastToDieMedicLinkProjections",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        _ = method!.Invoke(world, null);
    }

    private static int InvokeExperimentalReloadMultiplier(PlayerEntity player, int ticks)
    {
        var method = typeof(PlayerEntity).GetMethod(
            "ApplyExperimentalReloadMultiplier",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return (int)method!.Invoke(player, [ticks])!;
    }

    private static void SetPlayerTimer(PlayerEntity player, string propertyName, int ticks)
    {
        var property = typeof(PlayerEntity).GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        property!.SetValue(player, ticks);
    }

    private static void SetWorldFrame(SimulationWorld world, long frame)
    {
        var property = typeof(SimulationWorld).GetProperty(
            nameof(SimulationWorld.Frame),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        property!.SetValue(world, frame);
    }

    private static PlayerEntity? InvokeResolveExsanguinationMedic(
        SimulationWorld world,
        PlayerEntity attacker)
    {
        var method = typeof(SimulationWorld).GetMethod(
            "TryResolveLastToDieExsanguinationMedic",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        object?[] arguments = [attacker, null];
        return (bool)method!.Invoke(world, arguments)!
            ? Assert.IsType<PlayerEntity>(arguments[1])
            : null;
    }

    private static void InvokeMedicHealNeedleTeammateHit(
        SimulationWorld world,
        PlayerEntity medic,
        PlayerEntity target,
        MedicHealNeedleProjectileEntity needle)
    {
        var method = typeof(SimulationWorld).GetMethod(
            "ApplyMedicHealNeedleTeammateHit",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        _ = method!.Invoke(world, [medic, target, needle]);
    }

    private static PlayerDamageResolution ResolveDamage(
        SimulationWorld world,
        PlayerEntity target,
        float damage,
        PlayerEntity attacker,
        PlayerDamageTraits traits = PlayerDamageTraits.None)
    {
        return world.ResolvePlayerDamage(
            target,
            new PlayerDamageRequest(
                PlayerDamageApplicationKind.Instant,
                damage,
                attacker,
                PlayerEntity.SpyDamageRevealAlpha,
                DamageEventFlags.None,
                traits,
                AllowOsmosisHealOwnedSentries: false,
                new PlayerDamageUmbrellaOptions(AllowBlock: false)));
    }

    private static void AdvanceSourceTicks(PlayerEntity player, int ticks)
    {
        for (var tick = 0; tick < ticks; tick += 1)
        {
            player.AdvanceTickState(default, 1d / LegacyMovementModel.SourceTicksPerSecond);
        }
    }
}
