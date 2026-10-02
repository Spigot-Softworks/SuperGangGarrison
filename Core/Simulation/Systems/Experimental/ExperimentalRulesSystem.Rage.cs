using System;
using System.Collections.Generic;

namespace OpenGarrison.Core;

internal sealed partial class ExperimentalRulesSystem
{

    private int GetExperimentalRageDurationTicks()
    {
        return Math.Max(1, (int)MathF.Round(_host.Config.TicksPerSecond * ExperimentalGameplaySettings.RageDurationSeconds));
    }

    private int GetExperimentalDemoknightPostRageRegenerationDurationTicks()
    {
        return Math.Max(1, (int)MathF.Round(_host.Config.TicksPerSecond * global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultDemoknightPostRageRegenerationDurationSeconds));
    }

    private static float GetExperimentalDemoknightPostRageRegenerationPerSecond()
    {
        return global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultDemoknightPostRageRegenerationPerSecond;
    }

    internal int GetExperimentalGhostDashDurationTicks()
    {
        return Math.Max(1, (int)MathF.Round(_host.Config.TicksPerSecond * global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultGhostDashDurationSeconds));
    }

    internal int GetExperimentalGhostDashCooldownTicks()
    {
        return Math.Max(1, (int)MathF.Round(_host.Config.TicksPerSecond * global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultGhostDashCooldownSeconds));
    }

    internal static float GetExperimentalGhostDashImpulse()
    {
        return 75f;
    }

    internal int GetHeavyGhostDashDurationTicks()
    {
        return Math.Max(1, (int)MathF.Round(_host.Config.TicksPerSecond * global::OpenGarrison.Core.ExperimentalGameplaySettings.HeavyGhostDashDurationSeconds));
    }

    internal int GetHeavyGhostDashMovementDurationTicks()
    {
        return Math.Max(1, (int)MathF.Round(_host.Config.TicksPerSecond * global::OpenGarrison.Core.ExperimentalGameplaySettings.HeavyGhostDashMovementDurationSeconds));
    }

    internal int GetHeavyGhostDashCooldownTicks()
    {
        return Math.Max(1, (int)MathF.Round(_host.Config.TicksPerSecond * global::OpenGarrison.Core.ExperimentalGameplaySettings.HeavyGhostDashCooldownSeconds));
    }

    internal static float GetHeavyGhostDashImpulse()
    {
        return GetExperimentalGhostDashImpulse() * global::OpenGarrison.Core.ExperimentalGameplaySettings.HeavyGhostDashImpulseScale;
    }

    private int GetExperimentalFinalRocketBurstDelayTicks()
    {
        return Math.Max(1, (int)MathF.Round(_host.Config.TicksPerSecond * 0.25f));
    }

    internal void QueueExperimentalSoldierFinalRocketBurst(
        PlayerEntity owner,
        float x,
        float y,
        float speed,
        float directionRadians,
        RocketCombatDefinition? rocketCombat,
        float directHitHealAmount,
        bool canGrantExperimentalInstantReloadOnHit,
        float knockbackScale,
        bool canIgniteTargets,
        bool enableStingerTracking,
        string? killFeedWeaponSpriteNameOverride)
    {
        if (!_host.GetLastToDieGameplaySettings(owner).EnableSoldierFinalClipRocketBurst
            || !IsExperimentalPracticePowerOwner(owner)
            || owner.ClassId != PlayerClass.Soldier)
        {
            return;
        }

        _host.CombatRuntime.QueuedRocketBursts.Add(new QueuedExperimentalRocketBurst(
            GetExperimentalFinalRocketBurstDelayTicks(),
            owner.Id,
            x,
            y,
            speed,
            directionRadians,
            rocketCombat,
            directHitHealAmount,
            canGrantExperimentalInstantReloadOnHit,
            knockbackScale,
            canIgniteTargets,
            enableStingerTracking,
            killFeedWeaponSpriteNameOverride));
    }

    private bool CanUseExperimentalRage(PlayerEntity? player)
    {
        return player is not null
            && _host.GetLastToDieGameplaySettings(player).EnableRage
            && IsExperimentalPracticePowerOwner(player)
            && (player.ClassId == PlayerClass.Soldier
                || player.ClassId == PlayerClass.Engineer
                || player.IsExperimentalDemoknightEnabled
                || player.ClassId == PlayerClass.Spy
                || player.ClassId == PlayerClass.Medic
                || player.ClassId == PlayerClass.Sniper);
    }

    internal bool TryHandleExperimentalRageActivation(PlayerEntity player)
    {
        if (!CanUseExperimentalRage(player))
        {
            return false;
        }

        var durationTicks = GetExperimentalRageDurationTicks();
        if (!player.TryStartRage(durationTicks))
        {
            return false;
        }

        if (!player.IsCarryingIntel)
        {
            player.RefreshUber();
        }
        if (_host.GetLastToDieGameplaySettings(player).EnableDemoknightPostRageRegeneration
            && player.IsExperimentalDemoknightEnabled)
        {
            player.ConfigureExperimentalDemoknightPostRageRegeneration(GetExperimentalDemoknightPostRageRegenerationPerSecond());
            player.StartExperimentalDemoknightPostRageRegeneration(GetExperimentalDemoknightPostRageRegenerationDurationTicks());
        }

        _host.CombatRuntime.RageEnemyHumiliationTicksRemaining = Math.Max(
            _host.CombatRuntime.RageEnemyHumiliationTicksRemaining,
            durationTicks);
        return true;
    }

    internal void ApplyExperimentalRageEffects()
    {
        if (!_host.IsLastToDieGameplaySettingEnabled(settings => settings.EnableRage))
        {
            _host.CombatRuntime.RageEnemyHumiliationTicksRemaining = 0;
            return;
        }

        foreach (var player in _host.EnumerateSimulatedPlayers())
        {
            if (player.IsRaging && !player.IsCarryingIntel)
            {
                player.RefreshUber();
            }
        }
    }

    internal void AdvanceExperimentalRageState()
    {
        if (_host.CombatRuntime.RageEnemyHumiliationTicksRemaining > 0)
        {
            _host.CombatRuntime.RageEnemyHumiliationTicksRemaining -= 1;
            if (_host.CombatRuntime.RageEnemyHumiliationTicksRemaining < 0)
            {
                _host.CombatRuntime.RageEnemyHumiliationTicksRemaining = 0;
            }
        }

        foreach (var player in _host.EnumerateSimulatedPlayers())
        {
            player.AdvanceRageState();
        }

        for (var index = _host.CombatRuntime.QueuedRocketBursts.Count - 1; index >= 0; index -= 1)
        {
            var queuedBurst = _host.CombatRuntime.QueuedRocketBursts[index];
            if (queuedBurst.TicksRemaining > 1)
            {
                _host.CombatRuntime.QueuedRocketBursts[index] = queuedBurst with { TicksRemaining = queuedBurst.TicksRemaining - 1 };
                continue;
            }

            _host.CombatRuntime.QueuedRocketBursts.RemoveAt(index);
            var owner = _host.FindPlayerById(queuedBurst.OwnerId);
            if (owner is null || !owner.IsAlive)
            {
                continue;
            }

            var burstCombat = queuedBurst.RocketCombat is null
                ? null
                : queuedBurst.RocketCombat with
                {
                    DirectHitDamage = (int)MathF.Ceiling(queuedBurst.RocketCombat.DirectHitDamage * global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultSoldierFinalRocketDamageMultiplier),
                    ExplosionDamage = queuedBurst.RocketCombat.ExplosionDamage * global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultSoldierFinalRocketDamageMultiplier,
                };

            _host.SpawnRocket(
                owner,
                queuedBurst.X,
                queuedBurst.Y,
                queuedBurst.Speed,
                queuedBurst.DirectionRadians,
                burstCombat,
                queuedBurst.DirectHitHealAmount,
                explodeImmediately: false,
                canGrantExperimentalInstantReloadOnHit: queuedBurst.CanGrantExperimentalInstantReloadOnHit,
                knockbackScale: queuedBurst.KnockbackScale,
                canIgniteTargets: queuedBurst.CanIgniteTargets,
                enableExperimentalStingerTracking: queuedBurst.EnableStingerTracking,
                killFeedWeaponSpriteNameOverride: queuedBurst.KillFeedWeaponSpriteNameOverride);
        }
    }

    internal bool IsExperimentalRageHumiliationActiveForPlayer(PlayerEntity player)
    {
        return _host.IsLastToDieGameplaySettingEnabled(settings => settings.EnableRage)
            && _host.CombatRuntime.RageEnemyHumiliationTicksRemaining > 0
            && !ReferenceEquals(player, _host.LocalPlayer)
            && player.Team != _host.LocalPlayer.Team;
    }
}
