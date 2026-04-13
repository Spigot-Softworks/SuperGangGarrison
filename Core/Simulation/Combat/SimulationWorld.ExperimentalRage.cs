using System;
using System.Collections.Generic;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private int _experimentalRageEnemyHumiliationTicksRemaining;
    private readonly List<QueuedExperimentalRocketBurst> _experimentalQueuedRocketBursts = new();

    private readonly record struct QueuedExperimentalRocketBurst(
        int TicksRemaining,
        int OwnerId,
        float X,
        float Y,
        float Speed,
        float DirectionRadians,
        RocketCombatDefinition? RocketCombat,
        float DirectHitHealAmount,
        bool CanGrantExperimentalInstantReloadOnHit,
        float KnockbackScale,
        bool CanIgniteTargets,
        bool EnableStingerTracking,
        string? KillFeedWeaponSpriteNameOverride);

    private int GetExperimentalRageDurationTicks()
    {
        return Math.Max(1, (int)MathF.Round(Config.TicksPerSecond * ExperimentalGameplaySettings.RageDurationSeconds));
    }

    private int GetExperimentalDemoknightPostRageRegenerationDurationTicks()
    {
        return Math.Max(1, (int)MathF.Round(Config.TicksPerSecond * global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultDemoknightPostRageRegenerationDurationSeconds));
    }

    private static float GetExperimentalDemoknightPostRageRegenerationPerSecond()
    {
        return global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultDemoknightPostRageRegenerationPerSecond;
    }

    private int GetExperimentalGhostDashDurationTicks()
    {
        return Math.Max(1, (int)MathF.Round(Config.TicksPerSecond * global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultGhostDashDurationSeconds));
    }

    private int GetExperimentalGhostDashCooldownTicks()
    {
        return Math.Max(1, (int)MathF.Round(Config.TicksPerSecond * global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultGhostDashCooldownSeconds));
    }

    private static float GetExperimentalGhostDashImpulse()
    {
        return 75f;
    }

    private int GetExperimentalFinalRocketBurstDelayTicks()
    {
        return Math.Max(1, (int)MathF.Round(Config.TicksPerSecond * 0.25f));
    }

    private void QueueExperimentalSoldierFinalRocketBurst(
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
        if (!ExperimentalGameplaySettings.EnableSoldierFinalClipRocketBurst
            || !IsExperimentalPracticePowerOwner(owner)
            || owner.ClassId != PlayerClass.Soldier)
        {
            return;
        }

        _experimentalQueuedRocketBursts.Add(new QueuedExperimentalRocketBurst(
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
            && ExperimentalGameplaySettings.EnableRage
            && IsExperimentalPracticePowerOwner(player)
            && (player.ClassId == PlayerClass.Soldier
                || player.IsExperimentalDemoknightEnabled);
    }

    private bool TryHandleExperimentalRageActivation(PlayerEntity player)
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

        player.RefreshUber();
        if (ExperimentalGameplaySettings.EnableDemoknightPostRageRegeneration
            && player.IsExperimentalDemoknightEnabled)
        {
            player.ConfigureExperimentalDemoknightPostRageRegeneration(GetExperimentalDemoknightPostRageRegenerationPerSecond());
            player.StartExperimentalDemoknightPostRageRegeneration(GetExperimentalDemoknightPostRageRegenerationDurationTicks());
        }

        _experimentalRageEnemyHumiliationTicksRemaining = Math.Max(
            _experimentalRageEnemyHumiliationTicksRemaining,
            durationTicks);
        return true;
    }

    private void ApplyExperimentalRageEffects()
    {
        if (!ExperimentalGameplaySettings.EnableRage)
        {
            _experimentalRageEnemyHumiliationTicksRemaining = 0;
            return;
        }

        foreach (var player in EnumerateSimulatedPlayers())
        {
            if (player.IsRaging)
            {
                player.RefreshUber();
            }
        }
    }

    private void AdvanceExperimentalRageState()
    {
        if (_experimentalRageEnemyHumiliationTicksRemaining > 0)
        {
            _experimentalRageEnemyHumiliationTicksRemaining -= 1;
            if (_experimentalRageEnemyHumiliationTicksRemaining < 0)
            {
                _experimentalRageEnemyHumiliationTicksRemaining = 0;
            }
        }

        foreach (var player in EnumerateSimulatedPlayers())
        {
            player.AdvanceRageState();
        }

        for (var index = _experimentalQueuedRocketBursts.Count - 1; index >= 0; index -= 1)
        {
            var queuedBurst = _experimentalQueuedRocketBursts[index];
            if (queuedBurst.TicksRemaining > 1)
            {
                _experimentalQueuedRocketBursts[index] = queuedBurst with { TicksRemaining = queuedBurst.TicksRemaining - 1 };
                continue;
            }

            _experimentalQueuedRocketBursts.RemoveAt(index);
            var owner = FindPlayerById(queuedBurst.OwnerId);
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

            SpawnRocket(
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

    private bool IsExperimentalRageHumiliationActiveForPlayer(PlayerEntity player)
    {
        return ExperimentalGameplaySettings.EnableRage
            && _experimentalRageEnemyHumiliationTicksRemaining > 0
            && !ReferenceEquals(player, LocalPlayer)
            && player.Team != LocalPlayer.Team;
    }
}
