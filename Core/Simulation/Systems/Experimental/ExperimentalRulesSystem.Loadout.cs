namespace OpenGarrison.Core;

internal sealed partial class ExperimentalRulesSystem
{
    internal void ConfigureExperimentalGameplaySettings(ExperimentalGameplaySettings settings)
    {
        _host.ExperimentalGameplaySettings = settings ?? new ExperimentalGameplaySettings();
        if (!_host.ExperimentalGameplaySettings.EnableRage)
        {
            _host.CombatRuntime.RageEnemyHumiliationTicksRemaining = 0;
            _host.LocalPlayer.ClearRageState();
        }

        if (!_host.ExperimentalGameplaySettings.EnableEnemyHealthPackDrops)
        {
            _host.Pickups.ClearTemporaryHealthPacks();
        }
        if (!_host.ExperimentalGameplaySettings.EnableEnemyDroppedWeapons)
        {
            _host.Pickups.ClearDroppedWeapons();
        }

        SyncExperimentalGameplayLoadouts();
    }

    private void SyncExperimentalGameplayLoadouts()
    {
        for (var index = 0; index < SimulationConstants.NetworkPlayerSlots.Count; index += 1)
        {
            var slot = SimulationConstants.NetworkPlayerSlots[index];
            if (_host.NetworkPlayers.IsNetworkPlayerEnabled(slot) && _host.NetworkPlayers.TryGetNetworkPlayer(slot, out var player))
            {
                SyncExperimentalGameplayLoadout(slot, player);
            }
        }
    }

    internal void SyncExperimentalGameplayLoadout(byte slot, PlayerEntity player)
    {
        var hasLastToDieProfile = _host.LastToDieRules.TryGetLastToDieLegacyGameplaySettings(
            slot,
            out var lastToDieSettings);
        var settings = hasLastToDieProfile
            ? lastToDieSettings
            : _host.ExperimentalGameplaySettings;
        if (slot != SimulationConstants.LocalPlayerSlot && !hasLastToDieProfile)
        {
            player.SetExperimentalDemoknightEnabled(false);
            player.SetExperimentalPassiveMovementSpeedMultiplier(global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultPassiveMovementSpeedMultiplier);
            player.SetExperimentalJumpHeightMultiplier(global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultPassiveJumpHeightMultiplier);
            player.SetExperimentalBonusAirJumps(0);
            player.SetExperimentalDemoknightSwordRangeMultiplier(global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultDemoknightSwordRangeMultiplier);
            player.SetExperimentalDemoknightSwordBaseDamage(global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultDemoknightSwordBaseDamage);
            player.SetExperimentalDemoknightSwordDamageMultiplier(global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultDemoknightSwordDamageMultiplier);
            player.SetExperimentalDemoknightSwordCooldownMultiplier(global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultDemoknightSwordCooldownMultiplier);
            player.SetExperimentalDemoknightChargeRechargeMultiplier(global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultDemoknightChargeRechargeMultiplier);
            player.SetExperimentalSoldierAmmoRegeneratesWhileSwappedOut(false);
            player.SetExperimentalSelfDamageHealing(false);
            player.SetExperimentalReloadSpeedMultiplier(global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultReloadSpeedMultiplier);
            player.SetExperimentalDemoknightChargeFullControlEnabled(false);
            player.ConfigureExperimentalDemoknightPostRageRegeneration(0f);
            player.StartExperimentalDemoknightPostRageRegeneration(0);
            player.SetExperimentalOffhandWeapon(
                GameplaySecondaryWeaponResolver.Resolve(player, allowSoldierShotgun: false, allowSoldierShotgunLtd: false));
            if (player.ClassId == PlayerClass.Soldier)
            {
                player.SetAcquiredWeapon(null);
            }
            _host.ServerTuning.ApplyNetworkPlayerMaxHealthOverride(slot, player, refillHealth: false);
            return;
        }

        player.SetExperimentalDemoknightEnabled(
            settings.EnableDemoknightKit
            && player.ClassId == PlayerClass.Demoman);
        player.SetExperimentalPassiveMovementSpeedMultiplier(settings.PassiveMovementSpeedMultiplier);
        player.SetExperimentalJumpHeightMultiplier(settings.PassiveJumpHeightMultiplier);
        player.SetExperimentalBonusAirJumps(settings.PassiveBonusAirJumps);
        player.SetExperimentalDemoknightSwordRangeMultiplier(settings.DemoknightSwordRangeMultiplier);
        player.SetExperimentalDemoknightSwordBaseDamage(settings.DemoknightSwordBaseDamage);
        player.SetExperimentalDemoknightSwordDamageMultiplier(settings.DemoknightSwordDamageMultiplier);
        player.SetExperimentalDemoknightSwordCooldownMultiplier(settings.DemoknightSwordCooldownMultiplier);
        player.SetExperimentalDemoknightChargeRechargeMultiplier(settings.DemoknightChargeRechargeMultiplier);
        player.SetExperimentalSoldierAmmoRegeneratesWhileSwappedOut(
            settings.EnableSoldierAmmoRegeneratesWhileSwappedOut
            && player.ClassId == PlayerClass.Soldier);
        player.SetExperimentalSelfDamageHealing(
            settings.EnableSelfDamageHealing
            && player.ClassId == PlayerClass.Soldier);
        player.SetExperimentalReloadSpeedMultiplier(
            player.ClassId == PlayerClass.Soldier
                ? settings.ReloadSpeedMultiplierValue
                : global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultReloadSpeedMultiplier);
        player.SetExperimentalDemoknightChargeFullControlEnabled(
            settings.EnableDemoknightFullControlDuringCharge
            && player.ClassId == PlayerClass.Demoman);
        if (settings.EnableDemoknightPostRageRegeneration
            && player.ClassId == PlayerClass.Demoman)
        {
            player.ConfigureExperimentalDemoknightPostRageRegeneration(
                global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultDemoknightPostRageRegenerationPerSecond);
        }
        else
        {
            player.ConfigureExperimentalDemoknightPostRageRegeneration(0f);
            player.StartExperimentalDemoknightPostRageRegeneration(0);
        }
        player.SetExperimentalOffhandWeapon(
            GameplaySecondaryWeaponResolver.Resolve(
                player,
                allowSoldierShotgun: settings.EnableSoldierShotgunSecondaryWeapon,
                allowSoldierShotgunLtd: settings.EnableSoldierShotgunLtdPerk));
        if (player.ClassId == PlayerClass.Soldier
            && !settings.EnableEnemyDroppedWeapons)
        {
            player.SetAcquiredWeapon(null);
        }

        if (player.ClassId == PlayerClass.Engineer)
        {
            // Legacy Engineer perks alter metal capacity and alternate
            // weapon presentation. Apply those profile-owned fields during
            // the same synchronization pass as the rest of the loadout.
            ApplyExperimentalEngineerPassivePlayerEffects(player);
        }

        _host.ServerTuning.ApplyNetworkPlayerMaxHealthOverride(slot, player, refillHealth: false);
    }
}
